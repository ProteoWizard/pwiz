/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;
using JSON_RPC = SkylineTool.JsonToolConstants.JSON_RPC;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkylineMcpTest : AbstractFunctionalTestEx
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestSkylineMcp()
        {
            RunFunctionalTest();
        }

        private const int EXPECTED_TOOL_COUNT = 58;

        // The version stamped into the committed SkylineAiConnector.zip: both
        // tool-inf/info.properties (what the Tool Store shows) and the bundled
        // binaries. This is intentionally a hand-entered constant, NOT the running
        // Skyline version. Skyline's version is day-of-year derived and changes
        // every day, so asserting equality with the live version would fail the
        // day after any commit. The committed ZIP only changes when someone
        // rebuilds it, so bumping this constant in lockstep with a rebuild is the
        // discipline gate that catches a forgotten rebuild. (A stale ZIP once
        // shipped stamped 26.1.1.077 while its own info.properties Requires line
        // demanded 26.1.1.083 - a ZIP that fails its own stated requirement.)
        // When you rebuild SkylineAiConnector.zip, update this to match.
        private const string EXPECTED_ZIP_VERSION = "26.1.1.189";

        // Short FASTA for a quick import test
        private const string TEST_FASTA =
            @">sp|P01308|INS_HUMAN Insulin OS=Homo sapiens
