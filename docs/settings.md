# Settings / 设置

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

The selection is stored in Config schema 3 and applies immediately after
**Save & apply**. Existing schema 0/1/2 configurations migrate to Automatic.

## Tray lifecycle / 托盘生命周期

- Minimize hides the main window and removes its taskbar button.
- Double-click the tray icon or choose **Open MyCodex** to restore and focus it.
- Starting `MyCodex.exe` again signals the existing hidden instance; it does not
  create a second Manager.
- Close offers **Exit MyCodex**, **Minimize to notification area**, and
  **Cancel**. Exit releases CDP sessions, Runtime observers, theme/system
  listeners, and the notification icon but leaves Codex running.
- The tray menu exposes Open, Start/apply, verified Restart, and Exit.

最小化表示收纳到托盘；关闭时可选择退出、最小化或取消。退出 MyCodex 不会关闭
Codex。再次运行程序会激活已有实例。

## Startup options / 启动选项

The two switches are independent:

1. **Start MyCodex when I sign in to Windows** writes exactly one fixed
   `MyCodex` value under
   `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
   Its command is the quoted full executable path plus `--background`.
2. **Start Codex after MyCodex starts** uses the existing private-pipe launch
   path when Codex is not running. A controlled session is reused. An
   uncontrolled running Codex is not duplicated; MyCodex reports that state so
   the user can choose an interactive restart later.

When both switches are enabled, sign-in starts MyCodex hidden with a usable
tray icon and then performs the non-interactive Codex check. TCP fallback never
opens a consent dialog during background startup. A failed registration rolls
the setting back and shows a privacy-safe error. Moving the self-contained
folder is detected and corrects the Run command on the next start.

这两个开关互不依赖。开机项只写当前用户的固定 `MyCodex` 值，不请求管理员权限，
关闭时也只精确删除该值。MyCodex 不修改官方 Codex 快捷方式、协议关联、App
Execution Alias 或安装目录。
