// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Repeat Attribute for Xunit testing.
    /// </summary>
    public sealed class RepeatAttribute : DataAttribute
    {
        private readonly int _count;

        public RepeatAttribute(int count)
        {
            const int minimumCount = 1;
            if (count < minimumCount)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(count),
                    message: "Repeat count must be greater than 0.");
            }
            _count = count;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            foreach (int iterationNumber in Enumerable.Range(start: 1, count: _count))
            {
                yield return new object[] { iterationNumber };
            }
        }
    }
}
