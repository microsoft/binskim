// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// DWARF symbol tag.
    /// </summary>
    public enum DwarfTag
    {
        None = 0,
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
        NamelistItems = 0x2c,
        PackedType = 0x2d,
        Subprogram = 0x2e,
        TemplateTypeParameter = 0x2f,
        TemplateValueParameter = 0x30,
        ThrownType = 0x31,
        TryBlock = 0x32,
        VariantPart = 0x33,
        Variable = 0x34,
        VolatileType = 0x35,
        DwarfProcedure = 0x36,
        RestrictType = 0x37,
        InterfaceType = 0x38,
        Namespace = 0x39,
        ImportedModule = 0x3a,
        UnspecifiedType = 0x3b,
        PartialUnit = 0x3c,
        ImportedUnit = 0x3d,
        MutableType = 0x3e, // Do not use DW_TAG_mutable_type, withdrawn from DWARF3 by DWARF3f.
        Condition = 0x3f,
        SharedType = 0x40,
        TypeUnit = 0x41,
        RvalueReferenceType = 0x42,
        TemplateAlias = 0x43,

        SkeletonUnit = 0x4A,

        LoUser = 0x4080,
        MipsLoop = 0x4081,
        HpArrayDescriptor = 0x4090,
        FormatLabel = 0x4101, // GNU. Fortran
        FunctionTemplate = 0x4102, // GNU. For C++
        ClassTemplate = 0x4103, // GNU. For C++
        GnuBincl = 0x4104,
        GnuEincl = 0x4105,
        GnuTemplateTemplateParameter = 0x4106,
        GnuTemplateParameterPack = 0x4107,
        GnuFormalParameterPack = 0x4108,
        GnuCallSite = 0x4109,
        GnuCallSiteParameter = 0x410a,
        AltiumCircType = 0x5101,
        AltiumMwaCircType = 0x5102,
        AltiumRevCarryType = 0x5103,
        AltiumRom = 0x5111,
        UpcSharedType = 0x8765,
        UpcStrictType = 0x8766,
        UpcRelaxedType = 0x8767,
        PgiKanjiType = 0xa000,
        PgiInterfaceBlock = 0xa020,
        SunFunctionTemplate = 0x4201,
        SunClassTemplate = 0x4202,
        SunStructTemplate = 0x4203,
        SunUnionTemplate = 0x4204,
        SunIndirectInheritance = 0x4205,
        SunCodeflags = 0x4206,
        SunMemopInfo = 0x4207,
        SunOmpChildFunc = 0x4208,
        SunRttoDescriptor = 0x4209,
        SunDtorInfo = 0x420a,
        SunDtor = 0x420b,
        SunF90Interface = 0x420c,
        SunFortranVaxStructure = 0x420d,
        SunHi = 0x42ff,
        HiUser = 0xffff,
    }

    /// <summary>
    /// DWARF attribute value data format
    /// </summary>
    public enum DwarfFormat
    {
        None = 0,
        Address = 0x01,
        Block2 = 0x03,
        Block4 = 0x04,
        Data2 = 0x05,
        Data4 = 0x06,
        Data8 = 0x07,
        String = 0x08,
        Block = 0x09,
        Block1 = 0x0a,
        Data1 = 0x0b,
        Flag = 0x0c,
        SData = 0x0d,
        Strp = 0x0e,
        UData = 0x0f,
        RefAddr = 0x10,
        Ref1 = 0x11,
        Ref2 = 0x12,
        Ref4 = 0x13,
        Ref8 = 0x14,
        RefUData = 0x15,
        Indirect = 0x16,
        SecOffset = 0x17,
        ExpressionLocation = 0x18,
        FlagPresent = 0x19,
        RefSig8 = 0x20,

        Strx = 0x1a,
        Addrx = 0x1b,
        RefSup4 = 0x1c,
        StrpSup = 0x1d,
        Data16 = 0x1e,
        LineStrp = 0x1f,


        ImplicitConst = 0x21,
        Loclistx = 0x22,

        Rnglistx = 0x23,
        RefSup8 = 0x24,
        Strx1 = 0x25,
        Strx2 = 0x26,
        Strx3 = 0x27,
        Strx4 = 0x28,
        Addrx1 = 0x29,
        Addrx2 = 0x2a,
        Addrx3 = 0x2b,
        Addrx4 = 0x2c,

        /* Extensions for Fission.  See http://gcc.gnu.org/wiki/DebugFission.  */
        GNURefIndex = 0x1f00,
        GNUAddrIndex = 0x1f01,
        GNUStrIndex = 0x1f02
    }

    /// <summary>
    /// DWARF attribute.
    /// </summary>
    public enum DwarfAttribute
    {
        None = 0,
        Sibling = 0x01,
        Location = 0x02,
        Name = 0x03,
        Ordering = 0x09,
        SubscrData = 0x0a,
        ByteSize = 0x0b,
        BitOffset = 0x0c,
        BitSize = 0x0d,
        ElementList = 0x0f,
        StmtList = 0x10,
        LowPc = 0x11,
        HighPc = 0x12,
        Language = 0x13,
        Member = 0x14,
        Discr = 0x15,
        DiscrValue = 0x16,
        Visibility = 0x17,
        Import = 0x18,
        StringLength = 0x19,
        CommonReference = 0x1a,
        CompDir = 0x1b,
        ConstValue = 0x1c,
        ContainingType = 0x1d,
        DefaultValue = 0x1e,
        Inline = 0x20,
        IsOptional = 0x21,
        LowerBound = 0x22,
        Producer = 0x25,
        Prototyped = 0x27,
        ReturnAddr = 0x2a,
        StartScopt = 0x2c,
        BitStride = 0x2e,
        StrideSize = 0x2e,
        UpperBound = 0x2f,
        AbstractOrigin = 0x31,
        Accessibility = 0x32,
        AddressClass = 0x33,
        Artifical = 0x34,
        BaseTypes = 0x35,
        CallingConvention = 0x36,
        Count = 0x37,
        DataMemberLocation = 0x38,
        DeclColumn = 0x39,
        DeclFile = 0x3a,
        DeclLine = 0x3b,
        Declaration = 0x3c,
        DiscrList = 0x3d,
        Encoding = 0x3e,
        External = 0x3f,
        FrameBase = 0x40,
        Friend = 0x41,
        IdentifierCase = 0x42,
        MacroInfo = 0x43,
        NamelistItem = 0x44,
        Priority = 0x45,
        Segment = 0x46,
        Specification = 0x47,
        StaticLink = 0x48,
        Type = 0x49,
        UseLocation = 0x4a,
        VariableParameter = 0x4b,
        Virtuality = 0x4c,
        VtableElemLocation = 0x4d,
        Allocated = 0x4e,
        Associated = 0x4f,
        DataLocation = 0x50,
        ByteStride = 0x51,
        Stride = 0x51,
        EntryPc = 0x52,
        UseUtf8 = 0x53,
        Extension = 0x54,
        Ranges = 0x55,
        Trampoline = 0x56,
        CallColumn = 0x57,
        CallFile = 0x58,
        CallLine = 0x59,
        Description = 0x5a,
        BinaryScale = 0x5b,
        DecimalScale = 0x5c,
        Small = 0x5d,
        DecimalSign = 0x5e,
        DigitCount = 0x5f,
        PictureString = 0x60,
        Mutable = 0x61,
        ThreadsScaled = 0x62,
        Explicit = 0x63,
        ObjectPointer = 0x64,
        Endianity = 0x65,
        Elemental = 0x66,
        Pure = 0x67,
        Recursive = 0x68,
        Signature = 0x69,
        MainSubprogram = 0x6a,
        DataBitOffset = 0x6b,
        ConstExpr = 0x6c,
        EnumClass = 0x6d,
        LinkageName = 0x6e,

        DwoName = 0x76,

        HpBlockIndex = 0x2000,
        LoUser = 0x2000,
        MipsFde = 0x2001,
        MipsLoopBegin = 0x2002,
        MipsTailLoopBegin = 0x2003,
        MipsEpilogBegin = 0x2004,
        MipsLoopUnrollFactor = 0x2005,
        MipsSoftwarePipelineDepth = 0x2006,
        MipsLinkageName = 0x2007,
        MipsStride = 0x2008,
        MipsAbstractName = 0x2009,
        MipsCloneOrigin = 0x200a,
        MipsHasInlines = 0x200b,
        MipsStrideByte = 0x200c,
        MipsStrideElem = 0x200d,
        MipsPtrDopetype = 0x200e,
        MipsAllocatableDopetype = 0x200f,
        MipsAssumedShapeDopetype = 0x2010,
        MipsAssumedSize = 0x2011,
        HpUnmodifiable = 0x2001,
        HpActualsStmtList = 0x2010,
        HpProcPerSection = 0x2011,
        HpRawDataPtr = 0x2012,
        HpPassByReference = 0x2013,
        HpOptLevel = 0x2014,
        HpProfVersionId = 0x2015,
        HpOptFlags = 0x2016,
        HpColdRegionLowPc = 0x2017,
        HpColdRegionHighPc = 0x2018,
        HpAllVariablesModifiable = 0x2019,
        HpLinkageName = 0x201a,
        HpProfFlags = 0x201b,
        CpqDiscontigRanges = 0x2001,
        CpqSemanticEvents = 0x2002,
        CpqSplitLifetimesVar = 0x2003,
        CpqSplitLifetimesRtn = 0x2004,
        CpqPrologueLength = 0x2005,
        IntelOtherEndian = 0x2026,
        SfNames = 0x2101,
        SrcInfo = 0x2102,
        MacInfo = 0x2103,
        SrcCoords = 0x2104,
        BodyBegin = 0x2105,
        BodyEnd = 0x2106,
        GnuVector = 0x2107,
        GnuGuardedBy = 0x2108,
        GnuPtGuardedBy = 0x2109,
        GnuGuarded = 0x210a,
        GnuPtGuarded = 0x210b,
        GnuLocksExcluded = 0x210c,
        GnuExclusiveLocksRequired = 0x210d,
        GnuSharedLocksRequired = 0x210e,
        GnuOdrSignature = 0x210f,
        GnuTemplateName = 0x2110,
        GnuCallSiteValue = 0x2111,
        GnuCallSiteDataVaule = 0x2112,
        GnuCallSiteTarget = 0x2113,
        GnuCallSiteTargetClobbered = 0x2114,
        GnuTailCall = 0x2115,
        GnuAllTailCallSites = 0x2116,
        GnuAllCallSites = 0x2117,
        GnuAllSourceCallSites = 0x2118,

        GnuDwoName = 0x2130,

        SunTemplate = 0x2201,
        VmsRtnbegPdAddress = 0x2201,
        SunAlignment = 0x2202,
        SunVtable = 0x2203,
        SunCountGuarantee = 0x2204,
        SunCommandLine = 0x2205,
        SunVbase = 0x2206,
        SunCompileOptions = 0x2207,
        SunLanguage = 0x2208,
        SunBrowserFile = 0x2209,
        SunVtableAbi = 0x2210,
        SunFuncOffsets = 0x2211,
        SunCfKind = 0x2212,
        SunVtableIndex = 0x2213,
        SunOmpTprivAddr = 0x2214,
        SunOmpChildFunc = 0x2215,
        SunFuncOffset = 0x2216,
        SunMemopTypeRef = 0x2217,
        SunProfileId = 0x2218,
        SunMemopSignature = 0x2219,
        SunObjDir = 0x2220,
        SunObjFile = 0x2221,
        SunOriginalName = 0x2222,
        SunHwcprofSignature = 0x2223,
        SunAmd64Parmdump = 0x2224,
        SunPartLinkName = 0x2225,
        SunLinkName = 0x2226,
        SunPassWithConst = 0x2227,
        SunReturnWithConst = 0x2228,
        SunImportByName = 0x2229,
        SunF90Pointer = 0x222a,
        SunPassByRef = 0x222b,
        SunF90Allocatable = 0x222c,
        SunF90AssumedShapeArray = 0x222d,
        SunCVla = 0x222e,
        SunReturnValuePtr = 0x2230,
        SunDtorStart = 0x2231,
        SunDtorLength = 0x2232,
        SunDtorStateInitial = 0x2233,
        SunDtorStateFinal = 0x2234,
        SunDtorStateDeltas = 0x2235,
        SunImportByLname = 0x2236,
        SunF90UseOnly = 0x2237,
        SunNamelistSpec = 0x2238,
        SunIsOmpChildFunc = 0x2239,
        SunFortranMainAlias = 0x223a,
        SunFortranBased = 0x223b,
        AltiumLoclist = 0x2300,
        UseGnatDescriptiveType = 0x2301,
        GnatDescriptiveType = 0x2302,
        UpcThreadsScaled = 0x3210,
        PgiLbase = 0x3a00,
        PgiSoffset = 0x3a01,
        PgiLstride = 0x3a02,
        AppleOptimized = 0x3fe1,
        AppleFlags = 0x3fe2,
        AppleIsa = 0x3fe3,
        AppleBlock = 0x3fe4,
        AppleMajorRuntimeVers = 0x3fe5,
        AppleRuntimeClass = 0x3fe6,
        AppleOmitFramePtr = 0x3fe7,
        AppleClosure = 0x3fe4,
        HiUser = 0x3fff,
    }

    /// <summary>
    /// DWARF line number standard operation codes
    /// </summary>
    public enum DwarfLineNumberStandardOpcode
    {
        Extended = 0,
        Copy = 0x01,
        AdvancePc = 0x02,
        AdvanceLine = 0x03,
        SetFile = 0x04,
        SetColumn = 0x05,
        NegateStmt = 0x06,
        SetBasicBlock = 0x07,
        ConstAddPc = 0x08,
        FixedAdvancePc = 0x09,
        SetPrologueEnd = 0x0a,
        SetEpilogueBegin = 0x0b,
        SetIsa = 0x0c,
    }

    /// <summary>
    /// DWARF line number extended operation codes.
    /// </summary>
    public enum DwarfLineNumberExtendedOpcode
    {
        Unknown = 0x00,
        EndSequence = 0x01,
        SetAddress = 0x02,
        DefineFile = 0x03,
        SetDiscriminator = 0x04,
        HpNegateIsUvUpdate = 0x11,
        HpPushContext = 0x12,
        HpPopContext = 0x13,
        HpSetFileLineColumn = 0x14,
        HpSetRoutineName = 0x15,
        HpSetSequence = 0x16,
        HpNegatePostSemantics = 0x17,
        HpNegateFunctionExit = 0x18,
        HpNegateFrontEndLogical = 0x19,
        HpDeficeProc = 0x20,
        HpSourceFileCorrelation = 0x80,
        LoUser = 0x80,
        HiUser = 0xff,
    }

    /// <summary>
    /// DWARF expression operation (DW_OP_xxx).
    /// </summary>
    public enum DwarfOperation
    {
        addr = 0x03,
        deref = 0x06,
        const1u = 0x08,
        const1s = 0x09,
        const2u = 0x0a,
        const2s = 0x0b,
        const4u = 0x0c,
        const4s = 0x0d,
        const8u = 0x0e,
        const8s = 0x0f,
        constu = 0x10,
        consts = 0x11,

        /// <summary>
        /// The DW_OP_dup operation duplicates the value (including its type identifier) at the top of the stack.
        /// </summary>
        Duplicate = 0x12,
        drop = 0x13,
        over = 0x14,
        pick = 0x15,
        swap = 0x16,
        rot = 0x17,
        xderef = 0x18,
        abs = 0x19,
        and = 0x1a,
        div = 0x1b,

        /// <summary>
        /// The DW_OP_minus operation pops the top two stack values, subtracts the former
        /// top of the stack from the former second entry, and pushes the result.
        /// </summary>
        Minus = 0x1c,
        mod = 0x1d,
        mul = 0x1e,
        neg = 0x1f,
        not = 0x20,
        or = 0x21,

        /// <summary>
        /// The DW_OP_plus operation pops the top two stack entries, adds them together, and pushes the result.
        /// </summary>
        Plus = 0x22,
        plus_uconst = 0x23,
        shl = 0x24,
        shr = 0x25,
        shra = 0x26,
        xor = 0x27,
        bra = 0x28,
        eq = 0x29,
        ge = 0x2a,
        gt = 0x2b,
        le = 0x2c,
        lt = 0x2d,
        ne = 0x2e,
        skip = 0x2f,
        lit0 = 0x30,
        lit1 = 0x31,
        lit2 = 0x32,
        lit3 = 0x33,
        lit4 = 0x34,
        lit5 = 0x35,
        lit6 = 0x36,
        lit7 = 0x37,
        lit8 = 0x38,
        lit9 = 0x39,
        lit10 = 0x3a,
        lit11 = 0x3b,
        lit12 = 0x3c,
        lit13 = 0x3d,
        lit14 = 0x3e,
        lit15 = 0x3f,
        lit16 = 0x40,
        lit17 = 0x41,
        lit18 = 0x42,
        lit19 = 0x43,
        lit20 = 0x44,
        lit21 = 0x45,
        lit22 = 0x46,
        lit23 = 0x47,
        lit24 = 0x48,
        lit25 = 0x49,
        lit26 = 0x4a,
        lit27 = 0x4b,
        lit28 = 0x4c,
        lit29 = 0x4d,
        lit30 = 0x4e,
        lit31 = 0x4f,
        reg0 = 0x50,
        reg1 = 0x51,
        reg2 = 0x52,
        reg3 = 0x53,
        reg4 = 0x54,
        reg5 = 0x55,
        reg6 = 0x56,
        reg7 = 0x57,
        reg8 = 0x58,
        reg9 = 0x59,
        reg10 = 0x5a,
        reg11 = 0x5b,
        reg12 = 0x5c,
        reg13 = 0x5d,
        reg14 = 0x5e,
        reg15 = 0x5f,
        reg16 = 0x60,
        reg17 = 0x61,
        reg18 = 0x62,
        reg19 = 0x63,
        reg20 = 0x64,
        reg21 = 0x65,
        reg22 = 0x66,
        reg23 = 0x67,
        reg24 = 0x68,
        reg25 = 0x69,
        reg26 = 0x6a,
        reg27 = 0x6b,
        reg28 = 0x6c,
        reg29 = 0x6d,
        reg30 = 0x6e,
        reg31 = 0x6f,
        breg0 = 0x70,
        breg1 = 0x71,
        breg2 = 0x72,
        breg3 = 0x73,
        breg4 = 0x74,
        breg5 = 0x75,
        breg6 = 0x76,
        breg7 = 0x77,
        breg8 = 0x78,
        breg9 = 0x79,
        breg10 = 0x7a,
        breg11 = 0x7b,
        breg12 = 0x7c,
        breg13 = 0x7d,
        breg14 = 0x7e,
        breg15 = 0x7f,
        breg16 = 0x80,
        breg17 = 0x81,
        breg18 = 0x82,
        breg19 = 0x83,
        breg20 = 0x84,
        breg21 = 0x85,
        breg22 = 0x86,
        breg23 = 0x87,
        breg24 = 0x88,
        breg25 = 0x89,
        breg26 = 0x8a,
        breg27 = 0x8b,
        breg28 = 0x8c,
        breg29 = 0x8d,
        breg30 = 0x8e,
        breg31 = 0x8f,
        regx = 0x90,
        FrameBaseRegister = 0x91,
        bregx = 0x92,
        piece = 0x93,
        deref_size = 0x94,
        xderef_size = 0x95,
        nop = 0x96,
        push_object_address = 0x97,
        call2 = 0x98,
        call4 = 0x99,
        call_ref = 0x9a,
        form_tls_address = 0x9b,
        call_frame_cfa = 0x9c,
        bit_piece = 0x9d,
        implicit_value = 0x9e,
        stack_value = 0x9f,
        GNU_push_tls_address = 0xe0,
        lo_user = 0xe0,
        GNU_uninit = 0xf0,
        GNU_encoded_addr = 0xf1,
        GNU_implicit_pointer = 0xf2,
        GNU_entry_value = 0xf3,
        HP_unknown = 0xe0,
        HP_is_value = 0xe1,
        HP_fltconst4 = 0xe2,
        HP_fltconst8 = 0xe3,
        HP_mod_range = 0xe4,
        HP_unmod_range = 0xe5,
        HP_tls = 0xe6,
        INTEL_bit_piece = 0xe8,
        APPLE_uninit = 0xf0,
        PGI_omp_thread_num = 0xf8,
        hi_user = 0xff,
    }

    /// <summary>
    /// DWARF canonical frame address instructions
    /// </summary>
    public enum DwarfCanonicalFrameAddressInstruction
    {
        advance_loc = 0x40,
        offset = 0x80,
        restore = 0xc0,
        extended = 0,
        nop = 0x00,
        set_loc = 0x01,
        advance_loc1 = 0x02,
        advance_loc2 = 0x03,
        advance_loc4 = 0x04,
        offset_extended = 0x05,
        restore_extended = 0x06,
        undefined = 0x07,
        same_value = 0x08,
        register = 0x09,
        remember_state = 0x0a,
        restore_state = 0x0b,
        def_cfa = 0x0c,
        def_cfa_register = 0x0d,
        def_cfa_offset = 0x0e,
        def_cfa_expression = 0x0f,
        expression = 0x10,
        offset_extended_sf = 0x11,
        def_cfa_sf = 0x12,
        def_cfa_offset_sf = 0x13,
        val_offset = 0x14,
        val_offset_sf = 0x15,
        val_expression = 0x16,
        lo_user = 0x1c,
        low_user = 0x1c,
        MIPS_advance_loc8 = 0x1d,
        GNU_window_save = 0x2d,
        GNU_args_size = 0x2e,
        GNU_negative_offset_extended = 0x2f,
        high_user = 0x3f,
    }

    /// <summary>
    /// Encodings used in Exception handling frames stream.
    /// </summary>
    [Flags]
    public enum DwarfExceptionHandlingEncoding : byte
    {
        /// <summary>
        /// An absolute pointer. The size is determined by whether this is a 32-bit or 64-bit address space, and will be 32 or 64 bits.
        /// </summary>
        AbsolutePointer = 0,

        /// <summary>
        /// The value is omitted.
        /// </summary>
        Omit = 0xff,

        /// <summary>
        /// The value is an unsigned LEB128.
        /// </summary>
        Uleb128 = 1,

        /// <summary>
        /// The value is stored as unsigned data with 2 bytes.
        /// </summary>
        UnsignedData2 = 2,

        /// <summary>
        /// The value is stored as unsigned data with 4 bytes.
        /// </summary>
        UnsignedData4 = 3,

        /// <summary>
        /// The value is stored as unsigned data with 8 bytes.
        /// </summary>
        UnsignedData8 = 4,

        /// <summary>
        /// A signed number. The size is determined by whether this is a 32-bit or 64-bit address space.
        /// I don’t think this ever appears in a CIE or FDE in practice.
        /// </summary>
        Signed = 8,

        /// <summary>
        /// A signed LEB128. Not used in practice.
        /// </summary>
        Sleb128 = 9,

        /// <summary>
        /// The value is stored as signed data with 2 bytes. Not used in practice.
        /// </summary>
        SignedData2 = 0x0a,

        /// <summary>
        /// The value is stored as signed data with 4 bytes. Not used in practice.
        /// </summary>
        SignedData4 = 0x0b,

        /// <summary>
        /// The value is stored as signed data with 8 bytes. Not used in practice.
        /// </summary>
        SignedData8 = 0x0c,

        /// <summary>
        /// Lower part of encoding that is not used as flags and says how to encode value.
        /// </summary>
        Mask = 0x0f,

        /// <summary>
        /// Value is PC relative.
        /// </summary>
        PcRelative = 0x10,

        /// <summary>
        /// Value is text relative.
        /// </summary>
        TextRelative = 0x20,

        /// <summary>
        /// Value is data relative.
        /// </summary>
        DataRelative = 0x30,

        /// <summary>
        /// Value is relative to start of function.
        /// </summary>
        FunctionRelative = 0x40,

        /// <summary>
        /// Value is aligned: padding bytes are inserted as required to make value be naturally aligned.
        /// </summary>
        Aligned = 0x50,

        /// <summary>
        /// This is actually the address of the real value.
        /// </summary>
        Indirect = 0x80,

        /// <summary>
        /// Higher part of encoding that are modifiers.
        /// </summary>
        Modifiers = 0x70,
    }

    /// <summary>
    /// C++ provides for virtual and pure virtual structure or class member functions and for virtual base classes.
    /// This enum represents possible values for <see cref="DwarfAttribute.Virtuality"/> attribute.
    /// </summary>
    public enum DwarfVirtuality
    {
        /// <summary>
        /// None is equivalent to the absence of the <see cref="DwarfAttribute.Virtuality"/> attribute.
        /// </summary>
        None = 0,

        /// <summary>
        /// Virtual
        /// </summary>
        Virtual = 1,

        /// <summary>
        /// Pure virtual
        /// </summary>
        PureVirtual = 2,
    }

    /// <summary>
    /// DWARF v5 Unit Types DW_UT
    /// </summary>
    public enum DwarfUnitType
    {
        Unknown = 0x00,
        Compile = 0x01,
        Type = 0x02,
        Partial = 0x03,
        Skeleton = 0x04,
        SplitCompile = 0x05,
        SplitType = 0x06,
        LoUser = 0x80,
        HiUser = 0xff,
    }

    /// <summary>
    /// Check http://dwarfstd.org/doc/DWARF5.pdf, page 62.
    /// </summary>
    public enum DwarfLanguage
    {
        Unknown = 0x0000,
        C89 = 0x0001,
        C = 0x0002,
        Ada83 = 0x0003,
        CPlusPlus = 0x0004,
        Cobol74 = 0x0005,
        Cobol85 = 0x0006,
        Fortran77 = 0x0007,
        Fortran90 = 0x0008,
        Pascal83 = 0x0009,
        Modula2 = 0x000a,

        // New in DWARF v3:
        Java = 0x000b,
        C99 = 0x000c,
        Ada95 = 0x000d,
        Fortran95 = 0x000e,
        PLI = 0x000f,
        ObjC = 0x0010,
        ObjCPlusPlus = 0x0011,
        UPC = 0x0012,
        D = 0x0013,

        // New in DWARF v4:
        Python = 0x0014,

        // New in DWARF v5:
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

        // Vendor extensions:
        MipsAssembler = 0x8001,
        GoogleRenderScript = 0x8e57,
        Borland_Delphi = 0xb000,
    }
}
