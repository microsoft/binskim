# Troubleshooting

## PDB loading failures

Several Windows PE rules require Program Database (PDB) information. When a required PDB cannot be loaded, BinSkim emits an `ERR997.ExceptionLoadingPdb` tool notification that includes the underlying PDB error.

### `E_PDB_NOT_FOUND`

BinSkim did not find a matching PDB.

1. Confirm that the build produced a PDB for the binary.
2. Place the PDB beside the binary or pass its directory through `--local-symbol-directories`.
3. For external dependencies, configure the required symbol servers through `--sympath`.
4. Include a writable local cache in the symbol path.
5. Run again with `--trace PdbLoad` to inspect every path BinSkim examined.

Remote symbol retrieval can fail intermittently. If a retry succeeds, investigate symbol-server reliability before suppressing the notification.

### `E_PDB_NO_DEBUG_INFO`

The binary does not contain the information required to locate or use its PDB.

For Microsoft C and C++ projects, confirm that the linker is configured to generate debug information and that the produced binary and PDB come from the same build. Rebuilding only one of the files can produce a signature mismatch.

## `BA2024.EnableSpectreMitigations`

`BA2024` reports modules that were not compiled with the expected speculative-execution mitigations.

Warnings involving Microsoft C++ runtime libraries usually mean that the project linked an unmitigated runtime variant.

1. Open the Visual Studio Installer and modify the toolchain used by the build.
2. Under Individual components, install the Spectre-mitigated libraries for every target architecture and toolset used by the project.
3. Configure the project and its static-library dependencies to use Spectre mitigation.
4. Rebuild all affected libraries and final binaries.
5. Analyze the rebuilt output and matching PDBs.

Debug and release configurations can select different runtime libraries. Confirm that the mitigated library variant exists for the configuration being analyzed.

## `BA2022.SignSecurely`

`BA2022` uses Windows Authenticode validation and therefore depends on the certificate and trust policy of the machine running BinSkim. A build environment can produce a different result from a target environment with different trusted or distrusted certificate stores.

When investigating an unexpected result:

1. Confirm that the same binary hash was examined in each environment.
2. Verify the signature with Windows signing tools on the analysis machine.
3. Compare the certificate stores and trust policy of environments that produce different results.
4. Preserve the SARIF notification and Windows verification error for diagnosis.

Do not treat a successful retry as proof that the original result was incorrect. Establish whether the binary, signing state, trust policy, or analysis environment changed.
