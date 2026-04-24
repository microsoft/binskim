# Active Context

## Current Work Focus
BinSkim Integration Tests - Track 1 complete, Tracks 2-4 ready.

## Recent Changes
- ✅ Created Test.IntegrationTests.BinSkim.Driver project (xUnit, net9.0, FluentAssertions, Sarif.Driver)
- ✅ Added project to BinSkim.sln
- ✅ Created BinSkimRunner helper class (launches BinSkim via dotnet BinSkim.dll, captures stdout/stderr/exit code, optional SARIF deserialization)
- ✅ Created 5 passing integration tests in AnalyzeCommandIntegrationTests
- ✅ Build: 0 warnings, 0 errors; Tests: 5/5 passing

## Active Decisions
- Invocation: dotnet BinSkim.dll for cross-platform (Linux + Windows)
- BinSkim.Driver referenced with ReferenceOutputAssembly=false (build-order dependency only)
- Path resolution: navigate from test assembly up to ld/bin/BinSkim.Driver/release/BinSkim.dll
- Self-scan pattern: BinSkim analyzing its own DLL (PDB co-located)
- CommandLineParser writes help/version to stderr, tests check combined output

## Files Created/Modified
- NEW: src/Test.IntegrationTests.BinSkim.Driver/Test.IntegrationTests.BinSkim.Driver.csproj
- NEW: src/Test.IntegrationTests.BinSkim.Driver/BinSkimRunner.cs
- NEW: src/Test.IntegrationTests.BinSkim.Driver/AnalyzeCommandIntegrationTests.cs
- MOD: src/BinSkim.sln (added project + build configs)

## Test Inventory (5 tests, all passing)
1. Analyze_SelfScan_ExitsWithZero - Scans BinSkim.dll, expects exit 0
2. Analyze_SelfScan_ProducesValidSarif - Validates SARIF structure and tool name
3. Analyze_NoValidTargets_ExitsWithNonZero - Non-existent target, expects non-zero exit
4. Analyze_HelpFlag_ExitsCleanly - help verb, exit 0, output present
5. Analyze_VersionFlag_ExitsCleanly - --version flag, exit 0, output present

## Next Steps (Tracks 2-4 from plan)
1. ☐ Track 2: Core Integration Tests - exit codes, analyze/dump/export verbs, known-bad binary scanning
2. ☐ Track 3: CLI Behavior Tests - response files, --recurse, config files, rule selection, error handling
3. ☐ Track 4: SARIF Validation Tests - schema compliance, determinism, data insertion/removal

## Current State
Track 1 complete. Integration test project scaffolding + BinSkimRunner helper + 5 tests all green.
