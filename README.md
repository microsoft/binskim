# BinSkim Binary Analyzer

This repository contains the source code for BinSkim, a Portable Executable (PE) light-weight scanner that validates compiler/linker settings and other security-relevant binary characteristics.

## For Developers

1. Fork the repository -- **[Need Help?](https://help.github.com/articles/fork-a-repo/)**
2. Read the **[Rule Contributions Guide](./docs/RuleContributions.md)**
3. Install the .NET SDK selected by `global.json` and the .NET 9 runtime, then load and compile `src\BinSkim.sln` to develop changes for contribution.
4. Execute BuildAndTest.cmd at the root of the enlistment to validate before submitting a PR.

Tests use xUnit v3 with Microsoft.Testing.Platform (MTP), selected by `global.json`.
Run `dotnet test --solution src\BinSkim.sln` on Windows, or `./BuildAndTest.sh` on Linux/macOS.
The build scripts write TRX reports to `bld\TestResults`; to generate reports manually, add
`--report-trx --results-directory bld\TestResults`. MTP options are passed directly,
for example `dotnet test --solution src\BinSkim.sln --filter-class '*RulePropertyTests'`.

Builds that deploy Windows x64 DIA download `Microsoft.VisualCpp.DIA.SDK` from
the public Visual Studio Stable distribution. The version, immutable download URL
and SHA-256 hashes of the VSIX and native DLL are pinned in
`src\Directory.Packages.props`. MSBuild caches and verifies the SDK in each
project's intermediate directory before deploying only `msdia140.dll`. A clean
build needs access to `download.visualstudio.microsoft.com`; it does not need
Visual Studio or COM registration. Non-Windows builds do not download the SDK
unless publishing for `win-x64`. The SDK contains no managed `Dia2Lib.dll`, so
the existing interop assembly and managed TraceEvent dependency are unchanged. See
[the runtime notices](./src/BinSkim.Driver/README.txt) for provenance and licensing.

### Validating native DIA versions

The C# tests validate the pinned SDK DLL by default. On Windows x64, run the same
tests against a trusted candidate DLL with the standard .NET test runner:

```powershell
dotnet test src\BinSkim.sln -c Release --environment "BINSKIM_DIA_CANDIDATE=C:\candidate\msdia140.dll"
```

Test-only native-library resolution selects the candidate for BinaryParsers'
in-process DIA calls without replacing build outputs or changing the production
loader. The xUnit tests check compiler options, function security flags, source
checksums, Source Link streams, segments and section contributions, including
classic-PDB/MSFZ equivalence. No PowerShell wrapper or second test framework is
required. This checks BinSkim's exercised contracts, not every DIA SDK interface
or binary compatibility of a replacement managed `Dia2Lib.dll`.

### Validating symbol-server behavior

[`SymsrvTests`](./src/Test.UnitTests.BinaryParsers/PEBinary/SymsrvTests.cs)
exercise DIA/symsrv using self-contained symbol stores on Windows. No external
network or credentials are required.

```powershell
dotnet test src\Test.UnitTests.BinaryParsers\Test.UnitTests.BinaryParsers.csproj -c Release --filter FullyQualifiedName~SymsrvTests --blame-hang --blame-hang-timeout 180s
```

### Live symbol-server integration

[`SymbolServerIntegrationTests`](./src/Test.FunctionalTests.BinSkim.Driver/SymbolServerIntegrationTests.cs)
cover real servers and require Windows with permitted outbound HTTPS.

Local runs exclude `Category=LiveSymbolServer` by default; explicit filters
select live cases. Windows CI runs the public case (`Requires=Network`) inside
`Build and Test`, excluding private cases (`Requires=PrivateSymbolServer`).
Linux CI excludes live cases.

Run the public case with:

```powershell
dotnet test src\Test.FunctionalTests.BinSkim.Driver\Test.FunctionalTests.BinSkim.Driver.csproj -c Release --filter "Category=LiveSymbolServer&Requires!=PrivateSymbolServer" --logger trx --blame-hang --blame-hang-timeout 180s
```

Run private cases only on a protected, network-enabled Windows agent with an
authorized managed/workload identity or another approved authorization method.
The agent must configure the native symbol client's credential provider.
Select private cases with:

```powershell
dotnet test src\Test.FunctionalTests.BinSkim.Driver\Test.FunctionalTests.BinSkim.Driver.csproj -c Release --filter "Requires=PrivateSymbolServer" --logger trx --blame-hang --blame-hang-timeout 180s
```

Set `BINSKIM_SYMBOL_SERVER_TEST_CONFIG` to a protected runtime JSON file.
Each array entry supplies `Name` (a non-sensitive alias), `ServerUrl` (HTTPS, no
user information or query string), and `BinaryPath` (a controlled PE whose full
PDB is on that server). Missing or empty configuration fails a selected test
rather than skipping it. Do not commit this file, private endpoints, customer
binaries or credentials/tokens.
Public access and HTTP 401/403 rejection do not prove private authentication.

For native upgrades, compare baseline and candidate builds in fresh test
processes, retaining TRX results and native version/hash diagnostics. Use
`--no-build` after deploying a candidate to preserve its test output. Private
authentication, proxies and the supported Windows-image matrix need separate
integration/canary coverage.

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
4. Executable files are available directly under the OS-specific folder in `tools` (for example, `tools\linux-x64`, `tools\win-x64`, or `tools\osx-x64`).
5. Navigate to this location to invoke the executable:
    - Windows: `binskim.exe analyze c:\bld\*.dll --recurse true --output MyRun.sarif`
    - Linux/Unix: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
    - Mac: `./BinSkim analyze /someDirectory/testBinary -o MyRun.sarif`
    - Using dotnet sdk: `dotnet binskim.dll analyze /directoryPath/testBinary -o MyRun.sarif`

### Command-Line Quick Guide

| Argument (short form, long form) | Meaning |
| -------------------------------- | ------- |
| **`--trace`** | Execution traces, expressed as a semicolon-delimited list enclosed in double quotes, that should be emitted to the console and log file (if appropriate). Valid values: PdbLoad, ScanTime, RuleScanTime, PeakWorkingSet, TargetsScanned, ResultsSummary. |
| **`--sympath`** | Symbol paths, expressed as a semicolon-delimited list enclosed in double quotes. (e.g. `SRV*https://msdl.microsoft.com/download/symbols` or `Cache*d:\symbols;Srv*https://symweb`) See https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/advanced-symsrv-use for syntax information. |
| **`--local-symbol-directories`** | Local directory paths, expressed as a semicolon-delimited list enclosed in double quotes, that will be examined when attempting to locate PDBs. |
| **`-o, --output`** | File path used to write and output analysis using [SARIF](https://github.com/Microsoft/sarif-sdk) |
| **`-r, --recurse [true\|false]`** | If true, recurse into subdirectories when evaluating file specifier arguments |
| **`-c, --config`** | (Default: ‘default’) Path to policy file to be used to configure analysis. Passing value of 'default' (or omitting the argument) invokes built-in settings |
| **`-q, --quiet [true\|false]`** | If true, do not log results to the console |
| **`-s, --statistics`** | Generate timing and other statistics for analysis session |
| **`--insert`** | Optionally present data, expressed as a semicolon-delimited list enclosed in double quotes, that should be inserted into the log file. Valid values include Hashes, TextFiles, BinaryFiles, EnvironmentVariables, RegionSnippets, ContextRegionSnippets, ContextRegionSnippetPartialFingerprints, Guids, VersionControlDetails, and NondeterministicProperties. |
| **`-e, --environment [true\|false]`** | <p>If true, log machine environment details of run to output file.</p><p>**WARNING:** This option records potentially sensitive information (such as all environment variable values) to the log file.</p> |
| **`-p, --plugin`** | Paths to plugin, expressed as a semicolon-delimited list enclosed in double quotes, that will be invoked against all targets in the analysis set. |
| **`--rich-return-code [true\|false]`** | If true, output a more detailed exit code consisting of a series of flags about execution, rather than outputting '0' for success/'1' for failure (see codes below) |
| **`--level`** | Failure levels, expressed as a semicolon-delimited list enclosed in double quotes, that is used to filter the scan results. Valid values: Error, Warning and Note. |
| **`--kind`** | Result kinds, expressed as a semicolon-delimited list enclosed in double quotes, that is used to filter the scan results. Valid values: Fail (for literal scan results), Pass, Review, Open, NotApplicable and Informational. |
| **`--baseline`** | A Sarif file to be used as baseline. |
| **`--help`** | Table of argument information. |
| **`--version`** | BinSkim version details. |
| **`value pos. 0`** | One or more specifiers to a file, directory, or filter pattern that resolves to one or more binaries to analyze. |

**Example:** `binskim.exe analyze c:\bld\*.dll --recurse true --output MyRun.sarif`
