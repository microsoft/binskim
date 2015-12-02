// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    public class UnhandledExceptionAnalyzingTarget : IRuleContext
    {
        public static UnhandledExceptionAnalyzingTarget Instance = new UnhandledExceptionAnalyzingTarget();

        public string Id
        {
            get
            {
                return "BA0998";
            }
        }

        public string Name
        {
            get
            {
                return nameof(UnhandledExceptionAnalyzingTarget);
            }
        }
    }
}
