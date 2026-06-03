# Avalonia Battleground PRD

## Problem Statement

The project needs a clear product and implementation specification for a Windows and Linux desktop 2D Arena Fighter built in C# with AvaloniaUI. The game should deliver a focused 4-player 2v2 hosted multiplayer experience without external visual assets, without central infrastructure, and without letting scope drift into matchmaking, accounts, bots, unsupported platform targets, or asset-heavy production.

The core challenge is to make a small, readable, networked combat game where four players on supported Windows or Linux desktop machines can host, join, choose valid team roles, fight best-of-3 rounds, and understand the outcome of each round and match.

## Solution

Build Avalonia Battleground as a Windows and Linux desktop Arena Fighter with a Host-Authoritative Listen Server network model. One player hosts the Match simulation, three other Clients connect by manual IP and port, and all Clients play in a Circular Arena using Keyboard-Only Controls.

Each Match contains exactly two Teams. Each Team must have one Melee Fighter and one Ranged Fighter. Players choose Team and role in the Lobby, then fight best-of-3 Rounds. Each Round lasts 90 seconds and is won by Team Elimination or, if time expires, by Health Tiebreaker.

The game uses Minimal Vector Combat Visuals rendered through a Custom Game Surface in Avalonia. Menus, Lobby, HUD, dialogs, and overlays use normal Avalonia UI controls. External audio assets are allowed for combat SFX, lobby music, battle music, and Kill Announcement.

## User Stories

