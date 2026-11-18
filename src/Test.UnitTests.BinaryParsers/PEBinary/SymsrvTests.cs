// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class SymsrvTests
    {
        private readonly ITestOutputHelper output;

        public SymsrvTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Pdb_LoadsFromHttpSymbolServerAndReusesOfflineCache()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            string symbolPath;
            using (var server = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(fixture.PdbPath)))
            {
                symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";
                using Pdb actual = LoadPdb(fixture, symbolPath);
                AssertPdbMatchesReference(actual, fixture);
                server.Requests.Should().Contain(fixture.RequestPath);

                string expectedDll = Path.Combine(AppContext.BaseDirectory, "symsrv.dll");
                using Process process = Process.GetCurrentProcess();
                process.Modules.Cast<ProcessModule>().Should().Contain(module =>
                    Path.GetFullPath(module.FileName).Equals(expectedDll, StringComparison.OrdinalIgnoreCase));
            }

            using Pdb cached = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(cached, fixture);
        }

        [Fact]
        public void Pdb_UsesValidCacheWithoutDownloadingAgain()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.CachedPdbPath));
            File.Copy(fixture.PdbPath, fixture.CachedPdbPath);
            using var server = new SymbolServer(fixture.RequestPath, Array.Empty<byte>(), status: 404);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";

            using Pdb actual = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(actual, fixture);
            server.Requests.Should().BeEmpty();
        }

        [Fact]
        public void Pdb_LoadsFromIndexedFileSymbolStore()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            string store = fixture.CreatePdbDirectory("store", indexed: true);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{store}";

            using Pdb actual = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(actual, fixture);
        }

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        public void Pdb_FallsBackToSecondHttpSymbolStore(int firstStatus)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            using var missing = new SymbolServer(fixture.RequestPath, Array.Empty<byte>(), status: firstStatus);
            using var available = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(fixture.PdbPath));
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{missing.Url};SRV*{fixture.CacheDirectory}*{available.Url}";

            using Pdb actual = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(actual, fixture);
            missing.Requests.Should().Contain(fixture.RequestPath);
            available.Requests.Should().Contain(fixture.RequestPath);
        }

        [Fact]
        public void Pdb_PrefersLocalSymbolsWithoutContactingServer()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            string local = fixture.CreatePdbDirectory("local", indexed: false);
            using var server = new SymbolServer(fixture.RequestPath, Array.Empty<byte>(), status: 404);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";

            using Pdb actual = LoadPdb(fixture, symbolPath, local);
            string expectedPath = Path.Combine(local, Path.GetFileName(fixture.PdbPath));
            AssertPdbMatchesReference(actual, fixture, expectedPath);
            server.Requests.Should().BeEmpty();
            File.Exists(fixture.CachedPdbPath).Should().BeFalse();
        }

        [Fact]
        public void Pdb_FallsBackFromMissingLocalSymbolsToHttpServer()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            using var server = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(fixture.PdbPath));
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";

            using Pdb actual = LoadPdb(fixture, symbolPath, fixture.CacheDirectory);
            AssertPdbMatchesReference(actual, fixture);
            server.Requests.Should().Contain(fixture.RequestPath);
        }

        [Theory]
        [InlineData(301)]
        [InlineData(302)]
        public void Pdb_FollowsHttpSymbolServerRedirect(int status)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            using var destination = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(fixture.PdbPath));
            using var redirect = new SymbolServer(
                fixture.RequestPath, Array.Empty<byte>(), status, destination.Url + fixture.RequestPath);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{redirect.Url}";

            using Pdb actual = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(actual, fixture);
            redirect.Requests.Should().Contain(fixture.RequestPath);
            destination.Requests.Should().Contain(fixture.RequestPath);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Pdb_HttpSymbolRetrievalPreservesClassicAndMsfzMetadata(bool compressed)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string directory = Path.Combine(PEBinaryTests.TestData, "PE", "Determinism", "OK");
            string pdbPath = compressed
                ? Path.Combine(directory, "msfz", "CPlusPlus_SourceLink.pdb")
                : Path.Combine(directory, "CPlusPlus_SourceLink.pdb");
            using var fixture = new SymbolFixture(
                Path.Combine(directory, "CPlusPlus_SourceLink.exe"), pdbPath, alignPdbIdentity: true);
            using var expected = new Pdb(pdbPath: fixture.PdbPath);
            string[] sourceLinks = expected.WindowsPdbGetSourceLinkDocuments().ToArray();
            sourceLinks.Should().NotBeEmpty();
            string symbolPath;
            string cachedPath;
            using (var server = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(fixture.PdbPath)))
            {
                symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";
                using Pdb actual = LoadPdb(fixture, symbolPath);
                cachedPath = actual.PdbLocation;
                if (compressed)
                {
                    // Newer symsrv isolates compressed PDBs in an msfz0 cache subdirectory.
                    string compressedPath = Path.Combine(
                        Path.GetDirectoryName(fixture.CachedPdbPath), "msfz0", Path.GetFileName(fixture.CachedPdbPath));
                    cachedPath.Should().BeOneOf(fixture.CachedPdbPath, compressedPath);
                }
                else
                {
                    cachedPath.Should().Be(fixture.CachedPdbPath);
                }
                AssertPdbMatchesReference(actual, fixture, cachedPath);
                actual.WindowsPdbGetSourceLinkDocuments().Should().Equal(sourceLinks);
                server.Requests.Should().Contain(fixture.RequestPath);
            }

            using Pdb cached = LoadPdb(fixture, symbolPath);
            AssertPdbMatchesReference(cached, fixture, cachedPath);
            cached.WindowsPdbGetSourceLinkDocuments().Should().Equal(sourceLinks);
        }

        private Pdb LoadPdb(SymbolFixture fixture, string symbolPath, string localSymbolDirectories = null)
        {
            try
            {
                return new Pdb(
                    fixture.BinaryPath, symbolPath: symbolPath, localSymbolDirectories: localSymbolDirectories, traceLoads: true);
            }
            catch (PdbException exception)
            {
                output.WriteLine(exception.LoadTrace);
                throw;
            }
        }

        [Theory]
        [InlineData(401, false)]
        [InlineData(404, false)]
        [InlineData(403, false)]
        [InlineData(200, true)]
        public void Pdb_RejectsMissingDeniedOrMismatchedServerSymbols(int status, bool mismatched)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            string responsePdb = mismatched
                ? Path.Combine(PEBinaryTests.TestData, "PE", "clangcl.pe.cpp.codeview.pdb")
                : fixture.PdbPath;
            using var server = new SymbolServer(fixture.RequestPath, File.ReadAllBytes(responsePdb), status);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";

            Action load = () =>
            {
                using var pdb = new Pdb(fixture.BinaryPath, symbolPath: symbolPath, traceLoads: true);
            };
            PdbException exception = load.Should().Throw<PdbException>().Which;
            exception.ExceptionCode.Should().Be(DiaHresult.E_PDB_NOT_FOUND);
            exception.LoadTrace.Should().NotBeNullOrWhiteSpace();
            exception.LoadTrace.Should().NotContain($"'{fixture.CachedPdbPath}'. HResult: S_OK.");
            server.Requests.Should().Contain(fixture.RequestPath);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Pdb_RejectsMalformedOrTruncatedServerPdb(bool truncated)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var fixture = new SymbolFixture();
            byte[] content = truncated
                ? File.ReadAllBytes(fixture.PdbPath).Take(64).ToArray()
                : Encoding.ASCII.GetBytes("This is not a PDB.");
            using var server = new SymbolServer(fixture.RequestPath, content);
            string symbolPath = $"SRV*{fixture.CacheDirectory}*{server.Url}";
            Action load = () =>
            {
                using var pdb = new Pdb(fixture.BinaryPath, symbolPath: symbolPath, traceLoads: true);
            };

            PdbException exception = load.Should().Throw<PdbException>().Which;
            exception.LoadTrace.Should().NotBeNullOrWhiteSpace();
            exception.LoadTrace.Should().NotContain($"'{fixture.CachedPdbPath}'. HResult: S_OK.");
            server.Requests.Should().Contain(fixture.RequestPath);
        }

        private static void AssertPdbMatchesReference(Pdb actual, SymbolFixture fixture, string expectedPath = null)
        {
            expectedPath ??= fixture.CachedPdbPath;
            using var expected = new Pdb(pdbPath: fixture.PdbPath);
            actual.PdbLocation.Should().Be(expectedPath);
            actual.GlobalScope.Guid.Should().Be(expected.GlobalScope.Guid);
            actual.IsStripped.Should().Be(expected.IsStripped);
            actual.GetCompilerNameFromCompilandDetails().Should().Be(expected.GetCompilerNameFromCompilandDetails());
            File.ReadAllBytes(actual.PdbLocation).Should().Equal(File.ReadAllBytes(fixture.PdbPath));
            actual.LoadTrace.Should().Contain($"'{expectedPath}'. HResult: S_OK.");
        }

        private sealed class SymbolFixture : IDisposable
        {
            private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("binskim-symsrv-");

            public string BinaryPath { get; }
            public string PdbPath { get; }
            public string CacheDirectory { get; }
            public string CachedPdbPath { get; }
            public string RequestPath { get; }
            private string RelativePdbPath { get; }

            public SymbolFixture(string binaryPath = null, string pdbPath = null, bool alignPdbIdentity = false)
            {
                try
                {
                    binaryPath ??= Path.Combine(PEBinaryTests.TestData, "PE", "Trace", "Case_NoPdbSideBySide", "PdbLoadTest.dll");
                    BinaryPath = Path.Combine(directory.FullName, "SymbolProbe" + Path.GetExtension(binaryPath));
                    PdbPath = pdbPath ?? Path.Combine(PEBinaryTests.TestData, "SymbolsFolder", "Trace", "Case_GoodPdb_Symbol", "vmpmem.pdb");
                    CacheDirectory = Path.Combine(directory.FullName, "cache");
                    Directory.CreateDirectory(CacheDirectory);
                    File.Copy(binaryPath, BinaryPath);

                    DebugDirectoryEntry entry;
                    CodeViewDebugDirectoryData codeView;
                    using (FileStream stream = File.OpenRead(BinaryPath))
                    using (var pe = new PEReader(stream))
                    {
                        entry = pe.ReadDebugDirectory().Single(value => value.Type == DebugDirectoryEntryType.CodeView);
                        codeView = pe.ReadCodeViewDebugDirectoryData(entry);
                    }

                    Guid guid = codeView.Guid;
                    uint age = (uint)codeView.Age;
                    if (alignPdbIdentity)
                    {
                        // The Source Link PDB was rebuilt independently; pair only the temporary PE with it.
                        using var reference = new Pdb(pdbPath: PdbPath);
                        guid = reference.GlobalScope.Guid;
                        age = reference.GlobalScope.UnderlyingSymbol.age;
                        using var writer = new BinaryWriter(File.OpenWrite(BinaryPath));
                        writer.BaseStream.Position = entry.DataPointer + 4;
                        writer.Write(guid.ToByteArray());
                        writer.Write(age);
                    }

                    string pdbName = Path.GetFileName(codeView.Path);
                    string key = guid.ToString("N").ToUpperInvariant() + age.ToString("X", CultureInfo.InvariantCulture);
                    RelativePdbPath = Path.Combine(pdbName, key, pdbName);
                    CachedPdbPath = Path.Combine(CacheDirectory, RelativePdbPath);
                    RequestPath = $"/{pdbName}/{key}/{pdbName}";
                }
                catch
                {
                    directory.Delete(recursive: true);
                    throw;
                }
            }

            public string CreatePdbDirectory(string name, bool indexed)
            {
                string path = Path.Combine(directory.FullName, name);
                string pdbPath = Path.Combine(path, indexed ? RelativePdbPath : Path.GetFileName(PdbPath));
                Directory.CreateDirectory(Path.GetDirectoryName(pdbPath));
                File.Copy(PdbPath, pdbPath);
                return path;
            }

            public void Dispose()
            {
                directory.Delete(recursive: true);
            }
        }

        private sealed class SymbolServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly Task worker;
            private readonly string requestPath;
            private readonly byte[] content;
            private readonly int status;
            private readonly string redirectLocation;

            public string Url { get; }
            public ConcurrentQueue<string> Requests { get; } = new ConcurrentQueue<string>();

            public SymbolServer(string requestPath, byte[] content, int status = 200, string redirectLocation = null)
            {
                this.requestPath = requestPath;
                this.content = content;
                this.status = status;
                this.redirectLocation = redirectLocation;
                listener.Start();
                Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
                worker = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                CancellationToken token = cancellation.Token;
                try
                {
                    while (true)
                    {
                        using TcpClient client = await listener.AcceptTcpClientAsync(token);
                        using NetworkStream stream = client.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                        string request = await reader.ReadLineAsync(token);
                        if (request == null) { continue; }
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }

                        string path = request.Split(' ')[1];
                        Requests.Enqueue(path);
                        bool matches = path.Equals(requestPath, StringComparison.OrdinalIgnoreCase);
                        int responseStatus = matches ? status : 404;
                        byte[] body = responseStatus == 200 ? content : Array.Empty<byte>();
                        string location = matches && redirectLocation != null ? $"Location: {redirectLocation}\r\n" : "";
                        string header = $"HTTP/1.1 {responseStatus} Response\r\nContent-Length: {body.Length}\r\n{location}Connection: close\r\n\r\n";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), token);
                        await stream.WriteAsync(body, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
            }

            public void Dispose()
            {
                cancellation.Cancel();
                try
                {
                    worker.GetAwaiter().GetResult();
                }
                finally
                {
                    listener.Stop();
                    cancellation.Dispose();
                }
            }
        }
    }
}
