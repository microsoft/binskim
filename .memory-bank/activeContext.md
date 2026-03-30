# Active Context

## Current Work Focus
DWARF bounds-checking and robustness in DwarfMemoryReader (DWARF4/5), with tests-first workflow.

## Recent Changes
- ☐ Initial tests for malformed/truncated DWARF data
- ☐ Bounds-checking added to DwarfMemoryReader methods (ReadBytes, Peek, ReadByte, ReadUshort, ReadUint, ReadUlong, ULEB128, SLEB128)

## Active Decisions
- Tests should cover both well-formed DWARF4/5 data and truncated/malformed inputs.
- Behavior on truncation should be graceful failure (no IndexOutOfRangeException), in line with BinSkim error-reporting patterns.

## Next Steps
1. ☐ Locate DwarfMemoryReader and any existing DWARF tests.
2. ☐ Design and add focused unit tests for malformed/truncated data for the risky methods.
3. ☐ Implement bounds-checking and error-handling behavior.
4. ☐ Verify behavior against DWARF4 and DWARF5 specifications for relevant encodings.
5. ☐ Run appropriate test suite and update learnings.

## Current State
Memory bank initialized; implementation and tests still to be designed and written.
