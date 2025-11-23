/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Koina.Config;
using pwiz.Skyline.Util.Extensions;
using Formatting = Newtonsoft.Json.Formatting;


namespace pwiz.SkylineTestUtil
{
   
    /// <summary>
    /// An intermediate base class containing simplified functions for unit tests.
    /// </summary>
    public class AbstractUnitTestEx : AbstractUnitTest
    {
        protected static string RunCommand(params string[] inputArgs)
        {
            return RunCommand(null, inputArgs);
        }

        protected static string RunCommand(bool? expectSuccess, params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleWriter = new CommandStatusWriter(new StringWriter(consoleBuffer));

            var exitStatus = CommandLineRunner.RunCommand(inputArgs, consoleWriter, true);

            var consoleOutput = consoleBuffer.ToString();
            bool errorReported = consoleWriter.IsErrorReported;

            ValidateRunExitStatus(expectSuccess, exitStatus, errorReported, consoleOutput);

            return consoleOutput;
        }

        protected static void CheckRunCommandOutputContains(string expectedMessage, string actualMessage)
        {
            Assert.IsTrue(actualMessage.Contains(expectedMessage),
                string.Format("Expected RunCommand result message containing \n\"{0}\",\ngot\n\"{1}\"\ninstead.", expectedMessage, actualMessage));
        }

        private static void ValidateRunExitStatus(bool? expectSuccess, int exitStatus, bool errorReported, string consoleOutput)
        {
            string message = null;
            // Make sure exist status and text error reporting match
            if (exitStatus == Program.EXIT_CODE_SUCCESS && errorReported ||
                exitStatus != Program.EXIT_CODE_SUCCESS && !errorReported)
            {
                message = string.Format("{0} reported but exit status was {1}.",
                    errorReported ? "Error" : "No error", exitStatus);
            }
            else if (expectSuccess.HasValue)
            {
                // Make sure expected exit status matches actual
                if (expectSuccess.Value && exitStatus != Program.EXIT_CODE_SUCCESS)
                    message = string.Format("Expecting successful command-line execution but got {0} exit code.", exitStatus);
                else if (!expectSuccess.Value && exitStatus == Program.EXIT_CODE_SUCCESS)
                    message = "Expecting command-line error but execution was successful.";
            }

            if (message != null)
            {
                Assert.Fail(TextUtil.LineSeparate(message, "Output: ", consoleOutput));
            }
        }


        public static void EnableAuditLogging(string path)
        {
            var doc = ResultsUtil.DeserializeDocument(path);
            Assert.IsFalse(doc.Settings.DataSettings.IsAuditLoggingEnabled);
            doc = AuditLogList.ToggleAuditLogging(doc, true);
            doc.SerializeToFile(path, path, SkylineVersion.CURRENT, new SilentProgressMonitor());
        }

        public static SrmDocument DeserializeWithAuditLog(string path)
        {
            using var hashingStream = (HashingStream)HashingStream.CreateReadStream(path);
            using var reader = XmlReader.Create(hashingStream, new XmlReaderSettings() { IgnoreWhitespace = true }, path);
            XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
            var document = (SrmDocument)ser.Deserialize(reader);
            var skylineDocumentHash = hashingStream.Done();
            return document.ReadAuditLog(path, skylineDocumentHash, () => throw new AssertFailedException("Document hash did not match"));
        }

        // Sometimes a text representation is useful for running down differences in behavior between branches
        public static void SerializeDocumentToFile(SrmDocument doc, string path)
        {
            var serializer = new XmlSerializer(typeof(SrmDocument));
            using var writer = new StreamWriter(path);
            serializer.Serialize(writer, doc);
        }

        public static void AssertLastEntry(AuditLogList auditLogList, MessageType messageType)
        {
            var lastEntry = auditLogList.AuditLogEntries;
            Assert.IsFalse(lastEntry.IsRoot);
            Assert.AreNotEqual(0, lastEntry.AllInfo.Count);
            Assert.AreEqual(messageType, lastEntry.AllInfo[0].MessageInfo.Type);
        }

        protected virtual bool IsRecordMode
        {
            get { return false; }
        }

        protected void CheckRecordMode()
        {
            Assert.IsFalse(IsRecordMode, "Set IsRecordMode to false before commit");   // Avoid merging code with record mode left on.
        }

