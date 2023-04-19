// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Coyote;
using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.SystematicTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.CoyoteTests
{
    [TestClass]
    public class BasicCoyoteTests
    {
        /// <summary>
        /// This is a very simple concurrency unit test, where the bug is hard to manifest
        /// using traditional test infrastructure. This test serves as a template for using
        /// Coyote testing (which is able to rapidly reveal the assertion failure).
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTaskAsync()
        {
            int value = 0;
            Task task = Task.Run(() =>
            {
                value = 1;
            });

            Specification.Assert(value == 0, "value is 1");
            await task;
        }

        [TestMethod, TestCategory("NightlyTest")]
        public void SystematicTestScenario()
        {
            RunSystematicTest(TestTaskAsync);
        }


        private static void RunSystematicTest(Func<Task> test)
        {
            // Configuration for how to run a concurrency unit test with Coyote.
            Configuration config = Configuration
                .Create()
                .WithMaxSchedulingSteps(5000)
                .WithTestingIterations(1000);

            async Task TestActionAsync()
            {
                await test();
            };

            var testingEngine = TestingEngine.Create(config, TestActionAsync);

            try
            {
                testingEngine.Run();

                string assertionText = testingEngine.TestReport.GetText(config);
                assertionText +=
                    $"{Environment.NewLine} Random Generator Seed: " +
                    $"{testingEngine.TestReport.Configuration.RandomGeneratorSeed}{Environment.NewLine}";
                foreach (string bugReport in testingEngine.TestReport.BugReports)
                {
                    assertionText +=
                    $"{Environment.NewLine}" +
                    "Bug Report: " + bugReport.ToString(CultureInfo.InvariantCulture);
                }

                Assert.IsTrue(testingEngine.TestReport.NumOfFoundBugs == 0, assertionText);

                Console.WriteLine(testingEngine.TestReport.GetText(config));
            }
            finally
            {
                testingEngine.Stop();
            }
        }

    }
}
