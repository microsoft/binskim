// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public class PE : IDisposable
    {
        private long? length;
        private bool? isBoot;
        private string sha1Hash;
        private string sha256Hash;
        private bool? isWixBinary;
        private string[] asImports;
        private bool? isKernelMode;
        private bool? isResourceOnly;
        private FileVersionInfo version;
        private bool? isManagedResourceOnly;

        private FileStream fs;
        private PEReader peReader;
        internal SafePointer pImage; // pointer to the beginning of the file in memory
        private readonly MetadataReader metadataReader;

        public PE(string fileName)
        {
            this.FileName = Path.GetFullPath(fileName);
            this.Uri = new Uri(this.FileName);
            this.IsPEFile = false;

            try
            {
                this.fs = File.OpenRead(this.FileName);

                if (!CheckPEMagicBytes(this.fs)) { return; }

                this.peReader = new PEReader(this.fs);
                this.PEHeaders = this.peReader.PEHeaders;

                this.IsPEFile = true;

                this.pImage = new SafePointer(this.peReader.GetEntireImage().GetContent().ToBuilder().ToArray());

                if (this.IsManaged)
                {
                    this.metadataReader = this.peReader.GetMetadataReader();
                }
            }
            catch (IOException e) { this.LoadException = e; }
            catch (BadImageFormatException e) { this.LoadException = e; }
            catch (UnauthorizedAccessException e) { this.LoadException = e; }
        }

        public static bool CheckPEMagicBytes(FileStream fs)
        {
            try
            {
                byte byteRead = (byte)fs.ReadByte();
                if (byteRead != 'M') { return false; }

                byteRead = (byte)fs.ReadByte();
                return byteRead == 'Z';
            }
            finally
            {
                fs.Seek(0, SeekOrigin.Begin);
            }
        }

        public void Dispose()
        {
            if (this.peReader != null)
            {
                this.peReader.Dispose();
                this.peReader = null;
            }

            if (this.fs != null)
            {
                this.fs.Dispose();
                this.fs = null;
            }
        }


        public Exception LoadException { get; set; }

        public Uri Uri { get; set; }

        public string FileName { get; set; }

        public PEHeaders PEHeaders { get; private set; }

        public bool IsPEFile { get; set; }

        public bool IsDotNetNative
        {
            get
            {
                PEHeader peHeader = this.PEHeaders.PEHeader;
                if ((peHeader.DllCharacteristics & DllCharacteristics.AppContainer) == 0)
                {
                    return false;
                }

                if (this.Imports != null)
                {
                    for (int i = 0; i < this.Imports.Length; i++)
                    {
                        if (this.Imports[i].Equals("mrt100.dll", StringComparison.OrdinalIgnoreCase) ||
                            this.Imports[i].Equals("mrt100_app.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                return this.IsDotNetNativeBootstrapExe;
            }
        }

        public bool IsNativeUniversalWindowsPlatform
        {
            get
            {
                if (this.IsManaged)
                {
                    return false;
                }

                if (this.Imports != null)
                {
                    for (int i = 0; i < this.Imports.Length; i++)
                    {
                        if (this.Imports[i].Equals("MSVCP140_APP.dll", StringComparison.OrdinalIgnoreCase) ||
                            this.Imports[i].Equals("VCRUNTIME140_APP.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public bool Is64Bit => this.PEHeaders.PEHeader != null
                    ? this.PEHeaders.PEHeader.Magic == PEMagic.PE32Plus
                    : false;

        public CodeViewDebugDirectoryData CodeViewDebugDirectoryData
        {
            get
            {
                foreach (DebugDirectoryEntry debugDirectoryEntry in this.peReader.ReadDebugDirectory())
                {
                    if (debugDirectoryEntry.Type == DebugDirectoryEntryType.CodeView)
                    {
                        return this.peReader.ReadCodeViewDebugDirectoryData(debugDirectoryEntry);
                    }
                }
                return new CodeViewDebugDirectoryData();
            }
        }

        public string[] Imports
        {
            get
            {
                if (this.asImports == null)
                {
                    DirectoryEntry importTableDirectory = this.PEHeaders.PEHeader.ImportTableDirectory;
                    if (this.PEHeaders.PEHeader.ImportTableDirectory.Size == 0)
                    {
                        this.asImports = new string[0];
                    }
                    else
                    {
                        var sp = new SafePointer(this.pImage.array, importTableDirectory.RelativeVirtualAddress);

                        var al = new ArrayList();

                        if (sp.Address != 0)
                        {
                            sp = this.RVA2VA(sp);

                            while (((uint)sp != 0) || ((uint)(sp + 12) != 0) || ((uint)(sp + 16) != 0))
                            {
                                SafePointer spName = sp;
                                spName.Address = (int)(uint)(sp + 12);

                                spName = this.RVA2VA(spName);

                                string name = (string)(spName);

                                al.Add(name);

                                sp += 20;   // size of struct
                            }
                        }

                        this.asImports = (string[])al.ToArray(typeof(string));
                    }
                }

                return this.asImports;
            }
        }

        public SafePointer RVA2VA(SafePointer rva)
        {
            // find which section is our rva in
            var ish = new SectionHeader();
            foreach (SectionHeader sectionHeader in this.PEHeaders.SectionHeaders)
            {
                if ((rva.Address >= sectionHeader.VirtualAddress) &&
                    (rva.Address < sectionHeader.VirtualAddress + sectionHeader.SizeOfRawData))
                {
                    ish = sectionHeader;
                    break;
                }
            }

            if (ish.VirtualAddress == 0)
            {
                throw new InvalidOperationException("RVA does not belong to any section");
            }

            // calculate the VA
            rva.Address = (rva.Address - ish.VirtualAddress + ish.PointerToRawData);

            return rva;
        }

        public byte[] ImageBytes
        {
            get
            {
                if (this.pImage.array != null)
                {
                    return this.pImage.array;
                }

                throw new InvalidOperationException("Image bytes cannot be retrieved when data is backed by a stream.");
            }
        }


        /// <summary>
        /// Calculate SHA1 over the file contents
        /// </summary>
        public string SHA1Hash
        {
            get
            {
                if (this.sha1Hash != null)
                {
                    return this.sha1Hash;
                }

                // processing buffer
                byte[] buffer = new byte[4096];

                // create the hash object
                using (var sha1 = SHA1.Create())
                {
                    // open the input file
                    using (var fs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
                    {
                        int readBytes = -1;

                        // pump the file through the hash
                        do
                        {
                            readBytes = fs.Read(buffer, 0, buffer.Length);

                            sha1.TransformBlock(buffer, 0, readBytes, buffer, 0);
                        } while (readBytes > 0);

                        // need to call this to finalize the calculations
                        sha1.TransformFinalBlock(buffer, 0, readBytes);
                    }

                    // get the string representation
                    this.sha1Hash = BitConverter.ToString(sha1.Hash).Replace("-", "");
                }

                return this.sha1Hash;
            }
        }

        /// <summary>
        /// Calculate SHA256 hash of file contents
        /// </summary>
        public string SHA256Hash
        {
            get
            {
                if (this.sha256Hash != null)
                {
                    return this.sha256Hash;
                }
                this.sha256Hash = ComputeSha256Hash(this.FileName);

                return this.sha256Hash;
            }
        }

        public static string ComputeSha256Hash(string fileName)
        {
            string sha256Hash = null;

            try
            {
                using (FileStream stream = File.OpenRead(fileName))
                {
                    using (var bufferedStream = new BufferedStream(stream, 1024 * 32))
                    {
                        using (var algorithm = SHA256.Create())
                        {
                            byte[] checksum = algorithm.ComputeHash(bufferedStream);
                            sha256Hash = BitConverter.ToString(checksum).Replace("-", string.Empty);
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return sha256Hash;
        }

        /// <summary>
        /// File length
        /// </summary>
        public long Length
        {
            get
            {
                if (this.length != null)
                {
                    return (long)this.length;
                }

                var fi = new FileInfo(this.FileName);
                this.length = fi.Length;

                return (long)this.length;
            }
        }


        /// <summary>
        /// Windows OS file version information
        /// </summary>
        public FileVersionInfo FileVersion
        {
            get
            {
                if (this.version == null)
                {
                    this.version = FileVersionInfo.GetVersionInfo(Path.GetFullPath(this.FileName));
                }
                return this.version;
            }
        }


        public Packer Packer
        {
            get
            {
                if (this.PEHeaders != null)
                {
                    foreach (SectionHeader sh in this.PEHeaders.SectionHeaders)
                    {
                        if (sh.Name.StartsWith("UPX")) { return Packer.Upx; }
                    }
                }
                return Packer.UnknownOrNotPacked;
            }
        }

        public bool IsPacked => this.Packer != Packer.UnknownOrNotPacked;

        /// <summary>
        /// Returns true if the PE is managed
        /// </summary>
        public bool IsManaged => this.PEHeaders != null && this.PEHeaders.CorHeader != null;

        /// <summary>
        /// Returns true if the PE is pure managed
        /// </summary>
        public bool IsILOnly => this.PEHeaders?.CorHeader != null &&
                       (this.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == CorFlags.ILOnly;

        /// <summary>
        /// Returns true if the PE is a partially or completed 'ahead of time' compiled assembly
        /// </summary>
        public bool IsILLibrary => this.PEHeaders?.CorHeader != null &&
                       (this.PEHeaders.CorHeader.Flags & CorFlags.ILLibrary) == CorFlags.ILLibrary;

        /// <summary>
        /// Returns true if the PE is a mixed mode assembly
        /// </summary>
        public bool IsMixedMode => this.PEHeaders?.CorHeader != null &&
                       (this.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0;

        /// <summary>
        /// Returns true if the only directory present is Resource Directory (this also covers hxs and hxi files)
        /// </summary>
        public bool IsResourceOnly
        {
            get
            {
                if (this.isResourceOnly != null)
                {
                    return (bool)this.isResourceOnly;
                }

                if (this.IsILOnly)
                {
                    this.isResourceOnly = this.IsManagedResourceOnly;
                    return this.isResourceOnly.Value;
                }

                PEHeader peHeader = this.PEHeaders.PEHeader;
                if (peHeader == null)
                {
                    this.isResourceOnly = false;
                    return this.isResourceOnly.Value;
                }

                // IMAGE_DIRECTORY_ENTRY_RESOURCE == 2
                if (peHeader.ResourceTableDirectory.RelativeVirtualAddress == 0)
                {
                    this.isResourceOnly = false;
                    return this.isResourceOnly.Value;
                }

                this.isResourceOnly =
                       (peHeader.ThreadLocalStorageTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_TLS == 9
                        peHeader.ImportAddressTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_IAT == 12
                        peHeader.GlobalPointerTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_GLOBALPTR == 8
                        peHeader.DelayImportTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT == 13
                        peHeader.BoundImportTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT == 11
                        peHeader.LoadConfigTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG == 10
                        peHeader.CopyrightTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_ARCHITECTURE == 7
                        peHeader.ExceptionTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_EXCEPTION == 3	
                        peHeader.CorHeaderTableDirectory.RelativeVirtualAddress == 0 && // IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR == 14
                        peHeader.ImportTableDirectory.RelativeVirtualAddress == 0); // IMAGE_DIRECTORY_ENTRY_IMPORT == 1

                if (this.isResourceOnly.Value &&
                    peHeader.ExportTableDirectory.RelativeVirtualAddress != 0 && // IMAGE_DIRECTORY_ENTRY_EXPORT = 0;
                    peHeader.SizeOfCode > 0)
                {
                    // We require special checks in the event of a non-zero export table directory value
                    // If the binary only contains forwarders, we should regard it as not containing code
                    this.isResourceOnly = false;
                }


                return this.isResourceOnly.Value;

                // These are explicitly ignored. Debug info is sometimes present in resource
                // only binaries. We've seen cases where help resource-only DLLs had a bogus
                // non-empty relocation section. Security digital signatures are ok.
                //
                // IMAGE_DIRECTORY_ENTRY_SECURITY  = 4;	// Security Directory
                // IMAGE_DIRECTORY_ENTRY_BASERELOC = 5;	// Base Relocation Table
                // IMAGE_DIRECTORY_ENTRY_DEBUG     = 6; // Debug Directory
            }
        }

        /// <summary>
        /// Returns true if the assembly is pure managed and has no methods
        /// </summary>
        public bool IsManagedResourceOnly
        {
            get
            {
                if (this.isManagedResourceOnly != null)
                {
                    return this.isManagedResourceOnly.Value;
                }

                if (!this.IsILOnly)
                {
                    this.isManagedResourceOnly = false;
                    return this.isManagedResourceOnly.Value;
                }

                this.isManagedResourceOnly = this.metadataReader.MethodDefinitions.Count == 0;
                return this.isManagedResourceOnly.Value;
            }
        }

        private ManagedPlatform managedPlatform;

        public bool IsDotNetCore
        {
            get
            {
                if (this.IsManaged && this.managedPlatform == ManagedPlatform.Unknown)
                {
                    this.managedPlatform = ComputeIsDotNetCore(this.metadataReader);
                }
                return this.managedPlatform == ManagedPlatform.DotNetCore;
            }
        }

        public bool IsDotNetCoreBootstrapExe =>
                // The .NET core bootstrap exe is a generated native binary that loads
                // its corresponding .NET core application entry point dll.
                !this.IsDotNetCore
                    && this.CodeViewDebugDirectoryData.Path != null
                    && (this.CodeViewDebugDirectoryData.Path.EndsWith("apphost.pdb", StringComparison.OrdinalIgnoreCase)
                        || this.CodeViewDebugDirectoryData.Path.EndsWith("singlefilehost.pdb", StringComparison.OrdinalIgnoreCase));

        public bool IsDotNetNativeBootstrapExe
        {
            get
            {
                if (this.IsDotNetCore) { return false; }
                if (this.Imports == null || this.Imports.Length != 1) { return false; }

                string correspondingDllName = Path.GetFileNameWithoutExtension(this.Uri.OriginalString) + ".dll";

                return this.Imports[0].Equals(correspondingDllName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsDotNetStandard
        {
            get
            {
                if (this.IsManaged && this.managedPlatform == ManagedPlatform.Unknown)
                {
                    this.managedPlatform = ComputeIsDotNetCore(this.metadataReader);
                }
                return this.managedPlatform == ManagedPlatform.DotNetStandard;
            }
        }

        public bool IsDotNetFramework
        {
            get
            {
                if (this.IsManaged && this.managedPlatform == ManagedPlatform.Unknown)
                {
                    this.managedPlatform = ComputeIsDotNetCore(this.metadataReader);
                }
                return this.managedPlatform == ManagedPlatform.DotNetFramework;
            }
        }

        internal static ManagedPlatform ComputeIsDotNetCore(MetadataReader metadataReader)
        {
            foreach (AssemblyReferenceHandle handle in metadataReader.AssemblyReferences)
            {
                AssemblyReference assemblyReference = metadataReader.GetAssemblyReference(handle);
                StringHandle stringHandle = assemblyReference.Name;
                string assemblyName = metadataReader.GetString(stringHandle);

                switch (assemblyName)
                {
                    case "mscorlib":
                    {
                        return ManagedPlatform.DotNetFramework;
                    }

                    case "System.Runtime":
                    {
                        return ManagedPlatform.DotNetCore;
                    }

                    case "netstandard":
                    {
                        return ManagedPlatform.DotNetStandard;
                    }

                    default:
                    {
                        break;
                    }
                }
            }

            throw new InvalidOperationException("Could not identify managed platform.");
        }

        /// <summary>
        /// Returns true is the binary is likely compiled for kernel mode
        /// </summary>
        public bool IsKernelMode
        {
            get
            {
                if (this.isKernelMode != null)
                {
                    return (bool)this.isKernelMode;
                }


                this.isKernelMode = false;

                if (!this.IsPEFile || this.PEHeaders.PEHeader == null)
                {
                    return this.isKernelMode.Value;
                }

                string[] imports = this.Imports;

                foreach (string import in imports)
                {
                    if (import.StartsWith("ntoskrnl.exe", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("hal.dll", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("scsiport.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("win32k.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("ataport.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("drmk.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("ks.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("mcd.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("pciidex.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("storport.sys", StringComparison.OrdinalIgnoreCase) ||
                        import.StartsWith("tape.sys", StringComparison.OrdinalIgnoreCase))
                    {
                        this.isKernelMode = true;
                        break;
                    }
                }

                return this.isKernelMode.Value;
            }
        }

        /// <summary>
        /// Returns true if the binary is built for XBox
        /// </summary>
        public bool IsXBox => this.PEHeaders.PEHeader != null
                    ? this.PEHeaders.PEHeader.Subsystem == System.Reflection.PortableExecutable.Subsystem.Xbox
                    : false;

        public bool IsBoot
        {
            get
            {
                if (this.isBoot != null)
                {
                    return (bool)this.isBoot;
                }

                this.isBoot = false;

                if (this.PEHeaders.PEHeader != null)
                {
                    //
                    // Currently SubsystemVersion is an optional field but I would hope we can use this in the future
                    //
                    //Version ssVer = this.SubsystemVersion;

                    this.isBoot = this.Subsystem == Subsystem.EfiApplication ||
                                this.Subsystem == Subsystem.EfiBootServiceDriver ||
                                this.Subsystem == Subsystem.EfiRom ||
                                this.Subsystem == Subsystem.EfiRuntimeDriver ||
                                (int)this.Subsystem == 16; // BOOT_APPLICATION
                }

                return this.isBoot.Value;
            }
        }

        /// <summary>
        /// Machine type
        /// </summary>
        public Machine Machine => this.PEHeaders.PEHeader != null
                    ? this.PEHeaders.CoffHeader.Machine
                    : Machine.Unknown;

        /// <summary>
        /// Subsystem type
        /// </summary>
        public Subsystem Subsystem => this.PEHeaders.PEHeader != null
                    ? this.PEHeaders.PEHeader.Subsystem
                    : Subsystem.Unknown;

        /// <summary>
        /// OS version from the PE Optional Header
        /// </summary>
        public Version OSVersion
        {
            get
            {
                PEHeader optionalHeader = this.PEHeaders.PEHeader;

                if (optionalHeader != null)
                {
                    ushort major = optionalHeader.MajorOperatingSystemVersion;
                    ushort minor = optionalHeader.MinorOperatingSystemVersion;

                    return new Version(major, minor);
                }

                return null;
            }
        }

        /// <summary>
        /// Subsystem version from the PE Optional Header
        /// </summary>
        public Version SubsystemVersion
        {
            get
            {
                PEHeader optionalHeader = this.PEHeaders.PEHeader;

                if (optionalHeader != null)
                {
                    ushort major = optionalHeader.MajorSubsystemVersion;
                    ushort minor = optionalHeader.MinorSubsystemVersion;

                    return new Version(major, minor);
                }

                return null;
            }
        }

        /// <summary>
        /// Linker version from the PE Optional Header
        /// </summary>
        public Version LinkerVersion
        {
            get
            {
                PEHeader optionalHeader = this.PEHeaders.PEHeader;

                if (optionalHeader != null)
                {
                    byte major = optionalHeader.MajorLinkerVersion;
                    byte minor = optionalHeader.MinorLinkerVersion;

                    return new Version(major, minor);
                }

                return null;
            }
        }

        public bool IsWixBinary
        {
            get
            {
                if (this.isWixBinary != null)
                {
                    return (bool)this.isWixBinary;
                }

                this.isWixBinary = false;

                if (this.PEHeaders?.SectionHeaders != null)
                {
                    foreach (SectionHeader sectionHeader in this.PEHeaders.SectionHeaders)
                    {
                        if (sectionHeader.Name == ".wixburn")
                        {
                            this.isWixBinary = true;
                            break;
                        }
                    }
                }

                return this.isWixBinary.Value;
            }
        }
    }
}
