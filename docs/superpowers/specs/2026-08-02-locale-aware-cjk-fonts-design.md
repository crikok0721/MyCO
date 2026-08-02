# MyCO Locale-Aware CJK Font Design

## Problem and confirmed cause

MyCO Manager is a .NET 8 WPF application with WinForms tray controls and an
embedded TypeScript Runtime. The Manager currently defines one global WPF font
stack in DesignTokens.xaml:

Segoe UI Variable Text, Segoe UI, Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Microsoft JhengHei UI

That stack is shared by every locale and places Japanese families before both
Simplified and Traditional Chinese families. WPF therefore has a valid path to
select Japanese glyph variants for Simplified Chinese characters such as 置.
SharedStyles.xaml also resolves that resource statically, while
LocalizationService only replaces string dictionaries and CurrentUICulture.
Existing controls, newly opened windows, and the Runtime's generated nickname
therefore do not share one locale/font state.

## Design

LocaleFontCatalog is the single source of truth for the four canonical
locales. Each profile provides the WPF family stack, CSS family stack, and
preferred tray families:

- en-US: Segoe UI Variable → Segoe UI → Arial → sans-serif
- zh-CN: Segoe UI Variable → Microsoft YaHei UI → Microsoft YaHei → Noto Sans CJK SC → Noto Sans SC → sans-serif
- zh-TW: Segoe UI Variable → Microsoft JhengHei UI → Microsoft JhengHei → Noto Sans CJK TC → Noto Sans TC → sans-serif
- ja-JP: Segoe UI Variable → Yu Gothic UI → Yu Gothic → Meiryo → Noto Sans CJK JP → Noto Sans JP → sans-serif

LocalizationService.ApplyLanguage normalizes the input through LanguageCodes,
updates CurrentLocale, CurrentCulture, CurrentUICulture, and both default
thread culture values, then updates the application resources UiFontFamily and
UiLanguage. Shared WPF styles use DynamicResource for UiFontFamily, and a
shared Window style supplies Language, layout rounding, and pixel snapping to
all current and future windows. Existing design tokens, sizes, colors, and
visual geometry remain unchanged.

The tray is a separate WinForms surface, so TrayService selects the first
installed preferred family for the current locale and applies it to the
ContextMenuStrip. It refreshes whenever LanguageChanged fires and disposes the
previous GDI font.

The Runtime config carries the current canonical language code. Language
selection persists that value and reapplies the connected Runtime immediately.
Its style manager maps the code to the same CSS stack, sets html.lang while the
Runtime is active, and restores the prior lang attribute during destroy(). The
generated nickname uses the locale CSS variable. Runtime validation rejects
unsupported locale values, and the generated bundle is rebuilt from TypeScript
source.

## Error handling and compatibility

Unsupported language values continue to be rejected by the existing
LanguageCodes boundary. Missing or unavailable preferred fonts do not fail
startup: WPF's ordered stack continues to the next family, and the tray falls
back to the Windows message-box font if none of its preferred families is
installed. No CJK font files are added to the package. Existing persisted
configuration remains unchanged; the Runtime language field is derived from
the existing Manager language setting and does not change the config schema.

## Verification

Automated checks cover exact locale stacks, forbidden cross-region families,
canonical culture synchronization, dynamic WPF font/language resource usage,
Runtime CSS and html.lang updates, Runtime cleanup, and serialization of the
locale. The existing full Runtime check and Release .NET build/test commands
remain the primary gates. A real Manager executable smoke run checks startup,
language changes, new dialogs, tray text/font refresh, restart persistence, and
the CJK regression strings; automated tests remain supporting evidence for the
visual glyph result.
