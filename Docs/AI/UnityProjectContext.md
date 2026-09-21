# Unity project context — Network branch

Baseline: `5af7c8ccaef73a894ba4a2a64c73e113934865db`. Unity 6000.3.11f1.
The Network branch initially contained the template SampleScene, Readme scripts, builtin modules and Multiplayer Center; no gameplay or networking implementation.

The template still has URP assets/references, but no URP package in manifest. Legacy Input Manager is enabled. Do not merge unrelated renderer/input migrations into networking changes.

Implemented networking is under `Assets/ChessFight/Network`: pure Core assembly plus a Steam adapter/runtime assembly gated on the installed Steamworks.NET package. `Assets/ChessFight/Editor/NetworkSetup.cs` installs the pinned dependency using the UPM Client API. Keep generated manifest and lock changes together after successful installation.

Architecture: Steam private party lobby (1–6); public match lobby (12); host-owned atomic party reservations; Steam Networking Messages for host-authoritative flat-arena movement. Host departure aborts the match; Steam lobby ownership transfer is not gameplay host migration. No ranked queue or dedicated server.

`NetworkSandbox` bootstraps only SampleScene/NetworkSandbox. No serialized scene/prefab mutation is needed to try this prototype. The actual game scene integration must remain explicit.

Tests live outside Assets. `Tools/Test-NetworkCore.ps1` exercises core rules and the production session class with a fake service. `Tools/Test-NetworkCompile.ps1` compiles against real Unity and Steamworks sources. Real Steam, Player builds and visual checks are separate requirements; see `Docs/Network/VALIDATION.md`.

All Steam IDs are obtained from the transport/lobby service, not accepted as authoritative movement owners from packet payloads. Future protocol changes must increment the lobby protocol discriminator. Avoid introducing multiple independent SteamAPI initialization/shutdown owners.
