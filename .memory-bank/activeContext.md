# Active Context

## Current Work Focus
BinSkim Integration Tests - Tracks 1-2 complete, Tracks 3-4 ready.

## Recent Changes
- ✅ Track 1: Project scaffolding, BinSkimRunner helper, 5 initial tests
- ✅ Track 2: Core integration tests - 8 new tests (analyze verbs, dump, export-rules, export-config, error handling)
- Build: 0 warnings, 0 errors; Tests: 13/13 passing

## Active Decisions
- Invocation: dotnet BinSkim.dll for cross-platform (Linux + Windows)
- BinSkim.Driver referenced with ReferenceOutputAssembly=false (build-order dependency only)
- Path resolution: navigate from test assembly up to bld/bin/BinSkim.Driver/release/BinSkim.dll
- Self-scan pattern: BinSkim analyzing its own DLL (PDB co-located)
- CommandLineParser writes help/version to stderr, tests check combined output
- BinSkim exits 0 even when rules fire errors. Exit code reflects tool health, not rule results
- Verb names: analyze, dump, export-rules, export-config (NOT export-rules-metadata/export-configuration)
- export-rules/export-config take positional output path arg (not --output)

## Files Created/Modified
- NEW: src/Test.IntegrationTests.BinSkim.Driver/Test.IntegrationTests.BinSkim.Driver.csproj
- NEW: src/Test.IntegrationTests.BinSkim.Driver/BinSkimRunner.cs
- NEW: src/Test.IntegrationTests.BinSkim.Driver/AnalyzeCommandIntegrationTests.cs
- MOD: src/BinSkim.sln (added project + build configs)

## Test Inventory (13 tests, all passing)
### Track 1 (original)
1. Analyze_SelfScan_ExitsWithZero
2. Analyze_SelfScan_ProducesValidSarif
3. Analyze_NoValidTargets_ExitsWithNonZero
4. Analyze_HelpFlag_ExitsCleanly
5. Analyze_VersionFlag_ExitsCleanly

### Track 2 (new)
6. Analyze_KnownFailBinary_ProducesErrorResults - BA2016 fires error on ManagedFail.dll
7. Analyze_RunOnlyRules_FiltersToSpecifiedRule - --run-only-rules BA2016 filters results
8. Analyze_InvalidArgument_ExitsWithNonZero - --bogus-flag gives non-zero
9. Analyze_InvalidVerb_ExitsWithNonZero - unrecognized verb gives non-zero
10. Dump_SelfScan_ProducesMetadataOutput - dump outputs binary metadata
11. Dump_Verbose_ProducesMoreDetailedOutput - --verbose produces >= normal output
12. ExportRules_ProducesValidSarifOutput - export-rules creates .sarif with rule BA2016
13. ExportConfig_ProducesValidJsonOutput - export-config creates .json config

## Next Steps (Tracks 3-4 from plan)
1. ☐ Track 3: CLI Behavior Tests - response files, --recurse, config files, error handling

## Current State
Tracks 1-2 complete. 13 integration tests all green. Ready for Tracks 3-4.