        /// <summary>
        /// Returns true if Skyline was compiled with a Koina config file that enables connecting to a real server.
        /// </summary>
        public static bool HasKoinaServer()
        {
            return !string.IsNullOrEmpty(KoinaConfig.GetKoinaConfig().Server);
        }

        /// <summary>
        /// Get a system resource string. Useful when checking that a test throws an expected error message but the text comes from the OS or .NET.
        /// Use an ID from https://github.com/ng256/Tools/blob/main/MscorlibMessage.md
        /// </summary>
        /// <example>GetSystemResourceString("IO.FileNotFound_FileName", "SomeFilepath")</example>
        public string GetSystemResourceString(string resourceId, params object[] args)
        {
            if (!_systemResources.TryGetValue(CultureInfo.CurrentUICulture, out var resourceSet))
            {
                var assembly = Assembly.GetAssembly(typeof(object));
                var assemblyName = assembly.GetName().Name;
                var manager = new ResourceManager(assemblyName, assembly);

                if (!_systemResources.ContainsKey(CultureInfo.CurrentUICulture))
                {
                    resourceSet = _systemResources[CultureInfo.CurrentUICulture] = manager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
                    _systemResourceString[resourceSet] = new Dictionary<string, string>();
                }

                resourceSet = _systemResources[CultureInfo.CurrentUICulture];
            }

            if (!_systemResourceString[resourceSet!].TryGetValue(resourceId, out var formatString))
                formatString = _systemResourceString[resourceSet][resourceId] = resourceSet.GetString(resourceId) ??
                    throw new ArgumentException(nameof(resourceId));
            return string.Format(formatString, args);
        }

        private static Dictionary<CultureInfo, ResourceSet> _systemResources = new Dictionary<CultureInfo, ResourceSet>();
        private static Dictionary<ResourceSet, Dictionary<string, string>> _systemResourceString =
            new Dictionary<ResourceSet, Dictionary<string, string>>();


        #region HTTP Recording/Playback Support

        /// <summary>
        /// Controls whether the test uses actual web access (true) or recorded playback (false).
        /// Set this in your test method before calling RunFunctionalTest().
        /// </summary>
        protected bool DoActualWebAccess { get; set; }

        /// <summary>
        /// Returns an IDisposable scope that handles HTTP recording/playback setup and teardown.
        /// Use this in a using statement to automatically handle recording or playback based on
        /// DoActualWebAccess and IsRecordMode settings.
        /// </summary>
        /// <returns>An HttpRecordingScope that manages HttpClientTestHelper lifecycle</returns>
        protected HttpRecordingScope GetHttpRecordingScope()
        {
            return new HttpRecordingScope(this);
        }

        /// <summary>
        /// Gets the standardized file path for HTTP interaction recordings.
        /// Uses the pattern: Type.Name + "WebData.json" in the same directory as the test class.
        /// The directory is determined from the namespace (e.g., pwiz.SkylineTestConnected -> TestConnected).
        /// </summary>
        private string GetHttpRecordingFilePath()
        {
            return GetHttpRecordingFilePathFromType(GetType(), TestContext.GetProjectDirectory);
        }

        /// <summary>
        /// Loads recorded HTTP interactions from the standardized file path.
        /// Returns empty list if the file doesn't exist.
        /// </summary>
        protected List<HttpInteraction> LoadHttpInteractions()
        {
            return LoadHttpInteractionsForType(GetType(), TestContext.GetProjectDirectory);
        }

        /// <summary>
        /// Static helper to load HTTP interactions for a specific test type.
        /// Used by static methods like BeginPlaybackForFunctionalTests().
        /// Resolves response body references by index after deserialization.
        /// </summary>
        protected static List<HttpInteraction> LoadHttpInteractionsForType(Type testType, Func<string, string> getProjectDirectory)
        {
            Assert.IsNotNull(testType, "Test type cannot be null when loading HTTP interactions");
            Assert.IsNotNull(getProjectDirectory, "GetProjectDirectory function cannot be null when loading HTTP interactions");

            var jsonPath = GetHttpRecordingFilePathFromType(testType, getProjectDirectory);

            // CONSIDER: This is currently required for recording from the web when the JSON file does not yet exist.
            // It might be nice to be able to fail here when the file is really necessary, since it would allow us to
            // have the error message include the expected file path.
            if (!File.Exists(jsonPath))
                return new List<HttpInteraction>();

            var json = File.ReadAllText(jsonPath, Encoding.UTF8);
            var data = JsonConvert.DeserializeObject<HttpRecordingData>(json);
            var interactions = data?.HttpInteractions ?? new List<HttpInteraction>();
            
            // Resolve response body references after deserialization
            ResolveResponseBodyReferences(interactions);
            
            return interactions;
        }

