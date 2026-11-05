# Save schema v1 and MessagePack compatibility evidence

Date: 2026-11-04 (Asia/Singapore)

Status: PASS — story 001 complete within its schema/codec compatibility scope. Full gameplay save/load remains future work.

## Scope

Save / Load story 001: production MessagePack 3.1.12 + LZ4 codec, versioned string-key maps, raw unknown-field preservation, canonical item identity, concrete reservations, debug document inspection, and an isolated Windows Player probe. Full live-world capture, backups and hydration are not part of this spike.

## Results

| Layer | Result | Evidence |
| --- | --- | --- |
| Dependency pins | PASS | `Assets/packages.config`; `Packages/manifest.json`; `Packages/packages-lock.json`, MessagePack Unity commit `eb108f89700db93e67f10148b777380c244ac6ae` |
| Pure C# NUnit | 104 total, 101 passed, 3 Unity-only skips, 0 failed | `Logs/save-purecheck-20261104.trx` |
| Installed Unity references, offline compile | PASS, 0 warnings / 0 errors | `Logs/save-spike-compile.log`; .NET Standard 2.1; includes Editor tools and the `EQ_SAVE_SPIKE;ENABLE_IL2CPP` C# branch. This does not execute IL2CPP. |
| Unity EditMode (final) | 115 total, 114 passed, 1 skipped, 0 failed; 0.8293144 s test time | `Logs/save-schema-editmode-final-20261104.xml`; all 23 SaveLoad cases passed |
| Windows IL2CPP build | PASS, process exit 0; Unity 6000.6.1f1, Windows x64 | `Logs/save-il2cpp-build-20261104.log`; `Builds/SaveSpike/SaveSpike.provenance.json` (2026-11-04 12:50:31Z–13:09:40Z) |
| Windows Player round-trip | PASS, process exit 0, compressed fixture 492 bytes | `Logs/save-player-20261104-2110.result`; `Logs/save-player-20261104-2110.log` contains `EQ_SAVE_PROBE_PASS`; sibling `.eqs` and `.json` files are the actual Player output |
| CGS validate | 8 existing failures | `Logs/cgs-validate-save-20261104.log` |

The skipped Unity test is the pre-existing debug snapshot guard, compiled without `EQ_DEBUG_SNAPSHOT`; that test runs in PureCheck. PureCheck's three skipped tests require real Unity asmdef separation, which the EditMode run covers.

Player marker:

```text
PASS backend=IL2CPP codec=MessagePack-3.1.12 compression=Lz4BlockArray canonical-reservation raw-extension unknown-members json-export bytes=492
```

The probe explicitly refuses non-IL2CPP builds. This result establishes the configured save path under Windows IL2CPP; it does not establish other platforms, high stripping levels, or complete game save/load. The first native build took approximately 19 minutes, dominated by optimizing generated Unity Pipeline interpreter code. The successful build retained the existing compiler configuration.

## Behavioral coverage

- Actual LZ4 compression and complete envelope/section round-trip, UInt64 tick index and Addon/Market header fields.
- Incoming unknown sibling members at document, header, Addon, binding, section, record and reservation levels retain exact bytes over repeated binary round-trips. Fixture includes a non-minimal integer encoding, a float32 NaN payload, a custom extension and nested array/map data. Independent wire-map reads verify the original member location.
- Explicit Extras retains raw values; input and returned byte buffers cannot mutate retained values.
- JSON input with unknown root/header/section members survives, fixing the earlier `MissingMemberHandling.Ignore` data loss.
- A real Tag graph selects an iron ingot; the reservation persists `equilibrium:iron_ingot`, `input` and `ore`, without a handle or Tag expansion.
- Invalid/tag item ids, missing/newer/invalid headers, duplicate keys, truncated/trailing data (including trailing values inside the compressed envelope), huge collection claims and over-deep unknown structures are rejected. Oversized uncompressed documents are refused before producing a compressed file that the codec itself could not load.
- Save DTO reflection audit has no RNG or time-control properties. Debug inspection owns detached copies of the published document.

## Environment failures resolved during verification

