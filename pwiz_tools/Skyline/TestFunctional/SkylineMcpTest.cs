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

        private const int EXPECTED_TOOL_COUNT = 43;

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

            // Create JsonToolServer with known pipe name (like JsonToolServerTest)
            string testGuid = @"test-" + Guid.NewGuid();
            var toolService = new ToolService(testGuid, SkylineWindow);
            var server = new JsonToolServer(toolService, testGuid);

            string connectionFilePath = null;
            Process mcpProcess = null;
            try
            {
                // Write connection file so MCP server can find us
                server.WriteConnectionInfo();
                connectionFilePath = JsonToolConstants.GetConnectionFilePath(server.PipeName);

                // Start the server thread
                server.Start();

                // Launch SkylineMcpServer.exe
                mcpProcess = StartMcpServer(mcpServerPath);

                // Run MCP protocol tests
                ValidateMcpProtocol(mcpProcess, server);
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

        private static Process StartMcpServer(string mcpServerPath)
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

            // Import FASTA via MCP - the MCP server drives the document change
            McpToolCall(mcpProcess, stdin, stdout, ref id, "skyline_import_fasta",
                new JObject { ["textFasta"] = TEST_FASTA });

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
