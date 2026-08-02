# Locale-Aware CJK Fonts Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

Goal: Make Manager, tray, and Runtime-generated text use one canonical
locale-aware font mapping with correct SC/TC/JP glyph fallback and synchronized
culture/language state.

Architecture: Add one Manager-side LocaleFontCatalog that owns WPF/CSS/tray
font data. LocalizationService applies that profile through dynamic WPF
resources and culture settings; shared styles and all windows inherit it. The
existing Runtime config carries the canonical locale so its generated nickname
CSS and html.lang stay synchronized without changing the persisted config
schema.

Tech Stack: .NET 8 WPF, WinForms NotifyIcon, C# xUnit, TypeScript strict mode,
Node test runner, jsdom, esbuild-generated Runtime bundle.

## Global Constraints

- Canonical locales are exactly en-US, zh-CN, zh-TW, and ja-JP.
- zh-CN must not define JP/TC families before or alongside its own primary CJK fallback; the same isolation applies to zh-TW and ja-JP.
- Prefer Windows 10/11 system fonts; do not add packaged CJK font files.
- Preserve existing layout, colors, corner radii, spacing, controls, and Runtime safety boundaries.
- Do not edit src/MyCO.Runtime/dist/MyCO.runtime.js by hand; regenerate it with npm run check.
- Preserve unrelated user files and do not commit, push, create a PR, or release.

---

### Task 1: Add failing locale/font regression tests

Files:

- Create: tests/MyCO.Tests/LocaleFontCatalogTests.cs
- Modify: tests/MyCO.Tests/LocalizationResourceTests.cs
- Modify: tests/MyCO.Tests/ConfigurationTests.cs
- Modify: src/MyCO.Runtime/tests/runtime.test.ts

Interfaces:

- Consumes: the future LocaleFontCatalog.For(string) profile, LocalizationService.CurrentLocale, and Runtime defaultConfig().
- Produces: assertions for exact locale isolation, culture synchronization, dynamic WPF font/language resources, Runtime CSS/html locale propagation, cleanup, and serialized language.

- [x] Step 1: Write the C# red tests.

  Add inline cases for all four locales. Assert each profile contains the
  required ordered families and excludes the three wrong-region groups. Add a
  culture test that saves current culture values, calls
  LocalizationService.ApplyLanguage("zh-CN"), asserts CurrentLocale,
  CurrentCulture, and CurrentUICulture are zh-CN, then restores the saved
  values. Add source-structure assertions that shared WPF styles use
  DynamicResource UiFontFamily, that a Window style uses UiLanguage, and that
  the old unconditional Yu Gothic UI, Meiryo UI, Microsoft YaHei UI stack is
  gone. Extend Runtime serialization coverage with language == "zh-TW".

- [x] Step 2: Run the targeted .NET tests and confirm the expected red failure.

    dotnet test .\tests\MyCO.Tests\MyCO.Tests.csproj -c Release --filter "FullyQualifiedName~LocaleFontCatalogTests|FullyQualifiedName~LocalizationResourceTests|FullyQualifiedName~ConfigurationTests"

  Expected: failure because LocaleFontCatalog and the new synchronization
  behavior do not exist yet; existing unrelated tests must not be changed to
  make the new assertions pass.

- [x] Step 3: Write the TypeScript red tests.

  Add a test that applies defaultConfig() with each locale, checks
  document.documentElement.lang, checks the CSS variable contains the locale's
  SC/TC/JP stack and no wrong-region family, and checks the style element
  references the locale variable rather than system-ui. Add a cleanup assertion
  that destroy() restores the original lang attribute and removes the locale
  CSS variable.

- [x] Step 4: Run the focused Runtime test and confirm the expected red failure.

    Push-Location .\src\MyCO.Runtime
    npm test -- --test-name-pattern="locale|font|lang"
    Pop-Location

  Expected: failure because RuntimeConfig has no locale field and the style
  manager still emits system-ui.

### Task 2: Implement the central locale font catalog and WPF synchronization

Files:

- Create: src/MyCO.Manager/Localization/LocaleFontCatalog.cs
- Modify: src/MyCO.Manager/Localization/LocalizationService.cs
- Modify: src/MyCO.Manager/App.xaml.cs
- Modify: src/MyCO.Manager/Themes/DesignTokens.xaml
- Modify: src/MyCO.Manager/Themes/SharedStyles.xaml

Interfaces:

- Consumes: LanguageCodes.Normalize and the failing tests from Task 1.
- Produces: LocaleFontProfile, LocaleFontCatalog.For, and dynamic resources named UiFontFamily and UiLanguage.

- [x] Step 1: Implement the four immutable catalog profiles.

  Store the exact ordered WPF stack, CSS stack, and preferred tray families for
  each canonical locale. Normalize all lookup input through
  LanguageCodes.Normalize; never accept zh, cn, tw, jp, or display names as
  final values.

- [x] Step 2: Make ApplyLanguage update locale, culture, UI resources, and event state.

  Set CurrentLocale and the compatibility CurrentLanguage alias, both current
  culture properties, and both default-thread culture properties before
  notifying listeners. When an application exists, assign Resources[UiFontFamily]
  to the profile's WPF FontFamily, assign Resources[UiLanguage] to
  XmlLanguage.GetLanguage(normalized), and replace only the localized dictionary.
  Keep the no-Application path usable for tests by still applying canonical
  culture state.