1. As a player, I want to launch the game on Windows or Linux, so that I can play on a supported desktop platform.
2. As a player, I want a main menu with Host Match, Join Match, and Exit, so that I can quickly choose how to enter the game.
3. As a host player, I want to start a hosted Match from my machine, so that my friends can connect without a dedicated server.
4. As a host player, I want to see the host IP address and port, so that I can share connection details with other players.
5. As a joining player, I want to enter an IP address and port, so that I can connect to a hosted Match manually.
6. As a joining player, I want clear connection failure feedback, so that I know when the host address, port, or network route is wrong.
7. As a player, I want to set a Local Display Name, so that other players can identify me in the Lobby and Match.
8. As a player, I want my Local Display Name to persist locally, so that I do not need to re-enter it every launch.
9. As a player, I want to see all connected Clients in the Lobby, so that I know who has joined.
10. As a player, I want to choose my Team in the Lobby, so that I can coordinate with my partner.
11. As a player, I want to choose Melee Fighter or Ranged Fighter in the Lobby, so that I can play the role I prefer.
12. As a player, I want the Lobby to enforce one Melee Fighter and one Ranged Fighter per Team, so that Teams always satisfy the Role Constraint.
13. As a host player, I want the Match start action locked until exactly four Clients are connected, so that player-facing Matches always use the intended 2v2 format.
14. As a host player, I want the Match start action locked until both Teams satisfy the Role Constraint, so that invalid Team composition cannot enter combat.
15. As a player, I want to move with WASD, so that movement uses familiar keyboard controls.
16. As a player, I want to aim with the arrow keys, so that I can control attack direction without a mouse or gamepad.
17. As a player, I want Spacebar to trigger my primary attack, so that my main combat action has a simple dedicated key.
18. As a player, I want Left Shift to trigger Universal Dash, so that every Fighter has a small movement burst.
19. As a player, I want Left Ctrl to trigger my role ability, so that my Fighter has a distinct high-impact action.
20. As a Melee Fighter, I want 200 HP, so that I can survive longer while fighting at close range.
21. As a Ranged Fighter, I want 100 HP, so that my safer range comes with lower durability.
22. As a Melee Fighter, I want Melee Frontal Strike as my primary attack, so that I can damage enemies in front of me at close range.
23. As a Melee Fighter, I want Melee Area Slash as my role ability, so that I can punish nearby enemies all around me.
24. As a Ranged Fighter, I want Ranged Single Arrow Shot as my primary attack, so that I can pressure enemies from distance.
25. As a Ranged Fighter, I want Ranged Cone Volley as my role ability, so that I can threaten multiple enemies in a cone.
26. As a player hit by Ranged Cone Volley, I want to take damage only once from that volley, so that overlapping arrows do not multiply damage unfairly.
27. As a player, I want attacks and abilities to use cooldowns only, so that combat is readable without mana, stamina, or ammo.
28. As a player, I want enemy-only damage, so that I cannot accidentally damage my partner.
29. As a player, I want Fighters to pass through each other, so that movement stays fluid and body blocking is not part of the game.
30. As a player, I want the Match to happen in an open Circular Arena, so that the fight focuses on movement, aim, cooldowns, and spacing.
31. As a player, I want the Circular Arena edge to be a hard boundary, so that I cannot move or dash outside the combat space.
32. As a player, I want projectiles to disappear when they hit the Circular Arena boundary, so that off-arena projectiles do not linger.
33. As a player, I want to see health, cooldowns, timer, and round score, so that I can make informed combat decisions.
34. As a player, I want clear visual attack telegraphs and projectile feedback, so that I can read what is happening without external art assets.
35. As a player, I want hit flashes and death feedback, so that combat outcomes are immediately understandable.
36. As a defeated player, I want to enter Spectator State until the next Round, so that I can continue watching without respawning mid-Round.
37. As a player, I want a Round Result overlay, so that I know which Team won the Round and why.
38. As a player, I want a Match Result screen, so that I know which Team won the best-of-3 Match.
39. As a player, I want lobby music and battle music, so that menu and combat states feel distinct.
40. As a player, I want combat SFX, so that attacks, dashes, hits, deaths, and round events are easier to perceive.
41. As a player, I want a Kill Announcement when an enemy is defeated, so that important combat moments feel clear and satisfying.
42. As a non-host player, I want the game to show when the host disconnects, so that I understand why the Match ended.
43. As a host player, I want non-host disconnects to resolve cleanly, so that a lost connection does not leave the Match stuck.
44. As a playtester, I want four players to complete a 10-minute playtest without crashes, so that the MVP is stable enough to iterate.
45. As a developer, I want the host to own the Match truth, so that movement, hits, health, cooldowns, deaths, Round state, and scoring are consistent.
46. As a developer, I want a fixed 60 Hz host simulation, so that combat timing and network behavior are predictable.
47. As a developer, I want gameplay rendered through a Custom Game Surface, so that fighter, projectile, and attack rendering do not depend on per-entity UI controls.
48. As a developer, I want the visual design to use native drawing primitives, so that the game avoids external visual asset dependencies.

## Implementation Decisions

