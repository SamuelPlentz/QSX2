# Changelog — QuickSearch eXtended 2 (QSX2)

All notable changes to this project will be documented in this file.

---

## 📦 QuickSearch eXtended 2 — Version 2026-11-27

### 🛡️ Startup, Compatibility & Diagnostics

- **Mark of the Web Recovery:** Added automatic recovery when loading `tcmatch.Core.dll` fails due to a Windows security mark. QSX2 attempts to remove the mark. If loading still fails, an error message provides troubleshooting instructions, including manual unblocking.
- **WebView2 Initialization Recovery:** Added exception handling for WebView2 initialization failures. Errors are logged, and the Documentation and Personal Note views display a localized fallback message with a link to the online GitHub version.
- **Bridge Logging Warning:** Added a performance warning in the Search Assistant whenever optional native C++ bridge logging is enabled. The warning explains how to disable it via `tcmatch.path.txt`.

### 📂 WebView2 Runtime

- **Fixed Version WebView2 Runtime:** Added optional `WebView2Runtime` path configuration via `tcmatch.path.txt`. This allows portable installations to use a locally deployed Microsoft WebView2 Fixed Version Runtime instead of relying on a system-installed Runtime. Normally, no additional configuration is required.

### 🎨 User Interface & Usability

- **Configuration Window Icon:** Added the QSX2 application icon and applied it to the configuration window and the taskbar.
- **Window Management:** Changed the configuration window to a normal, non-TopMost window. The Search Assistant's TopMost state is temporarily disabled while the configuration window is open and restored when it closes.
- **Improved Tooltips:** Changed tooltips to use left-aligned, wrapping text instead of the default justified alignment.

### 🌐 Localization

- **Russian Language Support:** Added a complete Russian UI translation contributed by AntonyD. Added Russian to the language selection and credited the translator in both README files.
- **New translation strings (DE, EN and RU):** `003_002_005`, `003_002_006`, `100_003_012`, `100_003_013` The four new strings were added to the German and English translations and included in the new Russian translation.

### 📖 Documentation

Updated both the German and English READMEs.

**Major additions and changes:**

- **Chapter 6.2 – Directory Redirection:** Expanded the documentation with a one-line portable `DataFolder` setup, the syntax and rules of `tcmatch.path.txt`, and the optional `WebView2Runtime` path configuration.
- **Chapter 7.4 – Startup Problems:** Added troubleshooting instructions for Windows Mark of the Web, including the automatic recovery mechanism and manual unblocking procedure.

**Pointwise additions and corrections:**

- **Chapter 3.5 – Replacement Rules:** Clarified how to disable rules and use comment lines in `tcmatch.replacements.txt`.
- **Chapter 6 – Installation:** Clarified the required Total Commander restart procedure when updating the plugin and the need to avoid reopening Quick Filter before replacing the DLLs.
- **Chapter 6.2 – Directory Redirection:** Added the multi-user explanation and updated the examples for `BrowserProfile` and `WebView2Runtime`.
- **Chapter 7.3 – Troubleshooting Checklist:** Added instructions for enabling additional bridge logging and including the bridge log in bug reports.
- **Chapter 8.1 – Contributors:** Added AntonyD as the Russian translator.

---

## 📦 QuickSearch eXtended 2 — Version 2026-11-21

### 🛡️ Startup, Compatibility & Diagnostics

