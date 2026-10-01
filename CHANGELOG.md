# Changelog — QuickSearch eXtended 2 (QSX2)

All notable changes to this project will be documented in this file.

---

## QuickSearch eXtended 2 — Version 2026-10-01

Complete ground-up rewrite in .NET / C# with modern UI architecture.

### 🎨 Major Features & Highlights

- **Modern Appearance:** Full Dark Mode support, custom color themes, and UI customization.
- **Interactive Search Assistant:** Overhauled floating window with live search visualization, search history, customizable screen anchoring/positioning, modular collapsible sections, tooltips, and context menus.
- **Redesigned Settings & Built-in Tools:** Integrated documentation viewer, log manager, custom translation engine, custom themes system, and full control over every syntax character.
- **Self-Explanatory Options:** Explanatory banners and detailed description texts explain settings directly on the spot.
- **Flexible Directory Redirection:** Separated `AppFolder` and `DataFolder` with path overriding via `tcmatch.path.txt` and automatic fallback to `%TEMP%`.

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
