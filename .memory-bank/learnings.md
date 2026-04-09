# Learnings

- Initialized learnings file; no project-specific learnings recorded yet.
- DwarfAttributeValue represents DWARF attribute values with a Type enum (DwarfAttributeValueType) and a boxed Value, plus an optional Offset used for deferred resolution.
- Equality in DwarfAttributeValue is type-sensitive and uses special handling for some enum values (Address, Constant, Reference, SecOffset, Block, ExpressionLocation, Flag, String); other enum values currently fall through to a default “equal” path regardless of Value, which is important to keep in mind when adding tests.
- DwarfCompilationUnit maps DWARF4 forms (Data*, Address, Block*, String/Strp, Flag*, Ref*, ExpressionLocation, SecOffset, etc.) and DWARF5/extended forms (LineStrp, StrpSup, Strx*/GNUStrIndex, Addrx*/GNUAddrIndex, Rnglistx, Loclistx, RefSup*, RefSig8) into DwarfAttributeValue instances by setting Type, Value, and/or Offset according to the DWARF spec and LLVM’s DWARFFormValue behavior.
