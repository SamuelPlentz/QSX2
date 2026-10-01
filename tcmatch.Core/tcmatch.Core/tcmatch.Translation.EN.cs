namespace tcmatch.Core
{
    public static class TranslationEN
    {
        public static readonly string languageNameEnglish = "English";
        public static readonly string languageNameLocal = "English";
        public static readonly string languageIsoCode = "en";
        public static readonly string readmeVersion = "2026-10-01"; // #RELEASE

        public static readonly string rawData = @"
001_001_001=Configuration

001_002_001=Defaults
001_002_002=Cancel
001_002_003=Save & close
001_002_004=Load default values?
001_002_005=Do you really want to reset all settings in this window to their default values? Unsaved changes will be lost.
001_002_006=Discard changes?
001_002_007=There are unsaved changes. Do you want to discard these changes?
001_002_008=Yes
001_002_009=No
001_002_100=Character for global OR operator
001_002_101=Character for AND operator
001_002_102=Character for negation
001_002_103=Character to toggle case sensitivity
001_002_104=Character to force match at start of text
001_002_105=Character to force match at end of text
001_002_106=Character for local OR operator
001_002_107=Character for metadata
001_002_108=Character for escaping
001_002_109=Decimal separator
001_002_110=Character for standard search
001_002_111=Character for regex search
001_002_112=Character for fuzzy search
001_002_113=Character for sequence search
001_002_114=Character for quoting
001_002_115=Character for pattern search
001_002_200=Metadata: GUI
001_002_201=Metadata: Name
001_002_202=Metadata: Extension
001_002_203=Metadata: Folder
001_002_204=Metadata: Path
001_002_205=Metadata: Description
001_002_206=Metadata: Content
001_002_207=Metadata: Age
001_002_208=Metadata: Size
001_002_209=Metadata: Attributes
001_002_290=Metadata: WDX - {0} - {1}
001_002_303=Search assistant window - Size: The window width is too small (Minimum {0}px).
001_002_304=Search assistant window - Size: The window height is too small (Minimum {0}px).
001_002_305=Search control characters: The decimal separator must not be empty.
001_002_306=Search control characters: Character assigned multiple times: ""{0}"" at ({1})
001_002_307=Metadata: The exclusive field ""{0}"" cannot be combined with other fields under the alias ""{1}"".
001_002_308=Metadata: The metadata alias ""{0}"" ({1}) contains the active control character ""{2}"" ({3}).
001_002_309=Search behavior: For fuzzy search, the typo threshold {0} must be at least {1}.
001_002_310=Search behavior: For fuzzy search, the typo threshold {0} must be greater than or equal to the typo threshold {1}.
001_002_311=Text replacements: The search term must not be empty.
001_002_312=Text replacements: The search term ""{0}"" is defined multiple times in conflicting scopes.
001_002_313=Text replacements: For 1:1 Mapping (Scope 4), the search term ""{0}"" must be exactly one character long.
001_002_314=Text replacements: For 1:1 Mapping (Scope 4), the replacement text for search term ""{0}"" must not be empty.
001_002_315=Performance, caching & diagnostics: The limit for cache entries must be at least {0}.
001_002_316=Performance, caching & diagnostics: The maximum file size for reading ""@content"" must be at least {0} MB.
001_002_317=Performance, caching & diagnostics: The total memory for the ""@content"" cache must be at least {0} MB.
001_002_318=Metadata: The default alias ""{0}"" for view ""{1}"" is not a valid or active metadata alias.
001_002_400=Search assistant window - Position: Due to the selected combination of anchor point, window size, and distance, the window might be partially or fully outside the visible screen area.
001_002_401=Performance, caching & diagnostics: Higher log levels drastically reduce search performance due to extensive log file writing.
001_002_402=Performance, caching & diagnostics: Disabling the cache significantly reduces search speed, as all file and folder information must be reread with every change to the search text.
001_002_403=32-bit
001_002_404=64-bit
001_002_405=WDX plugin {0}: Different INI files are used for this plugin. Only one single INI file should be defined per plugin.
001_002_406=WDX plugin {0}: The configured field ""{1}"" does not exist in the plugin.
001_002_407=WDX plugin {0} - Error: File not found: pluginPath = ""{1}""
001_002_408=WDX plugin {0} - Error: LoadLibrary(pluginPath = ""{1}"") => failed - Error code: {2}
001_002_409=WDX plugin {0} - Error: Function ""ContentGetSupportedField"" not found: pluginPath = ""{1}""
001_002_410=WDX plugin {0} - Error: Function ""ContentGetValueW"" (or ""ContentGetValue"") not found: pluginPath = ""{1}""
001_002_411=WDX plugin {0} - Error: CreateDirectory(""{1}"") (for ""{2}"") => failed - {3}
001_002_412=WDX plugin {0} - Error: ContentSetDefaultParams(pluginInterfaceVersion = {1}.{2}, defaultIniName = {3}) => failed - {4}
001_002_413=WDX plugin {0} - Error: ContentGetSupportedField(fieldIndex = {1}, fieldName = {2}, units = {3}, maxLen = {4}) => failed - result: {5} - {6}
001_002_414={0} plugin cannot be loaded into {1} QuickSearch eXtended 2
001_002_415=Plugin was not loaded for performance reasons because it is disabled

002_001_001=Settings

002_002_001=Language
002_002_002=UI language:
002_002_003=Custom text adjustments (Editing in an external editor recommended):
002_002_004=Load template from current language
002_002_005=Here you can specifically change individual texts used within the application using the ""ID=Text"" format or create a completely new language. Feel free to submit complete translations via the GitHub project!
002_002_006=Discard changes?
002_002_007=Should the existing entries in the ""Custom text adjustments"" field be discarded and the default template of the current language be reloaded?
002_002_008=# Copy the template into an AI (e.g., ChatGPT) using the following command: ""Translate these texts into [Target Language]. Leave the IDs (numbers before the =) exactly the same and only translate the text to the right of them. Respond exclusively with the finished result without any additional text:""
002_002_100=Appearance
002_002_101=UI theme:
002_002_102=Custom color adjustments (Editing in an external editor recommended):
002_002_103=Load template from current theme
002_002_104=Should the existing entries in the ""Custom color adjustments"" field be discarded and the default template of the current theme be reloaded?
002_002_105=Show expert settings in various sections:
002_002_201=Light
002_002_202=Dark

002_003_001=Search assistant window
002_003_002=Show search assistant when opening the Total Commander Quick Filter dialog (Ctrl+S):
002_003_003=If the search assistant is disabled, it can only be reopened by entering ""@gui"" into the Total Commander Quick Filter dialog.
002_003_004=Keep search assistant open after closing the Total Commander Quick Filter dialog:
002_003_005=Anchor point for window position:
002_003_006=Desktop screen
002_003_007=Total Commander main window
002_003_008=Total Commander Quick Filter dialog
002_003_009=Top-Left corner
002_003_010=Top-Right corner
002_003_011=Bottom-Left corner
002_003_012=Bottom-Right corner
002_003_013=Pixel offset from anchor point (X / Y):
002_003_014=Negative values move the window left/up, positive values right/down.
002_003_015=Window size (Width / Height):

002_004_001=Content and order of the Search Assistant
002_004_002=Specify which sections are displayed in the Search Assistant and whether they are expanded or collapsed by default. The order can be adjusted using the buttons. The Search Assistant window can be scrolled using the mouse wheel if needed. Hover over buttons for helpful tooltips.
002_004_010=Errors (e.g., invalid regex expressions in search text)
002_004_011=Warnings (performance hints for slow searches)
002_004_012=Information (general performance metrics)
002_004_013=Search structure (visual representation of search logic)
002_004_014=Detected search special characters (in current search text)
002_004_015=Input hints (dynamic suggestions based on current search text)
002_004_016=Available control characters (overview of all active characters)
002_004_017=Text replacements (overview of active rules applying to the current filter text, min. 2 search characters)
002_004_018=Quick access (recent search history and direct search options)
002_004_030=Section
002_004_031=Display state
002_004_032=Move up
002_004_033=Move down
002_004_040=Disabled
002_004_041=Collapsed
002_004_042=Expanded
002_004_050=Use full search structure (also shows default values for search mode and metadata shortcuts if the ""Search structure"" section is active)

002_005_001=Search control characters
002_005_002=Disabled search characters lose their control function. A search can then use these characters directly as normal text without escaping them with a backslash (\\).
002_005_003=Character for global OR operator (|):
002_005_004=Character for AND operator (space):
002_005_005=Character for negation (!):
002_005_006=Character to toggle case sensitivity (~):
002_005_007=Character to force match at start of text (^):
002_005_008=Character to force match at end of text ($):
002_005_009=Character for local OR operator (/):
002_005_010=Character for metadata (@):
002_005_011=Character for escaping (\\):
002_005_012=Decimal separator (for ""@age"" and ""@size""):
002_005_013=Character for quoting (""):

002_006_001=Search mode characters
002_006_002=Character for standard search (=):
002_006_003=Character for regex search (Regular expression) (?):
002_006_004=Character for fuzzy search (Approximate match) (<):
002_006_005=Character for sequence search (*):
002_006_006=Character for pattern search (%):

002_007_001=Search behavior
002_007_002=Default search mode:
002_007_003=Standard search (=)
002_007_004=Regex search (Regular expression) (?)
002_007_005=Fuzzy search (Approximate match) (<)
002_007_006=Sequence search (*)
002_007_007=Case sensitive by default:
002_007_008=Fuzzy search – 1 typo allowed from this text length:
002_007_009=Fuzzy search – 2 typos allowed from this text length:
002_007_010=Fuzzy search – 3 typos allowed from this text length:
002_007_011=Abort search via key:
002_007_012=Disabled
002_007_013=ESC key (closes Quick Search)
002_007_014=PAUSE key (keeps Quick Search open)
002_007_015=ESC and PAUSE keys (both active)
002_007_016=To stop a long-running content search, the selected key must be held down longer. The PAUSE key has the advantage that the Quick Search box remains open with your search text.
002_007_017=Forces Total Commander to continue transmitting the search filter, even if intermediate states (like an incomplete regular expression) temporarily lead to 0 matches:
002_007_018=Only disable this option if you fully understand what you are doing! May cause unexpected side effects. Requires a restart of Total Commander.
002_007_019=Force match at start of text or word (^) for the first search term by default:
002_007_020=When enabled, the very first search term in the first search branch must appear directly at the beginning of the item name (as if prefixed with ""^""). An explicit ""^"" inverts this behavior for that term.
002_007_021=Pattern search (%)
002_007_022=The anchors ^ and $ match not only at the start/end of the filename, but also at word boundaries (the following delimiters define a word):

002_008_001=Metadata
002_008_002=Here you can define custom aliases (e.g., ""@größe"" instead of ""@size"" or short ""@c"" instead of ""@content"") to adapt the search to your own language or personal preferences. Leaving the field empty disables the alias.
002_008_003=Custom alias for ""@gui"":
002_008_004=Custom alias for ""@name"":
002_008_005=Custom alias for ""@ext"":
002_008_006=Custom alias for ""@folder"":
002_008_007=Custom alias for ""@path"":
002_008_008=Custom alias for ""@desc"":
002_008_009=Custom alias for ""@content"":
002_008_010=Custom alias for ""@age"":
002_008_011=Custom alias for ""@size"":
002_008_012=Custom alias for ""@attr"":
002_008_101=Define additional metadata via content plugins (WDX)
002_008_102=Here you can register fields from external Total Commander plugins as custom metadata aliases. For example, if the ""Title"" field from the ""ShellDetails.wdx"" plugin is assigned the shortcut ""mp3"", you can filter by it in the search using ""@mp3 searchterm"". If multiple fields (e.g. also ""Artist"") are assigned to the same ""mp3"" shortcut, the engine searches all of these text fields simultaneously.
002_008_103=Active
002_008_104=Metadata alias
002_008_105=Architecture
002_008_106=Plugin path
002_008_107=INI path
002_008_108=Fieldname & Unit
002_008_109=Move up
002_008_110=Move down
002_008_111=Add definition
002_008_112=Remove definition
002_008_200=Default metadata aliases per source view
002_008_201=Usually you want to filter by file name. When searching the history or tab paths, it can be useful to search the entire path directly. Here you can configure the metadata alias used by default for each view. Of course, the active alias can still be overridden in the search string using ""@name"", ""@path"", or other aliases.
002_008_202=File panel (focus file panel → Ctrl+S):
002_008_203=Search results (Find Files → Feed to listbox → Ctrl+S):
002_008_204=Synchronize directories (Commands → Synchronize Dirs → start typing):
002_008_205=In the ""Synchronize directories"" window, Total Commander only provides a highlight search. Non-matching entries are not hidden; instead, you can navigate between matches using the Up/Down arrow keys.
002_008_206=Directory history (Alt+Down arrow → Ctrl+S):
002_008_207=Tab titles (Ctrl+Shift+A → start typing):
002_008_208=Tab paths (Ctrl+Shift+A → type ""*""):

002_009_001=Text replacements
002_009_002=Enable PinYin search (Chinese, e.g. ""ys"" matches ""耶稣""):
002_009_003=Enable Hangul search (Korean, e.g. ""ㅇㅅ"" matches ""예수""):
002_009_004=Please consult the help file for details on the 4 application scopes. Advanced mass replacement rules can also be defined via the ""{0}"" file.
002_009_005=Application Scope
002_009_006=Search for
002_009_007=Replace with
002_009_008=Move up
002_009_009=Move down
002_009_010=Add rule
002_009_011=Remove rule
002_009_012=1 - Search query only
002_009_013=2 - Elementname only (File/Folder)
002_009_014=3 - Both (Search query & Elementname)
002_009_015=4 - 1:1 Mapping
002_009_016=Ignore accents / diacritics (e.g. ""a"" matches ""ä"", ""á"", ""à"", ""â""):
002_009_017=Most text replacements (PinYin search, Hangul search, ignore accents/diacritics, and 1:1 mappings) are not supported in Regex and Pattern search.

002_010_001=Performance, caching & diagnostics
002_010_002=Internal logging verbosity (Log level):
002_010_003=Level 0 - No logging
002_010_004=Level 1 - Errors only
002_010_005=Level 2 - Plugin initialization
002_010_006=Level 3 - Technical search execution plans
002_010_007=Level 4 - Individual file scans
002_010_008=Level 5 - Low-level engine diagnostics
002_010_010=Metadata cache (File and folder information):
002_010_011=Disabled (Reread data on every pass)
002_010_012=Cache information of the last scanned directory only (uses less memory)
002_010_013=Use fixed limit for cache entries (better performance)
002_010_015=Metadata cache - Limit for cache entries:
002_010_016=Maximum file size for reading ""@content"" - larger files will be ignored (MB):
002_010_017=Total memory for ""@content"" cache - older content will be discarded when limit is reached (MB):

003_001_001=Documentation

003_002_001=Version ""{0} {1}""
003_002_002=Show Version ""{0} {1}""
003_002_003=# ❌ Documentation missing\n\n**File not found:**\n\n```{0}```
003_002_004=# ❌ Documentation missing\n\n**Error reading file:**\n\n```{0}```\n\n**Error message:**\n\n```\n{1}\n```

004_001_001=Log file

004_002_001=Log file from {0} ({1})
004_002_002=Delete log file
004_002_003=No log file available
004_002_004=[... Older log entries skipped for performance reasons ...]\n\n
004_002_005=❌ Log file missing\nFile not found: {0}
004_002_006=❌ Error reading log file\nFile: {0}\n\nError message:\n{1}

005_001_001=Personal Note

005_002_001={0}
005_002_002=Switch to ""{0}""

100_001_001=Open Settings
100_001_002=Errors (e.g., invalid regex expressions in search text) – Click to toggle visibility
100_001_003=Warnings (performance hints for slow searches) – Click to toggle visibility
100_001_004=Information (general performance metrics) – Click to toggle visibility
100_001_005=Search structure (visual representation of search logic) – Click to toggle visibility
100_001_006=Detected search special characters (in current search text) – Click to toggle visibility
100_001_007=Input hints (dynamic suggestions based on current search text) – Click to toggle visibility
100_001_008=Available control characters (overview of all active characters) – Click to toggle visibility
100_001_009=Text replacements (overview of active rules applying to the current filter text, min. 2 search characters) – Click to toggle visibility
100_001_010=Quick access (recent search history and direct search options) – Click to toggle visibility

100_002_001=Character for global OR operator: ""{0}"" – Splits the search string into independent branches. If at least one branch matches, an item is found.
100_002_002=Character for AND operator: ""{0}"" – Combines conditions within a search branch. All conditions must be met for the branch to match.
100_002_003=Character for local OR operator: ""{0}"" – Allows alternative terms within a single condition. At least one term must match for the condition to pass.
100_002_004=Character for escaping: ""{0}"" – Overrides the special function of the following control character and searches it as normal text.
100_002_005=Character for negation: ""{0}"" – Inverts the condition. For the condition to be met, the corresponding terms must not be contained.
100_002_006=Character to toggle case sensitivity: ""{0}"" – Although active by default, case sensitivity is ignored in this condition due to the toggle character.
100_002_007=Character to toggle case sensitivity: ""{0}"" – Although ignored by default, case sensitivity is observed in this condition due to the toggle character.
100_002_008=Character to force match at start of text: ""{0}"" – The search text for this condition must appear directly at the beginning of the item name.
100_002_009=Character to force match at end of text: ""{0}"" – The search text for this condition must appear directly at the end of the item name.
100_002_010=Character for standard search: ""{0}"" – Searches for the exact text anywhere within the item name.
100_002_011=Character for regex search: ""{0}"" – Interprets the text as a regular expression and searches with it.
100_002_012=Character for fuzzy search: ""{0}"" – Tolerates typos using an automatic Levenshtein distance.
100_002_013=Character for sequence search: ""{0}"" – Searches for characters in the specified order, allowing gaps in between.
100_002_014=Character for quoting: ""{0}"" – Overrides the special function of all control characters in the enclosed text and searches it as normal text.
100_002_015=Character to force match at start of text: ""{0}"" – The search text for this condition must appear directly at the beginning of the item name. Global settings automatically add this modifier to the first search term.
100_002_016=Character to disable match at start of text: ""{0}"" – Although global settings automatically add this modifier to the first search term, it is disabled by this character.
100_002_017=Character for pattern search: ""{0}"" – Interprets the text as a simplified pattern and searches with it.
100_002_100=Under the metadata alias ""{0}"", the following fields are searched simultaneously: {1}.
100_002_102=Under the metadata alias ""{0}"", the following field is searched: {1}.
100_002_103=The metadata alias ""{0}"" displays the search assistant.
100_002_104=Character for metadata: ""{0}"" – Switches the search context to the specified metadata alias and its associated fields.
100_002_110=GUI Control (display search assistant)
100_002_111=Name (e.g., ""Contract.docx"")
100_002_112=Extension (e.g., ""docx"")
100_002_113=Parent folder name (e.g., ""Documents"")
100_002_114=Full path (e.g., ""C:\\Data\\Documents\\Contract.docx"")
100_002_115=Description (file comments from ""descript.ion"" files)
100_002_116=Content (full-text search in files)
100_002_117=Modification age (Examples: "">14"", ""<2h"", "">2{0}5y"", ""=3d"")
100_002_118=File size (Examples: "">700"", ""<50M"", "">2{0}2G"", ""=7M"")
100_002_119=WDX: {0} - {1}
100_002_120=File attributes (e.g., ""r""=readonly, ""h""=hidden etc.)
100_002_200=Standard Search: Searches for the text ""{0}"".
100_002_201=Fuzzy Search: Tolerates typos in the search text ""{0}"" using automatic Levenshtein distance.
100_002_202=Sequence Search: Matches characters from ""{0}"" in exact order with arbitrary gaps in between.
100_002_203=Regex Search: Interprets ""{0}"" as a regular expression.
100_002_204=Regex Search: Error in regular expression ""{0}"" ({1}).
100_002_205=Regex: {0}
100_002_206=Pattern Search: Interprets ""{0}"" as pattern matching (Regex: ""{1}"").
100_002_207=Pattern Search: Pattern ""{0}"" was converted to regular expression ""{1}"", but generated an error: {2}
100_002_208=Pattern: {0}
100_002_210=from {0}
100_002_211=up to {0}
100_002_212={0} to {1}
100_002_213== {0}
100_002_214=≈ {0}
100_002_215=Invalid range: {0} to {1}
100_002_216=File size: At least {0} ({1} bytes).
100_002_217=File size: At most {0} ({1} bytes).
100_002_218=File size: Between {0} ({2} bytes) and {1} ({3} bytes).
100_002_219=File size: Exactly {0} ({1} bytes).
100_002_220=File size: Approximately {0} ({1} to {2} bytes).
100_002_221=File size: Invalid range! The lower bound {0} ({2} bytes) is greater than the upper bound {1} ({3} bytes).
100_002_222=File size: Invalid expression ""{0}"". Examples: "">700"", ""<50M"", "">2{1}2G"", ""=7M""
100_002_223=File size: {0}
100_002_224=File size filter: Restricts the search based on file size. Examples: "">700"", ""<50M"", "">2{0}2G"", ""=7M""
100_002_230=Units for modification age (seconds, minutes, hours, days, weeks, months, years)
100_002_231=S
100_002_232=M
100_002_233=H
100_002_234=D
100_002_235=W
100_002_236=MO
100_002_237=Y
100_002_240={0} seconds
100_002_241={0} minutes
100_002_242={0} hours
100_002_243={0} days
100_002_244={0} months
100_002_245={0} years
100_002_246=Now
100_002_250=from {0}
100_002_251=up to {0}
100_002_252={0} to {1}
100_002_253== {0}
100_002_254=≈ {0}
100_002_255=Invalid range: {0} to {1}
100_002_256=Modification age: At least {0}.
100_002_257=Modification age: At most {0}.
100_002_258=Modification age: Between {0} and {1}.
100_002_259=Modification age: Exactly {0}.
100_002_260=Modification age: Approximately {0}.
100_002_261=Modification age: Invalid range! The lower bound {0} is greater than the upper bound {1}.
100_002_262=Modification age: Invalid expression ""{0}"". Examples: "">14"", ""<2h"", "">2{1}5y"", ""=3d""
100_002_263=Modification age: {0}
100_002_264=Modification age filter: Restricts the search based on file modification date. Examples: "">14"", ""<2h"", "">2{0}5y"", ""=3d""

100_003_001={0} entries/s
100_003_002={0} entries/ms
100_003_003=Fast filter evaluation (values from previous search run): {0} entries evaluated in {1} ms ({2} ms/entry). Plugin startup time: {3} ms. The evaluation runs smoothly.
100_003_004=Slow filter evaluation (values from previous search run)! {0} entries evaluated in {1} ms ({2} ms/entry). Plugin startup time: {3} ms. Possible causes: Extensive logging, searching in file contents/WDX fields or disabled cache.
100_003_010=Logging active
100_003_011=Extensive logging is enabled. This can noticeably reduce search speed. Please disable or reduce it in the settings if it is not needed.

100_004_001=Escaping active
100_004_002=Quoting active
100_004_003=The character ""{0}"" activated escaping – The special function of the next character is disabled and it will be searched as normal text.
100_004_004=The character ""{0}"" started quoting – The special function of all control characters inside the block is disabled and text will be searched as normal text. Close quoting with {1}.
100_004_010=Search text
100_004_011=Enter your search text.
100_004_020=Equals operator: ""="" – Filters by value. If no operator is specified, equality is used automatically.
100_004_021=Greater-than operator: "">"" – Filters by values greater than the specified value.
100_004_022=Less-than operator: ""<"" – Filters by values less than the specified value.
100_004_023=Enter a numeric value. Use ""{0}"" as the decimal separator.
100_004_030=Unit: Byte – Measures size in bytes. The suffix ""B"" is optional.
100_004_031=Unit: Kilobyte – Measures size in kilobytes. Valid suffixes: ""K"" or ""KB"".
100_004_032=Unit: Megabyte – Measures size in megabytes. Valid suffixes: ""M"" or ""MB"".
100_004_033=Unit: Gigabyte – Measures size in gigabytes. Valid suffixes: ""G"" or ""GB"".
100_004_034=Unit: Terabyte – Measures size in terabytes. Valid suffixes: ""T"" or ""TB"".
100_004_035=Unit: Petabyte – Measures size in petabytes. Valid suffixes: ""P"" or ""PB"".
100_004_036=Unit: Exabyte – Measures size in exabytes. Valid suffixes: ""E"" or ""EB"".
100_004_040=Unit: Seconds – Measures age in seconds. Suffix: ""{0}"".
100_004_041=Unit: Minutes – Measures age in minutes. Suffix: ""{0}"".
100_004_042=Unit: Hours – Measures age in hours. Suffix: ""{0}"".
100_004_043=Unit: Days – Measures age in days. The suffix ""{0}"" is optional.
100_004_044=Unit: Weeks – Measures age in weeks. Suffix: ""{0}"".
100_004_045=Unit: Months – Measures age in months. Suffix: ""{0}"".
100_004_046=Unit: Years – Measures age in years. Suffix: ""{0}"".

100_005_001=Copy tooltip to clipboard
100_005_002=Insert ""{0}"" (Click)
100_005_003=Complete ""{0}"" (Click)

100_006_001=Text replacement: Searches for ""{0}"" and replaces it with ""{1}"".
100_006_002=Replace search text with ""{0}"" (Ctrl+Click)
100_006_003=Insert expanded text (""{0}"")

100_007_001=Show search history
100_007_002=(No search history available)
100_007_010=Paste clipboard
100_007_011=Paste clipboard (Click)
100_007_012=Replace search text with clipboard (Ctrl+Click)
100_007_020=Clear search bar
100_007_030=Case sensitivity enabled by default (Click to toggle)
100_007_031=Case sensitivity disabled by default (Click to toggle)
100_007_040=The very first search term in the first search branch must appear directly at the beginning of the item name. (Click to toggle)
100_007_041=The very first search term in the first search branch does not need to appear directly at the beginning of the item name. (Click to toggle)
100_007_050=Set Standard search as default search mode
100_007_051=Set Regex search as default search mode
100_007_052=Set Pattern search as default search mode
100_007_053=Set Fuzzy search as default search mode
100_007_054=Set Sequence search as default search mode
";

        public static readonly string personalNote = @"# ✍️ Personal Note

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

I encourage you to read for yourself and see: **Is this real?**";
    }
}
