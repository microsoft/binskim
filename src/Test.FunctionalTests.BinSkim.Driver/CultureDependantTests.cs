// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// CS0436: The type 'CultureDependantTests' in 'X' conflicts with the imported type
// 'CultureDependantTests' in 'Y'. Using the type defined in 'X'.
//
// This type is injected into all test assemblies and this warning therefore
// occurs when test assemblies reference each other. In that case, each test
// assembly will prefer its own copy, which is fine.
#pragma warning disable CS0436

using System;
using System.Globalization;
using System.Threading;

namespace Microsoft.CodeAnalysis.IL
{
    public class CultureDependantTests : IDisposable
    {
        private readonly CultureInfo Culture;

        private readonly CultureInfo originalCulture;
        private readonly CultureInfo originalUICulture;


        public CultureDependantTests(string culture)
        {
            Culture = new CultureInfo(culture, false);

            originalCulture = Thread.CurrentThread.CurrentCulture;
            originalUICulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = Culture;
            Thread.CurrentThread.CurrentUICulture = Culture;

            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }


        public void Dispose()
        {
            if (originalCulture is not null)
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
            if (originalUICulture is not null)
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }

            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }
    }
}
