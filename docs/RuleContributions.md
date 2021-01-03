# Making a BinSkim Rule Contribution

1. File a (or locate an existing) BinSkim issue that serves as the specification for the rule.
2. Create shell rule.
3. Prepare test assets for the rule.
4. Author standard unit tests.
5. Implement the analysis.
6. Review impact of new analysis on test set and update baselines if necessary.

----

## File or locate a rule request

- Create a rule request issue in the BinSkim repository by clicking `New issue` and selecting the `Rule request` template (or just click [here](https://github.com/microsoft/binskim/issues/new?assignees=&labels=rule-request&template=rule-request.md&title=%5BRULE+REQUEST%5D+Concise+description+of+new+analysis)).
- The issue templates contains additional guidance for authoring a complete rule specification.
- Continue to work with a repository maintainer to iron out details in implementation, etc.
- Rules marked with the `approved` label are ready to implement.
- See the [CET shadow stack compatibility](https://github.com/microsoft/binskim/issues/277) rule specification for an example.

## Create shell rule for implementation

1. Create a new rule id constant for the check in the [RuleIds](https://github.com/microsoft/binskim/blob/main/src/BinSkim.Rules/RuleIds.cs) class. The name of the property should match the rule friendly name, e.g. `EnableControlEnforcementTechnologyShadowStack` and its value should be the rule identifier, e.g., `BA2025`.
2. Open [RuleResources](https://github.com/microsoft/binskim/blob/main/src/BinSkim.Rules/RuleResources.resx) and create new user-facing strings for pass and fail conditions. By convention, these strings follow a naming schema that includes the rule identifier, the failure level and an optional additional description of the pass or fail condition, all separated by an underscore, e.g., `BA2025_Pass` and `BA2025_Error`. The string values should be retrieved from the approved rule specific/issue. You can start rules development with placeholder values, obviously.
3. Add the rule description from the specification issue to a string named as RULEID_FRIENDLYNAME_Decription, e.g., `BA2025_EnableControlEnforcementTechnologyShadowStack_Description`. Again, you can use a placeholder if this text is still under refinement.
4. Open the shell rule starter code in [BAXXXX.RuleFriendlyName.cs](BAXXXX.RuleFriendlyName.cs) and save a copy to either the [ELFRules] or [PERules] directory, depending on the target platform for the check. Rename the file on save to conform to the actual assigned rule id and friendly rule name, e.g., `BA2025.EnableControlEnforcementTechnologyShadowStack`.
5. Find/Replace all occurrences of `BAXXXX` in this file with the rule id.
6. Find/Replace all occurrences of `RULEFRIENDLYNAME` in this file with the rule name.

## Prepare test assets

- Every rule should be tested against binaries that explicitly fail the check as well as binaries designed to pass.
- Both passing and failing binaries should be updated with other security mitigations. I.e., the goal is for test binaries to be entirely clean in the `pass` case and to only fire results for the new check in the `rule` case.
- Create a directory in the rules functional test directory that matches the rule id and friendly name, separated with a dot character, e.g. [BA2025.EnableControlEnforcementTechnologyShadowStack](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestsData/BA2025.EnableControlEnforcementTechnologyShadowStack).
- Create directories named [Pass](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestsData/BA2025.EnableControlEnforcementTechnologyShadowStack/Pass) and [Fail](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestsData/BA2025.EnableControlEnforcementTechnologyShadowStack/Fail) in this directory and copy relevant secure and vulnerable test binaries to their respective location.
By convention, test binary names indicate their language, bittedness/processor, toolchain, and kind, with each attribute separated by an underscore. `Native_x64_VS2019_Console.exe`, for example, indicates a C++ Intel 64-bit console application compiled by the Microsoft Visual Studio 2019 toolchain.
- In some cases, it may be useful to create a specific binary to test proper return of the BinSkim `notApplicable` result (which indicates that the binary itself is not a relevant candidate for analysis). For many checks, the standard BinSkim "zoo" of test binaries can be used to verify proper enforcement of applicability.

## Author standard unit tests

1. Open [RuleTestShells.cs](RuleTestShells.cs) and copy the three shell methods into the actual BinSkim [RuleTests.cs](https://github.com/microsoft/binskim/blob/main/src/Test.FunctionalTests.BinSkim.Rules/RuleTests.cs) file.
2. Replace `BAXXX` and `RULEFRIENDLYNAME` in the test methods with the actual rule id and friendly name, leaving method names such as `BA2025_EnableControlEnforcementTechnologyShadowStack_Pass`.
3. Update the test methods, as per the code comments, to properly configure analysis. This mostly entails configuring checks to understand the applicability of a check to various binary conditions (e.g., whether the binary is 32-bit, an MSIL image, etc.).
4. Open the test explorer window and type your rule id and name prefix in the search field, e.g. `BA2025_EnableControlEnforcementTechnologyShadowStack`. You should be able to see your three tests. If you run them, they should fail. :)