- [x] Step 3: Initialize English before any startup window.

  Call LocalizationService.ApplyLanguage(LanguageCodes.English) before
  TryApplyStoredLanguage() in App.OnStartup. Change the design-token bootstrap
  value to the English stack only; locale-specific stacks come from the central
  catalog.

- [x] Step 4: Make all shared typography dynamic and window-wide.

  Replace every StaticResource UiFontFamily in shared control styles with
  DynamicResource UiFontFamily. Add a default Window style with dynamic
  Language="{DynamicResource UiLanguage}", UseLayoutRounding=True, and
  SnapsToDevicePixels=True. Do not touch icon-font, diagnostics-code-font,
  image-crop, or temporary button-animation transforms.

- [x] Step 5: Run the focused C# tests and then the existing .NET suite.

    dotnet test .\tests\MyCO.Tests\MyCO.Tests.csproj -c Release

### Task 3: Synchronize tray and embedded Runtime typography

Files:

- Modify: src/MyCO.Manager/Services/TrayService.cs
- Modify: src/MyCO.Manager/ViewModels/MainWindowViewModel.cs
- Modify: src/MyCO.Core/Injection/RuntimeConfigSerializer.cs
- Modify: src/MyCO.Runtime/src/types.ts
- Modify: src/MyCO.Runtime/src/runtime.ts
- Modify: src/MyCO.Runtime/src/style-manager.ts
- Modify: src/MyCO.Runtime/tests/runtime.test.ts
- Modify: tools/MyCO.VisualAcceptance/AcceptanceFixture.cs

Interfaces:

- Consumes: LocaleFontCatalog profiles and existing AppConfig.Language.
- Produces: Runtime LanguageCode, serialized language, locale CSS variable,
  safe html.lang lifecycle, a locale-aware tray Font, and immediate Runtime
  refresh when the Manager language selection changes.

- [x] Step 1: Add canonical language to Runtime config and serialization.

  Define the four-value LanguageCode union, add required language to
  RuntimeConfig, set English in defaultConfig, validate the four values, and
  serialize LanguageCodes.Normalize(config.Language) as language. Do not bump
  the persisted config schema because this is a derived Runtime field.

- [x] Step 2: Apply and clean up the Runtime locale state.

  Map each LanguageCode to the same ordered CSS stack. Set --mc-font-family
  and document.documentElement.lang during style install and config apply.
  Capture the pre-existing lang exactly once per document and restore it on
  destroy(); remove the CSS variable with the other MyCO variables. Use
  font: 400 13.5px/18px var(--mc-font-family) and retain letter-spacing: 0.

- [x] Step 3: Apply locale-aware font to the WinForms tray.

  Add a disposable _menuFont field. On construction and every language change,
  choose the first installed preferred family from the current catalog; if none
  exists, use SystemFonts.MessageBoxFont.FontFamily.Name. Apply the resulting
  font to the ContextMenuStrip and dispose the old font on replace and shutdown.
  Keep existing localized text and theme behavior unchanged.

- [x] Step 4: Remove the remaining product system-ui fixture fallback.

  Change the English-only VisualAcceptance fixture CSS to the Windows English
  stack so repository searches no longer identify it as an uncontrolled
  product-like fallback. Leave historical SVG design references unchanged.

- [x] Step 5: Run Runtime red/green checks and regenerate the bundle.

    Push-Location .\src\MyCO.Runtime
    npm run check
    Pop-Location

  Expected: TypeScript lint, all Runtime tests, and generated bundle complete;
  dist/MyCO.runtime.js changes only through the build.

### Task 4: Record the fix and perform full verification

Files:

- Modify: docs/DEVELOPMENT_LOG.md
- Modify: docs/CONTEXT.md only if the current validation baseline or active version statement needs to reflect this development pass

Interfaces:

- Consumes: green targeted tests, Runtime check, Release build/test output, and smoke evidence.
- Produces: a concise dated engineering record with cause, impact, modified modules, and verification limits.

- [x] Step 1: Run the full project checks.

    Push-Location .\src\MyCO.Runtime
    npm ci
    npm run check
    Pop-Location
    dotnet build .\MyCO.sln -c Release
    dotnet test .\MyCO.sln -c Release --no-build
    git diff --check

- [x] Step 2: Start the built Manager in an isolated build-output smoke session.

  Launch the Release executable with a temporary APPDATA directory and no
  Codex target required. Confirm startup does not crash, then exercise
  zh-CN → ja-JP → zh-CN, zh-CN → zh-TW, zh-TW → en-US, and en-US → zh-CN;
  reopen Settings and a dialog after each switch. Confirm the tray menu text
  refreshes and the process can exit cleanly. Preserve the existing user's real
  APPDATA\Myco data.

- [x] Step 3: Validate CJK regression strings and state persistence.

  Check the Settings/reset strings plus 骨 门 直 置 关 开 图 语 in zh-CN, the
  specified TC strings in zh-TW, the specified Japanese strings in ja-JP, and
  the English strings in en-US. Close/restart with the temporary config and
  verify locale, WPF resource, and Runtime serialization remain aligned.
  Record that screenshot/visual glyph acceptance still needs a real Windows
  10/11 disposable-session check if it cannot be observed from this environment.

- [x] Step 4: Update DEVELOPMENT_LOG.md and inspect the final diff.

  Record the confirmed bad stack/static-resource/culture causes, the four
  locale strategies, the absence of product text transforms or unsupported
  weights, and exact commands/results. Review git status, git diff --stat, and
  git diff --check; do not stage or commit.
