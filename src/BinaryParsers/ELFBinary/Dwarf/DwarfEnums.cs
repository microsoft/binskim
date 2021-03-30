// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public enum DW_TAG
    {
        ArrayType = 0x01,
        ClassType = 0x02,
        EntryPoint = 0x03,
        EnumerationType = 0x04,
        FormalParameter = 0x05,
        ImportedDeclaration = 0x08,
        Label = 0x0a,
        LexicalBlock = 0x0b,
        Member = 0x0d,
        PointerType = 0x0f,
        ReferenceType = 0x10,
        CompileUnit = 0x11,
        StringType = 0x12,
        StructureType = 0x13,
        SubroutineType = 0x15,
        Typedef = 0x16,
        UnionType = 0x17,
        UnspecifiedParameters = 0x18,
        Variant = 0x19,
        CommonBlock = 0x1a,
        CommonInclusion = 0x1b,
        Inheritance = 0x1c,
        InlinedSubroutine = 0x1d,
        Module = 0x1e,
        PtrToMemberType = 0x1f,
        SetType = 0x20,
        SubrangeType = 0x21,
        WithStmt = 0x22,
        AccessDeclaration = 0x23,
        BaseType = 0x24,
        CatchBlock = 0x25,
        ConstType = 0x26,
        Constant = 0x27,
        Enumerator = 0x28,
        FileType = 0x29,
        Friend = 0x2a,
        Namelist = 0x2b,
        NamelistItem = 0x2c,
        PackedType = 0x2d,
        Subprogram = 0x2e,
        TemplateTypeParam = 0x2f,
        TemplateValueParam = 0x30,
        ThrownType = 0x31,
        TryBlock = 0x32,
        VariantPart = 0x33,
        Variable = 0x34,
        VolatileType = 0x35,
        LoUser = 0x4080,
        HiUser = 0xffff,
    }

    public enum DW_CHILDREN
    {
        No = 0,
        Yes = 1
    }

    public enum DW_AT
    {
        Sibling = 0x01, // reference
        Location = 0x02, // block, constant
        Name = 0x03, // string
        Ordering = 0x09, // constant
        ByteSize = 0x0b, // constant
        BitOffset = 0x0c, // constant
        BitSize = 0x0d, // constant
        StmtList = 0x10, // constant
        LowPc = 0x11, // address
        HighPc = 0x12, // address
        Language = 0x13, // constant
        Discr = 0x15, // reference
        DiscrValue = 0x16, // constant
        Visibility = 0x17, // constant
        Import = 0x18, // reference
        StringLength = 0x19, // block, constant
        CommonReference = 0x1a, // reference
        CompDir = 0x1b, // string
        ConstValue = 0x1c, // string, constant, block
        ContainingType = 0x1d, // reference
        DefaultValue = 0x1e, // reference
        Inline = 0x20, // constant
        IsOptional = 0x21, // flag
        LowerBound = 0x22, // constant, reference
        Producer = 0x25, // string
        Prototyped = 0x27, // flag
        ReturnAddr = 0x2a, // block, constant
        StartScope = 0x2c, // constant
        StrideSize = 0x2e, // constant
        UpperBound = 0x2f, // constant, reference
        AbstractOrigin = 0x31, // reference
        Accessibility = 0x32, // constant
        AddressClass = 0x33, // constant
        Artificial = 0x34, // flag
        BaseTypes = 0x35, // reference
        CallingConvention = 0x36, // constant
        Count = 0x37, // constant, reference
        DataMemberLocation = 0x38, // block, reference
        DeclColumn = 0x39, // constant
        DeclFile = 0x3a, // constant
        DeclLine = 0x3b, // constant
        Declaration = 0x3c, // flag
        DiscrList = 0x3d, // block
        Encoding = 0x3e, // constant
        External = 0x3f, // flag
        FrameBase = 0x40, // block, constant
        Friend = 0x41, // reference
        IdentifierCase = 0x42, // constant
        MacroInfo = 0x43, // constant
        NamelistItem = 0x44, // block
        Priority = 0x45, // reference
        Segment = 0x46, // block, constant
        Specification = 0x47, // reference
        StaticLink = 0x48, // block, constant
        Type = 0x49, // reference
        UseLocation = 0x4a, // block, constant
        VariableParameter = 0x4b, // flag
        Virtuality = 0x4c, // constant
        VtableElemLocation = 0x4d, // block, reference
        LoUser = 0x2000, // —
        HiUser = 0x3fff // —
    }

    public enum DW_FORM
    {
        Addr = 0x01, // address
        Block2 = 0x03, // block
        Block4 = 0x04, // block
        Data2 = 0x05, // constant
        Data4 = 0x06, // constant
        Data8 = 0x07, // constant
        String = 0x08, // string
        Block = 0x09, // block
        Block1 = 0x0a, // block
        Data1 = 0x0b, // constant
        Flag = 0x0c, // flag
        Sdata = 0x0d, // constant
        Strp = 0x0e, // string
        Udata = 0x0f, // constant
        RefAddr = 0x10, // reference
        Ref1 = 0x11, // reference
        Ref2 = 0x12, // reference
        Ref4 = 0x13, // reference
        Ref8 = 0x14, // reference
        RefUdata = 0x15, // reference
        Indirect = 0x16, //

        /* DWARF 4*/
        SecOffset = 0x17,
        Exprloc = 0x18,
        FlagPresent = 0x19,
        RefSig8 = 0x20,

        /* Extensions for Fission.  See http://gcc.gnu.org/wiki/DebugFission.  */
        GNURefIndex = 0x1f00,
        GNUAddrIndex = 0x1f01,
        GNUStrIndex = 0x1f02
    }

    public enum DW_ATE
    {
        Address, // linear machine address
        Boolean, // true or false
        ComplexFloat, // complex floating-point number
        Float, // floating-point number
        Signed, // signed binary integer
        SignedChar, // signed character
        Unsigned, // unsigned binary integer
        UnsignedChar // unsigned character
    }

    /// <summary>
    /// Check http://dwarfstd.org/doc/DWARF5.pdf, page 62.
    /// </summary>
    public enum DW_LANGUAGE
    {
        C89 = 0x0001,
        C = 0x0002,
        Ada83p = 0x0003,
        CPlusPlus = 0x0004,
        Cobol74p = 0x0005,
        Cobol85p = 0x0006,
        Fortran77 = 0x0007,
        Fortran90 = 0x0008,
        Pascal83 = 0x0009,
        Modula2 = 0x000a,
        Java = 0x000b,
        C99 = 0x000c,
        Ada95 = 0x000d,
        Fortran95 = 0x000e,
        PLI = 0x000f,
        ObjC = 0x0010,
        ObjCPlusPlus = 0x0011,
        UPC = 0x0012,
        D = 0x0013,
        Python = 0x0014,
        OpenCL = 0x0015,
        Go = 0x0016,
        Modula3 = 0x0017,
        Haskell = 0x0018,
        CPlusPlus03 = 0x0019,
        CPlusPlus11 = 0x001a,
        OCaml = 0x001b,
        Rust = 0x001c,
        C11 = 0x001d,
        Swift = 0x001e,
        Julia = 0x001f,
        Dylan = 0x0020,
        CPlusPlus14 = 0x0021,
        Fortran03 = 0x0022,
        Fortran08 = 0x0023,
        RenderScript = 0x0024,
        BLISS = 0x0025,
        loUser = 0x8000,
        hiUser = 0xffff
    }

    /*
    public enum DW_OP
    {
        addr = 0x03, // 1 constant address (size target specific)
        deref = 0x06, // 0
        const1u = 0x08, // 1 1-byte constant
        const1s = 0x09, // 1 1-byte constant
        const2u = 0x0a, // 1 2-byte constant
        const2s = 0x0b, // 1 2-byte constant
        const4u = 0x0c, // 1 4-byte constant
        const4s = 0x0d, // 1 4-byte constant
        const8u = 0x0e, // 1 8-byte constant
        const8s = 0x0f, // 1 8-byte constant
        constu = 0x10, // 1 ULEB128 constant
        consts = 0x11, // 1 SLEB128 constant
        dup = 0x12, // 0
        drop = 0x13, // 0
        over = 0x14, // 0
        pick = 0x15, // 1 1-byte stack index
        swap = 0x16, // 0
        rot = 0x17, // 0
        xderef = 0x18, // 0
        abs = 0x19, // 0
        and = 0x1a, // 0
        div = 0x1b, // 0
        minus = 0x1c, // 0
        mod = 0x1d, // 0
        mul = 0x1e, // 0
        neg = 0x1f, // 0
        not = 0x20, // 0
        or = 0x21, // 0
        plus = 0x22, // 0
        plusUconst = 0x23, // 1 ULEB128 addend
        shl = 0x24, // 0
        shr = 0x25, // 0
        shra = 0x26, // 0
        xor = 0x27, // 0
        skip = 0x2f, // 1 signed 2-byte constant
        bra = 0x28, // 1 signed 2-byte constant
        eq = 0x29, // 0
        ge = 0x2a, // 0
        gt = 0x2b, // 0
        le = 0x2c, // 0
        lt = 0x2d, // 0
        ne = 0x2e, // 0
        lit0 = 0x30, // 0 literals 0..31 = (DW_OP_LIT0literal)
        lit1 = 0x31, // 0
        ...
        lit31 = 0x4f, // 0
        reg0 = 0x50, // 0 reg 0..31 = (DW_OP_REG0regnum)
        reg1 = 0x51, // 0
        ...
        reg31 = 0x6f, // 0
        breg0 = 0x70, // 1 SLEB128 offset
        breg1 = 0x71, // 1 base reg 0..31 = (DW_OP_BREG0regnum)
        ...
        breg31 = 0x8f, // 1
        regx = 0x90, // 1 ULEB128 register
        fbreg = 0x91, // 1 SLEB128 offset
        bregx = 0x92, // 2 ULEB128 register followed by SLEB128 offset
        piece = 0x93, // 1 ULEB128 size of piece addressed
        derefSize = 0x94, // 1 1-byte size of data retrieved
        xderefSize = 0x95, // 1 1-byte size of data retrieved
        nop = 0x96, // 0
        loUser = 0xe0, //
        hiUser = 0xff, //
    }
    */

    /*
    public enum DW_ACCESS
    {
        public = 1,
        protected = 2,
        private = 3
    }
    */

    /*
    public enum DW_VIS
    {
        local = 1,
        exported = 2,
        qualified = 3
    }
    */

    /*
    public enum DW_VIRTUALITY
    {
        none = 0,
        virtual = 1,
        pureVirtual = 2
    }
    */

    /*
    public enum DW_ID
    {
        caseSensitive = 0,
        upCase = 1,
        downCase = 2,
        caseInsensitive = 3
    }
    */

    /*
    public enum DW_CC
    {
        normal = 0x1,
        program = 0x2,
        nocall = 0x3,
        loUser = 0x40,
        hiUser = 0xf
    }
    */

    /*
    public enum DW_INL
    {
        notInlined = 0,
        inlined = 1,
        declaredNotInlined = 2,
        declaredInlined = 3
    }
    */

    /*
    public enum DW_ORD
    {
        rowMajor = 0,
        colMajor = 1
    }
    */

    /*
    public enum DW_DSC
    {
        label = 0,
        range = 1
    }
    */

    /*
    public enum DW_LNS
    {
        copy = 1,
        advancePc = 2,
        advanceLine = 3,
        setFile = 4,
        setColumn = 5,
        negateStmt = 6,
        setBasicBlock = 7,
        constAddPc = 8,
        fixedAdvancePc = 9
    }
    */

    /*
    public enum DW_LNE
    {
        endSequence = 1,
        setAddress = 2,
        defineFile = 3
    }
    */

    /*
    public enum DW_MACINFO
    {
        define = 1,
        undef = 2,
        startFile = 3,
        endFile = 4,
        vendorExt = 255
    }
    */

    /*
    public enum DW_CFA
    {
        advanceLoc = 0x1, // delta
        offset = 0x2, // register ULEB128 offset
        restore = 0x3, // register
        setLoc 0 = 0x01, // address
        advanceLoc1 0 = 0x02, // 1-byte delta
        advanceLoc2 0 = 0x03, // 2-byte delta
        advanceLoc4 0 = 0x04, // 4-byte delta
        offsetExtended 0 = 0x05, // ULEB128 register ULEB128 offset
        restoreExtended 0 = 0x06, // ULEB128 register
        undefined 0 = 0x07, // ULEB128 register
        sameValue 0 = 0x08, // ULEB128 register
        register 0 = 0x09, // ULEB128 register ULEB128 register
        rememberState 0 = 0x0a, //
        restoreState 0 = 0x0b, //
        defCfa 0 = 0x0c, // ULEB128 register ULEB128 offset
        defCfaRegister 0 = 0x0d, // ULEB128 register
        defCfaOffset 0 = 0x0e, // ULEB128 offset
        nop = 0, // 0
        loUser 0 = 0x1c, //
        hiUser 0 0x3f, //
    }
    */
}
