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
    }
}