        /// <summary>
        /// Resolves response body references by copying content from referenced indices.
        /// </summary>
        private static void ResolveResponseBodyReferences(List<HttpInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
                return;

            for (int i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                if (interaction.ResponseBodyIndex.HasValue)
                {
                    int referencedIndex = interaction.ResponseBodyIndex.Value;
                    
                    // Validate index is within bounds
                    if (referencedIndex < 0 || referencedIndex >= interactions.Count)
                    {
                        throw new InvalidOperationException(
                            $"Invalid ResponseBodyIndex {referencedIndex} in interaction {i}. " +
                            $"Index must be between 0 and {interactions.Count - 1}.");
                    }

                    // Validate we're not creating a circular reference
                    if (referencedIndex == i)
                    {
                        throw new InvalidOperationException(
                            $"Circular reference detected: interaction {i} references itself.");
                    }

                    var referencedInteraction = interactions[referencedIndex];
                    
                    // Copy response body content from the referenced interaction
                    interaction.ResponseBody = referencedInteraction.ResponseBody;
                    interaction.ResponseBodyIsBase64 = referencedInteraction.ResponseBodyIsBase64;
                    interaction.ResponseBodyLines = referencedInteraction.ResponseBodyLines?.ToList();
                    
                    // Clear the reference index now that we've resolved it
                    interaction.ResponseBodyIndex = null;
                }
            }
        }

        /// <summary>
        /// Gets the standardized file path for HTTP interaction recordings for a specific test type.
        /// </summary>
        private static string GetHttpRecordingFilePathFromType(Type type, Func<string, string> getProjectDirectory)
        {
            var fileName = type.Name + "WebData.json";
            Assert.IsFalse(string.IsNullOrEmpty(fileName), "Test class name cannot be empty");
            var relativePath = GetRelativePathFromNamespaceForType(type);
            var fullRelativePath = Path.Combine(relativePath, fileName);
            var jsonPath = getProjectDirectory(fullRelativePath);
            Assert.IsNotNull(jsonPath, $"Unable to determine project directory for HTTP recording file: {fullRelativePath}");
            return jsonPath;
        }

        /// <summary>
        /// Static helper to extract the relative directory path from a type's namespace.
        /// Converts namespace like "pwiz.SkylineTestConnected" to "TestConnected"
        /// or "pwiz.SkylineTest.Proteome" to "Test\Proteome".
        /// </summary>
        protected static string GetRelativePathFromNamespaceForType(Type type)
        {
            Assert.IsNotNull(type, "Type cannot be null when determining relative path from namespace");
            var ns = type.Namespace;
            Assert.IsFalse(string.IsNullOrEmpty(ns), $"Type {type.Name} must have a namespace to determine its relative path");

            // If namespace does not start with "pwiz.Skyline", return as-is (e.g. for CommonTest and TestPerf)
            if (ns.StartsWith("pwiz.Skyline"))
            {
                // Remove "pwiz." prefix if present
                // Remove "Skyline" prefix if present (e.g., "SkylineTestConnected" -> "TestConnected")
                Assert.IsTrue(ns.Length > 12 && ns[11] != '.', $"Type {type.FullName} is expected not to be in pwiz.Skyline namespace to determine its path");
                ns = ns.Substring(12);
            }

            // Convert remaining namespace parts to directory path
            // Replace dots with path separators
            var relativePath = ns.Replace('.', Path.DirectorySeparatorChar);
            return relativePath;
        }

        /// <summary>
        /// Records HTTP interactions to the standardized file path.
        /// Deduplicates identical response bodies by storing references to the first instance.
        /// </summary>
        internal void RecordHttpInteractions(IReadOnlyList<HttpInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
                return;

            var jsonPath = GetHttpRecordingFilePath();

            // Deduplicate response bodies by storing references to the first instance
            var interactionsToSerialize = DeduplicateResponseBodies(interactions);

            var data = new HttpRecordingData
            {
                HttpInteractions = interactionsToSerialize
            };

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
            Console.Out.WriteLine(@"Recorded HTTP interactions to: " + jsonPath);
        }

