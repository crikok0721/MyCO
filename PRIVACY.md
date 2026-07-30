# Privacy

MyCO is designed to run locally.

## Data MyCO does not collect

MyCO:

- does not upload conversations;
- does not require or request OpenAI credentials;
- does not read authentication cookies;
- does not intercept network requests;
- does not install analytics, crash reporting, or telemetry services;
- does not send configuration, diagnostics, or usage statistics to a project
  server.

There is no MyCO cloud service.

## Local data

MyCO stores the following under `%APPDATA%\Myco`:

- `config.json`: names and appearance settings;
- `calibration.json`: schema-versioned structural signatures;
- `avatars/`: validated copies of user-selected image files;
- `logs/`: privacy-filtered lifecycle and diagnostic events;
- `backups/`: damaged configuration files retained for recovery.

Calibration signatures contain tag names, semantic attributes, filtered class
tokens, ancestor/child structure, layout categories, capabilities, and an
anonymous structural fingerprint. They do not contain message text.

## In-memory page access

The injected runtime examines visible DOM structure to identify conversation
turns and distinguish prose from native code, diff, tool, status, toolbar, and
input surfaces. It may necessarily encounter page text in renderer memory while
classifying nodes, but it does not persist or transmit that text.

Avatars are converted to data URLs by the manager and supplied only to the
local renderer session.

## Diagnostics and logs

Allowed diagnostic fields include component versions, application metadata,
CDP transport (and loopback port only when used), target counts, match counts,
confidence, compatibility state, observer state, and exception type.

Message text, prompts, code content, email addresses, account identifiers,
tokens, cookies, authorization values, and unnecessary file paths are excluded
or redacted. Review any diagnostic file before attaching it to a public issue.

## Network behavior

MyCO communicates with Chromium DevTools Protocol over private inherited
pipes by default. If the user explicitly approves the fallback, it uses a
randomly selected `127.0.0.1` port and never a LAN interface. Dependency
downloads occur only while developers build the project; the published app has
no MyCO update or telemetry endpoint.

## Deletion

Exit MyCO, then delete `%APPDATA%\Myco` to remove its local configuration,
calibration, avatars, logs, and backups. This does not alter the official
Desktop installation.
