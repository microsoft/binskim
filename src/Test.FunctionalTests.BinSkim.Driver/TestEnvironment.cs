// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class TestEnvironment
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            // MTP does not apply the VSTest runsettings environment to the test process.
            Environment.SetEnvironmentVariable("System_TeamProjectId", "TestProjectId");
            Environment.SetEnvironmentVariable("System_TeamProject", "TestSystemTeamProject");
            Environment.SetEnvironmentVariable("Build_Repository_ID", "TestBuildRepositoryID");
            Environment.SetEnvironmentVariable("System_CollectionId", "TestSystemCollectionId");
            Environment.SetEnvironmentVariable("Build_BuildId", "TestBuildBuildId");
            Environment.SetEnvironmentVariable("Build_Repository_Name", "TestBuildRepositoryName");
            Environment.SetEnvironmentVariable("System_CollectionUri", "TestSystemCollectionUri");
            Environment.SetEnvironmentVariable("System_DefinitionId", "TestSystemDefinitionId");
            Environment.SetEnvironmentVariable("Build_DefinitionName", "TestBuildDefinitionName");
        }
    }
}