        /// <summary>
        /// Deduplicates response bodies by detecting identical content and storing index references.
        /// </summary>
        private static List<HttpInteraction> DeduplicateResponseBodies(IReadOnlyList<HttpInteraction> interactions)
        {
            var result = new List<HttpInteraction>();
            // Dictionary mapping response body content to the first index where it was seen
            var responseBodyMap = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var cloned = interaction.Clone();

                // Get the response body content as a normalized string for comparison
                string responseBodyKey = GetResponseBodyKey(interaction);
                
                if (!string.IsNullOrEmpty(responseBodyKey))
                {
                    // Check if we've seen this response body before
                    if (responseBodyMap.TryGetValue(responseBodyKey, out int firstIndex))
                    {
                        // Reference the first instance
                        cloned.ResponseBodyIndex = firstIndex;
                        cloned.ResponseBody = null;
                        cloned.ResponseBodyLines = null;
                    }
                    else
                    {
                        // First occurrence - store the index for future references
                        responseBodyMap[responseBodyKey] = i;
                    }
                }

                result.Add(cloned);
            }

            return result;
        }

        /// <summary>
        /// Gets a normalized key for response body comparison (handles both text and base64).
        /// </summary>
        private static string GetResponseBodyKey(HttpInteraction interaction)
        {
            if (interaction == null)
                return string.Empty;

            // Reconstruct the actual response body content for comparison
            string responseBodyContent;
            if (interaction.ResponseBodyLines != null && interaction.ResponseBodyLines.Count > 0)
            {
                // Join lines to reconstruct the full content
                // For base64, join without separator; for text, join with newline
                if (interaction.ResponseBodyIsBase64)
                {
                    responseBodyContent = string.Join("", interaction.ResponseBodyLines);
                }
                else
                {
                    responseBodyContent = string.Join("\n", interaction.ResponseBodyLines);
                }
            }
            else if (!string.IsNullOrEmpty(interaction.ResponseBody))
            {
                responseBodyContent = interaction.ResponseBody;
            }
            else
            {
                return string.Empty;
            }

            // Include the base64 flag in the key to distinguish between text and binary with same content
            return $"{interaction.ResponseBodyIsBase64}:{responseBodyContent}";
        }

        /// <summary>
        /// Container class for HTTP interaction recordings in JSON files.
        /// </summary>
        protected class HttpRecordingData
        {
            public List<HttpInteraction> HttpInteractions { get; set; } = new List<HttpInteraction>();
        }

        /// <summary>
        /// Manages the lifecycle of HTTP recording/playback for unit and functional tests.
        /// Automatically handles setup (recording or playback) and teardown (saving recordings).
        /// </summary>
        protected class HttpRecordingScope : IDisposable
        {
            private readonly AbstractUnitTestEx _test;
            private readonly HttpInteractionRecorder _recorder;
            private readonly HttpClientTestHelper _helper;

            public HttpRecordingScope(AbstractUnitTestEx test)
            {
                _test = test ?? throw new ArgumentNullException(nameof(test));

                var expectedData = _test.LoadHttpInteractions();

                // Create recorder if we're doing actual web access and in record mode
                if (_test.DoActualWebAccess && _test.IsRecordMode)
                {
                    _recorder = new HttpInteractionRecorder();
                }

                // Create HttpClientTestHelper based on mode
                if (_test.DoActualWebAccess)
                {
                    if (_recorder != null)
                    {
                        _helper = HttpClientTestHelper.BeginRecording(_recorder);
                    }
                    // else: null means use real network access (no recording/playback)
                }
                else
                {
                    // Use playback if we have recorded interactions
                    if (expectedData != null && expectedData.Count > 0)
                    {
                        _helper = HttpClientTestHelper.PlaybackFromInteractions(expectedData);
                    }
                    else
                    {
                        // No recorded data available - require it for offline tests
                        Assert.Fail("No longer support non-web access without recorded data");
                    }
                }
            }

            /// <summary>
            /// Gets the HttpClientTestHelper instance, or null if using real network access.
            /// </summary>
            public HttpClientTestHelper Helper => _helper;

            public void Dispose()
            {
                // Save recordings if we were recording
                if (_test.IsRecordMode && _test.DoActualWebAccess && _recorder != null)
                {
                    var recordedInteractions = _recorder.Interactions?.ToList();
                    _test.RecordHttpInteractions(recordedInteractions);
                }

                _helper?.Dispose();
            }
        }

        #endregion
    }
}
