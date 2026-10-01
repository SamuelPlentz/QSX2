using Markdig;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Serialization;
using Wpf.Ui.Appearance;

#pragma warning disable IDE0044 // Make field readonly
#pragma warning disable IDE1006 // Naming

namespace tcmatch.Core
{
    // Plugin
    #region Plugin Class
    /// <summary>
    /// Central entry point for the Total Commander plugin.
    /// Manages lifecycles, global configuration IO, and core API hooks.
    /// </summary>
    public static class Plugin
    {
        // Helpers
        #region Helpers
        private static readonly object _ioLock = new object();

        // Simplified structure holding only the required evaluation level and the pre-formatted message
        private struct PendingLogEntry
        {
            public LoggingLevel loggingLevel;
            public string formattedMessage;
        }

        // Thread-safe lock-free queue to cache logs during startup phase
        private static readonly ConcurrentQueue<PendingLogEntry> _logQueue = new ConcurrentQueue<PendingLogEntry>();
        private static readonly System.Diagnostics.Stopwatch _abortKeyPressTimer = new System.Diagnostics.Stopwatch();
        private static bool _isAbortKeyPressedByUser = false;
        #endregion

        // Global fields for the plugin lifetime
        #region Default Values
        public static char decimalSeparatorDefaultChar = '.';
        public static readonly bool usePinYinMatchingDefault = false;
        public static readonly bool useKoreanMatchingDefault = false;
        public static readonly Language languageDefault = Language.English;
        #endregion
        #region Global fields
        public static string version = "2026-10-01"; // #RELEASE

        // Path to files and folders
        public static string appFolder;             // e.g., C:\Total Commander\QSX2\
                                                    //       C:\Total Commander\QSX2\tcmatch.dll
                                                    //       C:\Total Commander\QSX2\tcmatch64.dll
                                                    //       C:\Total Commander\QSX2\tcmatch.Core.dll
        private static string pathOverrideFilePath; //       C:\Total Commander\QSX2\tcmatch.path.txt
        private static string pinyinFilePath;       //       C:\Total Commander\QSX2\tcmatch.pinyin.tbl
        public static string dataFolder;            // e.g., C:\Users\username\AppData\Roaming\QSX2\
        public static string logFilePath;           // e.g., C:\Users\username\AppData\Roaming\QSX2\tcmatch.log
        private static string configFilePath;       // e.g., C:\Users\username\AppData\Roaming\QSX2\tcmatch.xml
        public static string replaceFilePath;       // e.g., C:\Users\username\AppData\Roaming\QSX2\tcmatch.replacements.txt

        // Config & Runtime state settings
        public static LanguageManager languageManager = new LanguageManager();
        public static Config config { get; set; }

        public static ConfigWindow configWindow = null;

        public static int benchmarkEvaluationCount = 0;
        public static long benchmarkTotalExecutionTicks = 0;
        public static long benchmarkPluginStartTicks = 0;
        public static long benchmarkTotalStartupTicks = -1;

        public static string lastRawFilter { get; set; }
        public static readonly List<string> historyList = new List<string>();
        private static long historyLastTimestampTicks = 0;
        private static bool historyIsDeletingPhase = false;
        private const int HISTORY_LIMIT = 20;
        private const long HISTORY_INPUT_PAUSE_THRESHOLD_MILLISECONDS = 3000;

        public static SearchQueryPlan searchQueryPlan { get; set; }
        public static QueryPreProcessor queryPreProcessor = new QueryPreProcessor();


        private static bool isPinYinLoaded = false;
        public static ushort[] pinYinTable = null;
        public static readonly ushort[] koreanTable1 = new ushort[19] { 0x3131, 0x3132, 0x3134, 0x3137, 0x3138, 0x3139, 0x3141, 0x3142, 0x3143, 0x3145, 0x3146, 0x3147, 0x3148, 0x3149, 0x314A, 0x314B, 0x314C, 0x314D, 0x314E };
        public static readonly ushort[] koreanTable3 = new ushort[28] { 0x11A7, 0x3131, 0x3132, 0x3133, 0x3134, 0x3135, 0x3136, 0x3137, 0x3139, 0x313A, 0x313B, 0x313C, 0x313D, 0x313E, 0x313F, 0x3140, 0x3141, 0x3142, 0x3144, 0x3145, 0x3146, 0x3147, 0x3148, 0x314A, 0x314B, 0x314C, 0x314D, 0x314E };
        private static DateTime lastReplacementsFileWriteTime = DateTime.MinValue;
        public static List<ReplaceRule> stringReplacementsFile { get; set; } = new List<ReplaceRule>();
        #endregion

        // Core Plugin Initialization
        #region Constructor
        /// <summary>
        /// Triggers automatically on DLL load. Establishes environment paths, permissions and defaults.
        /// </summary>
        static Plugin()
        {
            benchmarkPluginStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            #region Startup Visuals
            int width = 44;

            // Determine the process architecture (64-Bit or 32-Bit)
            string bitness = Environment.Is64BitProcess ? "64-Bit" : "32-Bit";

            // Format the startup logging lines with version and architecture info
            string line1 = "QuickSearch eXtended 2 (C# Core) initialized";
            string line2 = $"Version: {version} ({bitness})";
            Log(LoggingLevel.Level_2_Startup, $"╔══════════════════════════════════════════════╗");
            Log(LoggingLevel.Level_2_Startup, $"║ {line1.PadRight(width)} ║");
            Log(LoggingLevel.Level_2_Startup, $"║ {line2.PadRight(width)} ║");
            Log(LoggingLevel.Level_2_Startup, $"╚══╤═══════════════════════════════════════════╝");
            #endregion
            #region 1. Establish the AppFolder (where the DLLs live)
            string assemblyLocation = typeof(Plugin).Assembly.Location;

            if(!string.IsNullOrEmpty(assemblyLocation)) {
                appFolder = Path.GetDirectoryName(assemblyLocation);
            } else {
                appFolder = AppDomain.CurrentDomain.BaseDirectory;
            }
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Log(LoggingLevel.Level_2_Startup, $"   ├── AppFolder (DLL and Data location):".PadRight(60) + appFolder);
            #endregion
            #region 2.1 Default DataFolder (%appdata%\QSX2)
            dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QSX2");
            #endregion
            #region 2.2 Check if "AppFolder\tcmatch.path.txt" exists and use the content to override the Default DataFolder
            pathOverrideFilePath = Path.Combine(appFolder, "tcmatch.path.txt");

            if(File.Exists(pathOverrideFilePath)) {
                Log(LoggingLevel.Level_2_Startup, $"   ├── Path override file found:".PadRight(60) + pathOverrideFilePath);
                try {
                    string overridePath = File.ReadAllText(pathOverrideFilePath, Encoding.UTF8).Trim();
                    string overridePathResolved = ResolveFolderPath(overridePath);

                    // Does it contain a valid and existing path?
                    if(!string.IsNullOrWhiteSpace(overridePathResolved) && Directory.Exists(overridePathResolved)) {
                        dataFolder = overridePathResolved;
                        Log(LoggingLevel.Level_2_Startup, $"   │   \"tcmatch.path.txt\" contains a valid path:".PadRight(60) + overridePath);
                        Log(LoggingLevel.Level_2_Startup, $"   │   Directory exists.".PadRight(60) + overridePathResolved);
                    } else {
                        Log(LoggingLevel.Level_2_Startup, $"   │   \"tcmatch.path.txt\" contains an invalid path:".PadRight(60) + overridePath);
                        Log(LoggingLevel.Level_2_Startup, $"   │   Directory doesn't exist".PadRight(60) + overridePathResolved);
                    }
                } catch(Exception ex) {
                    Log(LoggingLevel.Level_1_Errors, $"   │   Error reading \"tcmatch.path.txt\": {ex.Message}{Environment.NewLine}{ex}");
                }
            } else {
                Log(LoggingLevel.Level_2_Startup, $"   ├── No path override file found:".PadRight(60) + pathOverrideFilePath);
                Log(LoggingLevel.Level_2_Startup, $"   │   Using default AppData directory as DataFolder.");
            }
            Log(LoggingLevel.Level_2_Startup, $"   ├── DataFolder (Log and Config location):".PadRight(60) + dataFolder);
            #endregion
            #region 3. Fallback to Temp directory if current DataFolder is not writeable
            try {
                if(!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

                // Active write/delete test to ensure full NTFS permissions are given
                string writeTestFilePath = Path.Combine(dataFolder, "tcmatch.tmp");
                if(File.Exists(writeTestFilePath)) File.Delete(writeTestFilePath);
                File.WriteAllText(writeTestFilePath, "-");
                if(File.Exists(writeTestFilePath)) File.Delete(writeTestFilePath);
            } catch(Exception ex) {
                dataFolder = Path.GetTempPath();
                Log(LoggingLevel.Level_1_Errors,  $"   │   Error: Could not create or use data directory: {ex.Message}{Environment.NewLine}{ex}");
                Log(LoggingLevel.Level_2_Startup, $"   │   Using fallback:".PadRight(60) + dataFolder);
            }
            #endregion
            #region 4. Load Configuration
            configFilePath = Path.Combine(dataFolder, "tcmatch.xml");
            Log(LoggingLevel.Level_2_Startup, $"   ├── Config file:".PadRight(60) + configFilePath);

            // Auto-populate regional system settings directly from the active Host OS context
            string systemSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            decimalSeparatorDefaultChar = !string.IsNullOrEmpty(systemSeparator) ? systemSeparator[0] : '.';
            string currentLanguageIsoCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            usePinYinMatchingDefault = currentLanguageIsoCode == "zh";
            useKoreanMatchingDefault = currentLanguageIsoCode == "ko";

            // Find default language
            foreach(LanguageDefinition availableLanguage in Plugin.languageManager.availableLanguages.Values) {
                if(availableLanguage.languageIsoCode == currentLanguageIsoCode) {
                    languageDefault = availableLanguage.language;
                    break;
                }
            }

            LoadConfig();
            config.OnPostLoad();
            #endregion
            #region 5. Read "tcmatch.pinyin.tbl" if needed
            pinyinFilePath = Path.Combine(appFolder, "tcmatch.pinyin.tbl");

            // Initialize PinYin path and force early load if enabled in config
            LoadPinYinDatabase();
            #endregion
            #region 6. Read "tcmatch.replacements.txt" if exists
            replaceFilePath = Path.Combine(dataFolder, "tcmatch.replacements.txt");

            if(File.Exists(replaceFilePath)) {
                Log(LoggingLevel.Level_2_Startup, $"   ├── Replacements file found:".PadRight(60) + replaceFilePath);
                LoadReplacementsFile();
            } else {
                Log(LoggingLevel.Level_2_Startup, $"   ├── No replacements file found:".PadRight(60) + replaceFilePath);
            }
            #endregion
            #region Populate language and other internal elements
            config.PopulateViewFields_Language();
            config.PopulateInternalElements();
            config.PopulateViewFields_Theme();
            #endregion
            #region 7. Flush the bootstrap log into the newly initialized log file
            logFilePath = Path.Combine(dataFolder, "tcmatch.log");
            Log(LoggingLevel.Level_2_Startup, "   └── Initialisation completed.");
            #endregion
        }
        #endregion
        #region private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        /// <summary>
        /// Resolves and loads assemblies from architecture-specific subfolders or the main app folder.
        /// </summary>
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Extract the clean assembly name without version or culture details
            string assemblyName = new AssemblyName(args.Name).Name;

            // 1. Try loading from the architecture-specific subfolder (x64 or x32)
            string expectedPath = Path.Combine(appFolder, Environment.Is64BitProcess ? "x64" : "x32", assemblyName + ".dll");
            if(File.Exists(expectedPath)) return Assembly.LoadFrom(expectedPath);

            // 2. Fallback: Try loading directly from the main plugin directory
            expectedPath = Path.Combine(appFolder, assemblyName + ".dll");
            if(File.Exists(expectedPath)) return Assembly.LoadFrom(expectedPath);

            return null;
        }
        #endregion
        #region public static string ResolveFolderPath(string rawPath, string baseFolder = null)
        /// <summary>
        /// Expands environment variables and resolves relative paths anchored against baseFolder (defaults to appFolder).
        /// </summary>
        public static string ResolveFolderPath(string rawPath, string baseFolder = null)
        {
            if(string.IsNullOrWhiteSpace(rawPath)) return null;

            if(string.IsNullOrEmpty(baseFolder)) baseFolder = appFolder;

            // 1. Expand environment variables (e.g. %COMMANDER_PATH%\Plugins, %APPDATA%\QSX2)
            string expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim());

            // 2. Resolve relative paths against baseFolder (e.g. ".\Data" + "C:\TC\Plugins\QSX2" -> "C:\TC\Plugins\QSX2\Data")
            return Path.IsPathRooted(expanded) ? Path.GetFullPath(expanded) : Path.GetFullPath(Path.Combine(baseFolder, expanded));
        }
        #endregion
        #region public static void LoadConfig()
        /// <summary>
        /// Loads the configuration from tcmatch.xml or creates a default one if missing.
        /// </summary>
        public static void LoadConfig()
        {
            lock(_ioLock) {
                try {
                    if(File.Exists(configFilePath)) {
                        var serializer = new XmlSerializer(typeof(Config));
                        using(var stream = new FileStream(configFilePath, FileMode.Open, FileAccess.Read)) {
                            config = (Config)serializer.Deserialize(stream);
                        }
                    } else {
                        // Factory Reset / Initial creation
                        config = new Config();
                        SaveConfig();
                        Log(LoggingLevel.Level_2_Startup, "   │   No config found, write default config.");
                    }
                } catch(Exception ex) {
                    Log(LoggingLevel.Level_1_Errors, $"   │   Error: Could not load config: {ex.Message}{Environment.NewLine}{ex}");
                    // Fallback to defaults guarantees the plugin keeps running safely
                    config = new Config();
                }
            }
        }
        #endregion
        #region public static void SaveConfig()
        /// <summary>
        /// Serializes and writes the current configuration state back to tcmatch.xml.
        /// </summary>
        public static void SaveConfig()
        {
            lock(_ioLock) {
                try {
                    var serializer = new XmlSerializer(typeof(Config));
                    using(var stream = new StreamWriter(configFilePath, false, Encoding.UTF8)) {
                        serializer.Serialize(stream, config);
                    }
                } catch(Exception ex) {
                    Log(LoggingLevel.Level_1_Errors, $"Error: Could not write config: {ex.Message}{Environment.NewLine}{ex}");
                }
            }
        }
        #endregion
        #region public static void LoadReplacementsFile()
        /// <summary>
        /// Loads and validates advanced replacement rules from tcmatch.replacements.txt.
        /// </summary>
        public static void LoadReplacementsFile()
        {
            lock(_ioLock) {
                try {
                    stringReplacementsFile.Clear();
                    if(!File.Exists(replaceFilePath)) {
                        lastReplacementsFileWriteTime = DateTime.MinValue;
                        return;
                    }

                    // Check if the file has actually changed since the last load
                    DateTime currentWriteTime = File.GetLastWriteTime(replaceFilePath);
                    if(currentWriteTime == lastReplacementsFileWriteTime) return;
                    lastReplacementsFileWriteTime = currentWriteTime;

                    int lineNumber = 0;
                    using(var reader = new StreamReader(replaceFilePath, System.Text.Encoding.UTF8)) {
                        string line;
                        while((line = reader.ReadLine()) != null) {
                            lineNumber++;
                            if(string.IsNullOrEmpty(line)) continue;

                            int tabCount = line.Count(c => c == '\t');

                            // If a line contains tabs but not exactly 2, it's a structural error
                            if(tabCount > 0 && tabCount != 2) {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: Expected exactly 2 tabs, found {tabCount}: \"{line}\"");
                                continue;
                            }

                            // Treat pure comment lines (no tabs at all) safely
                            if(tabCount == 0) continue;

                            string[] parts = line.Split('\t');
                            string scopePart = parts[0].Trim();
                            string search = parts[1];
                            string replace = parts[2];

                            RuleScope parsedScope;
                            if(scopePart == "1") parsedScope = RuleScope.Filter;
                            else if(scopePart == "2") parsedScope = RuleScope.ElementName;
                            else if(scopePart == "3") parsedScope = RuleScope.Both;
                            else if(scopePart == "4") parsedScope = RuleScope.MatchOnly;
                            else {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: Invalid scope identifier \"{scopePart}\". Must be 1, 2, 3 or 4: \"{line}\"");
                                continue;
                            }

                            // Validation for Scopes 1, 2, 3: Search term must contain at least 1 character
                            if(parsedScope != RuleScope.MatchOnly && string.IsNullOrEmpty(search)) {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: Search term must not be empty for Scopes 1-3: \"{line}\"");
                                continue;
                            }

                            // Validation for Scope 4: Search term must be EXACTLY 1 character long
                            if(parsedScope == RuleScope.MatchOnly && search.Length != 1) {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: For 1:1 Mapping (Scope 4), the search term must be exactly 1 character long. Found {search.Length} chars: \"{line}\"");
                                continue;
                            }

                            // Validation for Scope 4: Replacement target must not be empty
                            if(parsedScope == RuleScope.MatchOnly && string.IsNullOrEmpty(replace)) {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: For 1:1 Mapping (Scope 4), the replacement target must not be empty: \"{line}\"");
                                continue;
                            }

                            // Pipeline-Conflict Check: Compare against already added file-bound rules
                            bool hasConflict = false;
                            if(parsedScope == RuleScope.Filter || parsedScope == RuleScope.Both) {
                                if(stringReplacementsFile.Any(r => r.searchFor == search && (r.scope == RuleScope.Filter || r.scope == RuleScope.Both))) hasConflict = true;
                            }
                            if(parsedScope == RuleScope.ElementName || parsedScope == RuleScope.Both) {
                                if(stringReplacementsFile.Any(r => r.searchFor == search && (r.scope == RuleScope.ElementName || r.scope == RuleScope.Both))) hasConflict = true;
                            }
                            if(parsedScope == RuleScope.MatchOnly) {
                                if(stringReplacementsFile.Any(r => r.searchFor == search && r.scope == RuleScope.MatchOnly)) hasConflict = true;
                            }

                            if(hasConflict) {
                                Log(LoggingLevel.Level_2_Startup, $"   │   [Line {lineNumber}] Error: The search term \"{search}\" has duplicate or conflicting scope assignments within the file: \"{line}\"");
                                continue;
                            }

                            stringReplacementsFile.Add(new ReplaceRule(search, replace, parsedScope));
                        }
                    }
                    Log(LoggingLevel.Level_2_Startup, $"   │   Successfully loaded {stringReplacementsFile.Count} replacement rules.");
                } catch(Exception ex) {
                    lastReplacementsFileWriteTime = DateTime.MinValue;
                    Log(LoggingLevel.Level_1_Errors, $"   │   Error: Could not load replacements file: {ex.Message}{Environment.NewLine}{ex}");
                }
            }
        }
        #endregion
        #region public static void LoadPinYinDatabase()
        /// <summary>
        /// Loads the PinYin translation database into memory if enabled and not already loaded.
        /// </summary>
        public static void LoadPinYinDatabase()
        {
            // If it's already in memory, we have absolutely nothing to do
            if(isPinYinLoaded) return;

            Log(LoggingLevel.Level_2_Startup, $"   ├── Loading PinYin database:".PadRight(60) + pinyinFilePath);

            if(!config.usePinYinMatching) {
                Log(LoggingLevel.Level_2_Startup, $"   │   PinYin is disabled, skipped import.");
                return;
            }

            if(!File.Exists(pinyinFilePath)) {
                Log(LoggingLevel.Level_2_Startup, $"   │   Error: PinYin database file is missing");
                return;
            }

            lock(_ioLock) {
                try {
                    // Instanciate the array size dynamically right before loading (0x9FA5 - 0x4E00 + 1)
                    int arrayLength = 20902;
                    int expectedByteSize = arrayLength * sizeof(ushort);

                    var fileInfo = new FileInfo(pinyinFilePath);
                    if(fileInfo.Length != expectedByteSize) {
                        Log(LoggingLevel.Level_2_Startup, $"   │   Error: PinYin file size mismatch. Expected {expectedByteSize} bytes, found {fileInfo.Length} bytes.");
                        return;
                    }

                    // Read the raw binary data from the file
                    byte[] rawBuffer = new byte[expectedByteSize];
                    using(var stream = new FileStream(pinyinFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        int bytesRead = stream.Read(rawBuffer, 0, expectedByteSize);
                        if(bytesRead != expectedByteSize) {
                            Log(LoggingLevel.Level_2_Startup, $"   │   Error: Could not read full PinYin table data from file.");
                            return;
                        }
                    }

                    // Now allocate the memory for the global table
                    pinYinTable = new ushort[arrayLength];

                    // High-performance block-copy from byte array straight into our ushort array
                    Buffer.BlockCopy(rawBuffer, 0, pinYinTable, 0, expectedByteSize);

                    isPinYinLoaded = true;
                    Log(LoggingLevel.Level_2_Startup, "   │   PinYin database successfully loaded into memory.");
                } catch(Exception ex) {
                    isPinYinLoaded = false;
                    pinYinTable = null;
                    Log(LoggingLevel.Level_1_Errors, $"   │   Error: Could not load PinYin database: {ex.Message}{Environment.NewLine}{ex}");
                }
            }
        }
        #endregion
        #region public static string ConfigToString(Config config)
        /// <summary>
        /// Serializes the current configuration state into an XML-formatted string.
        /// </summary>
        public static string ConfigToString(Config config)
        {
            try {
                var serializer = new XmlSerializer(typeof(Config));
                using(var stringWriter = new StringWriter()) {
                    serializer.Serialize(stringWriter, config);
                    return stringWriter.ToString();
                }
            } catch(Exception ex) {
                Log(LoggingLevel.Level_1_Errors, $"Error: Could not serialize config to string: {ex.Message}{Environment.NewLine}{ex}");
                return string.Empty;
            }
        }
        #endregion
        #region public static Config ConfigDeepClone(Config sourceConfig)
        /// <summary>
        /// Creates a deep copy of the specified configuration object using XML serialization.
        /// </summary>
        public static Config ConfigDeepClone(Config sourceConfig)
        {
            try {
                var serializer = new XmlSerializer(typeof(Config));
                using(var memoryStream = new MemoryStream()) {
                    // Serialize the source object into memory
                    serializer.Serialize(memoryStream, sourceConfig);
                    memoryStream.Position = 0;

                    // Deserialize into a completely new instance
                    var clonedConfig = (Config)serializer.Deserialize(memoryStream);

                    return clonedConfig;
                }
            } catch(Exception ex) {
                Log(LoggingLevel.Level_1_Errors, $"Error: Could not deep clone config: {ex.Message}{Environment.NewLine}{ex}");
                return null;
            }
        }
        #endregion
        #region public static void Log(LoggingLevel loggingLevel, string message)
        /// <summary>
        /// Writes a timestamped message to the log file. Caches entries automatically if the configuration is not yet initialized.
        /// </summary>
        public static void Log(LoggingLevel loggingLevel, string message)
        {
            if(config != null && config.loggingLevel < loggingLevel) return;

            try {
                // 1. Capture timestamp immediately and bake it into the final message string
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string formattedLine = $"[{timestamp}] {message}{Environment.NewLine}";

                // 2. Always enqueue the entry to maintain strict chronological order
                _logQueue.Enqueue(new PendingLogEntry {
                    loggingLevel = loggingLevel,
                    formattedMessage = formattedLine
                });

                // 3. Check if the environment is fully initialized to process the queue
                if(config != null && !string.IsNullOrEmpty(logFilePath)) {
                    // Dequeue and process all available items safely without manual locking
                    while(_logQueue.TryDequeue(out PendingLogEntry entry)) {
                        // Apply late-filtering based on the now available active log level configuration
                        if(config.loggingLevel >= entry.loggingLevel) {
                            byte[] encodedText = Encoding.UTF8.GetBytes(entry.formattedMessage);

                            // FileShare.ReadWrite allows multiple concurrent Total Commander processes to log simultaneously
                            using(var fs = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
                                fs.Write(encodedText, 0, encodedText.Length);
                            }
                        }
                    }
                }
            } catch {
                // A logger must never crash the main application under any circumstances
            }
        }
        #endregion
        #region public static bool IsLoggable(LoggingLevel loggingLevel)
        /// <summary>
        /// Checks if the specified logging level is currently active.
        /// </summary>
        public static bool IsLoggable(LoggingLevel loggingLevel)
        {
            if(config == null) throw new Exception("IsLoggable should not be used on startup.");
            return config.loggingLevel >= loggingLevel;
        }
        #endregion
        #region public static void Log(string message)
        /// <summary>
        /// Writes a pre-filtered message to the log file. Must only be called if IsLoggable returned true.
        /// </summary>
        public static void Log(string message)
        {
            Log(LoggingLevel.Level_0_None, message);
        }
        #endregion

        // Track History
        #region private static void TrackHistory(string rawFilter)
        private static void TrackHistory(string rawFilter)
        {
            long currentTicks = DateTime.UtcNow.Ticks;
            long elapsedMilliseconds = (currentTicks - historyLastTimestampTicks) / TimeSpan.TicksPerMillisecond;
            historyLastTimestampTicks = currentTicks;

            string current = rawFilter ?? string.Empty;
            string previous = lastRawFilter ?? string.Empty;

            if(previous.Length >= 2) {
                if(elapsedMilliseconds >= HISTORY_INPUT_PAUSE_THRESHOLD_MILLISECONDS) {
                    // Lazy commit: If more than 3 seconds elapsed since last input, commit previous filter
                    CommitToHistoryList(previous);
                    historyIsDeletingPhase = false;
                }

                if(!historyIsDeletingPhase && current.Length <= 1) {
                    // Search text cleared or reduced to <= 1 char -> commit previous filter immediately
                    CommitToHistoryList(previous);
                    historyIsDeletingPhase = false;
                } else if(!historyIsDeletingPhase && current.Length < previous.Length) {
                    // Text shortened: First character loss -> commit state directly before deletion
                    CommitToHistoryList(previous);
                    historyIsDeletingPhase = true;
                }
            }

            if(current.Length >= previous.Length) {
                // Text extended or replaced
                historyIsDeletingPhase = false;
            }
        }
        #endregion
        #region private static void CommitToHistoryList(string filter)
        private static void CommitToHistoryList(string filter)
        {
            // Remove duplicates and prepend to top (LRU style)
            historyList.Remove(filter);
            historyList.Insert(0, filter);

            // Maintain max capacity limit
            if(historyList.Count > HISTORY_LIMIT) {
                historyList.RemoveAt(historyList.Count - 1);
            }
        }
        #endregion
        #region public static void FlushHistoryOnClose()
        public static void FlushHistoryOnClose()
        {
            string previous = lastRawFilter ?? string.Empty;

            // Flushes current search filter to history on close.
            if(previous.Length >= 2) {
                CommitToHistoryList(previous);
            }
        }
        #endregion

        // Total Commander - tcmatch Interface
        #region public static int MatchGetSetOptions(int status)
        /// <summary>
        /// Total Commander sends the current search options, and the plugin communicates back which search options should be used.
        /// </summary>
        /// <param name="status">Bitmask of current TC internal quick search settings:
        /// 1: Configuration - Quick Search - Exact name match - Beginning
        /// 2: Configuration - Quick Search - Exact name match - Ending
        /// </param>
        /// <returns>A bitmask sum of requested operational modes:
        /// 1: Override internal search                                                                                                                                    - always active
        /// 2: No leading/trailing asterisks                                                                                                                               - always active
        /// 4: Pass full file path, not just the file name                                                                                                                 - always active
        /// 8: Allow empty search results. Forces TC to continue sending the filter string even if intermediate states (like an incomplete Regex clause) return 0 matches. - depends on configuration
        /// </returns>
        public static int MatchGetSetOptions(int status)
        {
            Log(LoggingLevel.Level_5_Function_Calls, "public static int MatchGetSetOptions(int status)");
            int result = 7;
            if(config.allowEmptyResult) result |= 8;

            if(IsLoggable(LoggingLevel.Level_3_SearchQueryPlan)) Log($"MatchGetSetOptions(Status: {status}) => {result}.");
            return result;
        }
        #endregion
        #region public static int MatchFileExW(string filter, string filename, int flags)
        /// <summary>
        /// Core filtering evaluation invoked by Total Commander for every single entry in the active view.
        /// Handles logging orchestration, exception tracking, and safety fallbacks.
        /// </summary>
        /// <param name="filter">The active search string typed by the user.</param>
        /// <param name="filename">The current entry under evaluation (including path).</param>
        /// <param name="flags">Execution context bitmask identifying source view and environment states:
        /// Source Views (Lower 8 bits - Values 1 to 6)
        ///  1 (FileList): Filtering the standard left or right file panel view. (focus file panel → Ctrl+S)
        ///  2 (SearchResults): Filtering within the "Find Files" results window. (Find Files → Feed to listbox → Ctrl+S)
        ///  3 (Synchronize): Filtering inside the "Synchronize Directories" comparison tool. (Commands → Synchronize Dirs → start typing; no filter only match; no ctrl+s)
        ///  4 (History): Filtering the directory history or frequent folders dropdown window. (Alt+Down arrow → Ctrl+S)
        ///  5 (TabTitles): Filtering the folder tab titles. (Ctrl+Shift+A → start typing; context menu on tab → list all tabs; context menu beside tab)
        ///  6 (TabPaths): Filtering the folder tab paths. (Ctrl+Shift+A → type "*"; command "cm_SrcTabsList 1"; command "cm_SrcTabsList 3")
        /// Context Modifiers (Bitmask of upper bits)
        ///  0x100 (LeftSide): Search executes in the left panel. If unset, target is the right panel.
        ///  0x200 (QuickFilter): Quick Filter mode is active (hides mismatches entirely instead of stepping through matches).
        ///  0x400 (WatchDirs): Background directory tracking is pushing instant updates through the DLL.
        ///  0x800 (FirstMatch): Signals the initial call of a brand-new search query sequence (ideal for cache invalidation).
        /// </param>
        /// <returns>Returns 1 if the entry passes the filter criteria; 0 if it should be skipped.</returns>
        public static int MatchFileExW(string filter, string filename, int flags)
        {
            long benchmarkMatchFileExWStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if(benchmarkTotalStartupTicks == -1) benchmarkTotalStartupTicks = benchmarkMatchFileExWStartTicks - benchmarkPluginStartTicks;

            Log(LoggingLevel.Level_5_Function_Calls, "public static int MatchFileExW(string filter, string filename, int flags)");
            int result;

            //WindowHelper.AllowLargeNumbers(true);
            try {
                result = EvaluateMatch(filter, filename, flags);
            } catch(Exception ex) {
                Log(LoggingLevel.Level_1_Errors, $"Error matching file (Filter: {filter}, Flags: 0x{flags:X}, Filename: {filename}): {ex.Message}{Environment.NewLine}{ex}");

                result = 0; // Fallback to pass-through on crash
            }
            //WindowHelper.AllowLargeNumbers(false);

            // Performance Optimization: Only assemble readable log strings if the entry is actually going to be logged
            if(IsLoggable(LoggingLevel.Level_4_FileEvaluation)) {
                #region Make flags readable for logging
                // 1. Extract the source view (lower 8 bits)
                int viewId = flags & 0xFF;
                string viewText;

                switch(viewId) {
                    case 1: viewText = "FileList"; break;
                    case 2: viewText = "SearchResults"; break;
                    case 3: viewText = "Synchronize"; break;
                    case 4: viewText = "History"; break;
                    case 5: viewText = "TabTitles"; break;
                    case 6: viewText = "TabPaths"; break;
                    default: viewText = viewId.ToString(); break;
                }

                // 2. Extract the context modifiers
                List<string> activeFlags = new List<string> { viewText };

                if((flags & 0x100) != 0) activeFlags.Add("isLeftSide");
                if((flags & 0x200) != 0) activeFlags.Add("isQuickFilter");
                if((flags & 0x400) != 0) activeFlags.Add("isWatchDirs");
                if((flags & 0x800) != 0) activeFlags.Add("isFirstMatch");

                // 3. Catch future flags: Mask out everything we already know
                int unknownBits = flags & ~(0xFF | 0x100 | 0x200 | 0x400 | 0x800);
                if(unknownBits != 0) activeFlags.Add($"unknownFlags: 0x{unknownBits:X}");

                // 3. Combine them into a single string: "FileList, isLeftSide, isQuickFilter, isFirstMatch"
                string flagsReadable = string.Join(", ", activeFlags);
                #endregion

                Log($"MatchFileExW(Filter: {filter}, Flags: {flagsReadable}, Filename: {filename}) => {result}.");
            }

            benchmarkEvaluationCount++;
            benchmarkTotalExecutionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - benchmarkMatchFileExWStartTicks;

            return result;
        }
        #endregion

        // evaluate Match
        #region private static int EvaluateMatch(string rawFilter, string filename, int flags)
        /// <summary>
        /// Internal worker for MatchFileExW. Resolves parsing, caching, and AST execution.
        /// </summary>
        private static int EvaluateMatch(string rawFilter, string filename, int flags)
        {
            // 1. Extract context modifiers
            bool isFirstMatch = (flags & 0x800) != 0;
            int viewId = flags & 0xFF;

            // Total Commander 11.03 release candidate 3 - Strip auto prepended asterisk ("*") for TabPaths (View 6) before any pre-processing or parsing occurs
            //  > 26.01.24 Added: cm_SrcTabsList, cm_TrgTabsList, cm_LeftTabsList, cm_RightTabsList: New parameter 2 to open quick search directly, with *at start if combined with 1 (32/64)
            //  > 28.01.24 Added: cm_SrcTabsList, cm_TrgTabsList, cm_LeftTabsList, cm_RightTabsList: When using parameter 1, pre - load quick search string with "*" (32/64)
            //  > 01.02.24 Fixed: cm_* TabsList with parameter 1 or 3: Automatically adding "*" to quick search string only worked with quick filter enabled and internal quick search(32/64)
            //  > 04.02.24 Fixed: cm_* TabsList with parameter 1: Automatically adding "*" to quick search string didn't add the typed character (32)
            if(viewId == 6 /* TabPaths */ && !string.IsNullOrEmpty(rawFilter) && rawFilter.StartsWith("*")) {
                rawFilter = rawFilter.Substring(1);
            }

            // 2. Cache invalidation and query compilation
            if(isFirstMatch || searchQueryPlan == null || lastRawFilter != rawFilter) {
                _isAbortKeyPressedByUser = false;

                TrackHistory(rawFilter);

                EvaluationContextCache.InitializeSequence();

                // Run advanced string replacements exclusively for the user search filter
                string processedFilter = queryPreProcessor.PreProcess(rawFilter, isFilter: true);

                searchQueryPlan = QueryParser.Parse(rawFilter, processedFilter, viewId);

                if(searchQueryPlan == null) Log(LoggingLevel.Level_1_Errors, $"Error parsing SearchQueryPlan for \"{processedFilter}\"");
                else {
                    searchQueryPlan.benchmarkEvaluationCount = benchmarkEvaluationCount;
                    searchQueryPlan.benchmarkTotalExecutionTicks = benchmarkTotalExecutionTicks;
                    searchQueryPlan.benchmarkTotalStartupTicks = benchmarkTotalStartupTicks;


                    if(IsLoggable(LoggingLevel.Level_3_SearchQueryPlan)) Log(searchQueryPlan.ToQueryPlanString());

                    config.usedMetadataGui = searchQueryPlan.usedMetadataGui;

                    if(config.showGui || config.usedMetadataGui) WindowHelper.EnsureSearchAssistantRunning();
                }
                benchmarkEvaluationCount = 0;
                benchmarkTotalExecutionTicks = 0;
                lastRawFilter = rawFilter;
            }

            // 3. Shortcuts
            if(_isAbortKeyPressedByUser) return 0;
            if(IsAbortKeyPressedByUser()) { _isAbortKeyPressedByUser = true; return 0; }
            if(searchQueryPlan == null) return 0;
            if(string.IsNullOrEmpty(rawFilter)) return 1; // Pass-through if no filter is specified
            if(string.IsNullOrEmpty(filename)) return 0;  // Guard clause against broken data pointers

            // 4. Feed the current context with paths
            FileSystemEntryType? forcedType = null;
            if(viewId == 5 /* TabTitles */) {
                forcedType = FileSystemEntryType.CustomText;
            }

            // 5. Get entry from cache
            FileSystemEvaluationContext context = EvaluationContextCache.GetOrCreate(filename, forcedType);

            // 6. Evaluate tree
            bool isMatch = FileMatcher.IsMatch(searchQueryPlan, context);

            return isMatch ? 1 : 0;
        }
        #endregion
        #region private static bool IsAbortKeyPressedByUser()
        /// <summary>
        /// Evaluates user abort requests via ESC or PAUSE keys based on configuration.
        /// Hyper-optimized via short-circuiting: Heavy window focus checks are only fired if a key is physically pressed.
        /// </summary>
        /// <returns>True if the user requested a hard abort and the limit was reached, false otherwise.</returns>
        private static bool IsAbortKeyPressedByUser()
        {
            var strategy = Plugin.config.abortKeyStrategy;
            if(strategy == AbortKeyStrategy.Disabled) return false;

            bool isAbortPressed = false;

            // 1. Ultra-cheap bit check first (Fails instantly in 99.9% of all file iterations without hitting window manager)
            if((strategy == AbortKeyStrategy.EscapeOnly || strategy == AbortKeyStrategy.Both) && (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0) {
                isAbortPressed = true;
            } else if((strategy == AbortKeyStrategy.PauseOnly || strategy == AbortKeyStrategy.Both) && (NativeMethods.GetAsyncKeyState(0x13) & 0x8000) != 0) {
                isAbortPressed = true;
            }

            // 2. Heavy window validation ONLY fires if the user is actually pushing a valid abort key
            if(isAbortPressed) {
                IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();

                if(WindowHelper.IsTotalCommanderQuicksearchWindow(foregroundWindow)) {
                    if(!_abortKeyPressTimer.IsRunning) {
                        _abortKeyPressTimer.Start();
                    }

                    if(_abortKeyPressTimer.ElapsedMilliseconds >= 250) return true;
                    else return false;
                }
            }

            _abortKeyPressTimer.Reset();
            return false;
        }
        #endregion
    }
    #endregion

    // Helpers
    #region Helpers
    #region Language Enum
    /// <summary>
    /// The language used in the gui.
    /// </summary>
    public enum Language
    {
        English,
        German
    }
    #endregion
    #region LanguageDefinition Class
    public class LanguageDefinition
    {
        public Language language { get; set; }
        public string languageNameEnglish { get; set; }
        public string languageNameLocal { get; set; }
        public string languageIsoCode { get; set; }
        public string readmeVersion { get; set; }
        public string personalNote { get; set; }
        public string rawData { get; set; }
    }
    #endregion
    #region Theme Enum
    /// <summary>
    /// The theme used in the gui.
    /// </summary>
    public enum Theme
    {
        Light,
        Dark
    }
    #endregion
    #region LoggingLevel Enum
    /// <summary>
    /// Defines the verbosity level of the plugin's internal logging engine.
    /// </summary>
    public enum LoggingLevel
    {
        Level_0_None,
        Level_1_Errors,
        Level_2_Startup,
        Level_3_SearchQueryPlan,
        Level_4_FileEvaluation,
        Level_5_Function_Calls
    }
    #endregion
    #region SearchMode Enum
    /// <summary>
    /// Specifies the evaluation strategy used for a given search condition.
    /// </summary>
    public enum SearchMode
    {
        Standard, // '=' or implicit literal matching
        Regex,    // '?' internal regular expressions
        Pattern,  // '%' simplified pattern matching with ranges and wildcards
        Fuzzy,    // '<' Levenshtein-distance based typing tolerance
        Sequence, // '*' sequence or chronological character search
        Math      // '>', '=', '<' specialized metadata numerical processing
    }
    #endregion
    #region MetadataConstants Class (Enum)
    /// <summary>
    /// Provides static mappings for built-in system metadata identifiers.
    /// Custom Content Plugins (.wdx) are dynamically mapped to IDs starting from 100.
    /// </summary>
    public static class MetadataConstants
    {
        public const int Gui = 0;         // @gui
        public const int Name = 1;        // @name
        public const int Ext = 2;         // @ext
        public const int Folder = 3;      // @folder
        public const int Path = 4;        // @path
        public const int Attr = 5;        // @attr
        public const int Desc = 6;        // @desc
        public const int Content = 7;     // @content
        public const int Age = 8;         // @age
        public const int Size = 9;        // @size
        public const int CustomWdx = 100; // @...
    }
    #endregion
    #region InputContext Enum
    /// <summary>
    /// Classification of the last input context to drive intelligent autocomplete hints.
    /// </summary>
    public enum InputContext
    {
        None,
        
        MetadataAliasTyping,
        
        ConditionStart,
        ConditionModifier,
        ConditionSearchMode,
        ConditionInTerm,
        ConditionInTermRegexOrPattern,

        MathOperatorExpected,
        MathInputExpected,
        MathSizeUnitExpected,
        MathAgeUnitExpected
    }
    #endregion
    #region FileSystemEntryType Enum
    public enum FileSystemEntryType
    {
        File,
        Directory,
        CustomText
    }
    #endregion
    #region CacheStrategy Enum
    /// <summary>
    /// Defines how metadata and file evaluation context objects are retained in memory.
    /// </summary>
    public enum CacheStrategy
    {
        /// <summary>No metadata caching is performed. Files are re-evaluated on every pass.</summary>
        Disabled = 0,
        /// <summary>Retains metadata only for elements within the most recently evaluated directory view.</summary>
        LastDirectoryOnly = 1,
        /// <summary>Retains a fixed absolute maximum number of metadata objects in memory across directories.</summary>
        SpecificCount = 2
    }
    #endregion
    #region AbortKeyStrategy Enum
    /// <summary>
    /// Defines the interception strategy for user abort requests during long-running file scans.
    /// </summary>
    public enum AbortKeyStrategy
    {
        Disabled = 0,
        EscapeOnly = 1,
        PauseOnly = 2,
        Both = 3
    }
    #endregion
    #region GuiDockingTarget Enum
    /// <summary>
    /// Specifies the anchor or reference element used to position and snap the plugin's status/helper GUI.
    /// </summary>
    public enum GuiDockingTarget
    {
        /// <summary>Positions the GUI relative to the coordinates of the active monitor workspace.</summary>
        DesktopScreen = 0,
        /// <summary>Bounds and positions the GUI relative to the Total Commander main window.</summary>
        TotalCommanderMainWindow = 1,
        /// <summary>Bounds and positions the GUI relative to the active Total Commander Quick Filter dialog box.</summary>
        TotalCommanderSearchWindow = 2
    }
    #endregion
    #region GuiDockingCorner Enum
    /// <summary>
    /// Specifies the exact corner of the target element where the GUI should align.
    /// </summary>
    public enum GuiDockingCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3
    }
    #endregion
    #region AgeLimits Class
    /// <summary>
    /// Upper boundary for evaluation (~10,000 years strictly bounded to avoid stack overflow conditions inside mixed unmanaged/managed execution loops).
    /// </summary>
    public static class AgeLimits
    {
        /// <summary>
        /// Upper bound for age queries (~10,000 years ≈ 3,650,000 days).
        ///
        /// Aditional background: see WindowHelper.AllowLargeNumbers
        /// 
        /// Layered plugin architecture:
        /// Total Commander (Delphi Host) → C++/CLI Wrapper Plugin → C#/.NET Core Plugin (tcmatch.Core)
        ///
        /// In this execution chain, using double.MaxValue as a sentinel value has previously resulted in a StackOverflowException at runtime when executed through the full plugin pipeline.
        ///
        /// To ensure deterministic and stable behavior in the mixed native/managed environment, a bounded domain-specific limit is used instead.
        /// </summary>
        public const double MaxValue = 3_650_000;
    }
    #endregion
    #region GuiSectionDisplayState Enum
    /// <summary>
    /// Defines the visibility and initial expansion state of a GUI section.
    /// </summary>
    public enum GuiSectionDisplayState
    {
        Hidden,    // Section is completely disabled and not rendered
        Collapsed, // Section is enabled but closed by default
        Expanded   // Section is enabled and opened by default
    }
    #endregion
    #region GuiSectionType Enum
    /// <summary>
    /// Defines the available UI sections in the search assistant.
    /// </summary>
    public enum GuiSectionType
    {
        Errors,
        Warnings,
        Informations,
        SearchQueryPlan,
        UsedModifiers,
        DynamicHints,
        AvailableCharsList,
        TextReplacements,
        QuickAccess
    }
    #endregion
    #region RuleScope Enum
    /// <summary>
    /// Defines the specific execution scope where a replacement rule is injected into the search pipeline.
    /// </summary>
    public enum RuleScope
    {
        Filter,       // Applied exclusively to the search filter text before parsing syntax tokens
        ElementName,  // Applied exclusively to target elements (filenames, folder names, tab titles)
        Both,         // Applied to both the search filter and the element name before matching begins
        MatchOnly     // Virtual 1-to-1 character equivalence evaluated dynamically during the search loop
    }
    #endregion
    #region WdxDefinition Class
    /// <summary>
    /// Represents a single WDX plugin field mapping for UI-bound data grids.
    /// </summary>
    public class WdxDefinition
    {
        public bool isActive { get; set; } = true;
        public string metadataAlias { get; set; }
        public bool is64Bit { get; set; } = true;
        [XmlIgnore] public string pluginBitness => is64Bit ? Plugin.config.L[001_002_404] /* 64-bit */ : Plugin.config.L[001_002_403] /* 32-bit */;

        #region public string pluginPath
        private string _pluginPath = string.Empty;
        public string pluginPath {
            get { return _pluginPath; }
            set {
                if(_pluginPath != value) {
                    _pluginPath = value;
                    ResetPluginCache();
                }
            }
        }
        #endregion
        #region public string pluginIniPath
        private string _pluginIniPath = string.Empty;
        public string pluginIniPath {
            get { return _pluginIniPath; }
            set {
                if(_pluginIniPath != value) {
                    _pluginIniPath = value;
                    ResetPluginCache();
                }
            }
        }
        #endregion
        #region public string fieldAndUnit
        private string _fieldAndUnit = string.Empty;
        public string fieldAndUnit {
            get { return _fieldAndUnit; }
            set {
                if(_fieldAndUnit != value) {
                    _fieldAndUnit = value;
                    ResetFieldCache();
                }
            }
        }
        #endregion

        // Extract the plugin file name without extension (e.g. "exif.wdx" or "exif.wdx64" -> "exif"; "ShellDetails.uwdx" or "ShellDetails.wdx" -> "ShellDetails")
        [XmlIgnore] public string pluginName => Path.GetFileNameWithoutExtension(_pluginPath);

        [XmlIgnore] public string oldFieldAndUnit { get; set; }

        #region public WdxPluginInstance wdxPluginInstance
        [XmlIgnore] public bool wdxPluginInstanceTriedToLoad { get; set; } = false;
        [XmlIgnore] private WdxPluginInstance _wdxPluginInstance { get; set; } = null;
        [XmlIgnore]
        public WdxPluginInstance wdxPluginInstance {
            get {
                if(_wdxPluginInstance == null && !wdxPluginInstanceTriedToLoad) {
                    wdxPluginInstanceTriedToLoad = true;

                    // Get or load the WDX instance
                    _wdxPluginInstance = WdxPluginManager.GetOrLoadPlugin(pluginPath, pluginIniPath);
                }

                return _wdxPluginInstance;
            }
        }
        #endregion
        #region public WdxFieldDefinition wdxFieldDefinition
        [XmlIgnore] public bool wdxFieldDefinitionTriedToLoad { get; set; } = false;
        [XmlIgnore] public WdxFieldDefinition _wdxFieldDefinition { get; set; } = null;
        [XmlIgnore]
        public WdxFieldDefinition wdxFieldDefinition {
            get {
                if(_wdxFieldDefinition == null && !wdxFieldDefinitionTriedToLoad) {
                    wdxFieldDefinitionTriedToLoad = true;

                    // Get or load the WDX instance
                    if(wdxPluginInstance != null && wdxPluginInstance.wdxFieldDefinitionLookup != null) {
                        wdxPluginInstance.wdxFieldDefinitionLookup.TryGetValue(fieldAndUnit, out WdxFieldDefinition value);
                        _wdxFieldDefinition = value;
                    }
                }

                return _wdxFieldDefinition;
            }
        }
        #endregion

        #region Constructor
        public WdxDefinition() { }
        public WdxDefinition(bool isActive, string metadataAlias, bool is64Bit, string pluginPath, string pluginIniPath, string fieldAndUnit)
        {
            this.isActive = isActive;
            this.metadataAlias = metadataAlias;
            this.is64Bit = is64Bit;
            this.pluginPath = pluginPath;
            this.pluginIniPath = pluginIniPath;
            this.fieldAndUnit = fieldAndUnit;
        }
        #endregion
        #region public void ResetPluginCache()
        public void ResetPluginCache()
        {
            _wdxPluginInstance = null;
            wdxPluginInstanceTriedToLoad = false;
            ResetFieldCache();
        }
        #endregion
        #region public void ResetFieldCache()
        public void ResetFieldCache()
        {
            _wdxFieldDefinition = null;
            wdxFieldDefinitionTriedToLoad = false;
        }
        #endregion
    }
    #endregion
    #region ReplaceRule Class
    /// <summary>
    /// Represents a single character-mapping wrapper rule for UI-bound data grids.
    /// </summary>
    public class ReplaceRule
    {
        public string searchFor { get; set; }
        public string replaceWith { get; set; }
        public RuleScope scope { get; set; } = RuleScope.Filter;

        public ReplaceRule() { }
        public ReplaceRule(string searchFor, string replaceWith, RuleScope scope)
        {
            this.searchFor = searchFor;
            this.replaceWith = replaceWith;
            this.scope = scope;
        }
    }
    #endregion
    #region GuiSection Class
    /// <summary>
    /// Represents a single UI section configuration entry for ordering and visibility.
    /// </summary>
    public class GuiSection
    {
        public GuiSectionType guiSectionType { get; set; }
        public GuiSectionDisplayState guiSectionDisplayState { get; set; } = GuiSectionDisplayState.Expanded;

        /// <summary>
        /// Returns the localization resource ID for this section type.
        /// </summary>
        public int labelId {
            get {
                switch(guiSectionType) {
                    case GuiSectionType.Errors: return 002_004_010; /* Errors (e.g., invalid regex expressions in search text) */
                    case GuiSectionType.Warnings: return 002_004_011; /* Warnings (performance hints for slow searches) */
                    case GuiSectionType.Informations: return 002_004_012; /* Information (general performance metrics) */
                    case GuiSectionType.SearchQueryPlan: return 002_004_013; /* Search structure (visual representation of search logic) */
                    case GuiSectionType.UsedModifiers: return 002_004_014; /* Detected search special characters (in current search text) */
                    case GuiSectionType.DynamicHints: return 002_004_015; /* Input hints (dynamic suggestions based on current search text) */
                    case GuiSectionType.AvailableCharsList: return 002_004_016; /* Available control characters (overview of all active characters) */
                    case GuiSectionType.TextReplacements: return 002_004_017; /* Text replacements (overview of active rules applying to the current filter text, min. 2 search characters) */
                    case GuiSectionType.QuickAccess: return 002_004_018; /* Quick access (recent search history and direct search options) */

                    default: return 0;
                }
            }
        }

        public GuiSection() { }
        public GuiSection(GuiSectionType guiSectionType, GuiSectionDisplayState guiSectionDisplayState)
        {
            this.guiSectionType = guiSectionType;
            this.guiSectionDisplayState = guiSectionDisplayState;
        }
    }
    #endregion
    #region RangeChunkType Enum
    public enum RangeChunkType
    {
        Literal,
        Number
    } 
    #endregion

    // GUI
    #region ButtonStyle Enum
    public enum ButtonStyle
    {
        Primary,
        Scope,
        Error,
        Warning,
        Information,
        LogicalOperator,
        MetadataAlias,
        Modifier,
        ModifierDisabled,
        SearchOperator,
        SearchText,
        QuickAction,
        QuickActionDisabled
    }
    #endregion
    #region ActionButton Class
    public class ActionButton
    {
        public string content { get; set; }
        public string toolTip { get; set; }
        public System.Windows.Style style { get; set; }
        public Action clickAction { get; set; }
    }
    #endregion
    #region ButtonActionType Enum
    public enum ButtonActionType
    {
        None,
        OpenConfig,
        InsertText,
        AutoCompleteText,
        InsertPreset,
        HistoryMenu,
        ClipboardMenu,
        ClearSearchbar,
        ToggleCaseSensitive,
        ToggleMatchFirstTermAsStartAnchor,
        ToggleDefaultSearchModeStandard,
        ToggleDefaultSearchModeRegex,
        ToggleDefaultSearchModePattern,
        ToggleDefaultSearchModeFuzzy,
        ToggleDefaultSearchModeSequence
    }
    #endregion
    #endregion

    // Language
    #region LanguageManager Class
    public class LanguageManager
    {
        public readonly Dictionary<Language, LanguageDefinition> availableLanguages;

        public LanguageManager()
        {
            availableLanguages = new Dictionary<Language, LanguageDefinition> {
                { Language.English, new LanguageDefinition { language = Language.English, languageNameEnglish = TranslationEN.languageNameEnglish, languageNameLocal = TranslationEN.languageNameLocal, languageIsoCode = TranslationEN.languageIsoCode, readmeVersion = TranslationEN.readmeVersion, personalNote = TranslationEN.personalNote, rawData = TranslationEN.rawData } },
                { Language.German,  new LanguageDefinition { language = Language.German,  languageNameEnglish = TranslationDE.languageNameEnglish, languageNameLocal = TranslationDE.languageNameLocal, languageIsoCode = TranslationDE.languageIsoCode, readmeVersion = TranslationDE.readmeVersion, personalNote = TranslationDE.personalNote, rawData = TranslationDE.rawData } }
            };
        }
    }
    #endregion
    #region LanguageProvider Class
    public class LanguageProvider
    {
        private readonly Dictionary<int, string> _languageEntriesIntKey = new Dictionary<int, string>();
        private readonly Dictionary<string, string> _languageEntriesStringKey = new Dictionary<string, string>();

        // query with int key (1002003)
        public string this[int key] {
            get {
                if(_languageEntriesIntKey.TryGetValue(key, out var val)) return val;
                throw new KeyNotFoundException($"Language key missing: {key} ({FormatIntToStringKey(key)})");
            }
        }

        // query with string key (001_002_003)
        public string this[string key] {
            get {
                if(_languageEntriesStringKey.TryGetValue(key, out var val)) return val;
                throw new KeyNotFoundException($"Language key missing: '{key}'");
            }
        }

        public void SetData(Dictionary<int, string> source)
        {
            _languageEntriesIntKey.Clear();
            _languageEntriesStringKey.Clear();

            foreach(var kvp in source) {
                _languageEntriesIntKey[kvp.Key] = kvp.Value;
                _languageEntriesStringKey[FormatIntToStringKey(kvp.Key)] = kvp.Value;
            }
        }

        #region public string FormatIntToStringKey(int intKey)
        public string FormatIntToStringKey(int intKey)
        {
            int p1 = intKey / 1000000;
            int p2 = (intKey / 1000) % 1000;
            int p3 = intKey % 1000;
            return $"{p1:D3}_{p2:D3}_{p3:D3}";
        }
        #endregion
    }
    #endregion

    // Configuration
    #region Config Class
    /// <summary>
    /// Manages all user configuration settings, modifiers, and localization mappings.
    /// </summary>
    public class Config : INotifyPropertyChanged
    {
        #region Language
        [XmlAttribute("xml:space")] public string space { get; set; } = "preserve";

        /// <summary>Language used in the informational GUI panel.</summary>
        public Language language { get; set; } = Plugin.languageDefault;
        public string customTranslationOverride { get; set; } = string.Empty;

        [XmlIgnore] public LanguageProvider L { get; private set; } = new LanguageProvider();
        #endregion
        #region Theme
        /// <summary>Theme used in the GUI panel.</summary>
        public Theme theme { get; set; } = Theme.Light;
        public string customColorOverride { get; set; } = string.Empty;

        [XmlIgnore] public Dictionary<string, bool> b { get; private set; } = new Dictionary<string, bool>();
        [XmlIgnore] public Dictionary<string, object> B { get; private set; } = new Dictionary<string, object>();

        [XmlIgnore]
        public static readonly IReadOnlyDictionary<Theme, string> defaultThemes = new Dictionary<Theme, string> {
            {
                Theme.Light, @"
#     Window (1.1 Accent color) - Header (2.1 Text color, 2.2 Bottom line)
01_01_01=blue70
01_02_01=blue70
01_02_02=rose50, fuchsia50, 0

#     Banner (1 Background, 2 TextColor, 3 Border) - Variants (1 Info - 2 Warning - 3 Error)
02_01_01=blue10
02_01_02=blue95
02_01_03=blue40

02_02_01=amber10
02_02_02=amber95
02_02_03=amber40

02_03_01=rose10
02_03_02=rose95
02_03_03=rose40

#     Buttons - Active (1 Background, 2 TextColor, 3 Border) - Hover (4 Background, 5 Text color, 6 Border) - Pressed (7 Background, 8 Text color, 9 Border) - Variants (1 Primary, 2 Danger)
03_01_01=gray05
03_01_02=sky50
03_01_03=sky50, cyan50, 45
03_01_04=sky50, cyan50, 45
03_01_05=gray05
03_01_06=sky50, cyan50, 45
03_01_07=sky60, cyan60, 45
03_01_08=gray05
03_01_09=sky60, cyan60, 45

03_02_01=gray05
03_02_02=rose50
03_02_03=rose50, pink50, 45
03_02_04=rose50, pink50, 45
03_02_05=gray05
03_02_06=rose50, pink50, 45
03_02_07=rose60, pink60, 45
03_02_08=gray05
03_02_09=rose60, pink60, 45

#     Search Assistent (1 Background, 2 Top-Left-Border, 3 Bottom-Right-Border)
04_01_01=#f0f0f0
04_01_02=#ffffff
04_01_03=#a0a0a0

#     Search Assistent Buttons - Active (1 Background, 2 TextColor, 3 Border) - Hover (4 Background, 5 Text color, 6 Border) - Variants (1 Scope, 2 Error, 3 Warning, 4 Information, 5 Logical Operator, 6 Metadata Alias, 7 Modifier, 8 Search Operator, 9 Search Text, 10 Quick Action)
05_01_02=gray90
05_01_05=gray70

05_02_01=pink10
05_02_02=pink95
05_02_03=pink50
05_02_04=pink50
05_02_05=pink05
05_02_06=pink50

05_03_01=amber10
05_03_02=amber95
05_03_03=amber50
05_03_04=amber50
05_03_05=amber05
05_03_06=amber50

05_04_01=green10
05_04_02=green95
05_04_03=green50
05_04_04=green60
05_04_05=green05
05_04_06=green60

05_05_01=amber10
05_05_02=amber95
05_05_03=amber50
05_05_04=amber50
05_05_05=amber05
05_05_06=amber50

05_06_01=purple10
05_06_02=purple95
05_06_03=purple50
05_06_04=purple50
05_06_05=purple05
05_06_06=purple50

05_07_01=pink10
05_07_02=pink95
05_07_03=pink50
05_07_04=pink50
05_07_05=pink05
05_07_06=pink50

05_08_01=sky10
05_08_02=sky95
05_08_03=sky50
05_08_04=sky50
05_08_05=sky05
05_08_06=sky50

05_09_01=sky10
05_09_02=sky95
05_09_03=sky50
05_09_04=sky50
05_09_05=sky05
05_09_06=sky50

05_10_01=slate10
05_10_02=slate95
05_10_03=slate50
05_10_04=slate50
05_10_05=slate05
05_10_06=slate50

" }, {
                Theme.Dark, @"
#     Window (1.1 Accent color) - Header (2.1 Text color, 2.2 Bottom line)
01_01_01=blue30
01_02_01=blue30
01_02_02=rose50, fuchsia50, 0

#     Banner (1 Background, 2 TextColor, 3 Border) - Variants (1 Info - 2 Warning - 3 Error)
02_01_01=blue90
02_01_02=blue05
02_01_03=blue60

02_02_01=amber80
02_02_02=amber05
02_02_03=amber60

02_03_01=rose90
02_03_02=rose05
02_03_03=rose60

#     Buttons - Active (1 Background, 2 TextColor, 3 Border) - Hover (4 Background, 5 Text color, 6 Border) - Pressed (7 Background, 8 Text color, 9 Border) - Variants (1 Primary, 2 Danger)
03_01_01=gray95
03_01_02=sky50
03_01_03=sky50, cyan50, 45
03_01_04=sky50, cyan50, 45
03_01_05=gray95
03_01_06=sky50, cyan50, 45
03_01_07=sky40, cyan40, 45
03_01_08=gray95
03_01_09=sky40, cyan40, 45

03_02_01=gray95
03_02_02=rose50
03_02_03=rose50, pink50, 45
03_02_04=rose50, pink50, 45
03_02_05=gray95
03_02_06=rose50, pink50, 45
03_02_07=rose40, pink40, 45
03_02_08=gray95
03_02_09=rose40, pink40, 45

#     Search Assistent (1 Background, 2 Top-Left-Border, 3 Bottom-Right-Border)
04_01_01=#202020
04_01_02=#383838
04_01_03=#121212

#     Search Assistent Buttons - Active (1 Background, 2 TextColor, 3 Border) - Hover (4 Background, 5 Text color, 6 Border) - Variants (1 Scope, 2 Error, 3 Warning, 4 Information, 5 Logical Operator, 6 Metadata Alias, 7 Modifier, 8 Search Operator, 9 Search Text, 10 Quick Action)
05_01_02=gray10
05_01_05=gray30

05_02_01=pink90
05_02_02=pink05
05_02_03=pink50
05_02_04=pink50
05_02_05=pink95
05_02_06=pink50

05_03_01=amber90
05_03_02=amber05
05_03_03=amber50
05_03_04=amber50
05_03_05=amber95
05_03_06=amber50

05_04_01=green90
05_04_02=green05
05_04_03=green50
05_04_04=green50
05_04_05=green95
05_04_06=green50

05_05_01=amber90
05_05_02=amber05
05_05_03=amber50
05_05_04=amber50
05_05_05=amber95
05_05_06=amber50

05_06_01=purple90
05_06_02=purple05
05_06_03=purple50
05_06_04=purple50
05_06_05=purple95
05_06_06=purple50

05_07_01=pink90
05_07_02=pink05
05_07_03=pink50
05_07_04=pink50
05_07_05=pink95
05_07_06=pink50

05_08_01=sky90
05_08_02=sky05
05_08_03=sky50
05_08_04=sky50
05_08_05=sky95
05_08_06=sky50

05_09_01=sky90
05_09_02=sky05
05_09_03=sky50
05_09_04=sky50
05_09_05=sky95
05_09_06=sky50

05_10_01=slate90
05_10_02=slate05
05_10_03=slate50
05_10_04=slate50
05_10_05=slate95
05_10_06=slate50

" }
        };
        public bool showExpertSettings { get; set; } = false;
        #endregion
        #region GUI Core Options
        /// <summary>Show or hide the secondary helper GUI panel during search.</summary>
        public bool showGui { get; set; } = true;

        /// <summary>Keep helper panel visible even after closing the TC search window.</summary>
        public bool pinGui { get; set; } = false;

        /// <summary>Alignment target for anchoring the overlay panel (Screen, TC Main, or TC Search).</summary>
        public GuiDockingTarget guiDocking { get; set; } = GuiDockingTarget.TotalCommanderSearchWindow;

        /// <summary>Specific corner of the alignment target to anchor the window.</summary>
        public GuiDockingCorner guiDockingCorner { get; set; } = GuiDockingCorner.TopRight;

        /// <summary>Horizontal window offset in pixels from the docking origin.</summary>
        public int guiOffsetX { get; set; } = 0;

        /// <summary>Vertical window offset in pixels from the docking origin.</summary>
        public int guiOffsetY { get; set; } = 0;

        /// <summary>Minimum allowed width for the search assistant UI.</summary>
        public const int guiSizeMinX = 36;

        /// <summary>Minimum allowed height for the search assistant UI.</summary>
        public const int guiSizeMinY = 36;

        /// <summary>Absolute width of the GUI panel in pixels.</summary>
        public int guiSizeX { get; set; } = 500;

        /// <summary>Absolute height of the GUI panel in pixels.</summary>
        public int guiSizeY { get; set; } = 36;
        #endregion
        #region GUI Content Visibilities
        /// <summary> Configured section display states and display order saved to settings. </summary>
        public List<GuiSection> guiSections { get; set; } = new List<GuiSection>(); // Defaults are set in "OnPostLoad".

        /// <summary>Use full search structure (also shows default values for search mode and metadata shortcuts)</summary>
        public bool guiUseFullSearchQueryPlan { get; set; } = true;
        #endregion
        #region Control Characters (Nullable to allow disabling features via GUI)
        public char? orChar { get; set; } = '|';
        public char? andChar { get; set; } = ' ';
        public char? notChar { get; set; } = '!';
        public char? invertCaseChar { get; set; } = '~';
        public char? startAnchorChar { get; set; } = '^';
        public char? endAnchorChar { get; set; } = '$';
        public char? localOrChar { get; set; } = '/';
        public char? metadataChar { get; set; } = '@';
        public char? escapeChar { get; set; } = '\\';
        public char? quoteChar { get; set; } = '"';

        /// <summary>Decimal separator for floating point range matching (e.g. @size =3.5m). Auto-loaded from OS.</summary>
        public char? decimalSeparatorChar { get; set; } = Plugin.decimalSeparatorDefaultChar;
        #endregion
        #region Search Mode Activation Characters
        public char? standardModeChar { get; set; } = '=';
        public char? regexModeChar { get; set; } = '?';
        public char? patternModeChar { get; set; } = '%';
        public char? fuzzyModeChar { get; set; } = '<';
        public char? sequenceModeChar { get; set; } = '*';
        #endregion
        #region Engine Properties
        /// <summary>Default search engine strategy when no prefix modifier is used.</summary>
        public SearchMode defaultSearchMode { get; set; } = SearchMode.Standard;
        [XmlIgnore] public SearchMode defaultSearchModeDynamic { get; set; } = SearchMode.Standard;

        /// <summary>Enforce strict case-sensitivity during matching checks.</summary>
        public bool caseSensitive { get; set; } = false;
        [XmlIgnore] public bool caseSensitiveDynamic { get; set; } = false;

        public bool enableWordBoundariesForAnchors { get; set; } = false;
        public string wordBoundaryDelimiters { get; set; } = @".,;!_-=+& (){}[]\/";
        [XmlIgnore] public string wordBoundaryUniversalStartAnchor { get; set; } = null;
        [XmlIgnore] public string wordBoundaryUniversalEndAnchor { get; set; } = null;
        [XmlIgnore] public HashSet<char> wordBoundarySet = new HashSet<char>();

        public bool matchFirstTermAsStartAnchor { get; set; } = false;
        [XmlIgnore] public bool matchFirstTermAsStartAnchorDynamic { get; set; } = false;

        /// <summary>Ignore diacritics and accents (like á, ü, ñ) during matching checks.</summary>
        public bool ignoreAccents { get; set; } = false;

        /// <summary>Min character length to allow 1 typo in fuzzy mode.</summary>
        public int levenshteinThreshold1 { get; set; } = 3;

        /// <summary>Min character length to allow 2 typos in fuzzy mode.</summary>
        public int levenshteinThreshold2 { get; set; } = 10;

        /// <summary>Min character length to allow 3 typos in fuzzy mode.</summary>
        public int levenshteinThreshold3 { get; set; } = 20;

        public AbortKeyStrategy abortKeyStrategy { get; set; } = AbortKeyStrategy.Both;

        /// <summary>Forces TC to continue sending the filter string even if intermediate states (like an incomplete Regex clause) return 0 matches. Requires Total Commander restart.</summary>
        public bool allowEmptyResult { get; set; } = true; // Used for MatchGetSetOptions
        #endregion
        #region Metadata aliases (Internal & WDX)
        // Maps aliases to metadata (e.g., "größe" instead of "size" for MetadataConstants.Size)
        /// <summary> Custom metadata alias to show the gui. Set to empty string "" to disable. </summary>
        public string metadataAliasGui { get; set; } = "gui";
        /// <summary> Custom metadata alias for file names. Set to empty string "" to disable. </summary>
        public string metadataAliasName { get; set; } = "name";

        /// <summary> Custom metadata alias for file extensions. Set to empty string "" to disable. </summary>
        public string metadataAliasExt { get; set; } = "ext";

        /// <summary> Custom metadata alias for parent folder names. Set to empty string "" to disable. </summary>
        public string metadataAliasFolder { get; set; } = "folder";

        /// <summary> Custom metadata alias for absolute file paths. Set to empty string "" to disable. </summary>
        public string metadataAliasPath { get; set; } = "path";

        /// <summary> Custom metadata alias for search within attributes. Set to empty string "" to disable. </summary>
        public string metadataAliasAttr { get; set; } = "attr";
        /// <summary> Custom metadata alias for search within descriptions. Set to empty string "" to disable. </summary>
        public string metadataAliasDesc { get; set; } = "desc";

        /// <summary> Custom metadata alias for inner file content search. Set to empty string "" to disable. </summary>
        public string metadataAliasContent { get; set; } = "content";

        /// <summary> Custom metadata alias for file age limits. Set to empty string "" to disable. </summary>
        public string metadataAliasAge { get; set; } = "age";

        /// <summary> Custom metadata alias for file size constraints. Set to empty string "" to disable. </summary>
        public string metadataAliasSize { get; set; } = "size";

        /// <summary> Fast internal lookup linking user-defined aliases to a list of system metadata constants. </summary>
        [XmlIgnore] public Dictionary<string, List<int>> _metadataAliasMappings { get; } = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        /// <summary> Description of user-defined aliases </summary>
        [XmlIgnore] public Dictionary<string, string> _metadataAliasTooltip { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary> WDX definitions </summary>
        public List<WdxDefinition> wdxDefinitions { get; set; } = new List<WdxDefinition>();

        /// <summary> Fast lookup linking a dynamic metadata ID (>= 100) to its active WDX definition. </summary>
        [XmlIgnore] public Dictionary<int, WdxDefinition> _wdxIdToDefinitionMappings { get; } = new Dictionary<int, WdxDefinition>();
        #endregion
        #region Default metadata aliases per source view
        /// <summary> Default search alias for FileList view (View 1). Default is "name". </summary>
        public string defaultMetadataAliasFileList { get; set; } = "name";

        /// <summary> Default search alias for SearchResults view (View 2). Default is "name". </summary>
        public string defaultMetadataAliasSearchResults { get; set; } = "name";

        /// <summary> Default search alias for Synchronize view (View 3). Default is "name". </summary>
        public string defaultMetadataAliasSynchronize { get; set; } = "name";

        /// <summary> Default search alias for History view (View 4). Default is "path". </summary>
        public string defaultMetadataAliasHistory { get; set; } = "path";

        /// <summary> Default search alias for TabTitles view (View 5). Default is "name". </summary>
        public string defaultMetadataAliasTabTitles { get; set; } = "name";

        /// <summary> Default search alias for TabPaths view (View 6). Default is "path". </summary>
        public string defaultMetadataAliasTabPaths { get; set; } = "path";
        #endregion
        #region Replacements
        public bool usePinYinMatching { get; set; } = Plugin.usePinYinMatchingDefault;
        public bool useKoreanMatching { get; set; } = Plugin.useKoreanMatchingDefault;
        /// <summary> List of character transliterations and shorthand replacements saved to settings. </summary>
        public List<ReplaceRule> stringReplacementsGui { get; set; } = new List<ReplaceRule>();


        // Compiled lookups for Scopes 1, 2 and 3
        [XmlIgnore] public Dictionary<string, string> _stringReplacementsFilter { get; set; } = new Dictionary<string, string>();
        [XmlIgnore] public Dictionary<string, string> _stringReplacementsElementName { get; set; } = new Dictionary<string, string>();

        // Sorted lengths for the Longest-Match-First algorithm
        [XmlIgnore] public List<int> _sortedNeedleLengthsFilter { get; set; } = new List<int>();
        [XmlIgnore] public List<int> _sortedNeedleLengthsElementName { get; set; } = new List<int>();

        // Prepared list for UI dynamic hints (Scope: Filter)
        [XmlIgnore] public List<KeyValuePair<string, string>> _stringReplacementsFilterAssistentWindowHints { get; set; } = new List<KeyValuePair<string, string>>();
        #endregion
        #region Performance & Caching Properties
        /// <summary> Verbosity level of internal logging framework. Warning: High verbosity slows down searches heavily. </summary>
        public LoggingLevel loggingLevel { get; set; } = LoggingLevel.Level_2_Startup;

        /// <summary> In-memory cache strategy applied to parsed file metadata fields. </summary>
        public CacheStrategy metadataCacheStrategy { get; set; } = CacheStrategy.LastDirectoryOnly;

        /// <summary> Max number of filesystem objects kept in cache. Used if strategy is SpecificCount. </summary>
        public int maxMetadataObjectCacheCount { get; set; } = 5000;

        /// <summary> Max file buffer size in MB to read from storage during @content metadata searches. </summary>
        public int maxMbToReadForContent { get; set; } = 1;

        /// <summary> Max allocation size in MB for file content memory caching. Content fields are pruned when exceeded. </summary>
        public int maxContentCacheMb { get; set; } = 50;
        #endregion
        #region Temporary fields used in the GUI
        [XmlIgnore] public bool usedMetadataGui { get; set; } = false;

        // Control Characters
        [XmlIgnore] public bool viewOrCharUsed { get; set; }
        [XmlIgnore] public string viewOrChar { get; set; }
        [XmlIgnore] public bool viewAndCharUsed { get; set; }
        [XmlIgnore] public string viewAndChar { get; set; }
        [XmlIgnore] public bool viewNotCharUsed { get; set; }
        [XmlIgnore] public string viewNotChar { get; set; }
        [XmlIgnore] public bool viewInvertCaseCharUsed { get; set; }
        [XmlIgnore] public string viewInvertCaseChar { get; set; }
        [XmlIgnore] public bool viewStartAnchorCharUsed { get; set; }
        [XmlIgnore] public string viewStartAnchorChar { get; set; }
        [XmlIgnore] public bool viewEndAnchorCharUsed { get; set; }
        [XmlIgnore] public string viewEndAnchorChar { get; set; }
        [XmlIgnore] public bool viewLocalOrCharUsed { get; set; }
        [XmlIgnore] public string viewLocalOrChar { get; set; }
        [XmlIgnore] public bool viewMetadataCharUsed { get; set; }
        [XmlIgnore] public string viewMetadataChar { get; set; }
        [XmlIgnore] public bool viewEscapeCharUsed { get; set; }
        [XmlIgnore] public string viewEscapeChar { get; set; }
        [XmlIgnore] public bool viewQuoteCharUsed { get; set; }
        [XmlIgnore] public string viewQuoteChar { get; set; }
        [XmlIgnore] public bool viewDecimalSeparatorCharUsed { get; set; }
        [XmlIgnore] public string viewDecimalSeparatorChar { get; set; }

        // Search Mode Activation Characters
        [XmlIgnore] public bool viewStandardModeCharUsed { get; set; }
        [XmlIgnore] public string viewStandardModeChar { get; set; }
        [XmlIgnore] public bool viewRegexModeCharUsed { get; set; }
        [XmlIgnore] public string viewRegexModeChar { get; set; }
        [XmlIgnore] public bool viewPatternModeCharUsed { get; set; }
        [XmlIgnore] public string viewPatternModeChar { get; set; }
        [XmlIgnore] public bool viewFuzzyModeCharUsed { get; set; }
        [XmlIgnore] public string viewFuzzyModeChar { get; set; }
        [XmlIgnore] public bool viewSequenceModeCharUsed { get; set; }
        [XmlIgnore] public string viewSequenceModeChar { get; set; }

        // Error Banner
        [XmlIgnore] public string viewErrorBannerText { get; set; }
        [XmlIgnore] public string viewErrorBannerText_Gui { get; set; }
        [XmlIgnore] public string viewErrorBannerText_Other { get; set; }
        [XmlIgnore] public System.Windows.Visibility viewErrorBannerVisibility { get; set; }
        [XmlIgnore] public string viewWarningBannerText { get; set; }
        [XmlIgnore] public string viewWarningBannerText_Gui { get; set; }
        [XmlIgnore] public string viewWarningBannerText_Other { get; set; }
        [XmlIgnore] public System.Windows.Visibility viewWarningBannerVisibility { get; set; }
        [XmlIgnore] public bool isSaveEnabled { get; set; }

        // Documentation
        [XmlIgnore] public string viewDocumentationCurrentLanguage { get; set; }
        [XmlIgnore] public string viewDocumentationToggleLanguage_NewLanguage { get; set; }
        [XmlIgnore] public System.Windows.Visibility viewDocumentationToggleLanguage_Visibility { get; set; }

        // Log file
        [XmlIgnore] public string viewLogFileStatusText { get; set; }
        [XmlIgnore] public string viewLogFileContent { get; set; }
        [XmlIgnore] public System.Windows.Visibility viewLogFileDelete_Visibility { get; set; }

        // Personal note
        [XmlIgnore] public string viewPersonalCurrentLanguage { get; set; }
        [XmlIgnore] public string viewPersonalToggleLanguage_NewLanguage { get; set; }
        [XmlIgnore] public System.Windows.Visibility viewPersonalToggleLanguage_Visibility { get; set; }

        // Notify GUI about changes for "viewErrorBanner..."
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        #region public void OnPostLoad()
        /// <summary>
        /// Called directly after loading configuration from XML or initialization of empty configuration.
        /// </summary>
        public void OnPostLoad()
        {
            #region Ensure all available GUI sections are present in settings
            List<GuiSection> defaultGuiSections = new List<GuiSection>
            {
                new GuiSection(GuiSectionType.Errors, GuiSectionDisplayState.Expanded),
                new GuiSection(GuiSectionType.Warnings, GuiSectionDisplayState.Expanded),
                new GuiSection(GuiSectionType.SearchQueryPlan, GuiSectionDisplayState.Expanded),
                new GuiSection(GuiSectionType.Informations, GuiSectionDisplayState.Collapsed),
                new GuiSection(GuiSectionType.UsedModifiers, GuiSectionDisplayState.Collapsed),
                new GuiSection(GuiSectionType.DynamicHints, GuiSectionDisplayState.Collapsed),
                new GuiSection(GuiSectionType.AvailableCharsList, GuiSectionDisplayState.Collapsed),
                new GuiSection(GuiSectionType.TextReplacements, GuiSectionDisplayState.Collapsed),
                new GuiSection(GuiSectionType.QuickAccess, GuiSectionDisplayState.Collapsed)
            };

            if(guiSections == null) guiSections = new List<GuiSection>();
            if(guiSections.Count == 0) guiSections = defaultGuiSections;
            else {
                foreach(GuiSection defaultItem in defaultGuiSections) {
                    bool exists = false;
                    foreach(GuiSection guiSection in guiSections) {
                        if(guiSection.guiSectionType == defaultItem.guiSectionType) {
                            exists = true;
                            break;
                        }
                    }
                    if(!exists) guiSections.Add(defaultItem);
                }
            }
            #endregion
        }
        #endregion
        #region public void PopulateViewFields_ControlCharacters()
        /// <summary>
        /// Maps the engine's nullable char properties into flat string/bool fields for GUI binding.
        /// Call this right after loading the configuration file.
        /// </summary>
        public void PopulateViewFields_ControlCharacters()
        {
            // Control Characters
            viewOrCharUsed = orChar.HasValue;
            viewOrChar = orChar.HasValue ? orChar.Value.ToString() : "|";
            viewAndCharUsed = andChar.HasValue;
            viewAndChar = andChar.HasValue ? andChar.Value.ToString() : " ";
            viewNotCharUsed = notChar.HasValue;
            viewNotChar = notChar.HasValue ? notChar.Value.ToString() : "!";
            viewInvertCaseCharUsed = invertCaseChar.HasValue;
            viewInvertCaseChar = invertCaseChar.HasValue ? invertCaseChar.Value.ToString() : "~";
            viewStartAnchorCharUsed = startAnchorChar.HasValue;
            viewStartAnchorChar = startAnchorChar.HasValue ? startAnchorChar.Value.ToString() : "^";
            viewEndAnchorCharUsed = endAnchorChar.HasValue;
            viewEndAnchorChar = endAnchorChar.HasValue ? endAnchorChar.Value.ToString() : "$";
            viewLocalOrCharUsed = localOrChar.HasValue;
            viewLocalOrChar = localOrChar.HasValue ? localOrChar.Value.ToString() : "/";
            viewMetadataCharUsed = metadataChar.HasValue;
            viewMetadataChar = metadataChar.HasValue ? metadataChar.Value.ToString() : "@";
            viewEscapeCharUsed = escapeChar.HasValue;
            viewEscapeChar = escapeChar.HasValue ? escapeChar.Value.ToString() : "\\";
            viewQuoteCharUsed = quoteChar.HasValue;
            viewQuoteChar = quoteChar.HasValue ? quoteChar.Value.ToString() : "\"";
            viewDecimalSeparatorCharUsed = decimalSeparatorChar.HasValue;
            viewDecimalSeparatorChar = decimalSeparatorChar.HasValue ? decimalSeparatorChar.Value.ToString() : Plugin.decimalSeparatorDefaultChar.ToString();

            // Search Mode Activation Characters
            viewStandardModeCharUsed = standardModeChar.HasValue;
            viewStandardModeChar = standardModeChar.HasValue ? standardModeChar.Value.ToString() : "=";
            viewRegexModeCharUsed = regexModeChar.HasValue;
            viewRegexModeChar = regexModeChar.HasValue ? regexModeChar.Value.ToString() : "?";
            viewPatternModeCharUsed = patternModeChar.HasValue;
            viewPatternModeChar = patternModeChar.HasValue ? patternModeChar.Value.ToString() : "%";
            viewFuzzyModeCharUsed = fuzzyModeChar.HasValue;
            viewFuzzyModeChar = fuzzyModeChar.HasValue ? fuzzyModeChar.Value.ToString() : "<";
            viewSequenceModeCharUsed = sequenceModeChar.HasValue;
            viewSequenceModeChar = sequenceModeChar.HasValue ? sequenceModeChar.Value.ToString() : "*";

            foreach(WdxDefinition wdxDefinition in wdxDefinitions) {
                wdxDefinition.oldFieldAndUnit = wdxDefinition.fieldAndUnit;
            }
        }
        #endregion
        #region public void PersistViewFields_ControlCharacters()
        /// <summary>
        /// Extracts the first character from GUI textboxes and saves them back to engine parameters.
        /// </summary>
        public void PersistViewFields_ControlCharacters()
        {
            orChar = (viewOrCharUsed && !string.IsNullOrEmpty(viewOrChar)) ? viewOrChar[0] : (char?)null;
            andChar = (viewAndCharUsed && !string.IsNullOrEmpty(viewAndChar)) ? viewAndChar[0] : (char?)null;
            notChar = (viewNotCharUsed && !string.IsNullOrEmpty(viewNotChar)) ? viewNotChar[0] : (char?)null;
            invertCaseChar = (viewInvertCaseCharUsed && !string.IsNullOrEmpty(viewInvertCaseChar)) ? viewInvertCaseChar[0] : (char?)null;
            startAnchorChar = (viewStartAnchorCharUsed && !string.IsNullOrEmpty(viewStartAnchorChar)) ? viewStartAnchorChar[0] : (char?)null;
            endAnchorChar = (viewEndAnchorCharUsed && !string.IsNullOrEmpty(viewEndAnchorChar)) ? viewEndAnchorChar[0] : (char?)null;
            localOrChar = (viewLocalOrCharUsed && !string.IsNullOrEmpty(viewLocalOrChar)) ? viewLocalOrChar[0] : (char?)null;
            metadataChar = (viewMetadataCharUsed && !string.IsNullOrEmpty(viewMetadataChar)) ? viewMetadataChar[0] : (char?)null;
            escapeChar = (viewEscapeCharUsed && !string.IsNullOrEmpty(viewEscapeChar)) ? viewEscapeChar[0] : (char?)null;
            quoteChar = (viewQuoteCharUsed && !string.IsNullOrEmpty(viewQuoteChar)) ? viewQuoteChar[0] : (char?)null;
            decimalSeparatorChar = (viewDecimalSeparatorCharUsed && !string.IsNullOrEmpty(viewDecimalSeparatorChar)) ? viewDecimalSeparatorChar[0] : (char?)null;

            standardModeChar = (viewStandardModeCharUsed && !string.IsNullOrEmpty(viewStandardModeChar)) ? viewStandardModeChar[0] : (char?)null;
            regexModeChar = (viewRegexModeCharUsed && !string.IsNullOrEmpty(viewRegexModeChar)) ? viewRegexModeChar[0] : (char?)null;
            patternModeChar = (viewPatternModeCharUsed && !string.IsNullOrEmpty(viewPatternModeChar)) ? viewPatternModeChar[0] : (char?)null;
            fuzzyModeChar = (viewFuzzyModeCharUsed && !string.IsNullOrEmpty(viewFuzzyModeChar)) ? viewFuzzyModeChar[0] : (char?)null;
            sequenceModeChar = (viewSequenceModeCharUsed && !string.IsNullOrEmpty(viewSequenceModeChar)) ? viewSequenceModeChar[0] : (char?)null;
        }
        #endregion
        #region public void PopulateViewFields_Language()
        /// <summary>
        /// Populates the translation dictionary based on the selected language and custom overrides.
        /// </summary>
        public void PopulateViewFields_Language()
        {
            var tempDict = new Dictionary<int, string>();

            // 1. Always load English first as the base fallback
            ParseRawStringToDictionary(Plugin.languageManager.availableLanguages[Language.English].rawData, tempDict);

            // 2. Override with other languages if selected
            if(language != Language.English) ParseRawStringToDictionary(Plugin.languageManager.availableLanguages[language].rawData, tempDict);

            // 3. Overlay the custom override text from config (enables live preview for translators)
            if(!string.IsNullOrWhiteSpace(customTranslationOverride)) ParseRawStringToDictionary(customTranslationOverride, tempDict);

            // 4. Add combined strings
            tempDict[001_001_001] = "QuickSearch eXtended 2 - " + tempDict[001_001_001] + " - " + Plugin.version;
            tempDict[002_009_004] = string.Format(tempDict[002_009_004] /* ℹ️ Advanced mass replacement rules can be defined via the "{0}" file. See help for details. */, Plugin.replaceFilePath);

            // 5. Update language provider and notify GUI for real-time binding refresh
            L.SetData(tempDict);
            OnPropertyChanged(nameof(L));
        }
        #endregion
        #region public void PopulateViewFields_Theme()
        /// <summary>
        /// Populates the color dictionary based on the selected theme and custom overrides.
        /// </summary>
        public void PopulateViewFields_Theme()
        {
            var definitions = new Dictionary<int, string>();
            var brushesAvailable = new Dictionary<string, bool>();
            var brushes = new Dictionary<string, object>();

            // 1. Always load Theme colors first as the base fallback
            ParseRawStringToDictionary(defaultThemes[theme], definitions);

            // 2. Overlay the custom override colors from config (enables live preview for designers)
            if(!string.IsNullOrWhiteSpace(customColorOverride)) ParseRawStringToDictionary(customColorOverride, definitions);

            foreach(var definition in definitions) {
                object value = ParseBrush(definition.Value);
                string stringKey = FormatIntToBrushKey(definition.Key);

                brushes[stringKey] = value;
                brushesAvailable[stringKey] = value != DependencyProperty.UnsetValue;
            }

            // 3. Update the property and notify the GUI for real-time binding refresh
            b = brushesAvailable;
            B = brushes;
            OnPropertyChanged(nameof(b));
            OnPropertyChanged(nameof(B));
        }

        #region private string FormatIntToBrushKey(int intKey)
        private string FormatIntToBrushKey(int intKey)
        {
            int p1 = intKey / 10000;
            int p2 = (intKey / 100) % 100;
            int p3 = intKey % 100;
            return $"{p1:D2}_{p2:D2}_{p3:D2}";
        }
        #endregion
        #endregion
        #region public void PopulateViewFields_CurrentWindow_Theme(FrameworkElement window)
        public void PopulateViewFields_CurrentWindow_Theme(FrameworkElement searchAssistantWindow, FrameworkElement configWindow)
        {
            if(B.TryGetValue("01_01_01", out object entry) && entry is SolidColorBrush brush) {
                ApplicationThemeManager.Apply(theme == Theme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: false);
                ApplicationAccentColorManager.Apply(brush.Color);
            } else {
                ApplicationThemeManager.Apply(theme == Theme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true);
            }

            if(searchAssistantWindow != null) {
                ApplicationThemeManager.Apply(searchAssistantWindow);
            }
            if(configWindow != null) {
                ApplicationThemeManager.Apply(configWindow);
                configWindow.ClearValue(Window.BackgroundProperty);
            }
        }
        #endregion
        #region public static Color ParseColor(string colortext)
        // https://tailwindcss.com/docs/colors
        private static readonly Dictionary<string, string> tailwindColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "red05",     "#fef2f2" }, { "red10",     "#fee2e2" }, { "red20",     "#fecaca" }, { "red30",     "#fca5a5" }, { "red40",     "#f87171" }, { "red50",     "#ef4444" }, { "red60",     "#dc2626" }, { "red70",     "#b91c1c" }, { "red80",     "#991b1b" }, { "red90",     "#7f1d1d" }, { "red95",     "#450a0a" },
            { "orange05",  "#fff7ed" }, { "orange10",  "#ffedd5" }, { "orange20",  "#fed7aa" }, { "orange30",  "#fdba74" }, { "orange40",  "#fb923c" }, { "orange50",  "#f97316" }, { "orange60",  "#ea580c" }, { "orange70",  "#c2410c" }, { "orange80",  "#9a3412" }, { "orange90",  "#7c2d12" }, { "orange95",  "#431407" },
            { "amber05",   "#fffbeb" }, { "amber10",   "#fef3c7" }, { "amber20",   "#fde68a" }, { "amber30",   "#fcd34d" }, { "amber40",   "#fbbf24" }, { "amber50",   "#f59e0b" }, { "amber60",   "#d97706" }, { "amber70",   "#b45309" }, { "amber80",   "#92400e" }, { "amber90",   "#78350f" }, { "amber95",   "#451a03" },
            { "yellow05",  "#fefce8" }, { "yellow10",  "#fef9c3" }, { "yellow20",  "#fef08a" }, { "yellow30",  "#fde047" }, { "yellow40",  "#facc15" }, { "yellow50",  "#eab308" }, { "yellow60",  "#ca8a04" }, { "yellow70",  "#a16207" }, { "yellow80",  "#854d0e" }, { "yellow90",  "#713f12" }, { "yellow95",  "#422006" },
            { "lime05",    "#f7fee7" }, { "lime10",    "#ecfccb" }, { "lime20",    "#d9f99d" }, { "lime30",    "#bef264" }, { "lime40",    "#a3e635" }, { "lime50",    "#84cc16" }, { "lime60",    "#65a30d" }, { "lime70",    "#4d7c0f" }, { "lime80",    "#3f6212" }, { "lime90",    "#365314" }, { "lime95",    "#1a2e05" },
            { "green05",   "#f0fdf4" }, { "green10",   "#dcfce7" }, { "green20",   "#bbf7d0" }, { "green30",   "#86efac" }, { "green40",   "#4ade80" }, { "green50",   "#22c55e" }, { "green60",   "#16a34a" }, { "green70",   "#15803d" }, { "green80",   "#166534" }, { "green90",   "#14532d" }, { "green95",   "#052e16" },
            { "emerald05", "#ecfdf5" }, { "emerald10", "#d1fae5" }, { "emerald20", "#a7f3d0" }, { "emerald30", "#6ee7b7" }, { "emerald40", "#34d399" }, { "emerald50", "#10b981" }, { "emerald60", "#059669" }, { "emerald70", "#047857" }, { "emerald80", "#065f46" }, { "emerald90", "#064e3b" }, { "emerald95", "#022c22" },
            { "teal05",    "#f0fdfa" }, { "teal10",    "#ccfbf1" }, { "teal20",    "#99f6e4" }, { "teal30",    "#5eead4" }, { "teal40",    "#2dd4bf" }, { "teal50",    "#14b8a6" }, { "teal60",    "#0d9488" }, { "teal70",    "#0f766e" }, { "teal80",    "#115e59" }, { "teal90",    "#134e4a" }, { "teal95",    "#042f2e" },
            { "cyan05",    "#ecfeff" }, { "cyan10",    "#cffafe" }, { "cyan20",    "#a5f3fc" }, { "cyan30",    "#67e8f9" }, { "cyan40",    "#22d3ee" }, { "cyan50",    "#06b6d4" }, { "cyan60",    "#0891b2" }, { "cyan70",    "#0e7490" }, { "cyan80",    "#155e75" }, { "cyan90",    "#164e63" }, { "cyan95",    "#083344" },
            { "sky05",     "#f0f9ff" }, { "sky10",     "#e0f2fe" }, { "sky20",     "#bae6fd" }, { "sky30",     "#7dd3fc" }, { "sky40",     "#38bdf8" }, { "sky50",     "#0ea5e9" }, { "sky60",     "#0284c7" }, { "sky70",     "#0369a1" }, { "sky80",     "#075985" }, { "sky90",     "#0c4a6e" }, { "sky95",     "#082f49" },
            { "blue05",    "#eff6ff" }, { "blue10",    "#dbeafe" }, { "blue20",    "#bfdbfe" }, { "blue30",    "#93c5fd" }, { "blue40",    "#60a5fa" }, { "blue50",    "#3b82f6" }, { "blue60",    "#2563eb" }, { "blue70",    "#1d4ed8" }, { "blue80",    "#1e40af" }, { "blue90",    "#1e3a8a" }, { "blue95",    "#172554" },
            { "indigo05",  "#eef2ff" }, { "indigo10",  "#e0e7ff" }, { "indigo20",  "#c7d2fe" }, { "indigo30",  "#a5b4fc" }, { "indigo40",  "#818cf8" }, { "indigo50",  "#6366f1" }, { "indigo60",  "#4f46e5" }, { "indigo70",  "#4338ca" }, { "indigo80",  "#3730a3" }, { "indigo90",  "#312e81" }, { "indigo95",  "#1e1b4b" },
            { "violet05",  "#f5f3ff" }, { "violet10",  "#ede9fe" }, { "violet20",  "#ddd6fe" }, { "violet30",  "#c4b5fd" }, { "violet40",  "#a78bfa" }, { "violet50",  "#8b5cf6" }, { "violet60",  "#7c3aed" }, { "violet70",  "#6d28d9" }, { "violet80",  "#5b21b6" }, { "violet90",  "#4c1d95" }, { "violet95",  "#2e1065" },
            { "purple05",  "#faf5ff" }, { "purple10",  "#f3e8ff" }, { "purple20",  "#e9d5ff" }, { "purple30",  "#d8b4fe" }, { "purple40",  "#c084fc" }, { "purple50",  "#a855f7" }, { "purple60",  "#9333ea" }, { "purple70",  "#7e22ce" }, { "purple80",  "#6b21a8" }, { "purple90",  "#581c87" }, { "purple95",  "#3b0764" },
            { "fuchsia05", "#fdf4ff" }, { "fuchsia10", "#fae8ff" }, { "fuchsia20", "#f5d0fe" }, { "fuchsia30", "#f0abfc" }, { "fuchsia40", "#e879f9" }, { "fuchsia50", "#d946ef" }, { "fuchsia60", "#c026d3" }, { "fuchsia70", "#a21caf" }, { "fuchsia80", "#86198f" }, { "fuchsia90", "#701a75" }, { "fuchsia95", "#4a044e" },
            { "pink05",    "#fdf2f8" }, { "pink10",    "#fce7f3" }, { "pink20",    "#fbcfe8" }, { "pink30",    "#f9a8d4" }, { "pink40",    "#f472b6" }, { "pink50",    "#ec4899" }, { "pink60",    "#db2777" }, { "pink70",    "#be185d" }, { "pink80",    "#9d174d" }, { "pink90",    "#831843" }, { "pink95",    "#500724" },
            { "rose05",    "#fff1f2" }, { "rose10",    "#ffe4e6" }, { "rose20",    "#fecdd3" }, { "rose30",    "#fda4af" }, { "rose40",    "#fb7185" }, { "rose50",    "#f43f5e" }, { "rose60",    "#e11d48" }, { "rose70",    "#be123c" }, { "rose80",    "#9f1239" }, { "rose90",    "#881337" }, { "rose95",    "#4c0519" },

            { "slate05",   "#f8fafc" }, { "slate10",   "#f1f5f9" }, { "slate20",   "#e2e8f0" }, { "slate30",   "#cbd5e1" }, { "slate40",   "#94a3b8" }, { "slate50",   "#64748b" }, { "slate60",   "#475569" }, { "slate70",   "#334155" }, { "slate80",   "#1e293b" }, { "slate90",   "#0f172a" }, { "slate95",   "#020617" },
            { "gray05",    "#f9fafb" }, { "gray10",    "#f3f4f6" }, { "gray20",    "#e5e7eb" }, { "gray30",    "#d1d5db" }, { "gray40",    "#9ca3af" }, { "gray50",    "#6b7280" }, { "gray60",    "#4b5563" }, { "gray70",    "#374151" }, { "gray80",    "#1f2937" }, { "gray90",    "#111827" }, { "gray95",    "#030712" },
            { "zinc05",    "#fafafa" }, { "zinc10",    "#f4f4f5" }, { "zinc20",    "#e4e4e7" }, { "zinc30",    "#d4d4d8" }, { "zinc40",    "#a1a1aa" }, { "zinc50",    "#71717a" }, { "zinc60",    "#52525b" }, { "zinc70",    "#3f3f46" }, { "zinc80",    "#27272a" }, { "zinc90",    "#18181b" }, { "zinc95",    "#09090b" },
            { "neutral05", "#fafafa" }, { "neutral10", "#f5f5f5" }, { "neutral20", "#e5e5e5" }, { "neutral30", "#d4d4d4" }, { "neutral40", "#a3a3a3" }, { "neutral50", "#737373" }, { "neutral60", "#525252" }, { "neutral70", "#404040" }, { "neutral80", "#262626" }, { "neutral90", "#171717" }, { "neutral95", "#0a0a0a" },
            { "stone05",   "#fafaf9" }, { "stone10",   "#f5f5f4" }, { "stone20",   "#e7e5e4" }, { "stone30",   "#d6d3d1" }, { "stone40",   "#a8a29e" }, { "stone50",   "#78716c" }, { "stone60",   "#57534e" }, { "stone70",   "#44403c" }, { "stone80",   "#292524" }, { "stone90",   "#1c1917" }, { "stone95",   "#0c0a09" },
            { "taupe05",   "#fbfaf9" }, { "taupe10",   "#f3f1f1" }, { "taupe20",   "#e8e4e3" }, { "taupe30",   "#d8d2d0" }, { "taupe40",   "#aba09c" }, { "taupe50",   "#7c6d67" }, { "taupe60",   "#5b4f4b" }, { "taupe70",   "#473c39" }, { "taupe80",   "#2b2422" }, { "taupe90",   "#1d1816" }, { "taupe95",   "#0c0a09" },
            { "mauve05",   "#fafafa" }, { "mauve10",   "#f3f1f3" }, { "mauve20",   "#e7e4e7" }, { "mauve30",   "#d7d0d7" }, { "mauve40",   "#a89ea9" }, { "mauve50",   "#79697b" }, { "mauve60",   "#594c5b" }, { "mauve70",   "#463947" }, { "mauve80",   "#2a212c" }, { "mauve90",   "#1d161e" }, { "mauve95",   "#0c090c" },
            { "mist05",    "#f9fbfb" }, { "mist10",    "#f1f3f3" }, { "mist20",    "#e3e7e8" }, { "mist30",    "#d0d6d8" }, { "mist40",    "#9ca8ab" }, { "mist50",    "#67787c" }, { "mist60",    "#4b585b" }, { "mist70",    "#394447" }, { "mist80",    "#22292b" }, { "mist90",    "#161b1d" }, { "mist95",    "#090b0c" },
            { "olive05",   "#fbfbf9" }, { "olive10",   "#f4f4f0" }, { "olive20",   "#e8e8e3" }, { "olive30",   "#d8d8d0" }, { "olive40",   "#abab9c" }, { "olive50",   "#7c7c67" }, { "olive60",   "#5b5b4b" }, { "olive70",   "#474739" }, { "olive80",   "#2b2b22" }, { "olive90",   "#1d1d16" }, { "olive95",   "#0c0c09" }
        };

        public static Color ParseColor(string colortext)
        {
            // 1. Dictionary Lookup for Color-Names ("blue10" => "#dbeafe")
            if(tailwindColors.TryGetValue(colortext, out string tailwindColor)) return (Color)ColorConverter.ConvertFromString(tailwindColor);

            // 2. Fallback: Hex-Parsing ("#dbeafe" or "#ff172554")
            return (Color)ColorConverter.ConvertFromString(colortext);
        }
        #endregion
        #region private object ParseBrush(string input)
        private object ParseBrush(string input)
        {
            if(string.IsNullOrWhiteSpace(input)) return DependencyProperty.UnsetValue;

            try {
                var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length == 0) return DependencyProperty.UnsetValue;

                // 1. Solid Color
                if(parts.Length == 1) {
                    var color = (Color)ParseColor(parts[0].Trim());
                    var solidBrush = new SolidColorBrush(color);
                    solidBrush.Freeze();
                    return solidBrush;
                }

                // 2. Multi-color gradient (requires at least 2 colors + 1 angle at the end)
                if(parts.Length >= 3) {
                    int colorCount = parts.Length - 1;

                    if(int.TryParse(parts[parts.Length - 1].Trim(), out int angle)) {
                        var (startPoint, endPoint) = GetGradientPointsFromAngle(angle);

                        var gradientBrush = new LinearGradientBrush {
                            StartPoint = startPoint,
                            EndPoint = endPoint
                        };

                        // Distribute gradient stops evenly between 0.0 and 1.0
                        for(int i = 0; i < colorCount; i++) {
                            var color = (Color)ParseColor(parts[i].Trim());
                            double offset = (double)i / (colorCount - 1);
                            gradientBrush.GradientStops.Add(new GradientStop(color, offset));
                        }

                        gradientBrush.Freeze();
                        return gradientBrush;
                    }
                }
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not parse brush: {ex.Message}{Environment.NewLine}{ex}");
            }

            return DependencyProperty.UnsetValue;
        }
        #endregion
        #region private (Point Start, Point End) GetGradientPointsFromAngle(int angle)
        private (Point Start, Point End) GetGradientPointsFromAngle(int angle)
        {
            // Normalize angle to 0..359 degrees
            angle = (angle % 360 + 360) % 360;

            // Convert degrees to radians
            double radians = angle * Math.PI / 180.0;
            double dx = Math.Cos(radians);
            double dy = Math.Sin(radians);

            // Scale vector so that at least one coordinate reaches +/- 1.0 (bounding box boundary)
            double max = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if(max > 0) {
                dx /= max;
                dy /= max;
            }

            // Map direction vector from [-1, 1] to WPF relative space [0, 1]
            // EndPoint points in the direction of the angle
            Point endPoint = new Point((dx + 1.0) / 2.0, (dy + 1.0) / 2.0);

            // StartPoint points in the opposite direction
            Point startPoint = new Point((-dx + 1.0) / 2.0, (-dy + 1.0) / 2.0);

            return (startPoint, endPoint);
        }
        #endregion
        #region public bool PopulateInternalElements()
        private bool AddMapping(string metadataAlias, int metadataConstant, string metadataDisplayName, Dictionary<string, List<string>> collectDisplayNames)
        {
            if(string.IsNullOrEmpty(metadataAlias)) return false;

            // If the alias already exists, verify exclusivity constraints
            if(_metadataAliasMappings.TryGetValue(metadataAlias, out List<int> existingMetadataIDs)) {
                // If the existing constant is Gui, Age, or Size, abort assignment to prevent illegal mixing
                if(existingMetadataIDs.Contains(MetadataConstants.Gui) || existingMetadataIDs.Contains(MetadataConstants.Age) || existingMetadataIDs.Contains(MetadataConstants.Size)) return false;
            }

            if(!_metadataAliasMappings.ContainsKey(metadataAlias)) _metadataAliasMappings[metadataAlias] = new List<int>();
            _metadataAliasMappings[metadataAlias].Add(metadataConstant);

            if(!collectDisplayNames.ContainsKey(metadataAlias)) collectDisplayNames[metadataAlias] = new List<string>();
            collectDisplayNames[metadataAlias].Add(metadataDisplayName);

            return true;
        }
        /// <summary> Synchronizes serializable properties into high-performance internal dictionaries. </summary>
        public void PopulateInternalElements()
        {
            #region Populate internal alias mappings
            _metadataAliasMappings.Clear();
            _metadataAliasTooltip.Clear();

            var collectDisplayNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // 1. Insert critical exclusive elements first to reserve their keys in the dictionary
            AddMapping(metadataAliasGui, MetadataConstants.Gui, L[100_002_110] /* GUI Control (display search assistant) */, collectDisplayNames);
            AddMapping(metadataAliasAge, MetadataConstants.Age, string.Format(L[100_002_117] /* Modification age (Examples: ">14", "<2h", ">2{0}5y", "=3d") */, decimalSeparatorChar?.ToString() ?? "."), collectDisplayNames);
            AddMapping(metadataAliasSize, MetadataConstants.Size, string.Format(L[100_002_118] /* File size (Examples: ">700", "<50M", ">2{0}2G", "=7M") */, decimalSeparatorChar?.ToString() ?? "."), collectDisplayNames);

            // 2. Insert standard text elements next (automatically blocked upon collision with step 1)
            AddMapping(metadataAliasName, MetadataConstants.Name, L[100_002_111] /* Name (e.g., "Contract.docx") */, collectDisplayNames);
            AddMapping(metadataAliasExt, MetadataConstants.Ext, L[100_002_112] /* Extension (e.g., "docx") */, collectDisplayNames);
            AddMapping(metadataAliasFolder, MetadataConstants.Folder, L[100_002_113] /* Parent folder name (e.g., "Documents") */, collectDisplayNames);
            AddMapping(metadataAliasPath, MetadataConstants.Path, L[100_002_114] /* Full path (e.g., "C:\Data\Documents\Contract.docx") */, collectDisplayNames);
            AddMapping(metadataAliasAttr, MetadataConstants.Attr, L[100_002_120] /* File attributes (e.g., "r"=readonly, "h"=hidden etc.) */, collectDisplayNames);
            AddMapping(metadataAliasDesc, MetadataConstants.Desc, L[100_002_115] /* Description (file comments from "descript.ion" files) */, collectDisplayNames);
            AddMapping(metadataAliasContent, MetadataConstants.Content, L[100_002_116] /* Content (full-text search in files) */, collectDisplayNames);

            // 3. Dynamically map active WDX definitions starting at CustomWdx (100)
            int currentWdxId = MetadataConstants.CustomWdx;
            foreach(var wdxDefinition in wdxDefinitions) {
                if(!wdxDefinition.isActive || wdxDefinition.is64Bit != Environment.Is64BitProcess) continue;

                string wdxDisplayName = string.Format(L[100_002_119] /* Metadata: WDX - {0} - {1} */, wdxDefinition.pluginName, wdxDefinition.fieldAndUnit);

                // Register alias mapping for the parser
                if(AddMapping(wdxDefinition.metadataAlias, currentWdxId, wdxDisplayName, collectDisplayNames)) {

                    // Register direct ID to definition mapping for the context evaluation engine
                    _wdxIdToDefinitionMappings[currentWdxId] = wdxDefinition;
                    currentWdxId++;
                }
            }

            // 4. Build formatted tooltips
            foreach(var kvp in collectDisplayNames) {
                // Check if this alias maps to the GUI control action
                if(_metadataAliasMappings.TryGetValue(kvp.Key, out List<int> ids) && ids.Count != 0 && ids[0] == MetadataConstants.Gui) {
                    _metadataAliasTooltip[kvp.Key] = string.Format(L[100_002_103] /* The metadata alias "{0}" displays the search assistant. */, (metadataChar?.ToString() ?? "") + kvp.Key);
                } else if(kvp.Value.Count == 1) {
                    _metadataAliasTooltip[kvp.Key] = string.Format(L[100_002_102] /* Under the metadata alias "{0}", the following field is searched: {1}. */, (metadataChar?.ToString() ?? "") + kvp.Key, kvp.Value[0]);
                } else {
                    _metadataAliasTooltip[kvp.Key] = string.Format(L[100_002_100] /* Under the metadata alias "{0}", the following fields are searched simultaneously: {1}. */, (metadataChar?.ToString() ?? "") + kvp.Key, string.Join(", ", kvp.Value));
                }
            }
            #endregion
            #region Compile replacements
            _stringReplacementsFilter.Clear();
            _stringReplacementsElementName.Clear();
            FileMatcher.matchOnlyLookup.Clear();
            FileMatcher.matchOnlyLookupLower.Clear();

            // 1. Process File-bound rules first
            foreach(ReplaceRule rule in Plugin.stringReplacementsFile) {
                if(string.IsNullOrEmpty(rule.searchFor)) continue;

                string searchFor = rule.searchFor;
                string replaceWith = rule.replaceWith ?? string.Empty;

                if(rule.scope == RuleScope.Filter || rule.scope == RuleScope.Both) _stringReplacementsFilter[searchFor] = replaceWith;
                if(rule.scope == RuleScope.ElementName || rule.scope == RuleScope.Both) _stringReplacementsElementName[searchFor] = replaceWith;

                #region RuleScope.MatchOnly
                if(rule.scope == RuleScope.MatchOnly) {
                    char searchChar = searchFor[0];
                    if(!FileMatcher.matchOnlyLookup.TryGetValue(searchChar, out var targetSet)) {
                        targetSet = new HashSet<char>();
                        FileMatcher.matchOnlyLookup[searchChar] = targetSet;
                    }
                    foreach(char c in replaceWith) targetSet.Add(c);
                }
                #endregion
            }

            // 2. Process GUI-bound rules second (Overwrites Scopes 1-3, merges Scope 4 additively)
            foreach(ReplaceRule rule in stringReplacementsGui) {
                if(string.IsNullOrEmpty(rule.searchFor)) continue;

                string searchFor = rule.searchFor;
                string replaceWith = rule.replaceWith ?? string.Empty;

                if(rule.scope == RuleScope.Filter || rule.scope == RuleScope.Both) _stringReplacementsFilter[searchFor] = replaceWith;
                if(rule.scope == RuleScope.ElementName || rule.scope == RuleScope.Both) _stringReplacementsElementName[searchFor] = replaceWith;

                #region RuleScope.MatchOnly
                if(rule.scope == RuleScope.MatchOnly) {
                    char searchChar = searchFor[0];
                    if(!FileMatcher.matchOnlyLookup.TryGetValue(searchChar, out var targetSet)) {
                        targetSet = new HashSet<char>();
                        FileMatcher.matchOnlyLookup[searchChar] = targetSet;
                    }
                    // Due to HashSet, duplicates between File and GUI are automatically filtered out
                    foreach(char c in replaceWith) targetSet.Add(c);
                }
                #endregion
            }

            // 3. Post-Processing: Generate Case-Insensitive Lookup Table for Scope 4
            foreach(var entry in FileMatcher.matchOnlyLookup) {
                char searchCharLower = char.ToLowerInvariant(entry.Key);
                if(!FileMatcher.matchOnlyLookupLower.TryGetValue(searchCharLower, out var targetSetLower)) {
                    targetSetLower = new HashSet<char>();
                    FileMatcher.matchOnlyLookupLower[searchCharLower] = targetSetLower;
                }
                foreach(char replaceWith in entry.Value) targetSetLower.Add(char.ToLowerInvariant(replaceWith));
            }

            // 4. Compile sorted needle lengths for both pipelines
            var filterLengths = new HashSet<int>();
            foreach(var key in _stringReplacementsFilter.Keys) if(!string.IsNullOrEmpty(key)) filterLengths.Add(key.Length);
            _sortedNeedleLengthsFilter = filterLengths.OrderByDescending(len => len).ToList();

            var elementLengths = new HashSet<int>();
            foreach(var key in _stringReplacementsElementName.Keys) if(!string.IsNullOrEmpty(key)) elementLengths.Add(key.Length);
            _sortedNeedleLengthsElementName = elementLengths.OrderByDescending(len => len).ToList();

            // 5. Prepare top 25 longest preprocessor filter rules, sorted alphabetically for UI display (2 or more characters)
            _stringReplacementsFilterAssistentWindowHints = _stringReplacementsFilter
                .Where(kvp => kvp.Key.Length >= 2)
                .OrderByDescending(kvp => kvp.Key.Length)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FileMatcher.usePinYinMatching = usePinYinMatching;
            FileMatcher.useKoreanMatching = useKoreanMatching;
            FileMatcher.useExtendedMatching = usePinYinMatching || useKoreanMatching || FileMatcher.matchOnlyLookup.Count != 0;

            defaultSearchModeDynamic = defaultSearchMode;
            caseSensitiveDynamic = caseSensitive;
            matchFirstTermAsStartAnchorDynamic = matchFirstTermAsStartAnchor;
            #endregion
            #region Word Boundary Delimiters
            wordBoundaryUniversalStartAnchor = "^";
            wordBoundaryUniversalEndAnchor = "$";
            wordBoundarySet.Clear();

            if(enableWordBoundariesForAnchors && !string.IsNullOrEmpty(wordBoundaryDelimiters)) {
                string escapedWordBoundaryDelimiters = Regex.Escape(wordBoundaryDelimiters).Replace("-", @"\-").Replace("]", @"\]");
                wordBoundaryUniversalStartAnchor = "(?:^|(?<=[" + escapedWordBoundaryDelimiters + "]))";
                wordBoundaryUniversalEndAnchor = "(?:$|(?=[" + escapedWordBoundaryDelimiters + "]))";
                for(int i = 0; i < wordBoundaryDelimiters.Length; i++) {
                    wordBoundarySet.Add(wordBoundaryDelimiters[i]);
                }
            }
            #endregion
        }
        #endregion

        #region public void Validate()
        public void Validate()
        {
            // Collect all active errors into a list
            var activeErrors = new List<string>();
            var activeWarnings = new List<string>();

            if(!string.IsNullOrEmpty(viewErrorBannerText_Gui)) activeErrors.Add(viewErrorBannerText_Gui);
            if(!string.IsNullOrEmpty(viewErrorBannerText_Other)) activeErrors.Add(viewErrorBannerText_Other);

            if(!string.IsNullOrEmpty(viewWarningBannerText_Gui)) activeWarnings.Add(viewWarningBannerText_Gui);
            if(!string.IsNullOrEmpty(viewWarningBannerText_Other)) activeWarnings.Add(viewWarningBannerText_Other);

            // If there are errors, join them with newlines and make the banner visible
            if(activeErrors.Count > 0) {
                viewErrorBannerText = string.Join("\n", activeErrors);
                viewErrorBannerVisibility = System.Windows.Visibility.Visible;
                isSaveEnabled = false;
            } else {
                viewErrorBannerText = string.Empty;
                viewErrorBannerVisibility = System.Windows.Visibility.Collapsed;
                isSaveEnabled = true;
            }

            // If there are warnings, join them with newlines and make the banner visible
            if(activeWarnings.Count > 0) {
                viewWarningBannerText = string.Join("\n", activeWarnings);
                viewWarningBannerVisibility = System.Windows.Visibility.Visible;
            } else {
                viewWarningBannerText = string.Empty;
                viewWarningBannerVisibility = System.Windows.Visibility.Collapsed;
            }

            // Always notify UI about the banner status updates
            OnPropertyChanged(nameof(viewErrorBannerVisibility));
            OnPropertyChanged(nameof(viewErrorBannerText));
            OnPropertyChanged(nameof(viewWarningBannerVisibility));
            OnPropertyChanged(nameof(viewWarningBannerText));
            OnPropertyChanged(nameof(isSaveEnabled));
            OnPropertyChanged(nameof(showExpertSettings));
            OnPropertyChanged(nameof(enableWordBoundariesForAnchors));
        }
        #endregion
        #region public void Validate_Gui(bool isVisible = true)
        /// <summary>
        /// Validates active control characters for duplicate assignments.
        /// </summary>
        public void Validate_Gui(bool isVisible = true)
        {
            List<string> activeErrors = new List<string>();
            List<string> activeWarnings = new List<string>();

            // 1. Validation for GUI Dimensions
            if(guiSizeX < guiSizeMinX) activeErrors.Add(string.Format(L[001_002_303] /* Search assistant window - Size: The window width is too small (Minimum {0}px). */, guiSizeMinX));
            if(guiSizeY < guiSizeMinY) activeErrors.Add(string.Format(L[001_002_304] /* Search assistant window - Size: The window height is too small (Minimum {0}px). */, guiSizeMinY));

            // 2. Validation for DesktopScreen Docking rules
            if(!isVisible) activeWarnings.Add(L[001_002_400] /* Search assistant window - Position: Due to the selected combination of anchor point, window size, and distance, the window might be partially or fully outside the visible screen area. */);

            // Clear error if everything is valid
            if(activeErrors.Count == 0) viewErrorBannerText_Gui = string.Empty;
            else viewErrorBannerText_Gui = string.Join("\n", activeErrors);

            // Clear warning if everything is valid
            if(activeWarnings.Count == 0) viewWarningBannerText_Gui = string.Empty;
            else viewWarningBannerText_Gui = string.Join("\n", activeWarnings);
        }
        #endregion
        #region public void Validate_Other()
        /// <summary>
        /// Validates active control characters for duplicate assignments.
        /// </summary>
        public void Validate_Other()
        {
            List<string> activeErrors = new List<string>();
            List<string> activeWarnings = new List<string>();

            #region 1. Search Characters
            // Validation for Search Characters
            var activeTokens = new[]
            {
                new { Name = L[001_002_100] /* Character for global OR operator */,          Active = viewOrCharUsed,               Text = viewOrChar               },
                new { Name = L[001_002_101] /* Character for AND operator */,                Active = viewAndCharUsed,              Text = viewAndChar              },
                new { Name = L[001_002_102] /* Character for negation */,                    Active = viewNotCharUsed,              Text = viewNotChar              },
                new { Name = L[001_002_103] /* Character to toggle case sensitivity */,      Active = viewInvertCaseCharUsed,       Text = viewInvertCaseChar       },
                new { Name = L[001_002_104] /* Character to force match at start of text */, Active = viewStartAnchorCharUsed,      Text = viewStartAnchorChar      },
                new { Name = L[001_002_105] /* Character to force match at end of text */,   Active = viewEndAnchorCharUsed,        Text = viewEndAnchorChar        },
                new { Name = L[001_002_106] /* Character for local OR operator */,           Active = viewLocalOrCharUsed,          Text = viewLocalOrChar          },
                new { Name = L[001_002_107] /* Character for metadata */,                    Active = viewMetadataCharUsed,         Text = viewMetadataChar         },
                new { Name = L[001_002_108] /* Character for escaping */,                    Active = viewEscapeCharUsed,           Text = viewEscapeChar           },
                new { Name = L[001_002_114] /* Character for quoting */,                     Active = viewQuoteCharUsed,            Text = viewQuoteChar            },
                new { Name = L[001_002_109] /* Decimal separator */,                         Active = viewDecimalSeparatorCharUsed, Text = viewDecimalSeparatorChar },
                new { Name = L[001_002_110] /* Character for standard search */,             Active = viewStandardModeCharUsed,     Text = viewStandardModeChar     },
                new { Name = L[001_002_111] /* Character for regex search */,                Active = viewRegexModeCharUsed,        Text = viewRegexModeChar        },
                new { Name = L[001_002_115] /* Character for pattern search */,              Active = viewPatternModeCharUsed,      Text = viewPatternModeChar      },
                new { Name = L[001_002_112] /* Character for fuzzy search */,                Active = viewFuzzyModeCharUsed,        Text = viewFuzzyModeChar        },
                new { Name = L[001_002_113] /* Character for sequence search */,             Active = viewSequenceModeCharUsed,     Text = viewSequenceModeChar     }
            }
            .Where(t => t.Active && !string.IsNullOrEmpty(t.Text))
            .ToList();

            var duplicateGroups = activeTokens
                .GroupBy(t => t.Text[0])
                .Where(g => g.Count() > 1);

            foreach(var group in duplicateGroups) {
                var tokenNames = string.Join(" + ", group.Select(t => t.Name));
                activeErrors.Add(string.Format(L[001_002_306] /* Search control characters: Character assigned multiple times: "{0}" at ({1}) */, group.Key, tokenNames));
            }
            #endregion
            #region 2. Metadata
            // Validation for Metadata Aliases (Exclusivity Collision Check)
            var activeMetadataAliases = new[]
            {
                new { Name = L[001_002_200] /* Metadata: Gui         */, Text = metadataAliasGui,     IsExclusive = true,  IsValidAsDefault = false },
                new { Name = L[001_002_201] /* Metadata: Name        */, Text = metadataAliasName,    IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_202] /* Metadata: Extension   */, Text = metadataAliasExt,     IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_203] /* Metadata: Folder      */, Text = metadataAliasFolder,  IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_204] /* Metadata: Path        */, Text = metadataAliasPath,    IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_209] /* Metadata: Attributes  */, Text = metadataAliasAttr,    IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_205] /* Metadata: Description */, Text = metadataAliasDesc,    IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_206] /* Metadata: Content     */, Text = metadataAliasContent, IsExclusive = false, IsValidAsDefault = true  },
                new { Name = L[001_002_207] /* Metadata: Age         */, Text = metadataAliasAge,     IsExclusive = true,  IsValidAsDefault = true  },
                new { Name = L[001_002_208] /* Metadata: Size        */, Text = metadataAliasSize,    IsExclusive = true,  IsValidAsDefault = true  }
            }
            .Where(m => !string.IsNullOrEmpty(m.Text))
            .ToList();

            // Append active and configured WDX definitions to the same list
            foreach(WdxDefinition wdxDefinition in wdxDefinitions) {
                if(wdxDefinition.isActive) {
                    activeMetadataAliases.Add(new { Name = string.Format(L[001_002_290] /* Metadata: WDX - {0} - {1} */, wdxDefinition.pluginName, wdxDefinition.fieldAndUnit), Text = wdxDefinition.metadataAlias, IsExclusive = false, IsValidAsDefault = true });
                }
            }

            var invalidGroups = activeMetadataAliases
                .GroupBy(m => m.Text, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Any(m => m.IsExclusive));

            foreach(var group in invalidGroups) {
                // Find all exclusive items in this faulty group to name them precisely
                var exclusiveItems = group.Where(m => m.IsExclusive);
                foreach(var exclusiveItem in exclusiveItems) activeErrors.Add(string.Format(L[001_002_307] /* Metadata: The exclusive field "{0}" cannot be combined with other fields under the alias "{1}". */, exclusiveItem.Name, group.Key));
            }

            // Metadata Alias vs Control Character Collision Check
            foreach(var token in activeTokens) {
                var collidingToken = activeMetadataAliases.FirstOrDefault(m => m.Text.Contains(token.Text));
                if(collidingToken != null) activeErrors.Add(string.Format(L[001_002_308] /* Metadata: The metadata alias "{0}" ({1}) contains the active control character "{2}" ({3}). */, collidingToken.Text, collidingToken.Name, token.Text, token.Name));
            }

            // Unique pluginIniPath per pluginPath
            var invalidIniGroups = wdxDefinitions
                .Where(w => w.isActive)
                .GroupBy(w => w.pluginPath)
                .Where(g => g.Select(w => w.pluginIniPath).Distinct().Count() > 1);

            foreach(var group in invalidIniGroups) {
                // Extract the plugin file name without extension (e.g. "exif.wdx" or "exif.wdx64" -> "exif"; "ShellDetails.uwdx" or "ShellDetails.wdx" -> "ShellDetails")
                string pluginName = Path.GetFileNameWithoutExtension(group.Key);

                activeWarnings.Add(string.Format(string.Format(L[001_002_405] /* WDX plugin {0}: Different INI files are used for this plugin. Only one single INI file should be defined per plugin. */, pluginName)));
            }

            // Warning for missing/invalid field configurations
            foreach(WdxDefinition wdxDefinition in wdxDefinitions) {
                // Only check if it is active and matches the current process architecture
                if(!wdxDefinition.isActive || wdxDefinition.is64Bit != Environment.Is64BitProcess) continue;

                if(!string.IsNullOrEmpty(wdxDefinition.wdxPluginInstance.loadPluginError) && !activeWarnings.Contains(wdxDefinition.wdxPluginInstance.loadPluginError)) activeWarnings.Add(wdxDefinition.wdxPluginInstance.loadPluginError);
                else {
                    if(!wdxDefinition.wdxPluginInstance.wdxFieldDefinitionLookup.ContainsKey(wdxDefinition.fieldAndUnit)) {
                        activeWarnings.Add(string.Format(L[001_002_406] /* WDX plugin {0}: The configured field "{1}" does not exist in the plugin. */, wdxDefinition.pluginName, wdxDefinition.fieldAndUnit));
                    }
                }
            }

            // Validation for Default Metadata Aliases per Source View

            // 1. Build a hash set of active aliases that are allowed as default
            var validAvailableAliases = activeMetadataAliases
                .Where(m => m.IsValidAsDefault)
                .Select(m => m.Text)
                .ToHashSet();

            // 2. Validate configured default aliases for each view against the set
            var configuredViewDefaults = new[]
            {
                new { ViewName = L[002_008_202] /* File panel (focus file panel → Ctrl+S)                               */, Value = defaultMetadataAliasFileList },
                new { ViewName = L[002_008_203] /* Search results (Find Files → Feed to listbox → Ctrl+S)               */, Value = defaultMetadataAliasSearchResults },
                new { ViewName = L[002_008_204] /* Synchronize directories (Commands → Synchronize Dirs → start typing) */, Value = defaultMetadataAliasSynchronize },
                new { ViewName = L[002_008_206] /* Directory history (Alt+Down arrow → Ctrl+S)                          */, Value = defaultMetadataAliasHistory },
                new { ViewName = L[002_008_207] /* Tab titles (Ctrl+Shift+A → start typing)                             */, Value = defaultMetadataAliasTabTitles },
                new { ViewName = L[002_008_208] /* Tab paths (Ctrl+Shift+A → type "*")                                  */, Value = defaultMetadataAliasTabPaths }
            };

            foreach(var viewDefault in configuredViewDefaults) {
                if(string.IsNullOrEmpty(viewDefault.Value) || !validAvailableAliases.Contains(viewDefault.Value)) {
                    activeErrors.Add(string.Format(L[001_002_318] /* Metadata: The default alias "{0}" for view "{1}" is not a valid or active metadata alias. */, viewDefault.Value, viewDefault.ViewName));
                }
            }
            #endregion
            #region 3. Levenshtein
            // Validation for Levenshtein / Fuzzy Thresholds
            if(levenshteinThreshold1 < 2) activeErrors.Add(string.Format(L[001_002_309] /* Search behavior: For fuzzy search, the typo threshold {0} must be at least {1}. */, 1, 2));
            if(levenshteinThreshold2 < 3) activeErrors.Add(string.Format(L[001_002_309] /* Search behavior: For fuzzy search, the typo threshold {0} must be at least {1}. */, 2, 3));
            if(levenshteinThreshold3 < 4) activeErrors.Add(string.Format(L[001_002_309] /* Search behavior: For fuzzy search, the typo threshold {0} must be at least {1}. */, 3, 4));
            if(levenshteinThreshold2 < levenshteinThreshold1) activeErrors.Add(string.Format(L[001_002_310] /* Search behavior: For fuzzy search, the typo threshold {0} must be greater than or equal to the typo threshold {1}. */, 2, 1));
            if(levenshteinThreshold3 < levenshteinThreshold2) activeErrors.Add(string.Format(L[001_002_310] /* Search behavior: For fuzzy search, the typo threshold {0} must be greater than or equal to the typo threshold {1}. */, 3, 2));
            #endregion
            #region 4. Replacements
            // Strictly forbid any row where 'searchFor' is null or empty
            bool hasInvalidRule = stringReplacementsGui.Any(r => string.IsNullOrEmpty(r.searchFor));

            if(hasInvalidRule) activeErrors.Add(L[001_002_311] /* Text replacements: The search term must not be empty. */);

            // Check for duplicate or conflicting scope assignments across processing pipelines
            var conflictingKeys = stringReplacementsGui
                .Where(r => !string.IsNullOrEmpty(r.searchFor))
                .GroupBy(r => r.searchFor)
                .Where(g => g.Count(r => r.scope == RuleScope.Filter || r.scope == RuleScope.Both) > 1 ||
                            g.Count(r => r.scope == RuleScope.ElementName || r.scope == RuleScope.Both) > 1 ||
                            g.Count(r => r.scope == RuleScope.MatchOnly) > 1);

            foreach(var group in conflictingKeys) activeErrors.Add(string.Format(L[001_002_312] /* Text replacements: The search term "{0}" is defined multiple times in conflicting scopes. */, group.Key));

            // Scope 4 (MatchOnly) restriction: 'searchFor' must be exactly 1 character long
            var invalidMatchOnlyLengths = stringReplacementsGui
                .Where(r => r.scope == RuleScope.MatchOnly && !string.IsNullOrEmpty(r.searchFor) && r.searchFor.Length != 1)
                .Select(r => r.searchFor)
                .Distinct();

            foreach(var key in invalidMatchOnlyLengths) activeErrors.Add(string.Format(L[001_002_313] /* Text replacements: For 1:1 Mapping (Scope 4), the search term "{0}" must be exactly one character long. */, key));

            // Scope 4 (MatchOnly) restriction: 'replaceWith' must not be empty
            var emptyMatchOnlyReplacements = stringReplacementsGui
                .Where(r => r.scope == RuleScope.MatchOnly && !string.IsNullOrEmpty(r.searchFor) && string.IsNullOrEmpty(r.replaceWith))
                .Select(r => r.searchFor)
                .Distinct();

            foreach(var key in emptyMatchOnlyReplacements) activeErrors.Add(string.Format(L[001_002_314] /* Text replacements: For 1:1 Mapping (Scope 4), the replacement text for search term "{0}" must not be empty. */, key));
            #endregion
            #region 5. Performance & Cache
            // Validation for Performance & Cache Limits
            if(maxMetadataObjectCacheCount < 10) activeErrors.Add(string.Format(L[001_002_315] /* Performance, caching & diagnostics: The limit for cache entries must be at least {0}. */, 10));
            if(maxMbToReadForContent < 1) activeErrors.Add(string.Format(L[001_002_316] /* Performance, caching & diagnostics: The maximum file size for reading "@content" must be at least {0} MB. */, 1));
            if(maxContentCacheMb < 10) activeErrors.Add(string.Format(L[001_002_317] /* Performance, caching & diagnostics: The total memory for the "@content" cache must be at least {0} MB. */, 10));

            if(loggingLevel > LoggingLevel.Level_2_Startup) activeWarnings.Add(L[001_002_401] /* Performance, caching & diagnostics: Higher log levels drastically reduce search performance due to extensive log file writing. */);
            if(metadataCacheStrategy == CacheStrategy.Disabled) activeWarnings.Add(L[001_002_402] /* Performance, caching & diagnostics: Disabling the cache significantly reduces search speed, as all file and folder information must be reread with every change to the search text. */);
            #endregion

            // Clear error if everything is valid
            if(activeErrors.Count == 0) viewErrorBannerText_Other = string.Empty;
            else viewErrorBannerText_Other = string.Join("\n", activeErrors);

            // Clear warnings if everything is valid
            if(activeWarnings.Count == 0) viewWarningBannerText_Other = string.Empty;
            else viewWarningBannerText_Other = string.Join("\n", activeWarnings);

            // Notify UI about enabled/disabled textboxes
            OnPropertyChanged(nameof(viewOrCharUsed));
            OnPropertyChanged(nameof(viewAndCharUsed));
            OnPropertyChanged(nameof(viewNotCharUsed));
            OnPropertyChanged(nameof(viewInvertCaseCharUsed));
            OnPropertyChanged(nameof(viewStartAnchorCharUsed));
            OnPropertyChanged(nameof(viewEndAnchorCharUsed));
            OnPropertyChanged(nameof(viewLocalOrCharUsed));
            OnPropertyChanged(nameof(viewMetadataCharUsed));
            OnPropertyChanged(nameof(viewEscapeCharUsed));
            OnPropertyChanged(nameof(viewQuoteCharUsed));
            OnPropertyChanged(nameof(viewDecimalSeparatorCharUsed));
            OnPropertyChanged(nameof(viewStandardModeCharUsed));
            OnPropertyChanged(nameof(viewRegexModeCharUsed));
            OnPropertyChanged(nameof(viewPatternModeCharUsed));
            OnPropertyChanged(nameof(viewFuzzyModeCharUsed));
            OnPropertyChanged(nameof(viewSequenceModeCharUsed));
        }
        #endregion
        #region public void CustomTranslationOverride_SetDefault()
        /// <summary>
        /// Generates the localization template from the current active language,
        /// prepends the AI translation prompt, sorts the IDs, groups them visually with empty lines, 
        /// and updates the custom override field.
        /// </summary>
        public void CustomTranslationOverride_SetDefault()
        {
            var tempDict = new Dictionary<int, string>();

            // 1. Always load English first as the base fallback
            ParseRawStringToDictionary(Plugin.languageManager.availableLanguages[Language.English].rawData, tempDict);

            // 2. Override with other languages if selected
            if(language != Language.English) ParseRawStringToDictionary(Plugin.languageManager.availableLanguages[language].rawData, tempDict);

            StringBuilder sb = new StringBuilder();

            // 3. Prepend the AI Translation Prompt Example (using ID 002_002_008)
            if(tempDict.TryGetValue(002_002_008, out string aiPrompt)) {
                sb.AppendLine(aiPrompt); // 002_002_008=# Copy the template into an AI (e.g., ChatGPT) using the following command: "Translate these texts into [Target Language]. Leave the IDs (numbers before the =) exactly the same and only translate the text to the right of them. Respond exclusively with the finished result without any additional text:"
            }

            // Tracking variables to detect changes in Level 1 or Level 2
            int lastPart1 = -1;
            int lastPart2 = -1;

            // 4. Convert numerical IDs (e.g., 1002003) to section strings (e.g., 001_002_003)
            foreach(var entry in tempDict.OrderBy(e => e.Key)) {
                int idPart1 = entry.Key / 1000000;       //  1002003 / 1000000      = 1
                int idPart2 = (entry.Key / 1000) % 1000; // (1002003 / 1000) % 1000 = 2
                int idPart3 = entry.Key % 1000;          //  1002003 % 1000         = 3

                // 5. Inject an empty line if Level 1 OR Level 2 changes
                // (We check 'lastPart1 != -1' so we don't start the list with an ugly empty line)
                if(idPart1 != lastPart1 || idPart2 != lastPart2) {
                    sb.AppendLine();
                }

                // Update tracking states for the next iteration
                lastPart1 = idPart1;
                lastPart2 = idPart2;

                // First escape standalone backslashes, then convert real newlines back into literal \n tokens
                string cleanValue = entry.Value.Replace("\\", "\\\\").Replace("\n", "\\n");

                sb.AppendLine($"{idPart1:D3}_{idPart2:D3}_{idPart3:D3}={cleanValue}");
            }

            // 6. Update property and notify GUI
            customTranslationOverride = sb.ToString();
            OnPropertyChanged(nameof(customTranslationOverride));
        }
        #endregion
        #region public void CustomColorOverride_SetDefault()
        /// <summary>
        /// Generates the localization template from the current active language,
        /// prepends the AI translation prompt, sorts the IDs, groups them visually with empty lines, 
        /// and updates the custom override field.
        /// </summary>
        public void CustomColorOverride_SetDefault()
        {
            // 1. Update property and notify GUI
            customColorOverride = defaultThemes[theme].TrimStart();
            OnPropertyChanged(nameof(customColorOverride));
        }
        #endregion
        #region public void ParseRawStringToDictionary(string rawData, Dictionary<int, string> dict)
        /// <summary>
        /// Parses raw translation strings into a dictionary with integer keys.
        /// </summary>
        public void ParseRawStringToDictionary(string rawData, Dictionary<int, string> dict)
        {
            if(string.IsNullOrEmpty(rawData)) return;

            // Split by lines but keep empty lines to maintain index integrity if needed
            string[] lines = rawData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach(string line in lines) {
                if(line.Length == 0) continue;

                // 1. Only process lines starting with a digit (0-9), everything else (comments, empty space) is ignored.
                char firstChar = line[0];
                if(firstChar < '0' || firstChar > '9') continue;

                int separatorIndex = line.IndexOf('=');
                if(separatorIndex <= 0) continue;

                // 1. Extract and trim the key, then convert to int (e.g., "001_002_003" -> 1002003)
                string rawKey = line.Substring(0, separatorIndex).Trim();
                if(!TryConvertKeyToInt(rawKey, out int intKey)) continue;

                // 2. Extract value (NO trimming at the end to preserve intentional trailing spaces)
                string rawValue = line.Substring(separatorIndex + 1);

                // 3. Process unescaping character by character
                dict[intKey] = UnescapeValue(rawValue);
            }
        }
        #endregion
        #region private bool TryConvertKeyToInt(string rawKey, out int result)
        /// <summary>
        /// Converts a formatted string key like "001_002_003" directly to an integer: 1002003
        /// </summary>
        private bool TryConvertKeyToInt(string rawKey, out int result)
        {
            return int.TryParse(rawKey.Replace("_", string.Empty), out result);
        }
        #endregion
        #region private string UnescapeValue(string input)
        /// <summary>
        /// Processes the string to handle \\ and \n correctly without simple string replacement.
        /// </summary>
        private string UnescapeValue(string input)
        {
            if(string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new System.Text.StringBuilder(input.Length);

            for(int i = 0; i < input.Length; i++) {
                // Check for escape character and ensure we aren't at the very end of the string
                if(input[i] == '\\' && i + 1 < input.Length) {
                    char next = input[i + 1];
                    if(next == 'n') {
                        sb.Append('\n');
                        i++; // Skip 'n'
                    } else if(next == '\\') {
                        sb.Append('\\');
                        i++; // Skip the second backslash
                    } else {
                        // Treat unknown escape sequences as literal backslashes
                        sb.Append('\\');
                    }
                } else {
                    sb.Append(input[i]);
                }
            }

            return sb.ToString();
        }
        #endregion
    }
    #endregion

    // Search Logic Execution Tree
    #region SearchQueryPlan Class
    /// <summary>
    /// Dictates the high-level operational execution flowchart of an isolated global search event.
    /// Structure sequence maps down into localized structural branches parsed out by OR processing logic.
    /// </summary>
    public class SearchQueryPlan
    {
        public List<SearchBranch> searchBranches { get; } = new List<SearchBranch>();
        public string defaultMetadataAlias;
        public bool usedMetadataGui = false;
        public QueryParser.TokenizationState tokenizationState;
        public InputContext inputContext { get; set; } = InputContext.None;
        public string inputContextText { get; set; } = string.Empty; // Holds fragments or already typed modifiers for autocompletion

        public string rawFilter { get; set; }
        public string processedFilter { get; set; }
        public int benchmarkEvaluationCount = 0;
        public long benchmarkTotalExecutionTicks = 0;
        public long benchmarkTotalStartupTicks = 0;        

        #region public string ToQueryPlanString()
        /// <summary>
        /// Renders a diagnostic visual layout mapping the execution flow hierarchy of evaluated conditions.
        /// </summary>
        public string ToQueryPlanString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SearchQueryPlan - visualize execution plan hierarchy");
            sb.AppendLine($"SearchQueryPlan for \"{rawFilter}\" => PreProcessor => \"{processedFilter}\"");

            if(searchBranches.Count == 0) sb.AppendLine("  ╠═ no Branches defined");

            for(int i = 0; i < searchBranches.Count; i++) {
                if(i != 0) {
                    sb.AppendLine("  ║");
                    sb.AppendLine("  ╟─ OR");
                    sb.AppendLine("  ║");
                }
                sb.AppendLine($"  ╠═ Branch #{i + 1}:");
                sb.Append(searchBranches[i].ToQueryPlanString());
            }

            sb.Append("  ╚════════════════════════════════════");
            return sb.ToString();
        }
        #endregion
    }
    #endregion
    #region SearchBranch Class
    /// <summary>
    /// Represents a single branch separated by the global OR (|).
    /// All conditions inside a branch are evaluated using AND logic.
    /// </summary>
    public class SearchBranch
    {
        public List<SearchCondition> searchConditions { get; } = new List<SearchCondition>();

        #region public string ToQueryPlanString()
        /// <summary>
        /// Renders the logical hierarchy of structural AND evaluations within this branch layout.
        /// </summary>
        public string ToQueryPlanString()
        {
            StringBuilder sb = new StringBuilder();

            if(searchConditions.Count == 0) sb.AppendLine("  ║   ╚═ no Conditions defined -> Matches all files");

            for(int i = 0; i < searchConditions.Count; i++) {
                if(i != 0) sb.AppendLine("  ║   ╟─ AND");
                string linePrefix = (i == searchConditions.Count - 1) ? "  ║   ╚═ " : "  ║   ╠═ ";
                sb.AppendLine($"{linePrefix}{searchConditions[i].ToQueryPlanString()}");
            }
            return sb.ToString();
        }
        #endregion
    }
    #endregion
    #region SearchCondition Class
    /// <summary>
    /// Represents a single evaluation unit within a branch (e.g., matching a term or a metadata value).
    /// </summary>
    public class SearchCondition
    {
        // Prefixes and Modifiers
        public bool isNegated { get; set; }     // Triggered by '!'
        public bool isInvertCase { get; set; }  // Triggered by '~'
        public bool isStartAnchor { get; set; } // Triggered by '^'
        public bool isEndAnchor { get; set; }   // Triggered by '$'
        public bool matchFirstTermAsStartAnchor { get; set; } = false;

        // Mode control
        public SearchMode searchMode { get; set; } = SearchMode.Standard;
        public string metadataAlias { get; set; } = null;
        // Metadata IDs: Built-in metadata IDs use constants 0-6. Custom WDX metadata uses IDs >= 100.
        public List<int> metadataIds { get; set; } = new List<int>();

        // Local OR terms split by '/'
        public List<string> localTerms { get; } = new List<string>();

        // Pre-compiled regex if searchMode == (SearchMode.Regex or SearchMode.Pattern)
        public Regex compiledRegex { get; set; }
        public Regex compiledRegexCaseSensitive { get; set; }
        public string regexParseError { get; set; } = null;
        public string pattern { get; set; } = null;

        // Used for MetadataConstants.Size
        public long minSize { get; set; } = 0;
        public long maxSize { get; set; } = long.MaxValue;
        public List<string> mathParseErrors { get; set; } = new List<string>();

        // Used for MetadataConstants.Age
        // Represents the relative age duration in fractional days (e.g., 2.5 days old)
        public DateTime minDateTime { get; set; } = DateTime.MinValue;
        public DateTime maxDateTime { get; set; } = DateTime.MaxValue;
        public double minAgeDays { get; set; } = 0.0;
        public double maxAgeDays { get; set; } = AgeLimits.MaxValue;

        #region public string ToQueryPlanString()
        /// <summary>
        /// Transforms processing instructions held inside this condition instance into human-readable text syntax for logging analysis.
        /// </summary>
        public string ToQueryPlanString()
        {
            // 1. Resolve metadata Ids to readable text
            string metadataToolTip = Plugin.config._metadataAliasTooltip.TryGetValue(metadataAlias, out string tooltip) ? tooltip : String.Empty;
            string condition = "";

            // 2. Handle specialized mathematical rendering
            if(searchMode == SearchMode.Math) {
                if(metadataIds[0] == MetadataConstants.Size) {
                    if(minSize <= maxSize) {
                        string minStr = FormatSizeReadable(minSize);
                        string maxStr = FormatSizeReadable(maxSize);
                        condition = $"Range: {minStr} to {maxStr}";
                    } else condition = $"Invalid Range";
                }
                if(metadataIds[0] == MetadataConstants.Age) {
                    if(minAgeDays <= maxAgeDays) {
                        string minStr = FormatAgeReadable(minAgeDays);
                        string maxStr = FormatAgeReadable(maxAgeDays);
                        condition = $"Range: {minStr} to {maxStr}";
                    } else condition = $"Invalid Range";
                }
            } else {
                // 3. Build modifier flags
                string modifiers = $"{(isNegated ? "!" : "")}{(isInvertCase ? "~" : "")}{(isStartAnchor ? "^" : "")}{(isEndAnchor ? "$" : "")}";
                if(modifiers.Length != 0) modifiers = $"Modifiers: \"{modifiers}\" - ";

                // 4. Build local OR evaluation lists
                string termsList = localTerms.Count > 0 ? string.Join("\" / \"", localTerms) : "no Terms defined";

                if(searchMode == SearchMode.Regex) {
                    if(regexParseError != null) {
                        condition = $"{modifiers}Regex is incomplete ({regexParseError}) -> Condition matches";
                    } else condition = $"{modifiers}Regex: \"{compiledRegex}\"";
                } else if(searchMode == SearchMode.Pattern) {
                    if(regexParseError != null) {
                        condition = $"{modifiers}Regex for pattern search is incomplete ({regexParseError}) -> Condition matches";
                    } else condition = $"{modifiers}Regex for pattern search: \"{compiledRegex}\"";
                } else condition = $"{modifiers}Terms: \"{termsList}\"";
            }

            return $"[{searchMode}] Metadata alias: {metadataAlias} ({metadataToolTip}) -> {condition}";
        }
        #endregion
        #region public static string FormatSizeReadable(long bytes)
        public static string FormatSizeReadable(long bytes)
        {
            if(bytes == long.MaxValue) return "∞";
            if(bytes == long.MinValue) return "-∞";

            double size = bytes;

            // Checking KB (1024)
            if(size < 1024.0) return $"{size.ToString("F0", CultureInfo.CurrentCulture)} B";
            size /= 1024.0;

            // Checking MB (1024 * 1024)
            if(size < 1024.0) return $"{size.ToString("F1", CultureInfo.CurrentCulture)} KB";
            size /= 1024.0;

            // Checking GB (1024 * 1024 * 1024)
            if(size < 1024.0) return $"{size.ToString("F1", CultureInfo.CurrentCulture)} MB";
            size /= 1024.0;

            // Checking TB (1024 * 1024 * 1024 * 1024)
            if(size < 1024.0) return $"{size.ToString("F1", CultureInfo.CurrentCulture)} GB";
            size /= 1024.0;

            // Checking PB (1024 * 1024 * 1024 * 1024 * 1024)
            if(size < 1024.0) return $"{size.ToString("F1", CultureInfo.CurrentCulture)} TB";
            size /= 1024.0;

            // Checking EB (1024 * 1024 * 1024 * 1024 * 1024 * 1024)
            if(size < 1024.0) return $"{size.ToString("F1", CultureInfo.CurrentCulture)} PB";
            size /= 1024.0;

            // Everything above is EB
            return $"{size.ToString("F1", CultureInfo.CurrentCulture)} EB";
        }
        #endregion
        #region public static string FormatAgeReadable(double days)
        public static string FormatAgeReadable(double days)
        {
            var L = Plugin.config.L;

            if(days >= AgeLimits.MaxValue) return "∞";
            if(days <= 0.0) return L[100_002_246] /* Now */;

            // Checking Seconds
            double value = days * 86400.0 /* a day has 86400 seconds */;
            if(value < 60.0) return string.Format(L[100_002_240] /* {0} seconds */, value.ToString("F0", CultureInfo.CurrentCulture));

            // Checking Minutes
            value = days * 1440.0 /* a day has 1440 minutes */;
            if(value < 60.0) return string.Format(L[100_002_241] /* {0} minutes */, value.ToString("F1", CultureInfo.CurrentCulture));

            // Checking Hours
            value = days * 24.0 /* a day has 24 hours */;
            if(value < 24.0) return string.Format(L[100_002_242] /* {0} hours */, value.ToString("F1", CultureInfo.CurrentCulture));

            // Checking Days
            value = days;
            if(value < 30.436875) return string.Format(L[100_002_243] /* {0} days */, value.ToString("F1", CultureInfo.CurrentCulture));

            // Checking Months
            value = days / 30.436875 /* a year has 12 months */;
            if(value < 12.0) return string.Format(L[100_002_244] /* {0} months */, value.ToString("F1", CultureInfo.CurrentCulture));

            // Everything above is Years
            value = days / 365.2425 /* average days per year factoring in all Gregorian leap rules */;
            return string.Format(L[100_002_245] /* {0} years */, value.ToString("F1", CultureInfo.CurrentCulture));
        }
        #endregion
    }
    #endregion

    // Parser
    #region QueryPreProcessor Class
    /// <summary>
    /// Pure, context-independent string translator.
    /// Executes identical, high-performance replacements for both search queries and file paths.
    /// </summary>
    public class QueryPreProcessor
    {
        #region Helpers

        // Thread-local reusable buffer to eliminate GC allocations during massive file loops
        [ThreadStatic]
        private static StringBuilder _perThreadBuffer;
        #endregion

        #region public string PreProcess(string rawString, bool isFilter)
        /// <summary>
        /// Executes a single linear pass over the raw string, applying all active replacements for either the filter or the element name.
        /// </summary>
        public string PreProcess(string rawString, bool isFilter)
        {
            if(string.IsNullOrEmpty(rawString)) return string.Empty;

            // Select the appropriate pre-compiled structures based on the target pipeline
            var stringReplacements = isFilter ? Plugin.config._stringReplacementsFilter : Plugin.config._stringReplacementsElementName;
            var sortedNeedleLengths = isFilter ? Plugin.config._sortedNeedleLengthsFilter : Plugin.config._sortedNeedleLengthsElementName;

            if(sortedNeedleLengths.Count == 0) return rawString;

            // Lazy initialization for each new worker thread entering this method
            if(_perThreadBuffer == null) {
                _perThreadBuffer = new StringBuilder(256); // Sensible baseline capacity
            } else {
                _perThreadBuffer.Clear(); // Clears the length pointer while keeping the underlying char[] array intact
            }

            int i = 0;

            while(i < rawString.Length) {
                bool matchFound = false;

                // Trigger 1: Longest-Match-First Dictionary Lookup
                foreach(int currentLength in sortedNeedleLengths) {
                    // Skip if the remaining string is too short for the current needle length
                    if(i + currentLength > rawString.Length) continue;

                    string subStringSlice = rawString.Substring(i, currentLength);

                    // Check the pre-compiled dictionary
                    if(stringReplacements.TryGetValue(subStringSlice, out string replacement)) {
                        _perThreadBuffer.Append(replacement);
                        i += currentLength; // Advance the pointer past the original matched needle
                        matchFound = true;
                        break; // Exit the length loop, continue the main string scan
                    }
                }

                // Trigger 2: Fallback Standard Character Copy
                if(!matchFound) {
                    _perThreadBuffer.Append(rawString[i]);
                    i++;
                }
            }

            if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) {
                string result = _perThreadBuffer.ToString();
                if(result != rawString) {
                    string category = isFilter ? "Filter" : "Metadata";
                    Plugin.Log($"PreProcess {category}: {rawString} => {result}");
                }
            }

            return _perThreadBuffer.ToString();
        }
        #endregion
    }
    #endregion
    #region QueryParser Class
    /// <summary>
    /// Parses a raw user quick-search string into an executable SearchQueryPlan hierarchy.
    /// Handles escaping, context-switching via metadata aliases, and mathematical boundary calculation.
    /// </summary>
    public static class QueryParser
    {
        // Helpers
        #region Helpers
        public const int MAX_QUOTE_DEPTH = 4;

        #region private readonly struct CharToken
        /// <summary>
        /// Immutable representation of a character and its escape state.
        /// </summary>
        private readonly struct CharToken
        {
            public char value { get; }
            public bool isEscaped { get; }

            public CharToken(char value, bool isEscaped)
            {
                this.value = value;
                this.isEscaped = isEscaped;
            }
        }
        #endregion
        #region public struct TokenizationState
        public struct TokenizationState
        {
            public bool usedEscapeChar;
            public bool usedQuoteChar;
            public bool isLastCharEscapeChar;
            public int activeQuoteDepth;
        }
        #endregion
        #region private static List<CharToken> Tokenize(string input, out TokenizationState tokenizationState)
        private static List<CharToken> Tokenize(string input, out TokenizationState tokenizationState)
        {
            tokenizationState = new TokenizationState();
            var stream = new List<CharToken>(input.Length);
            bool isEscaped = false;

            char? escapeChar = Plugin.config.escapeChar;
            char? quoteChar = Plugin.config.quoteChar;

            int i = 0;
            while(i < input.Length) {
                char c = input[i];

                // 1. Quoted mode: Everything inside is literal, escapeChar has no special meaning
                if(tokenizationState.activeQuoteDepth > 0) {
                    if(quoteChar.HasValue && c == quoteChar.Value) {
                        int runLength = 0;
                        while(i + runLength < input.Length && input[i + runLength] == quoteChar.Value) runLength++;

                        if(runLength >= tokenizationState.activeQuoteDepth) {
                            // Consumes only the exact number of quotes needed to close
                            i += tokenizationState.activeQuoteDepth;
                            tokenizationState.activeQuoteDepth = 0;
                            continue;
                        }
                    }

                    // Normal character inside quote block
                    stream.Add(new CharToken(c, true));
                    i++;
                    continue;
                }

                // 2. Single character escape logic
                if(isEscaped) {
                    stream.Add(new CharToken(c, true));
                    isEscaped = false;
                    i++;
                    continue;
                }

                if(escapeChar.HasValue && c == escapeChar.Value) {
                    isEscaped = true;
                    tokenizationState.usedEscapeChar = true;
                    i++;
                    continue;
                }

                // 3. Opening quote block logic
                if(quoteChar.HasValue && c == quoteChar.Value) {
                    int runLength = 0;
                    while(i + runLength < input.Length && input[i + runLength] == quoteChar.Value) runLength++;

                    int effectiveDepth = Math.Min(runLength, MAX_QUOTE_DEPTH);

                    tokenizationState.activeQuoteDepth = effectiveDepth;
                    tokenizationState.usedQuoteChar = true;
                    i += effectiveDepth;
                    continue;
                }

                // 4. Normal unescaped character
                stream.Add(new CharToken(c, false));
                i++;
            }

            tokenizationState.isLastCharEscapeChar = isEscaped;
            return stream;
        }
        #endregion
        #region private static List<CharToken> SimpleTokenize(string input)
        private static List<CharToken> SimpleTokenize(string input)
        {
            if(string.IsNullOrEmpty(input)) return new List<CharToken>();

            var stream = new List<CharToken>(input.Length);
            bool isEscaped = false;

            for(int i = 0; i < input.Length; i++) {
                char c = input[i];

                if(isEscaped) {
                    stream.Add(new CharToken(c, true));
                    isEscaped = false;
                    continue;
                }

                if(c == '\\') {
                    isEscaped = true;
                    continue;
                }

                stream.Add(new CharToken(c, false));
            }

            return stream;
        }
        #endregion
        #region private static List<List<CharToken>> SplitStream(List<CharToken> stream, char? splitChar)
        private static List<List<CharToken>> SplitStream(List<CharToken> stream, char? splitChar)
        {
            var result = new List<List<CharToken>>();
            if(!splitChar.HasValue) {
                result.Add(stream);
                return result;
            }

            var current = new List<CharToken>();

            for(int i = 0; i < stream.Count; i++) {
                var token = stream[i];
                if(!token.isEscaped && splitChar.HasValue && token.value == splitChar.Value) {
                    result.Add(current);
                    current = new List<CharToken>();
                } else {
                    current.Add(token);
                }
            }
            result.Add(current);
            return result;
        }
        #endregion
        #region private static int IndexOfUnescaped(List<CharToken> tokens, string matchSeq, int startIndex = 0)
        private static int IndexOfUnescaped(List<CharToken> tokens, string matchSeq, int startIndex = 0)
        {
            if(string.IsNullOrEmpty(matchSeq) || tokens == null || startIndex < 0) return -1;

            int seqLen = matchSeq.Length;
            int maxIndex = tokens.Count - seqLen;

            for(int i = startIndex; i <= maxIndex; i++) {
                bool match = true;
                for(int j = 0; j < seqLen; j++) {
                    var token = tokens[i + j];
                    if(token.isEscaped || token.value != matchSeq[j]) {
                        match = false;
                        break;
                    }
                }
                if(match) return i;
            }
            return -1;
        }
        #endregion
        #region private static string BuildStringFromTokens(List<CharToken> tokens, int startIndex)
        private static string BuildStringFromTokens(List<CharToken> tokens, int startIndex)
        {
            if(startIndex >= tokens.Count) return string.Empty;
            var sb = new StringBuilder(tokens.Count - startIndex);
            for(int i = startIndex; i < tokens.Count; i++) {
                sb.Append(tokens[i].value);
            }
            return sb.ToString();
        }
        #endregion
        #region private static double GetPrecisionDelta(string numberString)
        private static double GetPrecisionDelta(string numberString)
        {
            int decimalPointIndex = numberString.IndexOf('.');
            if(decimalPointIndex == -1) return 0.5;
            int decimalPlaces = numberString.Length - decimalPointIndex - 1;
            return 0.5 / Math.Pow(10, decimalPlaces);
        }
        #endregion
        #endregion

        #region public static SearchQueryPlan Parse(string rawFilter, string processedFilter, int viewId)
        /// <summary>
        /// Converts the raw input query into a complete search execution plan.
        /// </summary>
        public static SearchQueryPlan Parse(string rawFilter, string processedFilter, int viewId)
        {
            var plan = new SearchQueryPlan() { rawFilter = rawFilter, processedFilter = processedFilter };
            if(string.IsNullOrEmpty(processedFilter)) return plan;

            // 1. Linear tokenization respecting escape and quote characters
            List<CharToken> tokenStream = Tokenize(processedFilter, out plan.tokenizationState);
            if(tokenStream.Count == 0) return plan;

            // 2. Split into branches based on unescaped global OR (|)
            List<List<CharToken>> rawBranches = SplitStream(tokenStream, Plugin.config.orChar);

            // Determine default metadata alias according to current viewId
            string defaultMetadataAlias;
            switch(viewId) {
                case 1: defaultMetadataAlias = Plugin.config.defaultMetadataAliasFileList; break;
                case 2: defaultMetadataAlias = Plugin.config.defaultMetadataAliasSearchResults; break;
                case 3: defaultMetadataAlias = Plugin.config.defaultMetadataAliasSynchronize; break;
                case 4: defaultMetadataAlias = Plugin.config.defaultMetadataAliasHistory; break;
                case 5: defaultMetadataAlias = Plugin.config.defaultMetadataAliasTabTitles; break;
                case 6: defaultMetadataAlias = Plugin.config.defaultMetadataAliasTabPaths; break;
                default: defaultMetadataAlias = Plugin.config.defaultMetadataAliasFileList; break;
            }

            if(string.IsNullOrEmpty(defaultMetadataAlias)) defaultMetadataAlias = "name";
            plan.defaultMetadataAlias = defaultMetadataAlias;
            int branchIndex = 0;

            foreach(var rawBranch in rawBranches) {
                var branch = new SearchBranch();

                string currentMetadataAlias = defaultMetadataAlias;
                List<int> currentMetadataIds = new List<int>();
                if(Plugin.config._metadataAliasMappings.TryGetValue(currentMetadataAlias, out List<int> defaultMetadataIds)) currentMetadataIds.AddRange(defaultMetadataIds);

                // Split the branch into individual conditions based on unescaped spaces
                List<List<CharToken>> rawConditions = SplitStream(rawBranch, Plugin.config.andChar);

                int conditionIndex = 0;

                foreach(var rawCondition in rawConditions) {
                    plan.inputContextText = string.Empty;
                    if(rawCondition.Count == 0) {
                        if(currentMetadataIds.Count == 1 && (currentMetadataIds[0] == MetadataConstants.Size || currentMetadataIds[0] == MetadataConstants.Age)) plan.inputContext = InputContext.MathOperatorExpected;
                        else plan.inputContext = InputContext.ConditionStart;
                        conditionIndex++;
                        continue;
                    }
                    
                    bool matchFirstTermAsStartAnchor = branchIndex == 0 && conditionIndex == 0 && Plugin.config.matchFirstTermAsStartAnchorDynamic;

                    if(ParseConditionIntoBranch(rawCondition, branch, ref currentMetadataAlias, ref currentMetadataIds, plan, matchFirstTermAsStartAnchor)) plan.usedMetadataGui = true;

                    conditionIndex++;
                }

                plan.searchBranches.Add(branch);
                branchIndex++;
            }

            return plan;
        }
        #endregion
        #region private static bool ParseConditionIntoBranch(List<CharToken> stream, SearchBranch branch, ref string currentMetadataAlias, ref List<int> currentMetadataIds, SearchQueryPlan plan, bool matchFirstTermAsStartAnchor)
        private static bool ParseConditionIntoBranch(List<CharToken> stream, SearchBranch branch, ref string currentMetadataAlias, ref List<int> currentMetadataIds, SearchQueryPlan plan, bool matchFirstTermAsStartAnchor)
        {
            #region 1. Check for context switch (a @metadata entry)
            if(!stream[0].isEscaped && stream[0].value == Plugin.config.metadataChar) {
                string metadataAlias = BuildStringFromTokens(stream, 1);

                plan.inputContext = InputContext.MetadataAliasTyping;
                plan.inputContextText = metadataAlias;

                if(Plugin.config._metadataAliasMappings.TryGetValue(metadataAlias, out List<int> metadataIds)) {
                    currentMetadataAlias = metadataAlias;
                    currentMetadataIds = metadataIds;
                    if(currentMetadataIds.Count == 1 && currentMetadataIds[0] == MetadataConstants.Gui) return true;
                }
                return false; // A pure @metadata entry only changes the context for subsequent terms, creates no condition itself
            }

            plan.inputContextText = string.Empty;
            #endregion
            #region 2. Extract modifiers (!, ~, ^, $) if not in a mathematical context
            int index = 0;
            bool isNegated = false;
            bool isInvertCase = false;
            bool isStartAnchor = false;
            bool isEndAnchor = false;
            bool mathematicalContext = currentMetadataIds.Count == 1 && (currentMetadataIds[0] == MetadataConstants.Size || currentMetadataIds[0] == MetadataConstants.Age);

            if(!mathematicalContext) {
                string accumulatedModifiers = string.Empty;

                while(index < stream.Count && !stream[index].isEscaped) {
                    char c = stream[index].value;
                    if(!isNegated && c == Plugin.config.notChar) {
                        isNegated = true;
                        accumulatedModifiers += c;
                        index++;
                    } else if(!isInvertCase && c == Plugin.config.invertCaseChar) {
                        isInvertCase = true;
                        accumulatedModifiers += c;
                        index++;
                    } else if(!isStartAnchor && c == Plugin.config.startAnchorChar) {
                        isStartAnchor = true;
                        accumulatedModifiers += c;
                        index++;
                    } else if(!isEndAnchor && c == Plugin.config.endAnchorChar) {
                        isEndAnchor = true;
                        accumulatedModifiers += c;
                        index++;
                    } else break;
                }

                if(!string.IsNullOrEmpty(accumulatedModifiers)) {
                    plan.inputContext = InputContext.ConditionModifier;
                    plan.inputContextText = accumulatedModifiers;
                }
            }

            if(index >= stream.Count) return false;
            #endregion
            #region 3. Determine search mode activation characters
            SearchMode detectedMode = Plugin.config.defaultSearchModeDynamic;

            if(!mathematicalContext) {
                if(!stream[index].isEscaped) {
                    char c = stream[index].value;
                    if(c == Plugin.config.standardModeChar) {
                        plan.inputContext = InputContext.ConditionSearchMode;
                        detectedMode = SearchMode.Standard; index++;
                    } else if(c == Plugin.config.regexModeChar) {
                        plan.inputContext = InputContext.ConditionSearchMode;
                        detectedMode = SearchMode.Regex; index++;
                    } else if(c == Plugin.config.patternModeChar) {
                        plan.inputContext = InputContext.ConditionSearchMode;
                        detectedMode = SearchMode.Pattern; index++;
                    } else if(c == Plugin.config.fuzzyModeChar) {
                        plan.inputContext = InputContext.ConditionSearchMode;
                        detectedMode = SearchMode.Fuzzy; index++;
                    } else if(c == Plugin.config.sequenceModeChar) {
                        plan.inputContext = InputContext.ConditionSearchMode;
                        detectedMode = SearchMode.Sequence; index++;
                    }
                }
            }
            #endregion

            // Extract the remaining payload tokens
            List<CharToken> payloadTokens = stream.GetRange(index, stream.Count - index);

            if(mathematicalContext) {
                #region 4. Handle mathematical evaluation for @size and @age (with mathematical intersection chaining)
                string mathString = BuildStringFromTokens(payloadTokens, 0);

                if(string.IsNullOrEmpty(mathString)) {
                    plan.inputContext = InputContext.MathOperatorExpected;
                } else if(mathString.Length == 1 && (mathString[0] == '=' || mathString[0] == '>' || mathString[0] == '<')) {
                    plan.inputContext = InputContext.MathInputExpected;
                } else {
                    plan.inputContext = (currentMetadataIds[0] == MetadataConstants.Size) ? InputContext.MathSizeUnitExpected : InputContext.MathAgeUnitExpected;
                }

                // Smart Chaining: Check if the branch already has a mathematical condition for this metadata id
                SearchCondition targetCondition = null;
                foreach(var existingCondition in branch.searchConditions) {
                    if(existingCondition.metadataIds.Count == 1 && existingCondition.metadataIds[0] == currentMetadataIds[0]) {
                        targetCondition = existingCondition;
                        break;
                    }
                }

                // If none exists, create a new one
                if(targetCondition == null) {
                    targetCondition = new SearchCondition {
                        metadataAlias = currentMetadataAlias,
                        metadataIds = currentMetadataIds,
                        searchMode = SearchMode.Math
                    };
                    ParseMathematicalBoundaries(mathString, targetCondition);
                    branch.searchConditions.Add(targetCondition);
                } else {
                    // Update boundaries by calculating intersecting ranges
                    ParseMathematicalBoundaries(mathString, targetCondition);
                }
                #endregion
            } else {
                #region 5. Regular text filtering: Handle Local OR split ('/')
                var condition = new SearchCondition {
                    metadataAlias = currentMetadataAlias,
                    metadataIds = currentMetadataIds,
                    isNegated = isNegated,
                    isInvertCase = isInvertCase,
                    isStartAnchor = isStartAnchor,
                    isEndAnchor = isEndAnchor,
                    matchFirstTermAsStartAnchor = matchFirstTermAsStartAnchor,
                    searchMode = detectedMode
                };

                bool IsRegexOrPatternSearch = condition.searchMode == SearchMode.Regex || condition.searchMode == SearchMode.Pattern;

                if(payloadTokens.Count > 0) {
                    if(IsRegexOrPatternSearch) plan.inputContext = InputContext.ConditionInTermRegexOrPattern;
                    else plan.inputContext = InputContext.ConditionInTerm;
                }

                List<List<CharToken>> rawLocalTerms = SplitStream(payloadTokens, IsRegexOrPatternSearch ? null /* a regex search is not split by LocalOrChar */ : Plugin.config.localOrChar);
                foreach(var localTermTokens in rawLocalTerms) {
                    if(localTermTokens.Count == 0) continue;
                    string term = BuildStringFromTokens(localTermTokens, 0);
                    condition.localTerms.Add(term);
                }
                if(condition.localTerms.Count == 0) return false;
                #endregion
                #region 6. Precompile RegEx if active
                if(IsRegexOrPatternSearch && condition.localTerms.Count > 0) {
                    string pattern = condition.localTerms[0];
                    if(condition.searchMode == SearchMode.Pattern) {
                        condition.pattern = pattern;
                        pattern = ConvertPatternToRegex(pattern);
                    }

                    if(condition.matchFirstTermAsStartAnchor ^ condition.isStartAnchor) pattern = Plugin.config.wordBoundaryUniversalStartAnchor + pattern;
                    if(condition.isEndAnchor) pattern += Plugin.config.wordBoundaryUniversalEndAnchor;

                    try {
                        condition.compiledRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        condition.compiledRegexCaseSensitive = new Regex(pattern, RegexOptions.Compiled);
                    } catch(ArgumentException ex) {
                        condition.regexParseError = ex.Message;
                    }
                }

                branch.searchConditions.Add(condition);
                #endregion
            }
            return false;
        }
        #endregion
        #region private static void ParseMathematicalBoundaries(string input, SearchCondition condition)
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static void ParseMathematicalBoundaries(string input, SearchCondition condition)
        {
            char? decimalSeparator = Plugin.config.decimalSeparatorChar;

            // 2. Build the regex pattern dynamically based on whether a decimal separator is allowed
            // If separator is null, no decimal places are permitted, and the group is omitted from the pattern
            string decimalGroup = string.Empty;
            if(decimalSeparator.HasValue) {
                string escapedDecimalSeparator = Regex.Escape(decimalSeparator.Value.ToString());
                decimalGroup = $"(?:{escapedDecimalSeparator}[0-9]+)?";
            }

            // Regex pattern to extract mathematical conditions: Optional operator (=, <, >), followed by a number (with optional decimal parts if configured), followed by an optional unit
            string dynamicPattern = $@"^(=|<|>)?([0-9]+{decimalGroup})([a-zA-Z]*)$";

            // 3. Execute the match
            Match m = Regex.Match(input, dynamicPattern);
            if(!m.Success) { condition.mathParseErrors.Add(input); return; }

            string op = m.Groups[1].Value;
            string numStr = m.Groups[2].Value;
            string unit = m.Groups[3].Value.ToUpperInvariant();

            // 4. Normalize the custom separator back to a dot to ensure safe parsing via InvariantCulture
            if(decimalSeparator.HasValue) {
                numStr = numStr.Replace(decimalSeparator.Value, '.');
            }

            // 5. Parse the normalized numerical string
            if(!double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double rawValue)) { condition.mathParseErrors.Add(input); return; }

            if(condition.metadataIds[0] == MetadataConstants.Size) {
                #region Size boundary logic (Bytes) - Inclusive Intersection
                long bytesMultiplier = -1;
                switch(unit) {
                    case "": bytesMultiplier = 1; break;
                    case "B": bytesMultiplier = 1; break;
                    case "K": bytesMultiplier = 1024; break;
                    case "KB": bytesMultiplier = 1024; break;
                    case "M": bytesMultiplier = 1024 * 1024; break;
                    case "MB": bytesMultiplier = 1024 * 1024; break;
                    case "G": bytesMultiplier = 1024L * 1024 * 1024; break;
                    case "GB": bytesMultiplier = 1024L * 1024 * 1024; break;
                    case "T": bytesMultiplier = 1024L * 1024 * 1024 * 1024; break;
                    case "TB": bytesMultiplier = 1024L * 1024 * 1024 * 1024; break;
                    case "P": bytesMultiplier = 1024L * 1024 * 1024 * 1024 * 1024; break;
                    case "PB": bytesMultiplier = 1024L * 1024 * 1024 * 1024 * 1024; break;
                    case "E": bytesMultiplier = 1024L * 1024 * 1024 * 1024 * 1024 * 1024; break;
                    case "EB": bytesMultiplier = 1024L * 1024 * 1024 * 1024 * 1024 * 1024; break;
                }

                if(bytesMultiplier == -1) { condition.mathParseErrors.Add(input); return; }

                double calculatedSize = rawValue * bytesMultiplier;

                if(op == ">") {
                    // Inclusive Greater-Than (>=): Tighten lower bound
                    condition.minSize = Math.Max(condition.minSize, DoubleToLongSafe(calculatedSize));
                } else if(op == "<") {
                    // Inclusive Less-Than (<=): Tighten upper bound
                    condition.maxSize = Math.Min(condition.maxSize, DoubleToLongSafe(calculatedSize));
                } else {
                    // Tolerance match (=): Center point with unsharp precision delta
                    double delta = bytesMultiplier == 1 /* If no unit is provided (plain bytes), force exact match by setting delta to 0 */ ? 0.0 : (GetPrecisionDelta(numStr) * bytesMultiplier);

                    condition.minSize = Math.Max(condition.minSize, DoubleToLongSafe(calculatedSize - delta));
                    condition.maxSize = Math.Min(condition.maxSize, DoubleToLongSafe(calculatedSize + delta));
                }
                #endregion
            } else if(condition.metadataIds[0] == MetadataConstants.Age) {
                #region Age boundary logic (Days) - Inclusive Intersection
                var L = Plugin.config.L;

                double daysMultiplier = -1.0;

                if(unit == "") daysMultiplier = 1.0;

                // Localized units (checked first to prevent collisions)
                else if(unit == L[100_002_231]) daysMultiplier = 1.0 / 86400.0 /*  a day  has 86400 seconds                              */;
                else if(unit == L[100_002_232]) daysMultiplier = 1.0 / 1440.0 /*   a day  has  1440 minutes                              */;
                else if(unit == L[100_002_233]) daysMultiplier = 1.0 / 24.0 /*     a day  has    24 hours                                */;
                else if(unit == L[100_002_234]) daysMultiplier = 1.0 /*            a day  has     1 day ;-)                              */;
                else if(unit == L[100_002_235]) daysMultiplier = 7.0 /*            a week has     7 days                                 */;
                else if(unit == L[100_002_236]) daysMultiplier = 30.436875 /*      a year has    12 months                               */;
                else if(unit == L[100_002_237]) daysMultiplier = 365.2425 /* average days per year factoring in all Gregorian leap rules */;

                // English fallback units
                else if(unit == "S") daysMultiplier = 1.0 / 86400.0 /*           a day  has 86400 seconds                              */;
                else if(unit == "M") daysMultiplier = 1.0 / 1440.0 /*            a day  has  1440 minutes                              */;
                else if(unit == "H") daysMultiplier = 1.0 / 24.0 /*              a day  has    24 hours                                */;
                else if(unit == "D") daysMultiplier = 1.0 /*                     a day  has     1 day ;-)                              */;
                else if(unit == "W") daysMultiplier = 7.0 /*                     a week has     7 days                                 */;
                else if(unit == "MO") daysMultiplier = 30.436875 /*              a year has    12 months                               */;
                else if(unit == "Y") daysMultiplier = 365.2425 /*          average days per year factoring in all Gregorian leap rules */;

                if(daysMultiplier == -1.0) { condition.mathParseErrors.Add(input); return; }

                double calculatedDays = rawValue * daysMultiplier;
                DateTime referenceDateTime = DateTime.Now;

                if(op == ">") {
                    // Inclusive Greater-Than (>=): Tighten lower bound
                    DateTime newMaxDateTime = TryAddDaysSafe(referenceDateTime, -calculatedDays);
                    if(condition.maxDateTime > newMaxDateTime) {
                        condition.maxDateTime = newMaxDateTime;
                        condition.minAgeDays = calculatedDays;
                    }
                } else if(op == "<") {
                    // Inclusive Less-Than (<=): Tighten upper bound
                    DateTime newMinDateTime = TryAddDaysSafe(referenceDateTime, -calculatedDays);
                    if(condition.minDateTime < newMinDateTime) {
                        condition.minDateTime = newMinDateTime;
                        condition.maxAgeDays = calculatedDays;
                    }
                } else {
                    // Tolerance match (=): Center point with unsharp precision delta
                    double delta = GetPrecisionDelta(numStr) * daysMultiplier;
                    double newMinAgeDays = calculatedDays - delta;
                    double newMaxAgeDays = calculatedDays + delta;

                    DateTime newMinDateTime = TryAddDaysSafe(referenceDateTime, -newMaxAgeDays);
                    DateTime newMaxDateTime = TryAddDaysSafe(referenceDateTime, -newMinAgeDays);

                    if(condition.minDateTime < newMinDateTime) {
                        condition.minDateTime = newMinDateTime;
                        condition.maxAgeDays = newMaxAgeDays;
                    }
                    if(condition.maxDateTime > newMaxDateTime) {
                        condition.maxDateTime = newMaxDateTime;
                        condition.minAgeDays = newMinAgeDays;
                    }
                }
                if(condition.minAgeDays > AgeLimits.MaxValue) condition.minAgeDays = AgeLimits.MaxValue;
                #endregion
            }
        }
        #endregion
        #region private static DateTime TryAddDaysSafe(DateTime baseTime, double days)
        /// <summary>
        /// Safely adds fractional days to a DateTime instance without throwing an ArgumentOutOfRangeException.
        /// Clamps the output directly to DateTime bounds if an overflow occurs.
        /// </summary>
        private static DateTime TryAddDaysSafe(DateTime baseTime, double days)
        {
            try {
                // Guard clause using the absolute maximum calendar span from Year 1 to 9999 (including leap years)
                if(days > 3652059.0 /* Max days from 01.01.0001 to 31.12.9999 */) return DateTime.MaxValue;
                if(days < -3652059.0 /* Max days to safely subtract from maximum date range */) return DateTime.MinValue;

                return baseTime.AddDays(days);
            } catch {
                return days < 0.0 ? DateTime.MinValue : DateTime.MaxValue;
            }
        }
        #endregion
        #region private static long DoubleToLongSafe(double value)
        /// <summary>
        /// Safely clamps a double value to valid long boundaries to prevent Win32/Delphi FPU exceptions during casting.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static long DoubleToLongSafe(double value)
        {
            if(double.IsNaN(value) || value <= (double)long.MinValue) return long.MinValue;
            if(value >= (double)long.MaxValue) return long.MaxValue;
            return (long)value;
        }
        #endregion

        // Convert Pattern to Regex
        #region public static string ConvertPatternToRegex(string pattern)
        public static string ConvertPatternToRegex(string pattern)
        {
            if(string.IsNullOrEmpty(pattern)) return string.Empty;

            List<CharToken> tokens = SimpleTokenize(pattern);
            var result = new StringBuilder(pattern.Length * 2);
            ConvertTokenStreamToRegex(tokens, result);
            return result.ToString();
        }
        #endregion
        #region private static void ConvertTokenStreamToRegex(List<CharToken> tokens, StringBuilder result)
        private static void ConvertTokenStreamToRegex(List<CharToken> tokens, StringBuilder result)
        {
            int i = 0;

            while(i < tokens.Count) {
                var token = tokens[i];

                if(token.isEscaped) {
                    result.Append(Regex.Escape(token.value.ToString()));
                    i++;
                    continue;
                }

                if(token.value == '[') {
                    int closeIndex = IndexOfUnescaped(tokens, "]", i + 1);
                    if(closeIndex != -1) {
                        List<CharToken> innerTokens = tokens.GetRange(i + 1, closeIndex - i - 1);
                        if(TryProcessSquareBrackets(innerTokens, result)) {
                            i = closeIndex + 1;
                            continue;
                        }
                    } else {
                        // Incomplete bracket while typing: Stop processing further tokens until bracket is closed
                        break;
                    }
                }

                if(token.value == '(') {
                    int closeIndex = IndexOfUnescaped(tokens, ")", i + 1);
                    if(closeIndex != -1) {
                        List<CharToken> innerTokens = tokens.GetRange(i + 1, closeIndex - i - 1);
                        // No recursion risk: Since inner content is truncated at the first ')', any inner '(' inherently lacks a matching closing bracket.
                        if(TryProcessRoundBrackets(innerTokens, result)) {
                            i = closeIndex + 1;
                            continue;
                        }
                    } else {
                        // Incomplete parenthesis while typing: Stop processing further tokens until parenthesis is closed
                        break;
                    }
                }

                switch(token.value) {
                    case '*': result.Append(".*?"); break; // Non-greedy match: stops at the earliest matching anchor to prevent backtracking past subsequent pattern matches
                    case '?': result.Append('.'); break;
                    case '#': result.Append(@"\d"); break;
                    case '@': result.Append(@"\p{L}"); break; // Matches any Unicode letter, including accented characters (e.g., 'ä', 'é') and non-Latin scripts (e.g., 'ж', '日')
                    default: result.Append(Regex.Escape(token.value.ToString())); break;
                }
                i++;
            }
        }
        #endregion
        #region private static bool TryProcessRoundBrackets(List<CharToken> innerTokens, StringBuilder result)
        private static bool TryProcessRoundBrackets(List<CharToken> innerTokens, StringBuilder result)
        {
            if(innerTokens.Count == 0) return false;

            List<List<CharToken>> wordOptions = SplitStream(innerTokens, ',');
            result.Append("(?:"); // Non-capturing group: groups OR-options without memory overhead

            for(int w = 0; w < wordOptions.Count; w++) {
                if(w > 0) result.Append('|');
                ConvertTokenStreamToRegex(wordOptions[w], result);
            }

            result.Append(')');
            return true;
        }
        #endregion
        #region private static bool TryProcessSquareBrackets(List<CharToken> innerTokens, StringBuilder result)
        private static bool TryProcessSquareBrackets(List<CharToken> innerTokens, StringBuilder result)
        {
            if(innerTokens.Count == 0) return false;

            // 1. Check for number range [1..100], [001..100], [17-23..230-07], [1980.04.16..1983-09-18], [17.03.2026..09.04.2026]
            int dotDotIndex = IndexOfUnescaped(innerTokens, "..");
            if(dotDotIndex != -1) {
                List<CharToken> part1 = innerTokens.GetRange(0, dotDotIndex);
                List<CharToken> part2 = innerTokens.GetRange(dotDotIndex + 2, innerTokens.Count - (dotDotIndex + 2));

                if(part1.Count == 0 && part2.Count > 0) {
                    // fill open start range "[..789]" with "000": [000..789]
                    foreach(var token in part2) {
                        part1.Add(new CharToken(!token.isEscaped && char.IsDigit(token.value) ? '0' : token.value, token.isEscaped));
                    }
                } else if(part2.Count == 0 && part1.Count > 0) {
                    // fill open end range "[123..]" with "999": [123..999]
                    foreach(var token in part1) {
                        part2.Add(new CharToken(!token.isEscaped && char.IsDigit(token.value) ? '9' : token.value, token.isEscaped));
                    }
                }

                if(TryBuildRangeRegex(part1, part2, result)) {
                    return true;
                }
            }

            // 2. Negated or standard character class [!...] / [...]
            bool isNegated = !innerTokens[0].isEscaped && innerTokens[0].value == '!';
            int startIndex = isNegated ? 1 : 0;

            result.Append(isNegated ? "[^" : "[");

            for(int i = startIndex; i < innerTokens.Count; i++) {
                char c = innerTokens[i].value;

                // Escapes special characters inside character classes to preserve their literal meaning
                if(c == ']' || c == '\\' || c == '^' || c == '-') result.Append('\\');
                result.Append(c);
            }

            result.Append(']');
            return true;
        }
        #endregion

        // Build a Range Pattern
        #region private class RangeChunk
        private class RangeChunk
        {
            public RangeChunkType type;

            public string literalValue;

            public long numberValue;
            public int digitCount;
            public int digitOffset;
        }
        #endregion
        #region private static List<RangeChunk> ParseRangeChunks(List<CharToken> tokens)
        private static List<RangeChunk> ParseRangeChunks(List<CharToken> tokens)
        {
            var chunks = new List<RangeChunk>();
            int i = 0;

            while(i < tokens.Count) {
                // 1. Digits -> Number Chunk
                if(!tokens[i].isEscaped && char.IsDigit(tokens[i].value)) {
                    var sbNum = new StringBuilder();

                    while(i < tokens.Count && !tokens[i].isEscaped && char.IsDigit(tokens[i].value)) {
                        sbNum.Append(tokens[i].value);
                        i++;
                    }

                    string numStr = sbNum.ToString();
                    if(!long.TryParse(numStr, out long val)) return null; // Prevent overflow

                    chunks.Add(new RangeChunk {
                        type = RangeChunkType.Number,
                        numberValue = val,
                        digitCount = numStr.Length
                    });
                    continue;
                }

                // 2. Non-digits -> Literal Chunk
                var sbLit = new StringBuilder();
                while(i < tokens.Count && (tokens[i].isEscaped || !char.IsDigit(tokens[i].value))) {
                    sbLit.Append(tokens[i].value);
                    i++;
                }

                chunks.Add(new RangeChunk {
                    type = RangeChunkType.Literal,
                    literalValue = sbLit.ToString()
                });
            }

            return chunks;
        }
        #endregion
        #region private static bool ValidateRangeChunks(List<RangeChunk> chunks1, List<RangeChunk> chunks2, out bool requireLeadingZeros)
        private static bool ValidateRangeChunks(List<RangeChunk> chunks1, List<RangeChunk> chunks2, out bool requireLeadingZeros)
        {
            requireLeadingZeros = true;
            if(chunks1 == null || chunks2 == null || chunks1.Count == 0 || chunks1.Count != chunks2.Count) return false;

            for(int i = 0; i < chunks1.Count; i++) {
                var c1 = chunks1[i];
                var c2 = chunks2[i];

                // Mismatched token types (Literal vs Number) invalidate the range comparison
                if(c1.type != c2.type) return false;

                // Literal chunks must match exactly between both boundaries
                if(c1.type == RangeChunkType.Literal && c1.literalValue != c2.literalValue) return false;

                // Verify if all numeric pairs share identical string lengths for padded digit handling
                if(c1.type == RangeChunkType.Number && c1.digitCount != c2.digitCount) {
                    requireLeadingZeros = false;
                }
            }

            return true;
        }
        #endregion
        #region private static bool IsGermanDatePattern(List<RangeChunk> chunks)
        private static bool IsGermanDatePattern(List<RangeChunk> chunks)
        {
            if(chunks.Count != 5) return false;

            // Extrawurst: Matches German date formats (like DD.MM.YYYY or DD-MM-YYYY)
            return chunks[0].type == RangeChunkType.Number && chunks[0].digitCount == 2 &&
                   chunks[1].type == RangeChunkType.Literal && chunks[1].literalValue.Length == 1 &&
                   chunks[2].type == RangeChunkType.Number && chunks[2].digitCount == 2 &&
                   chunks[3].type == RangeChunkType.Literal && chunks[3].literalValue.Length == 1 &&
                   chunks[4].type == RangeChunkType.Number && chunks[4].digitCount == 4;
        }
        #endregion
        #region private static bool AssignDigitOffsetsAndExtractNumbers(List<RangeChunk> chunks1, List<RangeChunk> chunks2, out long number1, out long number2, out int totalDigits)
        private static bool AssignDigitOffsetsAndExtractNumbers(List<RangeChunk> chunks1, List<RangeChunk> chunks2, out long number1, out long number2, out int totalDigits)
        {
            number1 = 0;
            number2 = 0;
            totalDigits = 0;

            // Determine processing order for chunks based on value hierarchy
            int[] chunkOrder;
            if(IsGermanDatePattern(chunks1)) {
                chunkOrder = new int[] { 4, 2, 0 }; // Year (4) -> Month (2) -> Day (0)
            } else {
                chunkOrder = new int[chunks1.Count];
                for(int i = 0; i < chunks1.Count; i++) chunkOrder[i] = i;
            }

            // Normalize originalLength to max length of both chunks
            for(int i = 0; i < chunks1.Count; i++) {
                if(chunks1[i].type == RangeChunkType.Number) {
                    int maxLen = Math.Max(chunks1[i].digitCount, chunks2[i].digitCount);
                    chunks1[i].digitCount = maxLen;
                    chunks2[i].digitCount = maxLen;
                }
            }

            // Assign global offsets and shift numbers mathematically
            foreach(int chunkIndex in chunkOrder) {
                if(chunks1[chunkIndex].type != RangeChunkType.Number) continue;

                chunks1[chunkIndex].digitOffset = totalDigits;
                chunks2[chunkIndex].digitOffset = totalDigits;

                int len = chunks1[chunkIndex].digitCount;
                totalDigits += len;

                // max digits for long-numbers
                if(totalDigits > 18) return false;

                long factor = 1;
                for(int k = 0; k < len; k++) factor *= 10;

                number1 = number1 * factor + chunks1[chunkIndex].numberValue;
                number2 = number2 * factor + chunks2[chunkIndex].numberValue;
            }

            return number1 <= number2;
        }
        #endregion
        #region private class DigitRange
        private class DigitRange
        {
            public int Min;
            public int Max;

            public DigitRange(int min, int max)
            {
                Min = min;
                Max = max;
            }
        }
        #endregion
        #region private static List<DigitRange[]> GenerateDigitGrid(long number1, long number2, int totalDigits)
        private static List<DigitRange[]> GenerateDigitGrid(long number1, long number2, int totalDigits)
        {
            var grid = new List<DigitRange[]>();

            string numberString1 = number1.ToString().PadLeft(totalDigits, '0');
            string numberString2 = number2.ToString().PadLeft(totalDigits, '0');

            int[] digits1 = new int[totalDigits];
            int[] digits2 = new int[totalDigits];

            for(int i = 0; i < totalDigits; i++) {
                digits1[i] = numberString1[i] - '0';
                digits2[i] = numberString2[i] - '0';
            }

            // 1. Identify common prefix (digits that are identical in both numbers)
            int commonPrefixIndex = 0;
            while(commonPrefixIndex < totalDigits && digits1[commonPrefixIndex] == digits2[commonPrefixIndex]) commonPrefixIndex++;

            // Common prefix template
            var commonPrefix = new DigitRange[totalDigits];
            for(int i = 0; i < commonPrefixIndex; i++) commonPrefix[i] = new DigitRange(digits1[i], digits1[i]);

            // Special case: number1 and number2 are identical
            if(commonPrefixIndex == totalDigits) {
                grid.Add(commonPrefix);
                return grid;
            }

            int firstDiffDigit1 = digits1[commonPrefixIndex];
            int firstDiffDigit2 = digits2[commonPrefixIndex];

            // 2. Lower boundary branches (paths for number1 filling up to 9s)
            for(int i = totalDigits - 1; i > commonPrefixIndex; i--) {
                int currentDigit1 = digits1[i];
                int min = currentDigit1 + (i == totalDigits - 1 ? 0 : 1);
                int max = 9;

                if(min <= max) {
                    var row = (DigitRange[])commonPrefix.Clone();

                    // Fill exact digits of number1 between commonPrefixIndex and i
                    for(int k = commonPrefixIndex; k < i; k++) row[k] = new DigitRange(digits1[k], digits1[k]);

                    row[i] = new DigitRange(min, max);

                    // Fill full wildcards for all remaining digits after i
                    for(int j = i + 1; j < totalDigits; j++) row[j] = new DigitRange(0, 9);

                    grid.Add(row);
                }
            }

            // 3. Middle range
            bool isLastDigitPos = (commonPrefixIndex == totalDigits - 1);
            int midStart = isLastDigitPos ? firstDiffDigit1 : firstDiffDigit1 + 1;
            int midEnd = isLastDigitPos ? firstDiffDigit2 : firstDiffDigit2 - 1;

            if(midStart <= midEnd) {
                var midRow = (DigitRange[])commonPrefix.Clone();

                midRow[commonPrefixIndex] = new DigitRange(midStart, midEnd);

                for(int j = commonPrefixIndex + 1; j < totalDigits; j++) midRow[j] = new DigitRange(0, 9);
                grid.Add(midRow);
            }

            // 4. Upper boundary branches (paths starting from 0s up to number2)
            for(int i = commonPrefixIndex + 1; i < totalDigits; i++) {
                int currentDigit2 = digits2[i];
                int min = 0;
                int max = currentDigit2 - (i == totalDigits - 1 ? 0 : 1);

                if(min <= max) {
                    var row = (DigitRange[])commonPrefix.Clone();

                    // Fill exact digits of number2 between commonPrefixIndex and i
                    for(int k = commonPrefixIndex; k < i; k++) row[k] = new DigitRange(digits2[k], digits2[k]);

                    row[i] = new DigitRange(min, max);

                    // Fill full wildcards for all remaining digits after i
                    for(int j = i + 1; j < totalDigits; j++) row[j] = new DigitRange(0, 9);

                    grid.Add(row);
                }
            }

            return grid;
        }
        #endregion
        #region private static void RenderGridToRegex(List<DigitRange[]> grid, List<RangeChunk> chunks, bool requireLeadingZeros, StringBuilder result)
        private static void RenderGridToRegex(List<DigitRange[]> grid, List<RangeChunk> chunks, bool requireLeadingZeros, StringBuilder result)
        {
            if(grid.Count == 0) return;

            // Determine boundaries for static outer literals
            bool hasLeadingLiteral = chunks.Count > 0 && chunks[0].type == RangeChunkType.Literal;
            bool hasTrailingLiteral = chunks.Count > 1 && chunks[chunks.Count - 1].type == RangeChunkType.Literal;

            int startIndex = hasLeadingLiteral ? 1 : 0;
            int endIndex = hasTrailingLiteral ? chunks.Count - 2 : chunks.Count - 1;

            // 1. Render static outer prefix
            if(hasLeadingLiteral) result.Append(Regex.Escape(chunks[0].literalValue));

            // 2. Render dynamic inner grid
            if(startIndex <= endIndex) {
                bool useOuterGroup = grid.Count > 1;
                if(useOuterGroup) result.Append("(?:");

                for(int r = 0; r < grid.Count; r++) {
                    if(r > 0) result.Append('|');

                    DigitRange[] row = grid[r];

                    for(int c = startIndex; c <= endIndex; c++) {
                        var chunk = chunks[c];
                        if(chunk.type == RangeChunkType.Literal) {
                            result.Append(Regex.Escape(chunk.literalValue));
                        } else {
                            RenderChunkDigits(row, chunk.digitOffset, chunk.digitCount, requireLeadingZeros, result);
                        }
                    }
                }

                if(useOuterGroup) result.Append(')');
            }

            // 3. Render static outer suffix
            if(hasTrailingLiteral) result.Append(Regex.Escape(chunks[chunks.Count - 1].literalValue));
        }
        #endregion
        #region private static void RenderChunkDigits(DigitRange[] row, int digitOffset, int digitCount, bool requireLeadingZeros, StringBuilder result)
        private static void RenderChunkDigits(DigitRange[] row, int digitOffset, int digitCount, bool requireLeadingZeros, StringBuilder result)
        {
            // Step 1: Tokenize
            var tokens = new (string Text, bool IsOptional)[digitCount];
            bool allowOptional = !requireLeadingZeros;

            for(int i = 0; i < digitCount; i++) {
                DigitRange range = row[digitOffset + i];
                string text;

                if(range.Min == 0 && range.Max == 9) {
                    text = @"\d";
                } else if(range.Min == range.Max) {
                    text = range.Min.ToString();
                } else if(range.Max == range.Min + 1) {
                    text = $"[{range.Min}{range.Max}]";
                } else {
                    text = $"[{range.Min}-{range.Max}]";
                }

                if(i == digitCount - 1 || range.Min != 0) allowOptional = false;

                tokens[i] = (text, allowOptional);
            }

            // Step 2: Collect
            int index = 0;
            while(index < digitCount) {
                string currentText = tokens[index].Text;
                int totalCount = 0;
                int optionalCount = 0;

                while(index < digitCount && tokens[index].Text == currentText) {
                    totalCount++;
                    if(tokens[index].IsOptional) optionalCount++;
                    index++;
                }

                int minCount = totalCount - optionalCount;

                if(minCount == 1 && totalCount == 1) {
                    result.Append(currentText);
                } else if(minCount == 0 && totalCount == 1) {
                    result.Append(currentText + "?");
                } else if(minCount == totalCount) {
                    result.Append($"{currentText}{{{totalCount}}}");
                } else {
                    result.Append($"{currentText}{{{minCount},{totalCount}}}");
                }
            }
        }
        #endregion
        #region private static bool TryBuildRangeRegex(List<CharToken> part1, List<CharToken> part2, StringBuilder result)
        private static bool TryBuildRangeRegex(List<CharToken> part1, List<CharToken> part2, StringBuilder result)
        {
            // Example pattern input: [img_17.jpg..img_881.jpg]
            // Produced regex output: img_(?:0?1[7-9]|0?[2-9]\d|[1-7]\d{2}|8[0-7]\d|8{2}[01])\.jpg

            // Step 1: Parse flat character token streams into structured chunks (literals vs. numeric digit groups).
            // Example part1 ("img_17.jpg")  -> [Literal("img_"), Digits("17"),  Literal(".jpg")]
            // Example part2 ("img_881.jpg") -> [Literal("img_"), Digits("881"), Literal(".jpg")]
            List<RangeChunk> chunks1 = ParseRangeChunks(part1);
            List<RangeChunk> chunks2 = ParseRangeChunks(part2);

            // Step 2: Ensure structural alignment between start and end bounds.
            // Verifies that literal chunks match identically and determines leading zero constraints.
            // Example: Literals "img_" and ".jpg" match. Length mismatch ("17" vs "881") sets requireLeadingZeros = false.
            if(!ValidateRangeChunks(chunks1, chunks2, out bool requireLeadingZeros)) return false;

            // Step 3: Extract numeric boundaries and assign grid digit offsets across all numeric chunks.
            // Example: number1 = 17, number2 = 881, totalDigits = 3 (padded width of the largest bound).
            if(!AssignDigitOffsetsAndExtractNumbers(chunks1, chunks2, out long number1, out long number2, out int totalDigits)) return false;

            // Step 4: Construct the N-dimensional digit matrix covering the entire numeric span [number1..number2].
            // Example grid rows for [17..881] with 3 digits:
            //   Row 0: [0..0][1..1][7..9] -> 017..019
            //   Row 1: [0..0][2..9][0..9] -> 020..099
            //   Row 2: [1..7][0..9][0..9] -> 100..799
            //   Row 3: [8..8][0..7][0..9] -> 800..879
            //   Row 4: [8..8][8..8][0..1] -> 880..881
            List<DigitRange[]> grid = GenerateDigitGrid(number1, number2, totalDigits);

            // Step 5: Render static literal chunks and dynamic digit grid rows into the final regex pattern.
            // Processes optional leading zeros when requireLeadingZeros is false (e.g. [0..0][1..1][7..9] -> 0?1[7-9]).
            // Assembles final pattern: img_(?:0?1[7-9]|0?[2-9]\d|[1-7]\d{2}|8[0-7]\d|8{2}[01])\.jpg
            RenderGridToRegex(grid, chunks1, requireLeadingZeros, result);

            return true;
        }
        #endregion
    }
    #endregion

    // Files & Folders
    #region FileMatcher Class
    public static class FileMatcher
    {
        /// Scope 4 Lookup - Maps a single typed search character to a collection of dynamically allowed matching characters (e.g., 'a' to 'ä', 'á', 'à').
        public static Dictionary<char, HashSet<char>> matchOnlyLookup = new Dictionary<char, HashSet<char>>();
        public static Dictionary<char, HashSet<char>> matchOnlyLookupLower = new Dictionary<char, HashSet<char>>();
        public static bool useExtendedMatching = false;
        public static bool usePinYinMatching = false;
        public static bool useKoreanMatching = false;

        private static readonly CompareInfo invariantCompare = CultureInfo.InvariantCulture.CompareInfo;

        #region public static bool IsMatch(SearchQueryPlan plan, FileSystemEvaluationContext entry)
        /// <summary>
        /// Evaluates if an entry meets the criteria specified in the SearchQueryPlan.
        /// Called in a loop for each file entry.
        /// </summary>
        public static bool IsMatch(SearchQueryPlan plan, FileSystemEvaluationContext entry)
        {
            // If the query plan is empty, everything matches
            if(plan.searchBranches.Count == 0) return true;

            // 1. Global OR: At least one branch must evaluate to true
            for(int i = 0; i < plan.searchBranches.Count; i++) {
                if(EvaluateBranch(plan.searchBranches[i], entry)) {
                    return true; // Short-circuit: Branch matched -> Include file in results
                }
            }

            return false;
        }
        #endregion
        #region private static bool EvaluateBranch(SearchBranch branch, FileSystemEvaluationContext entry)
        /// <summary>
        /// Verifies if all sequential search conditions within a specific execution branch evaluate to true.
        /// </summary>
        private static bool EvaluateBranch(SearchBranch branch, FileSystemEvaluationContext entry)
        {
            // 2. Global AND: ALL conditions within the branch must evaluate to true
            for(int i = 0; i < branch.searchConditions.Count; i++) {
                var condition = branch.searchConditions[i];

                if(!EvaluateCondition(condition, entry)) {
                    return false; // Short-circuit: One condition failed -> Branch is dead
                }
            }

            return true;
        }
        #endregion
        #region private static bool EvaluateCondition(SearchCondition condition, FileSystemEvaluationContext entry)
        /// <summary>
        /// Routes the evaluation logic to the respective engine based on the specified SearchMode.
        /// </summary>
        private static bool EvaluateCondition(SearchCondition condition, FileSystemEvaluationContext entry)
        {
            // 3. Execution routing based on SearchMode
            bool isMatch = false;

            switch(condition.searchMode) {
                case SearchMode.Math:
                    isMatch = EvaluateMathCondition(condition, entry);
                    break;

                case SearchMode.Regex:
                case SearchMode.Pattern:
                    bool useCaseSensitive = Plugin.config.caseSensitiveDynamic ^ condition.isInvertCase;

                    // Regular expressions are pre-compiled during the parsing stage
                    if(condition.regexParseError != null) isMatch = true;
                    else {
                        foreach(int metadataId in condition.metadataIds) {
                            string textToSearch = entry.GetStringProperty(metadataId);
                            if(useCaseSensitive) {
                                if(condition.compiledRegexCaseSensitive != null && condition.compiledRegexCaseSensitive.IsMatch(textToSearch)) { isMatch = true; break; }
                            } else {
                                if(condition.compiledRegex != null && condition.compiledRegex.IsMatch(textToSearch)) { isMatch = true; break; }
                            }
                        }
                    }
                    break;

                case SearchMode.Standard:
                case SearchMode.Fuzzy:
                case SearchMode.Sequence:
                    // Resolve local OR logic (e.g., matching "house/yard/garden")
                    isMatch = EvaluateTextTerms(condition, entry);
                    break;
            }

            // Apply negation modifier (!) if requested
            return condition.isNegated ? !isMatch : isMatch;
        }
        #endregion
        #region private static bool EvaluateMathCondition(SearchCondition condition, FileSystemEvaluationContext entry)
        /// <summary>
        /// Handles mathematical validation and range intersections for file size and age parameters.
        /// </summary>
        private static bool EvaluateMathCondition(SearchCondition condition, FileSystemEvaluationContext entry)
        {
            if(condition.metadataIds[0] == MetadataConstants.Size) {
                long size = entry.GetFileSize();
                return size >= condition.minSize && size <= condition.maxSize;
            }

            if(condition.metadataIds[0] == MetadataConstants.Age) {
                DateTime lastModified = entry.GetLastModified();

                return lastModified >= condition.minDateTime && lastModified <= condition.maxDateTime;
            }

            return true;
        }
        #endregion
        #region private static bool EvaluateTextTerms(SearchCondition condition, FileSystemEvaluationContext entry)
        /// <summary>
        /// Evaluates local text parameters, verifying if at least one token satisfies the local OR criteria.
        /// </summary>
        private static bool EvaluateTextTerms(SearchCondition condition, FileSystemEvaluationContext entry)
        {
            bool useCaseSensitive = Plugin.config.caseSensitiveDynamic ^ condition.isInvertCase;
            bool useStartAnchor = condition.matchFirstTermAsStartAnchor ^ condition.isStartAnchor;
            CompareOptions compareOptions = CompareOptions.None;
            if(!useCaseSensitive) compareOptions |= CompareOptions.IgnoreCase;
            if(Plugin.config.ignoreAccents) compareOptions |= CompareOptions.IgnoreNonSpace;

            foreach(int metadataId in condition.metadataIds) {
                string haystack = entry.GetStringProperty(metadataId);
                if(string.IsNullOrEmpty(haystack)) continue;

                // Local OR: At least one term within the slash-separated condition must match
                for(int i = 0; i < condition.localTerms.Count; i++) {
                    string needle = condition.localTerms[i];

                    // Executes the matching strategy depending on standard, fuzzy, or sequence mode
                    if(MatchText(haystack, needle, condition.searchMode, compareOptions, useCaseSensitive, useStartAnchor, condition.isEndAnchor)) {
                        return true; // One term matched -> Local OR requirement fulfilled
                    }
                }
            }
            return false;
        }
        #endregion
        #region private static bool MatchText(string haystack, string needle, SearchMode searchMode, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Orchestrates the text matching strategy depending on the designated SearchMode and anchor flags.
        /// </summary>
        private static bool MatchText(string haystack, string needle, SearchMode searchMode, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            switch(searchMode) {
                case SearchMode.Standard:
                    if(Plugin.config.wordBoundarySet.Count != 0 && (isStartAnchor || isEndAnchor)) {
                        return MatchStandardTextUsingWordBoundaries(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor);
                    }
                    return MatchStandardText(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor);

                case SearchMode.Fuzzy:
                    // Fuzzy matching via Damerau-Levenshtein distance

                    // Determine maximum allowed differences dynamically based on needle length and thresholds
                    int maxAllowedDifferences = 0;
                    int needleLength = needle.Length;

                    if(needleLength >= Plugin.config.levenshteinThreshold3) {
                        maxAllowedDifferences = 3;
                    } else if(needleLength >= Plugin.config.levenshteinThreshold2) {
                        maxAllowedDifferences = 2;
                    } else if(needleLength >= Plugin.config.levenshteinThreshold1) {
                        maxAllowedDifferences = 1;
                    }

                    int differences = (Plugin.config.wordBoundarySet.Count != 0 && (isStartAnchor || isEndAnchor))
                        ? CalculateLevenshteinDistanceUsingWordBoundaries(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor)
                        : CalculateLevenshteinDistance(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor);

                    return differences <= maxAllowedDifferences;

                case SearchMode.Sequence:
                    // Character-by-character sequential matching (scattered search)
                    if(Plugin.config.wordBoundarySet.Count != 0 && (isStartAnchor || isEndAnchor)) {
                        return MatchSequenceTextUsingWordBoundaries(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor);
                    }
                    return MatchSequenceText(haystack, needle, compareOptions, useCaseSensitive, isStartAnchor, isEndAnchor);
            }

            return false;
        }
        #endregion
        #region private static bool IsValidWordBoundaryStart/IsValidWordBoundaryEnd(string haystack, int index)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidWordBoundaryStart(string haystack, int index)
        {
            if(index == 0) return true;
            return Plugin.config.wordBoundarySet.Contains(haystack[index - 1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidWordBoundaryEnd(string haystack, int index)
        {
            if(index == haystack.Length) return true;
            return Plugin.config.wordBoundarySet.Contains(haystack[index]);
        }
        #endregion
        #region private static bool MatchStandardText(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Performs standard substring, prefix or suffix matching using the extended character comparison engine.
        /// </summary>
        private static bool MatchStandardText(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int haystackLength = haystack.Length;
            int needleLength = needle.Length;

            if(needleLength == 0) return true;
            if(haystackLength < needleLength) return false;

            // Fast path: If no advanced matching is requested, use blazing fast native .NET string operations
            if(!useExtendedMatching && !Plugin.config.ignoreAccents) {
                StringComparison comparison = useCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                if(isStartAnchor && isEndAnchor) return haystack.Equals(needle, comparison);
                if(isStartAnchor) return haystack.StartsWith(needle, comparison);
                if(isEndAnchor) return haystack.EndsWith(needle, comparison);
                return haystack.IndexOf(needle, comparison) >= 0;
            }

            // Slow path: Advanced matching required (Scope 4, PinYin, Korean or Accents)

            // Anchor optimizations for full match, start, or end
            if(isStartAnchor && isEndAnchor) {
                if(haystackLength != needleLength) return false;
                for(int i = 0; i < needleLength; i++) {
                    if(!ExtendedCharMatch(needle[i], haystack[i], compareOptions, useCaseSensitive)) return false;
                }
                return true;
            }

            if(isStartAnchor) {
                for(int i = 0; i < needleLength; i++) {
                    if(!ExtendedCharMatch(needle[i], haystack[i], compareOptions, useCaseSensitive)) return false;
                }
                return true;
            }

            if(isEndAnchor) {
                int haystackStart = haystackLength - needleLength;
                for(int i = 0; i < needleLength; i++) {
                    if(!ExtendedCharMatch(needle[i], haystack[haystackStart + i], compareOptions, useCaseSensitive)) return false;
                }
                return true;
            }

            // Classic substring sliding window search (equivalent to IndexOf >= 0)
            int maxShift = haystackLength - needleLength;
            for(int startIdx = 0; startIdx <= maxShift; startIdx++) {
                bool matchFound = true;
                for(int i = 0; i < needleLength; i++) {
                    if(!ExtendedCharMatch(needle[i], haystack[startIdx + i], compareOptions, useCaseSensitive)) {
                        matchFound = false;
                        break;
                    }
                }
                if(matchFound) return true;
            }

            return false;
        }
        #endregion
        #region private static bool MatchStandardTextUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Performs word-boundary-aware matching where anchors ^ and $ match at delimiters or text edges.
        /// </summary>
        private static bool MatchStandardTextUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int haystackLength = haystack.Length;
            int needleLength = needle.Length;

            if(needleLength == 0) return true;
            if(haystackLength < needleLength) return false;

            // Fast path: If no advanced matching is requested, use blazing fast native .NET string operations
            if(!useExtendedMatching && !Plugin.config.ignoreAccents) {
                StringComparison comparison = useCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int idx = 0;

                while((idx = haystack.IndexOf(needle, idx, comparison)) >= 0) {
                    bool startOk = !isStartAnchor || IsValidWordBoundaryStart(haystack, idx);
                    bool endOk = !isEndAnchor || IsValidWordBoundaryEnd(haystack, idx + needleLength);

                    if(startOk && endOk) return true;
                    idx++;
                }

                return false;
            }

            // Classic substring sliding window search (equivalent to IndexOf >= 0)
            int maxShift = haystackLength - needleLength;
            for(int startIdx = 0; startIdx <= maxShift; startIdx++) {
                if(isStartAnchor && !IsValidWordBoundaryStart(haystack, startIdx)) continue;
                if(isEndAnchor && !IsValidWordBoundaryEnd(haystack, startIdx + needleLength)) continue;

                bool matchFound = true;
                for(int i = 0; i < needleLength; i++) {
                    if(!ExtendedCharMatch(needle[i], haystack[startIdx + i], compareOptions, useCaseSensitive)) {
                        matchFound = false;
                        break;
                    }
                }

                if(matchFound) return true;
            }

            return false;
        }
        #endregion
        #region private static bool MatchSequenceText(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Scans the target text sequentially for the presence of all characters in the needle, maintaining order.
        /// </summary>
        private static bool MatchSequenceText(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int haystackLength = haystack.Length;
            int needleLength = needle.Length;

            if(needleLength == 0) return true;
            if(haystackLength < needleLength) return false;

            if(isStartAnchor && !ExtendedCharMatch(needle[0], haystack[0], compareOptions, useCaseSensitive)) {
                return false;
            }
            if(isEndAnchor && !ExtendedCharMatch(needle[needleLength - 1], haystack[haystackLength - 1], compareOptions, useCaseSensitive)) {
                return false;
            }

            int i = 0; // Needle index pointer
            for(int j = 0; j < haystackLength; j++) {
                if(ExtendedCharMatch(needle[i], haystack[j], compareOptions, useCaseSensitive)) {
                    i++;
                }

                if(i == needleLength) {
                    return true; // All characters found sequentially!
                }
            }

            return false;
        }
        #endregion
        #region private static bool MatchSequenceTextUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Scans the target text sequentially for needle characters with word boundary support for anchors.
        /// </summary>
        private static bool MatchSequenceTextUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int haystackLength = haystack.Length;
            int needleLength = needle.Length;

            if(needleLength == 0) return true;
            if(haystackLength < needleLength) return false;

            int i = 0; // Needle index pointer
            for(int j = 0; j < haystackLength; j++) {
                if(ExtendedCharMatch(needle[i], haystack[j], compareOptions, useCaseSensitive)) {
                    if(i == 0 && isStartAnchor && !IsValidWordBoundaryStart(haystack, j)) continue;
                    if(i == needleLength - 1 && isEndAnchor && !IsValidWordBoundaryEnd(haystack, j + 1)) continue;

                    i++;
                }

                if(i == needleLength) {
                    return true; // All characters found sequentially!
                }
            }

            return false;
        }
        #endregion
        #region private static int CalculateLevenshteinDistance(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Calculates the modified Damerau-Levenshtein distance for substring matching with anchor support.
        /// </summary>
        private static int CalculateLevenshteinDistance(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int n = haystack.Length;
            int m = needle.Length;

            if(n == 0) return m;
            if(m == 0) return 0;

            // Using a flat 2D matrix array for the distance map calculations
            int[,] matrix = new int[n + 1, m + 1];

            // Substring adjustment: Initialize vertical edge to 0 to allow matching anywhere
            for(int i = 0; i <= n; i++) {
                matrix[i, 0] = 0;
            }

            // If forced to match beginning, enforce standard Levenshtein penalty on vertical edge
            if(isStartAnchor) {
                for(int i = 1; i <= n; i++) {
                    matrix[i, 0] = i;
                }
            }

            // Standard initialization of the horizontal edge
            for(int j = 0; j <= m; j++) {
                matrix[0, j] = j;
            }

            int minimumResult = int.MaxValue;

            for(int j = 1; j <= m; j++) {
                char t_j = needle[j - 1];

                for(int i = 1; i <= n; i++) {
                    char s_i = haystack[i - 1];

                    int cost = ExtendedCharMatch(t_j, s_i, compareOptions, useCaseSensitive) ? 0 : 1;

                    // Step 1: Classical Levenshtein operations (Insertion, Deletion, Substitution)
                    int cell = Math.Min(
                        matrix[i - 1, j] + 1,          // Deletion
                        Math.Min(matrix[i, j - 1] + 1, // Insertion
                        matrix[i - 1, j - 1] + cost)   // Substitution
                    );

                    // Step 2: Damerau Transposition check (swapped adjacent characters)
                    if(i > 2 && j > 2) {
                        int trans = matrix[i - 2, j - 2] + 1;
                        if(!ExtendedCharMatch(t_j, haystack[i - 2], compareOptions, useCaseSensitive)) trans++;
                        if(!ExtendedCharMatch(needle[j - 2], s_i, compareOptions, useCaseSensitive)) trans++;

                        if(cell > trans) {
                            cell = trans;
                        }
                    }

                    matrix[i, j] = cell;

                    // Track result scores at the final character of the needle matrix
                    if(j == m) {
                        if(isEndAnchor) {
                            // If end anchor is active, only the cell matching the exact end of the haystack is valid
                            if(i == n) {
                                minimumResult = matrix[i, j];
                            }
                        } else {
                            // Substring matching: gather the absolute lowest error score found across the last line
                            if(matrix[i, j] < minimumResult) {
                                minimumResult = matrix[i, j];
                            }
                        }
                    }
                }
            }

            return minimumResult;
        }
        #endregion
        #region private static int CalculateLevenshteinDistanceUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        /// <summary>
        /// Calculates the modified Damerau-Levenshtein distance for substring matching with word boundary anchor support.
        /// </summary>
        private static int CalculateLevenshteinDistanceUsingWordBoundaries(string haystack, string needle, CompareOptions compareOptions, bool useCaseSensitive, bool isStartAnchor, bool isEndAnchor)
        {
            int n = haystack.Length;
            int m = needle.Length;

            if(n == 0) return m;
            if(m == 0) return 0;

            // Using a flat 2D matrix array for the distance map calculations
            int[,] matrix = new int[n + 1, m + 1];

            // Substring adjustment: Initialize vertical edge
            for(int i = 0; i <= n; i++) {
                if(isStartAnchor) {
                    // Allowed free start only if position i is a valid word boundary start
                    if(IsValidWordBoundaryStart(haystack, i)) {
                        matrix[i, 0] = 0;
                    } else {
                        // Penalty propagates from previous cell if not a valid boundary
                        matrix[i, 0] = (i > 0) ? matrix[i - 1, 0] + 1 : 0;
                    }
                } else {
                    matrix[i, 0] = 0;
                }
            }

            // Standard initialization of the horizontal edge
            for(int j = 0; j <= m; j++) {
                matrix[0, j] = j;
            }

            int minimumResult = int.MaxValue;

            for(int j = 1; j <= m; j++) {
                char t_j = needle[j - 1];

                for(int i = 1; i <= n; i++) {
                    char s_i = haystack[i - 1];

                    int cost = ExtendedCharMatch(t_j, s_i, compareOptions, useCaseSensitive) ? 0 : 1;

                    // Step 1: Classical Levenshtein operations (Insertion, Deletion, Substitution)
                    int cell = Math.Min(
                        matrix[i - 1, j] + 1,          // Deletion
                        Math.Min(matrix[i, j - 1] + 1, // Insertion
                        matrix[i - 1, j - 1] + cost)   // Substitution
                    );

                    // Step 2: Damerau Transposition check (swapped adjacent characters)
                    if(i > 2 && j > 2) {
                        int trans = matrix[i - 2, j - 2] + 1;
                        if(!ExtendedCharMatch(t_j, haystack[i - 2], compareOptions, useCaseSensitive)) trans++;
                        if(!ExtendedCharMatch(needle[j - 2], s_i, compareOptions, useCaseSensitive)) trans++;

                        if(cell > trans) {
                            cell = trans;
                        }
                    }

                    matrix[i, j] = cell;

                    // Track result scores at the final character of the needle matrix
                    if(j == m) {
                        if(isEndAnchor) {
                            // Only consider positions that are valid word boundary ends
                            if(IsValidWordBoundaryEnd(haystack, i)) {
                                if(matrix[i, j] < minimumResult) {
                                    minimumResult = matrix[i, j];
                                }
                            }
                        } else {
                            // Substring matching: gather the absolute lowest error score found across the last line
                            if(matrix[i, j] < minimumResult) {
                                minimumResult = matrix[i, j];
                            }
                        }
                    }
                }
            }

            return minimumResult;
        }
        #endregion

        #region public static bool ExtendedCharMatch(char searchChar, char targetChar, CompareOptions compareOptions, bool useCaseSensitive)
        /// <summary>
        /// Evaluates if a search character matches a target character via Scope 4 rules, PinYin or Korean Hangul translation layers.
        /// - Chinese characters are compared to their English PinYin pendants (thanks to Christian Ghisler)
        /// - Korean characters are compared to other Korean characters that the current character could lead to (by appending vowels and trailing consonants)
        /// </summary>
        public static bool ExtendedCharMatch(char searchChar, char targetChar, CompareOptions compareOptions, bool useCaseSensitive)
        {
            // 1. Fast-Path: If characters are completely identical, it's always a match
            if(searchChar == targetChar) return true;

            // 2. Base .NET Collation (handles standard Case and Accents natively)
            if(compareOptions != CompareOptions.None) {
                if(compareOptions == CompareOptions.IgnoreCase) {
                    if(char.ToLowerInvariant(searchChar) == char.ToLowerInvariant(targetChar)) return true;
                } else {
                    if(invariantCompare.Compare(searchChar.ToString(), targetChar.ToString(), compareOptions) == 0) return true;
                }
            }

            if(!useExtendedMatching) return false;

            // 3. Scope 4 (MatchOnly) User Rules: Highest priority and ultra-fast O(1) lookup
            if(!useCaseSensitive) {
                // Normalize for linguistic layers (as pinyin and jamo targets are lowercase/invariant)
                searchChar = char.ToLowerInvariant(searchChar);
                targetChar = char.ToLowerInvariant(targetChar);

                if(matchOnlyLookupLower.TryGetValue(searchChar, out var allowedTargetsLower)) {
                    if(allowedTargetsLower.Contains(targetChar)) return true;
                }
            } else {
                if(matchOnlyLookup.TryGetValue(searchChar, out var allowedTargets)) {
                    if(allowedTargets.Contains(targetChar)) return true;
                }
            }

            // If both linguistic features are disabled, we can safely stop here
            if(!usePinYinMatching && !useKoreanMatching) return false;

            // 4. Chinese PinYin Layer
            #region usePinYinMatching
            if(usePinYinMatching) {
                if(targetChar == 0x3007) return searchChar == 'l';

                if(targetChar >= 0x4E00 && targetChar <= 0x9FA5 /* Chinese characters */) {
                    ushort tableEntry = Plugin.pinYinTable[targetChar - 0x4E00];
                    char englishChar1 = (char)((tableEntry & 0x1F) - 1 + 'a');
                    char englishChar2 = (char)(((tableEntry >> 5) & 0x1F) - 1 + 'a');
                    char englishChar3 = (char)(((tableEntry >> 10) & 0x1F) - 1 + 'a');

                    bool result = searchChar == englishChar1 || searchChar == englishChar2 || searchChar == englishChar3;
                    if(!result) {
                        // Hardcoded edge cases for the 3 ideographs with more than 3 spellings
                        switch(targetChar) {
                            case (char)0x7AD3: // qian1 fen1 zhi1 yi1 gong1 sheng1
                                result = searchChar == 'y' || searchChar == 'g' || searchChar == 's';
                                break;
                            case (char)0x7AD5: // shi2 fen1 zhi1 yi1 gong1 sheng1
                                result = searchChar == 'y' || searchChar == 'g';
                                break;
                            case (char)0x7AE1: // yi1 gong1 sheng1 bai3 bei4
                                result = searchChar == 'b';
                                break;
                        }
                    }
                    return result;
                }
            }
            #endregion

            // 5. Korean Hangul / Jamo Layer
            #region useKoreanMatching
            if(useKoreanMatching) {
                if(targetChar >= 0xAC00 && targetChar <= 0xD7A4 /* combined Korean characters */) {
                    // 1) a lead consonant in the search string should match all combinations of this lead consonant with any vowel.
                    // 2) a lead consonant in the search string should match all combinations of this lead consonant with any vowel and trail consonants.
                    // 3) a lead consonant combined with a vowel in the search string should match all combinations with a trail consonant.
                    int syllableIndex = targetChar - 0xAC00;

                    char leadConsonant = (char)(0x1100 + syllableIndex / 588);
                    if(searchChar == leadConsonant) return true;

                    char leadConsonantCompatibility = (char)Plugin.koreanTable1[syllableIndex / 588];
                    if(searchChar == leadConsonantCompatibility) return true;

                    //char vowel = (char)(0x1161 + (syllableIndex % 588) / 28);
                    //if(searchChar == vowel) return true;

                    //char vowelCompatibility = (char)(0x314F + (syllableIndex % 588) / 28);
                    //if(searchChar == vowelCompatibility) return true;

                    //char trailingConsonant = (char)(0x11A7 + syllableIndex % 28); // when trailingConsonant==0x11A7 then it is omitted
                    //if(searchChar == trailingConsonant) return true;

                    //char trailingConsonantCompatibility = (char)Plugin.koreanTable3[syllableIndex % 28]; // when trailingConsonant==0x11A7 then it is omitted
                    //if(searchChar == trailingConsonantCompatibility) return true;

                    char leadVowelCombination = (char)(0xAC00 + syllableIndex - (syllableIndex % 28));
                    if(searchChar == leadVowelCombination) return true;

                    return false;
                }

                // Compatibility and standard Jamo cross-matching loops
                if(targetChar >= 0x1100 && targetChar <= 0x1112 && Plugin.koreanTable1[targetChar - 0x1100] == searchChar) return true; // leading consonants match each other (normal and compatibility jamo)
                if(searchChar >= 0x1100 && searchChar <= 0x1112 && Plugin.koreanTable1[searchChar - 0x1100] == targetChar) return true; // leading consonants match each other (normal and compatibility jamo)

                if(targetChar >= 0x1161 && targetChar <= 0x1175 && targetChar - 0x1161 == searchChar - 0x314F) return true; // vowels match each other (normal and compatibility jamo)
                if(searchChar >= 0x1161 && searchChar <= 0x1175 && searchChar - 0x1161 == targetChar - 0x314F) return true; // vowels match each other (normal and compatibility jamo)

                if(targetChar >= 0x11A8 && targetChar <= 0x11C2 && Plugin.koreanTable3[targetChar - 0x11A7] == searchChar) return true; // trailing consonants match each other (normal and compatibility jamo)
                if(searchChar >= 0x11A8 && searchChar <= 0x11C2 && Plugin.koreanTable3[searchChar - 0x11A7] == targetChar) return true; // trailing consonants match each other (normal and compatibility jamo)
                return false;
            }
            #endregion

            return false;
        }
        #endregion
    }
    #endregion
    #region FileSystemEvaluationContext Class
    /// <summary>
    /// Holds the state and lazy-cached data for a single search entry during the evaluation loop.
    /// Supports files, directories, and arbitrary custom text types (e.g., Total Commander tab names).
    /// Prevents redundant disk I/O operations across multiple search branches.
    /// </summary>
    public class FileSystemEvaluationContext
    {
        #region Helpers
        private FileSystemEntryType? _entryType = null;
        private static readonly Dictionary<string, string> descriptionCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string descriptionCacheFolder = null;
        #endregion

        // Infrastructure data provided by Total Commander
        public string fullPath { get; }

        // Lazy-loaded fields (populated only when demanded by specific metadata)
        private long? fileSize = null;
        private DateTime? lastModified = null;

        // Core dictionary mapping MetadataConstants (or custom WDX IDs >= 100) to their string values
        private readonly Dictionary<int, string> stringProperties = new Dictionary<int, string>();

        #region Constructor
        /// <summary>
        /// Initializes a new context with the option to force a specific entry type (e.g., CustomText),
        /// completely bypassing expensive disk I/O operations during initialization and evaluation.
        /// </summary>
        public FileSystemEvaluationContext(string path, FileSystemEntryType? forcedType)
        {
            fullPath = path;

            if(forcedType.HasValue) {
                _entryType = forcedType.Value;
            }

            // Safe string parsing helper variables
            string name = string.Empty;
            string extension = string.Empty;
            string parentFolder = string.Empty;

            try {
                // Only try parsing as path layout if it contains characters
                if(!string.IsNullOrEmpty(path) && forcedType != FileSystemEntryType.CustomText) {
                    name = Path.GetFileName(path);

                    string ext = Path.GetExtension(path);
                    extension = ext.StartsWith(".") ? ext.Substring(1) : ext;

                    string directoryPath = Path.GetDirectoryName(path);
                    parentFolder = !string.IsNullOrEmpty(directoryPath) ? Path.GetFileName(directoryPath) : string.Empty;
                }
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not handle string as path: {ex.Message}{Environment.NewLine}{ex}");

                // Fallback for completely arbitrary custom texts that violate path formats
                name = path;
            }

            // Populate the dictionary with standard string components immediately
            SetPreProcessedProperty(MetadataConstants.Name, string.IsNullOrEmpty(name) ? path : name);
            SetPreProcessedProperty(MetadataConstants.Ext, extension);
            SetPreProcessedProperty(MetadataConstants.Folder, parentFolder);
            SetPreProcessedProperty(MetadataConstants.Path, path);
        }
        #endregion
        #region public FileSystemEntryType entryType
        /// <summary>
        /// Gets the specialized entry type. Determines and caches the entry classification on first call.
        /// </summary>
        public FileSystemEntryType entryType {
            get {
                if(!_entryType.HasValue) {
                    DetermineEntryType();
                }
                return _entryType.Value;
            }
        }
        #endregion
        #region private void DetermineEntryType()
        /// <summary>
        /// Evaluates the physical or virtual nature of the path exactly once using a nullable state check.
        /// </summary>
        private void DetermineEntryType()
        {
            try {
                if(string.IsNullOrEmpty(fullPath)) {
                    _entryType = FileSystemEntryType.CustomText;
                    return;
                }

                if(Directory.Exists(fullPath)) {
                    _entryType = FileSystemEntryType.Directory;
                } else if(File.Exists(fullPath)) {
                    _entryType = FileSystemEntryType.File;
                } else {
                    _entryType = FileSystemEntryType.CustomText;
                }
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not determine entry type: {ex.Message}{Environment.NewLine}{ex}");
                _entryType = FileSystemEntryType.CustomText;
            }
        }
        #endregion
        #region private void SetPreProcessedProperty(int metadataId, string rawValue)
        /// <summary>
        /// Pipelines a raw metadata value through the element name replacement rules and stores it in the properties map.
        /// </summary>
        private void SetPreProcessedProperty(int metadataId, string rawValue)
        {
            stringProperties[metadataId] = Plugin.queryPreProcessor.PreProcess(rawValue, isFilter: false);
        }
        #endregion
        #region public string GetStringProperty(int metadataId)
        /// <summary>
        /// Retrieves pre-calculated or cached string properties (Name, Ext, Folder, Path, Attr, or Custom WDX).
        /// </summary>
        public string GetStringProperty(int metadataId)
        {
            if(!stringProperties.ContainsKey(metadataId)) {

                if(metadataId == MetadataConstants.Gui) {
                    SetPreProcessedProperty(MetadataConstants.Gui, String.Empty);
                }

                if(metadataId == MetadataConstants.Attr) {
                    SetPreProcessedProperty(MetadataConstants.Attr, GetFileAttributesString());
                }

                if(metadataId == MetadataConstants.Desc) {
                    SetPreProcessedProperty(MetadataConstants.Desc, GetComment());
                }

                if(metadataId == MetadataConstants.Content) {
                    string loadedContent = GetFileContent();
                    SetPreProcessedProperty(MetadataConstants.Content, loadedContent);

                    // Notify cache about memory footprint change
                    if(!string.IsNullOrEmpty(loadedContent)) {
                        EvaluationContextCache.NotifyContentLoaded(fullPath, loadedContent.Length * sizeof(char));
                    }
                }

                if(metadataId >= MetadataConstants.CustomWdx) {
                    string wdxValue = string.Empty;

                    if(entryType != FileSystemEntryType.CustomText && Plugin.config._wdxIdToDefinitionMappings.TryGetValue(metadataId, out WdxDefinition wdxDefinition)) {

                        if(wdxDefinition != null && wdxDefinition.wdxPluginInstance != null) {
                            // Fetch content value using full path (e.g., "d:\images\photo.jpg")
                            wdxValue = wdxDefinition.wdxPluginInstance.GetValue(fullPath, wdxDefinition.wdxFieldDefinition);
                        }
                    }

                    SetPreProcessedProperty(metadataId, wdxValue ?? string.Empty);
                }

            }

            return stringProperties.TryGetValue(metadataId, out string value) ? value : string.Empty;
        }
        #endregion
        #region Internal Cache Invalidation Helpers
        /// <summary>
        /// Safely drops large text contents from the internal property maps to clear memory footprints.
        /// </summary>
        internal void PurgeContentProperty()
        {
            if(stringProperties.ContainsKey(MetadataConstants.Content)) {
                stringProperties.Remove(MetadataConstants.Content);
            }
        }
        #endregion

        #region private string GetFileContent()
        /// <summary>
        /// Retrieves the file content up to maxBytesToRead. Internal helper for stringProperties caching.
        /// </summary>
        private string GetFileContent()
        {
            try {
                if(entryType != FileSystemEntryType.File) return string.Empty;

                long size = GetFileSize();
                if(size > Plugin.config.maxMbToReadForContent * 1024 * 1024) return string.Empty; // Skip too large files

                string fileContent = null;
                using(var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using(var reader = new StreamReader(stream)) {
                    char[] buffer = new char[Plugin.config.maxMbToReadForContent * 1024 * 1024];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    fileContent = new string(buffer, 0, read);
                }
                return fileContent;
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not get file content: {ex.Message}{Environment.NewLine}{ex}");

                // On access violation (e.g., file locked by system), flag as evaluated but empty
                return string.Empty;
            }
        }
        #endregion
        #region public long GetFileSize()
        /// <summary>
        /// Retrieves the file size in bytes. Cached after first hit. Returns 0 immediately if the entry is not a physical file.
        /// </summary>
        public long GetFileSize()
        {
            if(fileSize.HasValue) return fileSize.Value;

            if(entryType != FileSystemEntryType.File) {
                fileSize = 0;
                return fileSize.Value;
            }

            try {
                fileSize = new FileInfo(fullPath).Length;
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not get file size: {ex.Message}{Environment.NewLine}{ex}");

                fileSize = 0;
            }

            return fileSize.Value;
        }
        #endregion
        #region public DateTime GetLastModified()
        /// <summary>
        /// Retrieves the last modification timestamp. Cached after first hit. Returns MinValue for virtual custom text entries.
        /// </summary>
        public DateTime GetLastModified()
        {
            if(lastModified.HasValue) return lastModified.Value;

            if(entryType == FileSystemEntryType.CustomText) {
                lastModified = DateTime.MinValue;
                return lastModified.Value;
            }

            try {
                lastModified = File.GetLastWriteTime(fullPath);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not get last modified timestamp for file: {ex.Message}{Environment.NewLine}{ex}");

                lastModified = DateTime.MinValue;
            }

            return lastModified.Value;
        }
        #endregion
        #region private string GetFileAttributesString()
        /// <summary>
        /// Formats file attributes as an 8-character string: [f|d|i][r|-][a|-][h|-][s|-][c|-][e|-][l|-].
        /// </summary>
        private string GetFileAttributesString()
        {
            if(string.IsNullOrEmpty(fullPath) || entryType == FileSystemEntryType.CustomText) return "i-------";

            char typeChar = (entryType == FileSystemEntryType.Directory) ? 'd' : 'f';

            try {
                FileAttributes attributes = File.GetAttributes(fullPath);

                char r = (attributes & FileAttributes.ReadOnly) != 0 ? 'r' /*     ReadOnly     */ : '-';
                char a = (attributes & FileAttributes.Archive) != 0 ? 'a' /*      Archive      */ : '-';
                char h = (attributes & FileAttributes.Hidden) != 0 ? 'h' /*       Hidden       */ : '-';
                char s = (attributes & FileAttributes.System) != 0 ? 's' /*       System       */ : '-';
                char c = (attributes & FileAttributes.Compressed) != 0 ? 'c' /*   Compressed   */ : '-';
                char e = (attributes & FileAttributes.Encrypted) != 0 ? 'e' /*    Encrypted    */ : '-';
                char l = (attributes & FileAttributes.ReparsePoint) != 0 ? 'l' /* ReparsePoint */ : '-';

                return $"{typeChar}{r}{a}{h}{s}{c}{e}{l}";
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not get file attributes for file: {ex.Message}{Environment.NewLine}{ex}");

                // Return default fallback matching current entryType on I/O or access error
                return $"{typeChar}-------";
            }
        }
        #endregion
        #region private string GetComment()
        /// <summary>
        /// Solves descript.ion fetching internally using a single-folder sticky cache.
        /// </summary>
        private string GetComment()
        {
            if(string.IsNullOrEmpty(fullPath) || entryType == FileSystemEntryType.CustomText) return string.Empty;

            try {
                string parentDir = Path.GetDirectoryName(fullPath);
                if(string.IsNullOrEmpty(parentDir)) return string.Empty;

                string targetName = Path.GetFileName(fullPath);
                if(string.IsNullOrEmpty(targetName)) return string.Empty;

                // If we are still processing the same directory, reuse the cached dictionary instantly, otherwise load it
                if(descriptionCacheFolder == null || descriptionCacheFolder != parentDir) {
                    LoadAllComments(parentDir);
                }

                return descriptionCache.TryGetValue(targetName, out string description) ? description : string.Empty;
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not get comment for file: {ex.Message}{Environment.NewLine}{ex}");

                return string.Empty;
            }
        }
        #endregion
        #region private void LoadAllComments(string directoryPath)
        /// <summary>
        /// Reads and parses the descript.ion file from the specified directory path directly into the internal dictionary.
        /// </summary>
        private void LoadAllComments(string directoryPath)
        {
            descriptionCache.Clear();
            descriptionCacheFolder = directoryPath;

            string descriptionFilePath = Path.Combine(directoryPath, "descript.ion");
            if(!File.Exists(descriptionFilePath)) return;

            try {
                byte[] fileBytes = File.ReadAllBytes(descriptionFilePath);
                if(fileBytes.Length == 0) return;

                // 1. Resolve Encoding via BOM
                System.Text.Encoding encoding = System.Text.Encoding.Default; // Fallback to ANSI (system locale)
                int bomOffset = 0;

                if(fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF) {
                    encoding = System.Text.Encoding.UTF8;
                    bomOffset = 3;
                } else if(fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE) {
                    encoding = System.Text.Encoding.Unicode; // UTF-16 LE
                    bomOffset = 2;
                } else if(fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF) {
                    encoding = System.Text.Encoding.BigEndianUnicode; // UTF-16 BE
                    bomOffset = 2;
                }

                // 2. Decode string content
                string content = encoding.GetString(fileBytes, bomOffset, fileBytes.Length - bomOffset);

                // Normalize line endings to map parsing safely
                content = content.Replace("\r\n", "\n").Replace("\r", "\n");
                string[] lines = content.Split(new[] { '\n' }, StringSplitOptions.None);

                foreach(string line in lines) {
                    if(string.IsNullOrEmpty(line)) continue;

                    string filename = string.Empty;
                    string remainder = string.Empty;

                    if(line.StartsWith("\"")) {
                        // 3.1 Parse filename: "file name.docx"
                        int nextQuoteIndex = line.IndexOf('"', 1);
                        if(nextQuoteIndex == -1) continue;

                        filename = line.Substring(1, nextQuoteIndex - 1);

                        // TC standard: The character directly after the closing quote is the delimiter space
                        remainder = line.Substring(nextQuoteIndex + 2);
                    } else {
                        // 3.1 Parse filename: filename.docx
                        int firstSpace = line.IndexOf(' ');
                        if(firstSpace == -1) continue;

                        filename = line.Substring(0, firstSpace);
                        remainder = line.Substring(firstSpace + 1);
                    }

                    if(string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(remainder)) continue;

                    // 4. Handle Multiline End Markers (0x04 0xC2)
                    bool isMultiline = remainder.EndsWith("\x04\xC2");

                    if(isMultiline) {
                        // Strip the 2-byte TC extension marker from the end
                        remainder = remainder.Substring(0, remainder.Length - 2);

                        // Robust sequential parsing loop to resolve only valid character escaping sequences
                        System.Text.StringBuilder sb = new System.Text.StringBuilder(remainder.Length);
                        for(int i = 0; i < remainder.Length; i++) {
                            if(remainder[i] == '\\' && i + 1 < remainder.Length) {
                                char next = remainder[i + 1];
                                if(next == 'n') {
                                    sb.Append("\r\n");
                                    i++; // Skip the 'n'
                                } else if(next == '\\') {
                                    sb.Append('\\');
                                    i++; // Skip the escaped backslash
                                } else {
                                    sb.Append('\\'); // Literal backslash if followed by an arbitrary character
                                }
                            } else {
                                sb.Append(remainder[i]);
                            }
                        }
                        remainder = sb.ToString();
                    }

                    descriptionCache[filename] = remainder;
                }
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not parse all comments from file: {ex.Message}{Environment.NewLine}{ex}");

                // Fail-safe clear if I/O faults happen
                descriptionCache.Clear();
            }
        }
        #endregion
    }
    #endregion
    #region EvaluationContextCache Class
    /// <summary>
    /// Thread-safe in-memory cache for FileSystemEvaluationContext elements.
    /// Purely state-driven cache lifecycle that works flawlessly across physical paths and virtual TC lists.
    /// </summary>
    public static class EvaluationContextCache
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, CacheEntry> _cacheMap = new Dictionary<string, CacheEntry>();

        // Metadata tracking queue (bounded by maxMetadataObjectCacheCount)
        private static readonly LinkedList<CacheEntry> _metadataLruList = new LinkedList<CacheEntry>();

        // Dedicated content tracking queue (only contains entries with active string allocations)
        private static readonly LinkedList<CacheEntry> _contentLruList = new LinkedList<CacheEntry>();

        private static bool _isFirstFileInSequence = false;
        private static long _currentContentBytes = 0;

        #region Constructor
        private class CacheEntry
        {
            public string path;
            public FileSystemEvaluationContext context;
            public long contentBytesAllocated;

            // Explicit node anchors to allow O(1) removal from both lists simultaneously
            public LinkedListNode<CacheEntry> metadataNode;
            public LinkedListNode<CacheEntry> contentNode;
        }
        #endregion

        #region public static void InitializeSequence()
        /// <summary>
        /// Signals the start of a brand new filter sequence, arming the first-file detection block.
        /// </summary>
        public static void InitializeSequence()
        {
            lock(_lock) {
                if(Plugin.config.metadataCacheStrategy == CacheStrategy.Disabled) {
                    Clear();
                    return;
                }

                _isFirstFileInSequence = true;
            }
        }
        #endregion
        #region public static FileSystemEvaluationContext GetOrCreate(string path, FileSystemEntryType? forcedType)
        /// <summary>
        /// Retrieves an existing context or creates, caches, and prunes a new one.
        /// Automatically detects list/directory switches on the very first evaluation call.
        /// </summary>
        public static FileSystemEvaluationContext GetOrCreate(string path, FileSystemEntryType? forcedType)
        {
            lock(_lock) {
                var strategy = Plugin.config.metadataCacheStrategy;

                if(strategy == CacheStrategy.Disabled) {
                    return new FileSystemEvaluationContext(path, forcedType);
                }

                // 1. Sequence-Start Validation (Only relevant on the very first file of a new filter)
                if(_isFirstFileInSequence) {
                    _isFirstFileInSequence = false;

                    if(strategy == CacheStrategy.LastDirectoryOnly) {
                        // If the first file of the new sequence is missing, we transitioned to a new directory/list
                        if(!_cacheMap.ContainsKey(path)) {
                            Clear();
                        }
                    }
                }

                // 2. Cache Hit
                if(_cacheMap.TryGetValue(path, out var entry)) {
                    _metadataLruList.Remove(entry.metadataNode);
                    _metadataLruList.AddFirst(entry.metadataNode); // Refresh metadata hotness
                    return entry.context;
                }

                // 3. Cache Miss
                var context = new FileSystemEvaluationContext(path, forcedType);

                var newEntry = new CacheEntry {
                    path = path,
                    context = context,
                    contentBytesAllocated = 0
                };

                newEntry.metadataNode = new LinkedListNode<CacheEntry>(newEntry);
                _metadataLruList.AddFirst(newEntry.metadataNode);
                _cacheMap[path] = newEntry;

                // Hard Limit Safety Belt: Active for BOTH SpecificCount and LastDirectoryOnly to protect system memory
                if(_cacheMap.Count > Plugin.config.maxMetadataObjectCacheCount) {
                    var oldest = _metadataLruList.Last;
                    if(oldest != null) {
                        CacheEntry oldestEntry = oldest.Value;

                        // Evict completely from dictionary and both lists
                        _cacheMap.Remove(oldestEntry.path);
                        _metadataLruList.RemoveLast();

                        if(oldestEntry.contentNode != null) {
                            _contentLruList.Remove(oldestEntry.contentNode);
                            _currentContentBytes -= oldestEntry.contentBytesAllocated;
                        }
                    }
                }

                return context;
            }
        }
        #endregion
        #region public static void NotifyContentLoaded(string path, int byteCount)
        /// <summary>
        /// Tracks and enforces the max allowed memory allocation for file content strings.
        /// </summary>
        public static void NotifyContentLoaded(string path, int byteCount)
        {
            lock(_lock) {
                if(!_cacheMap.TryGetValue(path, out var entry)) return;

                _currentContentBytes -= entry.contentBytesAllocated;
                entry.contentBytesAllocated = byteCount;
                _currentContentBytes += byteCount;

                // Update content list presence
                if(entry.contentNode != null) {
                    _contentLruList.Remove(entry.contentNode);
                    entry.contentNode = null;
                }

                if(byteCount > 0) {
                    entry.contentNode = new LinkedListNode<CacheEntry>(entry);
                    _contentLruList.AddFirst(entry.contentNode); // Add to the hot end of content
                }

                long maxAllowedBytes = (long)Plugin.config.maxContentCacheMb * 1024 * 1024;

                // Pruning Loop: Now instantly hits entries with active payload! No empty iterations.
                while(_currentContentBytes > maxAllowedBytes && _contentLruList.Last != null) {
                    var oldestContentNode = _contentLruList.Last;
                    CacheEntry targetEntry = oldestContentNode.Value;

                    targetEntry.context.PurgeContentProperty();
                    _currentContentBytes -= targetEntry.contentBytesAllocated;
                    targetEntry.contentBytesAllocated = 0;

                    targetEntry.contentNode = null;
                    _contentLruList.RemoveLast();
                }
            }
        }
        #endregion
        #region public static void Clear()
        public static void Clear()
        {
            _cacheMap.Clear();
            _metadataLruList.Clear();
            _contentLruList.Clear();
            _currentContentBytes = 0;
            _isFirstFileInSequence = false;
        }
        #endregion
    }
    #endregion

    // WDX
    #region WDX Structures and Constants
    public static class WdxConstants
    {
        // for ContentGetSupportedField
        public const int ft_nomorefields = 0;
        public const int ft_numeric_32 = 1;
        public const int ft_numeric_64 = 2;
        public const int ft_numeric_floating = 3;
        public const int ft_date = 4;
        public const int ft_time = 5;
        public const int ft_boolean = 6;
        public const int ft_multiplechoice = 7;
        public const int ft_string = 8;
        public const int ft_fulltext = 9;
        public const int ft_datetime = 10;
        public const int ft_stringw = 11;
        public const int ft_fulltextw = 12;
        public const int ft_comparecontent = 100;

        // for ContentSendStateInformation
        public const int contst_readnewdir = 1;     // It is called when TC reads one of the file lists.
        public const int contst_refreshpressed = 2; // The user has pressed F2 or Ctrl+R to force a reload.
        public const int contst_showhint = 4;       // A tooltip/hint window is shown for the current file.

        // for ContentGetValue
        public const int ft_nosuchfield = -1;  // error, invalid field number given
        public const int ft_fileerror = -2;    // file i/o error
        public const int ft_fieldempty = -3;   // field valid, but empty
        public const int ft_ondemand = -4;     // field will be retrieved only when user presses <SPACEBAR>
        public const int ft_notsupported = -5; // function not supported
        public const int ft_delayed = 0;       // field takes a long time to extract -> try again in background
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DefaultParamStruct
    {
        public int size;
        public int pluginInterfaceVersionLow;
        public int pluginInterfaceVersionHi;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string defaultIniName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WdxDate
    {
        public short year;
        public byte month;
        public byte day;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WdxTime
    {
        public byte hour;
        public byte minute;
        public byte second;
    }
    #endregion
    #region WDX Function Delegates
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void T_ContentSetDefaultParams(ref DefaultParamStruct dps);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int T_ContentGetSupportedField(int FieldIndex, StringBuilder FieldName, StringBuilder Units, int maxlen);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int T_ContentGetValue(string FileName, int FieldIndex, int UnitIndex, IntPtr FieldValue, int maxlen, int flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate int T_ContentGetValueW(string FileName, int FieldIndex, int UnitIndex, IntPtr FieldValue, int maxlen, int flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate void T_ContentSendStateInformation(int state, string path);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate void T_ContentSendStateInformationW(int state, string path);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void T_ContentPluginUnloading();
    #endregion
    #region WdxFieldDefinition Class
    public class WdxFieldDefinition
    {
        public string name { get; set; }

        public int dataType { get; set; }

        public int fieldIndex { get; set; }

        public int unitIndex { get; set; }

        public List<string> multipleChoiceValues { get; set; } = new List<string>();
    }
    #endregion
    #region WdxPluginInstance Class
    /// <summary> https://ghisler.github.io/WDX-SDK/contents.htm </summary>
    public class WdxPluginInstance : IDisposable
    {
        #region Native Windows API Imports
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        #endregion

        private IntPtr wdxPluginDll = IntPtr.Zero;
        private string lastDirectory = string.Empty;
        private const int bufferSizeContentGetSupportedField = 64 * 1024;
        private const int bufferSizeContentGetValue = 64 * 1024;
        private static readonly byte[] zeroes = new byte[bufferSizeContentGetValue];

        // Delegates
        private T_ContentSetDefaultParams _contentSetDefaultParams;
        private T_ContentGetSupportedField _contentGetSupportedField;
        private T_ContentSendStateInformation _contentSendStateInformation;
        private T_ContentSendStateInformationW _contentSendStateInformationW;
        private T_ContentGetValue _contentGetValue;
        private T_ContentGetValueW _contentGetValueW;
        private T_ContentPluginUnloading _contentPluginUnloading;

        public string pluginPath { get; private set; }
        public string pluginName { get; private set; }
        public string pluginIniPath { get; private set; }
        public string loadPluginError { get; private set; }
        public List<WdxFieldDefinition> wdxFieldDefinitions { get; private set; } = new List<WdxFieldDefinition>();
        public Dictionary<string, WdxFieldDefinition> wdxFieldDefinitionLookup { get; private set; } = new Dictionary<string, WdxFieldDefinition>();

        #region Constructor
        public WdxPluginInstance(string pluginPath, string pluginIniPath)
        {
            this.pluginPath = pluginPath;
            this.pluginIniPath = pluginIniPath;

            // Extract the plugin file name without extension (e.g. "exif.wdx" or "exif.wdx64" -> "exif"; "ShellDetails.uwdx" or "ShellDetails.wdx" -> "ShellDetails")
            pluginName = Path.GetFileNameWithoutExtension(pluginPath);

            LoadPlugin();
        }
        #endregion
        #region private void LoadPlugin()
        private void LoadPlugin()
        {
            Plugin.Log(LoggingLevel.Level_2_Startup, $"WDX plugin {pluginName} - LoadPlugin(pluginPath = \"{pluginPath}\", pluginIniPath = \"{pluginIniPath}\")");

            string pluginPathResolved = Plugin.ResolveFolderPath(pluginPath);
            string pluginIniPathResolved = string.IsNullOrEmpty(pluginIniPath) ? pluginIniPath : Plugin.ResolveFolderPath(pluginIniPath);

            if(!File.Exists(pluginPathResolved)) {
                loadPluginError = string.Format(Plugin.config.L[001_002_407] /* WDX plugin {0} - Error: File not found: pluginPath = "{1}" */, pluginName, pluginPathResolved);
                Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                return;
            }

            wdxPluginDll = LoadLibrary(pluginPathResolved);
            if(wdxPluginDll == IntPtr.Zero) {
                loadPluginError = string.Format(Plugin.config.L[001_002_408] /* WDX plugin {0} - Error: LoadLibrary(pluginPath = "{1}") => failed - Error code: {2} */, pluginName, pluginPathResolved, Marshal.GetLastWin32Error());
                Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                return;
            }
            Plugin.Log(LoggingLevel.Level_2_Startup, $"WDX plugin {pluginName} - LoadLibrary(pluginPath = \"{pluginPathResolved}\") => successful");

            // Bind compulsory and optional functions
            _contentSetDefaultParams = GetFuncDelegate<T_ContentSetDefaultParams>("ContentSetDefaultParams");
            _contentGetSupportedField = GetFuncDelegate<T_ContentGetSupportedField>("ContentGetSupportedField");
            _contentSendStateInformation = GetFuncDelegate<T_ContentSendStateInformation>("ContentSendStateInformation");
            _contentSendStateInformationW = GetFuncDelegate<T_ContentSendStateInformationW>("ContentSendStateInformationW");
            _contentGetValue = GetFuncDelegate<T_ContentGetValue>("ContentGetValue");
            _contentGetValueW = GetFuncDelegate<T_ContentGetValueW>("ContentGetValueW");
            _contentPluginUnloading = GetFuncDelegate<T_ContentPluginUnloading>("ContentPluginUnloading");

            if(_contentGetSupportedField == null) {
                loadPluginError = string.Format(Plugin.config.L[001_002_409] /* WDX plugin {0} - Error: Function "ContentGetSupportedField" not found: pluginPath = "{1}" */, pluginName, pluginPathResolved);
                Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                return;
            }
            if(_contentGetValueW == null && _contentGetValue == null) {
                loadPluginError = string.Format(Plugin.config.L[001_002_410] /* WDX plugin {0} - Error: Function "ContentGetValueW" (or "ContentGetValue") not found: pluginPath = "{1}" */, pluginName, pluginPathResolved);
                Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                return;
            }

            // Initialize Plugin with default INI path setup
            #region ContentSetDefaultParams
            if(_contentSetDefaultParams != null) {
                string finalIniPath = pluginIniPathResolved;

                if(string.IsNullOrEmpty(finalIniPath)) {
                    string targetFolder = Path.Combine(Plugin.dataFolder, "WdxIniFiles");
                    finalIniPath = Path.Combine(targetFolder, pluginName + ".ini");
                    try {
                        Directory.CreateDirectory(targetFolder);
                    } catch(Exception ex) {
                        loadPluginError = string.Format(Plugin.config.L[001_002_411] /* WDX plugin {0} - Error: CreateDirectory("{1}") (for "{2}") => failed - {3} */, pluginName, targetFolder, finalIniPath, ex.Message);
                        Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                        return;
                    }
                }

                var dps = new DefaultParamStruct();
                dps.size = Marshal.SizeOf(dps);
                dps.pluginInterfaceVersionHi = 2;
                dps.pluginInterfaceVersionLow = 12;
                dps.defaultIniName = finalIniPath;
                try {
                    _contentSetDefaultParams(ref dps);
                    Plugin.Log(LoggingLevel.Level_2_Startup, $"WDX plugin {pluginName} - ContentSetDefaultParams(pluginInterfaceVersion = {dps.pluginInterfaceVersionHi}.{dps.pluginInterfaceVersionLow}, defaultIniName = {dps.defaultIniName}) => successful");
                } catch(Exception ex) {
                    loadPluginError = string.Format(Plugin.config.L[001_002_412] /* WDX plugin {0} - Error: ContentSetDefaultParams(pluginInterfaceVersion = {1}.{2}, defaultIniName = {3}) => failed - {4} */, pluginName, dps.pluginInterfaceVersionHi, dps.pluginInterfaceVersionLow, dps.defaultIniName, ex.Message);
                    Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                    return;
                }
            }
            #endregion

            // Query all supported fields
            #region ContentGetSupportedField
            if(_contentGetSupportedField != null) {
                wdxFieldDefinitions.Clear();
                wdxFieldDefinitionLookup.Clear();

                var sbFieldName = new StringBuilder(bufferSizeContentGetSupportedField);
                var sbUnits = new StringBuilder(bufferSizeContentGetSupportedField);

                for(int fieldIndex = 0; ; fieldIndex++) {
                    sbFieldName.Clear();
                    sbUnits.Clear();
                    int result = 1983;

                    try {
                        result = _contentGetSupportedField(fieldIndex, sbFieldName, sbUnits, bufferSizeContentGetSupportedField);
                        Plugin.Log(LoggingLevel.Level_2_Startup, $"WDX plugin {pluginName} - ContentGetSupportedField(fieldIndex = {fieldIndex}, fieldName = {sbFieldName}, units = {sbUnits}, maxLen = {bufferSizeContentGetSupportedField}) => successful - result: {result}");
                    } catch(Exception ex) {
                        loadPluginError = string.Format(Plugin.config.L[001_002_413] /* WDX plugin {0} - Error: ContentGetSupportedField(fieldIndex = {1}, fieldName = {2}, units = {3}, maxLen = {4}) => failed - result: {5} - {6} */, pluginName, fieldIndex, sbFieldName, sbUnits, bufferSizeContentGetSupportedField, result, ex.Message);
                        Plugin.Log(LoggingLevel.Level_1_Errors, loadPluginError);
                        return;
                    }

                    if(result == WdxConstants.ft_nomorefields) break;
                    if(result < WdxConstants.ft_numeric_32 || result > WdxConstants.ft_fulltextw) continue;


                    string fieldName = sbFieldName.ToString();
                    string units = sbUnits.ToString();
                    List<string> unitList = string.IsNullOrEmpty(units) ? new List<string>() : units.Split('|').ToList();

                    if(result == WdxConstants.ft_multiplechoice /* Units contain multiple choice values */ ||
                        result == WdxConstants.ft_fulltext /* When calling ContentGetValue the unit contains the offset */ || result == WdxConstants.ft_fulltextw /* Same as ft_fulltext */ ||
                        unitList.Count == 0) {

                        // Ensure unique name in case it's already taken
                        string uniqueName = fieldName;
                        int suffix = 2;
                        while(wdxFieldDefinitionLookup.ContainsKey(uniqueName)) {
                            uniqueName = $"{fieldName} ({suffix})";
                            suffix++;
                        }

                        WdxFieldDefinition wdxFieldDefinition = new WdxFieldDefinition() { name = uniqueName, dataType = result, fieldIndex = fieldIndex, unitIndex = 0 };
                        if(result == WdxConstants.ft_multiplechoice) wdxFieldDefinition.multipleChoiceValues = unitList;

                        wdxFieldDefinitions.Add(wdxFieldDefinition);
                        wdxFieldDefinitionLookup.Add(wdxFieldDefinition.name, wdxFieldDefinition);
                    } else {
                        for(int unitIndex = 0; unitIndex < unitList.Count; unitIndex++) {
                            // Ensure unique name in case it's already taken
                            string uniqueName = $"{fieldName} - {unitList[unitIndex]}";
                            int suffix = 2;
                            while(wdxFieldDefinitionLookup.ContainsKey(uniqueName)) {
                                uniqueName = $"{fieldName} - {unitList[unitIndex]} ({suffix})";
                                suffix++;
                            }

                            WdxFieldDefinition wdxFieldDefinition = new WdxFieldDefinition() { name = uniqueName, dataType = result, fieldIndex = fieldIndex, unitIndex = unitIndex };

                            wdxFieldDefinitions.Add(wdxFieldDefinition);
                            wdxFieldDefinitionLookup.Add(wdxFieldDefinition.name, wdxFieldDefinition);
                        }
                    }
                }
            }
            #endregion
        }
        #endregion
        #region private T GetFuncDelegate<T>(string procName) where T : Delegate
        private T GetFuncDelegate<T>(string procName) where T : Delegate
        {
            IntPtr pFunc = GetProcAddress(wdxPluginDll, procName);
            if(pFunc == IntPtr.Zero) return null;
            return (T)Marshal.GetDelegateForFunctionPointer(pFunc, typeof(T));
        }
        #endregion
        #region public string GetValue(string fullFilePath, WdxFieldDefinition wdxFieldDefinition)
        public string GetValue(string filename, WdxFieldDefinition wdxFieldDefinition)
        {
            if(loadPluginError != null) return string.Empty;
            int fieldIndex = wdxFieldDefinition.fieldIndex;
            int unitIndex = wdxFieldDefinition.unitIndex;

            // Handle directory change
            #region ContentSendStateInformation
            string currentDirectory = Path.GetDirectoryName(filename);
            if(currentDirectory != lastDirectory) {
                lastDirectory = currentDirectory;
                if(_contentSendStateInformationW != null) {
                    try {
                        _contentSendStateInformationW(WdxConstants.contst_readnewdir, currentDirectory);
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} - ContentSendStateInformationW(state = \"{WdxConstants.contst_readnewdir}\", currentDirectory = \"{currentDirectory}\") => successful");
                    } catch(Exception ex) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentSendStateInformationW(state = \"{WdxConstants.contst_readnewdir}\", currentDirectory = \"{currentDirectory}\") => failed - {ex.Message}");
                        return string.Empty;
                    }
                } else if(_contentSendStateInformation != null) {
                    try {
                        _contentSendStateInformation(WdxConstants.contst_readnewdir, currentDirectory);
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} - ContentSendStateInformation(state = \"{WdxConstants.contst_readnewdir}\", currentDirectory = \"{currentDirectory}\") => successful");
                    } catch(Exception ex) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentSendStateInformation(state = \"{WdxConstants.contst_readnewdir}\", currentDirectory = \"{currentDirectory}\") => failed - {ex.Message}");
                        return string.Empty;
                    }
                }
            }
            #endregion

            IntPtr bufferContentGetValue = Marshal.AllocHGlobal(bufferSizeContentGetValue);
            StringBuilder fulltextStringBuilder = null;

            try {
                while(true) {
                    // Clear allocation site memory block safely before every call
                    Marshal.Copy(zeroes, 0, bufferContentGetValue, bufferSizeContentGetValue);
                    int result = 0;

                    // Get value for current file
                    #region ContentGetValue
                    if(_contentGetValueW != null) {
                        try {
                            result = _contentGetValueW(filename, fieldIndex, unitIndex, bufferContentGetValue, bufferSizeContentGetValue, 0);
                            if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} - ContentGetValueW(filename = \"{filename}\", fieldIndex = {fieldIndex}, unitIndex = {unitIndex}, fieldvalue = \"...\", maxLen = {bufferSizeContentGetValue}, flags = 0) => successful - result: {result}");
                        } catch(Exception ex) {
                            Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValueW(filename = \"{filename}\", fieldIndex = {fieldIndex}, unitIndex = {unitIndex}, fieldvalue = \"...\", maxLen = {bufferSizeContentGetValue}, flags = 0) => failed - result: {result} - {ex.Message}");
                            return string.Empty;
                        }
                    } else if(_contentGetValue != null) {
                        try {
                            result = _contentGetValue(filename, fieldIndex, unitIndex, bufferContentGetValue, bufferSizeContentGetValue, 0);
                            if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} - ContentGetValue(filename = \"{filename}\", fieldIndex = {fieldIndex}, unitIndex = {unitIndex}, fieldvalue = \"...\", maxLen = {bufferSizeContentGetValue}, flags = 0) => successful - result: {result}");
                        } catch(Exception ex) {
                            Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue(filename = \"{filename}\", fieldIndex = {fieldIndex}, unitIndex = {unitIndex}, fieldvalue = \"...\", maxLen = {bufferSizeContentGetValue}, flags = 0) => failed - result: {result} - {ex.Message}");
                            return string.Empty;
                        }
                    } else {
                        Plugin.Log(LoggingLevel.Level_1_Errors, "WDX plugin {pluginName} - Error: Function \"ContentGetValueW\" (or \"ContentGetValue\") not found");
                        return string.Empty;
                    }
                    #endregion

                    // strings
                    #region ft_stringw or ft_string
                    if(result == WdxConstants.ft_stringw) {
                        string value = Marshal.PtrToStringUni(bufferContentGetValue);
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_stringw)");
                        return value;
                    }
                    if(result == WdxConstants.ft_string) {
                        string value = Marshal.PtrToStringAnsi(bufferContentGetValue);
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_string)");
                        return value;
                    }
                    #endregion
                    #region ft_multiplechoice
                    if(result == WdxConstants.ft_multiplechoice) {
                        string value = Marshal.PtrToStringAnsi(bufferContentGetValue);

                        if(wdxFieldDefinition.multipleChoiceValues != null && wdxFieldDefinition.multipleChoiceValues.Count > 0) {
                            int choiceIndex = -1;

                            // Try parsing as string index first (most plugins return a string like "0", "1")
                            if(int.TryParse(value, out int parsedIndex)) {
                                choiceIndex = parsedIndex;
                            } else {
                                // Fallback: some plugins might write it as a direct 32-bit integer into the buffer
                                choiceIndex = Marshal.ReadInt32(bufferContentGetValue);
                            }

                            if(choiceIndex >= 0 && choiceIndex < wdxFieldDefinition.multipleChoiceValues.Count) {
                                value = wdxFieldDefinition.multipleChoiceValues[choiceIndex];
                            }
                        }

                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_multiplechoice)");
                        return value;
                    }
                    #endregion

                    // numbers
                    #region ft_boolean
                    if(result == WdxConstants.ft_boolean) {
                        string value = (Marshal.ReadInt32(bufferContentGetValue) != 0) ? "1" : "0";
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_boolean)");
                        return value;
                    }
                    #endregion
                    #region ft_numeric_32
                    if(result == WdxConstants.ft_numeric_32) {
                        string value = Marshal.ReadInt32(bufferContentGetValue).ToString();
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_numeric_32)");
                        return value;
                    }
                    #endregion
                    #region ft_numeric_64
                    if(result == WdxConstants.ft_numeric_64) {
                        string value = Marshal.ReadInt64(bufferContentGetValue).ToString();
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_numeric_64)");
                        return value;
                    }
                    #endregion
                    #region ft_numeric_floating
                    if(result == WdxConstants.ft_numeric_floating) {
                        double[] valueArray = new double[1];
                        Marshal.Copy(bufferContentGetValue, valueArray, 0, 1);
                        string value = valueArray[0].ToString("F3");
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_numeric_floating)");
                        return value;
                    }
                    #endregion

                    // date and time
                    #region ft_date
                    if(result == WdxConstants.ft_date) {
                        var date = (WdxDate)Marshal.PtrToStructure(bufferContentGetValue, typeof(WdxDate));
                        string value = $"{date.year:D4}-{date.month:D2}-{date.day:D2}";
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_date)");
                        return value;
                    }
                    #endregion
                    #region ft_time
                    if(result == WdxConstants.ft_time) {
                        var time = (WdxTime)Marshal.PtrToStructure(bufferContentGetValue, typeof(WdxTime));
                        string value = $"{time.hour:D2}:{time.minute:D2}:{time.second:D2}";
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_time)");
                        return value;
                    }
                    #endregion
                    #region ft_datetime
                    if(result == WdxConstants.ft_datetime) {
                        long fileTime = Marshal.ReadInt64(bufferContentGetValue);
                        try {
                            string value = DateTime.FromFileTime(fileTime).ToString("yyyy-MM-dd HH:mm:ss");
                            if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_datetime)");
                            return value;
                        } catch(Exception ex) {
                            Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: Parsing filetime {fileTime} failed - {ex.Message}");
                            return string.Empty;
                        }
                    }
                    #endregion

                    // text
                    #region ft_fulltext, ft_fulltextw and ft_fieldempty (end of ft_fulltext)
                    // Handle ANSI fulltext blocks
                    if(result == WdxConstants.ft_fulltext) {
                        if(fulltextStringBuilder == null) fulltextStringBuilder = new StringBuilder();
                        fulltextStringBuilder.Append(Marshal.PtrToStringAnsi(bufferContentGetValue));
                        unitIndex += bufferSizeContentGetValue - 1;
                        continue;
                    }

                    // Handle Unicode fulltext blocks (UTF-16 uses 2 bytes per character)
                    if(result == WdxConstants.ft_fulltextw) {
                        if(fulltextStringBuilder == null) fulltextStringBuilder = new StringBuilder();
                        fulltextStringBuilder.Append(Marshal.PtrToStringUni(bufferContentGetValue));
                        unitIndex += (bufferSizeContentGetValue / 2) - 1;
                        continue;
                    }

                    // Once there is no more data, the plugin needs to return ft_fieldempty.
                    if(result == WdxConstants.ft_fieldempty && fulltextStringBuilder != null) {
                        string value = fulltextStringBuilder.ToString();
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"{value}\" (ft_fieldempty after ft_fulltextw or ft_fulltext)");
                        return value;
                    }
                    #endregion

                    // field valid, but empty
                    #region ft_fieldempty
                    if(result == WdxConstants.ft_fieldempty) {
                        if(Plugin.IsLoggable(LoggingLevel.Level_4_FileEvaluation)) Plugin.Log($"WDX plugin {pluginName} -  > returned fieldvalue is \"\" (ft_fieldempty)");
                        return string.Empty;
                    }
                    #endregion

                    // valid error values
                    #region ft_nosuchfield
                    if(result == WdxConstants.ft_nosuchfield) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (ft_nosuchfield - The given fieldIndex {fieldIndex} is invalid)");
                        return string.Empty;
                    }
                    #endregion
                    #region ft_fileerror
                    if(result == WdxConstants.ft_fileerror) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (ft_fileerror - Error accessing the specified file - filename = \"{filename}\")");
                        return string.Empty;
                    }
                    #endregion
                    #region ft_notsupported
                    if(result == WdxConstants.ft_notsupported) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (ft_notsupported - Function not supported)");
                        return string.Empty;
                    }
                    #endregion

                    // invalid error values (QuickSearch eXtended 2 should not trigger this errors)
                    #region ft_ondemand
                    if(result == WdxConstants.ft_ondemand) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (ft_ondemand - The extraction of the field would take a very long time, so it should only be retrieved when the user presses the space bar. This error may only be returned if the flag CONTENT_DELAYIFSLOW was set, and if the plugin is thread - safe. QuickSearch eXtended 2 doesn't use the flag CONTENT_DELAYIFSLOW.)");
                        return string.Empty;
                    }
                    #endregion
                    #region ft_delayed
                    if(result == WdxConstants.ft_delayed) {
                        Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (ft_delayed - The extraction of the field would take a long time, so Total Commander should request it again in a background thread.This error may only be returned if the flag CONTENT_DELAYIFSLOW was set, and if the plugin is thread - safe. QuickSearch eXtended 2 doesn't use the flag CONTENT_DELAYIFSLOW.)");
                        return string.Empty;
                    }
                    #endregion

                    #region unknown result
                    Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentGetValue returned {result} (unknown result)");
                    return string.Empty;
                    #endregion
                }
            } finally {
                Marshal.FreeHGlobal(bufferContentGetValue);
            }
        }
        #endregion
        #region public void Dispose()
        public void Dispose()
        {
            if(wdxPluginDll == IntPtr.Zero) return;

            if(_contentPluginUnloading != null) {
                try {
                    _contentPluginUnloading();
                    Plugin.Log(LoggingLevel.Level_2_Startup, $"WDX plugin {pluginName} - ContentPluginUnloading() => successful");
                } catch(Exception ex) {
                    Plugin.Log(LoggingLevel.Level_1_Errors, $"WDX plugin {pluginName} - Error: ContentPluginUnloading() => failed - {ex.Message}");
                }
            }

            FreeLibrary(wdxPluginDll);
            wdxPluginDll = IntPtr.Zero;
        }

        #endregion
    }
    #endregion
    #region WdxPluginManager Class
    public static class WdxPluginManager
    {
        private static readonly Dictionary<string, WdxPluginInstance> _loadedPlugins = new Dictionary<string, WdxPluginInstance>();
        private static readonly object _lockObj = new object();

        #region public static WdxPluginInstance GetOrLoadPlugin(string pluginPath, string pluginIniPath)
        public static WdxPluginInstance GetOrLoadPlugin(string pluginPath, string pluginIniPath)
        {
            string lookupEntry = (pluginPath ?? "") + "\t" + (pluginIniPath ?? "");

            lock(_lockObj) {
                if(_loadedPlugins.TryGetValue(lookupEntry, out var existingInstance)) return existingInstance;

                var newInstance = new WdxPluginInstance(pluginPath, pluginIniPath);
                _loadedPlugins.Add(lookupEntry, newInstance);
                return newInstance;
            }
        }
        #endregion
        #region public static void UnloadAll()
        public static void UnloadAll()
        {
            lock(_lockObj) {
                foreach(var plugin in _loadedPlugins.Values) {
                    plugin.Dispose();
                }
                _loadedPlugins.Clear();
            }
        }
        #endregion
    }
    #endregion
    #region WdxPluginFieldsConverter Class
    public class WdxPluginFieldsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Unpack values based on XAML MultiBinding order
            string pluginPath = values.Length > 0 ? values[0] as string : string.Empty;
            string pluginIniPath = values.Length > 1 ? values[1] as string : string.Empty;
            bool is64Bit = values.Length > 2 && values[2] is bool bool1 && bool1;
            bool isActive = values.Length > 3 && values[3] is bool bool2 && bool2;
            string oldFieldAndUnit = values.Length > 4 ? values[4] as string : string.Empty;

            List<string> result = new List<string>();

            if(!string.IsNullOrEmpty(oldFieldAndUnit)) result.Add(oldFieldAndUnit);

            if(!isActive) {
                string entry = Plugin.config.L[001_002_415] /* Plugin ist deaktiviert (aus Performancegründen nicht geladen) */;

                if(!result.Contains(entry)) result.Add(entry);
                return result;
            }

            if(is64Bit != Environment.Is64BitProcess) {
                string entry = string.Format(Plugin.config.L[001_002_414] /* {0} plugin cannot be loaded into {1} QuickSearch eXtended 2 */,
                    is64Bit /*              */ ? Plugin.config.L[001_002_404] /* 64-bit */ : Plugin.config.L[001_002_403] /* 32-bit */,
                    Environment.Is64BitProcess ? Plugin.config.L[001_002_404] /* 64-bit */ : Plugin.config.L[001_002_403] /* 32-bit */);

                if(!result.Contains(entry)) result.Add(entry);
                return result;
            }

            WdxPluginInstance wdxPluginInstance = WdxPluginManager.GetOrLoadPlugin(pluginPath, pluginIniPath);
            if(wdxPluginInstance.loadPluginError != null) {
                if(!result.Contains(wdxPluginInstance.loadPluginError)) result.Add(wdxPluginInstance.loadPluginError);
                return result;
            }

            foreach(WdxFieldDefinition wdxFieldDefinition in wdxPluginInstance.wdxFieldDefinitions) {
                if(!result.Contains(wdxFieldDefinition.name)) result.Add(wdxFieldDefinition.name);
            }
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[targetTypes.Length];
        }
    }
    #endregion

    // GUI
    #region Win32 Native Methods
    internal static class NativeMethods
    {
        #region Window Styles & Handles
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        #endregion
        #region Process & Thread Tracking
        [DllImport("ucrtbase.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int _controlfp_s(ref uint currentControl, uint newControl, uint mask);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();
        #endregion
        #region Window Geometry & Monitors
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        #endregion
        #region Window Messages
        public const uint WM_SETTEXT = 0x000C;
        public const uint WM_GETTEXT = 0x000D;
        public const uint WM_GETTEXTLENGTH = 0x000E;
        public const uint EM_GETSEL = 0x00B0;
        public const uint EM_SETSEL = 0x00B1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, [In, Out] ref int wParam, [In, Out] ref int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
        #endregion
    }
    #endregion
    #region LocalizationConverter Class
    public class LocalizationConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if(values.Length == 2 && values[0] is int languageId) {
                if(values[1] is LanguageProvider languageProvider) return languageProvider[languageId];
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
    #region WindowHelper Class
    public static class WindowHelper
    {
        #region Helpers
        private static SearchAssistantWindow searchAssistantWindow;
        private static System.Windows.Threading.Dispatcher uiDispatcher;
        private static bool isInitializing = false;
        private static readonly object threadLock = new object();
        private static uint originalFpuState = 0;
        private const uint MCW_EM = 0x0008001F; // Mask all floating-point exceptions
        #endregion

        #region public static void AllowLargeNumbers(bool allow)
        /// <summary>
        /// Controls the hardware FPU (Floating Point Unit) exception masking to guarantee interoperability between the Delphi-based host (Total Commander) and the .NET CLR/JIT-compiler.
        /// 
        /// Layered plugin architecture:
        /// Total Commander (Delphi Host) → C++/CLI Wrapper Plugin → C#/.NET Core Plugin (tcmatch.Core)
        /// 
        /// In this execution chain, using double.MaxValue results in a StackOverflowException (0x800703E9) at runtime when executed through the full plugin pipeline.
        /// Even using a number larger than int.MaxValue (2147483647), for example "double c = 2147483648.0", triggers a StackOverflowException.
        /// 
        /// Christian Ghisler (Author of Total Commander) documents this architecture and the use of MCW_EM in the official forum:
        ///  > https://www.ghisler.ch/board/viewtopic.php?p=320834#p320834
        ///  > https://www.ghisler.ch/board/viewtopic.php?p=180775#p180775
        /// 
        /// The architectural conflict (Delphi vs. C++/C#):
        /// 
        /// Total Commander is compiled in Delphi. By default, Delphi configures the CPU's FPU Control Word to actively catch floating-point anomalies and raise hardware interrupts.
        /// Conversely, Microsoft Visual C++ and the .NET CLR mask these hardware exceptions entirely, choosing to handle anomalies silently.
        /// 
        /// Bit Layout:
        /// Host Default State: 0x800C001F -> [1000 0000 0000 1100 0000 0000 0001 1111]
        ///     MCW_EM Submask: 0x0008001F -> [0000 0000 0000 1000 0000 0000 0001 1111]
        /// Final Plugin State: 0x000C001F -> [0000 0000 0000 1100 0000 0000 0001 1111]
        /// </summary>
        /// <param name="allow">
        /// True: Masks all hardware floating-point exceptions (C++/CLI and C#/.NET standard behavior).
        ///       This allows the JIT-compiler to process doubles larger than MaxInt without breaking execution.
        /// False: Safely restores the exception flags back to the host's original configuration.
        /// </param>
        public static void AllowLargeNumbers(bool allow)
        {
            uint dummy = 0;
            if(allow) {
                if(originalFpuState == 0) {
                    // Read and cache the current FPU state from Total Commander before changing it
                    NativeMethods._controlfp_s(ref originalFpuState, 0, 0);
                }
                // Mask exceptions -> Allows processing of doubles larger than maxint without crashes
                NativeMethods._controlfp_s(ref dummy, MCW_EM, MCW_EM);
            } else {
                if(originalFpuState != 0) {
                    // Restore the exact FPU state we captured earlier
                    NativeMethods._controlfp_s(ref dummy, originalFpuState, MCW_EM);
                }
            }
        }
        #endregion

        #region public static void EnsureSearchAssistantRunning()
        /// <summary>
        /// Spawns or uses the existing UI thread to ensure the SearchAssistantWindow is open and cached.
        /// </summary>
        public static void EnsureSearchAssistantRunning()
        {
            lock(threadLock) {
                if(uiDispatcher == null && !isInitializing) {
                    isInitializing = true;

                    System.Threading.Thread windowThread = new System.Threading.Thread(() => {
                        try {
                            if(Application.Current == null) {
                                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                                Application.Current.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = ApplicationTheme.Light });
                                Application.Current.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                            }

                            uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                            searchAssistantWindow = new SearchAssistantWindow();

                            // Intercept the close signal and hide the window instead to keep it cached in memory
                            searchAssistantWindow.Closing += (s, ev) => {
                                ev.Cancel = true;
                                searchAssistantWindow.Hide();
                            };

                            searchAssistantWindow.IsVisibleChanged += (s, ev) => {
                                if(!searchAssistantWindow.IsVisible) {
                                    CloseAllContextMenus(searchAssistantWindow);
                                }
                            };

                            // Fallback cleanup: If the window is ever forcefully destroyed by the OS
                            searchAssistantWindow.Closed += (s, ev) => {
                                searchAssistantWindow = null;
                                uiDispatcher = null;
                            };

                            searchAssistantWindow.ShowWindow();
                            isInitializing = false;
                            System.Windows.Threading.Dispatcher.Run();
                        } catch(Exception ex) {
                            Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not show search assistant: {ex.Message}{Environment.NewLine}{ex}");

                            System.Windows.MessageBox.Show($"UI Thread Error: {ex}");
                            uiDispatcher = null;
                            isInitializing = false;
                        }
                    });
                    windowThread.SetApartmentState(System.Threading.ApartmentState.STA);
                    windowThread.IsBackground = true;
                    windowThread.Start();
                    return;
                }

                // If cached thread and window exist, instantly bring it back to the screen via the dispatcher
                uiDispatcher?.BeginInvoke(new Action(() => {
                    searchAssistantWindow?.ShowWindow();
                }));
            }
        }
        #endregion
        #region public static bool IsTotalCommanderQuicksearchWindow(IntPtr hWnd)
        /// <summary>
        /// Validates if the given window handle belongs to the Total Commander main user interface.
        /// </summary>
        public static bool IsTotalCommanderQuicksearchWindow(IntPtr hWnd)
        {
            if(hWnd == IntPtr.Zero) return false;

            // 256 characters is the maximum defined length for standard Win32 class names
            System.Text.StringBuilder classNameBuffer = new System.Text.StringBuilder(256);

            if(NativeMethods.GetClassName(hWnd, classNameBuffer, classNameBuffer.Capacity) > 0) {
                if(classNameBuffer.ToString() == "TQUICKSEARCH") {
                    return true;
                }
            }

            return false;
        }
        #endregion
        #region public static void LoadXAML(Window window, string resourceName)
        /// <summary>
        /// Loads an embedded XAML resource and binds it to the specified Window instance.
        /// </summary>
        public static void LoadXAML(Window window, string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using(Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                if(stream == null) throw new FileNotFoundException($"The embedded XAML resource '{resourceName}' could not be found.");

                try {
                    System.Xml.XmlReader xmlReader = System.Xml.XmlReader.Create(stream);
                    XamlSchemaContext schemaContext = System.Windows.Markup.XamlReader.GetWpfSchemaContext();
                    XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, schemaContext);
                    XamlObjectWriterSettings settings = new XamlObjectWriterSettings { RootObjectInstance = window };
                    XamlObjectWriter xamlObjectWriter = new XamlObjectWriter(schemaContext, settings);

                    while(xamlXmlReader.Read()) xamlObjectWriter.WriteNode(xamlXmlReader);
                } catch {
                    throw;
                }
            }
        }
        #endregion
        #region public static async Task<Wpf.Ui.Controls.MessageBoxResult> ShowThemedMessageBox(Window owner, string text, string title, Wpf.Ui.Controls.SymbolRegular symbol, string primaryButtonText, string closeButtonText)
        public static async Task<Wpf.Ui.Controls.MessageBoxResult> ShowThemedMessageBox(Window owner, string text, string title, Wpf.Ui.Controls.SymbolRegular symbol, string primaryButtonText, string closeButtonText)
        {
            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            var icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol, FontSize = 24, Margin = new System.Windows.Thickness(0, 0, 12, 0) };
            System.Windows.Controls.Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var textBlock = new Wpf.Ui.Controls.TextBlock { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center, TextWrapping = System.Windows.TextWrapping.Wrap };
            System.Windows.Controls.Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            var uiMessageBox = new Wpf.Ui.Controls.MessageBox {
                Title = title,
                Content = grid,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = owner,

                // WPF UI's MessageBox briefly shows a white frame when the default non-client window style is applied while opening the dialog in dark mode.
                // Setting WindowStyle.None prevents this initial white flicker.
                WindowStyle = WindowStyle.None
            };

            // Restore the normal window style shortly after the window has been initialized.
            // This brings back the native window shadow without reintroducing the visible white frame during startup in dark mode.
            uiMessageBox.Loaded += delegate {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += delegate { timer.Stop(); uiMessageBox.WindowStyle = WindowStyle.SingleBorderWindow; };
                timer.Start();
            };

            return await uiMessageBox.ShowDialogAsync();
        }
        #endregion
        #region public static void CloseAllContextMenus(DependencyObject parent)
        public static void CloseAllContextMenus(DependencyObject parent)
        {
            if(parent == null) return;

            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for(int i = 0; i < count; i++) {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if(child is FrameworkElement fe && fe.ContextMenu != null && fe.ContextMenu.IsOpen) {
                    fe.ContextMenu.IsOpen = false;
                }

                CloseAllContextMenus(child);
            }
        }
        #endregion
    }
    #endregion
    #region ConfigWindow Class
    public partial class ConfigWindow : Wpf.Ui.Controls.FluentWindow
    {
        private SearchAssistantWindow searchAssistantWindow;
        private Config config;
        private bool useraction = true;
        private bool isSaving = false;

        private bool _settingsTabActive = true;
        private double _settingsScrollOffset;
        private bool _documentationIsLoaded = false;
        private bool _documentationShowEnglishOverride = false;
        private bool _logFileIsLoaded = false;
        private bool _personalIsLoaded = false;
        private bool _personalShowEnglishOverride = false;
        private Microsoft.Web.WebView2.Core.CoreWebView2Environment _webViewEnvironment;

        // Basic actions
        #region public ConfigWindow()
        public ConfigWindow(SearchAssistantWindow searchAssistantWindow)
        {
            this.searchAssistantWindow = searchAssistantWindow;

            // Clone the global configuration to prevent direct manipulation before saving
            config = Plugin.ConfigDeepClone(Plugin.config);

            useraction = false;

            config.PopulateViewFields_ControlCharacters();
            config.PopulateViewFields_Language();
            config.PopulateViewFields_Theme();
            config.Validate_Gui();
            config.Validate_Other();
            config.Validate();

            DataContext = config;

            // Refresh the UI bindings
            searchAssistantWindow.DataContext = null;
            searchAssistantWindow.DataContext = config;
            searchAssistantWindow.config = config;

            WindowHelper.LoadXAML(this, "tcmatch.Core.ConfigWindow.xaml");
            config.PopulateViewFields_CurrentWindow_Theme(null, this);

            useraction = true;
        }
        #endregion
        #region private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
        private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if(Wpf.Ui.Controls.MessageBoxResult.Primary != await WindowHelper.ShowThemedMessageBox(this,
                config.L[001_002_005] /* Do you really want to reset all settings in this window to their default values? Unsaved changes will be lost. */,
                config.L[001_002_004] /* Load default values? */, Wpf.Ui.Controls.SymbolRegular.ArrowReset24, config.L[001_002_008] /* Yes */, config.L[001_002_009] /* No */)
                ) return;

            useraction = false;

            // Reset the cloned instance back to pristine factory defaults
            Config freshConfig = new Config();

            freshConfig.OnPostLoad();
            freshConfig.PopulateViewFields_ControlCharacters();
            freshConfig.PopulateViewFields_Language();
            freshConfig.PopulateViewFields_Theme();
            freshConfig.PopulateViewFields_CurrentWindow_Theme(searchAssistantWindow, this);
            freshConfig.Validate_Gui();
            freshConfig.Validate_Other();
            freshConfig.Validate();

            config = freshConfig;

            // Refresh the UI bindings
            DataContext = null;
            DataContext = config;
            searchAssistantWindow.DataContext = null;
            searchAssistantWindow.DataContext = config;
            searchAssistantWindow.config = config;

            useraction = true;
        }
        #endregion
        #region private void Cancel_Click(object sender, RoutedEventArgs e)
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(isSaving) return;

            useraction = false;

            config.PersistViewFields_ControlCharacters();

            useraction = true;

            // Check if modifications were made by comparing serialized XML strings
            string currentXml = Plugin.ConfigToString(config);
            string originalXml = Plugin.ConfigToString(Plugin.config);

            // Ask user if they really want to discard changes
            if(currentXml != originalXml && Wpf.Ui.Controls.MessageBoxResult.Primary != await WindowHelper.ShowThemedMessageBox(this,
                config.L[001_002_007] /* There are unsaved changes. Do you want to discard these changes? */,
                config.L[001_002_006] /* Discard changes? */, Wpf.Ui.Controls.SymbolRegular.Delete24, config.L[001_002_008] /* Yes */, config.L[001_002_009] /* No */)
                ) e.Cancel = true;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            searchAssistantWindow.DataContext = null;
            searchAssistantWindow.DataContext = Plugin.config;
            searchAssistantWindow.config = Plugin.config;
        }
        #endregion
        #region private void SaveButton_Click(object sender, RoutedEventArgs e)
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            useraction = false;

            config.PersistViewFields_ControlCharacters();

            // Overwrite global configuration with the modified clone and save to file
            Plugin.config = config;
            Plugin.SaveConfig();

            if(Plugin.config.usePinYinMatching) Plugin.LoadPinYinDatabase();
            Plugin.LoadReplacementsFile();
            config.PopulateInternalElements();
            EvaluationContextCache.Clear();
            WdxPluginManager.UnloadAll();

            foreach(WdxDefinition wdxDefinition in config.wdxDefinitions) {
                wdxDefinition.ResetPluginCache();
            }

            useraction = true;

            isSaving = true;
            this.Close();
        }
        #endregion

        // Settings: Changes and Buttons
        #region private async void LoadTranslationTemplate_Click(object sender, RoutedEventArgs e)
        private async void LoadTranslationTemplate_Click(object sender, RoutedEventArgs e)
        {
            if(!string.IsNullOrEmpty(config.customTranslationOverride) && Wpf.Ui.Controls.MessageBoxResult.Primary != await WindowHelper.ShowThemedMessageBox(this,
                config.L[002_002_007] /* Should the existing entries in the "Custom text adjustments" field be discarded and the default template of the current language be reloaded? */,
                config.L[002_002_006] /* Discard changes? */, Wpf.Ui.Controls.SymbolRegular.LocalLanguage24, config.L[001_002_008] /* Yes */, config.L[001_002_009] /* No */)
                ) return;

            useraction = false;

            config.CustomTranslationOverride_SetDefault();
            config.PopulateViewFields_Language();

            useraction = true;
        }
        #endregion
        #region private void Config_Language_Changed(object sender, RoutedEventArgs e)
        private void Config_Language_Changed(object sender, RoutedEventArgs e)
        {
            if(!useraction) return;

            config.PopulateViewFields_Language();
            ResetDocumentation();
            ResetLogFile();
            ResetPersonal();
        }
        private void Config_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config_Language_Changed(sender, e);
        }
        #endregion
        #region private async void LoadThemeTemplate_Click(object sender, RoutedEventArgs e)
        private async void LoadThemeTemplate_Click(object sender, RoutedEventArgs e)
        {
            if(!string.IsNullOrEmpty(config.customColorOverride) && Wpf.Ui.Controls.MessageBoxResult.Primary != await WindowHelper.ShowThemedMessageBox(this,
                config.L[002_002_104] /* Should the existing entries in the "Custom color adjustments" field be discarded and the default template of the current theme be reloaded? */,
                config.L[002_002_006] /* Discard changes? */, Wpf.Ui.Controls.SymbolRegular.PaintBrush24, config.L[001_002_008] /* Yes */, config.L[001_002_009] /* No */)
                ) return;

            useraction = false;

            config.CustomColorOverride_SetDefault();
            config.PopulateViewFields_Theme();
            config.PopulateViewFields_CurrentWindow_Theme(searchAssistantWindow, this);

            useraction = true;
        }
        #endregion
        #region private void Config_Theme_Changed(object sender, RoutedEventArgs e)
        private void Config_Theme_Changed(object sender, RoutedEventArgs e)
        {
            if(!useraction) return;
            config.PopulateViewFields_Theme();
            config.PopulateViewFields_CurrentWindow_Theme(searchAssistantWindow, this);
            ResetDocumentation();
            ResetPersonal();
        }
        private void Config_Theme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Config_Theme_Changed(sender, e);
        }
        #endregion
        #region private void Config_Gui_Changed(object sender, RoutedEventArgs e)
        private void Config_Gui_Changed(object sender, RoutedEventArgs e)
        {
            if(!useraction) return;

            // Live-update the parent assistant window using the current UI clone values
            bool isVisible = searchAssistantWindow.SetWindowPosition(
                Math.Max(config.guiSizeX, Config.guiSizeMinX),
                Math.Max(config.guiSizeY, Config.guiSizeMinY), config.guiOffsetX, config.guiOffsetY, config.guiDocking, config.guiDockingCorner);

            config.Validate_Gui(isVisible);
            config.Validate();
        }
        private void Config_Gui_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config_Gui_Changed(sender, e);
        }
        private void Config_Gui_TextChanged(object sender, TextChangedEventArgs e)
        {
            Config_Gui_Changed(sender, e);
        }
        private void Config_Gui_ValueChanged(object sender, Wpf.Ui.Controls.NumberBoxValueChangedEventArgs args)
        {
            Config_Gui_Changed(sender, null);
        }
        private void Config_Gui_LostFocus(object sender, RoutedEventArgs e)
        {
            Config_Gui_Changed(sender, null);

            // Workaround: Reset properties back to previously assigned values because NumberBox sometimes displays a different number on invalid input than what was set in the config => Therefore reset values when focus is lost
            Dispatcher.BeginInvoke(new Action(() => {
                config.OnPropertyChanged(nameof(config.guiOffsetX));
                config.OnPropertyChanged(nameof(config.guiOffsetY));
                config.OnPropertyChanged(nameof(config.guiSizeX));
                config.OnPropertyChanged(nameof(config.guiSizeY));
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        #endregion
        #region private void Config_Validate_Changed(object sender, RoutedEventArgs e)
        private void Config_Validate_Changed(object sender, RoutedEventArgs e)
        {
            if(!useraction) return;

            config.Validate_Other();
            config.Validate();
        }
        private void Config_Validate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config_Validate_Changed(sender, e);
        }
        private void Config_Validate_CellEditEndingChanged(object sender, DataGridCellEditEndingEventArgs e)
        {
            Config_Validate_Changed(sender, null);
        }
        private void Config_Validate_RowEditEndingChanged(object sender, DataGridRowEditEndingEventArgs e)
        {
            Config_Validate_Changed(sender, null);
        }
        #endregion
        #region private void Config_Changed(object sender, RoutedEventArgs e)
        private void Config_Changed(object sender, RoutedEventArgs e)
        {
            if(!useraction) return;

            config.Validate();
        }
        private void Config_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Config_Changed(sender, e);
        }
        #endregion

        #region private void GuiSections_MoveUp_Click(object sender, RoutedEventArgs e)
        private void GuiSections_MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("GuiSectionsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<GuiSection>()
                .OrderBy(item => config.guiSections.IndexOf(item))
                .ToList();

            // Find the lowest index among the selection. If it's 0, we can't move up at all.
            int minIndex = config.guiSections.IndexOf(selectedItems[0]);
            if(minIndex <= 0) return;

            int targetIndex = minIndex - 1;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.guiSections.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.guiSections.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[0]);
            grid.Focus();
        }
        #endregion
        #region private void GuiSections_MoveDown_Click(object sender, RoutedEventArgs e)
        private void GuiSections_MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("GuiSectionsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<GuiSection>()
                .OrderBy(item => config.guiSections.IndexOf(item))
                .ToList();

            // Find the highest index among the selection. If it's already the last element, we can't move down.
            int maxIndex = config.guiSections.IndexOf(selectedItems[selectedItems.Count - 1]);
            if(maxIndex >= config.guiSections.Count - 1) return;

            // Target position shifts depending on how many items we move, because the elements themselves shift indices
            int targetIndex = maxIndex + 2 - selectedItems.Count;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.guiSections.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.guiSections.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[selectedItems.Count - 1]);
            grid.Focus();
        }
        #endregion

        #region private void Wdx_Definitions_MoveUp_Click(object sender, RoutedEventArgs e)
        private void Wdx_Definitions_MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("WdxDefinitionsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<WdxDefinition>()
                .OrderBy(item => config.wdxDefinitions.IndexOf(item))
                .ToList();

            // Find the lowest index among the selection. If it's 0, we can't move up at all.
            int minIndex = config.wdxDefinitions.IndexOf(selectedItems[0]);
            if(minIndex <= 0) return;

            int targetIndex = minIndex - 1;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.wdxDefinitions.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.wdxDefinitions.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[0]);
            grid.Focus();
        }
        #endregion
        #region private void Wdx_Definitions_MoveDown_Click(object sender, RoutedEventArgs e)
        private void Wdx_Definitions_MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("WdxDefinitionsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<WdxDefinition>()
                .OrderBy(item => config.wdxDefinitions.IndexOf(item))
                .ToList();

            // Find the highest index among the selection. If it's already the last element, we can't move down.
            int maxIndex = config.wdxDefinitions.IndexOf(selectedItems[selectedItems.Count - 1]);
            if(maxIndex >= config.wdxDefinitions.Count - 1) return;

            // Target position shifts depending on how many items we move, because the elements themselves shift indices
            int targetIndex = maxIndex + 2 - selectedItems.Count;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.wdxDefinitions.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.wdxDefinitions.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[selectedItems.Count - 1]);
            grid.Focus();
        }
        #endregion
        #region private void Wdx_Definitions_AddDefinition_Click(object sender, RoutedEventArgs e)
        private void Wdx_Definitions_AddDefinition_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("WdxDefinitionsGrid") is DataGrid wdxDefinitionsGrid)) return;

            // Determine the position where the new definition should be inserted
            int insertIndex = config.wdxDefinitions.Count; // Default: At the end

            // If a row is selected, insert directly below it (+1)
            if(wdxDefinitionsGrid.SelectedIndex != -1) insertIndex = wdxDefinitionsGrid.SelectedIndex + 1;

            // Create the new definition object
            var newDefinition = new WdxDefinition(true, string.Empty, Environment.Is64BitProcess, string.Empty, string.Empty, string.Empty);

            // Insert the new definition at the calculated position in the list
            config.wdxDefinitions.Insert(insertIndex, newDefinition);

            // Refresh the grid
            wdxDefinitionsGrid.Items.Refresh();

            // Set focus to the newly inserted row
            wdxDefinitionsGrid.UnselectAll();
            wdxDefinitionsGrid.SelectedIndex = insertIndex;
            wdxDefinitionsGrid.ScrollIntoView(newDefinition);
            wdxDefinitionsGrid.Focus();

            config.Validate_Other();
            config.Validate();
        }
        #endregion
        #region private void Wdx_Definitions_RemoveDefinition_Click(object sender, RoutedEventArgs e)
        private void Wdx_Definitions_RemoveDefinition_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("WdxDefinitionsGrid") is DataGrid wdxDefinitionsGrid)) return;
            if(wdxDefinitionsGrid.SelectedItems == null || wdxDefinitionsGrid.SelectedItems.Count == 0) return;

            // Remember the lowest index of the current selection
            int minSelectedIndex = wdxDefinitionsGrid.SelectedItems
                .Cast<WdxDefinition>()
                .Select(definition => config.wdxDefinitions.IndexOf(definition))
                .Min();

            // Remove from the list
            var selectedDefinitions = wdxDefinitionsGrid.SelectedItems.Cast<WdxDefinition>().ToList();
            foreach(var definition in selectedDefinitions) {
                config.wdxDefinitions.Remove(definition);
            }

            // Update the UI so that the remaining rows are recalculated
            wdxDefinitionsGrid.Items.Refresh();

            // Determine the new index and select it
            if(config.wdxDefinitions.Count > 0) {

                int targetIndex = 0; // Default: At the beginning

                // Try to target the element directly before the deleted selection
                if(targetIndex < minSelectedIndex - 1) targetIndex = minSelectedIndex - 1;

                // Safety cap to prevent out-of-bounds errors
                if(targetIndex >= config.wdxDefinitions.Count) {
                    targetIndex = config.wdxDefinitions.Count - 1;
                }

                // Set focus
                wdxDefinitionsGrid.UnselectAll();
                wdxDefinitionsGrid.SelectedIndex = targetIndex;
                wdxDefinitionsGrid.ScrollIntoView(wdxDefinitionsGrid.SelectedItem);
                wdxDefinitionsGrid.Focus();
            }

            config.Validate_Other();
            config.Validate();
        }
        #endregion

        #region private void String_Replacements_MoveUp_Click(object sender, RoutedEventArgs e)
        private void String_Replacements_MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("ReplacementsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<ReplaceRule>()
                .OrderBy(item => config.stringReplacementsGui.IndexOf(item))
                .ToList();

            // Find the lowest index among the selection. If it's 0, we can't move up at all.
            int minIndex = config.stringReplacementsGui.IndexOf(selectedItems[0]);
            if(minIndex <= 0) return;

            int targetIndex = minIndex - 1;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.stringReplacementsGui.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.stringReplacementsGui.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[0]);
            grid.Focus();
        }
        #endregion
        #region private void String_Replacements_MoveDown_Click(object sender, RoutedEventArgs e)
        private void String_Replacements_MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("ReplacementsGrid") is DataGrid grid)) return;
            if(grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            // Get all selected items sorted by their current index to process them from top to bottom
            var selectedItems = grid.SelectedItems
                .Cast<ReplaceRule>()
                .OrderBy(item => config.stringReplacementsGui.IndexOf(item))
                .ToList();

            // Find the highest index among the selection. If it's already the last element, we can't move down.
            int maxIndex = config.stringReplacementsGui.IndexOf(selectedItems[selectedItems.Count - 1]);
            if(maxIndex >= config.stringReplacementsGui.Count - 1) return;

            // Target position shifts depending on how many items we move, because the elements themselves shift indices
            int targetIndex = maxIndex + 2 - selectedItems.Count;

            // Remove all selected elements from the list
            foreach(var item in selectedItems) {
                config.stringReplacementsGui.Remove(item);
            }

            // Insert them back as a compact block right at the calculated target position
            for(int i = 0; i < selectedItems.Count; i++) {
                config.stringReplacementsGui.Insert(targetIndex + i, selectedItems[i]);
            }

            grid.Items.Refresh();

            // Restore the focus and selection state
            grid.UnselectAll();
            foreach(var item in selectedItems) {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selectedItems[selectedItems.Count - 1]);
            grid.Focus();
        }
        #endregion
        #region private void String_Replacements_AddRule_Click(object sender, RoutedEventArgs e)
        private void String_Replacements_AddRule_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("ReplacementsGrid") is DataGrid replacementsGrid)) return;

            // Determine the position where the new rule should be inserted
            int insertIndex = config.stringReplacementsGui.Count; // Default: At the end

            // If a row is selected, insert directly below it (+1)
            if(replacementsGrid.SelectedIndex != -1) insertIndex = replacementsGrid.SelectedIndex + 1;

            // Insert the new rule at the calculated position in the list
            config.stringReplacementsGui.Insert(insertIndex, new ReplaceRule(string.Empty, string.Empty, RuleScope.Filter));

            // Refresh the grid
            replacementsGrid.Items.Refresh();

            // Set focus to the newly inserted row
            replacementsGrid.UnselectAll();
            replacementsGrid.SelectedIndex = insertIndex;
            replacementsGrid.ScrollIntoView(replacementsGrid.SelectedItem);
            replacementsGrid.Focus();

            config.Validate_Other();
            config.Validate();
        }
        #endregion
        #region private void String_Replacements_RemoveRule_Click(object sender, RoutedEventArgs e)
        private void String_Replacements_RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if(!(this.FindName("ReplacementsGrid") is DataGrid replacementsGrid)) return;
            if(replacementsGrid.SelectedItems == null || replacementsGrid.SelectedItems.Count == 0) return;

            // Remember the lowest index of the current selection
            int minSelectedIndex = replacementsGrid.SelectedItems
                .Cast<ReplaceRule>()
                .Select(rule => config.stringReplacementsGui.IndexOf(rule))
                .Min();

            // Remove from the list
            var selectedRules = replacementsGrid.SelectedItems.Cast<ReplaceRule>().ToList();
            foreach(var rule in selectedRules) {
                config.stringReplacementsGui.Remove(rule);
            }

            // Update the UI so that the remaining rows are recalculated
            replacementsGrid.Items.Refresh();

            // Determine the new index and select it
            if(config.stringReplacementsGui.Count > 0) {

                int targetIndex = 0; // Default: At the beginning

                // Try to target the element directly before the deleted selection
                if(targetIndex < minSelectedIndex - 1) targetIndex = minSelectedIndex - 1;

                // Safety cap to prevent out-of-bounds errors
                if(targetIndex >= config.stringReplacementsGui.Count) {
                    targetIndex = config.stringReplacementsGui.Count - 1;
                }

                // Set focus
                replacementsGrid.UnselectAll();
                replacementsGrid.SelectedIndex = targetIndex;
                replacementsGrid.ScrollIntoView(replacementsGrid.SelectedItem);
                replacementsGrid.Focus();
            }

            config.Validate_Other();
            config.Validate();
        }
        #endregion

        #region private void Disable_MouseWheel(object sender, MouseWheelEventArgs e)
        private void Disable_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Do not forward the event if a ComboBox is currently focused and its dropdown is open
            var focusedCombo = Keyboard.FocusedElement as ComboBox;
            if(focusedCombo == null && Keyboard.FocusedElement is DependencyObject depObj) {
                focusedCombo = ItemsControl.ItemsControlFromItemContainer(depObj) as ComboBox;
            }

            if(focusedCombo != null && focusedCombo.IsDropDownOpen) return;

            if(!e.Handled) {
                e.Handled = true;

                // Clone the event with the correct delta
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };

                // Forward the event to the parent element (the outer ScrollViewer)
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
        #endregion

        // TabControl
        #region TabControl - Selection changed
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(!(this.FindName("TabItemSettings") is TabItem tabItemSettings)) return;
            if(!(this.FindName("TabItemDocumentation") is TabItem tabItemDocumentation)) return;
            if(!(this.FindName("TabItemLogFile") is TabItem tabItemLogFile)) return;
            if(!(this.FindName("TabItemPersonal") is TabItem tabItemPersonal)) return;
            if(!(this.FindName("SettingsScrollViewer") is ScrollViewer settingsScrollViewer)) return;

            // remember settingsScrollViewer position
            if(_settingsTabActive) {
                _settingsScrollOffset = settingsScrollViewer.VerticalOffset;
                _settingsTabActive = false;
            }

            // restore settingsScrollViewer position
            if(tabItemSettings.IsSelected) {
                _settingsTabActive = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { settingsScrollViewer.ScrollToVerticalOffset(_settingsScrollOffset); }));
            }

            if(tabItemDocumentation.IsSelected && !_documentationIsLoaded) LoadDocumentation();
            if(tabItemLogFile.IsSelected && !_logFileIsLoaded) LoadLogFile();
            if(tabItemPersonal.IsSelected && !_personalIsLoaded) LoadPersonal();
        }
        #endregion

        // Documentation:
        #region private async void LoadDocumentation()
        private async void LoadDocumentation()
        {
            if(!(this.FindName("DocWebView") is Microsoft.Web.WebView2.Wpf.WebView2 docWebView)) return;

            // 1. Determine the language to load and to toggle
            Language languageToLoad = _documentationShowEnglishOverride ? Core.Language.English : config.language;
            Language languageToToggle = _documentationShowEnglishOverride ? config.language : Core.Language.English;

            // 2. Validate availability and fallback if necessary
            LanguageDefinition languageDefinitionToLoad = Plugin.languageManager.availableLanguages[languageToLoad];
            LanguageDefinition languageDefinitionToToggle = Plugin.languageManager.availableLanguages[languageToToggle];
            if(string.IsNullOrEmpty(languageDefinitionToLoad.readmeVersion)) {
                languageToLoad = languageToToggle;
                languageDefinitionToLoad = languageDefinitionToToggle;
            }

            // 3. Update UI states via Data-Binding properties
            config.viewDocumentationCurrentLanguage = string.Format(config.L[003_002_001] /* Version "{0} {1}" */, languageDefinitionToLoad.languageNameLocal, languageDefinitionToLoad.readmeVersion);
            config.OnPropertyChanged(nameof(config.viewDocumentationCurrentLanguage));

            if(languageToLoad == languageToToggle) {
                config.viewDocumentationToggleLanguage_Visibility = Visibility.Collapsed;
                config.OnPropertyChanged(nameof(config.viewDocumentationToggleLanguage_Visibility));
            } else {
                config.viewDocumentationToggleLanguage_Visibility = Visibility.Visible;
                config.viewDocumentationToggleLanguage_NewLanguage = string.Format(config.L[003_002_002] /* Show Version "{0} {1}" */, languageDefinitionToToggle.languageNameLocal, languageDefinitionToToggle.readmeVersion);
                config.OnPropertyChanged(nameof(config.viewDocumentationToggleLanguage_Visibility));
                config.OnPropertyChanged(nameof(config.viewDocumentationToggleLanguage_NewLanguage));
            }

            // 4. Load markup content and convert to HTML
            string markupFile = System.IO.Path.Combine(Plugin.appFolder, $"tcmatch.readme.{languageDefinitionToLoad.languageIsoCode}.md");
            string markupContent = ReadMarkdownFile(markupFile);
            string htmlContent = GetHtmlFromMarkdown(markupContent, config.theme);
            _documentationIsLoaded = true;

            // 5. Initialize browser engine and navigate
            await InitializeWebViewOnceAsync(docWebView);
            docWebView.NavigateToString(htmlContent);
        }
        #endregion
        #region private string ReadMarkdownFile(string filePath)
        /// <summary>
        /// Reads the content of a markdown file and returns localized error strings if loading fails.
        /// </summary>
        private string ReadMarkdownFile(string filePath)
        {
            if(File.Exists(filePath)) {
                try {
                    return System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                } catch(Exception ex) {
                    return string.Format(config.L[003_002_004] /* # ❌ Documentation missing\n\n**Error reading file:**\n\n```{0}```\n\n**Error message:**\n\n```\n{1}\n``` */, filePath, ex);
                }
            }
            return string.Format(config.L[003_002_003] /* # ❌ Documentation missing\n\n**File not found:**\n\n```{0}``` */, filePath);
        }
        #endregion
        #region public string GetHtmlFromMarkdown(string markdownText, Theme theme)
        public string GetHtmlFromMarkdown(string markdownText, Theme theme)
        {
            var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            string rawHtml = Markdig.Markdown.ToHtml(markdownText, pipeline);

            string cssRules = theme == Theme.Dark ? @"
            body {
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                font-size: 14px; line-height: 1.5; padding: 20px; color: #c9d1d9; background-color: #0d1117;
                color-scheme: dark;
            }
            ::-webkit-scrollbar { width: 10px; height: 10px; }
            ::-webkit-scrollbar-track { background: #0d1117; }
            ::-webkit-scrollbar-thumb { background: #30363d; border-radius: 5px; }
            ::-webkit-scrollbar-thumb:hover { background: #8b949e; }

            h1 { font-size: 2em; border-bottom: 1px solid #21262d; padding-bottom: .3em; }
            h2 { font-size: 1.5em; border-bottom: 1px solid #21262d; padding-bottom: .3em; }
            h3 { font-size: 1.25em; }
            hr { height: .25em; padding: 0; margin: 24px 0; background-color: #30363d; border: 0; }
            code {
                font-family: monospace; padding: .2em .4em; margin: 0; 
                font-size: 85%; background-color: rgba(110,118,129,0.4); border-radius: 3px;
            }
            table { border-spacing: 0; border-collapse: collapse; margin-top: 0; margin-bottom: 16px; width: 100%; }
            table th, table td { padding: 6px 13px; border: 1px solid #30363d; }
            table th { font-weight: 600; background-color: #161b22; }
            table tr { background-color: #0d1117; border-top: 1px solid #21262d; }
            table tr:nth-child(even) { background-color: #161b22; }"
            : @"
            body {
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                font-size: 14px; line-height: 1.5; padding: 20px; color: #24292e; background-color: #fff;
            }
            h1 { font-size: 2em; border-bottom: 1px solid #eaecef; padding-bottom: .3em; }
            h2 { font-size: 1.5em; border-bottom: 1px solid #eaecef; padding-bottom: .3em; }
            h3 { font-size: 1.25em; }
            hr { height: .25em; padding: 0; margin: 24px 0; background-color: #e1e4e6; border: 0; }
            code {
                font-family: monospace; padding: .2em .4em; margin: 0; 
                font-size: 85%; background-color: rgba(27,31,35,.05); border-radius: 3px;
            }
            table { border-spacing: 0; border-collapse: collapse; margin-top: 0; margin-bottom: 16px; width: 100%; }
            table th, table td { padding: 6px 13px; border: 1px solid #dfe2e5; }
            table th { font-weight: 600; background-color: #f6f8fa; }
            table tr { background-color: #fff; border-top: 1px solid #c6cbd1; }
            table tr:nth-child(even) { background-color: #f6f8fa; }";

            string htmlDocument = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
        <meta charset='utf-8'>
        <style>
            {cssRules}
        </style>
    </head>
    <body>
        <div class='markdown-body'>
            {rawHtml}
        </div>
    </body>
    </html>";

            return htmlDocument;
        }
        #endregion
        #region private async Task InitializeWebViewOnceAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        /// <summary>
        /// Asynchronously initializes the CoreWebView2 environment once and registers permanent link routing rules.
        /// </summary>
        private async Task InitializeWebViewOnceAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            if(webView.CoreWebView2 != null) return;

            // Using "BrowserProfile" for transparent profile path definitions
            if(_webViewEnvironment == null) {
                string userDataFolder = System.IO.Path.Combine(Plugin.dataFolder, "BrowserProfile");
                _webViewEnvironment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
            }

            await webView.EnsureCoreWebView2Async(_webViewEnvironment);

            // Forward external web links directly to the default OS browser app context
            webView.CoreWebView2.NavigationStarting += (s, e) => {
                if(e.Uri.StartsWith("data:") || e.Uri == "about:blank") return;

                e.Cancel = true;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = e.Uri,
                    UseShellExecute = true
                });
            };
        }
        #endregion
        #region private void DocumentationToggleLanguage_Click(object sender, RoutedEventArgs e)
        private void DocumentationToggleLanguage_Click(object sender, RoutedEventArgs e)
        {
            _documentationShowEnglishOverride = !_documentationShowEnglishOverride;
            LoadDocumentation();
        }
        #endregion
        #region public void ResetDocumentation()
        public void ResetDocumentation()
        {
            _documentationIsLoaded = false;
            _documentationShowEnglishOverride = false;
        }
        #endregion

        // Log File:
        #region private void LoadLogFile()
        private void LoadLogFile()
        {
            if(!File.Exists(Plugin.logFilePath)) {
                config.viewLogFileStatusText = config.L[004_002_003] /* No log file available */;
                config.viewLogFileContent = string.Format(config.L[004_002_005] /* ❌ Log file missing\nFile not found: {0} */, Plugin.logFilePath);
                config.viewLogFileDelete_Visibility = Visibility.Collapsed;
                config.OnPropertyChanged(nameof(config.viewLogFileStatusText));
                config.OnPropertyChanged(nameof(config.viewLogFileContent));
                config.OnPropertyChanged(nameof(config.viewLogFileDelete_Visibility));
                return;
            }

            try {
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(Plugin.logFilePath);
                long fileSizeInBytes = fileInfo.Length;

                // Format file size and date dynamically based on current culture setup
                string formattedFileSize = FormatFileSize(fileSizeInBytes);
                string lastWriteTime = fileInfo.LastWriteTime.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm");

                config.viewLogFileStatusText = string.Format(config.L[004_002_001] /* Log file from {0} ({1}) */, lastWriteTime, formattedFileSize);
                config.viewLogFileDelete_Visibility = Visibility.Visible;

                // Tail-Loading: Only read the last 100 KB if file is too large
                long maxBytesToRead = 100 * 1024;
                if(fileSizeInBytes <= maxBytesToRead) {
                    using(var stream = new System.IO.FileStream(Plugin.logFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                    using(var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8)) {
                        config.viewLogFileContent = reader.ReadToEnd();
                    }
                } else {
                    using(var stream = new System.IO.FileStream(Plugin.logFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)) {
                        stream.Seek(fileSizeInBytes - maxBytesToRead, System.IO.SeekOrigin.Begin);
                        using(var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8)) {
                            string rawTail = reader.ReadToEnd();

                            // Find the end of the first partial line to start cleanly
                            int firstNewLine = rawTail.IndexOf('\n');
                            if(firstNewLine >= 0 && firstNewLine < rawTail.Length - 1) {
                                config.viewLogFileContent = config.L[004_002_004] /* [... Older log entries skipped for performance reasons ...]\n\n */ + rawTail.Substring(firstNewLine + 1);
                            } else {
                                config.viewLogFileContent = config.L[004_002_004] /* [... Older log entries skipped for performance reasons ...]\n\n */ + rawTail;
                            }
                        }
                    }
                }
            } catch(Exception ex) {
                config.viewLogFileContent = string.Format(config.L[004_002_006] /* ❌ Error reading log file\nFile: {0}\n\nError message:\n{1} */, Plugin.logFilePath, ex.Message);
            }

            config.OnPropertyChanged(nameof(config.viewLogFileStatusText));
            config.OnPropertyChanged(nameof(config.viewLogFileContent));
            config.OnPropertyChanged(nameof(config.viewLogFileDelete_Visibility));
            _logFileIsLoaded = true;
        }
        #endregion
        #region private string FormatFileSize(long bytes)
        /// <summary>
        /// Dynamically scales byte counts to human-readable strings (B, KB, MB, GB).
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int order = 0;

            while(value >= 1024 && order < suffixes.Length - 1) {
                order++;
                value /= 1024;
            }

            // Return with 1 decimal place (or no decimals if it's raw bytes)
            return order == 0 ? $"{value} {suffixes[order]}" : $"{value:F1} {suffixes[order]}";
        }
        #endregion
        #region private void LogFileDelete_Click(object sender, RoutedEventArgs e)
        private void LogFileDelete_Click(object sender, RoutedEventArgs e)
        {
            if(File.Exists(Plugin.logFilePath)) {
                File.Delete(Plugin.logFilePath);
            }

            // Immediately refresh the view to show empty state
            _logFileIsLoaded = false;
            LoadLogFile();
        }
        #endregion
        #region public void ResetLogFile()
        public void ResetLogFile()
        {
            _logFileIsLoaded = false;
        }
        #endregion

        // Personal Note:
        #region private async void LoadPersonal()
        private async void LoadPersonal()
        {
            if(!(this.FindName("PersonalWebView") is Microsoft.Web.WebView2.Wpf.WebView2 personalWebView)) return;

            // 1. Determine the language to load and to toggle
            Language languageToLoad = _personalShowEnglishOverride ? Core.Language.English : config.language;
            Language languageToToggle = _personalShowEnglishOverride ? config.language : Core.Language.English;

            // 2. Validate availability and fallback if necessary
            LanguageDefinition languageDefinitionToLoad = Plugin.languageManager.availableLanguages[languageToLoad];
            LanguageDefinition languageDefinitionToToggle = Plugin.languageManager.availableLanguages[languageToToggle];

            string markupContent = languageDefinitionToLoad.personalNote;
            string toggleContent = languageDefinitionToToggle.personalNote;

            if(string.IsNullOrEmpty(markupContent)) {
                languageToLoad = languageToToggle;
                languageDefinitionToLoad = languageDefinitionToToggle;
                markupContent = toggleContent;
            }

            // 3. Update UI states via Data-Binding properties
            config.viewPersonalCurrentLanguage = string.Format(config.L[005_002_001] /* {0} */, languageDefinitionToLoad.languageNameLocal);
            config.OnPropertyChanged(nameof(config.viewPersonalCurrentLanguage));

            if(languageToLoad == languageToToggle) {
                config.viewPersonalToggleLanguage_Visibility = Visibility.Collapsed;
                config.OnPropertyChanged(nameof(config.viewPersonalToggleLanguage_Visibility));
            } else {
                config.viewPersonalToggleLanguage_Visibility = Visibility.Visible;
                config.viewPersonalToggleLanguage_NewLanguage = string.Format(config.L[005_002_002] /* Switch to "{0}" */, languageDefinitionToToggle.languageNameLocal);
                config.OnPropertyChanged(nameof(config.viewPersonalToggleLanguage_Visibility));
                config.OnPropertyChanged(nameof(config.viewPersonalToggleLanguage_NewLanguage));
            }

            // 4. Convert markup content to HTML
            string htmlContent = GetHtmlFromMarkdown(markupContent, config.theme);
            _personalIsLoaded = true;

            // 5. Initialize browser engine and navigate
            await InitializeWebViewOnceAsync(personalWebView);
            personalWebView.NavigateToString(htmlContent);
        }
        #endregion
        #region private void PersonalToggleLanguage_Click(object sender, RoutedEventArgs e)
        private void PersonalToggleLanguage_Click(object sender, RoutedEventArgs e)
        {
            _personalShowEnglishOverride = !_personalShowEnglishOverride;
            LoadPersonal();
        }
        #endregion
        #region public void ResetPersonal()
        public void ResetPersonal()
        {
            _personalIsLoaded = false;
            _personalShowEnglishOverride = false;
        }
        #endregion
    }
    #endregion
    #region SearchAssistantWindow Class
    public partial class SearchAssistantWindow : Window
    {
        #region Helpers
        public Config config;

        private IntPtr tcMainHandle = IntPtr.Zero;
        private IntPtr tcQuickSearchHandle = IntPtr.Zero;
        private IntPtr tcQuickSearchEditHandle = IntPtr.Zero;

        private NativeMethods.RECT tcMainRect;
        private NativeMethods.RECT tcQuickSearchRect;
        private NativeMethods.RECT desktopRect;

        private System.Windows.Threading.DispatcherTimer activeWindowMonitorTimer;
        private bool isConfigOpen = false;

        private readonly Dictionary<ButtonStyle, System.Windows.Style> buttonStyles = new Dictionary<ButtonStyle, System.Windows.Style>();
        #endregion

        #region public SearchAssistantWindow()
        public SearchAssistantWindow()
        {
            config = Plugin.config;
            this.DataContext = Plugin.config;

            WindowHelper.LoadXAML(this, "tcmatch.Core.SearchAssistantWindow.xaml");
            Plugin.config.PopulateViewFields_CurrentWindow_Theme(this, null);

            // Cache all styles once during startup for performance
            buttonStyles[ButtonStyle.Primary] = TryFindResource("PrimaryButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.Scope] = TryFindResource("ScopeButtonStyle") as System.Windows.Style;

            buttonStyles[ButtonStyle.Error] = TryFindResource("ErrorButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.Warning] = TryFindResource("WarningButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.Information] = TryFindResource("InformationButtonStyle") as System.Windows.Style;

            buttonStyles[ButtonStyle.LogicalOperator] = TryFindResource("LogicalOperatorButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.MetadataAlias] = TryFindResource("MetadataAliasButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.Modifier] = TryFindResource("ModifierButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.ModifierDisabled] = TryFindResource("ModifierDisabledButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.SearchOperator] = TryFindResource("SearchOperatorButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.SearchText] = TryFindResource("SearchTextButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.QuickAction] = TryFindResource("QuickActionButtonStyle") as System.Windows.Style;
            buttonStyles[ButtonStyle.QuickActionDisabled] = TryFindResource("QuickActionDisabledButtonStyle") as System.Windows.Style;

            this.ShowActivated = false;
            this.ShowInTaskbar = false;
            this.Topmost = true;
        }
        #endregion
        #region public void ShowWindow()
        public void ShowWindow()
        {
            #region Change Windowposition if needed
            if(tcQuickSearchHandle != IntPtr.Zero && !NativeMethods.IsWindow(tcQuickSearchHandle)) {
                tcQuickSearchHandle = IntPtr.Zero;
                Plugin.FlushHistoryOnClose();
            }

            // Reposition if the window is hidden, OR if we are pinned and lost our target handle
            if(!IsVisible || (config.pinGui && tcQuickSearchHandle == IntPtr.Zero)) {
                FindWindowHandles();
                FindDockingCoordinates();
                SetWindowPosition(config.guiSizeX, config.guiSizeY, config.guiOffsetX, config.guiOffsetY, config.guiDocking, config.guiDockingCorner);
                Show();
            }
            #endregion

            #region Define Basics
            var searchQueryPlan = Plugin.searchQueryPlan;

            if(config == null || searchQueryPlan == null) return;

            bool guiShowErrors = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.Errors)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowWarnings = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.Warnings)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowInformations = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.Informations)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowSearchQueryPlan = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.SearchQueryPlan)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowUsedModifiers = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.UsedModifiers)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowDynamicHints = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.DynamicHints)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowAvailableCharsList = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.AvailableCharsList)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowTextReplacements = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.TextReplacements)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;
            bool guiShowQuickAccess = config.guiSections.FirstOrDefault(guiSection => guiSection.guiSectionType == GuiSectionType.QuickAccess)?.guiSectionDisplayState != GuiSectionDisplayState.Hidden;

            List<Button> buttons_errors = new List<Button>();
            List<Button> buttons_warnings = new List<Button>();
            List<Button> buttons_informations = new List<Button>();
            List<Button> buttons_searchQueryPlan = new List<Button>();
            List<Button> buttons_usedModifiers = new List<Button>();
            List<Button> buttons_dynamicHints = new List<Button>();
            List<Button> buttons_availableCharsList = new List<Button>();
            List<Button> buttons_textReplacements = new List<Button>();
            List<Button> buttons_quickAccess = new List<Button>();

            bool isUsed_isNegated = false;
            bool isUsed_isInvertCase = false;
            bool isUsed_isStartAnchor = false;
            bool isUsed_isEndAnchor = false;

            string lastMetadataAlias = null;

            Thickness margin_0_1 = new Thickness(0 /* left */, 2 /* top */, 1 /* right */, 2 /* bottom */);
            Thickness margin_2_2 = new Thickness(2 /* left */, 2 /* top */, 2 /* right */, 2 /* bottom */);
            Thickness margin_5_5 = new Thickness(5 /* left */, 2 /* top */, 5 /* right */, 2 /* bottom */);
            Thickness margin_15_15 = new Thickness(15 /* left */, 2 /* top */, 15 /* right */, 2 /* bottom */);
            #endregion

            #region 1. Collect search query plan content - 2. Collect errors - 3. Collect used modifiers
            for(int b = 0; b < searchQueryPlan.searchBranches.Count; b++) {
                var searchBranch = searchQueryPlan.searchBranches[b];

                if(guiShowSearchQueryPlan && b != 0) {
                    buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.ArrowSplit24, null, string.Format(config.L[100_002_001] /* Character for global OR operator: "{0}" – Splits the search string into independent branches. If at least one branch matches, an item is found. */, config.orChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_15_15));
                }

                if(config.guiUseFullSearchQueryPlan) lastMetadataAlias = null;

                for(int c = 0; c < searchBranch.searchConditions.Count; c++) {
                    var searchCondition = searchBranch.searchConditions[c];

                    #region 1. Collect search query plan content
                    if(guiShowSearchQueryPlan) {
                        if(c != 0) {
                            buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.AddCircle24, null, string.Format(config.L[100_002_002] /* Character for AND operator: "{0}" – Combines conditions within a search branch. All conditions must be met for the branch to match. */, config.andChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_5_5));
                        }

                        #region Metadata
                        if(lastMetadataAlias != searchCondition.metadataAlias) {
                            if(config.guiUseFullSearchQueryPlan || b + c != 0 || searchCondition.metadataAlias != searchQueryPlan.defaultMetadataAlias) {
                                string metadataToolTip = config._metadataAliasTooltip.TryGetValue(searchCondition.metadataAlias, out string tooltip) ? tooltip : "";
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Tag24, searchCondition.metadataAlias, metadataToolTip, ButtonStyle.MetadataAlias, margin_0_1));
                            }

                            lastMetadataAlias = searchCondition.metadataAlias;
                        }
                        #endregion
                        #region Modifiers
                        if(searchCondition.isNegated) {
                            buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EyeOff24, null, string.Format(config.L[100_002_005] /* Character for negation: "{0}" – Inverts the condition. For the condition to be met, the corresponding terms must not be contained. */, config.notChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                        }
                        if(searchCondition.isInvertCase) {
                            if(config.caseSensitiveDynamic) {
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, string.Format(config.L[100_002_006] /* Character to toggle case sensitivity: "{0}" – Although active by default, case sensitivity is ignored in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                            } else {
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, string.Format(config.L[100_002_007] /* Character to toggle case sensitivity: "{0}" – Although ignored by default, case sensitivity is observed in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                            }
                        }
                        if(searchCondition.matchFirstTermAsStartAnchor) {
                            if(searchCondition.isStartAnchor) {
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, string.Format(config.L[100_002_016] /* Character to disable match at start of text: "{0}" – Although global settings automatically add this modifier to the first search term, it is disabled by this character. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.ModifierDisabled, margin_0_1));
                            } else {
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, string.Format(config.L[100_002_015] /* Character to force match at start of text: "{0}" – The search text for this condition must appear directly at the beginning of the item name. Global settings automatically add this modifier to the first search term. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                            }
                        } else if(searchCondition.isStartAnchor) {
                            buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, string.Format(config.L[100_002_008] /* Character to force match at start of text: "{0}" – The search text for this condition must appear directly at the beginning of the item name. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                        }
                        if(searchCondition.isEndAnchor) {
                            buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingRight24, null, string.Format(config.L[100_002_009] /* Character to force match at end of text: "{0}" – The search text for this condition must appear directly at the end of the item name. */, config.endAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_0_1));
                        }
                        #endregion

                        if(searchCondition.searchMode == SearchMode.Math) {
                            if(searchCondition.metadataIds[0] == MetadataConstants.Size) {
                                #region Search Mode: Size
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Storage24, null, string.Format(config.L[100_002_224] /* File size filter: Restricts the search based on file size. Examples: ">700", "<50M", ">2{0}2G", "=7M" */, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.SearchOperator, margin_0_1));

                                string buttonText = "";
                                string toolTipText = "";
                                ButtonStyle buttonStyle = ButtonStyle.SearchText;

                                string minBytesStr = searchCondition.minSize.ToString("N0", CultureInfo.CurrentCulture);
                                string maxBytesStr = searchCondition.maxSize.ToString("N0", CultureInfo.CurrentCulture);

                                if(searchCondition.minSize > searchCondition.maxSize) {
                                    string minStr = SearchCondition.FormatSizeReadable(searchCondition.minSize);
                                    string maxStr = SearchCondition.FormatSizeReadable(searchCondition.maxSize);
                                    buttonText = string.Format(config.L[100_002_215] /* Invalid range: {0} to {1} */, minStr, maxStr);
                                    toolTipText = string.Format(config.L[100_002_221] /* File size: Invalid range! The lower bound {0} ({2} bytes) is greater than the upper bound {1} ({3} bytes). */, minStr, maxStr, minBytesStr, maxBytesStr);
                                    buttonStyle = ButtonStyle.Error;
                                } else if(searchCondition.minSize == searchCondition.maxSize) {
                                    string sizeStr = SearchCondition.FormatSizeReadable(searchCondition.minSize);
                                    buttonText = string.Format(config.L[100_002_213] /* = {0} */, sizeStr);
                                    toolTipText = string.Format(config.L[100_002_219] /* File size: Exactly {0} ({1} bytes). */, sizeStr, minBytesStr);
                                } else if(searchCondition.minSize > 0 && searchCondition.maxSize < long.MaxValue) {
                                    string minStr = SearchCondition.FormatSizeReadable(searchCondition.minSize);
                                    string maxStr = SearchCondition.FormatSizeReadable(searchCondition.maxSize);

                                    if(minStr == maxStr) {
                                        buttonText = string.Format(config.L[100_002_214] /* ≈ {0} */, minStr);
                                        toolTipText = string.Format(config.L[100_002_220] /* File size: Approximately {0} ({1} to {2} bytes). */, minStr, minBytesStr, maxBytesStr);
                                    } else {
                                        buttonText = string.Format(config.L[100_002_212] /* {0} to {1} */, minStr, maxStr);
                                        toolTipText = string.Format(config.L[100_002_218] /* File size: Between {0} ({2} bytes) and {1} ({3} bytes). */, minStr, maxStr, minBytesStr, maxBytesStr);
                                    }
                                } else if(searchCondition.minSize > 0) {
                                    string minStr = SearchCondition.FormatSizeReadable(searchCondition.minSize);
                                    buttonText = string.Format(config.L[100_002_210] /* from {0} */, minStr);
                                    toolTipText = string.Format(config.L[100_002_216] /* File size: At least {0} ({1} bytes). */, minStr, minBytesStr);
                                } else if(searchCondition.maxSize < long.MaxValue) {
                                    string maxStr = SearchCondition.FormatSizeReadable(searchCondition.maxSize);
                                    buttonText = string.Format(config.L[100_002_211] /* up to {0} */, maxStr);
                                    toolTipText = string.Format(config.L[100_002_217] /* File size: At most {0} ({1} bytes). */, maxStr, maxBytesStr);
                                }

                                if(buttonText != "") buttons_searchQueryPlan.Add(CreateButton(null, buttonText, toolTipText, buttonStyle, margin_0_1));

                                if(searchCondition.mathParseErrors.Count != 0) {
                                    foreach(string mathParseError in searchCondition.mathParseErrors) {
                                        buttons_searchQueryPlan.Add(CreateButton(null, mathParseError, string.Format(config.L[100_002_222] /* File size: Invalid expression "{0}". Examples: >700, <50M, >2{1}2G, =7M */, mathParseError, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.Error, margin_0_1));
                                    }
                                }
                                #endregion
                            }

                            if(searchCondition.metadataIds[0] == MetadataConstants.Age) {
                                #region Search Mode: Age
                                buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.CalendarLtr24, null, string.Format(config.L[100_002_264] /* Modification age filter: Restricts the search based on file modification date. Examples: ">14", "<2h", ">2{0}5y", "=3d" */, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.SearchOperator, margin_0_1));

                                string buttonText = "";
                                string toolTipText = "";
                                ButtonStyle buttonStyle = ButtonStyle.SearchText;

                                if(searchCondition.minAgeDays > searchCondition.maxAgeDays) {
                                    string minStr = SearchCondition.FormatAgeReadable(searchCondition.minAgeDays);
                                    string maxStr = SearchCondition.FormatAgeReadable(searchCondition.maxAgeDays);
                                    buttonText = string.Format(config.L[100_002_255] /* Invalid range: {0} to {1} */, minStr, maxStr);
                                    toolTipText = string.Format(config.L[100_002_261] /* Modification age: Invalid range! The lower bound {0} is greater than the upper bound {1}. */, minStr, maxStr);
                                    buttonStyle = ButtonStyle.Error;
                                } else if(searchCondition.minAgeDays == searchCondition.maxAgeDays) {
                                    string ageStr = SearchCondition.FormatAgeReadable(searchCondition.minAgeDays);
                                    buttonText = string.Format(config.L[100_002_253] /* = {0} */, ageStr);
                                    toolTipText = string.Format(config.L[100_002_259] /* Modification age: Exactly {0}. */, ageStr);
                                } else if(searchCondition.minAgeDays > 0.0 && searchCondition.maxAgeDays < AgeLimits.MaxValue) {
                                    string minStr = SearchCondition.FormatAgeReadable(searchCondition.minAgeDays);
                                    string maxStr = SearchCondition.FormatAgeReadable(searchCondition.maxAgeDays);

                                    if(minStr == maxStr) {
                                        buttonText = string.Format(config.L[100_002_254] /* ≈ {0} */, minStr);
                                        toolTipText = string.Format(config.L[100_002_260] /* Modification age: Approximately {0}. */, minStr);
                                    } else {
                                        buttonText = string.Format(config.L[100_002_252] /* {0} to {1} */, minStr, maxStr);
                                        toolTipText = string.Format(config.L[100_002_258] /* Modification age: Between {0} and {1}. */, minStr, maxStr);
                                    }
                                } else if(searchCondition.minAgeDays > 0.0) {
                                    string minStr = SearchCondition.FormatAgeReadable(searchCondition.minAgeDays);
                                    buttonText = string.Format(config.L[100_002_250] /* from {0} */, minStr);
                                    toolTipText = string.Format(config.L[100_002_256] /* Modification age: At least {0}. */, minStr);
                                } else if(searchCondition.maxAgeDays < AgeLimits.MaxValue) {
                                    string maxStr = SearchCondition.FormatAgeReadable(searchCondition.maxAgeDays);
                                    buttonText = string.Format(config.L[100_002_251] /* up to {0} */, maxStr);
                                    toolTipText = string.Format(config.L[100_002_257] /* Modification age: At most {0}. */, maxStr);
                                }

                                if(buttonText != "") buttons_searchQueryPlan.Add(CreateButton(null, buttonText, toolTipText, buttonStyle, margin_0_1));

                                if(searchCondition.mathParseErrors.Count != 0) {
                                    foreach(string mathParseError in searchCondition.mathParseErrors) {
                                        buttons_searchQueryPlan.Add(CreateButton(null, mathParseError, string.Format(config.L[100_002_262] /* Modification age: Invalid expression "{0}". Examples: >14, <2h, >2{1}5y, =3d */, mathParseError, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.Error, margin_0_1));
                                    }
                                }
                                #endregion
                            }
                        } else {
                            if(searchCondition.searchMode == SearchMode.Regex) {
                                #region Search Mode: Regex Search
                                if(config.guiUseFullSearchQueryPlan || config.defaultSearchModeDynamic != SearchMode.Regex) buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Code24, null, string.Format(config.L[100_002_011] /* Character for regex search: "{0}" – Interprets the text as a regular expression and searches with it. */, config.regexModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_0_1));

                                if(searchCondition.regexParseError != null) {
                                    buttons_searchQueryPlan.Add(CreateButton(null, searchCondition.localTerms[0], string.Format(config.L[100_002_204] /* Regex Search: Error in regular expression "{0}" ({1}). */, searchCondition.localTerms[0], searchCondition.regexParseError), ButtonStyle.Error, margin_0_1));
                                } else {
                                    string regexTerm = config.caseSensitiveDynamic ^ searchCondition.isInvertCase ? searchCondition.compiledRegexCaseSensitive.ToString() : searchCondition.compiledRegex.ToString();
                                    buttons_searchQueryPlan.Add(CreateButton(null, regexTerm, string.Format(config.L[100_002_203] /* Regex Search: Interprets "{0}" as a regular expression. */, regexTerm), ButtonStyle.SearchText, margin_0_1));
                                }
                                #endregion
                            } else if(searchCondition.searchMode == SearchMode.Pattern) {
                                #region Search Mode: Pattern Search
                                if(config.guiUseFullSearchQueryPlan || config.defaultSearchModeDynamic != SearchMode.Pattern) buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, null, string.Format(config.L[100_002_017] /* Character for pattern search: "{0}" – Interprets the text as a simplified pattern and searches with it. */, config.patternModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_0_1));

                                if(searchCondition.regexParseError != null) {
                                    buttons_searchQueryPlan.Add(CreateButton(null, searchCondition.pattern, string.Format(config.L[100_002_207] /* Pattern Search: Pattern "{0}" was converted to regular expression "{1}", but generated an error: {2} */, searchCondition.pattern, searchCondition.localTerms[0], searchCondition.regexParseError), ButtonStyle.Error, margin_0_1));
                                } else {
                                    string regexTerm = config.caseSensitiveDynamic ^ searchCondition.isInvertCase ? searchCondition.compiledRegexCaseSensitive.ToString() : searchCondition.compiledRegex.ToString();
                                    buttons_searchQueryPlan.Add(CreateButton(null, searchCondition.pattern, string.Format(config.L[100_002_206] /* Pattern Search: Interprets "{0}" as pattern matching (Regex: "{1}"). */, searchCondition.pattern, regexTerm), ButtonStyle.SearchText, margin_0_1));
                                }
                                #endregion
                            } else {
                                #region Search Modes: Standard Search, Fuzzy Search, Sequence Search
                                if(searchCondition.searchMode == SearchMode.Standard) {
                                    if(config.guiUseFullSearchQueryPlan || config.defaultSearchModeDynamic != SearchMode.Standard) buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EqualCircle24, null, string.Format(config.L[100_002_010] /* Character for standard search: "{0}" – Searches for the exact text anywhere within the item name. */, config.standardModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_0_1));
                                }
                                if(searchCondition.searchMode == SearchMode.Fuzzy) {
                                    if(config.guiUseFullSearchQueryPlan || config.defaultSearchModeDynamic != SearchMode.Fuzzy) buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TargetArrow24, null, string.Format(config.L[100_002_012] /* Character for fuzzy search: "{0}" – Tolerates typos using an automatic Levenshtein distance. */, config.fuzzyModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_0_1));
                                }
                                if(searchCondition.searchMode == SearchMode.Sequence) {
                                    if(config.guiUseFullSearchQueryPlan || config.defaultSearchModeDynamic != SearchMode.Sequence) buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Highlight24, null, string.Format(config.L[100_002_013] /* Character for sequence search: "{0}" – Searches for characters in the specified order, allowing gaps in between. */, config.sequenceModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_0_1));
                                }

                                for(int t = 0; t < searchCondition.localTerms.Count; t++) {
                                    var localTerm = searchCondition.localTerms[t];

                                    if(t != 0) {
                                        buttons_searchQueryPlan.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.SplitVertical24, null, string.Format(config.L[100_002_003] /* Character for local OR operator: "{0}" – Allows alternative terms within a single condition. At least one term must match for the condition to pass. */, config.localOrChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_0_1));
                                    }

                                    if(searchCondition.searchMode == SearchMode.Standard) {
                                        buttons_searchQueryPlan.Add(CreateButton(null, localTerm, string.Format(config.L[100_002_200] /* Standard Search: Searches for the text "{0}". */, localTerm), ButtonStyle.SearchText, margin_0_1));
                                    }
                                    if(searchCondition.searchMode == SearchMode.Fuzzy) {
                                        buttons_searchQueryPlan.Add(CreateButton(null, localTerm, string.Format(config.L[100_002_201] /* Fuzzy Search: Tolerates typos in the search text "{0}" using automatic Levenshtein distance. */, localTerm), ButtonStyle.SearchText, margin_0_1));
                                    }
                                    if(searchCondition.searchMode == SearchMode.Sequence) {
                                        buttons_searchQueryPlan.Add(CreateButton(null, localTerm, string.Format(config.L[100_002_202] /* Sequence Search: Matches characters from "{0}" in exact order with arbitrary gaps in between. */, localTerm), ButtonStyle.SearchText, margin_0_1));
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                    #endregion
                    #region 2. Collect errors
                    if(guiShowErrors) {
                        if(searchCondition.searchMode == SearchMode.Regex && searchCondition.regexParseError != null) {
                            buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_205] /* Regex: {0} */, searchCondition.localTerms[0]), string.Format(config.L[100_002_204] /* Regex Search: Error in regular expression "{0}" ({1}). */, searchCondition.localTerms[0], searchCondition.regexParseError), ButtonStyle.Error, margin_2_2));
                        }
                        if(searchCondition.searchMode == SearchMode.Pattern && searchCondition.regexParseError != null) {
                            buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_208] /* Pattern: {0} */, searchCondition.pattern), string.Format(config.L[100_002_207] /* Pattern Search: Pattern "{0}" was converted to regular expression "{1}", but generated an error: {2} */, searchCondition.pattern, searchCondition.localTerms[0], searchCondition.regexParseError), ButtonStyle.Error, margin_2_2));
                        }
                        if(searchCondition.searchMode == SearchMode.Math) {
                            if(searchCondition.metadataIds[0] == MetadataConstants.Size) {
                                string minBytesStr = searchCondition.minSize.ToString("N0", CultureInfo.CurrentCulture);
                                string maxBytesStr = searchCondition.maxSize.ToString("N0", CultureInfo.CurrentCulture);

                                if(searchCondition.minSize > searchCondition.maxSize) {
                                    string minStr = SearchCondition.FormatSizeReadable(searchCondition.minSize);
                                    string maxStr = SearchCondition.FormatSizeReadable(searchCondition.maxSize);
                                    string errorText = string.Format(config.L[100_002_215] /* Invalid range: {0} to {1} */, minStr, maxStr);
                                    buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_223] /* File size: {0} */, errorText), string.Format(config.L[100_002_221] /* File size: Invalid range! The lower bound {0} ({2} bytes) is greater than the upper bound {1} ({3} bytes). */, minStr, maxStr, minBytesStr, maxBytesStr), ButtonStyle.Error, margin_2_2));
                                }

                                if(searchCondition.mathParseErrors.Count != 0) {
                                    foreach(string mathParseError in searchCondition.mathParseErrors) {
                                        buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_223] /* File size: {0} */, mathParseError), string.Format(config.L[100_002_222] /* File size: Invalid expression "{0}". Examples: ">700", "<50M", ">2{1}2G", "=7M" */, mathParseError, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.Error, margin_2_2));
                                    }
                                }
                            }

                            if(searchCondition.metadataIds[0] == MetadataConstants.Age) {
                                if(searchCondition.minAgeDays > searchCondition.maxAgeDays) {
                                    string minStr = SearchCondition.FormatAgeReadable(searchCondition.minAgeDays);
                                    string maxStr = SearchCondition.FormatAgeReadable(searchCondition.maxAgeDays);
                                    string errText = string.Format(config.L[100_002_255] /* Invalid range: {0} to {1} */, minStr, maxStr);
                                    buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_263] /* Modification age: {0} */, errText), string.Format(config.L[100_002_261] /* Modification age: Invalid range! The lower bound {0} is greater than the upper bound {1}. */, minStr, maxStr), ButtonStyle.Error, margin_2_2));
                                }

                                if(searchCondition.mathParseErrors.Count != 0) {
                                    foreach(string mathParseError in searchCondition.mathParseErrors) {
                                        buttons_errors.Add(CreateButton(null, string.Format(config.L[100_002_263] /* Modification age: {0} */, mathParseError), string.Format(config.L[100_002_262] /* Modification age: Invalid expression "{0}". Examples: >14, <2h, >2{1}5y, =3d */, mathParseError, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.Error, margin_2_2));
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                    #region 3. Collect used modifiers
                    if(guiShowUsedModifiers) {
                        if(searchCondition.isNegated) isUsed_isNegated = true;
                        if(searchCondition.isInvertCase) isUsed_isInvertCase = true;

                        // Check if start anchor is effectively active (either via global setting or explicit '^')
                        if(searchCondition.matchFirstTermAsStartAnchor ^ searchCondition.isStartAnchor) isUsed_isStartAnchor = true;

                        if(searchCondition.isEndAnchor) isUsed_isEndAnchor = true;
                    }
                    #endregion
                }
            }
            #endregion
            #region 3. Add used modifiers
            if(guiShowUsedModifiers) {
                if(searchQueryPlan.tokenizationState.usedEscapeChar) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TooltipQuote24, null, string.Format(config.L[100_002_004] /* Character for escaping: "{0}" – Overrides the special function of the following control character and searches it as normal text. */, config.escapeChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
                if(searchQueryPlan.tokenizationState.usedQuoteChar) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextQuote24, null, string.Format(config.L[100_002_014] /* Character for quoting: "{0}" – Overrides the special function of all control characters in the enclosed text and searches it as normal text. */, config.quoteChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));

                if(isUsed_isNegated) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EyeOff24, null, string.Format(config.L[100_002_005] /* Character for negation: "{0}" – Inverts the condition. For the condition to be met, the corresponding terms must not be contained. */, config.notChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
                if(config.caseSensitiveDynamic) {
                    if(isUsed_isInvertCase) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, string.Format(config.L[100_002_006] /* Character to toggle case sensitivity: "{0}" – Although active by default, case sensitivity is ignored in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
                } else {
                    if(isUsed_isInvertCase) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, string.Format(config.L[100_002_007] /* Character to toggle case sensitivity: "{0}" – Although ignored by default, case sensitivity is observed in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
                }
                if(isUsed_isStartAnchor) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, string.Format(config.L[100_002_008] /* Character to force match at start of text: "{0}" – The search text for this condition must appear directly at the beginning of the item name. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
                if(isUsed_isEndAnchor) buttons_usedModifiers.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingRight24, null, string.Format(config.L[100_002_009] /* Character to force match at end of text: "{0}" – The search text for this condition must appear directly at the end of the item name. */, config.endAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2));
            }
            #endregion
            #region 4. Available chars - cheat-sheet
            if(guiShowAvailableCharsList) {
                if(config.orChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.ArrowSplit24, config.orChar?.ToString() ?? "", string.Format(config.L[100_002_001] /* Character for global OR operator: "{0}" – Splits the search string into independent branches. If at least one branch matches, an item is found. */, config.orChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.andChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.AddCircle24, config.andChar?.ToString() ?? "", string.Format(config.L[100_002_002] /* Character for AND operator: "{0}" – Combines conditions within a search branch. All conditions must be met for the branch to match. */, config.andChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.localOrChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.SplitVertical24, config.localOrChar?.ToString() ?? "", string.Format(config.L[100_002_003] /* Character for local OR operator: "{0}" – Allows alternative terms within a single condition. At least one term must match for the condition to pass. */, config.localOrChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));

                if(config.escapeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TooltipQuote24, config.escapeChar?.ToString() ?? "", string.Format(config.L[100_002_004] /* Character for escaping: "{0}" – Overrides the special function of the following control character and searches it as normal text. */, config.escapeChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                if(config.quoteChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextQuote24, config.quoteChar?.ToString() ?? "", string.Format(config.L[100_002_014] /* Character for quoting: "{0}" – Overrides the special function of all control characters in the enclosed text and searches it as normal text. */, config.quoteChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                if(config.notChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EyeOff24, config.notChar?.ToString() ?? "", string.Format(config.L[100_002_005] /* Character for negation: "{0}" – Inverts the condition. For the condition to be met, the corresponding terms must not be contained. */, config.notChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                if(config.caseSensitiveDynamic) {
                    if(config.invertCaseChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, config.invertCaseChar?.ToString() ?? "", string.Format(config.L[100_002_006] /* Character to toggle case sensitivity: "{0}" – Although active by default, case sensitivity is ignored in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                } else {
                    if(config.invertCaseChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, config.invertCaseChar?.ToString() ?? "", string.Format(config.L[100_002_007] /* Character to toggle case sensitivity: "{0}" – Although ignored by default, case sensitivity is observed in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                }
                if(config.startAnchorChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, config.startAnchorChar?.ToString() ?? "", string.Format(config.L[100_002_008] /* Character to force match at start of text: "{0}" – The search text for this condition must appear directly at the beginning of the item name. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                if(config.endAnchorChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingRight24, config.endAnchorChar?.ToString() ?? "", string.Format(config.L[100_002_009] /* Character to force match at end of text: "{0}" – The search text for this condition must appear directly at the end of the item name. */, config.endAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));

                if(config.standardModeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EqualCircle24, config.standardModeChar?.ToString() ?? "", string.Format(config.L[100_002_010] /* Character for standard search: "{0}" – Searches for the exact text anywhere within the item name. */, config.standardModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.regexModeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Code24, config.regexModeChar?.ToString() ?? "", string.Format(config.L[100_002_011] /* Character for regex search: "{0}" – Interprets the text as a regular expression and searches with it. */, config.regexModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.patternModeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, config.patternModeChar?.ToString() ?? "", string.Format(config.L[100_002_017] /* Character for pattern search: "{0}" – Interprets the text as a simplified pattern and searches with it. */, config.patternModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.fuzzyModeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TargetArrow24, config.fuzzyModeChar?.ToString() ?? "", string.Format(config.L[100_002_012] /* Character for fuzzy search: "{0}" – Tolerates typos using an automatic Levenshtein distance. */, config.fuzzyModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                if(config.sequenceModeChar.HasValue) buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Highlight24, config.sequenceModeChar?.ToString() ?? "", string.Format(config.L[100_002_013] /* Character for sequence search: "{0}" – Searches for characters in the specified order, allowing gaps in between. */, config.sequenceModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));

                if(config.metadataChar.HasValue) {
                    buttons_availableCharsList.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Tag24, config.metadataChar?.ToString() ?? "", string.Format(config.L[100_002_104] /* Character for metadata: "{0}" – Switches the search context to the specified metadata alias and its associated fields. */, config.metadataChar?.ToString() ?? ""), ButtonStyle.MetadataAlias, margin_2_2, ButtonActionType.InsertText));

                    foreach(var kvp in config._metadataAliasMappings) {
                        string metadataToolTip = config._metadataAliasTooltip.TryGetValue(kvp.Key, out string tooltip) ? tooltip : "";
                        buttons_availableCharsList.Add(CreateButton(null, $"{config.metadataChar}{kvp.Key}", metadataToolTip, ButtonStyle.MetadataAlias, margin_2_2, ButtonActionType.AutoCompleteText));
                    }
                }
            }
            #endregion
            #region 5. Dynamic Hints
            if(guiShowDynamicHints) {

                // 1. Escaping active
                if(searchQueryPlan.tokenizationState.isLastCharEscapeChar) {
                    if(config.escapeChar.HasValue) {
                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TooltipQuote24, config.L[100_004_001] /* Escaping active */, string.Format(config.L[100_004_003] /* The character "{0}" activated escaping – The special function of the next character is disabled and it will be searched as normal text. */, config.escapeChar.Value), ButtonStyle.Modifier, margin_2_2));
                        searchQueryPlan.inputContext = InputContext.None;
                    }
                }

                // 2. Quoting active
                if(searchQueryPlan.tokenizationState.activeQuoteDepth != 0) {
                    if(config.quoteChar.HasValue) {
                        string closingQuotes = new string(config.quoteChar.Value, searchQueryPlan.tokenizationState.activeQuoteDepth);
                        string tooltip = string.Format(config.L[100_004_004] /* The character "{0}" started quoting – The special function of all control characters inside the block is disabled and text will be searched as normal text. Close quoting with {1}. */, config.quoteChar.Value, closingQuotes);

                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextQuote24, config.L[100_004_002] /* Quoting active */, tooltip, ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText, closingQuotes));
                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextQuote24, closingQuotes, tooltip, ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                        searchQueryPlan.inputContext = InputContext.None;
                    }
                }

                bool isStandardPath = searchQueryPlan.inputContext == InputContext.ConditionStart ||
                                     searchQueryPlan.inputContext == InputContext.ConditionModifier ||
                                     searchQueryPlan.inputContext == InputContext.ConditionSearchMode ||
                                     searchQueryPlan.inputContext == InputContext.ConditionInTerm ||
                                     searchQueryPlan.inputContext == InputContext.ConditionInTermRegexOrPattern;

                // 3. Condition start
                if(searchQueryPlan.inputContext == InputContext.ConditionStart) {
                    if(config.metadataChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Tag24, config.metadataChar?.ToString() ?? "", string.Format(config.L[100_002_104] /* Character for metadata: "{0}" – Switches the search context to the specified metadata alias and its associated fields. */, config.metadataChar?.ToString() ?? ""), ButtonStyle.MetadataAlias, margin_2_2, ButtonActionType.InsertText));
                }

                // 4. Modifiers and search modes (only for ConditionStart and ConditionModifier)
                if(searchQueryPlan.inputContext == InputContext.ConditionStart || searchQueryPlan.inputContext == InputContext.ConditionModifier) {
                    string typedModifiers = searchQueryPlan.inputContextText;

                    // Modifiers
                    if(config.notChar.HasValue && !typedModifiers.Contains(config.notChar.Value)) {
                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EyeOff24, config.notChar?.ToString() ?? "", string.Format(config.L[100_002_005] /* Character for negation: "{0}" – Inverts the condition. For the condition to be met, the corresponding terms must not be contained. */, config.notChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                    }
                    if(config.invertCaseChar.HasValue && !typedModifiers.Contains(config.invertCaseChar.Value)) {
                        if(config.caseSensitiveDynamic) {
                            buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, config.invertCaseChar?.ToString() ?? "", string.Format(config.L[100_002_006] /* Character to toggle case sensitivity: "{0}" – Although active by default, case sensitivity is ignored in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                        } else {
                            buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, config.invertCaseChar?.ToString() ?? "", string.Format(config.L[100_002_007] /* Character to toggle case sensitivity: "{0}" – Although ignored by default, case sensitivity is observed in this condition due to the toggle character. */, config.invertCaseChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                        }
                    }
                    if(config.startAnchorChar.HasValue && !typedModifiers.Contains(config.startAnchorChar.Value)) {
                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, config.startAnchorChar?.ToString() ?? "", string.Format(config.L[100_002_008] /* Character to force match at start of text: "{0}" – The search text for this condition must appear directly at the beginning of the item name. */, config.startAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                    }
                    if(config.endAnchorChar.HasValue && !typedModifiers.Contains(config.endAnchorChar.Value)) {
                        buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingRight24, config.endAnchorChar?.ToString() ?? "", string.Format(config.L[100_002_009] /* Character to force match at end of text: "{0}" – The search text for this condition must appear directly at the end of the item name. */, config.endAnchorChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                    }

                    // Search modes
                    if(config.standardModeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EqualCircle24, config.standardModeChar?.ToString() ?? "", string.Format(config.L[100_002_010] /* Character for standard search: "{0}" – Searches for the exact text anywhere within the item name. */, config.standardModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.regexModeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Code24, config.regexModeChar?.ToString() ?? "", string.Format(config.L[100_002_011] /* Character for regex search: "{0}" – Interprets the text as a regular expression and searches with it. */, config.regexModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.patternModeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, config.patternModeChar?.ToString() ?? "", string.Format(config.L[100_002_017] /* Character for pattern search: "{0}" – Interprets the text as a simplified pattern and searches with it. */, config.patternModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.fuzzyModeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TargetArrow24, config.fuzzyModeChar?.ToString() ?? "", string.Format(config.L[100_002_012] /* Character for fuzzy search: "{0}" – Tolerates typos using an automatic Levenshtein distance. */, config.fuzzyModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.sequenceModeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Highlight24, config.sequenceModeChar?.ToString() ?? "", string.Format(config.L[100_002_013] /* Character for sequence search: "{0}" – Searches for characters in the specified order, allowing gaps in between. */, config.sequenceModeChar?.ToString() ?? ""), ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                }

                // 5. Default search text hint
                if(isStandardPath) {
                    buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Search24, config.L[100_004_010] /* Search text */, config.L[100_004_011] /* Enter your search text. */, ButtonStyle.SearchText, margin_2_2));
                }

                // 6. First search character entered in term
                if(searchQueryPlan.inputContext == InputContext.ConditionInTerm || searchQueryPlan.inputContext == InputContext.ConditionInTermRegexOrPattern) {
                    if(searchQueryPlan.inputContext != InputContext.ConditionInTermRegexOrPattern && config.localOrChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.SplitVertical24, config.localOrChar?.ToString() ?? "", string.Format(config.L[100_002_003] /* Character for local OR operator: "{0}" – Allows alternative terms within a single condition. At least one term must match for the condition to pass. */, config.localOrChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.andChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.AddCircle24, config.andChar?.ToString() ?? "", string.Format(config.L[100_002_002] /* Character for AND operator: "{0}" – Combines conditions within a search branch. All conditions must be met for the branch to match. */, config.andChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.orChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.ArrowSplit24, config.orChar?.ToString() ?? "", string.Format(config.L[100_002_001] /* Character for global OR operator: "{0}" – Splits the search string into independent branches. If at least one branch matches, an item is found. */, config.orChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                }

                // 7. Offer escaping (\) and quoting (") across the default path
                if(isStandardPath) {
                    if(config.escapeChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TooltipQuote24, config.escapeChar?.ToString() ?? "", string.Format(config.L[100_002_004] /* Character for escaping: "{0}" – Overrides the special function of the following control character and searches it as normal text. */, config.escapeChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                    if(config.quoteChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextQuote24, config.quoteChar?.ToString() ?? "", string.Format(config.L[100_002_014] /* Character for quoting: "{0}" – Overrides the special function of all control characters in the enclosed text and searches it as normal text. */, config.quoteChar?.ToString() ?? ""), ButtonStyle.Modifier, margin_2_2, ButtonActionType.InsertText));
                }

                // 8. Typing metadata alias
                if(searchQueryPlan.inputContext == InputContext.MetadataAliasTyping && config.metadataChar.HasValue) {
                    foreach(var kvp in config._metadataAliasMappings) {
                        if(kvp.Key.StartsWith(searchQueryPlan.inputContextText, StringComparison.OrdinalIgnoreCase)) {
                            string metadataToolTip = config._metadataAliasTooltip.TryGetValue(kvp.Key, out string tooltip) ? tooltip : "";
                            buttons_dynamicHints.Add(CreateButton(null, $"{config.metadataChar}{kvp.Key}", metadataToolTip, ButtonStyle.MetadataAlias, margin_2_2, ButtonActionType.AutoCompleteText));
                        }
                    }

                    if(config.andChar.HasValue && searchQueryPlan.inputContextText.Length > 0) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.AddCircle24, config.andChar?.ToString() ?? "", string.Format(config.L[100_002_002] /* Character for AND operator: "{0}" – Combines conditions within a search branch. All conditions must be met for the branch to match. */, config.andChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                }

                // 9. Math operator expected (=, >, <)
                if(searchQueryPlan.inputContext == InputContext.MathOperatorExpected) {
                    if(config.metadataChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Tag24, config.metadataChar?.ToString() ?? "", string.Format(config.L[100_002_104] /* Character for metadata: "{0}" – Switches the search context to the specified metadata alias and its associated fields. */, config.metadataChar?.ToString() ?? ""), ButtonStyle.MetadataAlias, margin_2_2));

                    buttons_dynamicHints.Add(CreateButton(null, "=", config.L[100_004_020] /* Equals operator: "=" – Filters by value. If no operator is specified, equality is used automatically. */, ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, ">", config.L[100_004_021] /* Greater-than operator: ">" – Filters by values greater than the specified value. */, ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "<", config.L[100_004_022] /* Less-than operator: "<" – Filters by values less than the specified value. */, ButtonStyle.SearchOperator, margin_2_2, ButtonActionType.InsertText));
                }

                // 10. Value input expected
                if(searchQueryPlan.inputContext == InputContext.MathInputExpected || searchQueryPlan.inputContext == InputContext.MathAgeUnitExpected || searchQueryPlan.inputContext == InputContext.MathSizeUnitExpected) {
                    buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Search24, $"3{config.decimalSeparatorChar?.ToString() ?? "."}14", string.Format(config.L[100_004_023] /* Enter a numeric value. Use "{0}" as the decimal separator. */, config.decimalSeparatorChar?.ToString() ?? "."), ButtonStyle.SearchText, margin_2_2));
                }

                // 11. Size unit expected (B, K, M, G, T, P, E)
                if(searchQueryPlan.inputContext == InputContext.MathSizeUnitExpected) {
                    buttons_dynamicHints.Add(CreateButton(null, "B", config.L[100_004_030] /* Unit: Byte     – Measures size in bytes.      The suffix "B" is optional. */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "K", config.L[100_004_031] /* Unit: Kilobyte – Measures size in kilobytes. Valid suffixes: "K" or "KB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "M", config.L[100_004_032] /* Unit: Megabyte – Measures size in megabytes. Valid suffixes: "M" or "MB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "G", config.L[100_004_033] /* Unit: Gigabyte – Measures size in gigabytes. Valid suffixes: "G" or "GB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "T", config.L[100_004_034] /* Unit: Terabyte – Measures size in terabytes. Valid suffixes: "T" or "TB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "P", config.L[100_004_035] /* Unit: Petabyte – Measures size in petabytes. Valid suffixes: "P" or "PB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, "E", config.L[100_004_036] /* Unit: Exabyte  – Measures size in exabytes.  Valid suffixes: "E" or "EB". */, ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                }

                // 12. Age unit expected (Seconds, Minutes, Hours, Days, Weeks, Months, Years)
                if(searchQueryPlan.inputContext == InputContext.MathAgeUnitExpected) {
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_231], string.Format(config.L[100_004_040] /* Unit: Seconds – Measures age in seconds. Suffix: "{0}".             */, config.L[100_002_231] /* S  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_232], string.Format(config.L[100_004_041] /* Unit: Minutes – Measures age in minutes. Suffix: "{0}".             */, config.L[100_002_232] /* M  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_233], string.Format(config.L[100_004_042] /* Unit: Hours   – Measures age in hours.   Suffix: "{0}".             */, config.L[100_002_233] /* H  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_234], string.Format(config.L[100_004_043] /* Unit: Days    – Measures age in days. The suffix "{0}" is optional. */, config.L[100_002_234] /* D  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_235], string.Format(config.L[100_004_044] /* Unit: Weeks   – Measures age in weeks.   Suffix: "{0}".             */, config.L[100_002_235] /* W  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_236], string.Format(config.L[100_004_045] /* Unit: Months  – Measures age in months.  Suffix: "{0}".             */, config.L[100_002_236] /* MO */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                    buttons_dynamicHints.Add(CreateButton(null, config.L[100_002_237], string.Format(config.L[100_004_046] /* Unit: Years   – Measures age in years.   Suffix: "{0}".             */, config.L[100_002_237] /* Y  */), ButtonStyle.SearchText, margin_2_2, ButtonActionType.InsertText));
                }

                // 13. Logical operators available after numeric input
                if(searchQueryPlan.inputContext == InputContext.MathAgeUnitExpected || searchQueryPlan.inputContext == InputContext.MathSizeUnitExpected) {
                    if(config.andChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.AddCircle24, config.andChar?.ToString() ?? "", string.Format(config.L[100_002_002] /* Character for AND operator: "{0}" – Combines conditions within a search branch. All conditions must be met for the branch to match. */, config.andChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                    if(config.orChar.HasValue) buttons_dynamicHints.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.ArrowSplit24, config.orChar?.ToString() ?? "", string.Format(config.L[100_002_001] /* Character for global OR operator: "{0}" – Splits the search string into independent branches. If at least one branch matches, an item is found. */, config.orChar?.ToString() ?? ""), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertText));
                }
            }
            #endregion
            #region 6. Fast and slow filter evaluation
            if(guiShowErrors || guiShowWarnings || guiShowInformations) {
                long evaluationCount = searchQueryPlan.benchmarkEvaluationCount;
                long totalExecutionTicks = searchQueryPlan.benchmarkTotalExecutionTicks;
                long totalStartupTicks = searchQueryPlan.benchmarkTotalStartupTicks;

                double totalExecutionMilliseconds = totalExecutionTicks > 0 ? (double)totalExecutionTicks / System.Diagnostics.Stopwatch.Frequency * 1000.0 : 0.0;
                double totalStartupMilliseconds = totalStartupTicks > 0 ? (double)totalStartupTicks / System.Diagnostics.Stopwatch.Frequency * 1000.0 : 0.0;
                double itemsPerSecond = totalExecutionMilliseconds > 0 ? (evaluationCount / totalExecutionMilliseconds) * 1000.0 : 0.0;
                double averageMillisecondsPerItem = evaluationCount > 0 ? totalExecutionMilliseconds / evaluationCount : 0.0;

                string formattedValue;
                if(totalExecutionTicks == 0 || evaluationCount == 0) {
                    formattedValue = "-";
                } else if(itemsPerSecond >= 1000.0) {
                    formattedValue = (itemsPerSecond / 1000.0).ToString("N1", CultureInfo.CurrentCulture);
                } else if(itemsPerSecond < 10.0) {
                    formattedValue = itemsPerSecond.ToString("N2", CultureInfo.CurrentCulture);
                } else {
                    formattedValue = itemsPerSecond.ToString("N0", CultureInfo.CurrentCulture);
                }

                string buttonText = itemsPerSecond >= 1000.0 && totalExecutionTicks > 0 && evaluationCount > 0
                    ? string.Format(config.L[100_003_002] /* {0} entries/ms */, formattedValue)
                    : string.Format(config.L[100_003_001] /* {0} entries/s */, formattedValue);

                string evaluationCountString = evaluationCount.ToString("N0", CultureInfo.CurrentCulture);
                string totalExecutionMillisecondsString = totalExecutionMilliseconds.ToString("N1", CultureInfo.CurrentCulture);
                string totalStartupMillisecondsString = totalStartupMilliseconds.ToString("N1", CultureInfo.CurrentCulture);
                string averageMillisecondsPerItemString = averageMillisecondsPerItem.ToString("N3", CultureInfo.CurrentCulture);

                if(totalExecutionMilliseconds >= 1000.0 && itemsPerSecond < 300.0) {
                    if(guiShowErrors) {
                        string tooltip = string.Format(config.L[100_003_004] /* Slow filter evaluation (values from previous search run)! {0} entries evaluated in {1} ms ({2} ms/entry). Plugin startup time: {3} ms. Possible causes: Extensive logging, searching in file contents/WDX fields or disabled cache. */, evaluationCountString, totalExecutionMillisecondsString, averageMillisecondsPerItemString, totalStartupMillisecondsString);
                        buttons_errors.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Gauge24, buttonText, tooltip, ButtonStyle.Error, margin_2_2));
                    }
                } else if(totalExecutionMilliseconds >= 300.0 && itemsPerSecond < 2000.0) {
                    if(guiShowWarnings) {
                        string tooltip = string.Format(config.L[100_003_004] /* Slow filter evaluation (values from previous search run)! {0} entries evaluated in {1} ms ({2} ms/entry). Plugin startup time: {3} ms. Possible causes: Extensive logging, searching in file contents/WDX fields or disabled cache. */, evaluationCountString, totalExecutionMillisecondsString, averageMillisecondsPerItemString, totalStartupMillisecondsString);
                        buttons_warnings.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Gauge24, buttonText, tooltip, ButtonStyle.Warning, margin_2_2));
                    }
                } else {
                    if(guiShowInformations) {
                        string tooltip = string.Format(config.L[100_003_003] /* Fast filter evaluation (values from previous search run): {0} entries evaluated in {1} ms ({2} ms/entry). Plugin startup time: {3} ms. The evaluation runs smoothly. */, evaluationCountString, totalExecutionMillisecondsString, averageMillisecondsPerItemString, totalStartupMillisecondsString);
                        buttons_informations.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Gauge24, buttonText, tooltip, ButtonStyle.Information, margin_2_2));
                    }
                }
            }

            if(guiShowWarnings) {
                if(config.loggingLevel > LoggingLevel.Level_3_SearchQueryPlan) buttons_warnings.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.DocumentTextClock24, config.L[100_003_010] /* Logging active */, config.L[100_003_011] /* Extensive logging is enabled. This can noticeably reduce search speed. Please disable or reduce it in the settings if it is not needed. */, ButtonStyle.Warning, margin_2_2));
            }
            #endregion
            #region 7. Text replacements section (Top 25 rules with 2+ characters)
            if(guiShowTextReplacements && config._stringReplacementsFilterAssistentWindowHints != null) {
                foreach(var kvp in config._stringReplacementsFilterAssistentWindowHints) {
                    buttons_textReplacements.Add(CreateButton(null, kvp.Key, string.Format(config.L[100_006_001] /* Text replacement: Searches for "{0}" and replaces it with "{1}". */, kvp.Key, kvp.Value), ButtonStyle.LogicalOperator, margin_2_2, ButtonActionType.InsertPreset, kvp.Value));
                }
            }
            #endregion
            #region 8. Quick access section
            if(guiShowQuickAccess) {
                // Actions & Tools
                buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.History24, null, config.L[100_007_001] /* Show search history */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.HistoryMenu));
                buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Clipboard24, null, config.L[100_007_010] /* Paste clipboard */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ClipboardMenu));
                buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Eraser24, null, config.L[100_007_020] /* Clear search bar */, searchQueryPlan.rawFilter.Length > 0 ? ButtonStyle.QuickAction : ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ClearSearchbar));

                // Options
                if(config.caseSensitiveDynamic) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, config.L[100_007_030] /* Case sensitivity enabled by default (Click to toggle) */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleCaseSensitive));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TextChangeCase24, null, config.L[100_007_031] /* Case sensitivity disabled by default (Click to toggle) */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleCaseSensitive));
                }
                if(config.matchFirstTermAsStartAnchorDynamic) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, config.L[100_007_040] /* The very first search term in the first search branch must appear directly at the beginning of the item name. (Click to toggle) */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleMatchFirstTermAsStartAnchor));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.PaddingLeft24, null, config.L[100_007_041] /* The very first search term in the first search branch does not need to appear directly at the beginning of the item name. (Click to toggle) */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleMatchFirstTermAsStartAnchor));
                }

                // Search Modes
                if(config.defaultSearchModeDynamic == SearchMode.Standard) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EqualCircle24, null, config.L[100_007_050] /* Set Standard search as default search mode */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleDefaultSearchModeStandard));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.EqualCircle24, null, config.L[100_007_050] /* Set Standard search as default search mode */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleDefaultSearchModeStandard));
                }
                if(config.defaultSearchModeDynamic == SearchMode.Regex) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Code24, null, config.L[100_007_051] /* Set Regex search as default search mode */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleDefaultSearchModeRegex));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Code24, null, config.L[100_007_051] /* Set Regex search as default search mode */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleDefaultSearchModeRegex));
                }
                if(config.defaultSearchModeDynamic == SearchMode.Pattern) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, null, config.L[100_007_052] /* Set Pattern search as default search mode */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleDefaultSearchModePattern));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, null, config.L[100_007_052] /* Set Pattern search as default search mode */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleDefaultSearchModePattern));
                }
                if(config.defaultSearchModeDynamic == SearchMode.Fuzzy) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TargetArrow24, null, config.L[100_007_053] /* Set Fuzzy search as default search mode */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleDefaultSearchModeFuzzy));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.TargetArrow24, null, config.L[100_007_053] /* Set Fuzzy search as default search mode */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleDefaultSearchModeFuzzy));
                }
                if(config.defaultSearchModeDynamic == SearchMode.Sequence) {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Highlight24, null, config.L[100_007_054] /* Set Sequence search as default search mode */, ButtonStyle.QuickAction, margin_2_2, ButtonActionType.ToggleDefaultSearchModeSequence));
                } else {
                    buttons_quickAccess.Add(CreateButton(Wpf.Ui.Controls.SymbolRegular.Highlight24, null, config.L[100_007_054] /* Set Sequence search as default search mode */, ButtonStyle.QuickActionDisabled, margin_2_2, ButtonActionType.ToggleDefaultSearchModeSequence));
                }
            }
            #endregion

            #region Add buttons to window
            if(!(this.FindName("buttonWrapPanel") is WrapPanel buttonWrapPanel)) return;

            buttonWrapPanel.Children.Clear();

            Button settingsButton = CreateButton(Wpf.Ui.Controls.SymbolRegular.Settings24, null, config.L[100_001_001] /* Open Settings */, ButtonStyle.Primary, null, ButtonActionType.OpenConfig);
            buttonWrapPanel.Children.Add(settingsButton);

            foreach(var guiSection in config.guiSections) {
                if(guiSection.guiSectionDisplayState == GuiSectionDisplayState.Hidden) continue;

                bool isExpanded = guiSection.guiSectionDisplayState == GuiSectionDisplayState.Expanded;

                switch(guiSection.guiSectionType) {
                    case GuiSectionType.Errors:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.DismissCircle24, config.L[100_001_002] /* Errors (e.g., invalid regex expressions in search text) – Click to toggle visibility */, buttons_errors, isExpanded);
                        break;

                    case GuiSectionType.Warnings:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Warning24, config.L[100_001_003] /* Warnings (performance hints for slow searches) – Click to toggle visibility */, buttons_warnings, isExpanded);
                        break;

                    case GuiSectionType.Informations:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Info24, config.L[100_001_004] /* Information (general performance metrics) – Click to toggle visibility */, buttons_informations, isExpanded);
                        break;

                    case GuiSectionType.SearchQueryPlan:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Search24, config.L[100_001_005] /* Search structure (visual representation of search logic) – Click to toggle visibility */, buttons_searchQueryPlan, isExpanded);
                        break;

                    case GuiSectionType.UsedModifiers:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.EditSettings24, config.L[100_001_006] /* Detected search special characters (in current search text) – Click to toggle visibility */, buttons_usedModifiers, isExpanded);
                        break;

                    case GuiSectionType.DynamicHints:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Lightbulb24, config.L[100_001_007] /* Input hints (dynamic suggestions based on current search text) – Click to toggle visibility */, buttons_dynamicHints, isExpanded);
                        break;

                    case GuiSectionType.AvailableCharsList:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Keyboard24, config.L[100_001_008] /* Available control characters (overview of all active characters) – Click to toggle visibility */, buttons_availableCharsList, isExpanded);
                        break;

                    case GuiSectionType.TextReplacements:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.ScreenSearch24, config.L[100_001_009] /* Text replacements (overview of active rules applying to the current filter text, min. 2 search characters) – Click to toggle visibility */, buttons_textReplacements, isExpanded);
                        break;

                    case GuiSectionType.QuickAccess:
                        AddScopeGroup(buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular.Flash24, config.L[100_001_010] /* Quick access (recent search history and direct search options) – Click to toggle visibility */, buttons_quickAccess, isExpanded);
                        break;
                }
            }
            #endregion
        }
        #endregion
        #region private Button CreateButton(Wpf.Ui.Controls.SymbolRegular? symbol, string text, string toolTip, ButtonStyle buttonStyle, Thickness? margin = null, ButtonActionType buttonActionType = ButtonActionType.None, string actionData = null)
        private Button CreateButton(Wpf.Ui.Controls.SymbolRegular? symbol, string text, string toolTip, ButtonStyle buttonStyle, Thickness? margin = null, ButtonActionType buttonActionType = ButtonActionType.None, string actionData = null)
        {
            #region Prepare visible Button
            UIElement content;

            if(symbol.HasValue && text != null) {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol.Value, FontSize = 20, VerticalAlignment = VerticalAlignment.Center });
                panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6 /* left */, 0 /* top */, 2 /* right */, 1 /* bottom */) });
                content = panel;
            } else if(symbol.HasValue) {
                content = new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol.Value, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
            } else {
                content = new TextBlock { Text = text ?? string.Empty, VerticalAlignment = VerticalAlignment.Center };
            }

            Button button = new Button { Content = content, ToolTip = toolTip, Style = buttonStyles[buttonStyle] };
            if(margin.HasValue) button.Margin = margin.Value;
            #endregion
            #region Attach Click Actions
            if(actionData == null) actionData = text;
            if(buttonActionType == ButtonActionType.OpenConfig) {
                button.Click += (s, e) => OpenConfig();
            } else if(buttonActionType == ButtonActionType.HistoryMenu) {
                button.Click += (s, e) => {
                    if(button.ContextMenu != null) button.ContextMenu.IsOpen = true;
                };
            } else if(buttonActionType == ButtonActionType.ClipboardMenu) {
                button.Click += (s, e) => {
                    if((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                        ReplaceAllSearchText(GetClipboardText());
                    } else {
                        InsertOrReplaceSelection(GetClipboardText());
                    }
                };
            } else if(buttonActionType == ButtonActionType.ClearSearchbar) {
                button.Click += (s, e) => ReplaceAllSearchText("");
            } else if(buttonActionType == ButtonActionType.ToggleCaseSensitive) {
                button.Click += (s, e) => { config.caseSensitiveDynamic = !config.caseSensitiveDynamic; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleMatchFirstTermAsStartAnchor) {
                button.Click += (s, e) => { config.matchFirstTermAsStartAnchorDynamic = !config.matchFirstTermAsStartAnchorDynamic; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleDefaultSearchModeStandard) {
                button.Click += (s, e) => { if(config.defaultSearchModeDynamic == SearchMode.Standard) return; config.defaultSearchModeDynamic = SearchMode.Standard; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleDefaultSearchModeRegex) {
                button.Click += (s, e) => { if(config.defaultSearchModeDynamic == SearchMode.Regex) return; config.defaultSearchModeDynamic = SearchMode.Regex; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleDefaultSearchModePattern) {
                button.Click += (s, e) => { if(config.defaultSearchModeDynamic == SearchMode.Pattern) return; config.defaultSearchModeDynamic = SearchMode.Pattern; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleDefaultSearchModeFuzzy) {
                button.Click += (s, e) => { if(config.defaultSearchModeDynamic == SearchMode.Fuzzy) return; config.defaultSearchModeDynamic = SearchMode.Fuzzy; ForceSearchReload(); };
            } else if(buttonActionType == ButtonActionType.ToggleDefaultSearchModeSequence) {
                button.Click += (s, e) => { if(config.defaultSearchModeDynamic == SearchMode.Sequence) return; config.defaultSearchModeDynamic = SearchMode.Sequence; ForceSearchReload(); };
            } else if(!string.IsNullOrEmpty(actionData)) {
                if(buttonActionType == ButtonActionType.InsertText) {
                    button.Click += (s, e) => InsertOrReplaceSelection(actionData);
                } else if(buttonActionType == ButtonActionType.AutoCompleteText) {
                    button.Click += (s, e) => AutoCompleteOrInsertToken(actionData);
                } else if(buttonActionType == ButtonActionType.InsertPreset) {
                    button.Click += (s, e) => {
                        if((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                            ReplaceAllSearchText(text);
                        } else {
                            InsertOrReplaceSelection(text);
                        }
                    };
                }
            }
            #endregion
            #region Attach context menu
            var contextMenu = new ContextMenu();
            MenuItem menuItem;
            if(buttonActionType == ButtonActionType.HistoryMenu) {
                if(Plugin.historyList.Count == 0) {
                    menuItem = new MenuItem { Header = config.L[100_007_002] /* (No search history available) */, IsEnabled = false };
                    contextMenu.Items.Add(menuItem);
                } else {
                    for(int i = Plugin.historyList.Count - 1; i >= 0; i--) {
                        string historyItemText = Plugin.historyList[i];
                        string shortHistoryText = historyItemText?.Length > 50 ? historyItemText.Substring(0, 50) + "..." : historyItemText;
                        menuItem = new MenuItem { Header = shortHistoryText };
                        menuItem.Click += (s, e) => {
                            if((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                                InsertOrReplaceSelection(historyItemText);
                            } else {
                                ReplaceAllSearchText(historyItemText);
                            }
                        };
                        contextMenu.Items.Add(menuItem);
                    }
                }
            } else if(buttonActionType == ButtonActionType.ClipboardMenu) {
                menuItem = new MenuItem { Header = config.L[100_007_011] /* Paste clipboard (Click) */ };
                menuItem.Click += (s, e) => InsertOrReplaceSelection(GetClipboardText());
                contextMenu.Items.Add(menuItem);

                menuItem = new MenuItem { Header = config.L[100_007_012] /* Replace search text with clipboard (Ctrl+Click) */ };
                menuItem.Click += (s, e) => ReplaceAllSearchText(GetClipboardText());
                contextMenu.Items.Add(menuItem);
            } else if(!string.IsNullOrEmpty(actionData)) {
                if(buttonActionType == ButtonActionType.InsertText) {
                    menuItem = new MenuItem { Header = string.Format(config.L[100_005_002] /* Insert "{0}" (Click) */, actionData) };
                    menuItem.Click += (s, e) => InsertOrReplaceSelection(actionData);
                    contextMenu.Items.Add(menuItem);
                } else if(buttonActionType == ButtonActionType.AutoCompleteText) {
                    menuItem = new MenuItem { Header = string.Format(config.L[100_005_003] /* Complete "{0}" (Click) */, actionData) };
                    menuItem.Click += (s, e) => AutoCompleteOrInsertToken(actionData);
                    contextMenu.Items.Add(menuItem);
                } else if(buttonActionType == ButtonActionType.InsertPreset) {
                    menuItem = new MenuItem { Header = string.Format(config.L[100_005_002] /* Insert "{0}" (Click) */, text) };
                    menuItem.Click += (s, e) => InsertOrReplaceSelection(text);
                    contextMenu.Items.Add(menuItem);

                    menuItem = new MenuItem { Header = string.Format(config.L[100_006_002] /* Replace search text with "{0}" (Ctrl+Click) */, text) };
                    menuItem.Click += (s, e) => ReplaceAllSearchText(text);
                    contextMenu.Items.Add(menuItem);

                    string shortActionData = actionData?.Length > 25 ? actionData.Substring(0, 25) + "..." : actionData;

                    menuItem = new MenuItem { Header = string.Format(config.L[100_006_003] /* Insert expanded text ("{0}") */, shortActionData) };
                    menuItem.Click += (s, e) => InsertOrReplaceSelection(actionData);
                    contextMenu.Items.Add(menuItem);
                }
            }

            if(!string.IsNullOrEmpty(toolTip) && buttonActionType != ButtonActionType.HistoryMenu) {
                if(contextMenu.Items.Count > 0) contextMenu.Items.Add(new Separator());

                menuItem = new MenuItem { Header = config.L[100_005_001] /* Copy tooltip to clipboard */ };
                menuItem.Click += (s, e) => SetClipboardText(toolTip);
                contextMenu.Items.Add(menuItem);
            }

            if(contextMenu.Items.Count > 0) {
                button.ContextMenu = contextMenu;

                button.PreviewMouseRightButtonUp += (s, e) => {
                    button.ContextMenu.IsOpen = true;
                    e.Handled = true;
                };

                button.Unloaded += (s, e) => {
                    if(button.ContextMenu != null) button.ContextMenu.IsOpen = false;
                };
            }
            #endregion

            return button;
        }
        #endregion
        #region private void AddScopeGroup(WrapPanel buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular symbol, string toolTip, List<Button> buttons, bool isInitiallyVisible)
        private void AddScopeGroup(WrapPanel buttonWrapPanel, Wpf.Ui.Controls.SymbolRegular symbol, string toolTip, List<Button> buttons, bool isInitiallyVisible)
        {
            if(buttons == null || buttons.Count == 0) return;

            var chevronSymbol = isInitiallyVisible ? Wpf.Ui.Controls.SymbolRegular.ChevronLeft20 : Wpf.Ui.Controls.SymbolRegular.ChevronRight20;

            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol, FontSize = 20, VerticalAlignment = VerticalAlignment.Center });

            var chevronIcon = new Wpf.Ui.Controls.SymbolIcon { Symbol = chevronSymbol, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2 /* left */, 0 /* top */, 0 /* right */, 0 /* bottom */) };
            panel.Children.Add(chevronIcon);

            Button scopeButton = new Button { Content = panel, ToolTip = toolTip, Style = buttonStyles[ButtonStyle.Scope], Tag = buttons, Margin = new Thickness(buttonWrapPanel.Children.Count != 0 ? 10 : 0 /* left */, 0 /* top */, 0 /* right */, 0 /* bottom */) };

            // Initialen Status setzen
            SetGroupVisibility(buttons, isInitiallyVisible);

            scopeButton.Click += (s, e) => {
                if(s is Button button && button.Tag is List<Button> targetButtons) {
                    bool newVisibleState = !(targetButtons.FirstOrDefault()?.Visibility == Visibility.Visible);

                    SetGroupVisibility(targetButtons, newVisibleState);
                    chevronIcon.Symbol = newVisibleState ? Wpf.Ui.Controls.SymbolRegular.ChevronLeft20 : Wpf.Ui.Controls.SymbolRegular.ChevronRight20;
                }
            };

            buttonWrapPanel.Children.Add(scopeButton);
            foreach(var item in buttons) buttonWrapPanel.Children.Add(item);
        }
        #endregion
        #region private void SetGroupVisibility(List<Button> buttons, bool visible)
        private void SetGroupVisibility(List<Button> buttons, bool visible)
        {
            var targetVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
            foreach(var button in buttons) button.Visibility = targetVisibility;
        }
        #endregion

        #region protected override void OnSourceInitialized(EventArgs e)
        /// <summary>
        /// Injects Win32 styles into the window handle to completely disable UI focus activation.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Use InteropHelper to modify the native window styles
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            // WS_EX_NOACTIVATE: Prevents the window from becoming the foreground window when clicked
            // WS_EX_TOOLWINDOW: Hides it from the Alt-Tab menu
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;

            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

            // Start background handle validation thread loop
            InitializeWindowMonitor();
        }
        #endregion
        #region private void InitializeWindowMonitor()
        /// <summary>
        /// Registers and boots the high-frequency UI polling timer to track the lifecycle of the TQUICKSEARCH handle.
        /// </summary>
        private void InitializeWindowMonitor()
        {
            activeWindowMonitorTimer = new System.Windows.Threading.DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            activeWindowMonitorTimer.Tick += (s, e) => {
                // If the assistant is already hidden, there is no need to evaluate further states
                if(!this.IsVisible) return;

                // Evaluate high-priority persistence overrides first
                if(isConfigOpen) return;
                if(config.pinGui) return;

                // Validate the handle and purge it if the native window was destroyed
                if(tcQuickSearchHandle != IntPtr.Zero && !NativeMethods.IsWindow(tcQuickSearchHandle)) {
                    tcQuickSearchHandle = IntPtr.Zero;
                    Plugin.FlushHistoryOnClose();
                }

                // If the quick search panel is gone, immediately withdraw the assistant
                if(tcQuickSearchHandle == IntPtr.Zero) {
                    this.Hide();
                    return;
                }

                // If TC search is alive, visibility depends entirely on showGui settings or @gui metadata trigger
                if(!config.showGui && !config.usedMetadataGui) {
                    this.Hide();
                }
            };

            activeWindowMonitorTimer.Start();
        }
        #endregion
        #region private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if(sender is ScrollViewer scrollViewer) {
                e.Handled = true;

                double offset = scrollViewer.VerticalOffset - (e.Delta > 0 ? 32.0 : -32.0);
                scrollViewer.ScrollToVerticalOffset(offset);
            }
        }
        #endregion

        #region private void FindWindowHandles()
        /// <summary>
        /// Validates existing handles and scans for new ones only if necessary, stopping immediately upon success.
        /// </summary>
        private void FindWindowHandles()
        {
            // Check if our cached handles are still alive and valid in Windows
            if(tcMainHandle != IntPtr.Zero && tcQuickSearchHandle != IntPtr.Zero && NativeMethods.IsWindow(tcQuickSearchHandle)) {
                return; // Both handles are perfectly valid. Exit instantly!
            }

            // If we reach here, the quick search handle was either never found or closed/reopened.
            tcQuickSearchHandle = IntPtr.Zero;
            uint currentPid = NativeMethods.GetCurrentProcessId();
            StringBuilder className = new StringBuilder(256);

            NativeMethods.EnumWindows((hWnd, lParam) => {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);

                if(windowPid == currentPid) {
                    className.Clear();
                    // Ensure GetClassName actually succeeded before evaluating the string
                    if(NativeMethods.GetClassName(hWnd, className, className.Capacity) > 0) {
                        string clsName = className.ToString();

                        if(clsName == "TQUICKSEARCH" && tcQuickSearchHandle == IntPtr.Zero) {
                            tcQuickSearchHandle = hWnd;
                        } else if(clsName == "TTOTAL_CMD" && tcMainHandle == IntPtr.Zero) {
                            tcMainHandle = hWnd;
                        }

                        // Stop loop immediately as soon as we have both handles collected
                        if(tcQuickSearchHandle != IntPtr.Zero && tcMainHandle != IntPtr.Zero) {
                            return false;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        #endregion
        #region private void FindDockingCoordinates()
        /// <summary>
        /// Reads and updates the coordinates of the TC windows and the bounding monitor using native Win32.
        /// </summary>
        private void FindDockingCoordinates()
        {
            if(tcMainHandle != IntPtr.Zero) NativeMethods.GetWindowRect(tcMainHandle, out tcMainRect);
            if(tcQuickSearchHandle != IntPtr.Zero) NativeMethods.GetWindowRect(tcQuickSearchHandle, out tcQuickSearchRect);

            if(tcMainHandle != IntPtr.Zero) {
                IntPtr hMonitor = NativeMethods.MonitorFromWindow(tcMainHandle, 2 /* MONITOR_DEFAULTTONEAREST = 0x00000002 */);
                if(hMonitor != IntPtr.Zero) {
                    NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO {
                        cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
                    };

                    if(NativeMethods.GetMonitorInfo(hMonitor, ref info)) {
                        desktopRect = info.rcMonitor; // Pure physical screen bounds, completely ignoring taskbar
                    }
                }
            }
        }
        #endregion
        #region public bool SetWindowPosition(int guiSizeX, int guiSizeY, int guiOffsetX, int guiOffsetY, GuiDockingTarget guiDocking, GuiDockingCorner guiDockingCorner)
        /// <summary>
        /// Calculates coordinates based on targets and returns true only if the final window bounding box rests within the active monitor bounds.
        /// </summary>
        public bool SetWindowPosition(int guiSizeX, int guiSizeY, int guiOffsetX, int guiOffsetY, GuiDockingTarget guiDocking, GuiDockingCorner guiDockingCorner)
        {
            // Always ensure dimensions are updated from configuration before relocating
            this.Width = guiSizeX;
            this.Height = guiSizeY;

            NativeMethods.RECT activeRect = new NativeMethods.RECT();

            if(guiDocking == GuiDockingTarget.TotalCommanderMainWindow) {
                activeRect = tcMainRect;
            } else if(guiDocking == GuiDockingTarget.TotalCommanderSearchWindow) {
                activeRect = tcQuickSearchRect;
            } else if(guiDocking == GuiDockingTarget.DesktopScreen) {
                activeRect = desktopRect;
            }

            var source = PresentationSource.FromVisual(this);
            double scaleX = 1.0;
            double scaleY = 1.0;

            if(source != null && source.CompositionTarget != null) {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Determine the base anchor point depending on the selected corner
            double anchorX = activeRect.Left;
            double anchorY = activeRect.Top;

            if(guiDockingCorner == GuiDockingCorner.TopRight) {
                anchorX = activeRect.Right;
            } else if(guiDockingCorner == GuiDockingCorner.BottomLeft) {
                anchorY = activeRect.Bottom;
            } else if(guiDockingCorner == GuiDockingCorner.BottomRight) {
                anchorX = activeRect.Right;
                anchorY = activeRect.Bottom;
            }

            // Combine DPI-scaled win32 anchor positions with raw configuration offsets
            double targetX = (anchorX / scaleX) + guiOffsetX;
            double targetY = (anchorY / scaleY) + guiOffsetY;

            this.Left = targetX;
            this.Top = targetY;

            // Calculate physical coordinates directly from native anchors and scaled config offsets
            // Math.Round prevents precision loss from double-to-int conversion
            int physicalFinalLeft = (int)(anchorX + Math.Round(guiOffsetX * scaleX));
            int physicalFinalTop = (int)(anchorY + Math.Round(guiOffsetY * scaleY));
            int physicalFinalRight = physicalFinalLeft + (int)Math.Round(guiSizeX * scaleX);
            int physicalFinalBottom = physicalFinalTop + (int)Math.Round(guiSizeY * scaleY);

            // Validation with a 2-pixel tolerance buffer to catch edge-rounding errors on high-DPI
            if(physicalFinalLeft >= (desktopRect.Left - 2) && physicalFinalTop >= (desktopRect.Top - 2) && physicalFinalRight <= (desktopRect.Right + 2) && physicalFinalBottom <= (desktopRect.Bottom + 2)) {
                return true;
            } else {
                return false;
            }
        }
        #endregion

        // tcQuickSearchEditHandle - simple Functions
        #region private bool GetEditControlHandle()
        private bool GetEditControlHandle()
        {
            tcQuickSearchEditHandle = IntPtr.Zero;

            if(tcQuickSearchHandle == IntPtr.Zero || !NativeMethods.IsWindow(tcQuickSearchHandle)) return false;

            // 1. Locate the TPanel child control within TQUICKSEARCH
            IntPtr hPanel = NativeMethods.FindWindowEx(tcQuickSearchHandle, IntPtr.Zero, "TPanel", null);
            if(hPanel == IntPtr.Zero) return false;

            // 2. Locate the TTabEdit child control within TPanel
            tcQuickSearchEditHandle = NativeMethods.FindWindowEx(hPanel, IntPtr.Zero, "TTabEdit", null);
            if(tcQuickSearchEditHandle == IntPtr.Zero) return false;

            return true;
        }
        #endregion
        #region private string GetSearchText()
        private string GetSearchText()
        {
            int length = NativeMethods.SendMessage(tcQuickSearchEditHandle, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32();
            if(length <= 0) return string.Empty;

            StringBuilder sb = new StringBuilder(length + 1);
            NativeMethods.SendMessage(tcQuickSearchEditHandle, NativeMethods.WM_GETTEXT, (IntPtr)sb.Capacity, sb);
            return sb.ToString();
        }
        #endregion
        #region private void SetSearchText(string newText)
        private void SetSearchText(string newText)
        {
            // Direct atomic string replacement
            NativeMethods.SendMessage(tcQuickSearchEditHandle, NativeMethods.WM_SETTEXT, IntPtr.Zero, newText ?? string.Empty);
        }
        #endregion
        #region private (int start, int end) GetCaretPosition()
        private (int start, int end) GetCaretPosition()
        {
            int start = 0;
            int end = 0;
            NativeMethods.SendMessage(tcQuickSearchEditHandle, NativeMethods.EM_GETSEL, ref start, ref end);
            return (start, end);
        }
        #endregion
        #region private void SetCaretPosition(int start, int end)
        private void SetCaretPosition(int start, int end)
        {
            NativeMethods.SendMessage(tcQuickSearchEditHandle, NativeMethods.EM_SETSEL, (IntPtr)start, (IntPtr)end);
        }
        #endregion

        // tcQuickSearchEditHandle
        #region public void ForceSearchReload()
        public void ForceSearchReload()
        {
            try {
                if(!GetEditControlHandle()) return;

                Plugin.FlushHistoryOnClose();
                Plugin.lastRawFilter = null; // Recalculate searchQueryPlan on next call

                string currentText = GetSearchText();
                var (start, end) = GetCaretPosition();

                // Clear first to break equality check and reset TC's search state instantly
                SetSearchText(currentText != "" ? "" : " ");
                SetSearchText(currentText);

                SetCaretPosition(start, end);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not force search reload in TC Quick Search Window: {ex.Message}{Environment.NewLine}{ex}");
            }
        }
        #endregion
        #region public void InsertOrReplaceSelection(string newText)
        public void InsertOrReplaceSelection(string newText)
        {
            try {
                if(!GetEditControlHandle()) return;

                if(newText == null) newText = string.Empty;

                string currentText = GetSearchText();
                var (start, end) = GetCaretPosition();

                // Calculate new string by replacing selection or inserting at caret
                string updatedText;
                int newCaretPos;

                if(start < currentText.Length) {
                    int selectionLength = Math.Max(0, end - start);
                    updatedText = currentText.Remove(start, selectionLength).Insert(start, newText);
                    newCaretPos = start + newText.Length;
                } else {
                    updatedText = currentText + newText;
                    newCaretPos = updatedText.Length;
                }

                SetSearchText(updatedText);
                SetCaretPosition(newCaretPos, newCaretPos);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not insert text in TC Quick Search Window: {ex.Message}{Environment.NewLine}{ex}");
            }
        }
        #endregion
        #region public void AutoCompleteOrInsertToken(string token)
        public void AutoCompleteOrInsertToken(string token)
        {
            try {
                if(!GetEditControlHandle()) return;

                string currentText = GetSearchText();
                var (start, end) = GetCaretPosition();

                // Handle active selection by simply overwriting it
                if(string.IsNullOrEmpty(token) || start != end) {
                    InsertOrReplaceSelection(token);
                    return;
                }

                // Inspect the text preceding the caret for partial matching
                int matchedPrefixLength = 0;
                int maxPossiblePrefix = Math.Min(start, token.Length - 1);

                for(int i = maxPossiblePrefix; i > 0; i--) {
                    string textBeforeCaret = currentText.Substring(start - i, i);

                    if(token.StartsWith(textBeforeCaret, StringComparison.OrdinalIgnoreCase)) {
                        matchedPrefixLength = i;
                        break;
                    }
                }

                // Remove matched partial prefix and insert full token
                int replaceStartPos = start - matchedPrefixLength;
                string updatedText = currentText.Remove(replaceStartPos, matchedPrefixLength).Insert(replaceStartPos, token);
                int newCaretPos = replaceStartPos + token.Length;

                SetSearchText(updatedText);
                SetCaretPosition(newCaretPos, newCaretPos);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not autocomplete text in TC Quick Search Window: {ex.Message}{Environment.NewLine}{ex}");
            }
        }
        #endregion
        #region public void ReplaceAllSearchText(string newText)
        public void ReplaceAllSearchText(string newText)
        {
            try {
                if(!GetEditControlHandle()) return;

                Plugin.FlushHistoryOnClose();

                if(newText == null) newText = string.Empty;

                SetSearchText(newText);

                SetCaretPosition(newText.Length, newText.Length);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not replace all text in TC Quick Search Window: {ex.Message}{Environment.NewLine}{ex}");
            }
        }
        #endregion
        #region private void SetClipboardText(string text)
        private void SetClipboardText(string text)
        {
            try {
                Clipboard.SetText(text);
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not access clipboard (set): {ex.Message}{Environment.NewLine}{ex}");
            }
        }
        #endregion
        #region private static string GetClipboardText()
        private static string GetClipboardText()
        {
            try {
                if(Clipboard.ContainsText()) {
                    return Clipboard.GetText();
                }
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not access clipboard (get): {ex.Message}{Environment.NewLine}{ex}");
            }
            return null;
        }
        #endregion

        #region private void OpenConfig()
        private void OpenConfig()
        {
            try {
                ConfigWindow configWindow = new ConfigWindow(this) { Owner = this };
                Plugin.configWindow = configWindow;

                // Set the guard flag before opening the modal dialog
                isConfigOpen = true;

                Plugin.configWindow.ShowDialog();
            } catch(Exception ex) {
                Plugin.Log(LoggingLevel.Level_1_Errors, $"Error: Could not open configuration: {ex.Message}{Environment.NewLine}{ex}");

                System.Windows.MessageBox.Show($"Error opening configuration: {ex}");
                throw;
            } finally {
                // Safely reset the flag as soon as the modal window loop exits
                Plugin.configWindow = null;
                isConfigOpen = false;
            }
        }
        #endregion
    }
    #endregion
}

// Checklist for new Release:
//  > Change version string in source code above: #RELEASE
//  > Update CHANGELOG.md
//  > Update README.md and README.de.md (if necessary)
//     > Change version string in "tcmatch.Translation.##.cs": #RELEASE
//  > Build solution.
//  > Commit changes and push commit to origin
//  > Create new Release on GitHub (upload "QSX2 YYYY-MM-DD.zip", copy release notes from CHANGELOG.md)
//  > Update forum thread: https://www.ghisler.ch/board/viewtopic.php?t=22592


// ToDo:
//  > avoid [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)] in release build
//     > replace double by decimal?
//     > check if other places cause errors
//     > see also: WindowHelper.AllowLargeNumbers
