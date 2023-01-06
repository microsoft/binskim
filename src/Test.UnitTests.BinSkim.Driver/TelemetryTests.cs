// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using FluentAssertions;

using Microsoft.ApplicationInsights.Extensibility;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class TelemetryTests
    {
        [Fact]
        public void Telemetry_TelemetryClientShouldBeNullIfEnvironmentVariablesNotSet()
        {
            using var telemetry = new IL.Sdk.Telemetry();
            telemetry.TelemetryClient.Should().BeNull();
        }

        [Fact]
        public void Telemetry_ShouldInitializeFromIKeyEnvironmentVariable()
        {
            try
            {
                string iKey = Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsInstrumentationKeyEnvVar, iKey);

                using var telemetry = new IL.Sdk.Telemetry();
                telemetry.TelemetryClient.Should().NotBeNull();
            }
            finally
            {
                // Clear environment variable set for this test.
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsInstrumentationKeyEnvVar, null);
            }
        }

        [Fact]
        public void Telemetry_ShouldInitializeFromConnectionStringEnvironmentVariable()
        {
            try
            {
                string connectionString = "InstrumentationKey=" + Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsConnectionStringEnvVar, connectionString);

                using var telemetry = new IL.Sdk.Telemetry();
                telemetry.TelemetryClient.Should().NotBeNull();
            }
            finally
            {
                // Clear environment variable set for this test.
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsConnectionStringEnvVar, null);
            }
        }

        [Fact]
        public void Telemetry_ShouldDisposeTelemetryConfigurationInDispose()
        {
            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = "InstrumentationKey=" + Guid.NewGuid().ToString()
            };

            using (var telemetry = new IL.Sdk.Telemetry(telemetryConfiguration))
            {
                telemetryConfiguration.TelemetryChannel.Should().NotBeNull();
            }

            telemetryConfiguration.TelemetryChannel.Should().BeNull();
        }
    }
}
