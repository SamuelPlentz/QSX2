#include "pch.h"
#include "tcmatch.h"

#define WIN32_LEAN_AND_MEAN 
#include <windows.h>

using namespace System;
using namespace System::IO;
using namespace System::Reflection;


#pragma region ResolveAssembly - Automatically look for "tcmatch.Core.dll" in the specific directory containing "tcmatch.dll" or "tcmatch64.dll"
static Assembly^ ResolveAssembly(Object^ sender, ResolveEventArgs^ args)
{
    if (args->Name->StartsWith("tcmatch.Core", StringComparison::OrdinalIgnoreCase))
    {
        String^ tcmatchDllDirectory = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        if (tcmatchDllDirectory != nullptr)
        {
            return Assembly::LoadFrom(Path::Combine(tcmatchDllDirectory, "tcmatch.Core.dll"));
        }
    }
    return nullptr;
}
#pragma endregion
#pragma region PluginInitialization - Initialization and event registration
struct PluginInitializer
{
    PluginInitializer()
    {
        AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(&ResolveAssembly);
    }
};

static PluginInitializer pluginInitializer;
#pragma endregion
#pragma region Total Commander Interface - Forwarding "MatchGetSetOptions" and "MatchFileExW" to CSharp Code in "tcmatch.Core.dll"
// Update "tcmatch.def" on interface changes

extern "C" int __stdcall MatchGetSetOptions(int status)
{
    return tcmatch::Core::Plugin::MatchGetSetOptions(status);
}

extern "C" int __stdcall MatchFileExW(const wchar_t* filter, const wchar_t* filename, int flags)
{
    return tcmatch::Core::Plugin::MatchFileExW(gcnew String(filter), gcnew String(filename), flags);
}

// "MatchFileW" is deprecated ("MatchFileExW" is used instead)
//extern "C" int __stdcall MatchFileW(const wchar_t* filter, const wchar_t* filename)
//{
//    return tcmatch::Core::Plugin::MatchFileW(gcnew String(filter), gcnew String(filename));
//}
#pragma endregion
