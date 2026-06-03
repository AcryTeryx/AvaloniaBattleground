using AvaloniaBattleground.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AvaloniaBattleground.Networking;

public sealed class TcpLobbyNetworkService : ILobbyNetworkService
{
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
            var reader = new WireMessageReader(stream);

            await WireMessageWriter.WriteAsync(
                stream,
                WireMessage.JoinRequest(
                    request.ProtocolVersion,
                    LocalProfileStore.NormalizeDisplayName(request.DisplayName)),
                timeout.Token);

            var response = await reader.ReadAsync(timeout.Token);
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
                reader,
                response.ClientId.Value,
                new LobbyState(response.Clients));

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

    private sealed class HostLobbySession : IHostLobbySession
    {
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<ConnectedClient> _connectedClients = [];
        private readonly IMatchHost _matchHost = new MatchHost();
        private readonly TcpListener _listener;
        private readonly object _syncRoot = new();
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

        public event EventHandler<LobbyState>? SnapshotChanged;

        public event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

        public event EventHandler<LobbySessionEnded>? SessionEnded
        {
            add { }
            remove { }
        }

        public IReadOnlyList<string> ShareableAddresses { get; }

        public int Port { get; }

        public int LocalClientId => 1;

        public LobbyState Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lobby;
                }
            }
        }

        public MatchSnapshot? MatchSnapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _matchHost.Snapshot;
                }
            }
        }

        public async Task<StartMatchResult> StartMatchAsync(CancellationToken cancellationToken = default)
        {
            MatchSnapshot snapshot;
            StartMatchResult result;

            lock (_syncRoot)
            {
                result = _matchHost.TryStart(_lobby);
                if (!result.Succeeded)
                {
                    return result;
                }

                snapshot = result.MatchSnapshot!;
            }

            MatchSnapshotChanged?.Invoke(this, snapshot);
            await BroadcastMatchSnapshotAsync(snapshot, cancellationToken);
            _ = RunMatchLoopAsync();

            return result;
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
                var reader = new WireMessageReader(stream);
                var request = await reader.ReadAsync(_stopping.Token);

                if (request is null || request.MessageType != WireMessageTypes.JoinRequest)
                {
                    tcpClient.Dispose();
                    return;
                }

                if (request.ProtocolVersion != LobbyProtocol.CurrentVersion)
                {
                    await WireMessageWriter.WriteAsync(
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

                await WireMessageWriter.WriteAsync(
                    stream,
                    WireMessage.JoinAccepted(clientId, snapshot.Clients),
                    _stopping.Token);
                await BroadcastSnapshotAsync(snapshot);
                _ = ReadClientMessagesAsync(clientId, tcpClient, reader);
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

        private async Task ReadClientMessagesAsync(
            int clientId,
            TcpClient tcpClient,
            WireMessageReader reader)
        {
            try
            {
                while (!_stopping.IsCancellationRequested)
                {
                    try
                    {
                        var message = await reader.ReadAsync(_stopping.Token);

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
                            await WireMessageWriter.WriteAsync(
                                tcpClient.GetStream(),
                                WireMessage.SelectionAccepted(
                                    message.RequestId.Value,
                                    result.Lobby.Clients),
                                _stopping.Token);
                        }
                        else
                        {
                            await WireMessageWriter.WriteAsync(
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
            LobbyState? snapshot = null;
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

                if (!_matchHost.IsRunning)
                {
                    _lobby = new LobbyState(
                        _lobby.Clients
                            .Where(client => client.ClientId != clientId)
                            .ToArray());
                    snapshot = _lobby;
                }
                else
                {
                    matchSnapshot = _matchHost.HandleClientDisconnected(clientId);
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
                _matchHost.SetInput(clientId, input);
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
                        snapshot = _matchHost.Tick();
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
            LobbyState snapshot;
            int clientId;

            lock (_syncRoot)
            {
                clientId = _nextClientId++;
                _connectedClients.Add(new ConnectedClient(clientId, tcpClient));
                _lobby = new LobbyState(
                    [.. _lobby.Clients, new LobbyClient(clientId, displayName, false)]);
                snapshot = _lobby;
            }

            SnapshotChanged?.Invoke(this, snapshot);
            return clientId;
        }

        private async Task<LobbySelectionResult> ApplySelectionAsync(
            LobbySelection selection,
            CancellationToken cancellationToken)
        {
            LobbySelectionResult result;
            LobbyState snapshot;

            lock (_syncRoot)
            {
                result = _lobby.ApplySelection(selection);
                if (result.Succeeded)
                {
                    _lobby = result.Lobby;
                }

                snapshot = _lobby;
            }

            if (result.Succeeded)
            {
                SnapshotChanged?.Invoke(this, snapshot);
                await BroadcastSnapshotAsync(snapshot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        private async Task BroadcastSnapshotAsync(LobbyState snapshot)
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
                    await WireMessageWriter.WriteAsync(
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
                catch (InvalidOperationException)
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
                    await WireMessageWriter.WriteAsync(
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
                catch (InvalidOperationException)
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
                    await WireMessageWriter.WriteAsync(
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
                catch (InvalidOperationException)
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
        private readonly WireMessageReader _reader;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private MatchSnapshot? _matchSnapshot;
        private int _sessionEnded;
        private LobbyState _snapshot;

        public ClientLobbySession(
            TcpClient tcpClient,
            WireMessageReader reader,
            int localClientId,
            LobbyState initialSnapshot)
        {
            _tcpClient = tcpClient;
            _reader = reader;
            LocalClientId = localClientId;
            _snapshot = initialSnapshot;

            _ = ReadSnapshotsAsync();
        }

        public event EventHandler<LobbyState>? SnapshotChanged;

        public event EventHandler<MatchSnapshot>? MatchSnapshotChanged;

        public event EventHandler<LobbySessionEnded>? SessionEnded;

        public int LocalClientId { get; }

        public LobbyState Snapshot => _snapshot;

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
                    await WireMessageWriter.WriteAsync(
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
            catch (InvalidOperationException)
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
                    await WireMessageWriter.WriteAsync(
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
            catch (InvalidOperationException)
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

                await WireMessageWriter.WriteAsync(
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
            catch (InvalidOperationException)
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
                    var message = await _reader.ReadAsync(_stopping.Token);

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
                        UpdateSnapshot(new LobbyState(message.Clients));
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
                        var snapshot = new LobbyState(message.Clients);
                        UpdateSnapshot(snapshot);
                        CompleteSelection(
                            message.RequestId.Value,
                            LobbySelectionResult.Success(snapshot));
                        continue;
                    }

                    if (message.MessageType == WireMessageTypes.SelectionRejected &&
                        message.RequestId is not null)
                    {
                        CompleteSelection(
                            message.RequestId.Value,
                            LobbySelectionResult.Failure(
                                _snapshot,
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

        private void UpdateSnapshot(LobbyState snapshot)
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
                        _snapshot,
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
                _snapshot,
                LobbySelectionFailureReason.UnknownClient,
                sessionEnded.Message);
        }
    }

    private sealed record ConnectedClient(int ClientId, TcpClient TcpClient);

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
