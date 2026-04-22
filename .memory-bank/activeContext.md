# Active Context

## Current Work Focus
Planning guardian integration tests for BinSkim.

## Recent Changes
- (none - new work focus)

## Active Decisions
- Pending: scope clarification needed before plan creation (see Open Questions below)

## Research Findings

### What Guardian Means in This Repo
- Guardian = Microsoft 1ES SDL infrastructure pools (guardian-build-infra-*)
- 1ESPT = 1 Engineering System Perf and Test pool for compliance scanning
- NOT a NuGet package, NOT a separate tool - it is infrastructure
- No .gdnconfig or .gdn files exist in the repo

### Existing Pipelines
- .pipelines/1ES.binskim.BuildAndTest.EXTERNAL.yml - build/test pipeline on guardian-build-infra pools (Linux + Windows matrix), triggers internal pipeline ID 21327
- .pipelines/1ES.binskim.compliance.EXTERNAL.yml - compliance/SDL pipeline on pool-1espt-mseng, runs BinSkim v4.3.1, CodeQL, CredScan, SPMI on built outputs
- Build uses 1ES.Official.PipelineTemplate.yml, compliance uses 1ES.Unofficial.PipelineTemplate.yml

### Compliance Pipeline Details (1ES.binskim.compliance.EXTERNAL.yml)
- Trigger: main branch only
- Builds with BuildAndTest.cmd, then runs BinSkim@4 task (exact version 4.3.1)
- AnalyzeTargetGlob scans net9.0 release output DLLs and EXEs
- AnalyzeSymPath uses Cache + symweb
- Also runs CredScan@2, publishes security analysis logs

### Build Pipeline Details (1ES.binskim.BuildAndTest.EXTERNAL.yml)
- Trigger: every PR and commit
- Matrix: Linux (guardian-build-infra-linux-x64) and Windows (guardian-build-infra-windows-x64)
- Uses custom NuGet feed: BinSkim.Build on mseng/1ES
- Has ValidateReleasePRs stage (checks PRs in ReleaseHistory.md are merged)
- Has InternalValidation stage (triggers internal BinSkimInternal pipeline ID 21327, polls for completion)

### Existing Test Patterns
- Test.FunctionalTests.BinSkim.Driver - end-to-end driver/baseline tests with expected SARIF outputs
- Test.FunctionalTests.BinSkim.Rules - rule functional tests with Pass/Fail test data folders
- Test.UnitTests.* - unit test projects
- Tech stack: xUnit, FluentAssertions, .NET 9
- Test assets include PE, ELF, Mach-O binaries with various compiler versions
- Baseline tests compare actual SARIF output against Expected/ and NonWindowsExpected/ folders

### Current State - No Guardian Integration Tests Exist
- No dedicated guardian/1ESPT integration test suite
- No cross-project validation tests
- BinSkim self-scans in compliance pipeline but no formal integration test suite

## Open Questions (Asked, Awaiting Response)
Scope clarification: What does "guardian integration tests" mean?
- Option A: New ADO pipeline YAML that runs BinSkim on curated test binaries on 1ESPT infrastructure, validates SARIF/SDL reporting end-to-end
- Option B: Local xUnit integration tests that invoke BinSkim as external process with same args as compliance pipeline, validate SARIF output - runnable without 1ESPT
- Option C: Both pipeline-based + local integration tests
- Option D: Something else

## Next Steps
1. Get scope clarification from user (Options A/B/C/D above)
2. Create detailed implementation plan based on answer
3. Decompose into tracks and tasks per CoDev workflow

## Current State
Research complete. Awaiting user decision on scope before creating implementation plan.