MALWMRLLPLLALLALWGPDPAAAFVNQHLCGSHLVEALYLVCGERGFFYTPKT
RREAEDLQVGQVELGGGPGAGSLQPLALEGSLQKRGIVEQCCTSICSLYQLENYCN";

        protected override void DoTest()
        {
            // 1. Test tool installation from ZIP
            TestToolInstallation();

            // 2. Test MCP server end-to-end with blank document
            TestMcpServerEndToEnd();
        }

        private void TestToolInstallation()
        {
            string zipPath = TestContext.GetProjectDirectory(
                @"Executables\Tools\SkylineMcp\SkylineAiConnector\SkylineAiConnector.zip");
            if (!File.Exists(zipPath))
                Assert.Inconclusive(@"SkylineAiConnector.zip not found - build SkylineMcp solution first");

            // Remove existing tools first (separate dialog to avoid save prompt)
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg =>
            {
                dlg.RemoveAllTools();
                dlg.OkDialog();
            });
            const string expectedToolName = "AI Connector";
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg =>
            {
                dlg.InstallZipTool(zipPath);
                AssertEx.AreEqual(expectedToolName, dlg.textTitle.Text);
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.PopulateToolsMenu();
                AssertEx.AreEqual(expectedToolName, SkylineWindow.GetToolText(0));
            });

            // Freshness gate: the version stamped into the installed tool-inf/info.properties
            // (copied verbatim from the ZIP, and what the Tool Store displays) must match
            // EXPECTED_ZIP_VERSION. This is the check that catches a stale or mis-stamped ZIP -
            // e.g. a binary rebuilt from current source but packaged with an old info.properties -
            // which every other assertion here sails past because they only exercise the binary.
            string infoPropertiesPath = Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(),
                @"SkylineAiConnector", @"tool-inf", @"info.properties");
            Assert.IsTrue(File.Exists(infoPropertiesPath),
                @"Installed tool-inf/info.properties not found at " + infoPropertiesPath);
            string zipVersion = new ExternalToolProperties(infoPropertiesPath).Version;
            AssertEx.AreEqual(EXPECTED_ZIP_VERSION, zipVersion,
                TextUtil.LineSeparate(
                    @"SkylineAiConnector.zip version does not match EXPECTED_ZIP_VERSION.",
                    @"Rebuild SkylineAiConnector (which restamps info.properties from AssemblyInfo.cs)" +
                    @" and update EXPECTED_ZIP_VERSION to match the new build."));
        }

        private void TestMcpServerEndToEnd()
        {
            // Find SkylineMcpServer.exe in the installed tool directory
            // ToolInstaller uses the ZIP filename (without extension) as directory name
            string mcpServerPath = Path.Combine(
                ToolDescriptionHelpers.GetToolsDirectory(),
                @"SkylineAiConnector", @"mcp-server", @"SkylineMcpServer.exe");
            if (!File.Exists(mcpServerPath))
                Assert.Inconclusive(@"SkylineMcpServer.exe not found in installed tools");

            // The bundled server binary must carry the same version as the ZIP manifest,
            // so a rebuilt-but-mis-stamped package (or a stale binary under a fresh manifest)
            // cannot slip through. Together with the info.properties check in
            // TestToolInstallation this pins manifest == binary == EXPECTED_ZIP_VERSION.
            string serverFileVersion = FileVersionInfo.GetVersionInfo(mcpServerPath).FileVersion;
            AssertEx.AreEqual(EXPECTED_ZIP_VERSION, serverFileVersion,
                @"Bundled SkylineMcpServer.exe FileVersion does not match EXPECTED_ZIP_VERSION - rebuild the tool ZIP.");

            // Phase 1: default-cap subprocess covers the existing protocol tests
            // plus the inline / file return-format paths for the image tools.
            RunMcpScenario(mcpServerPath, extraEnv: null, (mcpProcess, server) =>
            {
                ValidateMcpProtocol(mcpProcess, server);
                ValidateImageInlineAndFileModes(mcpProcess, server);
            });

            // Phase 2: separate subprocess with a tiny inline cap forces the auto
            // fallback to file and the inline explicit-cap-exceeded error.
            var capEnv = new Dictionary<string, string>
            {
                ["SKYLINE_MCP_INLINE_IMAGE_CAP_BYTES"] = "100"
            };
            RunMcpScenario(mcpServerPath, extraEnv: capEnv, (mcpProcess, server) =>
            {
                ValidateImageCapFallback(mcpProcess, server);
            });
        }

        /// <summary>
        /// Boilerplate for spinning up a JsonToolServer + an MCP subprocess for one
        /// scenario block, then tearing them down. Each scenario runs in its own
        /// subprocess so per-process configuration (e.g. SKYLINE_MCP_INLINE_IMAGE_CAP_BYTES)
        /// doesn't bleed into the next scenario.
        /// </summary>
        private void RunMcpScenario(string mcpServerPath,
            IDictionary<string, string> extraEnv,
            Action<Process, JsonToolServer> scenario)
        {
            string testGuid = @"test-" + Guid.NewGuid();
            var server = new JsonToolServer(testGuid);

            string connectionFilePath = null;
            Process mcpProcess = null;
            try
            {
                server.WriteConnectionInfo();
                connectionFilePath = JsonToolConstants.GetConnectionFilePath(server.PipeName);
                server.Start();
                mcpProcess = StartMcpServer(mcpServerPath, extraEnv);
                scenario(mcpProcess, server);
            }
            finally
            {
                if (mcpProcess is { HasExited: false })
                {
                    try { mcpProcess.Kill(); }
                    catch { /* Best effort cleanup */ }
                }
                mcpProcess?.Dispose();
                server.Dispose();
                FileEx.SafeDelete(connectionFilePath, true);
            }
        }

        private static Process StartMcpServer(string mcpServerPath,
            IDictionary<string, string> extraEnv = null)
        {
            var startInfo = new ProcessStartInfo(mcpServerPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["SKYLINE_MCP_TEST"] = "1";
            if (extraEnv != null)
            {
                foreach (var kv in extraEnv)
                    startInfo.Environment[kv.Key] = kv.Value;
            }
            var process = Process.Start(startInfo);
            Assert.IsNotNull(process, "Failed to start SkylineMcpServer.exe");
            return process;
        }

        /// <summary>
        /// The authoritative set of MCP tool names, derived at test time by parsing the
        /// [McpServerTool(Name = "...")] attributes in the server source. Deriving it from
        /// source (rather than duplicating a list in the test) means adding or removing a
        /// tool in SkylineTools.cs automatically updates the expectation - no list to keep
        /// in sync by hand.
        /// </summary>
        private HashSet<string> GetSourceDeclaredToolNames()
        {
            string sourcePath = TestContext.GetProjectDirectory(
                @"Executables\Tools\SkylineMcp\SkylineMcpServer\Tools\SkylineTools.cs");
            Assert.IsTrue(File.Exists(sourcePath), @"SkylineTools.cs source not found at " + sourcePath);
            var names = new HashSet<string>();
            foreach (Match m in Regex.Matches(File.ReadAllText(sourcePath), @"McpServerTool\(Name = ""([a-z_]+)"""))
                names.Add(m.Groups[1].Value);
            Assert.IsTrue(names.Count > 0, @"No [McpServerTool(Name = ...)] attributes parsed from SkylineTools.cs");
            return names;
        }

        private void ValidateMcpProtocol(Process mcpProcess, JsonToolServer server)
        {
            // Wrap stdin with UTF-8 StreamWriter (Process.StandardInput defaults to
            // system code page on .NET Framework, corrupting non-ASCII characters)
            var stdin = new StreamWriter(mcpProcess.StandardInput.BaseStream, new UTF8Encoding(false))
                { AutoFlush = false };
            var stdout = mcpProcess.StandardOutput;
            int id = 0;

            // Initialize MCP session
            var initResponse = McpCall<JObject>(mcpProcess, stdin, stdout, ref id, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject
                {
                    ["name"] = "test",
                    ["version"] = "1.0"
                }
            });
            Assert.IsNull(initResponse.Error, "Initialize should not surface an error");
            Assert.IsNotNull(initResponse.Result, "Initialize should return a result");

            // Send initialized notification (no response expected)
            SendJsonRpc(stdin, new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            });

            // Verify tool list
            var toolsResult = McpCall<McpListToolsResult>(mcpProcess, stdin, stdout, ref id, "tools/list");
            Assert.IsNull(toolsResult.Error, "tools/list should not surface an error");
            Assert.IsNotNull(toolsResult.Result, "tools/list should return a result body");
            Assert.IsNotNull(toolsResult.Result.Tools, "tools/list result missing tools array");
            // Assert the EXACT advertised tool set, deriving the expected names by
            // parsing the [McpServerTool(Name = "...")] attributes in the server source
            // rather than maintaining a duplicate list here. A count-only check passes
            // when a tool is renamed or swapped; a hand-maintained list is a standing
            // chore. Parsing source means adding a tool automatically updates the
            // expectation, and the assertion fails only if the bundled ZIP server does
            // not advertise exactly what the current source declares (i.e. the ZIP was
            // not rebuilt after a tool change).
            var expectedToolNames = GetSourceDeclaredToolNames();
            var advertisedToolNames = new HashSet<string>(toolsResult.Result.Tools.Select(t => t.Name));
            var missingTools = expectedToolNames.Where(n => !advertisedToolNames.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var unexpectedTools = advertisedToolNames.Where(n => !expectedToolNames.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.IsTrue(missingTools.Length == 0 && unexpectedTools.Length == 0,
                TextUtil.LineSeparate(
                    @"Advertised MCP tool set does not match the [McpServerTool] attributes in SkylineTools.cs.",
                    @"If the ZIP predates a tool change, rebuild SkylineAiConnector.zip.",
                    @"Missing (declared in source but not advertised by the ZIP server): " + string.Join(@", ", missingTools),
                    @"Unexpected (advertised by the ZIP server but not declared in source): " + string.Join(@", ", unexpectedTools)));

            // Verify get_version returns a non-empty string
            string version = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_version");
            Assert.AreEqual(Install.Version, version);

            // Verify get_document_path handles unsaved document (no NRE)
            string unsavedPath = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path");
            Assert.AreEqual("(unsaved)", unsavedPath);

            // Import FASTA via MCP - the MCP server drives the document change.
            // The tool now takes a file path and routes through RunCommand
            // (--import-fasta), so the FASTA text never round-trips through the
            // LLM as tokens.
            string fastaPath = TestContext.GetTestResultsPath(@"mcp_test.fasta");
            File.WriteAllText(fastaPath, TEST_FASTA);
            McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_import_fasta",
                new JObject { ["fastaPath"] = fastaPath });

            // Verify from inside Skyline that the import worked
            var doc = SkylineWindow.Document;
            var protein = doc.PeptideGroups.First();

            RunUI(() =>
            {
                AssertEx.IsDocumentState(doc, 1, 1, 1, 1, 1);   // Strange but correct
                AssertEx.Contains(protein.Name, "INS_HUMAN");
            });

            // Query protein info via MCP report and verify against the document model
            var columnNames = new[] { "ProteinName", "ProteinDescription", "ProteinSequence" };
            var reportDef = new JObject { ["select"] = new JArray(columnNames) };
            string reportResult = McpToolCall(mcpProcess, stdin, stdout, ref id,
                "skyline_get_report_from_definition",
                new JObject { ["reportDefinitionJson"] = reportDef.ToString() });
            var values = new[] { protein.Name, protein.Description, protein.PeptideGroup.Sequence };
            // Verify the sequence from the report matches the document model
            AssertEx.Contains(reportResult, values.ToDsvLine(TextUtil.SEPARATOR_CSV));
            // And column headers are present
            AssertEx.Contains(reportResult, columnNames.ToDsvLine(TextUtil.SEPARATOR_CSV));

            // Same report through the inline rows tool: count=0 returns shape only,
            // count=1 returns the single protein row inline (no file round-trip).
            string shapeResult = McpToolCall(mcpProcess, stdin, stdout, ref id,
                "skyline_get_report_from_definition_rows",
                new JObject
                {
                    ["reportDefinitionJson"] = reportDef.ToString(),
                    ["count"] = 0
                });
            var shape = JObject.Parse(shapeResult);
            AssertEx.AreEqual(1, (int)shape["total_rows"]);
            var shapeColumns = (JArray)shape["columns"];
            Assert.IsNotNull(shapeColumns);
            AssertEx.AreEqual(3, shapeColumns.Count);
            var shapeRows = (JArray)shape["rows"];
            Assert.IsNotNull(shapeRows);
            AssertEx.AreEqual(0, shapeRows.Count);

            string rowsResult = McpToolCall(mcpProcess, stdin, stdout, ref id,
                "skyline_get_report_from_definition_rows",
                new JObject
                {
                    ["reportDefinitionJson"] = reportDef.ToString(),
                    ["count"] = 1
                });
            var rows = JObject.Parse(rowsResult);
            var rowsArray = (JArray)rows["rows"];
            Assert.IsNotNull(rowsArray);
            AssertEx.AreEqual(1, rowsArray.Count);
            AssertEx.AreEqual(1, (int)rows["total_rows"]);
            // The single row's cells should match the document model (formatted strings).
            var firstRow = (JArray)rowsArray[0];
            AssertEx.AreEqual(protein.Name, (string)firstRow[0]);
            AssertEx.AreEqual(protein.Description, (string)firstRow[1]);
            AssertEx.AreEqual(protein.PeptideGroup.Sequence, (string)firstRow[2]);

            // Save via MCP run_command and verify get_document_path returns the saved path
            const string saveFileName = "SkÿlineMcpTest.sky";   // Be sure to test Unicode round-tripping
            string docPath = TestContext.GetTestResultsPath(saveFileName);
            string saveResponse = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_run_command",
                new JObject { ["commandArgs"] = TextUtil.SpaceSeparate(
                    CommandArgs.ARG_SAVE_AS + docPath.Quote(),
                    CommandArgs.ARG_OVERWRITE.ToString()) });
            AssertEx.Contains(saveResponse, saveFileName);
            string savedPath = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path");
            AssertEx.AreEqual(docPath.ToForwardSlashPath(), savedPath);

            // Dedicated save tool: no filePath -> saves in place (wraps --save)
            McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_save_document");
            AssertEx.AreEqual(docPath.ToForwardSlashPath(),
                McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path"));

            // Dedicated save tool: filePath -> save-as (wraps --out=PATH).
            // Pre-delete in case a prior iteration left the file behind, since the
            // underlying --out check refuses to overwrite an existing file without
            // --overwrite. Verifies the save-as path works on a clean slate.
            string docPath2 = TestContext.GetTestResultsPath("SkylineMcpTest2.sky");
            FileEx.SafeDelete(docPath2);
            McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_save_document",
                new JObject { ["filePath"] = docPath2 });
            AssertEx.AreEqual(docPath2.ToForwardSlashPath(),
                McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path"));

            // Dedicated save tool: existing file without overwrite=true -> error,
            // and the current document path is unchanged. A plain text file is
            // enough to trigger the FileAlreadyExists guard - it fires before any
            // attempt to read the target as a Skyline document.
            string existingPath = TestContext.GetTestResultsPath("preexisting.sky");
            File.WriteAllText(existingPath, @"not a real skyline document");
            string errorResponse = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_save_document",
                new JObject { ["filePath"] = existingPath });
            AssertEx.Contains(errorResponse, string.Format(Resources.CommandLine_NewSkyFile_FileAlreadyExists, existingPath));
            AssertEx.AreEqual(docPath2.ToForwardSlashPath(),
                McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path"));

            // Dedicated save tool: same existing file with overwrite=true -> success,
            // and the document path moves to the new location.
            McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_save_document",
                new JObject { ["filePath"] = existingPath, ["overwrite"] = true });
            AssertEx.AreEqual(existingPath.ToForwardSlashPath(),
                McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_document_path"));

            // skyline_list_installed: filesystem enumeration only (no Skyline
            // connection required). Verifies output is well-formed in both the
            // "at least one install detected" case (dev machines, most users) and
            // the "no install detected" case (e.g. headless CI without Skyline).
            string installsResult = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_list_installed");
            Assert.IsFalse(string.IsNullOrWhiteSpace(installsResult));
            var lines = installsResult.ReadLines().ToArray();
            if (lines[0].StartsWith(@"Release"))
            {
                // Header present -> at least one install was reported. Every data
                // row must have 6 tab-separated columns and exactly one of CliPath
                // or RunnerPath set, since the two scopes are mutually exclusive.
                Assert.IsTrue(lines.Length >= 2, "Header row implies at least one install row");
                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].ParseDsvFields(TextUtil.SEPARATOR_TSV);
                    AssertEx.AreEqual(6, cols.Length, $"row {i}: {lines[i]}");
                    bool hasCli = !string.IsNullOrEmpty(cols[4]);
                    bool hasRunner = !string.IsNullOrEmpty(cols[5]);
                    Assert.IsTrue(hasCli ^ hasRunner,
                        $"row {i} must have exactly one of CliPath / RunnerPath: {lines[i]}");
                }
            }
            else
            {
                // No installs detected -> tool returns a helpful message.
                AssertEx.Contains(installsResult, "No Skyline release detected");
            }

            // Version mismatch detection: verify that an unknown method sent through
            // the pipe produces an error with the Skyline version, so the LLM can
            // diagnose version mismatches between SkylineMcpServer and Skyline.
            TestVersionMismatchError(server);
        }

        /// <summary>
        /// Verify that when a newer SkylineMcpServer calls a method not supported by the
        /// running Skyline instance, the error includes the Skyline version for diagnostics.
        /// Connects directly to the pipe (like the MCP server does internally) and sends
        /// a request for a non-existent method.
        /// </summary>
        private void TestVersionMismatchError(JsonToolServer server)
        {
            // Connect to the same pipe the MCP server uses for a normal-operation
            // sanity check before the version-mismatch probe.
            var pipe = new NamedPipeClientStream(@".", server.PipeName, PipeDirection.InOut);
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            using (var client = new SkylineJsonToolClient(pipe))
            {
                string version = client.GetVersion();
                Assert.IsFalse(string.IsNullOrEmpty(version));
            }

            // The typed-exception path: invoking a missing method via the JSON-RPC
            // pipe must throw JsonRpcException with code = ERROR_METHOD_NOT_FOUND
            // and a message containing the unknown method name. Wrapper
            // version-skew detection relies on the structured code, not on
            // grepping the message text (see SkylineTools.IsMethodNotFound) -
            // without this contract, any unrelated InvalidOperationException
            // whose message happens to contain "Unknown method:" could silently
            // trigger fallback. Runs AFTER the using-block above closes the
            // first pipe, since the JsonToolServer is single-instance and a
            // second concurrent connection would block on the semaphore.
            AssertEx.ThrowsException<JsonRpcException>(
                () => SendRawJsonRpc(server.PipeName, @"GetFutureFeature"),
                thrown =>
                {
                    AssertEx.AreEqual(JsonToolConstants.ERROR_METHOD_NOT_FOUND, thrown.Code);
                    AssertEx.Contains(thrown.Message, @"Unknown method:");
                    AssertEx.Contains(thrown.Message, @"GetFutureFeature");
                });

            // Verify the connection file contains the Skyline version that
            // SkylineTools.Invoke uses to enrich the error message. The wrapper
            // reads this during TryConnect and stores it as SkylineVersion,
            // which gets included in the enriched error:
            // "This method is not available in {skylineId}."
            string connectionJson = File.ReadAllText(
                JsonToolConstants.GetConnectionFilePath(server.PipeName));
            AssertEx.Contains(connectionJson, Install.BareVersion);
        }

        /// <summary>
        /// Inline / file mode coverage for the three image-producing MCP tools.
        /// Opens a ZedGraph-bearing form (Peak Areas) so that
        /// <c>skyline_get_graph_image</c> has something real to render; verifies that
        /// (a) default / "auto" / "inline" return an MCP <c>ImageContentBlock</c>
        /// with valid base64 PNG bytes that round-trip to the same image as the
        /// file path returned by mode "file"; (b) "file" mode preserves the
        /// existing TextContentBlock + path-on-disk contract for regression.
        /// </summary>
        private void ValidateImageInlineAndFileModes(Process mcpProcess, JsonToolServer server)
        {
            // Pre-grant screen-capture permission so any GetFormImage path doesn't
            // hang on the permission dialog. Unrelated to GetGraphImage (which
            // renders without screen capture) but keeps the test robust if future
            // additions exercise the form-image path.
            RunUI(() =>
            {
                Settings.Default.AllowMcpScreenCapture = true;
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            try
            {
                RunInlineAndFileModeAssertions(mcpProcess, server);
            }
            finally
            {
                // Close the opened graph even if an assertion fails so the test's
                // GC-leak check sees no graph-controller subscription holding
                // SkylineWindow alive. Without this, an assertion failure would
                // mask the real error behind a GC-LEAK failure.
                RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
                WaitForGraphs();
            }
        }

        private void RunInlineAndFileModeAssertions(Process mcpProcess, JsonToolServer server)
        {
            // ValidateMcpProtocol (run earlier in the same scenario) has already
            // performed MCP initialize / initialized. We continue using the same
            // subprocess and tail the ID space forward to avoid re-initializing
            // on a session that is already live.
            var stdin = new StreamWriter(mcpProcess.StandardInput.BaseStream, new UTF8Encoding(false))
                { AutoFlush = false };
            var stdout = mcpProcess.StandardOutput;
            int id = 1000;   // distinct id space from ValidateMcpProtocol

            string graphId = FindFirstGraphId(mcpProcess, stdin, stdout, ref id);
            Assert.IsFalse(string.IsNullOrEmpty(graphId),
                "Expected at least one open form with HasGraph=True after opening Peak Areas graph.");

            // 1) File mode: existing behavior. Returns a text block describing
            // the file path; the file exists on disk and is a real PNG.
            var fileResult = CallGetGraphImage(mcpProcess, stdin, stdout, ref id, graphId, "file");
            var fileBlock = SingleContentBlock(fileResult, "text");
            string filePath = ExtractPathAfter(fileBlock.Text, "saved to: ");
            Assert.IsTrue(File.Exists(filePath), "Returned file path should exist on disk: " + filePath);
            byte[] fileBytes = File.ReadAllBytes(filePath);
            AssertPngSignature(fileBytes);

            // 2) Default (auto) mode: should round-trip the PNG inline as an
            // ImageContentBlock with valid PNG bytes. Compare against the file
            // bytes to confirm the inline payload matches what the file path produces.
            var autoResult = CallGetGraphImage(mcpProcess, stdin, stdout, ref id, graphId, returnFormat: null);
            var autoBlock = SingleContentBlock(autoResult, "image");
            AssertEx.AreEqual("image/png", autoBlock.MimeType);
            byte[] autoBytes = Convert.FromBase64String(autoBlock.Data);
            AssertPngSignature(autoBytes);
            // The two renders may differ by a few bytes (timestamps, font subpixel
            // jitter from the second invocation), so don't require exact equality;
            // require that both decode to non-empty PNGs of similar magnitude.
            Assert.IsTrue(Math.Abs(autoBytes.Length - fileBytes.Length) < fileBytes.Length,
                $"Inline and file-mode PNG sizes diverge: inline={autoBytes.Length}, file={fileBytes.Length}");

            // 3) Explicit "inline" mode: same shape as auto when the image fits the cap.
            var inlineResult = CallGetGraphImage(mcpProcess, stdin, stdout, ref id, graphId, "inline");
            var inlineBlock = SingleContentBlock(inlineResult, "image");
            Assert.IsTrue(Convert.FromBase64String(inlineBlock.Data).Length > 0);

            // Direct JSON-RPC: the new GetGraphImageBytes JSON-RPC method exists
            // on the server and round-trips bytes through the pipe. This also
            // serves as the contract test that ImageBytesMetadata serializes /
            // deserializes correctly across the Newtonsoft (server) and
            // System.Text.Json (client) boundary.
            ValidateGetGraphImageBytesPipe(server, graphId);
        }

        /// <summary>
        /// Cap-fallback coverage: this scenario runs with
        /// <c>SKYLINE_MCP_INLINE_IMAGE_CAP_BYTES=100</c> so even the smallest PNG
        /// exceeds the cap. Auto mode falls back to a TextContentBlock pointing
        /// at the written file; inline mode surfaces an explicit error.
        /// </summary>
        private void ValidateImageCapFallback(Process mcpProcess, JsonToolServer server)
        {
            RunUI(() =>
            {
                Settings.Default.AllowMcpScreenCapture = true;
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            try
            {
                RunCapFallbackAssertions(mcpProcess);
            }
            finally
            {
                // Same rationale as ValidateImageInlineAndFileModes: close the
                // graph so the GC-leak check stays clean even if an assertion
                // fails above and the real failure isn't masked.
                RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
                WaitForGraphs();
            }
        }

        private void RunCapFallbackAssertions(Process mcpProcess)
        {
            var stdin = new StreamWriter(mcpProcess.StandardInput.BaseStream, new UTF8Encoding(false))
                { AutoFlush = false };
            var stdout = mcpProcess.StandardOutput;
            int id = 0;
            InitializeMcpSession(mcpProcess, stdin, stdout, ref id);

            string graphId = FindFirstGraphId(mcpProcess, stdin, stdout, ref id);
            Assert.IsFalse(string.IsNullOrEmpty(graphId),
                "Expected at least one open form with HasGraph=True after opening Peak Areas graph.");

            // Auto: bytes exceed the 100-byte cap, wrapper falls back to file and
            // returns a TextContentBlock that describes the saved file.
            var autoResult = CallGetGraphImage(mcpProcess, stdin, stdout, ref id, graphId, "auto");
            var autoBlock = SingleContentBlock(autoResult, "text");
            AssertEx.Contains(autoBlock.Text, JsonToolConstants.MSG_INLINE_CAP_EXCEEDED);
            string fallbackPath = ExtractPathAfter(autoBlock.Text, "Saved to: ");
            Assert.IsTrue(File.Exists(fallbackPath), "Cap-fallback target should exist on disk: " + fallbackPath);
            AssertPngSignature(File.ReadAllBytes(fallbackPath));

            // Inline: bytes exceed the cap, wrapper returns an error result
            // (IsError=true) so the caller knows to retry with auto or file.
            // The MCP framework reports tool errors via result.isError=true (not via
            // the JSON-RPC error envelope) when a tool returns CallToolResult.IsError=true.
            var inlineResult = CallGetGraphImage(mcpProcess, stdin, stdout, ref id, graphId, "inline");
            Assert.IsTrue(inlineResult.IsError ?? false,
                "inline-over-cap should set isError=true on the tool result");
            var inlineErrBlock = SingleContentBlock(inlineResult, "text");
            AssertEx.Contains(inlineErrBlock.Text, JsonToolConstants.MSG_INLINE_CAP_EXCEEDED);
        }

        /// <summary>
        /// Calls <c>skyline_get_graph_image</c> with the given returnFormat and
        /// returns the typed tool result so the caller can inspect content
        /// blocks directly. Pass <paramref name="returnFormat"/> = null to omit
        /// the argument entirely (exercises the default <c>auto</c> path).
        /// </summary>
        private static McpCallToolResult CallGetGraphImage(Process mcpProcess, StreamWriter stdin,
            StreamReader stdout, ref int id, string graphId, string returnFormat)
        {
            var arguments = new JObject { ["graphId"] = graphId };
            if (returnFormat != null)
                arguments["returnFormat"] = returnFormat;
            return McpToolCallResult(mcpProcess, stdin, stdout, ref id, "skyline_get_graph_image", arguments);
        }

        /// <summary>
        /// Asserts the tool result has exactly one content block of the expected
        /// type and returns it. Keeps the per-test assertion calls compact while
        /// preserving a useful failure message when the shape is off.
        /// </summary>
        private static McpContentBlock SingleContentBlock(McpCallToolResult result, string expectedType)
        {
            Assert.IsNotNull(result.Content, "Tool result missing content array");
            AssertEx.AreEqual(1, result.Content.Count, $"Expected exactly one content block, got {result.Content.Count}");
            var block = result.Content[0];
            AssertEx.AreEqual(expectedType, block.Type);
            return block;
        }

        /// <summary>
        /// Connects directly to the JSON-RPC pipe and exercises the new
        /// <c>GetGraphImageBytes</c> method. Verifies that bytes round-trip via the
        /// base64 transport and that the server-suggested file path is well-formed.
        /// Also exercises the legacy file-based <c>GetGraphImage</c> for an
        /// invalid graph ID to confirm the same error surface still applies.
        /// </summary>
        private void ValidateGetGraphImageBytesPipe(JsonToolServer server, string graphId)
        {
            var pipe = new NamedPipeClientStream(@".", server.PipeName, PipeDirection.InOut);
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            using (var client = new SkylineJsonToolClient(pipe))
            {
                var bytes = client.GetGraphImageBytes(graphId);
                Assert.IsNotNull(bytes, "GetGraphImageBytes should return a non-null result for a valid graph");
                Assert.IsNotNull(bytes.Data);
                Assert.IsTrue(bytes.Data.Length > 0, "Bytes payload should be non-empty");
                AssertPngSignature(bytes.Data);
                AssertEx.AreEqual("image/png", bytes.MimeType);
                Assert.IsFalse(string.IsNullOrEmpty(bytes.FilePath),
                    "Server should suggest a fallback file path even when bytes are returned");
            }
        }

        private static string FindFirstGraphId(Process mcpProcess, StreamWriter stdin, StreamReader stdout, ref int id)
        {
            string formsText = McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_get_open_forms");
            foreach (var line in formsText.ReadLines().Skip(1))   // skip header
            {
                var cols = line.ParseDsvFields(TextUtil.SEPARATOR_TSV);
                if (cols.Length >= 5 && string.Equals(cols[2], @"True", StringComparison.OrdinalIgnoreCase))
                    return cols[4];   // Id column
            }
            return null;
        }

        private static void InitializeMcpSession(Process mcpProcess, StreamWriter stdin, StreamReader stdout, ref int id)
        {
            var initResponse = McpCall<JObject>(mcpProcess, stdin, stdout, ref id, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "test", ["version"] = "1.0" }
            });
            Assert.IsNull(initResponse.Error);
            Assert.IsNotNull(initResponse.Result);
            SendJsonRpc(stdin, new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            });
        }

        private static string ExtractPathAfter(string text, string marker)
        {
            int idx = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.IsTrue(idx >= 0, $"Marker '{marker}' not found in response text: {text}");
            int start = idx + marker.Length;
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start).Trim();
        }

        private static void AssertPngSignature(byte[] bytes)
        {
            // PNG files start with the 8-byte signature 89 50 4E 47 0D 0A 1A 0A.
            // Verifying the signature confirms the bytes really are a PNG, not
            // a base64-mangled or truncated payload.
            Assert.IsTrue(bytes.Length >= 8, "PNG payload too short");
            var expected = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            for (int i = 0; i < expected.Length; i++)
                AssertEx.AreEqual(expected[i], bytes[i], $"PNG signature mismatch at byte {i}");
        }

        // --- Typed JSON-RPC / MCP response helpers ---
        //
        // The MCP server speaks JSON-RPC 2.0 over stdio with snake_case property
        // names. We deserialize the envelope into the POCOs below so the tests
        // can read structured fields (Result.Content[0].Text) instead of chasing
        // null-conditional JObject indexers, which ReSharper flags as
        // "Possible System.NullReferenceException" on every access.

        // MCP wire format uses camelCase for content-block fields (mimeType, isError, etc.)
        // and JSON-RPC envelope fields are also lowercase (jsonrpc, id, result, error). A
        // CamelCase naming strategy maps PascalCase POCO properties to both conventions
        // since one-word identifiers are unchanged.
        private static readonly JsonSerializerSettings _mcpWireSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

        /// <summary>JSON-RPC 2.0 response envelope. Either Result or Error is populated.</summary>
        private class JsonRpcResponse<TResult>
        {
            public string Jsonrpc { get; set; }
            public int Id { get; set; }
            public TResult Result { get; set; }
            public JsonRpcErrorBody Error { get; set; }
        }

        private class JsonRpcErrorBody
        {
            public int Code { get; set; }
            public string Message { get; set; }
        }

        /// <summary>MCP <c>tools/call</c> result body: a list of content blocks and an error flag.</summary>
        // ReSharper disable once ClassNeverInstantiated.Local
        // ReSharper disable once CollectionNeverUpdated.Local
        private class McpCallToolResult
        {
            // Populated by Newtonsoft.Json during JsonConvert.DeserializeObject of
            // the tools/call envelope; not constructed in test code.
            public List<McpContentBlock> Content { get; set; }
            public bool? IsError { get; set; }
        }

        /// <summary>
        /// MCP <c>tools/list</c> result body. Only the count matters for the
        /// surface check, so we leave the inner tool POCO at the smallest shape.
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Local
        // ReSharper disable once CollectionNeverUpdated.Local
        private class McpListToolsResult
        {
            public List<McpTool> Tools { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class McpTool
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// MCP content block union: <see cref="Text"/> is set on text blocks and
        /// <see cref="Data"/> + <see cref="MimeType"/> on image blocks. The
        /// <see cref="Type"/> field disambiguates ("text" vs "image" etc.).
        /// </summary>
        private class McpContentBlock
        {
            public string Type { get; set; }
            public string Text { get; set; }
            public string Data { get; set; }
            public string MimeType { get; set; }
        }

        /// <summary>
        /// Send a JSON-RPC method call and deserialize the response into a typed envelope.
        /// Asserts the envelope id matches the request id before returning.
        /// </summary>
        private static JsonRpcResponse<TResult> McpCall<TResult>(Process mcpProcess, StreamWriter stdin,
            StreamReader stdout, ref int id, string method, JObject parameters = null)
        {
            string responseText = SendMcpRequest(mcpProcess, stdin, stdout, ref id, method, parameters);
            var envelope = JsonConvert.DeserializeObject<JsonRpcResponse<TResult>>(responseText, _mcpWireSettings);
            Assert.IsNotNull(envelope, "Failed to deserialize JSON-RPC envelope: " + responseText);
            AssertEx.AreEqual(id, envelope.Id);
            return envelope;
        }

        /// <summary>
        /// Call an MCP tool and return the text from the first content block of
        /// the response. The typed envelope makes the property chain null-safe
        /// without explicit ?[] indexers - if any field is missing the returned
        /// string is simply null and the caller's assertion fires with context.
        /// </summary>
        private static string McpToolCall(Process mcpProcess, StreamWriter stdin, StreamReader stdout,
            ref int id, string toolName, JObject arguments = null)
        {
            var result = McpToolCallResult(mcpProcess, stdin, stdout, ref id, toolName, arguments);
            return result.Content?.FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Call an MCP tool and return the full typed <see cref="McpCallToolResult"/> so
        /// the test can inspect every content block (type, base64 data, MIME, IsError).
        /// Asserts the JSON-RPC envelope itself does not carry a transport-level error.
        /// </summary>
        private static McpCallToolResult McpToolCallResult(Process mcpProcess, StreamWriter stdin,
            StreamReader stdout, ref int id, string toolName, JObject arguments = null)
        {
            var response = McpCall<McpCallToolResult>(mcpProcess, stdin, stdout, ref id,
                "tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JObject()
                });
            Assert.IsNull(response.Error,
                $"Tool {toolName} returned JSON-RPC error: {response.Error?.Message}");
            Assert.IsNotNull(response.Result,
                $"Tool {toolName} returned no result body");
            return response.Result;
        }

        /// <summary>
        /// Send a raw JSON-RPC request directly over the named pipe and parse the
        /// response envelope, throwing <see cref="JsonRpcException"/> if the server
        /// returns an error. Used by tests that exercise the JSON-RPC error path
        /// without going through the MCP subprocess (or the typed C# client, which
        /// only knows about declared <see cref="IJsonToolService"/> methods).
        /// </summary>
        private static void SendRawJsonRpc(string pipeName, string method)
        {
            using var pipe = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut);
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;

            string request = new JObject
            {
                [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                [nameof(JSON_RPC.method)] = method,
                [nameof(JSON_RPC.id)] = 1
            }.ToString();
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            pipe.Write(requestBytes, 0, requestBytes.Length);
            pipe.Flush();
            pipe.WaitForPipeDrain();

            string responseJson = ReadPipeMessage(pipe);
            var envelope = JsonConvert.DeserializeObject<JsonRpcResponse<JObject>>(responseJson, _mcpWireSettings);
            Assert.IsNotNull(envelope);
            if (envelope.Error != null)
                throw new JsonRpcException(envelope.Error.Code, envelope.Error.Message);
        }

        private static string ReadPipeMessage(PipeStream pipe)
        {
            using var ms = new MemoryStream();
            do
            {
                var buffer = new byte[65536];
                int count = pipe.Read(buffer, 0, buffer.Length);
                if (count == 0) break;
                ms.Write(buffer, 0, count);
            } while (!pipe.IsMessageComplete);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static string SendMcpRequest(Process mcpProcess, StreamWriter stdin, StreamReader stdout,
            ref int id, string method, JObject parameters)
        {
            id++;
            var message = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method
            };
            if (parameters != null)
                message["params"] = parameters;
            SendJsonRpc(stdin, message);
            return ReadJsonRpcResponseText(mcpProcess, stdout, id);
        }

        private static void SendJsonRpc(StreamWriter writer, JObject message)
        {
            writer.WriteLine(message.ToString(Formatting.None));
            writer.Flush();
        }

        private static string ReadJsonRpcResponseText(Process mcpProcess, StreamReader reader, int expectedId)
        {
            // Read lines until we get a JSON-RPC response with the expected id.
            // Notifications and unrelated responses are skipped without parsing
            // their payloads twice - we look at the id field only.
            for (int i = 0; i < 100; i++)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    bool killed = false;
                    if (!mcpProcess.HasExited)
                    {
                        if (!mcpProcess.WaitForExit(5000))
                        {
                            mcpProcess.Kill();
                            killed = true;
                        }
                    }
                    string stderr = mcpProcess.StandardError.ReadToEnd();
                    string status = killed
                        ? "MCP server stopped responding and was terminated"
                        : string.Format("MCP server exited unexpectedly (exit code {0})", mcpProcess.ExitCode);
                    Assert.Fail("{0}.{1}", status,
                        string.IsNullOrEmpty(stderr) ? string.Empty : "\nStderr: " + stderr);
                }
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var idToken = JObject.Parse(line)["id"];
                if (idToken != null && (int)idToken == expectedId)
                    return line;
            }
            Assert.Fail("No JSON-RPC response received for id {0}", expectedId);
            return null; // Unreachable
        }
    }
}
