// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinSkim
{
    internal abstract class DriverCommand<T>
    {
        abstract public int Run(T options);

        protected const int FAILED = 1;
        protected const int SUCCEEDED = 0;
    }
}
