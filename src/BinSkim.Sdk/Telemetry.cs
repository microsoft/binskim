// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    /// <summary>
    /// Wraps an Application Insights telemetry pipeline.
    /// If Application Insights is enabled, then the <see cref="TelemetryClient"/>
    /// property will be non-null.
    /// </summary>
    public sealed class Telemetry : IDisposable
    {
        /// <summary>
        /// The name of an environment variable that holds the connection
        /// string for an Application Insights resource.
        /// </summary>
        internal const string AppInsightsConnectionStringEnvVar = "BinskimAppInsightsConnectionString";

        /// <summary>
        /// The name of an environment variable that holds the instrumentation
        /// key for an Application Insights resource.
        /// </summary>
        internal const string AppInsightsInstrumentationKeyEnvVar = "BinskimCompilerDataAppInsightsKey";

        /// <summary>
        /// Gets the telemetry configuration. May be null if Application Insights
        /// telemetry is not enabled.
        /// </summary>
        private TelemetryConfiguration? TelemetryConfiguration { get; }

        /// <summary>
        /// Gets the telemetry client. This may be null if Application Insights
        /// telemetry is not enabled.
        /// </summary>
        public TelemetryClient? TelemetryClient { get; }

        /// <summary>
        /// Construct a new <see cref="Telemetry"/> instance from environment variables.
        /// </summary>
        /// <remarks>
        /// If environment variables aren't set, then <see cref="TelemetryClient"/> will be null.
        /// </remarks>
        public Telemetry() : this(CreateTelemetryConfigurationFromEnvironment())
        {
        }

        /// <summary>
        /// Construct a new <see cref="Telemetry"/> instance using
        /// the given <see cref="ApplicationInsights.Extensibility.TelemetryConfiguration"/>.
        /// </summary>
        /// <param name="telemetryConfiguration">The pipeline to use. May be null, in which case
        /// Application Insights is disabled.</param>
        /// <remarks>
        /// If <paramref name="telemetryConfiguration"/> is non-null, it will be
        /// disposed when this object is disposed.
        /// </remarks>
        internal Telemetry(TelemetryConfiguration? telemetryConfiguration)
        {
            if (telemetryConfiguration != null)
            {
                var telemetryClient = new TelemetryClient(telemetryConfiguration);
                ConfigureTelemetryContext(telemetryClient.Context);
                this.TelemetryClient = telemetryClient;
                this.TelemetryConfiguration = telemetryConfiguration;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Flush and close AppInsights client.
            this.TelemetryClient?.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            this.TelemetryConfiguration?.Dispose();
        }

        private static void ConfigureTelemetryContext(TelemetryContext context)
        {
            context.Session.Id = CreateRandomSessionId();
            context.Component.Version = Assembly.GetCallingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            context.Device.OperatingSystem = RuntimeInformation.OSDescription;
        }

        private static TelemetryConfiguration? CreateTelemetryConfigurationFromEnvironment()
        {
            string? connectionString = GetConnectionStringFromEnvironment();
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }

            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = connectionString
            };

            telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            return telemetryConfiguration;
        }

        private static string? GetConnectionStringFromEnvironment()
        {
            // Try connection string first.
            string? appInsightsConnectionString = CompilerDataLogger.RetrieveEnvironmentVariable(AppInsightsConnectionStringEnvVar);
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                return appInsightsConnectionString;
            }

            // Fall back to instrumentation key.
            string? appInsightsKey = CompilerDataLogger.RetrieveEnvironmentVariable(AppInsightsInstrumentationKeyEnvVar);
            if (!string.IsNullOrEmpty(appInsightsKey) && Guid.TryParse(appInsightsKey, out _))
            {
                return "InstrumentationKey=" + appInsightsKey;
            }

            // Fall back to DefaultTelemetryConnectionString.
            string defaultTelemetryConnectionString = EnvironmentResources.DefaultTelemetryConnectionString;
            if (!string.IsNullOrWhiteSpace(defaultTelemetryConnectionString))
            {
                return defaultTelemetryConnectionString;
            }

            return null;
        }

        /// <summary>
        /// Create a cryptographically random string to represent a session ID.
        /// </summary>
        /// <returns>A unique session ID.</returns>
        /// <remarks>
        /// The session ID is a Base64-encoded sequence of random bytes.
        /// The length of the encoded string will be 16 characters.
        /// </remarks>
        private static string CreateRandomSessionId()
        {
            using var rng = RandomNumberGenerator.Create();

            // The length of the random byte array should be a
            // multiple of 3 to get maximum benefit from Base64
            // encoding with no unnecessary padding characters.
            // Base64 encoding uses 4 characters for every 3 bytes,
            // so this will result in a 16 character string.
            byte[] sessionId = new byte[12];
            rng.GetBytes(sessionId);
            return Convert.ToBase64String(sessionId);
        }

        /// <summary>
        /// Log the command line arguments as a custom event.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public void LogCommandLine(string[]? args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            TelemetryClient? telemetryClient = TelemetryClient;
            if (telemetryClient == null)
            {
                return;
            }

            var item = new EventTelemetry("CommandLine");
            item.Metrics.Add("argc", args.Length);
            for (int i = 0; i < args.Length; i++)
            {
                item.Properties.Add($"arg{i}", args[i] ?? "null");
            }

            telemetryClient.TrackEvent(item);
        }
    }
}
