# Avalonia Battleground

This context defines the domain language for a 2D arena fighting game built with C# and AvaloniaUI. It exists to keep design, PRD, and implementation discussions aligned around the same gameplay concepts.

## Language

**Arena Fighter**:
A top-down competitive combat game where players fight inside a bounded arena.
_Avoid_: MOBA, platform fighter, shooter

**Match**:
A single 2v2 battle between two teams.
_Avoid_: Game session

**Round**:
A 90-second combat segment within a Match that awards one point to the winning Team.
_Avoid_: Match, wave

**Team**:
A pair of players fighting together in a Match.
_Avoid_: Squad, party

**Fighter**:
A player-controlled combatant in a Match.
_Avoid_: Character, hero, avatar

**Melee Fighter**:
A close-range combat role intended to contest space near opponents.
_Avoid_: Tank, brawler

**Ranged Fighter**:
A distance combat role intended to pressure opponents from outside melee range.
_Avoid_: Gunner, archer

**Top-Down View**:
An overhead camera perspective similar to agar.io, where the arena is seen from above.
_Avoid_: Isometric, side view

**Circular Arena**:
A bounded open combat space with a circular edge and no internal obstacles.
_Avoid_: Map, level, obstacle arena

**Pass-Through Fighters**:
The collision rule that Fighters do not block or push each other and may occupy overlapping positions.
_Avoid_: Body blocking, solid fighters

**Hard Arena Boundary**:
The Circular Arena edge rule where Fighters cannot move or dash outside the arena.
_Avoid_: Soft boundary, bounce wall, shrinking arena

**Projectile Boundary Removal**:
The rule that projectiles disappear when they hit the Circular Arena boundary.
_Avoid_: Projectile bounce, off-map projectile

**Enemy-Only Damage**:
The damage rule that attacks and abilities affect opposing Fighters but never allied Fighters.
_Avoid_: Friendly fire, team damage

**Cooldown-Only Combat**:
The action pacing rule where attacks, dash, and abilities are limited by cooldown timers rather than stamina, mana, ammo, or other spendable resources.
_Avoid_: Mana, stamina, ammo

**Role Health**:
The starting health assigned by Fighter role, with Melee Fighters starting at 200 HP and Ranged Fighters starting at 100 HP.
_Avoid_: Shared health pool, normalized health

**Visual Asset Constraint**:
The rule that gameplay visuals should be created procedurally or with native drawing primitives rather than external art assets.
_Avoid_: No assets, programmer art

**Minimal Vector Combat Visuals**:
A visual style using native drawing primitives, team colors, simple shapes, telegraphs, health bars, cooldown indicators, hit flashes, and projectile effects.
_Avoid_: Sprite art, texture packs, character portraits

**MVP Audio Set**:
The external audio assets needed for MVP, including gameplay sound effects and looping music.
_Avoid_: SFX-only audio, full voice pack

**Kill Announcement**:
A short audio cue played when a Fighter defeats an enemy.
_Avoid_: Announcer system, character voice line

**MVP Screen Flow**:
The first playable screen set: main menu, host setup, join screen, lobby, match screen, round result overlay, match result screen, and connection or error dialog.
_Avoid_: Settings screen, account screen, matchmaking screen

**Local Display Name**:
A player-chosen name stored on the local machine and shown in lobbies, match UI, and kill messages.
_Avoid_: Account, profile, username service

**Multiplayer-Only MVP**:
The MVP scope where only hosted 4-player multiplayer matches are player-facing.
_Avoid_: Bots, training mode, single-player mode

**Windows and Linux Desktop Product**:
The platform scope where the game targets Windows and Linux desktop as supported product platforms.
_Avoid_: macOS support, mobile support, WebAssembly support, console support

**Custom Game Surface**:
An Avalonia custom-drawn gameplay area used to render the arena, fighters, attacks, projectiles, and combat feedback.
_Avoid_: Per-entity UI controls, sprite canvas

**Fixed-Tick Host Simulation**:
A 60 Hz host-side simulation loop where the host is authoritative for movement, hits, health, cooldowns, deaths, round state, and scoring.
_Avoid_: Variable-delta combat simulation, client-authoritative simulation

**Host-Authoritative Listen Server**:
A network model where one player's machine hosts the Match simulation and connected players send inputs to that host.
_Avoid_: Pure peer-to-peer, dedicated server

**Team Elimination**:
A Round win condition where a Team wins by defeating both opposing Fighters.
_Avoid_: Deathmatch, kill count

**Spectator State**:
The state of a defeated Fighter who can observe but cannot rejoin combat until the next Round.
_Avoid_: Respawn timer, ghost mode

**Health Tiebreaker**:
A Round resolution rule where the Team with the highest combined remaining health wins if both Teams still have living Fighters when time expires.
_Avoid_: Sudden death, draw

**Keyboard-Only Controls**:
A control scheme where players move with WASD, aim with arrow keys, primary attack with Spacebar, dash with Left Shift, and use their role ability with Left Ctrl.
_Avoid_: Mouse aim, gamepad support, rebindable controls

**Client**:
A single running game instance controlled from one machine by one player.
_Avoid_: Local player slot, shared keyboard

**Lobby**:
The pre-match state where connected Clients choose Teams and roles before the host starts the Match.
_Avoid_: Queue, party

