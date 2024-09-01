# BinSkim Binary Analyzer

This repository contains the source code for BinSkim, a Portable Executable (PE) light-weight scanner that validates compiler/linker settings and other security-relevant binary characteristics.

## For Developers

1. Fork the repository -- **[Need Help?](https://help.github.com/articles/fork-a-repo/)**
2. Read the **[Rule Contributions Guide](./docs/RuleContributions.md)**
3. Load and compile `src\BinSkim.sln` to develop changes for contribution.
4. Execute BuildAndTest.cmd at the root of the enlistment to validate before submitting a PR.

### Submit Pull Requests

1. Run `BuildAndTest.cmd` at the root of the enlistment to ensure that all tests pass, release build succeeds, and NuGet packages are created
2. Submit a Pull Request to the 'develop' branch -- **[Need Help?](https://help.github.com/articles/about-pull-requests/)**

## For Users

1. Download BinSkim from **[NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/)**
2. Read the **[User Guide](./docs/UserGuide.md)**
3. Find out more about the Static Analysis Results Interchange Format **([SARIF](https://github.com/sarif-standard/sarif-spec/))** used to output Binskim results

### How to extract the exe file from the nuget package

If you only want to run the Binskim tool without installing anything, then you can

1. Download BinSkim from **[NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/)**
2. Rename the file extension from .nupkg to .zip (ie. via commandline: `rename microsoft.codeanalysis.binskim.x.y.z.nupkg microsoft.codeanalysis.binskim.x.y.z.zip`)
3. Unzip
4. Executable files are now available in the OS specific folder within _tools\net8.0_ (ie. linux-x64, win-x64, and osx-x64).
5. Navigate to this location to invoke the executable:
    - Windows: `binskim.exe analyze c:\bld\*.dll --recurse true --output MyRun.sarif`
    - Linux/Unix: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
    - Mac: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
    - Using dotnet sdk: `dotnet binskim.dll analyze /directoryPath/testBinary -o MyRun.sarif`

### Command-Line Quick Guide
For more information you can follow our [UserGuide.md](https://github.com/microsoft/binskim/blob/main/docs/UserGuide.md).

#### Analyze Command
The primary function of BinSkim is to analyze Windows portable executables (.dlls, .exes, etc). To analyze a file, pass one or more arguments that resolve one or more portable executables.
```pwsh
    // Analyze a single binary named MyProjectFile.dll found in c:\temp
    // and emit verbose messages during analysis
    binskime.exe analyze c:\temp\MyProjectFile.dll –verbose
    // Analyze all files with the .dll or .exe extension starting in the
    // current working directory and recursing through all child directories
    binskim analyze *.exe *.dll –recurse
    // Analyze all files with the .dll extension starting in the current
    // current directory and write results to a SARIF log file
    binskim analyze *.dll --output MyLog.sarif 
```

#### Help command
The following command-lines invoke the general BinSkime help message. This message will display all the built-in ModernCop commands (help, analyze, capture, et al) for which more detailed help can be requested: 
```pwsh
    binskim.exe --help
```
To request detailed help for specific commands, invoke ‘binskim.exe help [command]’, eg:
```pwsh
    binskim.exe help analyze
    binskim.exe help exportRules
    binskim.exe help exportConfig
    binskim.exe help dump
    binskim.exe help version
```
