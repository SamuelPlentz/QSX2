#include "pch.h"
#include "tcmatch.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <vcclr.h>

using namespace System;
using namespace System::IO;
using namespace System::Reflection;
using namespace System::Text;
using namespace System::Globalization;

#pragma region Native Logging - Optional bridge logging configured through "tcmatch.path.txt"
static int loggingActive = 0;
static gcroot<String^> logFilePath;

// Append one line to the log file. Open and close for every entry.
static void Log(String^ message)
{
    try {
        String^ line = DateTime::Now.ToString("[yyyy-MM-dd HH:mm:ss.fff] ", CultureInfo::InvariantCulture) + message + Environment::NewLine;
        File::AppendAllText(logFilePath, line, gcnew UTF8Encoding(false));
    } catch(Exception^) {
        loggingActive = 0;
    }
}

// Enable native bridge logging only when "folder for tcmatch.bridge.log=" is specified.
static void InitializeLogging()
{
    try {
        String^ pluginFolder = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        String^ pathFile = Path::Combine(pluginFolder, "tcmatch.path.txt");

        if(!File::Exists(pathFile)) return;

        String^ prefix = "folder for tcmatch.bridge.log=";

        for each(String^ line in File::ReadAllLines(pathFile, Encoding::UTF8)) {
            if(!line->StartsWith(prefix)) continue;
            String^ folder = line->Substring(prefix->Length)->Trim();

            if(String::IsNullOrWhiteSpace(folder)) return;
            folder = Environment::ExpandEnvironmentVariables(folder);
            if(!Path::IsPathRooted(folder)) folder = Path::Combine(pluginFolder, folder);
            folder = Path::GetFullPath(folder);

            logFilePath = Path::Combine(folder, "tcmatch.bridge.log");
            loggingActive = 1;
            return;
        }
    } catch(Exception^) {
    }
}
#pragma endregion
#pragma region ResolveAssembly - Automatically look for "tcmatch.Core.dll" in the specific directory containing "tcmatch.dll" or "tcmatch64.dll"
static Assembly^ ResolveAssembly(Object^ sender, ResolveEventArgs^ args)
{
    if(args->Name->StartsWith("tcmatch.Core", StringComparison::OrdinalIgnoreCase)) {
        String^ tcmatchDllDirectory = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        if(tcmatchDllDirectory != nullptr) {
            return Assembly::LoadFrom(Path::Combine(tcmatchDllDirectory, "tcmatch.Core.dll"));
        }
    }
    return nullptr;
}
#pragma endregion
#pragma region PluginInitialization - Initialize bridge logging and register assembly resolver
struct PluginInitializer
{
    PluginInitializer()
    {
        InitializeLogging();
        if(loggingActive) Log("╔═════════════════════════════════════════════════╗");
        if(loggingActive) Log("║ QuickSearch eXtended 2 (C++ Bridge) initialized ║");
        if(loggingActive) Log("╚═════════════════════════════════════════════════╝");

        AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(&ResolveAssembly);
    }
};

static PluginInitializer pluginInitializer;
#pragma endregion
#pragma region Total Commander Interface - Forwarding "MatchGetSetOptions", "MatchFileExW" and "MatchNotify" to CSharp Code in "tcmatch.Core.dll"
// Update "tcmatch.def" on interface changes

extern "C" int __stdcall MatchGetSetOptions(int status)
{
    if(loggingActive) Log(String::Format("MatchGetSetOptions(Status: {0})", status));

    int result = tcmatch::Core::Plugin::MatchGetSetOptions(status);

    if(loggingActive) Log(String::Format("MatchGetSetOptions => {0}.", result));

    return result;
}

extern "C" void __stdcall MatchNotify(int flags, HWND handle)
{
    if(loggingActive) Log(String::Format("MatchNotify(Flags: {0}, Handle: 0x{1:X})", flags, IntPtr(handle).ToInt64()));

    tcmatch::Core::Plugin::MatchNotify(flags, IntPtr(handle));
}

extern "C" int __stdcall MatchFileExW(const wchar_t* filter, const wchar_t* filename, int flags)
{
    if(loggingActive) {
        String^ filterText = filter != nullptr ? gcnew String(filter) : "<null>";
        String^ filenameText = filename != nullptr ? gcnew String(filename) : "<null>";

        Log(String::Format("MatchFileExW(Filter: \"{0}\", Filename: \"{1}\", Flags: {2})", filterText, filenameText, flags));
    }

    int result = tcmatch::Core::Plugin::MatchFileExW(gcnew String(filter), gcnew String(filename), flags);

    if(loggingActive) Log(String::Format("MatchFileExW => {0}.", result));

    return result;
}

// "MatchFileW" is deprecated ("MatchFileExW" is used instead)
//extern "C" int __stdcall MatchFileW(const wchar_t* filter, const wchar_t* filename)
//{
//    return tcmatch::Core::Plugin::MatchFileW(gcnew String(filter), gcnew String(filename));
//}
#pragma endregion