- Build the product as a Windows and Linux Desktop Product. macOS, mobile, WebAssembly, console, and broader platform support are out of scope.
- Use AvaloniaUI for the desktop app shell, menus, Lobby, HUD, dialogs, overlays, and Custom Game Surface hosting.
- Render gameplay through a Custom Game Surface rather than representing every Fighter, projectile, and attack as an independent UI control.
- Keep the game as a Multiplayer-Only MVP. No player-facing bots, training mode, or single-player mode are included.
- Use a Host-Authoritative Listen Server. One player's machine hosts the Match simulation; other Clients connect to it.
- Use Manual Address Join. Clients enter host IP address and port.
- Assume LAN, VPN, or manually port-forwarded networking. Matchmaking, relay servers, NAT traversal, lobby browser, and invite codes are excluded.
- Run a Fixed-Tick Host Simulation at 60 Hz.
- Treat the host as authoritative for movement, hits, health, cooldowns, deaths, Round state, scoring, and Match results.
- Clients send input commands or input state to the host and render host-provided Match state.
- End the Match for all Clients if the host disconnects.
- Trigger Disconnect Forfeit if a non-host Client disconnects during a Match.
- Require exactly four connected Clients before a player-facing Match can start.
- Enforce the Role Constraint in the Lobby: each Team must contain exactly one Melee Fighter and exactly one Ranged Fighter.
- Store only Local Display Name persistently.
- Use Keyboard-Only Controls: WASD movement, arrow-key aim, Spacebar primary attack, Left Shift Universal Dash, and Left Ctrl role ability.
- Do not support key rebinding, mouse aiming, or gamepad input.
- Structure the combat model around Fighters, Teams, Matches, Rounds, Role Kits, cooldowns, health, projectiles, attacks, abilities, and spectator state.
- Use Cooldown-Only Combat. No mana, stamina, ammo, or other spendable resources.
- Use Role Health: Melee Fighter starts with 200 HP, Ranged Fighter starts with 100 HP.
- Use Melee Frontal Strike as the Melee Fighter primary attack: 18 damage, 0.45s cooldown, short frontal arc in aim direction.
- Use Melee Area Slash as the Melee Fighter role ability: 35 damage, 5s cooldown, close-range circular AoE around the Fighter.
- Use Ranged Single Arrow Shot as the Ranged Fighter primary attack: 14 damage, 0.6s cooldown, straight projectile in aim direction.
- Use Ranged Cone Volley as the Ranged Fighter role ability: 24 damage, 6s cooldown, five arrows fired in a cone.
- Apply the Single-Hit Volley Rule to Ranged Cone Volley.
- Use Universal Dash for all Fighters: 2.5s cooldown, tiny distance, no damage, no invulnerability.
- Apply Enemy-Only Damage to all attacks and abilities.
- Use a single Circular Arena with no internal obstacles, pickups, or hazards.
- Apply a Hard Arena Boundary to Fighter movement and dash movement.
- Apply Projectile Boundary Removal when projectiles hit the Circular Arena edge.
- Use Pass-Through Fighters collision rules.
- Use Minimal Vector Combat Visuals: colored Fighter shapes, team outlines, aim direction, attack telegraphs, projectiles, AoE indicators, health bars, cooldown indicators, hit flashes, death feedback, and overlays.
- Include an MVP Audio Set with lobby music, battle music, combat SFX, lobby join/leave SFX, connection/error SFX, Round and Match SFX, and Kill Announcement.

Major modules to build:

- App shell module: owns application startup, screen navigation, and high-level state transitions.
- Local profile module: owns Local Display Name persistence.
- Lobby module: owns connected player list, Team selection, role selection, readiness/start eligibility, and Role Constraint validation.
- Match flow module: owns best-of-3 Match progression, Round start/end, timer, Team Elimination, Health Tiebreaker, Spectator State, and Match result.
- Simulation core module: owns deterministic host-side tick updates for movement, dash, cooldowns, attacks, projectiles, hit detection, health, death, and scoring.
- Combat rules module: owns Role Health, Role Kits, damage values, cooldown values, Enemy-Only Damage, Single-Hit Volley Rule, Pass-Through Fighters, Hard Arena Boundary, and Projectile Boundary Removal.
- Input module: maps Keyboard-Only Controls into normalized player input state or commands.
- Networking module: owns host startup, manual join, connection lifecycle, input messages, state snapshots, disconnect handling, and protocol versioning.
- Render module: owns Custom Game Surface drawing for arena, Fighters, projectiles, telegraphs, health, cooldowns, hit flashes, and overlays.
- Audio module: owns music switching, SFX playback, Kill Announcement, and volume-safe triggering.
- UI screens module: owns main menu, host setup, join screen, Lobby screen, Match screen HUD, Round result overlay, Match result screen, and connection/error dialogs.

Deep modules to keep isolated and testable:

