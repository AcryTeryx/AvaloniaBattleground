using AvaloniaBattleground.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AvaloniaBattleground.Networking;

public sealed class TcpLobbyNetworkService : ILobbyNetworkService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultJoinTimeout = TimeSpan.FromSeconds(5);

    public Task<IHostLobbySession> StartHostAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        IHostLobbySession session = new HostLobbySession(
            listener,
            GetShareableAddresses(),
            port,
            LocalProfileStore.NormalizeDisplayName(displayName));

        return Task.FromResult(session);
    }

    public async Task<JoinLobbyResult> JoinAsync(
        JoinLobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(request.HostAddress, out var hostAddress) ||
            hostAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return JoinLobbyResult.Failure(
                JoinFailureReason.InvalidAddress,
                "Enter a valid IPv4 host address.");
        }

        if (request.Port is <= 0 or > IPEndPoint.MaxPort)
        {
            return JoinLobbyResult.Failure(
                JoinFailureReason.InvalidPort,
                "Enter a port from 1 to 65535.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? DefaultJoinTimeout);

        var tcpClient = new TcpClient(AddressFamily.InterNetwork);

        try
        {
            await tcpClient.ConnectAsync(hostAddress, request.Port, timeout.Token);
            var stream = tcpClient.GetStream();

            await WriteWireMessageAsync(
                stream,
                WireMessage.JoinRequest(
                    request.ProtocolVersion,
                    LocalProfileStore.NormalizeDisplayName(request.DisplayName)),
                timeout.Token);

            var response = await ReadWireMessageAsync(stream, timeout.Token);
            if (response is null)
            {
                tcpClient.Dispose();
                return JoinLobbyResult.Failure(
                    JoinFailureReason.ConnectionFailed,
                    "Could not connect to the hosted Lobby.");
            }

            if (response.MessageType == WireMessageTypes.JoinRejected)
            {
                tcpClient.Dispose();
                return JoinLobbyResult.Failure(
                    MapFailureReason(response.FailureReason),
                    response.FailureMessage ?? "Could not join the hosted Lobby.");
            }

            if (response.MessageType != WireMessageTypes.JoinAccepted ||
                response.ClientId is null ||
                response.Clients is null)
            {
                tcpClient.Dispose();
                return JoinLobbyResult.Failure(
                    JoinFailureReason.ConnectionFailed,
                    "The host sent an invalid Lobby response.");
            }

            var session = new ClientLobbySession(
                tcpClient,
                response.ClientId.Value,
                new LobbySnapshot(response.Clients));

            return JoinLobbyResult.Success(session);
        }
        catch (OperationCanceledException)
        {
            tcpClient.Dispose();
            return JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby before the join attempt timed out.");
        }
        catch (SocketException)
        {
            tcpClient.Dispose();
            return JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby.");
        }
        catch (IOException)
        {
            tcpClient.Dispose();
            return JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "Could not connect to the hosted Lobby.");
        }
        catch (JsonException)
        {
            tcpClient.Dispose();
            return JoinLobbyResult.Failure(
                JoinFailureReason.ConnectionFailed,
                "The host sent an invalid Lobby response.");
        }
    }

    private static IReadOnlyList<string> GetShareableAddresses()
    {
        string[] addresses;

        try
        {
            addresses = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct()
                .Order()
                .ToArray();
        }
        catch (SocketException)
        {
            addresses = [];
        }

        return addresses.Length == 0
            ? ["127.0.0.1"]
            : addresses;
    }

    private static JoinFailureReason MapFailureReason(string? failureReason)
    {
        return Enum.TryParse<JoinFailureReason>(failureReason, out var parsed)
            ? parsed
            : JoinFailureReason.ConnectionFailed;
    }

    private static async Task WriteWireMessageAsync(
        NetworkStream stream,
        WireMessage message,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, message, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<WireMessage?> ReadWireMessageAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var messageBuffer = new MemoryStream();
        var buffer = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return messageBuffer.Length == 0
                    ? null
                    : throw new JsonException("Partial wire message.");
            }

            if (buffer[0] == '\n')
            {
                cancellationToken.ThrowIfCancellationRequested();
                return JsonSerializer.Deserialize<WireMessage>(
                    messageBuffer.ToArray(),
                    JsonOptions);
            }

            messageBuffer.WriteByte(buffer[0]);
        }
    }

    private sealed class HostLobbySession : IHostLobbySession
    {
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<ConnectedClient> _connectedClients = [];
        private readonly TcpListener _listener;
        private readonly object _syncRoot = new();
        private MatchSimulation? _matchSimulation;
        private int _nextClientId = 2;
        private LobbyState _lobby;

        public HostLobbySession(
            TcpListener listener,
            IReadOnlyList<string> shareableAddresses,
            int port,
            string displayName)
        {
            _listener = listener;
            ShareableAddresses = shareableAddresses;
            Port = port;
            _lobby = new LobbyState([new LobbyClient(1, displayName, true)]);

            _ = AcceptClientsAsync();
        }

        public event EventHandler<LobbySnapshot>? SnapshotChanged;

        public event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

        public event EventHandler<LobbySessionEnded>? SessionEnded
        {
            add { }
            remove { }
        }

        public IReadOnlyList<string> ShareableAddresses { get; }

        public int Port { get; }

        public int LocalClientId => 1;

        public LobbySnapshot Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return LobbySnapshot.FromLobbyState(_lobby);
                }
            }
        }

        public MatchSnapshot? MatchSnapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _matchSimulation?.Snapshot;
                }
            }
        }

        public async Task<StartMatchResult> StartMatchAsync(CancellationToken cancellationToken = default)
        {
            MatchSnapshot snapshot;

            lock (_syncRoot)
            {
                if (_matchSimulation is not null)
                {
                    return StartMatchResult.Failure(
                        StartMatchFailureReason.AlreadyStarted,
                        "The Match has already started.");
                }

                if (!_lobby.StartEligibility.CanStart)
                {
                    return StartMatchResult.Failure(
                        StartMatchFailureReason.LobbyNotReady,
                        "The Lobby must have exactly four Clients and valid Team roles.");
                }

                _matchSimulation = MatchSimulation.Start(_lobby);
                snapshot = _matchSimulation.Snapshot;
            }

            MatchSnapshotChanged?.Invoke(this, snapshot);
            await BroadcastMatchSnapshotAsync(snapshot, cancellationToken);
            _ = RunMatchLoopAsync();

            return StartMatchResult.Success(snapshot);
        }

        public async Task<LobbySelectionResult> SelectTeamRoleAsync(
            Team team,
            FighterRole role,
            CancellationToken cancellationToken = default)
        {
            return await ApplySelectionAsync(
                new LobbySelection(1, team, role),
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_stopping.IsCancellationRequested)
            {
                await BroadcastSessionEndedAsync(new LobbySessionEnded(
                    LobbySessionEndReason.HostDisconnectEnd,
                    "Host Disconnect End: the host closed the game."));
            }

            await _stopping.CancelAsync();
            _listener.Stop();

            ConnectedClient[] connectedClients;
            lock (_syncRoot)
            {
                connectedClients = [.. _connectedClients];
                _connectedClients.Clear();
            }

            foreach (var client in connectedClients)
            {
                client.TcpClient.Dispose();
            }

            _stopping.Dispose();
        }

        private async Task AcceptClientsAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                TcpClient tcpClient;

                try
                {
                    tcpClient = await _listener.AcceptTcpClientAsync(_stopping.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = HandleClientJoinAsync(tcpClient);
            }
        }

        private async Task HandleClientJoinAsync(TcpClient tcpClient)
        {
            try
            {
                var stream = tcpClient.GetStream();
                var request = await ReadWireMessageAsync(stream, _stopping.Token);

                if (request is null || request.MessageType != WireMessageTypes.JoinRequest)
                {
                    tcpClient.Dispose();
                    return;
                }

                if (request.ProtocolVersion != LobbyProtocol.CurrentVersion)
                {
                    await WriteWireMessageAsync(
                        stream,
                        WireMessage.JoinRejected(
                            JoinFailureReason.ProtocolVersionMismatch,
                            "Client protocol version is incompatible with this host."),
                        _stopping.Token);
                    tcpClient.Dispose();
                    return;
                }

                var clientId = AddClient(
                    tcpClient,
                    LocalProfileStore.NormalizeDisplayName(request.DisplayName));
                var snapshot = Snapshot;

                await WriteWireMessageAsync(
                    stream,
                    WireMessage.JoinAccepted(clientId, snapshot.Clients),
                    _stopping.Token);
                await BroadcastSnapshotAsync(snapshot);
                _ = ReadClientMessagesAsync(clientId, tcpClient);
            }
            catch (OperationCanceledException)
            {
                tcpClient.Dispose();
            }
            catch (IOException)
            {
                tcpClient.Dispose();
            }
            catch (JsonException)
            {
                tcpClient.Dispose();
            }
        }

        private async Task ReadClientMessagesAsync(int clientId, TcpClient tcpClient)
        {
            try
            {
                while (!_stopping.IsCancellationRequested)
                {
                    try
                    {
                        var message = await ReadWireMessageAsync(
                            tcpClient.GetStream(),
                            _stopping.Token);

                        if (message is null)
                        {
                            return;
                        }

                        if (message.MessageType == WireMessageTypes.ClientDisconnected)
                        {
                            return;
                        }

                        if (message.MessageType == WireMessageTypes.PlayerInput &&
                            message.PlayerInput is not null)
                        {
                            SetPlayerInput(clientId, message.PlayerInput);
                            continue;
                        }

                        if (message.MessageType != WireMessageTypes.SelectionRequest ||
                            message.RequestId is null ||
                            message.Team is null ||
                            message.Role is null)
                        {
                            continue;
                        }

                        var result = await ApplySelectionAsync(
                            new LobbySelection(clientId, message.Team.Value, message.Role.Value),
                            _stopping.Token);

                        if (result.Succeeded)
                        {
                            await WriteWireMessageAsync(
                                tcpClient.GetStream(),
                                WireMessage.SelectionAccepted(
                                    message.RequestId.Value,
                                    LobbySnapshot.FromLobbyState(result.Lobby).Clients),
                                _stopping.Token);
                        }
                        else
                        {
                            await WriteWireMessageAsync(
                                tcpClient.GetStream(),
                                WireMessage.SelectionRejected(
                                    message.RequestId.Value,
                                    result.FailureReason ?? LobbySelectionFailureReason.UnknownClient,
                                    result.Message),
                                _stopping.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (IOException)
                    {
                        return;
                    }
                    catch (JsonException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                }
            }
            finally
            {
                await HandleClientDisconnectedAsync(clientId);
            }
        }

        private async Task HandleClientDisconnectedAsync(int clientId)
        {
            if (_stopping.IsCancellationRequested)
            {
                return;
            }

            ConnectedClient? disconnectedClient = null;
            LobbySnapshot? snapshot = null;
            MatchSnapshot? matchSnapshot = null;

            lock (_syncRoot)
            {
                var connectedClientIndex = _connectedClients.FindIndex(client =>
                    client.ClientId == clientId);
                if (connectedClientIndex < 0)
                {
                    return;
                }

                disconnectedClient = _connectedClients[connectedClientIndex];
                _connectedClients.RemoveAt(connectedClientIndex);

                if (_matchSimulation is null)
                {
                    _lobby = new LobbyState(
                        _lobby.Clients
                            .Where(client => client.ClientId != clientId)
                            .ToArray());
                    snapshot = LobbySnapshot.FromLobbyState(_lobby);
                }
                else
                {
                    _matchSimulation.CompleteMatchByDisconnectForfeit(clientId);
                    matchSnapshot = _matchSimulation.Snapshot;
                }
            }

            disconnectedClient.TcpClient.Dispose();

            if (snapshot is not null)
            {
                SnapshotChanged?.Invoke(this, snapshot);
                await BroadcastSnapshotAsync(snapshot);
            }

            if (matchSnapshot is not null)
            {
                MatchSnapshotChanged?.Invoke(this, matchSnapshot);
                await BroadcastMatchSnapshotAsync(matchSnapshot, _stopping.Token);
            }
        }

        public Task SendPlayerInputAsync(
            PlayerInput input,
            CancellationToken cancellationToken = default)
        {
            SetPlayerInput(LocalClientId, input);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private void SetPlayerInput(int clientId, PlayerInput input)
        {
            lock (_syncRoot)
            {
                _matchSimulation?.SetInput(clientId, input);
            }
        }

        private async Task RunMatchLoopAsync()
        {
            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(MatchRules.FixedDeltaSeconds));

            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(_stopping.Token))
                    {
                        return;
                    }

                    MatchSnapshot? snapshot;
                    lock (_syncRoot)
                    {
                        _matchSimulation?.Tick();
                        snapshot = _matchSimulation?.Snapshot;
                    }

                    if (snapshot is null)
                    {
                        return;
                    }

                    MatchSnapshotChanged?.Invoke(this, snapshot);
                    await BroadcastMatchSnapshotAsync(snapshot, _stopping.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private int AddClient(TcpClient tcpClient, string displayName)
        {
            LobbySnapshot snapshot;
            int clientId;

            lock (_syncRoot)
            {
                clientId = _nextClientId++;
                _connectedClients.Add(new ConnectedClient(clientId, tcpClient));
                _lobby = new LobbyState(
                    [.. _lobby.Clients, new LobbyClient(clientId, displayName, false)]);
                snapshot = LobbySnapshot.FromLobbyState(_lobby);
            }

            SnapshotChanged?.Invoke(this, snapshot);
            return clientId;
        }

        private async Task<LobbySelectionResult> ApplySelectionAsync(
            LobbySelection selection,
            CancellationToken cancellationToken)
        {
            LobbySelectionResult result;
            LobbySnapshot snapshot;

            lock (_syncRoot)
            {
                result = _lobby.ApplySelection(selection);
                if (result.Succeeded)
                {
                    _lobby = result.Lobby;
                }

                snapshot = LobbySnapshot.FromLobbyState(_lobby);
            }

            if (result.Succeeded)
            {
                SnapshotChanged?.Invoke(this, snapshot);
                await BroadcastSnapshotAsync(snapshot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        private async Task BroadcastSnapshotAsync(LobbySnapshot snapshot)
        {
            ConnectedClient[] connectedClients;
            lock (_syncRoot)
            {
                connectedClients = [.. _connectedClients];
            }

            foreach (var connectedClient in connectedClients)
            {
                try
                {
                    await WriteWireMessageAsync(
                        connectedClient.TcpClient.GetStream(),
                        WireMessage.LobbySnapshot(snapshot.Clients),
                        _stopping.Token);
                }
                catch (IOException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
                catch (SocketException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
                catch (ObjectDisposedException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
            }
        }

        private async Task BroadcastMatchSnapshotAsync(
            MatchSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            ConnectedClient[] connectedClients;
            lock (_syncRoot)
            {
                connectedClients = [.. _connectedClients];
            }

            foreach (var connectedClient in connectedClients)
            {
                try
                {
                    await WriteWireMessageAsync(
                        connectedClient.TcpClient.GetStream(),
                        WireMessage.MatchSnapshotMessage(snapshot),
                        cancellationToken);
                }
                catch (IOException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
                catch (SocketException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
                catch (ObjectDisposedException)
                {
                    await HandleClientDisconnectedAsync(connectedClient.ClientId);
                }
            }
        }

        private async Task BroadcastSessionEndedAsync(LobbySessionEnded sessionEnded)
        {
            ConnectedClient[] connectedClients;
            lock (_syncRoot)
            {
                connectedClients = [.. _connectedClients];
            }

            foreach (var connectedClient in connectedClients)
            {
                try
                {
                    await WriteWireMessageAsync(
                        connectedClient.TcpClient.GetStream(),
                        WireMessage.SessionEnded(sessionEnded),
                        CancellationToken.None);
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }

    private sealed class ClientLobbySession : IClientLobbySession
    {
        private readonly CancellationTokenSource _stopping = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<LobbySelectionResult>> _pendingSelections = new();
        private readonly TcpClient _tcpClient;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private MatchSnapshot? _matchSnapshot;
        private int _sessionEnded;
        private LobbySnapshot _snapshot;

        public ClientLobbySession(TcpClient tcpClient, int localClientId, LobbySnapshot initialSnapshot)
        {
            _tcpClient = tcpClient;
            LocalClientId = localClientId;
            _snapshot = initialSnapshot;

            _ = ReadSnapshotsAsync();
        }

        public event EventHandler<LobbySnapshot>? SnapshotChanged;

        public event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

        public event EventHandler<LobbySessionEnded>? SessionEnded;

        public int LocalClientId { get; }

        public LobbySnapshot Snapshot => _snapshot;

        public MatchSnapshot? MatchSnapshot => _matchSnapshot;

        public async Task<LobbySelectionResult> SelectTeamRoleAsync(
            Team team,
            FighterRole role,
            CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var completion = new TaskCompletionSource<LobbySelectionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingSelections.TryAdd(requestId, completion))
            {
                throw new InvalidOperationException("Selection request could not be tracked.");
            }

            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));

            try
            {
                await _writeLock.WaitAsync(cancellationToken);
                try
                {
                    await WriteWireMessageAsync(
                        _tcpClient.GetStream(),
                        WireMessage.SelectionRequest(requestId, team, role),
                        cancellationToken);
                }
                finally
                {
                    _writeLock.Release();
                }

                return await completion.Task;
            }
            catch (IOException)
            {
                return CreateHostDisconnectSelectionFailure();
            }
            catch (SocketException)
            {
                return CreateHostDisconnectSelectionFailure();
            }
            catch (ObjectDisposedException)
            {
                return CreateHostDisconnectSelectionFailure();
            }
            finally
            {
                _pendingSelections.TryRemove(requestId, out _);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await TrySendClientDisconnectedAsync();
            await _stopping.CancelAsync();
            try
            {
                _tcpClient.Client.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _tcpClient.Dispose();
            _writeLock.Dispose();
            _stopping.Dispose();
        }

        private async Task TrySendClientDisconnectedAsync()
        {
            try
            {
                await _writeLock.WaitAsync();
                try
                {
                    await WriteWireMessageAsync(
                        _tcpClient.GetStream(),
                        WireMessage.ClientDisconnected(),
                        CancellationToken.None);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public async Task SendPlayerInputAsync(
            PlayerInput input,
            CancellationToken cancellationToken = default)
        {
            var lockTaken = false;
            try
            {
                await _writeLock.WaitAsync(cancellationToken);
                lockTaken = true;

                await WriteWireMessageAsync(
                    _tcpClient.GetStream(),
                    WireMessage.PlayerInputMessage(input),
                    cancellationToken);
            }
            catch (IOException)
            {
                EndSessionForHostDisconnect();
            }
            catch (SocketException)
            {
                EndSessionForHostDisconnect();
            }
            catch (ObjectDisposedException)
            {
                EndSessionForHostDisconnect();
            }
            finally
            {
                if (lockTaken)
                {
                    _writeLock.Release();
                }
            }
        }

        private async Task ReadSnapshotsAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    var message = await ReadWireMessageAsync(
                        _tcpClient.GetStream(),
                        _stopping.Token);

                    if (message is null)
                    {
                        EndSessionForHostDisconnect();
                        return;
                    }

                    if (message.MessageType == WireMessageTypes.SessionEnded)
                    {
                        EndSession(new LobbySessionEnded(
                            MapSessionEndReason(message.FailureReason),
                            message.FailureMessage ?? "Host Disconnect End: the host disconnected."));
                        return;
                    }

                    if (message.MessageType == WireMessageTypes.LobbySnapshot &&
                        message.Clients is not null)
                    {
                        UpdateSnapshot(new LobbySnapshot(message.Clients));
                        continue;
                    }

                    if (message.MessageType == WireMessageTypes.MatchSnapshot &&
                        message.MatchSnapshot is not null)
                    {
                        UpdateMatchSnapshot(message.MatchSnapshot);
                        continue;
                    }

                    if (message.MessageType == WireMessageTypes.SelectionAccepted &&
                        message.RequestId is not null &&
                        message.Clients is not null)
                    {
                        var snapshot = new LobbySnapshot(message.Clients);
                        UpdateSnapshot(snapshot);
                        CompleteSelection(
                            message.RequestId.Value,
                            LobbySelectionResult.Success(snapshot.ToLobbyState()));
                        continue;
                    }

                    if (message.MessageType == WireMessageTypes.SelectionRejected &&
                        message.RequestId is not null)
                    {
                        CompleteSelection(
                            message.RequestId.Value,
                            LobbySelectionResult.Failure(
                                _snapshot.ToLobbyState(),
                                MapSelectionFailureReason(message.FailureReason),
                                message.FailureMessage ?? "Selection was rejected."));
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    EndSessionForHostDisconnect();
                    return;
                }
                catch (JsonException)
                {
                    EndSessionForHostDisconnect();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    EndSessionForHostDisconnect();
                    return;
                }
            }
        }

        private void UpdateSnapshot(LobbySnapshot snapshot)
        {
            _snapshot = snapshot;
            SnapshotChanged?.Invoke(this, _snapshot);
        }

        private void CompleteSelection(Guid requestId, LobbySelectionResult result)
        {
            if (_pendingSelections.TryRemove(requestId, out var completion))
            {
                completion.TrySetResult(result);
            }
        }

        private void UpdateMatchSnapshot(MatchSnapshot snapshot)
        {
            _matchSnapshot = snapshot;
            MatchSnapshotChanged?.Invoke(this, snapshot);
        }

        private void EndSessionForHostDisconnect()
        {
            EndSession(new LobbySessionEnded(
                LobbySessionEndReason.HostDisconnectEnd,
                "Host Disconnect End: the host disconnected or closed the game."));
        }

        private void EndSession(LobbySessionEnded sessionEnded)
        {
            if (_stopping.IsCancellationRequested ||
                Interlocked.Exchange(ref _sessionEnded, 1) == 1)
            {
                return;
            }

            foreach (var pendingSelection in _pendingSelections)
            {
                pendingSelection.Value.TrySetResult(
                    LobbySelectionResult.Failure(
                        _snapshot.ToLobbyState(),
                        LobbySelectionFailureReason.UnknownClient,
                        sessionEnded.Message));
            }

            SessionEnded?.Invoke(this, sessionEnded);
        }

        private LobbySelectionResult CreateHostDisconnectSelectionFailure()
        {
            var sessionEnded = new LobbySessionEnded(
                LobbySessionEndReason.HostDisconnectEnd,
                "Host Disconnect End: the host disconnected or closed the game.");

            EndSession(sessionEnded);

            return LobbySelectionResult.Failure(
                _snapshot.ToLobbyState(),
                LobbySelectionFailureReason.UnknownClient,
                sessionEnded.Message);
        }
    }

    private sealed record ConnectedClient(int ClientId, TcpClient TcpClient);

    private static class WireMessageTypes
    {
        public const string JoinAccepted = nameof(JoinAccepted);
        public const string JoinRejected = nameof(JoinRejected);
        public const string JoinRequest = nameof(JoinRequest);
        public const string ClientDisconnected = nameof(ClientDisconnected);
        public const string LobbySnapshot = nameof(LobbySnapshot);
        public const string MatchSnapshot = nameof(MatchSnapshot);
        public const string PlayerInput = nameof(PlayerInput);
        public const string SelectionAccepted = nameof(SelectionAccepted);
        public const string SelectionRejected = nameof(SelectionRejected);
        public const string SelectionRequest = nameof(SelectionRequest);
        public const string SessionEnded = nameof(SessionEnded);
    }

    private sealed record WireMessage(
        string MessageType,
        int ProtocolVersion,
        string? DisplayName = null,
        int? ClientId = null,
        Guid? RequestId = null,
        string? FailureReason = null,
        string? FailureMessage = null,
        Team? Team = null,
        FighterRole? Role = null,
        PlayerInput? PlayerInput = null,
        MatchSnapshot? MatchSnapshot = null,
        IReadOnlyList<LobbyClientInfo>? Clients = null)
    {
        public static WireMessage JoinRequest(int protocolVersion, string displayName)
        {
            return new WireMessage(
                WireMessageTypes.JoinRequest,
                protocolVersion,
                DisplayName: displayName);
        }

        public static WireMessage JoinAccepted(
            int clientId,
            IReadOnlyList<LobbyClientInfo> clients)
        {
            return new WireMessage(
                WireMessageTypes.JoinAccepted,
                LobbyProtocol.CurrentVersion,
                ClientId: clientId,
                Clients: clients);
        }

        public static WireMessage JoinRejected(
            JoinFailureReason failureReason,
            string failureMessage)
        {
            return new WireMessage(
                WireMessageTypes.JoinRejected,
                LobbyProtocol.CurrentVersion,
                FailureReason: failureReason.ToString(),
                FailureMessage: failureMessage);
        }

        public static WireMessage ClientDisconnected()
        {
            return new WireMessage(
                WireMessageTypes.ClientDisconnected,
                LobbyProtocol.CurrentVersion);
        }

        public static WireMessage LobbySnapshot(IReadOnlyList<LobbyClientInfo> clients)
        {
            return new WireMessage(
                WireMessageTypes.LobbySnapshot,
                LobbyProtocol.CurrentVersion,
                Clients: clients);
        }

        public static WireMessage SelectionRequest(Guid requestId, Team team, FighterRole role)
        {
            return new WireMessage(
                WireMessageTypes.SelectionRequest,
                LobbyProtocol.CurrentVersion,
                RequestId: requestId,
                Team: team,
                Role: role);
        }

        public static WireMessage SelectionAccepted(
            Guid requestId,
            IReadOnlyList<LobbyClientInfo> clients)
        {
            return new WireMessage(
                WireMessageTypes.SelectionAccepted,
                LobbyProtocol.CurrentVersion,
                RequestId: requestId,
                Clients: clients);
        }

        public static WireMessage SelectionRejected(
            Guid requestId,
            LobbySelectionFailureReason failureReason,
            string failureMessage)
        {
            return new WireMessage(
                WireMessageTypes.SelectionRejected,
                LobbyProtocol.CurrentVersion,
                RequestId: requestId,
                FailureReason: failureReason.ToString(),
                FailureMessage: failureMessage);
        }

        public static WireMessage PlayerInputMessage(PlayerInput playerInput)
        {
            return new WireMessage(
                WireMessageTypes.PlayerInput,
                LobbyProtocol.CurrentVersion,
                PlayerInput: playerInput);
        }

        public static WireMessage MatchSnapshotMessage(MatchSnapshot matchSnapshot)
        {
            return new WireMessage(
                WireMessageTypes.MatchSnapshot,
                LobbyProtocol.CurrentVersion,
                MatchSnapshot: matchSnapshot);
        }

        public static WireMessage SessionEnded(LobbySessionEnded sessionEnded)
        {
            return new WireMessage(
                WireMessageTypes.SessionEnded,
                LobbyProtocol.CurrentVersion,
                FailureReason: sessionEnded.Reason.ToString(),
                FailureMessage: sessionEnded.Message);
        }
    }

    private static LobbySelectionFailureReason MapSelectionFailureReason(string? failureReason)
    {
        return Enum.TryParse<LobbySelectionFailureReason>(failureReason, out var parsed)
            ? parsed
            : LobbySelectionFailureReason.UnknownClient;
    }

    private static LobbySessionEndReason MapSessionEndReason(string? sessionEndReason)
    {
        return Enum.TryParse<LobbySessionEndReason>(sessionEndReason, out var parsed)
            ? parsed
            : LobbySessionEndReason.HostDisconnectEnd;
    }
}
