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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Koina.Config;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using System.Globalization;
using System.Reflection;


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

        public SrmDocument ConvertToSmallMolecules(SrmDocument doc, ref string docPath, IEnumerable<string> dataPaths,
            RefinementSettings.ConvertToSmallMoleculesMode mode)
        {
            if (doc == null)
            {
                using (var cmd = new CommandLine())
                {
                    Assert.IsTrue(cmd.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imsdb file
                    var docLoad = cmd.Document;
                    using (var docContainer = new ResultsTestDocumentContainer(null, docPath))
                    {
                        docContainer.SetDocument(docLoad, null, true);
                        docContainer.AssertComplete();
                        doc = docContainer.Document;
                    }
                }
            }
            if (mode == RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                return doc;
            }

            var docOriginal = doc;
            var refine = new RefinementSettings();
            docPath = docPath.Replace(".sky", "_converted_to_small_molecules.sky");
            var docSmallMol =
                refine.ConvertToSmallMolecules(doc, Path.GetDirectoryName(docPath), mode);
            if (docSmallMol.MeasuredResults != null)
            {
                foreach (var stream in docSmallMol.MeasuredResults.ReadStreams)
                {
                    stream.CloseStream();
                }
            }
            var listChromatograms = new List<ChromatogramSet>();
            if (dataPaths != null)
            {
                foreach (var dataPath in dataPaths)
                {
                    if (!string.IsNullOrEmpty(dataPath))
                    {
                        listChromatograms.Add(AssertResult.FindChromatogramSet(docSmallMol, new MsDataFilePath(dataPath)) ??
                                              new ChromatogramSet(Path.GetFileName(dataPath).Replace('.', '_'),
                                                  new[] { dataPath }));
                    }
                }
            }
            var docResults = docSmallMol.ChangeMeasuredResults(listChromatograms.Any() ? new MeasuredResults(listChromatograms) : null);

            // Since refine isn't in a document container, have to close the streams manually to avoid file locking trouble (thanks, Nick!)
            foreach (var library in docResults.Settings.PeptideSettings.Libraries.Libraries)
            {
                foreach (var stream in library.ReadStreams)
                {
                    stream.CloseStream();
                }
            }

            // Save and restore to ensure library caches
            var cmdline = new CommandLine();
            cmdline.SaveDocument(docResults, docPath, TextWriter.Null);
            Assert.IsTrue(cmdline.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imsdb file
            docResults = cmdline.Document;
            using (var docContainer = new ResultsTestDocumentContainer(null, docPath))
            {
                docContainer.SetDocument(docResults, null, true);
                docContainer.AssertComplete();
                doc = docContainer.Document;
            }
            AssertEx.ConvertedSmallMoleculeDocumentIsSimilar(docOriginal, doc, Path.GetDirectoryName(docPath), mode);
            return doc;
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


        public static void AssertLastEntry(AuditLogList auditLogList, MessageType messageType)
        {
            var lastEntry = auditLogList.AuditLogEntries;
            Assert.IsFalse(lastEntry.IsRoot);
            Assert.AreNotEqual(0, lastEntry.AllInfo.Count);
            Assert.AreEqual(messageType, lastEntry.AllInfo[0].MessageInfo.Type);
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
            if (_systemResources == null || !Equals(_systemResourcesCultureInfo, CultureInfo.CurrentUICulture))
            {
                _systemResources?.Dispose();
                var assembly = Assembly.GetAssembly(typeof(object));
                var assemblyName = assembly.GetName().Name;
                var manager = new ResourceManager(assemblyName, assembly);
                _systemResources = manager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
                _systemResourcesCultureInfo = CultureInfo.CurrentUICulture;
            }

            return string.Format(_systemResources.GetString(resourceId) ?? throw new ArgumentException(nameof(resourceId)), args);
        }

        private static ResourceSet _systemResources;
        private static CultureInfo _systemResourcesCultureInfo;
    }
}
