# MyCO Error Ledger

This is the single current index of user-visible regressions and confirmed
engineering defects. It stores short, privacy-safe evidence and points to the
requirements ledger for acceptance details.

| ID | Requirement IDs | Trigger | Observable symptom | Confirmed root cause | Permanent invariant | Regression / real evidence | Status | First found | Last reviewed |
|---|---|---|---|---|---|---|---|---|---|
| ERR-001 | BUB-002,BUB-003 | Whole mode on a long Markdown renderer shell | Narrow page-tall blank bubble and missing prose | A stretching flex/grid Markdown shell was selected as the decorated surface; host height rules leaked into the marker | Never decorate a stretching layout shell; fall back to safe semantic prose blocks and keep protected nodes native | `bubble-segmenter.test.ts` shell fixture; supplied screenshot; real Codex recheck pending | Fixed, visual unverified | 2026-08-06 | 2026-08-06 |
| ERR-002 | BUB-005,PRE-003 | Short Assistant reply under a percentage width cap | Different short replies render at nearly the same fixed width | Block `fit-content(100%)` interacted with renderer flex/stretch sizing instead of using a shrink-to-content width | Width is content-adaptive and only capped by the configured maximum; no fixed height or forced minimum width | Runtime CSS regression test; supplied screenshot; real Codex timing/visual pending | Fixed, visual unverified | 2026-08-06 | 2026-08-06 |
| ERR-003 | PRE-001,PRE-003 | Manager preview with default light theme | Assistant looks like bare text while User has a complete bubble | Preview roles did not bind the same dynamic width cap and relied on close-to-background Assistant surface contrast | Both role previews use one bubble layout primitive and one effective width/padding/radius model; semantic brushes remain separate | XAML regression test; supplied screenshot; WPF DPI/theme matrix pending | Fixed, visual unverified | 2026-08-06 | 2026-08-06 |
| ERR-004 | DEF-001 | New profile or factory reset with packaged Logo available | Default identity can be saved without a managed avatar path | Default resource was seeded only as a Manager-side inline operation with no reusable asset contract/test | Packaged Logo is copied through `AvatarService` into managed storage; existing custom avatars are never overwritten; failures fall back safely | `AvatarTests` managed-seed test and package resource check; first-run desktop observation pending | Fixed, visual unverified | 2026-08-06 | 2026-08-06 |

IDs in this file are stable. Historical chat, message text, credentials, cookies,
and complete DOM snapshots are intentionally unavailable and must not be added.
