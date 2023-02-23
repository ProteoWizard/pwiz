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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;


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

        public static void CleanupMSAmandaTmpFiles()
        {
            // MSAmanda intentionally leaves tempfiles behind (as caches in case of repeat runs)
            // But our test system wants a clean finish
            var msAmandaTmpDir = Path.Combine(Path.GetTempPath(), MSAmandaSearchWrapper.MS_AMANDA_TMP);
            DirectoryEx.SafeDelete(msAmandaTmpDir);
        }
    }
}
