# BinSkim Binary Analyzer

This repository contains the source code for BinSkim, a Portable Executable (PE) light-weight scanner that validates compiler/linker settings and other security-relevant binary characteristics.

## For Developers

1. Fork the repository -- **[Need Help?](https://help.github.com/articles/fork-a-repo/)**
2. Load and compile `src\BinSkim.sln`
3. Execute output in `bld\bin\BinSkim.Driver` for testing

### Submit Pull Requests

1. Run `BuildAndTest.cmd` at the root of the enlistment to ensure that all tests pass, release build succeeds, and NuGet packages are created
2. Submit a Pull Request -- **[Need Help?](https://help.github.com/articles/about-pull-requests/)**

## For Users

1. Download BinSkim from **[NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/)**
2. Read the **[User Guide](./docs/UserGuide.md)**
3. Find out more about the Static Analysis Results Interchange Format **([SARIF](https://github.com/sarif-standard/sarif-spec/))** used to output Binskim results

### Command-Line Quick Guide

| Argument (short form, long form) | Meaning |
| -------------------------------- | ------- |
| **`--sympath`** | Symbols path value (e.g. `SRV http://msdl.microsoft.com/download/symbols or Cache d:\symbols;Srv http://symweb`) |
| **`-o, --output`** | File path used to write and output analysis using [SARIF](https://github.com/Microsoft/sarif-sdk) |
| **`-v, --verbose`** | Emit verbose output. The comprehensive report is designed to provide appropriate evidence for compliance scenarios |
| **`-r, --recurse`** | Recurse into subdirectories when evaluating file specifier arguments |
| **`-c, --config`** | (Default: ‘default’) Path to policy file to be used to configure analysis. Passing value of 'default' (or omitting the argument) invokes built-in settings |
| **`-q, --quiet`** | Do not log results to the console |
| **`-s, --statistics`** | Generate timing and other statistics for analysis session |
| **`-h, --hashes`** | Output hashes of analysis targets when emitting SARIF reports |
| **`-e, --environment`** | <p>Log machine environment details of run to output file.</p><p>**WARNING:** This option records potentially sensitive information (such as all environment variable values) to the log file.</p> |
| **`-p, --plug-in`** | Path to plug-in that will be invoked against all targets in the analysis set. |
| **`--help`** | Table of argument information. |
| **`--version`** | BinSkim version details. |
| **`value pos. 0`** | One or more specifiers to a file, directory, or filter pattern that resolves to one or more binaries to analyze. |

**Example:** `binskim.exe analyze c:\bld\*.dll --recurse --output MyRun.sarif`