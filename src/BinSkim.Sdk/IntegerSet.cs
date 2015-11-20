// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Options
{
    [Serializable]
    public class IntegerSet : HashSet<int>
    {
        public IntegerSet() { }

        public IntegerSet(IEnumerable<int> integers) : base(integers) { }

        protected IntegerSet(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