**Role Constraint**:
The requirement that each Team has exactly one Melee Fighter and one Ranged Fighter before a Match can start.
_Avoid_: Class balance, team lock

**Full Lobby Requirement**:
The rule that a player-facing Match requires exactly four connected Clients before it can start.
_Avoid_: Partial match, bot fill

**Disconnect Forfeit**:
The match resolution rule where a non-host Client disconnect causes that Client's Team to lose the Match.
_Avoid_: Reconnect, bot replacement

**Host Disconnect End**:
The network failure rule where the Match ends for all Clients if the host disconnects or closes the game.
_Avoid_: Host migration, server failover

**Manual Address Join**:
The connection flow where Clients join a hosted Match by entering the host IP address and port.
_Avoid_: Matchmaking, lobby browser, invite code

**Local Network Assumption**:
The MVP networking assumption that hosted matches are played over LAN, VPN, or manually port-forwarded connections.
_Avoid_: NAT traversal, relay server, internet matchmaking

**Role Kit**:
The role-specific combat actions available to a Fighter, consisting of one primary attack and one role ability.
_Avoid_: Character kit, spellbook

**Universal Dash**:
A short movement burst available to every Fighter regardless of role.
_Avoid_: Melee dash, dodge roll

**Melee Area Slash**:
The Melee Fighter role ability that damages nearby enemies all around the Fighter.
_Avoid_: Spin attack, whirlwind

**Ranged Cone Volley**:
The Ranged Fighter role ability that fires five arrows in a cone and can hit multiple Fighters.
_Avoid_: Multishot, shotgun

**Single-Hit Volley Rule**:
The rule that a Fighter can take damage only once from a single Ranged Cone Volley, even if struck by multiple arrows from that volley.
_Avoid_: Multi-hit volley, arrow stacking

**Melee Frontal Strike**:
The Melee Fighter primary attack: a quick short-range sword strike in the current aim direction.
_Avoid_: Auto attack, basic slash

**Ranged Single Arrow Shot**:
The Ranged Fighter primary attack: a straight projectile fired in the current aim direction.
_Avoid_: Auto shot, basic arrow

## Relationships

- A **Match** contains exactly two **Teams**
- A **Match** is best of three **Rounds**
- A **Round** lasts 90 seconds
- A **Team** contains exactly one **Melee Fighter** and exactly one **Ranged Fighter**
- A **Melee Fighter** and **Ranged Fighter** are both Fighters
- A **Melee Fighter** starts with 200 HP
- A **Ranged Fighter** starts with 100 HP
- A **Melee Fighter** and **Ranged Fighter** each have a distinct **Role Kit**
- Every Fighter has **Universal Dash**
- The **Melee Fighter** primary attack is **Melee Frontal Strike**
- The **Ranged Fighter** primary attack is **Ranged Single Arrow Shot**
- The **Melee Fighter** role ability is **Melee Area Slash**
- The **Ranged Fighter** role ability is **Ranged Cone Volley**
- A **Ranged Cone Volley** follows the **Single-Hit Volley Rule**
- A **Lobby** enforces the **Role Constraint**
- A **Match** can start only after the **Role Constraint** is satisfied for both **Teams**
- A **Match** can start only after the **Full Lobby Requirement** is satisfied
- A **Client** controls exactly one Fighter in a **Match**
- A non-host **Client** disconnect triggers **Disconnect Forfeit**
- Host disconnection triggers **Host Disconnect End**
- Clients connect using **Manual Address Join**
- MVP networking follows the **Local Network Assumption**
- A **Round** is won by **Team Elimination** or by **Health Tiebreaker** when time expires
- A defeated Fighter enters **Spectator State** until the next **Round**
- An **Arena Fighter** uses a **Top-Down View**
- A **Match** is fought in a **Circular Arena**
- The **Circular Arena** uses a **Hard Arena Boundary**
- Projectiles follow **Projectile Boundary Removal**
- Fighters follow **Pass-Through Fighters** collision rules
- Attacks and abilities follow **Enemy-Only Damage**
- Combat actions follow **Cooldown-Only Combat**
- An **Arena Fighter** uses **Keyboard-Only Controls**
- The **Visual Asset Constraint** applies to visual gameplay assets, not audio assets
- **Minimal Vector Combat Visuals** satisfy the **Visual Asset Constraint**
- **MVP Audio Set** may use external audio assets
- The **MVP Audio Set** includes lobby music, battle music, combat sound effects, and **Kill Announcement**
- The game uses the **MVP Screen Flow**
- MVP persistence is limited to **Local Display Name**
- The product MVP is a **Multiplayer-Only MVP**
- The product is a **Windows and Linux Desktop Product**
- Gameplay is rendered through a **Custom Game Surface**
- A **Match** uses a **Host-Authoritative Listen Server** network model
- The host runs a **Fixed-Tick Host Simulation**

## Example Dialogue

> **Dev:** "Can both players on a **Team** choose **Ranged Fighter**?"
> **Domain expert:** "No. Each **Team** has exactly one **Melee Fighter** and one **Ranged Fighter**."

## Flagged Ambiguities

- "peer to peer with a host" was resolved to mean **Host-Authoritative Listen Server**, not pure peer-to-peer networking.
- "5-7 arrows" for **Ranged Cone Volley** was resolved to five arrows for MVP.
