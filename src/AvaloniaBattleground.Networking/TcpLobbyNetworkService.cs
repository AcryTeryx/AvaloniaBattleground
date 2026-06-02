using AvaloniaBattleground.Core;
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
                messageBuffer.Position = 0;
                return await JsonSerializer.DeserializeAsync<WireMessage>(
                    messageBuffer,
                    JsonOptions,
                    cancellationToken);
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
        private int _nextClientId = 2;
        private LobbySnapshot _snapshot;

        public HostLobbySession(
            TcpListener listener,
            IReadOnlyList<string> shareableAddresses,
            int port,
            string displayName)
        {
            _listener = listener;
            ShareableAddresses = shareableAddresses;
            Port = port;
            _snapshot = new LobbySnapshot([new LobbyClientInfo(1, displayName, true)]);

            _ = AcceptClientsAsync();
        }

        public event EventHandler<LobbySnapshot>? SnapshotChanged;

        public IReadOnlyList<string> ShareableAddresses { get; }

        public int Port { get; }

        public LobbySnapshot Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _snapshot;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
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

        private int AddClient(TcpClient tcpClient, string displayName)
        {
            LobbySnapshot snapshot;
            int clientId;

            lock (_syncRoot)
            {
                clientId = _nextClientId++;
                _connectedClients.Add(new ConnectedClient(clientId, tcpClient));
                _snapshot = _snapshot with
                {
                    Clients = [.. _snapshot.Clients, new LobbyClientInfo(clientId, displayName, false)],
                };
                snapshot = _snapshot;
            }

            SnapshotChanged?.Invoke(this, snapshot);
            return clientId;
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
                    // Disconnect outcome handling belongs to a later issue.
                }
                catch (SocketException)
                {
                    // Disconnect outcome handling belongs to a later issue.
                }
                catch (ObjectDisposedException)
                {
                    // Disconnect outcome handling belongs to a later issue.
                }
            }
        }
    }

    private sealed class ClientLobbySession : IClientLobbySession
    {
        private readonly CancellationTokenSource _stopping = new();
        private readonly TcpClient _tcpClient;
        private LobbySnapshot _snapshot;

        public ClientLobbySession(TcpClient tcpClient, LobbySnapshot initialSnapshot)
        {
            _tcpClient = tcpClient;
            _snapshot = initialSnapshot;

            _ = ReadSnapshotsAsync();
        }

        public event EventHandler<LobbySnapshot>? SnapshotChanged;

        public LobbySnapshot Snapshot => _snapshot;

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            _tcpClient.Dispose();
            _stopping.Dispose();
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
                        return;
                    }

                    if (message.MessageType != WireMessageTypes.LobbySnapshot ||
                        message.Clients is null)
                    {
                        continue;
                    }

                    _snapshot = new LobbySnapshot(message.Clients);
                    SnapshotChanged?.Invoke(this, _snapshot);
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
    }

    private sealed record ConnectedClient(int ClientId, TcpClient TcpClient);

    private static class WireMessageTypes
    {
        public const string JoinAccepted = nameof(JoinAccepted);
        public const string JoinRejected = nameof(JoinRejected);
        public const string JoinRequest = nameof(JoinRequest);
        public const string LobbySnapshot = nameof(LobbySnapshot);
    }

    private sealed record WireMessage(
        string MessageType,
        int ProtocolVersion,
        string? DisplayName = null,
        int? ClientId = null,
        string? FailureReason = null,
        string? FailureMessage = null,
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

        public static WireMessage LobbySnapshot(IReadOnlyList<LobbyClientInfo> clients)
        {
            return new WireMessage(
                WireMessageTypes.LobbySnapshot,
                LobbyProtocol.CurrentVersion,
                Clients: clients);
        }
    }
}
