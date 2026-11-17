# Processor Tycoon — Windows installer

Russian WinForms installer targeting .NET 11, self-contained Windows x64, single EXE. All source/build/test output for this component stays in `installer/`.

## Production build

Prepare `../dist/payload.zip`, then run from this directory:

```powershell
dotnet publish .\ProcessorTycoon.Installer.csproj -c Release -o ..\dist\setup -t:Rebuild -p:DebugType=None -p:DebugSymbols=false
```

Distribute only `dist/setup/ProcessorTycoon-Russian-Setup.exe` (relative to the project root). End users do not need .NET. The ZIP is an embedded resource; no sidecar package is needed. Publishing without a payload is rejected. `dotnet build` without a payload is allowed for source validation, but installation will explicitly fail.

To embed a differently located ZIP, pass `-p:PayloadPath=C:\absolute\payload.zip` (also useful for isolated tests). **Do not distribute a test fixture build.** Run the production publish again after tests, using `-t:Rebuild` to avoid stale embedded payload bytes when switching package paths. Run `Test-ProductionInstaller.ps1` after publishing to compare every installed file against the actual ZIP and exercise uninstall with and without another mod on synthetic game fixtures.

## Payload contract

ZIP paths must use `/`, without a leading package folder. Exact allowlist is in `InstallerEngine.cs`; unknown files and duplicate paths are rejected. No game EXE, game data, Unity DLLs, foreign plugins, arbitrary configs or arbitrary assemblies are permitted.

- `winhttp.dll`, `doorstop_config.ini`, optional `.doorstop_version`
- BepInEx 5 core DLL/XML files, explicitly named in the allowlist
- `BepInEx/plugins/ProcessorTycoon.Russian/ProcessorTycoon.Russian.dll`
- Under that plugin's `translations/`: `ui.json`, `code.json`, `extra.json`, `templates.json`

The loader, doorstop config, BepInEx.dll and Russian plugin DLL are required. Translations should all be included by the package producer. ZIP directory entries are ignored after path validation. File limits: 64 MiB each / 256 MiB total. Traversal, ADS paths, backslashes, trailing dots/spaces and existing reparse points/junctions are rejected.

## GUI and CLI

Double-click the EXE. Default folder: the current user's Desktop/Processor Tycoon. Select a folder containing both `Processor Tycoon.exe` and `Processor Tycoon_Data/Managed/Assembly-CSharp.dll`. Close the game first. No elevation is requested; pick a folder writable by the user.

```powershell
$p = Start-Process ..\dist\setup\ProcessorTycoon-Russian-Setup.exe -ArgumentList '--install', '"C:\Games\Processor Tycoon"', '--result', '"C:\Temp\install-result.json"' -Wait -PassThru
$p.ExitCode
```

Commands: `--install <dir>`, `--uninstall <dir>`, `--verify <dir>`. All optionally accept `--result <path>` and write UTF-8 JSON `{ "exitCode": 0, "message": "…" }`. Use `Get-Content -Encoding UTF8` on Windows PowerShell 5. GUI builds have no console window; CLI attempts to attach the parent's console, but use the result file and process exit code for reliable automation. PowerShell must wait for the GUI-subsystem executable (as shown above).

Exit codes: 0 success/idempotent operation; 1 I/O/unexpected error; 2 bad CLI or game folder; 3 conflict, corrupt metadata or invalid package; 4 running game / cannot inspect matching process; 5 verify when not installed.

## Preservation and recovery

`game/.processor-tycoon-russian/` contains a process lock, versioned hash manifest, immutable original backups, transaction snapshots and a pending transaction journal. SHA-256 identifies installed and original bytes. These backups are retained after uninstall deliberately; the installer never recursively deletes user folders or other mods. Empty directories created by the installer may remain.

Install refuses any preexisting shared payload file (loader, doorstop configuration/version, or BepInEx core) whose bytes differ from the package, before creating installer state or modifying game files on a fresh installation. The error identifies the file and recommends a separate clean game copy or resolving runtime compatibility with the other mod authors. Identical shared files are accepted and backed up for restoration; translation files are also backed up. Reinstall of exactly the same package verifies hashes and preserves the original manifest/backups. A different package requires uninstall first. Changed/missing installed files cause a complete refusal before uninstall writes anything; restore their expected bytes or preserve your edits separately before retrying. Missing/corrupt backups also stop the operation.

On uninstall, files anywhere in `BepInEx/patchers` or outside `BepInEx/plugins/ProcessorTycoon.Russian/` in `BepInEx/plugins` count as other mods. When present, all shared payload runtime files are retained unchanged (including files originally created by setup), and only translation payload files are removed or restored. The success message explicitly reports retained BepInEx/loader files. Other mods added after installation are also detected. With no other mods, all payload files are removed or restored normally. Empty foreign mod directories alone do not require runtime retention.

Writes use sibling temporary files and atomic rename, with a durable write-ahead journal and transaction snapshots. On normal failure the installer rolls back. A later invocation recovers an interrupted transaction before proceeding, unless an external edit creates an ambiguous state, in which case it refuses and retains all evidence. Do not remove metadata while installed. Backups/snapshots are never overwritten or garbage-collected automatically. They are recovery data, not cryptographically authenticated records; local administrators can alter them. Avoid concurrently changing game files while setup runs.

Process checks compare the full MainModule executable path for processes named `Processor Tycoon`; the installer never kills a process. Game presence is validated, **not a game-version hash**; no supported Assembly-CSharp hash was supplied. This component does not certify translation compatibility with other game releases.

## Tests

```powershell
.\Test-Installer.ps1
```

Builds with a clearly synthetic payload and operates only on fixtures under `installer/test-output/<guid>`. Covers shared loader/config/core/version refusal without mutation, install, verify, rerun/backup idempotency, refusal on user edits, identical-byte original runtime restoration, original translation restoration, foreign-plugin and newly added patcher preservation with complete runtime retention, reinstall after retention, repeated uninstall, invalid game folder, directory conflicts and a mid-transaction I/O failure with rollback. Fixtures are retained for inspection. Production payload/game compatibility, process-in-use handling, hard-crash recovery and interactive GUI rendering still need release-level checks. This test does not launch a real game.