- Simulation core, because it encapsulates the most gameplay behavior behind tick input and state output.
- Combat rules, because it can validate damage, cooldowns, collision rules, projectile rules, and win conditions without UI.
- Match flow, because it can verify Round and Match progression without rendering or networking.
- Lobby rules, because it can enforce Full Lobby Requirement and Role Constraint without Avalonia views.
- Network protocol, because serialization and message handling should be stable and testable without a running UI.

## Testing Decisions

Good tests should verify external behavior and domain outcomes rather than implementation details. Tests should describe what a player, host, Client, or Match observes: legal Lobby start conditions, combat results, cooldown availability, projectile removal, Round winners, disconnect outcomes, and state synchronization behavior.

There is no existing codebase or prior test suite in this workspace yet, so the first implementation should establish the test structure alongside the first deep modules.

Modules that should receive automated tests:

- Lobby rules: Full Lobby Requirement, Role Constraint, Team/role selection conflicts, and start eligibility.
- Match flow: best-of-3 progression, 90-second Round timeout, Team Elimination, Health Tiebreaker, Spectator State, and Match result.
- Combat rules: Role Health, cooldown gating, Melee Frontal Strike damage, Melee Area Slash damage, Ranged Single Arrow Shot damage, Ranged Cone Volley five-arrow behavior, Single-Hit Volley Rule, Enemy-Only Damage, and death transitions.
- Arena rules: Hard Arena Boundary, dash clamping at the Circular Arena edge, Projectile Boundary Removal, and Pass-Through Fighters.
- Fixed-tick simulation: movement over ticks, cooldown countdowns, projectile travel, hit detection, and deterministic state updates for equivalent inputs.
- Networking protocol: message serialization, protocol version mismatch handling, input command handling, state snapshot handling, join failure paths, Disconnect Forfeit, and Host Disconnect End.
- Local profile: Local Display Name save/load behavior and fallback/default display name behavior.

Recommended manual tests:

- Four supported Windows or Linux desktop machines on the same LAN complete a full best-of-3 Match.
- Three Clients join one host by manual IP and port.
- Invalid IP/port shows a connection error.
- Host disconnect ends the Match for all Clients.
- Non-host disconnect causes that Client's Team to forfeit.
- Players can understand Round winner, Match winner, health, cooldowns, and death state without verbal explanation.
- Lobby music plays outside combat, battle music plays during combat, and Kill Announcement plays on enemy defeat.

Rendering and audio should be covered primarily by focused manual verification at MVP stage, with automated tests reserved for pure state, rule, and protocol behavior.

## Out of Scope

- Dedicated servers.
- Matchmaking.
- Lobby browser.
- Invite codes.
- Relay server.
- NAT traversal.
- Account system.
- Online profile service.
- Global stats.
- Match history.
- Progression.
- Cosmetics.
- Unlocks.
- Bots.
- Training mode.
- Single-player mode.
- Gamepad support.
- Mouse aim.
- Rebindable controls.
- Settings screen.
- Friendly fire.
- Obstacles.
- Pickups.
- Hazards.
- Shrinking arena.
- Host migration.
- Reconnection.
- Bot replacement after disconnect.
- macOS, mobile, WebAssembly, console, and broader platform support.
- External visual assets, sprite sheets, texture packs, character portraits, or art packs.
- Full announcer system or character voice-line system.
- Final competitive balance.

## Further Notes

Baseline combat values are starting points for MVP playtesting, not final balance targets. The MVP success bar is a readable, stable, playable foundation that four players can understand and enjoy enough to iterate.

The PRD follows the domain glossary in CONTEXT.md and the accepted ADRs for Windows and Linux desktop scope, Custom Game Surface rendering, Fixed-Tick Host Simulation, and Host-Authoritative Listen Server networking.

Issue tracker publication is currently blocked because this workspace has no configured GitHub remote and the GitHub connector found no installed repository named AvaloniaBattleground. When a repository exists, publish this PRD as a feature issue with a priority label and keep it in Backlog or Needs review until triage produces an agent-ready implementation brief.
