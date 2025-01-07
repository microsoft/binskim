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
4. Executable files are now available in the OS specific folder within _tools\net9.0_ (ie. linux-x64, win-x64, and osx-x64).
5. Navigate to this location to invoke the executable:
   - Windows: `binskim.exe analyze c:\bld\*.dll --recurse true --output MyRun.sarif`
   - Linux/Unix: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
   - Mac: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
   - Using dotnet sdk: `dotnet binskim.dll analyze /directoryPath/testBinary -o MyRun.sarif`

### Command-Line Quick Guide

| Argument (short form, long form)       | Meaning                                                                                                                                                                                                                                                                                                                                                         |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`--trace`**                          | Execution traces, expressed as a semicolon-delimited list enclosed in double quotes, that should be emitted to the console and log file (if appropriate). Valid values: PdbLoad, ScanTime, RuleScanTime, PeakWorkingSet, TargetsScanned, ResultsSummary.                                                                                                        |
| **`--sympath`**                        | Symbol paths, expressed as a semicolon-delimited list enclosed in double quotes. (e.g. `SRV*https://msdl.microsoft.com/download/symbols` or `Cache*d:\symbols;Srv*https://symweb`) See https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/advanced-symsrv-use for syntax information.                                                           |
| **`--local-symbol-directories`**       | Local directory paths, expressed as a semicolon-delimited list enclosed in double quotes, that will be examined when attempting to locate PDBs.                                                                                                                                                                                                                 |
| **`-o, --output`**                     | File path used to write and output analysis using [SARIF](https://github.com/Microsoft/sarif-sdk)                                                                                                                                                                                                                                                               |
| **`-r, --recurse [true\|false]`**      | If true, recurse into subdirectories when evaluating file specifier arguments                                                                                                                                                                                                                                                                                   |
| **`-c, --config`**                     | (Default: ‘default’) Path to policy file to be used to configure analysis. Passing value of 'default' (or omitting the argument) invokes built-in settings                                                                                                                                                                                                      |
| **`-q, --quiet [true\|false]`**        | If true, do not log results to the console                                                                                                                                                                                                                                                                                                                      |
| **`-s, --statistics`**                 | Generate timing and other statistics for analysis session                                                                                                                                                                                                                                                                                                       |
| **`--insert`**                         | Optionally present data, expressed as a semicolon-delimited list enclosed in double quotes, that should be inserted into the log file. Valid values include Hashes, TextFiles, BinaryFiles, EnvironmentVariables, RegionSnippets, ContextRegionSnippets, ContextRegionSnippetPartialFingerprints, Guids, VersionControlDetails, and NondeterministicProperties. |
| **`-e, --environment [true\|false]`**  | <p>If true, log machine environment details of run to output file.</p><p>**WARNING:** This option records potentially sensitive information (such as all environment variable values) to the log file.</p>                                                                                                                                                      |
| **`-p, --plugin`**                     | Paths to plugin, expressed as a semicolon-delimited list enclosed in double quotes, that will be invoked against all targets in the analysis set.                                                                                                                                                                                                               |
| **`--rich-return-code [true\|false]`** | If true, output a more detailed exit code consisting of a series of flags about execution, rather than outputting '0' for success/'1' for failure (see codes below)                                                                                                                                                                                             |
| **`--level`**                          | Failure levels, expressed as a semicolon-delimited list enclosed in double quotes, that is used to filter the scan results. Valid values: Error, Warning and Note.                                                                                                                                                                                              |
| **`--kind`**                           | Result kinds, expressed as a semicolon-delimited list enclosed in double quotes, that is used to filter the scan results. Valid values: Fail (for literal scan results), Pass, Review, Open, NotApplicable and Informational.                                                                                                                                   |
| **`--baseline`**                       | A Sarif file to be used as baseline.                                                                                                                                                                                                                                                                                                                            |
| **`--help`**                           | Table of argument information.                                                                                                                                                                                                                                                                                                                                  |
| **`--version`**                        | BinSkim version details.                                                                                                                                                                                                                                                                                                                                        |
| **`value pos. 0`**                     | One or more specifiers to a file, directory, or filter pattern that resolves to one or more binaries to analyze.                                                                                                                                                                                                                                                |

**Example:** `binskim.exe analyze c:\bld\*.dll --recurse true --output MyRun.sarif`
