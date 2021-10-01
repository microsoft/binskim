// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.ApplicationInsights.Channel;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class StubTelemetryChannel : ITelemetryChannel
    {
        public delegate void TelemetryAction(ITelemetry telemetry);

        /// <summary>
        /// Initializes a new instance of the <see cref="StubTelemetryChannel"/> class.
        /// </summary>
        public StubTelemetryChannel()
        {
            this.OnSend = telemetry => { };
        }

        /// <summary>
        /// Gets or sets a value indicating whether this channel is in developer mode.
        /// </summary>
        public bool? DeveloperMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the channel's URI. To this URI the telemetry is expected to be sent.
        /// </summary>
        public string EndpointAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to throw an error.
        /// </summary>
        public bool ThrowError { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked by the <see cref="Send"/> method.
        /// </summary>
        public TelemetryAction OnSend { get; set; }

        /// <summary>
        /// Implements the <see cref="ITelemetryChannel.Send"/> method by invoking the <see cref="OnSend"/> callback.
        /// </summary>
        public void Send(ITelemetry item)
        {
            if (this.ThrowError)
            {
                throw new Exception("test error");
            }

            this.OnSend(item);
        }

        /// <summary>
        /// Implements the <see cref="IDisposable.Dispose"/> method.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Mock for the Flush method in <see cref="ITelemetryChannel"/>.
        /// </summary>
        public void Flush()
        {
        }
    }
}
