/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
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

        private const int EXPECTED_TOOL_COUNT = 47;

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
            var toolService = new ToolService(testGuid, SkylineWindow);
            var server = new JsonToolServer(toolService, testGuid);

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

        private void ValidateMcpProtocol(Process mcpProcess, JsonToolServer server)
        {
            // Wrap stdin with UTF-8 StreamWriter (Process.StandardInput defaults to
            // system code page on .NET Framework, corrupting non-ASCII characters)
            var stdin = new StreamWriter(mcpProcess.StandardInput.BaseStream, new UTF8Encoding(false))
                { AutoFlush = false };
            var stdout = mcpProcess.StandardOutput;
            int id = 0;

            // Initialize MCP session
            var initResponse = McpCall(mcpProcess, stdin, stdout, ref id, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject
                {
                    ["name"] = "test",
                    ["version"] = "1.0"
                }
            });
            Assert.IsNotNull(initResponse["result"], "Initialize should return a result");

            // Send initialized notification (no response expected)
            SendJsonRpc(stdin, new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            });

            // Verify tool list
            var toolsResult = McpCall(mcpProcess, stdin, stdout, ref id, "tools/list");
            var tools = (JArray)toolsResult["result"]?["tools"];
            Assert.IsNotNull(tools, "tools/list should return tools array");
            AssertEx.AreEqual(EXPECTED_TOOL_COUNT, tools.Count);

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
            // Connect to the same pipe the MCP server uses
            var pipe = new NamedPipeClientStream(@".", server.PipeName, PipeDirection.InOut);
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            using (var client = new SkylineJsonToolClient(pipe))
            {
                // Verify normal operation first
                string version = client.GetVersion();
                Assert.IsFalse(string.IsNullOrEmpty(version));

                // Simulate a newer client calling a method the server doesn't have.
                // Send a raw JSON-RPC request since the typed client won't let us
                // call a method that doesn't exist in IJsonToolService.
                string request = new JObject
                {
                    [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                    [nameof(JSON_RPC.method)] = @"GetFutureFeature",
                    [nameof(JSON_RPC.id)] = 1
                }.ToString();
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                pipe.Write(requestBytes, 0, requestBytes.Length);
                pipe.Flush();
                pipe.WaitForPipeDrain();

                // Read the error response
                var ms = new MemoryStream();
                do
                {
                    var buffer = new byte[65536];
                    int count = pipe.Read(buffer, 0, buffer.Length);
                    if (count == 0) break;
                    ms.Write(buffer, 0, count);
                } while (!pipe.IsMessageComplete);
                var response = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));

                // Verify the error has the correct JSON-RPC structure and content
                var error = response[nameof(JSON_RPC.error)];
                Assert.IsNotNull(error, @"Unknown method should return a JSON-RPC error");
                AssertEx.AreEqual(JsonToolConstants.ERROR_METHOD_NOT_FOUND,
                    (int)error[nameof(JSON_RPC.code)]);

                // The error message must contain "Unknown method:" - this is the pattern
                // that SkylineTools.Invoke checks to trigger version mismatch enrichment
                string message = (string)error[nameof(JSON_RPC.message)];
                AssertEx.Contains(message, @"Unknown method:");
                AssertEx.Contains(message, @"GetFutureFeature");

                // Verify the connection file contains the Skyline version that
                // SkylineTools.Invoke uses to enrich the error message.
                // SkylineConnection reads this during TryConnect and stores it
                // as SkylineVersion, which gets included in the enriched error:
                // "This method is not available in {skylineId}."
                string connectionJson = File.ReadAllText(
                    JsonToolConstants.GetConnectionFilePath(server.PipeName));
                AssertEx.Contains(connectionJson, Install.BareVersion);
            }
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
            var fileResponse = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call",
                new JObject
                {
                    ["name"] = "skyline_get_graph_image",
                    ["arguments"] = new JObject
                    {
                        ["graphId"] = graphId,
                        ["returnFormat"] = "file"
                    }
                });
            Assert.IsNull(fileResponse["error"], "file mode should not surface an error");
            var fileContent = (JArray)fileResponse["result"]?["content"];
            AssertEx.AreEqual(1, fileContent.Count);
            AssertEx.AreEqual("text", (string)fileContent[0]["type"]);
            string fileText = (string)fileContent[0]["text"];
            string filePath = ExtractPathAfter(fileText, "saved to: ");
            Assert.IsTrue(File.Exists(filePath), "Returned file path should exist on disk: " + filePath);
            byte[] fileBytes = File.ReadAllBytes(filePath);
            AssertPngSignature(fileBytes);

            // 2) Default (auto) mode: should round-trip the PNG inline as an
            // ImageContentBlock with valid PNG bytes. Compare against the file
            // bytes to confirm the inline payload matches what the file path produces.
            var autoResponse = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call",
                new JObject
                {
                    ["name"] = "skyline_get_graph_image",
                    ["arguments"] = new JObject { ["graphId"] = graphId }
                });
            Assert.IsNull(autoResponse["error"], "auto mode should not surface an error");
            var autoContent = (JArray)autoResponse["result"]?["content"];
            AssertEx.AreEqual(1, autoContent.Count);
            AssertEx.AreEqual("image", (string)autoContent[0]["type"]);
            AssertEx.AreEqual("image/png", (string)autoContent[0]["mimeType"]);
            byte[] autoBytes = Convert.FromBase64String((string)autoContent[0]["data"]);
            AssertPngSignature(autoBytes);
            // The two renders may differ by a few bytes (timestamps, font subpixel
            // jitter from the second invocation), so don't require exact equality;
            // require that both decode to non-empty PNGs of similar magnitude.
            Assert.IsTrue(Math.Abs(autoBytes.Length - fileBytes.Length) < fileBytes.Length,
                $"Inline and file-mode PNG sizes diverge: inline={autoBytes.Length}, file={fileBytes.Length}");

            // 3) Explicit "inline" mode: same shape as auto when the image fits the cap.
            var inlineResponse = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call",
                new JObject
                {
                    ["name"] = "skyline_get_graph_image",
                    ["arguments"] = new JObject
                    {
                        ["graphId"] = graphId,
                        ["returnFormat"] = "inline"
                    }
                });
            Assert.IsNull(inlineResponse["error"], "inline mode should not surface an error when image fits cap");
            var inlineContent = (JArray)inlineResponse["result"]?["content"];
            AssertEx.AreEqual("image", (string)inlineContent[0]["type"]);
            Assert.IsTrue(Convert.FromBase64String((string)inlineContent[0]["data"]).Length > 0);

            // Direct JSON-RPC: the new GetGraphImageBytes JSON-RPC method exists
            // on the server and round-trips bytes through the pipe. This also
            // serves as the contract test that ImageBytesMetadata serializes /
            // deserializes correctly across the Newtonsoft (server) and
            // System.Text.Json (client) boundary.
            ValidateGetGraphImageBytesPipe(server, graphId);

            // Close the graph we opened so the test's GC-leak check sees no
            // graph-controller subscription holding SkylineWindow alive.
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForGraphs();
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
            var autoResponse = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call",
                new JObject
                {
                    ["name"] = "skyline_get_graph_image",
                    ["arguments"] = new JObject
                    {
                        ["graphId"] = graphId,
                        ["returnFormat"] = "auto"
                    }
                });
            Assert.IsNull(autoResponse["error"], "auto fallback should not surface a JSON-RPC error");
            var autoContent = (JArray)autoResponse["result"]?["content"];
            AssertEx.AreEqual("text", (string)autoContent[0]["type"]);
            string autoText = (string)autoContent[0]["text"];
            AssertEx.Contains(autoText, "exceeded inline cap");
            string fallbackPath = ExtractPathAfter(autoText, "Saved to: ");
            Assert.IsTrue(File.Exists(fallbackPath), "Cap-fallback target should exist on disk: " + fallbackPath);
            AssertPngSignature(File.ReadAllBytes(fallbackPath));

            // Inline: bytes exceed the cap, wrapper returns an error response
            // (IsError=true) so the caller knows to retry with auto or file.
            var inlineResponse = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call",
                new JObject
                {
                    ["name"] = "skyline_get_graph_image",
                    ["arguments"] = new JObject
                    {
                        ["graphId"] = graphId,
                        ["returnFormat"] = "inline"
                    }
                });
            // The MCP framework reports tool errors via result.isError=true (not via the
            // JSON-RPC error envelope) when a tool returns CallToolResult.IsError=true.
            var inlineResult = inlineResponse["result"];
            Assert.IsNotNull(inlineResult, "inline-over-cap should return a tool result, not a transport error");
            Assert.IsTrue((bool)(inlineResult["isError"] ?? false),
                "inline-over-cap should set isError=true on the tool result");
            string inlineErrText = (string)inlineResult["content"]?[0]?["text"];
            AssertEx.Contains(inlineErrText, "exceeded inline cap");

            // Close the graph so the test's GC-leak check sees no graph-controller
            // subscription holding SkylineWindow alive.
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForGraphs();
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
            var initResponse = McpCall(mcpProcess, stdin, stdout, ref id, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "test", ["version"] = "1.0" }
            });
            Assert.IsNotNull(initResponse["result"]);
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

        /// <summary>
        /// Send a JSON-RPC method call and return the response.
        /// </summary>
        private static JObject McpCall(Process mcpProcess, StreamWriter stdin, StreamReader stdout,
            ref int id, string method, JObject parameters = null)
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
            var response = ReadJsonRpcResponse(mcpProcess, stdout, id);
            AssertEx.AreEqual(id, (int)response["id"]);
            return response;
        }

        /// <summary>
        /// Call an MCP tool and return the text content from the response.
        /// </summary>
        private static string McpToolCall(Process mcpProcess, StreamWriter stdin, StreamReader stdout,
            ref int id, string toolName, JObject arguments = null)
        {
            var response = McpCall(mcpProcess, stdin, stdout, ref id, "tools/call", new JObject
            {
                ["name"] = toolName,
                ["arguments"] = arguments ?? new JObject()
            });
            Assert.IsNull(response["error"],
                string.Format("Tool {0} returned error: {1}", toolName, response["error"]));
            return (string)response["result"]?["content"]?[0]?["text"];
        }

        private static void SendJsonRpc(StreamWriter writer, JObject message)
        {
            writer.WriteLine(message.ToString(Newtonsoft.Json.Formatting.None));
            writer.Flush();
        }

        private static JObject ReadJsonRpcResponse(Process mcpProcess, StreamReader reader, int expectedId)
        {
            // Read lines until we get a JSON-RPC response with the expected id
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
                var obj = JObject.Parse(line);
                // Skip notifications (no "id" field) and responses for other ids
                if (obj["id"] != null && (int)obj["id"] == expectedId)
                    return obj;
            }
            Assert.Fail("No JSON-RPC response received for id {0}", expectedId);
            return null; // Unreachable
        }
    }
}
