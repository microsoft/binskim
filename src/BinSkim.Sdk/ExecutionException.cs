// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class ExecutionException : Exception
    {
        private readonly string stackTrace;

        private readonly string originalType;

        public ExecutionException(string type, string message, string stackTrace, Exception innerException)
            : base(message, innerException)
        {
            this.originalType = type;
            this.stackTrace = stackTrace;
        }

        public override string ToString()
        {
            return $"{this.originalType},{this.Message},{this.StackTrace}";
        }

        public string OriginalType => this.originalType;

        public override string StackTrace => this.stackTrace;
    }
}
