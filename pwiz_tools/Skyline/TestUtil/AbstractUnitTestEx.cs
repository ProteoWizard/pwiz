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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            var exitStatus = CommandLineRunner.RunCommand(inputArgs, consoleOutput, true);

            var fail = exitStatus == Program.EXIT_CODE_SUCCESS && consoleOutput.IsErrorReported ||
                       exitStatus != Program.EXIT_CODE_SUCCESS && !consoleOutput.IsErrorReported;
            if (fail)
            {
                var message =
                    TextUtil.LineSeparate(
                        string.Format("{0} reported but exit status was {1}.",
                            consoleOutput.IsErrorReported ? "Error" : "No error", exitStatus),
                        "Output: ", consoleBuffer.ToString());
                Assert.Fail(message);
            }

            return consoleBuffer.ToString();
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

        /// <summary>
        /// Examine the lines of a DSV file an attempt to determine what kind of delimiter it uses
        /// N.B. NOT ROBUST ENOUGH FOR GENERAL USE - would likely fail, for example, on data that has
        /// irregular column counts. But still useful in the test context where we aren't handed random
        /// data sets from users.
        /// </summary>
        /// <param name="lines">lines of the file</param>
        /// <param name="columnCount">return value: column count</param>
        /// <returns>the identified delimiter</returns>
        /// <exception cref="LineColNumberedIoException">thrown when we can't figure it out</exception>
        public static char DetermineDsvDelimiter(string[] lines, out int columnCount)
        {

            // If a candidate delimiter yields different column counts line to line, it's probably not the right one.
            // So parse some distance in to see which delimiters give a consistent column count.
            // NOTE we do see files like that in the wild, but not in our test suite
            var countsPerLinePerCandidateDelimiter = new Dictionary<char, List<int>>
            {
                { TextUtil.SEPARATOR_CSV, new List<int>()},
                { TextUtil.SEPARATOR_SPACE, new List<int>()},
                { TextUtil.SEPARATOR_TSV, new List<int>()},
                { TextUtil.SEPARATOR_CSV_INTL, new List<int>()}
            };

            for (var lineNum = 0; lineNum < Math.Min(100, lines.Length); lineNum++)
            {
                foreach (var sep in countsPerLinePerCandidateDelimiter.Keys)
                {
                    countsPerLinePerCandidateDelimiter[sep].Add((new DsvFileReader(new StringReader(lines[lineNum]), sep)).NumberOfFields);
                }
            }

            var likelyCandidates = 
                countsPerLinePerCandidateDelimiter.Where(kvp => kvp.Value.Distinct().Count() == 1).ToArray();
            if (likelyCandidates.Length > 0)
            {
                // The candidate that yields the highest column count wins
                var maxColumnCount = likelyCandidates.Max(kvp => kvp.Value[0]);
                if (likelyCandidates.Count(kvp => Equals(maxColumnCount, kvp.Value[0])) == 1)
                {
                    var delimiter = likelyCandidates.First(kvp => Equals(maxColumnCount, kvp.Value[0])).Key;
                    columnCount = maxColumnCount;
                    return delimiter;
                }
            }

            throw new LineColNumberedIoException(Resources.TextUtil_DeterminDsvSeparator_Unable_to_determine_format_of_delimiter_separated_value_file, 1, 1);
        }
    }
}
