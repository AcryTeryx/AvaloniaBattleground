# Avalonia Battleground

A 2D top-down **Arena Fighter** for Windows and Linux desktop, built in C# with
[AvaloniaUI](https://avaloniaui.net/). Four players fight a 2v2, best-of-three
match in a circular arena. One player hosts the match simulation; the other
three connect over the local network.

This is a focused, multiplayer-only MVP: no bots, no accounts, no matchmaking.
See [`docs/PRD.md`](docs/PRD.md) for the full product spec and
[`CONTEXT.md`](CONTEXT.md) for the domain glossary that the code and docs share.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer (the projects
  target `net10.0`).
- A desktop environment (the app uses Avalonia's desktop backend).
- **Audio (optional):** sound plays by shelling out to a system audio player.
  The game runs silently if none is found.
  - Linux: one of `pw-play`, `paplay`, or `aplay` on your `PATH`.
  - Windows: `pwsh.exe` or `powershell.exe` (uses `System.Media.SoundPlayer`).

## Build and test

```bash
# Restore + build everything
dotnet build AvaloniaBattleground.sln

# Run the automated test suite (Core simulation, lobby rules,
# networking protocol, and view-model behavior)
dotnet test
```

## Run the game

```bash
dotnet run --project src/AvaloniaBattleground.App
```

### Playing a match

The MVP is multiplayer-only and needs **exactly four connected clients**, so you
launch four instances. The simplest setup is four windows on one machine:

1. In the first window, set your display name and click **Host Match**. Note the
   **port** shown on the lobby screen (the host listens on an ephemeral port).
2. In the other three windows, click **Join Match**, enter `127.0.0.1` and the
   host's port, then connect.
3. In the lobby, each client picks a **Team** (Red/Blue) and a **Role**. Each
   team needs exactly **one Melee and one Ranged** fighter.
4. Once four clients are connected with valid team roles, the host's **Start
   Match** button unlocks.

To play across machines, use the host's LAN IP (shown on the lobby screen)
instead of `127.0.0.1`. The host port must be reachable (same LAN, VPN, or a
manual port-forward) — there is no matchmaking or NAT traversal.

### Controls (keyboard only)

| Action          | Key         |
| --------------- | ----------- |
| Move            | `W A S D`   |
| Aim             | Arrow keys  |
| Primary attack  | `Space`     |
| Universal dash  | `Left Shift`|
| Role ability    | `Left Ctrl` |

- **Melee Fighter** (200 HP): frontal sword strike + area slash.
- **Ranged Fighter** (100 HP): single arrow shot + five-arrow cone volley.

## Project layout

| Project                          | Responsibility |
| -------------------------------- | -------------- |
| `src/AvaloniaBattleground.Core`        | Pure domain + the deterministic, host-authoritative 60 Hz match simulation. No UI or networking dependencies. |
| `src/AvaloniaBattleground.Networking`  | TCP listen-server transport, lobby protocol, and session lifecycle. |
| `src/AvaloniaBattleground.App`         | Avalonia desktop shell: menus, lobby, HUD, the custom-drawn game surface, and procedural audio. |
| `tests/AvaloniaBattleground.Tests`     | xUnit tests for simulation, lobby rules, networking, and view-model behavior. |

Architectural decisions are recorded in [`docs/adr/`](docs/adr/).
