# Unity project context — Network branch

Baseline: `5af7c8ccaef73a894ba4a2a64c73e113934865db`. Unity 6000.3.11f1.
Current implementation reference: `c9c1e6af50548a8161d10f8754b9a9897a38b3ac` (Network).
Read [the detailed Korean handoff](NETWORK_HANDOFF_KO.md) before continuing. The implementation already exists; do not recreate it.
The Network branch initially contained the template SampleScene, Readme scripts, builtin modules and Multiplayer Center; no gameplay or networking implementation.

The template still has URP assets/references, but no URP package in manifest. Legacy Input Manager is enabled. Do not merge unrelated renderer/input migrations into networking changes.

Implemented networking is under `Assets/ChessFight/Network`: pure Core assembly plus a Steam adapter/runtime assembly gated on the installed Steamworks.NET package. `Assets/ChessFight/Editor/NetworkSetup.cs` installs the pinned dependency using the UPM Client API. Keep generated manifest and lock changes together after successful installation.

Architecture: Steam private party lobby (1–6); public match lobby (12); host-owned atomic party reservations; Steam Networking Messages for host-authoritative flat-arena movement. Host departure aborts the match; Steam lobby ownership transfer is not gameplay host migration. No ranked queue or dedicated server.

`NetworkSandbox` bootstraps only SampleScene/NetworkSandbox. No serialized scene/prefab mutation is needed to try this prototype. The actual game scene integration must remain explicit.

Tests live outside Assets. `Tools/Test-NetworkCore.ps1` exercises core rules and the production session class with a fake service. `Tools/Test-NetworkCompile.ps1` compiles against real Unity and Steamworks sources. Real Steam, Player builds and visual checks are separate requirements; see `Docs/Network/VALIDATION.md`.

Update (2026-09-21/22): UPM installation and Editor compilation succeeded in the user's original checkout. Manifest/lock and the package-generated Standalone define are committed in c9c1e6a. Editor Steam party creation, a private one-player room and capsule rendering were observed; the Windows x64 development build succeeded. On 2026-09-22 the standalone displayed an initialization error without Steam, then successfully created a one-player party and private match with a BLUE capsule after Steam login and restart. Two-account movement replication and real 12-player matchmaking/load remain unverified unless newer evidence is recorded in VALIDATION.md.

The user's Unity project is C:/Users/dua07/GitHub/ChessFighter. The initial implementation copy is C:/Users/dua07/Documents/Codex/2026-09-14/x20-ex-1-x20-vs-x20-2/ChessFight-Network. GitHub Desktop may label both ChessFight; verify the physical path before Pull or opening Unity. The original checkout has an unrelated generated UnityConnectSettings.asset m_Enabled change left uncommitted; inspect and preserve user changes.

All Steam IDs are obtained from the transport/lobby service, not accepted as authoritative movement owners from packet payloads. Future protocol changes must increment the lobby protocol discriminator. Avoid introducing multiple independent SteamAPI initialization/shutdown owners.
