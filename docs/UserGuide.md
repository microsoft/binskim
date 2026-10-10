# BinSkim user guide

BinSkim analyzes Windows Portable Executable (PE), Linux ELF, and macOS Mach-O binaries for security and correctness issues. Analysis results use the Static Analysis Results Interchange Format (SARIF).

## Install BinSkim

BinSkim is distributed through the [Microsoft.CodeAnalysis.BinSkim NuGet package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/).

To run BinSkim without adding it as a project dependency:

1. Download the `.nupkg` file from NuGet.
2. Extract the package as a ZIP archive.
3. Under `tools/net9.0`, select the directory matching the operating system and architecture where BinSkim will run.

| Runtime identifier | Platform | Executable |
| --- | --- | --- |
| `win-x64` | Windows x64 | `BinSkim.exe` |
| `linux-x64` | Linux x64 | `BinSkim` |
| `linux-arm64` | Linux Arm64 | `BinSkim` |
| `osx-x64` | macOS x64 | `BinSkim` |

On Linux and macOS, run `chmod +x BinSkim` if the archive extraction tool did not preserve the executable permission.

Add the selected directory to `PATH`, or invoke the executable using a platform-appropriate path. Run `BinSkim --version` to verify the installation.

## Analyze binaries

The examples below assume the executable is available as `BinSkim` on `PATH`. Pass one or more files, directories, or wildcard patterns to the `analyze` command, using paths appropriate for the current platform.

```console
BinSkim analyze path-to-binary
BinSkim analyze path-to-publish-directory --recurse true
BinSkim analyze path-to-publish-directory --recurse true --output results.sarif
```

Use `BinSkim analyze --help` for the authoritative list of arguments and defaults. In particular, command help covers input selection, SARIF output, rule selection, symbols, tracing, inserted SARIF data, archive extraction, and error-handling options.

### SARIF output

Use `--output` to write results to a SARIF file. Without this option, BinSkim writes results to the console.

Treat SARIF logs as potentially sensitive build artifacts. Options that insert environment variables, file contents, binary contents, or other contextual data can disclose information from the analysis environment.

### Select rules

BinSkim enables its standard rule set by default.

- Use `--enable-disabled-rules` to add rules that are disabled by default.
- Use `--run-only-rules` to disable the standard rule set and run only the specified rules.
- Use `--config` to apply a generated and customized rule configuration.

Rule selectors accept semicolon-delimited rule IDs. A selector can optionally override the result level by using `RuleId:Level`, such as `BA2029:Note`.

The generated [rule reference](BinSkimRules.md) describes the available rules and their messages.

### Analyze archives

The analysis driver extracts supported archive formats and scans their contents by default. Use `--disable-archive-extraction` when only the explicitly selected files should be analyzed.

### Supply Windows symbols

Several PE rules require Program Database (PDB) information.

BinSkim first looks for a matching PDB beside the binary. Use `--local-symbol-directories` when the build stores PDBs elsewhere. Use `--sympath` to retrieve symbols from symbol servers.

Include a writable local cache in remote symbol paths. This improves performance and reduces dependence on symbol-server availability.

BinSkim clears `_NT_SYMBOL_PATH` and `_NT_ALT_SYMBOL_PATH` during analysis. Supply required symbol locations explicitly through `--sympath`.

Use `--trace PdbLoad` to see the paths examined while locating a PDB. See [Troubleshooting](Troubleshooting.md) for common symbol failures.

## Interpret results

Each result identifies a rule, result level, message, and target. The generated [rule reference](BinSkimRules.md) provides the description and possible messages for every rule.

`Pass` results confirm that BinSkim observed the expected condition. `NotApplicable` results explain why a rule did not apply to the target. Errors and warnings identify conditions that require investigation.

An analysis can complete successfully while reporting security findings. Automation should consume the SARIF results rather than treating a zero process exit code as proof that every target passed every rule.

Use `--rich-return-code` when automation needs to distinguish command-line, file-access, parsing, rule-execution, and result conditions. Treat the value as a bit field because more than one condition can occur in a single run.

## Generate configuration and rule metadata

Use the built-in export commands to generate files from the installed BinSkim version:

```console
BinSkim export-config binskim.config.xml
BinSkim export-rules BinSkimRules.md
```

Run each command with `--help` before integrating it into automation.

## Troubleshoot analysis

See [Troubleshooting](Troubleshooting.md) for PDB loading failures and Spectre-mitigation guidance. When the documented steps do not resolve a problem, search the [BinSkim issue tracker](https://github.com/microsoft/binskim/issues) before opening a new issue.

Include the BinSkim version, operating system, command line with sensitive values removed, relevant SARIF notifications, and the smallest shareable reproducer.
