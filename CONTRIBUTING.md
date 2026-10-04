# 🤝 Contributing

Contributions to QSX2 are welcome! Whether you want to improve translations, fix a bug, optimize code, or add a feature, there are several ways to help.

## 🌍 Translations

Translation improvements are very welcome.

If you find a typo, bad wording, grammatical mistake, or anything that could be expressed better, simply report it in an issue.

The main translation-related files are:

```text
QSX2\README.md
QSX2\README.de.md
QSX2\tcmatch.Core\tcmatch.Core\tcmatch.Translation.DE.cs
QSX2\tcmatch.Core\tcmatch.Core\tcmatch.Translation.EN.cs
```

For a new language, `tcmatch.Translation.EN.cs` is the most important file to translate. The translations can be tested directly in QSX2; see 🌍 [5.1 Custom Translations](README.md#custom-translations).

I will personally keep the German and English translations up to date. Contributions for other languages are very welcome.

AI-assisted translation is fine, but please review the result carefully. Do not submit AI-generated translations without checking the result.

## 💻 Code

The C++ project is the part of the plugin directly called by Total Commander:

```text
QSX2\tcmatch\
```

It mainly forwards calls to the C# implementation, so there is usually little to do there. If you find a way to optimize the bridge or integrate the C# code more elegantly, a PR or example code is more than welcome.

The main development takes place in:

```text
QSX2\tcmatch.Core\
```

Important files include:

```text
QSX2\tcmatch.Core\tcmatch.Core\tcmatch.Core.cs            – the core logic of the plugin
QSX2\tcmatch.Core\tcmatch.Core\ConfigWindow.xaml          – the XAML layout of the configuration window
QSX2\tcmatch.Core\tcmatch.Core\SearchAssistantWindow.xaml – the XAML layout of the interactive Search Assistant
QSX2\tcmatch.Core\tcmatch.Core\tcmatch.Translation.EN.cs  – English translation strings
QSX2\tcmatch.Core\tcmatch.Core\tcmatch.Translation.DE.cs  – German translation strings
```

`tcmatch.Core.cs` is organized into regions, which can help you navigate the code by expanding and collapsing the relevant sections.

### 🔍 Finding the right place in the code

If you are looking for the code behind a visible text, the easiest way is often to search for that text in the translation files, e.g. in `tcmatch.Translation.EN.cs`.

The translation entry contains an ID, which can then be searched for in the other `.cs` or `.xaml` files. This is often the quickest way to find the relevant code.

### 🔨 Build the Code

You need **Visual Studio** to build QSX2.

Build the projects in this order:

1. `QSX2\tcmatch.Core\tcmatch.Core.sln` – build **Release | Any CPU** first.
2. `QSX2\tcmatch\tcmatch.sln` – then build **Release | 32-bit** and **Release | 64-bit**.

Both projects use the following PowerShell script during the build:

```text
QSX2\data\BuildAndDeploy.ps1
```

The script populates the build output in:

```text
QSX2\bin\
```

The first build may show some post-build errors. This is expected and usually not a problem. Once both projects have been built successfully at least once, these errors should no longer occur.

You may need to adjust paths in `BuildAndDeploy.ps1` for your local setup. Where possible, the script uses variables to avoid hard-coded paths.

### 🔧 Testing

All code changes should be tested in Total Commander.

### 🚀 Issues and Pull Requests

Bug fixes and small improvements can be submitted directly as pull requests.

For larger changes or new features, please open an issue first and describe the intended implementation. I would like to agree on the concept before substantial implementation work begins. This helps avoid frustration if the proposed approach does not fit QSX2.

Pull requests are welcome, even for ideas I do not plan to implement myself.

# ❤️ Thank you for helping improve QSX2!