1. Initial Editor attempt exited **198** before compilation with `EDITOR_LICENSING_FAILURE`. The user restored local authorization; later execution reached compilation and tests.
2. The prior raw NuGet restore had enabled all framework variants and `ref/` DLLs. Unity failed domain reload while loading `Assets/Packages/Microsoft.NET.StringTools.17.11.4/ref/net472/Microsoft.NET.StringTools.dll` as executable code. The stalled test process was stopped. `tools/repair-save-package-imports.ps1 -Apply` changed 30 restored DLL import metadata files, without deleting/moving DLLs or changing GUIDs; a second dry run reported 0 changes. The following EditMode run passed.
3. CLI `unity build` owns `-nographics`; forwarding it through `--args` was rejected before launch. The corrected build command below omits it.
4. The first build-method execution could not add a scene to the batch Editor's untitled initial scene. The probe now requires batch mode and creates its temporary scene with `NewSceneMode.Single`; interactive Editors are refused before any scene change.

## Reproduction

Run from `D:\UnityProj\Equilibrium` in PowerShell. Test reports, compiler temp and build output use D: because C: free space is limited. Environment variables below affect only that PowerShell process and its children.

```powershell
$env:TEMP = 'D:\UnityProj\Equilibrium\Logs\save-spike-temp'
$env:TMP = $env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj --verbosity minimal
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode EditMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\save-schema-editmode-20261104.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\save-schema-editmode.log'
unity build 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --target StandaloneWindows64 --execute-method Equilibrium.Editor.SaveSpikeBuild.Build --output-path 'D:\UnityProj\Equilibrium\Builds\SaveSpike\SaveSpike.exe' --log-file 'D:\UnityProj\Equilibrium\Logs\save-il2cpp-build-20261104.log' --timeout 1500 --no-tail --allow-dirty-build --format json --no-banner --no-pager --non-interactive
$marker = Join-Path $PWD ('Logs/save-player-' + [Guid]::NewGuid().ToString('N') + '.result')
$player = Start-Process -FilePath 'D:\UnityProj\Equilibrium\Builds\SaveSpike\SaveSpike.exe' -ArgumentList @('-batchmode', '-nographics', '-saveProbeResult', $marker, '-logFile', 'D:\UnityProj\Equilibrium\Logs\save-player.log') -WindowStyle Hidden -PassThru
if (!$player.WaitForExit(45000)) { Stop-Process -Id $player.Id; throw 'Probe timed out' }
if ($player.ExitCode -ne 0) { throw 'Player failed; inspect the log and marker' }
Get-Content -LiteralPath $marker
```

The build method enables the probe only through `BuildPlayerOptions.extraScriptingDefines`, chooses IL2CPP, uses a temporary scene, and restores the previous backend in `finally`. Do not enable `EQ_SAVE_SPIKE` for ordinary production builds.

Cleanup verification: no `Assets/__SaveSpike_*` directory remained after the successful build. Standalone backend returned to Mono (`0`). Unity materialized an explicit default `Standalone: 0`; that build-only entry was then removed, retaining the user's existing scripting-define order change. `ProjectSettings/ProjectSettings.asset` SHA-256 returned to the pre-run `709BA261FC6FA7F47A6FCA57CF1211A0149E58D8110BF4338C7FA984BAB5E078`. `Assets/Scenes/Main.unity` SHA-256 remained `C21354C86D8235B2BCAF7C875EBCFDA71EDFCAC36DC71F3F04B69DC958A92AB7` across this work.

## Remaining boundaries

- Editor menu code and detached snapshot behavior are covered; manual file-dialog interaction is not automated. Publishing the live factory document awaits story 002's boundary capture service.
- Missing Addon identifiers survive this codec; UnknownItem/UnknownBlock world hydration awaits story 003.
- Performance on large worlds, atomic writes/backup rotation, migrations and remote market authorization remain outside this acceptance. The writer currently materializes uncompressed and compressed buffers to verify its own input-size compatibility; no streaming allocation/performance claim is made.
- CGS still expects a nested `src` Unity project, monolithic GDD and several bootstrap artifacts. No duplicate project or dummy documents were created to hide those eight failures.
- `docs-impact --base HEAD` cannot see much of the untracked implementation; its “no functional game paths changed” output is not evidence of absent code changes. Owning architecture, story and session documents are updated explicitly.
