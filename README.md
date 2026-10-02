# 🔍 QuickSearch eXtended 2

- 🏠 **[Project Page & Source Code](https://github.com/SamuelPlentz/QSX2)**
- 📜 **[Changelog](https://github.com/SamuelPlentz/QSX2/blob/main/CHANGELOG.md)**
- 🐞 **[Report Issues & Suggest Features](https://github.com/SamuelPlentz/QSX2/issues)**
- 💬 **[Discussion on Total Commander Forum](https://www.ghisler.ch/board/viewtopic.php?t=88089)**

---

<a id="table-of-contents"></a>
# 📖 Table of Contents

- 🚀 [1. Introduction](#introduction)
- 🧠 [2. Search Syntax](#search-syntax)
    - 💡 [2.1 Core Idea](#core-idea)
    - 🧬 [2.2 Anatomy of a Search Query](#anatomy-of-a-search-query)
    - 🔍 [2.3 Search Modes](#search-modes)
        - 🔍 [2.3.1 Standard Search (`=`)](#standard-search)
        - 🔍 [2.3.2 Regex Search (`?`)](#regex-search)
        - 🔍 [2.3.3 Pattern Search (`%`)](#pattern-search)
        - 🔍 [2.3.4 Fuzzy Search (`<`)](#fuzzy-search)
        - 🔍 [2.3.5 Sequence Search (`*`)](#sequence-search)
    - 🏷️ [2.4 Metadata](#metadata)
    - 🛡️️ [2.5 Escaping Characters (`\` and `"..."`)](#escaping-characters)
    - 🎯 [2.6 Practical Search Examples](#practical-search-examples)
- ⚡ [3. Preprocessing & Character Equivalence](#preprocessing-character-equivalence)
    - 🔄 [3.1 How Preprocessing Works](#how-preprocessing-works)
    - 🔀 [3.2 How Character Equivalence Works](#how-character-equivalence-works)
    - 🌏 [3.3 Chinese Search (PinYin)](#chinese-search-pinyin)
    - 🌏 [3.4 Korean Search (Hangul)](#korean-search-hangul)
    - 📝 [3.5 Replacement Rules](#replacement-rules)
    - 🧩 [3.6 Templates](#templates)
- 💡 [4. The Interactive Search Assistant](#the-interactive-search-assistant)
- ⚙️ [5. Settings](#settings)
    - 🌍 [5.1 Custom Translations](#custom-translations)
    - 🎨 [5.2 Custom Themes](#custom-themes)
- 📦 [6. Installation](#installation)
    - 📂 [6.1 Alternative Installation Folder](#alternative-installation-folder)
    - 📂 [6.2 Directory Redirection](#directory-redirection)
- 🛠️ [7. System Limits & Troubleshooting](#system-limits-troubleshooting)
    - ⚙️ [7.1 Operation & Limits of the `tcmatch.dll` Interface](#operation-limits-of-the-tcmatch-dll-interface)
    - 🚧 [7.2 Known Limitations](#known-limitations)
    - 📋 [7.3 Troubleshooting Checklist](#troubleshooting-checklist)
- 📜 [8. Credits & Legal](#credits-legal)
    - 👥 [8.1 Contributors](#contributors)
    - 📦 [8.2 Acknowledgments](#acknowledgments)
    - ⚖️ [8.3 License Terms](#license-terms)
- ✍️ [9. Personal Note](#personal-note)

---

<a id="introduction"></a>
# 🚀 1. Introduction

## 💡 What is QuickSearch eXtended 2?

**QuickSearch eXtended 2** is a highly flexible quick search plugin for the **Quick Filter dialog** (Ctrl+S) of **[Total Commander](https://www.ghisler.com/)**. It transforms simple text input into a powerful search tool to filter files and folders lightning-fast according to complex criteria.

The multi-layered search syntax seamlessly combines standard text filters, regular expressions, fuzzy search, and metadata queries in a single line, without requiring complex parentheses.

Whether filtering by patterns, file age, file size, content, or WDX content plugins — the plugin interprets input in real time and provides direct feedback on the constructed search logic via the **interactive search assistant**.

[📖 Back to top](#table-of-contents)

---

## ⭐ Key Features at a Glance

- 🧩 **Powerful combining logic:** Conditions can be freely combined using AND, OR, and NEGATION.
- 🔍 **5 flexible search modes:** Support for exact standard search, pattern search, sequence search, fuzzy search, and powerful regular expressions.
- 🏷️ **Extensive metadata filtering:** Targeted filtering by file name, extension, path, comment, full-text content, file age, file size, or file attributes.
- 🔌 **WDX plugin integration:** Fields from external Total Commander content plugins can be integrated directly (e.g., MP3 tags, EXIF data, PDF properties).
- 💡 **Interactive search assistant:** A live window displays the interpreted search structure, detected control characters, performance warnings, notes, and previous search history in real time.
- 🛠️ **Fully customizable:** Every single control character and metadata shortcut can be freely redefined or completely disabled in the configuration.
- 🔄 **Text replacements:** Built-in support for ignoring umlauts and accents, Chinese PinYin and Korean Hangul search, as well as custom replacement rules.
- 🎨 **Modern appearance:** Supports Dark Mode and enables color customizations. The plugin is multilingual and easy to translate.

[📖 Back to top](#table-of-contents)

---

<a id="search-syntax"></a>
# 🧠 2. Search Syntax

<a id="core-idea"></a>
## 💡 2.1 Core Idea

The order of search words does not matter. For example, searching for `invoice final` finds both `invoice_final.pdf` and `final_invoice.txt`.

> 💡 If an exact order needs to be enforced, this can be solved using pattern search (e.g., `%invoice*final` finds `invoice_final.pdf`, but excludes `final_invoice.txt`; see 🔍 [2.3.3 Pattern Search](#pattern-search)).

The engine splits the search string into independent paths. The following example illustrates this functionality:

```text
client xlsx/docx !rejected | invoice final
```

This single line is split into **two completely independent branches** separated by the global OR (`|`):

- **Branch 1 (3 conditions):**
    - `client` → Filename must contain `client`.
    - **AND**
    - `xlsx/docx` → Filename must contain `xlsx` **OR** `docx` (Local OR).
    - **AND**
    - `!rejected` → Filename must **NOT** contain `rejected` (Negation).
- **Branch 2 (2 conditions):**
    - `invoice` → Filename must contain `invoice`.
    - **AND**
    - `final` → Filename must contain `final`.

The following table shows how this example query is evaluated for different filenames:

| Match  | Filename                     | Matching Branch / Reason for Exclusion                                                                                                       |
| :---:  | :---                         | :---                                                                                                                                         |
| **✅** | `invoice_final.pdf`          | **Branch 2 matches:** Contains `invoice` AND `final`.                                                                                        |
| **✅** | `final_invoice.txt`          | **Branch 2 matches:** Contains `invoice` AND `final` in a different order.                                                                   |
| **✅** | `2026_client_report.docx`    | **Branch 1 matches:** Contains `client` AND `docx` and does not contain `rejected`.                                                          |
| **✅** | `final_client.xlsx`          | **Branch 1 matches:** Contains `client` AND `xlsx` and does not contain `rejected`.                                                          |
| **✅** | `final_invoice_rejected.txt` | **Branch 2 matches:** Contains `invoice` AND `final`. The word `rejected` is only excluded in Branch 1, so Branch 2 still matches this file. |
| **❌** | `client_rejected_sheet.xlsx` | **Excluded:** Contains `rejected`, which violates Branch 1. The keyword `invoice` required for Branch 2 is also missing.                     |
| **❌** | `rejected.png`               | **Excluded:** Most unfortunate file ever 😉.                                                                                                 |

[📖 Back to top](#table-of-contents)

---

<a id="anatomy-of-a-search-query"></a>
## 🧬 2.2 Anatomy of a Search Query

The input is read like a set of building blocks and processed from the largest units (branches) down to the smallest elements (terms):

- **Search string** = `Branch1|Branch2|Branch3|...`
    - **Global OR:** Splits the search using the `|` character into completely separate **branches**. If **any** single branch matches the file, it is found.
- **Branch** = `Condition1 Condition2 Condition3 ...`
    - **AND:** Separates individual **conditions** within a branch with a space (` `). A branch is fulfilled only if **all** of its conditions apply.
- **Condition** = `[!][~][^][$][Search mode]Term1/Term2/Term3/...`
    - **Negation:** An optional prefix with the `!` character that reverses the result. The file must **NOT** fulfill this condition.
    - **Toggle case sensitivity:** An optional prefix with the `~` character that toggles the global case sensitivity state for this specific condition. If the global setting is case-insensitive, this condition becomes case-sensitive and vice versa.
    - **Start of text:** An optional prefix with the `^` character that enforces that this condition matches at the exact start of the filename.
    - **End of text:** An optional prefix with the `$` character that enforces that this condition matches at the exact end of the filename (including file extension).
    - **Search mode:** An optional **control character** (see 🔍 [2.3 Search Modes](#search-modes)) that changes the processing method of the text (e.g., regex or fuzzy search).
    - **Local OR:** Allows a selection of alternative **terms** within a single condition using the `/` character. Only **one** of these terms must be contained (ideal for file extensions like `jpg/png/gif`).

Every control character can be fully customized or disabled in the settings.

[📖 Back to top](#table-of-contents)

---

<a id="search-modes"></a>
## 🔍 2.3 Search Modes

By default, every term of a condition is evaluated in the **Standard Search mode** defined in the settings. To specifically customize the evaluation for an individual condition, a corresponding **search mode control character** is prepended to it (see 🧬 [2.2 Anatomy of a Search Query](#anatomy-of-a-search-query)).

The following modes are available:

- 🔍 [2.3.1 Standard Search (`=`)](#standard-search)
- 🔍 [2.3.2 Regex Search (`?`)](#regex-search)
- 🔍 [2.3.3 Pattern Search (`%`)](#pattern-search)
- 🔍 [2.3.4 Fuzzy Search (`<`)](#fuzzy-search)
- 🔍 [2.3.5 Sequence Search (`*`)](#sequence-search)

> 💡 **Limitations:** In **Regex Search** (`?`) and **Pattern Search** (`%`), the local OR (`/`) cannot be used within the condition. Furthermore, most text replacements (PinYin search, Hangul search, ignoring umlauts/accents, as well as 1:1 mappings) are not available in these modes.

Every control character can be fully customized or disabled in the settings.

[📖 Back to top](#table-of-contents)

---

<a id="standard-search"></a>
### 🔍 2.3.1 Standard Search (`=`)

Searches for the exact **term** at any position within the filename.

- **Search mode control character:** `=`
- **Example:** `=report` finds `annual_report_2026.pdf`.

> 💡 By default, **Standard Search** is already set as the active mode in the settings. In this case, the control character `=` can be omitted (`report` instead of `=report`). It is only required if a different mode was chosen as the default in the settings and an exact search needs to be explicitly enforced for an individual condition.

[📖 Back to top](#table-of-contents)

---

<a id="regex-search"></a>
### 🔍 2.3.2 Regex Search (`?`)

Evaluates the **term** as a regular expression based on the [.NET Regular Expression Engine](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference). This allows highly flexible pattern matching and complex string filtering.

- **Search mode control character:** `?`
- **Example:** `?202[3-6].*Invoice` finds files containing a year from 2023 to 2026, followed by any characters and the word `invoice` (e.g., `2024_05_Invoice.pdf` or `2026-Company-Invoice.xlsx`).

[📖 Back to top](#table-of-contents)

---

<a id="pattern-search"></a>
### 🔍 2.3.3 Pattern Search (`%`)

Evaluates the **term** using a custom, intuitive syntax. It offers powerful wildcard searching with support for digits, characters, and ranges without requiring users to learn complex regular expressions. The various elements of this syntax are explained in detail below.

- **Search mode control character:** `%`
- **Example:** `%[2013..2026]*Invoice` finds files containing a year from 2013 to 2026, followed by any characters and the word `invoice` (e.g., `2014_05_Invoice.pdf` or `2026-Company-Invoice.xlsx`).

---

**🔹 Simple Wildcards**

| Character | Meaning                            | Example             | Match Example                                        |
| :---      | :---                               | :---                | :---                                                 |
| **`*`**   | Any number of characters (or none) | `%report*.txt`      | `Report.txt`, `Report_2026.txt`                      |
| **`?`**   | Exactly **1** arbitrary character  | `%image?.png`       | `image1.png`, `ImageA.png` *(but not `image12.png`)* |
| **`#`**   | Exactly **1 digit** (0–9)          | `%invoice_####.pdf` | `invoice_2026.pdf`                                   |
| **`@`**   | Exactly **1 letter**               | `%code_@@.dat`      | `Code_AB.dat`                                        |

---

**🔹 Character Selection `[abc]`**

Finds exactly one character from the specified selection within the brackets.

- **Example:** `%file_[abc].txt` finds `file_a.txt`, `file_b.txt`, or `file_c.txt`.

---

**🔹 Character Exclusion `[!abc]`**

Finds exactly one character that is **not** inside the brackets.

- **Example:** `%version_[!12].log` finds `version_3.log`, but not `version_1.log` or `version_2.log`.

---

**🔹 Word Selection `(Word1,Word2)`**

Searches for one of the specified words inside the parentheses (OR combination).

- **Example:** `%(vacation,trip)_2025` finds `vacation_2025.jpg` or `trip_2025.pdf`.

---

**🔹 Numeric and Date Ranges `[from..to]`**

Enables checking ascending value ranges for numbers, dates, versions, or other patterns containing digits.

**Rules:**

- **Range limits:** Can be closed (`[1..100]`) or open (`[2010..]` or `[..2026]`).
- **Length constraint:** Start and end values define the allowed number of digits per segment. `[7..]` searches single-digit (7–9), whereas `[007..]` searches strictly 3-digit (007–999).
- **Structural consistency:** All characters other than digits must match in exact positions at front and back (`[1-322.7..5-720.3]`).

**Examples:**

| Pattern                                 | Match Examples                                     | No Match                          |
| ---                                     | ---                                                | ---                               |
| `%image_[003..077].png`                 | `image_007.png`, `image_045.png`                   | `image_15.png`, `image_093.png`   |
| `%report_[7..103].pdf`                  | `report_9.pdf`, `report_42.pdf`, `report_042.pdf`  | `report_1.pdf`, `report_0042.pdf` |
| `%file_[7..].txt`                       | `file_7.txt`, `file_9.txt`                         | `file_08.txt`, `file_99.txt`      |
| `%file_[003..].txt`                     | `file_007.txt`, `file_500.txt`, `file_987.txt`     | `file_3.txt`, `file_1500.txt`     |
| `%app_v[7.32..12.3].exe`                | `app_v8.02.exe`, `app_v10.1.exe`, `app_v12.02.exe` | `app_v10.131.exe`, `app_v11.exe`  |
| `%archive_[1980-04-16..1983-09-18].tar` | `archive_1982-12-03.tar`, `archive_1980-05-99.tar` | `archive_1980-03-29.tar`          |
| `%log_[12.03.2024..19.07.2026].txt`     | `log_01.04.2024.txt`, `log_99.99.2025.txt`         | `log_15.05.2023.txt`              |
| `%volume-[1-part-12..3-part-02].dat`    | `Volume-1-part-17.dat`, `Volume-2-part-99.dat`     | `Volume-3-part-11.dat`            |

---

**🔹 Searching Special Characters as Literal Text**

To search for pattern search control characters (such as `#`, `@`, `*`, or `?`) as regular text, two methods are available:

The control character is enclosed in square brackets `[...]` as a single character, causing it to be interpreted literally.

- **Example:** `%file_[#]` finds strictly `file_#.txt`.

A prepended backslash (`\`) escapes the control function of the subsequent character:

- **Example:** `%file_\#` finds strictly `file_#.txt`.

> 💡 If the characters `\` and `"` are already configured for escaping in the settings, write either `%file_\\#` or `%"file_\#"` (see 🛡️ [2.5 Escaping Characters](#escaping-characters)).

[📖 Back to top](#table-of-contents)

---

<a id="fuzzy-search"></a>
### 🔍 2.3.4 Fuzzy Search (`<`)

Uses the [Levenshtein Distance](https://en.wikipedia.org/wiki/Levenshtein_distance) to reliably find terms even with typos or varying spellings.

- **Search mode control character:** `<`
- **Error tolerance:** To avoid false positives with short terms, the allowed deviation scales dynamically with the length of the search term. The thresholds for assigning allowed errors can be adjusted in the settings:
    - **3+ characters:** Maximum **1 error** (e.g., `<color` finds `colour.txt` despite the additional `u`).
    - **10+ characters:** Maximum **2 errors** (e.g., `<preasentatiom` finds `presentation.pdf` despite the additional `a` and the `m` instead of an `n`).
    - **20+ characters:** Maximum **3 errors**.

[📖 Back to top](#table-of-contents)

---

<a id="sequence-search"></a>
### 🔍 2.3.5 Sequence Search (`*`)

Enforces that all entered characters occur in the specified sequence within the filename. Unlike an exact search, the characters do not need to be directly adjacent.

- **Search mode control character:** `*`
- **Example:** `*dwn` finds `download.zip` (**d**o**wn**load) as well as `database_warning.png` (**d**atabase_**w**ar**n**ing).

[📖 Back to top](#table-of-contents)

---

<a id="metadata"></a>
## 🏷️ 2.4 Metadata

Metadata tags do not define **how** to search, but **where**. By default, the engine checks the filename. Inserting a metadata tag (such as `@age` or `@size`) triggers a context switch: **all subsequent conditions within this branch** filter this specific attribute instead of the name.

This context remains active until a new tag is declared or the branch ends. Each branch (separated by `|`) starts in the configurable default context: default is `@name` (filename) — except for searches in directory history and tab paths, where `@path` (file path) applies by default.

> 💡 All tag names can be freely customized to your language in the settings (e.g., `@größe` instead of `@size`).

---

### Metadata Tags for UI Control

**🔹 Metadata Tag `@gui`**

Control tag without filter function that immediately opens the search assistant even if the UI is disabled and provides access to the settings.

- **Example:** `report 2026 @gui` opens the search assistant directly while typing.

---

### Metadata Tags with Range Filter

These tags evaluate numeric values using the operators `=` (equal), `>` (greater than or equal to/older), and `<` (less than or equal to/younger), instead of using the standard 🔍 [2.3 Search Modes](#search-modes).

**🔹 Metadata Tag `@age`**

Filters by the last modification date of the file relative to the current time.

Available units: `s` (seconds), `m` (minutes), `h` (hours), `d` (days, default), `w` (weeks), `mo` (months, 30.436875 days), `y` (years, 365.2425 days).

- **Older (`>`):** `@age >14` finds files older than 14 days; `@age >2.5y` finds files older than 2.5 years; `@age >30m` finds files older than 30 minutes.
- **Younger (`<`):** `@age <7` finds files younger than 7 days; `@age <1.5h` finds files younger than 1.5 hours; `@age <1000s` finds files younger than 1000 seconds.
- **Equal (`=`):** `@age =3d` finds files with an age between 2.5 and 3.49999 days; `@age =3.0d` finds files with an age between 2.95 and 3.04999 days.
- **Chaining:** Expressions such as `@age >7d <1mo` can be freely combined within a branch.

**🔹 Metadata Tag `@size`**

Filters by file size in bytes.

Available units: `B` (bytes, default), `K`/`KB` (KB), `M`/`MB` (MB), `G`/`GB` (GB), `T`/`TB` (TB), `P`/`PB` (PB), `E`/`EB` (EB).

- **Larger (`>`):** `@size >700` finds files greater than or equal to 700 bytes; `@size >2.2G` finds files greater than or equal to 2.2 GB.
- **Smaller (`<`):** `@size <50M` finds files less than or equal to 50 MB; `@size <8000B` finds files less than or equal to 8,000 bytes.
- **Equal (`=`):** `@size =7M` finds files between 6.5 MB and 7.49999 MB.
- **Chaining:** Expressions such as `@size >200MB <1GB` can be freely combined within a branch.

---

### Metadata Tags with Standard Search Rules

These tags support the standard 🔍 [2.3 Search Modes](#search-modes), but evaluate the specified metadata field instead of the filename.

> 💡 **Example reference:** For illustration purposes, the file path `C:\Projects\Documents\Report.txt` is used below.

**🔹 Metadata Tag `@name`**

Checks exclusively the filename including extension (in the example: `Report.txt`). Ideal if path search is enabled in the settings, but a term should only apply to the filename.

- **Example:** `@name report` finds `Report.txt`, but ignores files inside a folder named `Report`.

**🔹 Metadata Tag `@ext`**

Checks exclusively the file extension (in the example: `txt`). Prevents false matches within the filename.

- **Example:** `@ext docx/xlsx/pdf` finds only files with these specific extensions.

**🔹 Metadata Tag `@folder`**

Checks exclusively the name of the immediate parent folder (in the example: `Documents`).

- **Example:** `@folder documents` finds files in the folder `Documents`, but ignores files that merely contain the word `documents` in their name.

**🔹 Metadata Tag `@path`**

Checks the complete absolute path including drive letter, folder structure, filename, and extension (in the example: `C:\Projects\Documents\Report.txt`). Ideal for finding files within a specific project structure.

- **Example:** `@path "\projects\2026\"` filters for files within this folder structure.

**🔹 Metadata Tag `@attr`**

Filters items based on Windows file attributes, from which the following 8-character attribute string is formed for each item:

`[f|d|i][r|-][a|-][h|-][s|-][c|-][e|-][l|-]`

| Position | Character                   | Meaning                                   |
| :---:    | :---:                       | :---                                      |
| **1**    | **`f`** / **`d`** / **`i`** | File, Directory, or Virtual Item (*Item*) |
| **2**    | **`r`** / **`-`**           | ReadOnly                                  |
| **3**    | **`a`** / **`-`**           | Archive                                   |
| **4**    | **`h`** / **`-`**           | Hidden                                    |
| **5**    | **`s`** / **`-`**           | System                                    |
| **6**    | **`c`** / **`-`**           | Compressed                                |
| **7**    | **`e`** / **`-`**           | Encrypted                                 |
| **8**    | **`l`** / **`-`**           | Symlink / Reparse Point (*Link*)          |

**Examples:**

- `@attr d` finds only folders.
- `@attr !h` finds items that are not hidden.
- `@attr r h` finds read-only and hidden items.
- `@attr d !h` finds non-hidden folders.
- `@attr d-------` finds strictly folders without any additional attributes.
- `@attr %fr?h?---` uses pattern search (file with read-only, any archive flag, hidden, etc.).

**🔹 Metadata Tag `@desc`**

Checks file comments from the `descript.ion` file located in the folder (in the example: comments for the file `Report.txt` from `C:\Projects\Documents\descript.ion`). Supports ANSI, UTF-8, and UTF-16.

- **Example:** `@desc !draft` finds files whose comment in `descript.ion` does not contain the word `draft`.

**🔹 Metadata Tag `@content`**

Performs a full-text search within the file (in the example: searches the text content of `Report.txt`). To maintain high performance, files larger than 1 MB (default) are skipped and total memory usage is limited to 50 MB (default).

- **Example:** `@content ?[0-9]{4}_report` searches the file content for this regex pattern.

---

### Metadata Tags from WDX Content Plugins

Custom metadata tags can be assigned in the settings to any external Total Commander content plugin (`*.wdx`). This allows searching metadata fields such as `@title`, `@composer`, or `@artist` for audio files as well as `@resolution` for images.

- **Path Mapping:** Environment variables as well as relative paths are supported when integrating WDX plugins in the settings.
- **Example:** `@composer mozart` finds audio files whose ID3 tag contains the composer `Mozart`.

[📖 Back to top](#table-of-contents)

---

<a id="escaping-characters"></a>
## 🛡️ 2.5 Escaping Characters (`\` and `"..."`)

To treat control characters (`|`, ` `, `/`, `!`, `~`, `^`, `$`, `=`, `?`, `%`, `<`, `*`, `@`) as regular search text, they must be escaped. Without escaping, for example, spaces in `Project Status Report` would unintentionally split the search text into separate conditions. Two methods are available for escaping:

- **Escaping individual characters (`\`):** A preceding backslash (`\`) cancels the special function of the directly following character (e.g., `Project\ Status\ Report`).
- **Escaping text blocks (`"..."`):** To disable all control characters in a text simultaneously, the entire text is enclosed in double quotes (e.g., `"Project Status Report"`).

**🔹 Complex Examples:**

When many control characters are present, escaping text blocks (`"..."`) simplifies input significantly.

- **Example:** Searching for files like `Report 2026 - #final.pdf`.

| Variant                       | 🔍 [2.3.2 Regex Search](#regex-search) | 🔍 [2.3.3 Pattern Search](#pattern-search) |
| ---                           | ---                                    | ---                                        |
| **Unescaped search text**     | `?Report \d\d\d\d - #final`            | `%Report #### - \#final`                   |
| **Text block escaping**       | `?"Report \d\d\d\d - #final"`          | `%"Report #### - \#final"`                 |
| **Single character escaping** | `?Report\ \\d\\d\\d\\d\ -\ #final`     | `%Report\ ####\ -\ \\#final`               |

**🔹 Nested Quotes**

If the searched text itself contains double quotes, the text block can be escaped using multiple double quotes (e.g., `@content ""string msg = "ok";""`). The engine supports up to 4 outer double quotes (allowing up to 3 inner consecutive double quotes).

| Variant                       | 1 quote                    | 2 consecutive quotes       | 4 consecutive quotes          |
| ---                           | ---                        | ---                        | ---                           |
| **Unescaped search text**     | `@content msg = "ok";`     | `@content msg = "";`       | `@content msg = @"""";`       |
| **Text block escaping**       | `@content ""msg = "ok";""` | `@content """msg = "";"""` | *not possible*                |
| **Single character escaping** | `@content msg\ =\ \"ok\";` | `@content msg\ =\ \"\";`   | `@content msg\ =\ @\"\"\"\";` |

[📖 Back to top](#table-of-contents)

---

<a id="practical-search-examples"></a>
## 🎯 2.6 Practical Search Examples

To see the syntax in action, the following overview shows practical queries ranging from simple everyday searches to advanced combinations for power users.

| Search String                              | Description / Search Result                                                                                                                                                   |
| ---                                        | ---                                                                                                                                                                           |
| `report 2026`                              | Filenames containing both `report` and `2026` (in any order).                                                                                                                 |
| `~Important`                               | Inverts the global case-sensitivity setting for this term. If the search is normally case-insensitive, it searches strictly for `Important` (not `important` or `IMPORTANT`). |
| `^~Invoice !draft`                         | Files starting with `Invoice` (evaluated with inverted case-sensitivity logic) and **not** containing the word `draft` (normal case-sensitivity logic).                       |
| `$jpg/png/gif`                             | Quick filter for file extensions at the end of the name (finds image types `.jpg`, `.png`, or `.gif`). Alternatively: `@ext jpg/png/gif`.                                     |
| `<color`                                   | Fuzzy search with typo tolerance (finds e.g., `colour.png` or `colon.txt`).                                                                                                   |
| `*dwn`                                     | Sequence search. Finds `download.zip` (**d**o**wn**load) or `database_warning.png` (**d**atabase_**w**ar**n**ing).                                                            |
| `@size =0`                                 | Tracks down empty 0-byte files.                                                                                                                                               |
| `@desc urgent`                             | Searches file comments from `descript.ion` for the keyword `urgent`.                                                                                                          |
| `@attr d !h`                               | Filters strictly folders (`d`) that are not hidden (`!h`).                                                                                                                    |
| `@ext pdf @age <7`                         | Finds PDF files (`@ext`) modified within the last 7 days (`@age`).                                                                                                            |
| `@size >2G @ext mkv/mp4`                   | Searches for video files (`.mkv` or `.mp4`) larger than 2 GB.                                                                                                                 |
| `@path "\archive\2025\" @name !^backup`    | Considers only files whose path contains `\archive\2025\`, but excludes files whose name starts with `backup`.                                                                |
| `^?[0-9]{4}_backup`                        | Regex search for filenames starting with a 4-digit year followed by `_backup` (e.g., `2026_backup.zip`).                                                                      |
| `@content %[80..100]%`                     | Full-text search inside documents for percentage values starting from 80% (e.g., `83%` or `95%`).                                                                             |
| `@res ^$%[1920..]x[1080..]`                | WDX plugin search: Finds images with Full HD resolution or higher (at least 1920×1080 pixels).                                                                                |

[📖 Back to top](#table-of-contents)

---

<a id="preprocessing-character-equivalence"></a>
# ⚡ 3. Preprocessing & Character Equivalence

Before the search syntax from 🧠 [2. Search Syntax](#search-syntax) takes effect, filter text and filenames are preprocessed using the replacement rules defined for the respective area.

After constructing the search structure, the search modes 🔍 [2.3.1 Standard Search](#standard-search), 🔍 [2.3.4 Fuzzy Search](#fuzzy-search), and 🔍 [2.3.5 Sequence Search](#sequence-search) additionally process the rules of dynamic character equivalence. Due to technical limitations, these rules are not available in the search modes 🔍 [2.3.2 Regex Search](#regex-search) and 🔍 [2.3.3 Pattern Search](#pattern-search).

> 💡 When not searching directly in filenames, all other metadata to be searched undergoes the same preprocessing and character equivalence processing.

[📖 Back to top](#table-of-contents)

---

<a id="how-preprocessing-works"></a>
## 🔄 3.1 How Preprocessing Works

Suppose the following replacement rules exist for a scope:

- Search term `abc` → Replacement text `x`
- Search term `ab` → Replacement text `y`
- Search term `bc` → Replacement text `z`
- Search term `b` → Replacement text `bb`

Without strict processing rules, the text `abc` could theoretically be transformed in many different ways (`x`, `yc`, `az`, `abbc`, `abbbc`, etc.).

To guarantee uniqueness and prevent infinite loops, the engine scans the text from left to right in a single pass according to three principles:

- At each character position, the engine evaluates replacement rules in descending order of search term length. The first match wins. This ensures that a specific, longer rule like `abc` reliably takes precedence over shorter rules like `ab`.
- Upon a match, the engine inserts the replacement text and immediately advances in the original text by the length of the matched search term. Already processed sections are not evaluated again — excluding recursions (such as `b` → `bb` → `bbb`).
- If no rule matches at the current position, the single character is preserved as-is and the search pointer advances by one character.

| Input Text | Processing Notes                                                              | Result   |
| ---        | ---                                                                           | ---      |
| `abc`      | `abc` matches directly as the longest term.                                   | `x`      |
| `abbc`     | `ab` becomes `y`, remaining `bc` becomes `z`.                                 | `yz`     |
| `bbc`      | First letter `b` becomes `bb`. Position advances; remaining `bc` becomes `z`. | `bbz`    |
| `bbb`      | Each `b` is individually expanded to `bb` in sequence.                        | `bbbbbb` |
| `abcd`     | `abc` becomes `x`. No rule exists for `d` → preserved unchanged.              | `xd`     |

[📖 Back to top](#table-of-contents)

---

<a id="how-character-equivalence-works"></a>
## 🔀 3.2 How Character Equivalence Works

Character equivalence operates directly when matching individual letters: an entered search character acts as a bridge for equivalent target characters in the filename.

**🔹 Character Equivalence via Replacement Rules (1:1 Mappings / Scope 4)**

With a 1:1 mapping, a single character in the search term builds a bridge to multiple possible target characters.

For example, if the following rule exists:

* Search term `_` → Replacement text `. -`

Then entering `_` will also find dots (`.`), spaces (` `), and hyphens (`-`) in the filename. Conversely, entering a dot (`.`) will not match an underscore (`_`).

> 💡 Regardless of the separators used in the filename (e.g., `my.document`, `my document`, or `my-document`), searching for `my_document` is sufficient in this case. This also eliminates the need to escape spaces in the search.

**🔹 Character Equivalence via Built-in Umlauts & Accents**

If the `Ignore umlauts and accents` option is enabled in the settings, equivalence rules for vowels and accents take effect.

- **Behavior:** Entering `a` finds filenames with `a`, `ä`, `á`, `à`, `â`, etc. Conversely, entering `ä` also finds filenames with `a`.

**🔹 Character Equivalence for Asian Languages (PinYin & Hangul)**

Special equivalence rule sets cover the characteristics of East Asian characters.

See 🌏 [3.3 Chinese Search (PinYin)](#chinese-search-pinyin) and 🌏 [3.4 Korean Search (Hangul)](#korean-search-hangul).

[📖 Back to top](#table-of-contents)

---

<a id="chinese-search-pinyin"></a>
## 🌏 3.3 Chinese Search (PinYin)

**PinYin search** allows finding Chinese characters (Hanzi) using a standard keyboard. Instead of entering Chinese characters directly, entering the initial letters of the respective phonetic spelling (PinYin) is sufficient.

Because many Chinese characters share the same initial sounds or are pronounced differently depending on context (polyphones), a single Latin letter covers multiple Chinese characters, just as a Chinese character can be found by multiple Latin letters:

- **1:n (One letter → Many characters):** Entering `ys` finds both `耶稣` (*Yēsū* — Jesus) and `医生` (*Yīshēng* — Doctor).
- **n:1 (One character → Many letters):** The character `行` is pronounced as *háng* or *xíng* depending on context. Thus, `银行` (*Yínháng* — Bank) can be found via `yh`, while `行为` (*Xíngwéi* — Behavior) is found via `xw`.

If the system language is set to Chinese, this feature is active from the first launch; otherwise, it can be enabled in the settings.

> 💡 Special thanks go to **Christian Ghisler** for the original conception of this algorithm and for providing the underlying translation table (`tcmatch.pinyin.tbl`).

[📖 Back to top](#table-of-contents)

---

<a id="korean-search-hangul"></a>
## 🌏 3.4 Korean Search (Hangul)

**Hangul search** allows finding Korean words by entering initial consonants (*Choseong*) or individual syllable components (*Jamo*).

Since the Korean script consists of composite syllable blocks (initial consonant + vowel + optional final consonant), the engine dynamically decomposes these characters during matching:

- **Initial Consonant Search:** Entering pure initial consonants finds all syllable blocks starting with these sounds. For example, `ㅇㅅ` finds the word `예수` (*Yesu*) and `ㅍㅇ` finds `평양` (*Pyeongyang*).
- **Flexible Syllable Stem:** Entering a combination of initial consonant and vowel automatically matches all extended syllable variants that additionally feature a final consonant (e.g., `야` also matches `양`).
- **Jamo Compatibility:** The engine treats standard and compatibility Jamos as equivalent, so entering a character from one category finds the corresponding character in the other category.

If the system language is set to Korean, this feature is active from the first launch; otherwise, it can be enabled in the settings.

[📖 Back to top](#table-of-contents)

---

<a id="replacement-rules"></a>
## 📝 3.5 Replacement Rules

Replacement rules consisting of `Search term`, `Replacement text`, and `Scope` can be configured directly in the settings or via an external configuration file. The assigned effective scope (Scope 1 to 4) determines at which processing stage a rule takes effect.

**🔹 Effective Scopes 1–3** (See 🔄 [3.1 How Preprocessing Works](#how-preprocessing-works))

**Scope 1 — Search text only:** Modifies exclusively the entered search query. Ideal for 🧩 [3.6 Templates](#templates).

- *Example:* Search term `#office` → Replacement text `@ext docx/xlsx/pdf` *(Typing `#office` automatically expands to search for these file extensions).*

**Scope 2 — Item name only:** Modifies exclusively the filename and other metadata.

- *Example:* Search term ` ` → Replacement text `_` *(Replaces spaces in the filename with underscores. This eliminates the need to escape spaces in the search).*

**Scope 3 — Both (Search text & Item name):** Applies the replacement symmetrically to search query and filename (or other metadata). Normalizes both sides.

- *Example:* Search term `ä` → Replacement text `ae` *(Entering `ä` finds `ae` in the filename, and entering `ae` finds `ä`).*

**🔹 Effective Scope 4** (See 🔀 [3.2 How Character Equivalence Works](#how-character-equivalence-works))

**Scope 4 — 1:1 Mapping:** Defines a dynamic character equivalence. The search term must be exactly **1 character** in length.

- *Example:* Search term `_` → Replacement text `. -` *(Entering `_` also finds dots (`.`), spaces (` `), and hyphens (`-`) in the filename).*

---

### Configuration via `tcmatch.replacements.txt`

For bulk replacements or direct editing in a text editor, the file `tcmatch.replacements.txt` in the 📂 [DataFolder](#directory-redirection) can be used.

**🔹 Format & Syntax**

- The file is **UTF-8 encoded**.
- A replacement rule is defined line-by-line, separated by tabs: `[Scope]` → **TAB** → `[Search term]` → **TAB** → `[Replacement text]`
- Lines without a tab character are ignored as comments.
- Changes are automatically reloaded on plugin startup and whenever settings are saved.

**Example file for the rules mentioned above** (`→` represents a tab character):

```text
1→#office→@ext docx/xlsx/pdf
2→ →_
3→ä→ae
4→_→. -
```

**🔹 Prioritization of Replacement Rules**

**Scope 1, 2, and 3:** Replacement rules from the settings take precedence. If a rule with the same search term exists in the settings, it overrides the entry from the text file.

**Scope 4:** Replacement rules behave **additively**. Entries from the settings and the text file are merged and complement each other.

[📖 Back to top](#table-of-contents)

---

<a id="templates"></a>
## 🧩 3.6 Templates

Templates are a practical special case of the 📝 [3.5 Replacement Rules](#replacement-rules) in **Scope 1** (search text only). They allow frequently used search filters to expand automatically via shortcuts before the regular 🧠 [2. Search Syntax](#search-syntax) takes effect.

**🔹 Practical Examples**

| Shortcut  | Replacement Text           | Meaning / Use Case                                                              |
| ---       | ---                        | ---                                                                             |
| `#office` | `@ext docx/xlsx/pdf`       | Filters for common Office documents.                                            |
| `#old`    | `@age >1y`                 | Finds files whose last modification was more than one year ago.                 |
| `#hqpic`  | `%img_###.jpg @size >10MB` | Searches via pattern search for filenames like `img_001.jpg` larger than 10 MB. |

> 💡 Using the `#` prefix is a recommended convention to prevent shortcuts from expanding accidentally. In principle, the prefix can be omitted or chosen differently.

[📖 Back to top](#table-of-contents)

---

<a id="the-interactive-search-assistant"></a>
# 💡 4. The Interactive Search Assistant

The **interactive search assistant** usually opens automatically alongside the **Quick Filter dialog** (Ctrl+S) of Total Commander. Among other features, it provides live visual feedback on the entered search logic, gives input tips, points out syntax errors, and offers quick accesses.

The ⚙️ [5. Settings](#settings) can be opened directly via the ⚙️ **gear icon** in the assistant.

> 💡 If the assistant has been disabled in the settings, it can be called manually at any time by entering the control command `@gui` in the Total Commander search box.

---

### Search Assistant Window

The settings allow you to specify exactly how and where the assistant presents itself on the screen:

- **Anchor Point:** The window can be aligned relative to the **screen**, the **Total Commander main window**, or the **Total Commander Quick Filter dialog** (alignment at all four corners is possible).
- **Fine Adjustment:** Using pixel offsets (X/Y) and setting width and height, the window can be seamlessly adapted to your layout.

> 💡 The configured position and size are applied unchanged and may extend beyond the visible screen area. This can be useful with multiple monitors and is the user's responsibility.

---

### Content and Order of the Search Assistant

The search assistant is modularly structured. In the settings, each category is introduced with a short description. The order and display status (*Expanded*, *Collapsed*, or *Disabled*) of individual categories can be customized individually.

**💡 Tips for Use:**

- For an optimal overview, only regularly required categories should remain initially *expanded*.
- The search assistant window can be scrolled using the right scrollbar or the mouse wheel if necessary.
- Hover over buttons to display helpful tooltips.
- Advanced actions as well as keyboard shortcuts (e.g., **Ctrl + Click**) can be accessed via the context menu in some areas.

[📖 Back to top](#table-of-contents)

---

<a id="settings"></a>
# ⚙️ 5. Settings

The settings window serves as the central hub for configuring and customizing the plugin (among other things, all control characters of the search syntax can be adjusted or completely disabled here). It can be accessed at any time via the ⚙️ **gear icon** in the header of the 💡 [4. Interactive Search Assistant](#the-interactive-search-assistant).

> 💡 The settings window has been designed so that all options are directly understandable on the spot through explanatory banners and detailed description texts. Therefore, redundant repetition of every single setting is intentionally avoided in this manual — with the exception of the advanced customizations in 🌍 [5.1 Custom Translations](#custom-translations) and 🎨 [5.2 Custom Themes](#custom-themes).

Via the integrated tabs of the settings window, the following tools can also be accessed directly:

- 📖 **Documentation:** Calls up this help directly inside the plugin window.
- 📋 **Log File:** Allows viewing and clearing internal diagnostic logs for troubleshooting.

---

### Overview of Configuration Areas

- **Language & Appearance:** Selection of UI language and theme (Light / Dark) as well as support for custom language and color adjustments.
- **Search Assistant:** Control over window position, window size, and window content of the search assistant.
- **Special Characters:** Every single control character of the search syntax can be adjusted or disabled. Disabled characters can be searched as regular text without escaping.
- **Search Behavior:** Setting the default search mode, default case sensitivity, and further options for controlling search behavior.
- **Metadata & WDX Integration:** Customizing shortcut names, defining default shortcuts per search scope, and registering external Total Commander Content Plugins (WDX).
- **Text Replacements & Language Support:** Management of replacement rules, ignoring umlauts/accents, as well as enabling Chinese PinYin and Korean Hangul search.
- **Performance, Caching & Diagnostics:** Configuration of the metadata cache as well as defining the log detail level.

[📖 Back to top](#table-of-contents)

---

<a id="custom-translations"></a>
## 🌍 5.1 Custom Translations

The plugin offers a flexible localization system that allows you to customize individual user interface texts or create completely new translations.

> 💡 The fields for language editing become visible as soon as the **Expert Settings** option is activated in the **Appearance** section.

---

### Three-Stage Language Model

When loading texts, the engine uses a three-stage model with an integrated fallback level:

1. **English (Base Language):** Serves as a permanent foundation, ensuring that if translations are missing (e.g., after a plugin update), at least the English term is always displayed.
2. **Selected Language:** Overrides the English base with the texts of the language selected in the settings.
3. **Custom Text Adjustments:** Take top priority. All entries in the `Custom Text Adjustments` field override the underlying levels.

---

### Customizing Individual Texts

Using the `Custom Text Adjustments` field, individual texts can be targeted and modified without having to create a complete language file:

- **Format:** `ID=Text` (e.g., `002_001_001=Settings`).
- **Formatting Rules:** Lines that do not start with a digit (`0`–`9`) or do not contain an `=` are treated as comments and are ignored. Line breaks within texts can be inserted using `\n`; a single backslash must be doubled (`\\`).
- **Applying Changes:** Texts are applied without a restart as soon as focus leaves the text box (focus change).

---

### Creating Full Translations (AI Workflow)

Procedure for creating a completely new translation:

1. Preferably set the plugin language to **English**.
2. Click **Load template from current language**. The text box will be populated with all available language IDs.
3. **Translation via AI:** Copy the entire content of the field into an AI model (e.g., ChatGPT). The prompt at the beginning automatically instructs the AI to translate only the texts to the right of the `=` sign and leave the IDs to the left unchanged.
4. Paste the AI's response back into the `Custom Text Adjustments` field.
5. Make manual corrections if necessary after generation to ensure a precise and high-quality translation.
6. Optionally, this documentation can also be translated.

> 💡 Completed language customizations can be submitted directly via the [GitHub project](https://github.com/SamuelPlentz/QSX2/issues) to make them available to all users in future plugin versions.

[📖 Back to top](#table-of-contents)

---

<a id="custom-themes"></a>
## 🎨 5.2 Custom Themes

The plugin offers a flexible theme system that allows you to adjust the colors of individual design elements (such as buttons or banners) or create completely new themes.

> 💡 The fields for color editing become visible as soon as the **Expert Settings** option is activated in the **Appearance** section.

---

### Two-Stage Color Model

When loading colors, the engine uses a two-stage model with an integrated fallback level:

1. **Selected Base Theme (Light / Dark):** Serves as a permanent foundation so that a valid color definition is always present for every control.
2. **Custom Color Adjustments:** Take top priority. All entries in the `Custom Color Adjustments` field selectively override the color values of the base theme.

---

### Customizing Individual Colors & Brushes

Using the `Custom Color Adjustments` field, individual colors and brushes can be targeted and modified without having to create a complete theme:

- **Format:** `ID=Color Value` (e.g., `01_01_01=blue30`). The parser supports three flexible formats:
    - **Tailwind Color Names:** Integrated palette names with a combination of base color and brightness level (e.g., `blue30`, `sky50`, `slate95`):
        - **Base Colors (Chromatic):** `red`, `orange`, `amber`, `yellow`, `lime`, `green`, `emerald`, `teal`, `cyan`, `sky`, `blue`, `indigo`, `violet`, `purple`, `fuchsia`, `pink`, `rose`
        - **Base Colors (Neutral / Earth Tones):** `slate`, `gray`, `zinc`, `neutral`, `stone`, `taupe`, `mauve`, `mist`, `olive`
        - **Shades (very light to very dark):** `05`, `10`, `20`, `30`, `40`, `50`, `60`, `70`, `80`, `90`, `95`
    - **HEX Color Codes:** Exact hexadecimal values for precise color shades (e.g., `#f0f0f0` or including transparency `#ff172554`).
    - **Gradients:** Comma-separated list of two or more colors with the gradient angle in degrees as the last value: `rose50, fuchsia50, 0` *(gradient of two red tones at a 0° angle)*.
- **Formatting Rules:** Lines that do not start with a digit (`0`–`9`) or do not contain an `=` are treated as comments and are ignored.
- **Applying Changes:** Color values are applied without a restart as soon as focus leaves the text box (focus change). Color customizations for the search assistant take effect on its next call.

---

### Creating Full Themes

Procedure for designing a completely custom color theme:

1. Select the desired base theme (**Light** or **Dark**).
2. Click **Load template from current theme**. The text box will be populated with all available color IDs — clearly structured and commented by functional areas (Window, Banners, Buttons, Search Assistant).
3. Adjust color values as desired.

> 💡 Particularly successful themes can be submitted directly via the [GitHub project](https://github.com/SamuelPlentz/QSX2/issues) to make them available to all users in future plugin versions. Additionally, the **GitHub Wiki** is available for sharing custom color schemes with the community.

[📖 Back to top](#table-of-contents)

---

<a id="installation"></a>
# 📦 6. Installation

Before installing the plugin, ensure that the system meets the following prerequisites:

- **Total Commander:** Version **11.00** or higher
- **.NET Framework:** Version **4.8** or higher

### Installation Procedure

1. **Download:** Download the archive [QSX2 ####-##-##.zip](https://github.com/SamuelPlentz/QSX2/releases).
2. **Restart Total Commander (for updates):** If an older plugin version has been used since Total Commander was last launched, restarting Total Commander releases the file locks on the DLLs (provided Quick Search has not been opened since).
3. **Automatic Installation:** Open the `QSX2.zip` file inside Total Commander with `Enter`. The plugin will automatically be installed in the Total Commander subfolder `QSX2` via the internal dialog.
4. **Configuration (`wincmd.ini`):** For initial installation, add or adjust the following keys under the `[Configuration]` section in `wincmd.ini`:

```ini
[Configuration]
tcmatch=%COMMANDER_PATH%\QSX2\tcmatch.dll
tcmatch64=%COMMANDER_PATH%\QSX2\tcmatch64.dll
```

> 💡 **Uninstallation:** Close Total Commander and delete the plugin folder `QSX2` in the Total Commander directory. Then remove the entries `tcmatch` and `tcmatch64` in `wincmd.ini` under the `[Configuration]` section.

[📖 Back to top](#table-of-contents)

---

<a id="alternative-installation-folder"></a>
## 📂 6.1 Alternative Installation Folder

The plugin can also be placed in any directory (e.g., for a portable setup under `D:\Portable\Total Commander Tools\QSX2\`). To do this, unpack the ZIP archive manually in the target directory instead of using automatic installation.

Adjust the paths in `wincmd.ini` accordingly:

```ini
[Configuration]
tcmatch=D:\Portable\Total Commander Tools\QSX2\tcmatch.dll
tcmatch64=D:\Portable\Total Commander Tools\QSX2\tcmatch64.dll
```

> 💡 Without a path specification in `wincmd.ini`, Total Commander searches directly in its own root directory for `tcmatch.dll` or `tcmatch64.dll`. An installation in the root directory is possible, but **not recommended**, as plugin files and subfolders will clutter and mix with Total Commander's files.

[📖 Back to top](#table-of-contents)

---

<a id="directory-redirection"></a>
## 📂 6.2 Directory Redirection

The plugin uses two different directories for its files:

### 📂 PluginFolder (Plugin Directory)

This directory contains the plugin files, in particular:

* `tcmatch.dll`
* `tcmatch64.dll`
* `tcmatch.path.txt`
* `tcmatch.readme.en.md`
* `tcmatch.readme.de.md`
* `tcmatch.pinyin.tbl`

The path is specified directly in `wincmd.ini` (Default: `%COMMANDER_PATH%\QSX2\`, see 📦 [6. Installation](#installation); for an alternative location, see 📂 [6.1 Alternative Installation Folder](#alternative-installation-folder)).

### 📂 DataFolder (Configuration & Logs)

This directory contains the plugin's configuration and log files:

- `tcmatch.xml`
- `tcmatch.log`
- a temporary browser profile for the plugin interface

By default, the `DataFolder` is located in the roaming user profile:

`%APPDATA%\QSX2\` (e.g. `C:\Users\<username>\AppData\Roaming\QSX2\`)

### 🔀 Redirect DataFolder via `tcmatch.path.txt`

To store the `DataFolder` in a different location, a file named `tcmatch.path.txt` can be created in the `PluginFolder`.

If this file contains a valid path, it is used as the `DataFolder` and replaces the default path `%APPDATA%\QSX2\`.

Examples of possible path specifications in `tcmatch.path.txt`:
- **Absolute path:** `D:\Portable\Total Commander Tools\QSX2Config\`
- **With environment variables:** `%COMMANDER_PATH%\QSX2\Config\`
- **Relative to `PluginFolder`:**
    - `.\` → resolves to `...\Total Commander\QSX2\` (`PluginFolder` & `DataFolder` are the same)
    - `.\QSX2Config\` → resolves to `...\Total Commander\QSX2\QSX2Config\`
    - `.\..\QSX2Config\` → resolves to `...\Total Commander\QSX2Config\`

### 🛡️ Fallback for Missing Write Permissions

If writing to the determined `DataFolder` fails, the plugin automatically falls back to the system temporary directory `%TEMP%` (e.g. `C:\Users\<username>\AppData\Local\Temp\`) to ensure stable operation.

[📖 Back to top](#table-of-contents)

---

<a id="system-limits-troubleshooting"></a>
# 🛠️ 7. System Limits & Troubleshooting

<a id="operation-limits-of-the-tcmatch-dll-interface"></a>
## ⚙️ 7.1 Operation & Limits of the `tcmatch.dll` Interface

To understand system limitations, it is helpful to know how the Total Commander `tcmatch.dll` interface operates:

1. **Binary Decision:** The core method `MatchFileExW` decides purely binarily for each file whether it matches the filter text (`1`) or not (`0`). Graduations (e.g., `90% match`) or visual highlights cannot be returned via the interface.
2. **Passed Data:** Total Commander passes only the filename and path. All additional data (such as metadata or properties) must be determined by the plugin itself.
3. **File-by-File Query:** No overall list of a directory is transmitted. With every change in the Quick Filter dialog, Total Commander iterates through the folder contents item by item and sends a single request to the plugin for each entry.
4. **No Control Over User Interface:** The Quick Filter dialog is managed entirely by Total Commander. Look, position, and basic functionality of the input window cannot be influenced by the plugin. The interface currently does not notify the plugin when the Quick Filter dialog is opened or closed.

Some features or behaviors can therefore not be influenced by the plugin itself, as they are defined by the interface.

[📖 Back to top](#table-of-contents)

---

<a id="known-limitations"></a>
## 🚧 7.2 Known Limitations

- **Minimum Total Commander Version:** Although the `tcmatch.dll` interface was introduced back in Total Commander 7.50, `QSX2` exclusively uses the expanded method `MatchFileExW` (available starting from Total Commander 11.00). For older Total Commander versions, the predecessor `QSX1` must be used instead.
- **File Locks by .NET Runtime (CLR):** While the predecessor `QSX1` was based on performant yet complex C++, `QSX2` utilizes modern .NET infrastructure and C#. Technically, a lightweight C++ bridge (`tcmatch.dll` / `tcmatch64.dll`) invokes the actual plugin logic in `tcmatch.Core.dll` (C#). As a result, the Common Language Runtime (CLR) hooks deeply into the `totalcmd.exe` process upon the first call of the Quick Filter dialog and enforces file locks on the loaded DLLs. Internal commands like `cm_UnloadPlugins 16` **cannot** release these .NET locks. To update or delete plugin files, Total Commander must be closed completely.
- **Brief Delay on First Launch:** Upon the very first call of the Quick Filter dialog after launching Total Commander, a brief delay may occur because the .NET runtime environment must be initialized.
- **Ignoring Total Commander Default Settings:** Options under `Configure` → `Options` → `Quick Search` (such as *Exact start* or *Exact end*) have no influence on the plugin's filter logic.
- **Limited Functionality on Network Drives, FTP, and in Archives:** Only limited functionality is available on network drives, FTP connections, and inside archive files. Since content metadata usually cannot be retrieved there, only information that can be derived directly from the file path is usable.
- **Limitations with Content Plugins:** The `ContentGetDetectString` function and translated field names are not supported by the plugin.
- **Leading Asterisk in Tab Paths:** When filtering tab paths, Total Commander 11 sometimes automatically prepends a leading `*` to the search text in order to search the entire path instead of just the filename. The plugin ignores this leading `*` and in this case automatically filters according to the `Default metadata aliases per source view` setting.

[📖 Back to top](#table-of-contents)

---

<a id="troubleshooting-checklist"></a>
## 📋 7.3 Troubleshooting Checklist

In case of unexpected behavior (e.g., crashes or non-matching search results in the file list), the following procedure is recommended to isolate the issue:

1. **Check Search Structure:** Expand the search structure in the ⚙️ [5. Settings](#settings) of the search assistant and then check in the **tooltips** how the term is interpreted. Rebuild the search text character by character to track each change in the filter logic.
2. **Test Default Configuration:** Reset the plugin to default values in the ⚙️ [5. Settings](#settings) to rule out misconfigurations. Backing up `tcmatch.xml` in the 📂 [DataFolder](#directory-redirection) beforehand is recommended.
3. **Analyze Log File:** Increase the log detail level in the ⚙️ [5. Settings](#settings). The `tcmatch.log` file (viewable in the settings window or in the 📂 [DataFolder](#directory-redirection)) records plugin initialization, filter interactions, and errors.
4. **Cross-Check:** Uninstall the plugin temporarily (📦 [6. Installation](#installation)) or test with the official [tcmatch plugin by Christian Ghisler](https://www.ghisler.ch/board/viewtopic.php?p=173110#p173110). This determines whether the cause lies with Total Commander or the plugin.
5. **Report Errors:** Attach `tcmatch.xml` and `tcmatch.log` files when making support requests or bug reports. Due to data volume at high detail levels, clear the log file beforehand or send only extracts. Anonymize sensitive data (e.g., paths or filenames) beforehand.

[📖 Back to top](#table-of-contents)

---

<a id="credits-legal"></a>
# 📜 8. Credits & Legal

<a id="contributors"></a>
## 👥 8.1 Contributors

This project thrives on the commitment of its community. A heartfelt thank you goes out to everyone contributing to further development with bug reports, feature requests, themes, translations, or pull requests.

| Role                        | Contributors  |
| ---                         | ---           |
| **Idea & Main Development** | Samuel Plentz |
| **Further Development**     | You?          |
| **Translation (DE)**        | Samuel Plentz |
| **Translation (EN)**        | Samuel Plentz |
| **Theme (Light)**           | Samuel Plentz |
| **Theme (Dark)**            | Samuel Plentz |

[📖 Back to top](#table-of-contents)

---

<a id="acknowledgments"></a>
## 📦 8.2 Acknowledgments

> ❤️ Special thanks go to **Christian Ghisler** for Total Commander, the original concept of PinYin search, and providing the underlying translation table (`tcmatch.pinyin.tbl`).

A big thank you also goes to all developers and contributors of the following libraries and resources used in this project:

- **WPF UI** — Controls for the modern user interface.
- **Markdig** — Conversion and processing of Markdown content.
- **Microsoft Edge WebView2** — Display of Markdown documentation.
- **System.Buffers** / **System.Memory** / **System.Numerics.Vectors** / **System.Runtime.CompilerServices.Unsafe** — High-performance .NET core components.
- **Tailwind Colors** — Color palette that can be used in themes.

*A detailed breakdown of all licenses for these third-party components can be found in the separate file `LICENSE.md`.*

[📖 Back to top](#table-of-contents)

---

<a id="license-terms"></a>
## ⚖️ 8.3 License Terms

`QuickSearch eXtended 2` is licensed under a modified MIT License:

> **MIT License with Custom Provision**
> 
> Copyright (c) 2026-present Samuel Plentz  
> Software: QuickSearch eXtended 2 (QSX2)
> 
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation on the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
> 
> 1. The above copyright notice and this permission notice (including the next paragraph) shall be included in all copies or substantial portions of the Software.
> 
> 2. Special Provision Regarding the "Personal Note" Section: The user interface tab titled "Personal Note" and its content, as well as the corresponding chapter in the documentation — specifically including the statements regarding the Christian faith — are an integral part of this work. In all copies, forks, modifications, and derivative works, these elements must be retained in full, unaltered, and prominently visible as a distinct section. They may not be removed, abridged, distorted, or supplemented within the project by opposing viewpoints, counter-statements, or disclaimers.
> 
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
> 
> Soli Deo Gloria.

[📖 Back to top](#table-of-contents)

---

<a id="personal-note"></a>
# ✍️ 9. Personal Note

## ❤️ I Love Programming…

My name is **Samuel Plentz** and I have been programming with great passion and joy since my school days. In the summer of 2026, this project was created as my first **vibe-coding experiment**: AI generates the code, which I then manually review, refine, and merge.

It is my personal contribution and a tribute to **Total Commander** — a great companion in daily PC use.

This Total Commander plugin is available to all users **free of charge**.

## ❤️ I Love My Family…

Special thanks go to **my wife**. She faithfully stands by my side, supports me in many ways, and is a great role model to me in her dedication to our family and to others. Thank you for making my life so rich!

My **three sons** are a huge blessing: The oldest fascinates me as a structured thinker with a big heart, the middle one inspires through his creativity, helpfulness, and empathy, and the youngest in his carefree way is a daily source of joy and good cheer.

I am also deeply grateful to **my parents**: They are simply wonderful and got so many things just right along my journey in life.

## ❤️ I Love Jesus…

**Life is more** than what we can see, achieve, or possess. **Jesus Christ** makes the ultimate difference; He gives purpose, hope, and strength.

The central question is: **Is Jesus truly who He claims to be?**

I am convinced with all my heart: Yes, He is. **He is the Son of God**, who came into this world to bear humanity's guilt. His crucifixion and resurrection happened out of love. Everyone who believes in Him and His salvation receives forgiveness and **eternal life**.

Anyone who reads the **New Testament** with an open mind can recognize that Jesus is not just an extraordinary human being, but indeed the **Son of God**.

I encourage you to read for yourself and see: **Is this real?**

[📖 Back to top](#table-of-contents)
