# Save document schema v1

Updated: 2026-11-04

Owner: Save / Load story 001. Governing decisions: ADR-0005, ADR-0003. This document describes the implemented serialization envelope; automatic factory capture, atomic file replacement, backup rotation, migrations, and world hydration belong to stories 002/003.

## Format and dependencies

The production codec is `Assets/Scripts/Simulation/Save/SaveDocumentCodec.cs`. It uses MessagePack-CSharp **3.1.12**, `Lz4BlockArray`, string-keyed maps, and `UntrustedData` security settings. The save resolver registers only explicit, statically referenced formatters in `SaveFormatters.cs`. There is no contractless, typeless, reflection-based object serialization or dynamic formatter fallback on this path.

Unity dependency installation has two parts: NuGet packages pinned by `Assets/packages.config`, and the Unity integration package pinned by `Packages/manifest.json` with its resolved commit in `Packages/packages-lock.json`. Restored binaries in `Assets/Packages/` are ignored. The pure C# verification project pins the same MessagePack version.

An older local extraction left all target frameworks and `ref/` DLLs active. Unity attempted to execute `Microsoft.NET.StringTools/ref/net472`, which prevents domain reload. `tools/repair-save-package-imports.ps1` repairs only importer metadata for the nine exact pinned package directories: default invocation previews changes, `-Apply` applies them. It leaves package files and GUIDs in place, retains the three required runtime DLLs and analyzer labels, disables duplicate framework/reference DLLs, and uses Unity 6000.6's own .NET Standard 2.1 framework assemblies. This is a repair for that existing workspace state; normal NuGetForUnity installation selects a compatible `lib/` framework and excludes `ref/` itself.

| Map | Known fields |
| --- | --- |
| Document | `header`, `world`, `containers`, `mech`, `market`, `ledger`, `unlocks`, `tasks`, `extras` |
| Header | `schemaVersion`, `gameVersion`, `addons`, `marketBinding`, `lastSaveWallTime`, `tickIndex`, `extras` |
| Addon | `namespace`, `version`, `extras` |
| Market binding | `mode`, `serverId`, `extras` |
| Section | `version`, `records`, `extras` |
| Record | `id`, `containerKey`, `slotKey`, `itemId`, `count`, `reservations`, `extras` |
| Reservation | `id`, `containerKey`, `slotKey`, `itemId`, `count`, `extras` |

`tickIndex` uses UInt64. Counts use Int64; negative counts are refused. A reservation requires positive count, an id, string binding keys, and a concrete item identity. `lastSaveWallTime` is an optional ISO timestamp string in the envelope; the capture service will supply the clock value. Sections have independent positive version numbers. Their system-specific payload and hydration schemas are not frozen by this spike.

## Identity boundary

`SaveRecord.ItemId` and `SaveReservation.ItemId` canonicalize shorthand on assignment using the existing Content `ItemId` parser. `wood` becomes `equilibrium:wood`. Tag references, uppercase ids, and malformed ids are refused. A missing Addon namespace is allowed at this boundary so later hydration can create an UnknownItem without losing the original id.

`SaveReservation.FromResolved` accepts an already chosen runtime `ItemId`. A recipe may select that item through a Tag, but the serialized reservation contains only the concrete canonical string. Runtime handles and expanded Tag memberships are not save fields. The format does not add RNG or speed/pause fields.

## Unknown members and Extras

Every save object exposes `IDictionary<string, object> Extras`. Incoming unknown fields are captured as immutable `RawSaveValue` objects, retaining the original encoded value, including integer width, float bit patterns, arbitrary extension payloads, and nested data. `CopyBytes()` returns a copy rather than the retained buffer.

An unknown sibling remains a sibling when reserialized. A value originally inside the explicit `extras` map remains inside that map. A record tracks this location internally; no extra location metadata is written to the file. Replacing a retained value is an explicit data edit and may change its encoding. Removing and re-adding the same sibling key retains its sibling location for that object instance.

Duplicate map keys, null keys, and the same unknown key appearing both as a sibling and inside `extras` are refused as ambiguous. They are not silently merged. Application-authored Extras values may be null, primitive booleans/numbers/strings, binary bytes, or validated `RawSaveValue`. Arbitrary CLR objects and recursive object dictionaries are rejected; Addons must encode structured extension data as a raw MessagePack value.

Exact preservation applies to an unknown **value**, not the entire file: compression, map ordering, and known-field encoding can change. JSON is an inspection/interchange view, not a byte-preserving replacement for binary data, custom extensions, or unusual floating point encodings.

## Validation and limits

- A document must contain a non-null header and an explicit positive `schemaVersion`. A newer schema is refused with `SaveFromNewerVersion`.
- The reader refuses trailing values, truncated input, duplicate keys, invalid item identities and invalid concrete reservations.
- Encoded input and decompressed input are bounded to 64 MiB. The writer also checks the uncompressed document before LZ4 compression so it cannot produce an oversized file that this codec refuses to read. Object graph depth is bounded to 64; individual collection counts to 1,000,000 and available input bytes. Unknown nested data goes through the same bounded depth walk before its raw bytes are retained.
- Optional absent sections receive v1 empty defaults. This is envelope parsing, not permission to silently drop required gameplay state; migration/hydration must validate system-specific completeness before changing the live world.
- Market binding here is data. Enforcing immutable binding and migration credentials remains a SaveService/Market responsibility, not a property of deserializing a DTO.

The current writer materializes the uncompressed bytes to enforce the size limit, then compresses those bytes. Both buffers can coexist during a save. Large-world allocation and timing targets are not yet measured; streaming/pooling can be evaluated with SaveService without changing this wire format.

## Debug inspection

`SaveDocumentSession.Publish(document)` captures detached codec bytes. `ReadCurrent()` returns a new decoded copy; subsequent mutation of either the original or inspected document cannot change the published snapshot. The session clears when Play mode initializes, including when domain reload is disabled.

`Equilibrium/Save/Inspect existing EQS` loads a selected file into this detached session. It never hydrates the live world. `Equilibrium/Save/Export JSON` writes the session document to a user-selected path and is disabled until a document exists. The live SaveService must publish its last completed boundary capture when story 002 is implemented; this spike does not fabricate a live factory document.

## Player compatibility probe

`Assets/Editor/SaveSpikeBuild.cs` builds a temporary empty scene with `EQ_SAVE_SPIKE`. It temporarily chooses Windows IL2CPP, restores the previous scripting backend in `finally`, and removes only the uniquely named probe scene directory. Existing game scenes and build settings are not rewritten by the build method. A process kill can bypass `finally`, so after an interrupted build verify the backend and any `Assets/__SaveSpike_*` directory before retrying.

`Assets/Scripts/Presentation/SaveCodecPlayerProbe.cs` is excluded unless `EQ_SAVE_SPIKE` is set. In that build it verifies LZ4, canonical reservations, unknown members, raw extension bytes, UInt64 tick index, and JSON inspection, then writes a marker and exits. Editor tooling is inside the Editor assembly.

Reproduction commands and actual platform results are in `production/qa/evidence/save-il2cpp-spike-evidence.md`. A successful compile or build alone is not a successful Player run.

## Reference

[MessagePack-CSharp v3.1.12 documentation](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/v3.1.12/README.md) describes Unity's two-part installation, custom formatters, explicit resolver configuration, compression, security depth checks, and AOT requirements. The wire contract above is specific to Equilibrium.
