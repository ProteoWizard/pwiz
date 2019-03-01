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
using pwiz.Skyline.Model.Results;


namespace pwiz.SkylineTestUtil
{
   
    /// <summary>
    /// An intermediate base class containing simplified functions for unit tests.
    /// </summary>
    public class AbstractUnitTestEx : AbstractUnitTest
    {
        protected static string RunCommand(params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            CommandLineRunner.RunCommand(inputArgs, consoleOutput);
            return consoleBuffer.ToString();
        }

        public SrmDocument ConvertToSmallMolecules(SrmDocument doc, ref string docPath, IEnumerable<string> dataPaths,
            RefinementSettings.ConvertToSmallMoleculesMode mode)
        {
            if (doc == null)
            {
                using (var cmd = new CommandLine())
                {
                    Assert.IsTrue(cmd.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imdb file
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
            Assert.IsTrue(cmdline.OpenSkyFile(docPath)); // Handles any path shifts in database files, like our .imdb file
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
    }
}
