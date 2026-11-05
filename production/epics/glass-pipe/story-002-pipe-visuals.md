# Story 002: Mask-driven pipe content visuals

> **Epic**: Glass Pipe
> **Status**: Ready
> **Layer**: Feature
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/glass-pipe.md`
**Requirement**: `TR-pipe-002`

**Governing ADRs**: ADR-0007
**ADR Decision Summary**: shader/material parameters generate regions and alpha from masks; use URP 17 Render Graph API if a custom pass is needed; no generated placeholder art for missing masks.

**Engine**: Unity 6000.6.1f1 | **Risk**: HIGH (URP 17 shader work)

## Acceptance Criteria

- [ ] Liquid layer then gas layer both over the full topology mask (GDD AC 1).
- [ ] Components split the layer into equal-area regions in SubstanceId order with alpha by amount (GDD AC 2); positions never swap as amounts change (GDD AC 3).
- [ ] Bridge uses two masks; `r` means left-to-right.
- [ ] Missing mask -> missing-asset marker, no generated art (GDD AC 8).
- [ ] Uses the existing `Assets/Art/GlassPipe` masks; material parameters set from simulation data each committed tick.

## QA Test Cases

- Manual: A+B liquid, C gas -> screenshots at three amount ratios; regions stable
- Manual: remove a mask asset -> marker shown

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/glass-pipe-visual-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001
- Unlocks: none
