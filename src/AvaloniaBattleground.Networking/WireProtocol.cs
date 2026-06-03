using AvaloniaBattleground.Core;
using System.Net.Sockets;
using System.Text.Json;

namespace AvaloniaBattleground.Networking;

internal static class WireMessageTypes
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

internal sealed record WireMessage(
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
    IReadOnlyList<LobbyClient>? Clients = null)
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
        IReadOnlyList<LobbyClient> clients)
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

    public static WireMessage LobbySnapshot(IReadOnlyList<LobbyClient> clients)
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
        IReadOnlyList<LobbyClient> clients)
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

internal static class WireMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(
        NetworkStream stream,
        WireMessage message,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, message, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}

// Reads newline-delimited JSON wire messages off a single connection. The
// read buffer is retained between calls so bytes that arrive after a
// message terminator (for example, a lobby snapshot the host sends right
// after the join response) are not lost. One reader must be reused for the
// whole lifetime of a connection.
internal sealed class WireMessageReader(NetworkStream stream)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _readBuffer = new byte[4096];
    private readonly MemoryStream _messageBuffer = new();
    private int _bufferStart;
    private int _bufferEnd;

    public async Task<WireMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        _messageBuffer.SetLength(0);

        while (true)
        {
            while (_bufferStart < _bufferEnd)
            {
                var current = _readBuffer[_bufferStart++];
                if (current == (byte)'\n')
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return JsonSerializer.Deserialize<WireMessage>(
                        _messageBuffer.ToArray(),
                        JsonOptions);
                }

                _messageBuffer.WriteByte(current);
            }

            _bufferStart = 0;
            _bufferEnd = await stream.ReadAsync(_readBuffer, cancellationToken);
            if (_bufferEnd == 0)
            {
                return _messageBuffer.Length == 0
                    ? null
                    : throw new JsonException("Partial wire message.");
            }
        }
    }
}
