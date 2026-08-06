# Settings / 设置

## Interface language / 界面语言

The Manager and first-run window support English, Simplified Chinese,
Traditional Chinese, and Japanese (`en-US`, `zh-CN`, `zh-TW`, `ja-JP`). The
change is immediate and is persisted independently from unsaved appearance
edits. All four resource dictionaries keep identical keys and format
placeholders.

Manager 与首次启动页支持 English、简体中文、繁體中文和日本語。切换立即生效，
语言会独立持久化，不会顺带保存尚未提交的外观编辑。

## Manager theme / 启动器主题

The Manager theme is independent from the injected bubble theme.

- **Dark / 深色** and **Light / 浅色** force the WPF Manager palette.
- **System / 跟随 Windows** reads the current user's
  `AppsUseLightTheme` setting and listens for Windows preference changes.
- If the registry or system event source is unavailable, the Manager uses a
  stable dark fallback instead of failing startup.
- Codex bubbles always follow the active Codex renderer. Changing this setting
  never forces a renderer theme.

Manager 主题只控制 WPF 启动器。Codex 气泡始终独立跟随当前 Renderer；系统主题
读取或监听失败时，Manager 安全回退到深色。

## Bubble palettes / 气泡调色板

Appearance stores separate Dark and Light values for the Assistant bubble,
text, nickname, avatar background, and avatar border. User bubbles remain under
Codex's native theme. Normal Assistant text must have at least `4.5:1`
contrast against its effective bubble background before settings can be saved.

Schema 1's existing Assistant colors migrate to the Dark palette. The Light
palette receives readable defaults; names, avatar paths, layout, language, and
calibration are preserved.

## Bubble display / 气泡显示

- **Automatic grouping / 自动切割气泡** groups headings with following prose,
  coalesces related short paragraphs, and keeps lists and quotes atomic.
- **Whole response / 整段完整气泡** groups all contiguous safe prose.
- Code, tables, math, diffs, tools, status, and controls are structural
  barriers and remain native in both modes.

The selection is stored in the versioned configuration and applies immediately
after **Save & apply**. Config schema 7 preserves prior schema 0/1/2/3/4/5/6
settings and adds the optional launch-association state without losing user
choices.

## Appearance geometry / 外观几何

Schema-7 geometry sliders store relative deltas from baseline version 2. A zero
delta is the centered theoretical baseline: a 35px avatar, Assistant avatar Y
11px, and User avatar Y -4px. Existing schema-7 baseline-1 and schema-0..6
absolute values are migrated once through their effective geometry, so old
layouts are not silently treated as new relative deltas.

## Tray lifecycle / 托盘生命周期

- Minimize hides the main window and removes its taskbar button.
- Double-click the tray icon or choose **Open MyCO** to restore and focus it.
- Starting `MyCO.exe` again signals the existing hidden instance; it does not
  create a second Manager.
- Close offers **Exit MyCO**, **Minimize to notification area**, and
  **Cancel**. Exit releases CDP sessions, Runtime observers, theme/system
  listeners, and the notification icon but leaves Codex running.
- The tray menu exposes Open, Start/apply, verified Restart, and Exit.
- The minimize balloon appears once after each Windows boot, only after a
  user-triggered minimize. Background startup, reopening, and repeated state
  events do not show it.

最小化表示收纳到托盘；关闭时可选择退出、最小化或取消。退出 MyCO 不会关闭
Codex。再次运行程序会激活已有实例。

## Startup options / 启动选项

The startup card has three independent switches:

1. **Start MyCO when I sign in to Windows** writes exactly one fixed
   `MyCO` value under
   `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
   Its command is the quoted full executable path plus `--background`.
2. **Start Codex after MyCO starts** uses the existing private-pipe launch
   path when Codex is not running. A controlled session is reused. An
   uncontrolled running Codex is not duplicated; MyCO reports that state so
   the user can choose an interactive restart later.
3. **Associate MyCO with Codex launches** is off by default. It creates only
   MyCO-owned Start-menu/Desktop launch entries and a MyCO-owned launch link;
   existing official Codex shortcuts, pinned taskbar items, and other launch
   entries are not rewritten. Turning it off or restoring defaults removes
   only exact MyCO-owned entries.

When the first two switches are enabled, sign-in starts MyCO hidden with a usable
tray icon and then performs the non-interactive Codex check. TCP fallback never
opens a consent dialog during background startup. A failed registration rolls
the setting back and shows a privacy-safe error. Moving the self-contained
folder is detected and corrects the Run command on the next start.

这三个开关互不依赖。开机项只写当前用户的固定 `MyCO` 值，不请求管理员权限，
关闭时也只精确删除该值。Codex 关联默认关闭，只创建 MyCO 自己的启动入口；
MyCO 不修改官方 Codex 快捷方式、任务栏固定项、安装目录或用户资料。

## Check for updates / 检查更新

The update card checks only the latest formal release in the official
`crikok0721/MyCO` repository. It ignores drafts and previews, reports offline,
timeout, rate-limit, and invalid-release states separately, and does not
download anything when **Later** is chosen. **Update now** accepts only the
exact x64 ZIP and matching SHA-256 asset, stages it below `%TEMP%\MyCO\Updates`,
and uses the project-owned external updater. The updater verifies the running
MyCO PID, path, and start time, keeps AppData and Codex data untouched, and
rolls back if replacement or verification fails.

## Brand migration / 品牌迁移

If `%APPDATA%\Myco` does not exist and the legacy `%APPDATA%\MyCodex` directory
does, MyCO copies the complete legacy directory through a same-parent staging
directory and then atomically adopts the copy. Existing data under `Myco` is never
overwritten, migration failures do not block startup, and the legacy directory
is never deleted. Avatar paths are rewritten only when the corresponding copied
file exists. A privacy-safe result is written to
`%APPDATA%\Myco\logs\brand-migration.log`.

The `HKCU\...\Run` value uses the visible name `MyCO`. If a transitional
`Myco` value or legacy `MyCodex` value is present, startup reconciliation reads
it, writes and verifies the current value, then removes only those known prior
values to avoid duplicate login launches.

## Restore defaults / 恢复默认设置

The red action at the bottom of Settings opens a localized confirmation with
Cancel as the default. After confirmation MyCO destroys its Runtime without
closing Codex, removes only the known `MyCO`, `Myco`, and `MyCodex` login-startup
values, and transactionally stages `config.json`, `calibration.json`, managed
avatars, privacy-safe logs, and backups. The `%APPDATA%\Myco` root remains in
place so the preserved legacy source is not imported again. Reparse points and
paths outside the data root are rejected. A failure restores staged data and
the prior startup registration instead of reporting success.

红色危险操作会先显示确认对话框，且默认焦点为“取消”。确认后只重置 MyCO 自己
管理的设置、校准、头像、日志、备份和已知登录启动项；不会关闭 Codex，也不会删除
MyCO 安装文件、旧版迁移源目录、Codex 用户资料、聊天或凭据。完成后会重新载入
默认主题与打包 Logo，并再次显示欢迎页。
