# Active Context

## Current Work Focus
Design and plan unit tests for DWARF-related types, starting with DwarfAttributeValue and including DWARF4/DWARF5-specific behaviors.

## Recent Changes
- ✅ Defined detailed unit test coverage goals for DwarfAttributeValue (all enum values, accessors, equality, hash code, and ToString).
- ✅ Identified DWARF4 and DWARF5 form mappings in DwarfCompilationUnit that feed into DwarfAttributeValue.

## Active Decisions
- ✅ Full enum coverage for DwarfAttributeValueType is desired, including currently less-used values like Invalid, ResolvedReference, and Loclistx.
- ✅ Tests should pin current behavior even when it may be surprising (e.g., equality/hash interactions for unhandled enum types), so future changes are explicit.

## Next Steps
1. ✅ Create a new DwarfAttributeValue-focused unit test class in the BinaryParsers unit test project, following existing naming and structure conventions.
2. ✅ Add tests for each accessor (Address, Block, Constant, String, Flag, Reference, ExpressionLocation, SecOffset) covering “happy path” usage.
3. ✅ Add Constant decoding tests for byte[] lengths 1/2/4/8, unsupported lengths (throw NotImplementedException), and direct ulong Value.
4. ✅ Add equality/hash code tests covering null semantics, type mismatches, handled enum types, and unhandled enum types (Invalid, ResolvedReference, Loclistx).
5. ✅ Add DWARF4-oriented tests that verify DwarfCompilationUnit maps classic forms (Data1/2/4/8, SData, UData, Address, Block*, String/Strp, Flag*, Ref*) into the expected DwarfAttributeValue Type/Value combinations.
6. ✅ Add DWARF5-oriented tests that verify newer forms (LineStrp, StrpSup, Strx*/GNUStrIndex, Addrx*/GNUAddrIndex, Rnglistx, Loclistx, RefSup*, RefSig8) are mapped to the correct Type/Value/Offset in DwarfAttributeValue.
7. ✅ Add a couple of small end-to-end DWARF4 and DWARF5 synthetic units that exercise representative attributes and assert on the resulting DwarfAttributeValue objects.

## Current State
All planned DWARF-related unit tests, including end-to-end DWARF4 and DWARF5 synthetic units, are implemented, and the BinaryParsers unit tests are currently passing.