- **Startup Recovery & Safe Mode (Issue #1):** Added automatic recovery after failed startup attempts. A startup sentinel detects interrupted first search requests and activates Safe Mode on the next start, using a separate `tcmatch.debug.xml` configuration. Added an expert setting to disable automatic Safe Mode activation.
- **Floating-Point Compatibility:** Added an optional compatibility mode to address potential floating-point exception issues.
- **Improved Logging:** Added fallback logging to `%TEMP%` if the regular log file cannot be written.
- **Native Bridge Logging:** Added optional diagnostic logging for the native C++ bridge via `tcmatch.bridge.log` (see Flexible Directory Redirection), helping investigate problems that occur before the C# component is initialized.
- **Experimental MatchNotify Support:** Added and exported the `MatchNotify` callback through the C++ bridge and C# implementation. This integration is still experimental: no released Total Commander version supports it yet. Christian Ghisler has promised support in a future version.

### 📂 Configuration & Directory Redirection

- **Flexible Directory Redirection (Issue #6):** Renamed `AppFolder` to `PluginFolder` and extended `tcmatch.path.txt` to support independent redirection of configuration files, logs, replacement rules, and the WebView2 browser profile.
- **Configuration Templates:** Added `tcmatch.default.xml` to provide an initial configuration and `tcmatch.overwrite.xml` to enforce a configuration on every startup.

### 📐 User Interface & Usability

- **Settings Window (Issue #3):** Fixed window sizing to respect the available monitor work area, ensuring that controls remain accessible. Improved initial window activation and fixed closing the window with ESC.

### 📖 Documentation

**Major changes:**

- **Chapter 1 – Introduction:** Added light and dark mode screenshots.
- **Chapter 4 – The Interactive Search Assistant (Issue #8):** Significantly expanded the documentation with dedicated sections explaining all Search Assistant categories, their purpose, and their interactions with the Quick Filter dialog.
- **Chapter 6.2 – Directory Redirection (Issue #6):** Completely revised and expanded the documentation to explain `PluginFolder`, `DataFolder`, individual path overrides, configuration templates, and supported entries.
- **Chapter 7.4 – Startup Problems (Issue #1):** Added a new section covering automatic Safe Mode recovery, troubleshooting, and diagnostic logs.

**Additional corrections and improvements:**

- **Chapter 2 – Search Syntax:** Corrected and clarified examples, escaping terminology, and wording.
- **Chapter 6 – Installation:** Clarified that Total Commander must be restarted after changing the `wincmd.ini` configuration.
- **Chapter 7.1 – Operation & Limits:** Clarified the current limitations of the Total Commander plugin interface, including notification support.
- Updated wording, formatting, links, and examples throughout the German and English READMEs.

### 🤝 Developer & Repository Improvements

- **Contributor Guide:** Added `CONTRIBUTING.md` with information about translations, code structure, building, testing, and contribution guidelines.
- **GitHub Language Detection:** Added `.gitattributes` to improve language classification and exclude project and resource files from repository statistics.
- **Dependencies** Updated Markdig from 1.4.0 to 1.6.0. Updated Microsoft.Web.WebView2 from 1.0.4191.47 to 1.0.4370.26.

---

## 📦 QuickSearch eXtended 2 — Version 2026-10-01

Complete ground-up rewrite in .NET / C# with modern UI architecture.

### 🎨 Major Features & Highlights

- **Modern Appearance:** Full Dark Mode support, custom color themes, and UI customization.
- **Interactive Search Assistant:** Overhauled floating window with live search visualization, search history, customizable screen anchoring/positioning, modular collapsible sections, tooltips, and context menus.
- **Built-in Search Assistant:** The previous search assistant was provided externally via AutoHotkey, requiring a separate thread and occasionally being incorrectly flagged by antivirus software. The new search assistant is fully integrated into the plugin DLL.
- **Redesigned Settings & Built-in Tools:** Integrated documentation viewer, log manager, custom translation engine, custom themes system, and full control over every syntax character.
- **Self-Explanatory Options:** Explanatory banners and detailed description texts explain settings directly on the spot.
- **Flexible Directory Redirection:** Separated `PluginFolder` (previously `AppFolder`) and `DataFolder` with path overriding via `tcmatch.path.txt` and automatic fallback to `%TEMP%`.
- **New Official Download Location:** The QSX1 download folder was unfortunately not reliably accessible for many years. I apologize for the inconvenience. QSX2 is now hosted and distributed through the GitHub repository.

### 🔍 Search Engine & Syntax Enhancements

- **New Pattern Search (`%`):** Intuitive wildcard mode supporting digits (`#`), letters (`@`), ranges (`[2013..2026]`), sets (`[abc]`/`[!abc]`), and word lists (`(a,b)`).
- **Upgraded Regex Engine:** Replaced `deelx` with the native .NET Regular Expression engine for higher reliability.
- **New Modifiers & Controls:**
    - `~` : Toggles case sensitivity for specific conditions.
    - `^` / `$` : Enforces match at exact start / end of filename or word (with auto-match option for first term).
    - `/` : Local OR selection within a single condition (e.g., `jpg/png/gif`).
    - `\` and `"..."` : Escapes individual characters or entire text blocks.
- **Search Cancellation:** Instant cancellation via `ESC` or `Pause` keys (fixes QSX1 cancellation bug).
- **Text Replacement Options:** New option to ignore accents/diacritics (`a` matches `ä`/`á`).

### 🏷️ Metadata & WDX Integration

- **Metadata Query Tags (`@tag`):** Switch context to specific metadata fields (every metadata alias/shortcut can be freely renamed or disabled):
    - `@gui` : Immediately opens search assistant & settings.
    - `@age` : Filters by modification date (`>14d`, `<2h`, `=3d`).
    - `@size` : Filters by file size (`>200MB`, `<1GB`).
    - `@name` / `@ext` : Targets filename or file extension specifically.
    - `@folder` / `@path` : Targets parent folder name or full path.
    - `@attr` : Filters by Windows file attributes (`d`, `r`, `h`, `s`, etc.).
    - `@desc` : Searches `descript.ion` comments.
    - `@content` : Full-text file content search.
- **WDX Plugin Integration:** Map custom `@tags` to external Total Commander `.wdx` plugins (supports relative paths & env variables).
- **Per-Scope Defaults:** Configurable default metadata tag per view scope (e.g., `@path` by default for history/tab paths, `@name` for file list).

### 📖 Documentation

- You all wanted it: Extensive documentation has been added! 😉
