# Equilibrium publishing checkpoint

Date: 2026-11-05. Environment: Windows 11, Unity 6000.6.1f1, .NET 8 PureCheck.
Review base: `4dbba695d478601a23341c0f4cca1e425e2218ab`.

## Scope

User requested a README and one push of the current project progress, then supplied the LINUX DO
community rules and selected ARR. This checkpoint includes the previously developed content,
simulation, logistics, factory scene, processing, power, UI, save codec, tests and associated docs.
It preserves the existing Unity scene/asset changes and the creator's additional conveyor sprites.
The unrelated untracked `Bruno/123/` scratch collection is left outside the commit.

Publication additions:

- `README.md`: project identity, implemented versus planned systems, setup, controls and test paths.
- `LICENSE`: ARR for original project material; existing third-party licenses remain applicable.
- `docs/community/linux-do-intro.md`: separately written post draft and publication notes. No post
  has been sent to LINUX DO. No open-source status, endorsement or lifetime free-use promise is made.
- `docs/images/factory-saw-line.png`: copy of the actual Game View capture already inspected during
  the powered-line verification. It shows temporary art and a working 300 W saw, not a target mockup.
  SHA-256: `CDBC3F045B40FF8A3B58DF2E1DAB8AF39D2FF19C9DEB37AA6AA28DAE2096F127`.

The incomplete live-save start contained a reference to an absent BlockSaveMetadata class. That
unused hook was removed, leaving the completed named-slot snapshot/restore foundation buildable.
Two regressions cover detached working-machine state and continued tick numbering. SaveService,
live-world hydration, disk I/O, backup rotation, save UI and autosave remain pending.

## Verification

| Check | Result | Local artifact |
| --- | --- | --- |
| .NET logic suite | 170 passed, 3 skipped, 0 failed; exit 0 | `Logs/publish-checkpoint-final-20261105.trx` |
| Unity EditMode | 184 passed, 1 skipped, 0 failed; exit 0 | `Logs/publish-editmode-20261105.xml` |
| Unity PlayMode | 5 passed, 1 skipped, 0 failed; exit 0 | `Logs/publish-playmode-20261105.xml` |
| CGS validate | exit 1; 8 existing template/state failures | `Logs/publish-cgs-validation-20261105.txt` |
| CGS docs-impact | exit 0; mapping limitation below | `Logs/publish-docs-impact-20261105.txt` |

Commands, run from the repository root in PowerShell:

```powershell
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj --no-restore --logger 'trx;LogFileName=publish-checkpoint-final-20261105.trx' --results-directory Logs
unity test . --editor-version 6000.6.1f1 --mode EditMode --report-format nunit --output Logs/publish-editmode-20261105.xml --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile Logs/publish-editmode-20261105.log
unity test . --editor-version 6000.6.1f1 --mode PlayMode --report-format nunit --output Logs/publish-playmode-20261105.xml --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile Logs/publish-playmode-20261105.log
.\codex-game-studio.cmd validate
.\codex-game-studio.cmd docs-impact --base HEAD
```

Unity was closed only after confirming Main was clean and not playing; automatic Git sync was
disabled. EditMode and PlayMode ran serially. Logs and restored package/cache payloads stay ignored.
The Editor was reopened on a clean Main scene. The test/import cycle's change from Visible Meta
Files to Unity Version Control was restored through the Editor API. Main, ProjectSettings and
FactoryPresentation hashes match their pre-test values, preserving the existing Unity edits.
The three PureCheck skips require real Unity assembly separation. The EditMode skip requires the
debug snapshot define; the PlayMode skip requires the optional generated Resources catalog.

The eight CGS failures are missing approvals/context-manifest files, three checks against an obsolete
`src/` Unity root, and three missing template document paths (`docs/market-overview.md`, `design/gdd.md`,
`docs/architecture/README.md`). docs-impact reports no functional paths because of that same root
mapping. This is not evidence that the game had no code changes; actual paths and owning docs were
reviewed manually. No Unity files were relocated to satisfy the template.

## Publication limits

- Tests prove the checked logic and scenes at this checkpoint, not large-world performance,
  completed gameplay, a full Player release or remote CI success.
- The earlier Windows IL2CPP evidence covers the standalone save-codec probe; it was not repeated
  as a full game build for this documentation/publishing task.
- A credential-pattern scan of commit candidates found no common private-key/API-token literals.
  This is a bounded hygiene check, not a claim that every historical commit was audited.
- Unity-generated empty YAML fields have trailing spaces in the scene, settings and importer metadata.
  These serialized files are preserved. Source/docs whitespace is checked separately, allowing the
  repository's existing CRLF line endings; redundant final blank lines are removed from authored text.
- LINUX DO publication is manual. ARR does not satisfy the open-source-promotion declaration;
  the draft uses the supplied ordinary-promotion category/tag rules and avoids the original tag.
