# Development and verification notes

Date: 2026-07-26  
MyCodex: `0.2.0-alpha.4`

## Environment inspected

- Windows 11 Home x64, build 26200
- Local .NET SDK used for development: 8.0.423
- Node.js 24.18.0 / npm 11.16.0
- Official packaged Desktop detected:
  `OpenAI.Codex_26.721.4979.0_x64__2p2nqsd0c76g0`
- Official application version: 26.721.4979.0

No official executable, bundle, source, icon, DOM snapshot, or user chat data
was copied into this repository.

## CDP feasibility gate

The required gate was executed against the installed official application using
an isolated temporary user-data directory to avoid changing the already-active
development session.

Observed:

- random loopback port: 57345;
- `/json/version`: Browser `Chrome/150.0.7871.128`, Protocol `1.3`;
- `/json/list`: page target at `app://-/index.html`;
- WebSocket connection succeeded;
- `Runtime.evaluate("1 + 1")` returned `2`;
- `data-mycodex-probe="true"` was added to `body`, verified, and removed;
- the production runtime bundle injected successfully;
- handshake returned runtime `0.2.0-alpha.2`, protocol `1`;
- a synthetic current-Codex user/assistant fixture was installed only inside
  the isolated profile, then its style and conversation root were removed;
- the session health check restored the style, observer, and new root;
- diagnostics reported two scanned turns, one identified user, zero decorated
  user turns, one decorated assistant turn, zero unknown turns, and `0.955`
  average confidence;
- the official user bubble retained its class and had no MyCodex prose marker
  or inline style, while the assistant Markdown received the bubble;
- synthetic tool-card content remained undecorated;
- `destroy()` removed runtime style/decorations.

The probe exited successfully. These are local observations, not a promise for
every future Desktop version.

## Private-pipe gate

The same official Desktop version was launched with an isolated profile through
the Windows private-pipe path:

- restricted `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` inheritance succeeded;
- Browser `Chrome/150.0.7871.128`, protocol `1.3`;
- one `app://-/index.html` page target attached without a WebSocket URL;
- `Runtime.evaluate` and a reversible body data-attribute mutation passed;
- the result reported `Port: 0` and `BindAddress: private-pipe`;
- while the pipe was held open, the root Desktop process had zero listening
  TCP sockets;
- all isolated probe processes were cleaned up after validation.

## Manager GUI verification

The built executable was launched directly and inspected through Windows UI
automation:

- first-run onboarding rendered and continued;
- main Reference Dark Appearance page and compact preview rendered;
- application detection identified the installed packaged Desktop;
- a Store update from 26.721.3996.0 to 26.721.4979.0 was resolved through the
  stable Package Family identity instead of retaining the removed versioned path;
- both preview avatars rendered as circular cover crops;
- the preview states that assistant prose is bubbled while native user bubbles
  remain unchanged;
- Calibration page and instructions rendered;
- Diagnostics page produced redacted component/session metadata;
- About/privacy/disclaimer content rendered;
- a WPF re-entrant close crash was reproduced from Windows Event Log, fixed by
  deferring the second close request, and direct shutdown then completed cleanly.

The official app's active signed-in session was intentionally not terminated
during development. Production runtime injection was instead verified with the
isolated CDP gate above. Therefore end-to-end interactions inside a real
signed-in conversation—copy buttons, composer input, and every tool/diff variant—
remain version-specific manual release checks.

## Dual-Codex visual acceptance

On 2026-07-27, the development-only acceptance controller was run while the
active controlling Codex session stayed open.

- Codex A remained PID `43664`, start time `15:43:01`, throughout the run.
- Run `2c112eac3dde42dca8e6cabb0fee46c0` started Codex B as PID `53228`
  with an isolated profile and private CDP pipe.
- Runtime `0.2.0-alpha.2`, protocol `1`, injected successfully.
- Computer Use directly observed the run marker, circular cover-cropped
  avatars, nicknames, Assistant prose bubble, untouched native User bubble,
  long/multi-paragraph wrapping, code, tool, Diff, status, and toolbar.
- Computer Use observed both the normal window and a `780 x 820` window without
  overflow, overlap, or abnormal truncation.
- Restart Target closed only PID `53228`, started PID `59216` with the same
  isolated profile, and restored the visible effect; Codex A remained alive.
- Disable/destroy visibly removed MyCodex avatars, nicknames, prose bubble, and
  markers while leaving the synthetic fixture and native surfaces visible.
- Stop removed PID `59216` and the run/profile directory. Computer Use then
  listed only Codex A's original window.
- The final machine-readable result remains in the temporary acceptance root as
  `<run-id>.final.json`; no real chat, DOM snapshot, Cookie, credential, or
  machine-specific path was added to Git.

See [`visual-acceptance.md`](visual-acceptance.md) for the repeatable command
chain. These PID values are evidence from this run, not stable configuration.

## Automated coverage

- .NET: configuration create/load/migration/corruption, calibration recovery,
  language persistence/validation, nickname validation, avatar
  validation/import, application discovery/scoring,
  stale Store-path resolution and side-by-side package collapsing,
  compatibility state, CDP message correlation, signature serialization/scoring,
  generated class filtering, structural fingerprint privacy, and Runtime
  session health rehydration.
- Runtime: idempotent install/destroy, prose-only decoration, native-surface
  preservation, dynamic mutation, generated-class changes, wrapper changes,
  attribute changes, current Codex structural turns, ambiguous-calibration
  precedence, native user-bubble preservation, circular avatar cover crops,
  root/style self-healing, Runtime hot upgrade, signature privacy, and
  composed-path calibration root.
- Visual acceptance safety: canonical temporary paths, private-pipe isolated
  profile arguments, exact PID/executable/start-time/profile ownership,
  rejection of another process using the same executable, and valid
  Start/Restart/Disable/Stop lifecycle transitions.

## Dependency downloads

npm dependencies were installed through `https://registry.npmmirror.com` after
respecting the user's preference for faster mainland-China-compatible sources.
NuGet restore used Huawei Cloud's NuGet v3 mirror after the default route was
too slow. Lockfiles and exact package versions remain the build inputs.
