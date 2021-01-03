# Rules and Errors Troubleshooting Guide

## ERR997.ExceptionLoadingPdb

*Example message*:

     error ERR997.ExceptionLoadingPdb : BA2002 : 'test.exe' was not evaluated for check 'DoNotIncorporateVulnerableDependencies' because its PDB could not be loaded.(E_PDB_NO_DEBUG_INFO)

Several BinSkim checks (documented in the [User Guide]()) require PDBs in order to perform analysis. If a PDB can't be loaded or if PDB information has been stripped or simply not emitted, BinSkim will raise an `ERR997.ExceptionLoadingPdb` message. The error message will include an HRESULT value describing the failure (as in the example above). The two most common error conditions are `E_PDB_NOT_FOUND` and `E_PDB_NO_DEBUGINFO`.

### Resolving `E_PDB_NO_DEBUG_INFO`

The MSVC linker [\DEBUG](https://docs.microsoft.com/en-us/cpp/build/reference/debug-generate-debug-info?view=vs-2019) command is used to configure PDB production. To set this linker option in the Visual Studio development environment:

1. Right-click the project and select `Properties` or hit `ALT-ENTER`.
2. Expand the `Linker` folder.
3. Click the `Debugging` property page.
4. Modify the `Generate Debug Info` property.

### Resolving `E_PDB_NOT_FOUND`

By default, BinSkim will only look for a PDB alongside the binary under analysis. If a build persists PDBs to an alternate location, this path must be specified b using the `--local-symbol-directories` argument (which accepts a semicolon-delimted list of local paths containing PDBs). The `--sympath` argument must be used to configure retrieving symbols from a remote symbol server. This is typically required when analyzing 3rd party dependencies in a project, such as binaries from a NuGet package.

**NOTE:** Team's that utilize 3rd-party components assume the security risk of those dependencies. It is therefore important to acquired PDBs for any 3rd party code that is included in a production system.

#### Troubleshooting steps

1. **If a binary is compiled by your build process**: ensure that the build is producing a PDB for the file. The PDB must be persisted to the same location as the scanned binary (or a path to the PDB location must be included in the `--local-symbol-directories` argument).
2. **If the PDB is associated with an external dependency**: add or review your `--sympath` argument to ensure it references all required symbol servers. See the [User Guide](https://github.com/microsoft/binskim/blob/master/docs/UserGuide.md) for mor information on this or other command-line arguments. It is important to performance and reliability reasons that your `--sympath` argument include a `CACHE*` argument that points to a writable local cache.
3. **Enable tracing**: you can enable the `--trace PdbLoad` argument as of BinSkim version 1.7.0 in order to capture BinSkim's PDB path probing logic. When enabled, BinSkim will show every file path that's examine in order to locate a PDB.
4. **Rerun analysis**: Retrieving a PDB from a symbol server is an inherently unreliable operation, as the server may not be available or may simply fail a specific request. If your PDB failures are for 3rd party PDBs retrieved from a symbol and are intermittent, this may be the problem.
