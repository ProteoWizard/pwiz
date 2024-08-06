/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Support for various tests originally written for peptide data, but now also used for small
    /// molecule development and test
    /// </summary>
    public class AsSmallMoleculeTestUtil
    {
        public static void TranslateFilesToSmallMolecules(string dir, bool discardProteomicColumns)
        {
            // Deal with transition lists
            foreach (var fname in Directory.GetFiles(dir, "*.?sv"))
            {
                // Modify the header line to make this look like a small molecule transition list that gives just names and mz
                var lines = File.ReadAllLines(fname);
                TranslateTransitionListToSmallMolecules(lines, 0, discardProteomicColumns);
                if (fname.Contains(@"cirts.tsv")) // Has conflicting IRTs, just remove the conflicts
                {
                    RemoveConflictLines(lines);
                }
                File.WriteAllLines(fname, lines);
            }

        }

        public static void TranslateSkylineDocumentToSmallMolecules(string path)
        {
            var docPath = path;
            AsSmallMoleculeTestUtil.ConvertToSmallMolecules(null, ref docPath, null, RefinementSettings.ConvertToSmallMoleculesMode.formulas);
            File.Delete(path);
            File.Move(docPath, path);
        }

        public static string AdjustTransitionListForTestMode(string text, int addZ, bool asSmallMolecules)
        {
            if (!asSmallMolecules)
            {
                return text;
            }
            var lines = text.Split('\n');
            TranslateTransitionListToSmallMolecules(lines, addZ);
            return string.Join("\n", lines);
        }

        private static void TranslateTransitionListToSmallMolecules(string[] lines, int addZ = 0, bool discardProteomicColumns = false)
        {
            // Clean up a funky line in OpenSWATH_SM4_iRT.csv - missing a column
            const string badPep = "YILAGVENSK";
            const string badTail = "\t0\t0\t0\t2\tlight\t";
            for (var l = 1; l < lines.Length; l++)
            {
                var text = lines[l];
                if (text.Contains(badPep) && text.Contains(badTail))
                {
                    text = text.Replace(badTail+"\t", "\t" + badPep + badTail);
                    lines[l] = text;
                }
            }

            RemoveColumn(string.Empty, lines);
            RemoveColumn("copies", lines);
            RemoveColumn("FragmentSeriesNumber", lines);
            RemoveColumn("FragmentType", lines);
            RemoveColumn("FullUniModPeptideName", lines);
            RemoveColumn("CE", lines); // There's a negative CE in there, but not pertinent to this test
            RemoveColumn("CollisionEnergy", lines); // There's a negative CE in there, but not pertinent to this test
            RemoveColumn("PrecursorMz", lines); // Prefer to get mz from formula
            if (discardProteomicColumns)
            {
                foreach (var col in new[]
                         {
                             "decoy",
                             "transition_name",
                             "transition_group_id",
                             "FullUniModPeptideName",
                             "MissedCleavages",
                             "Replicates",
                             "NrModifications",
                             "UniprotID"
                         })
                {
                    RemoveColumn(col, lines);
                }
            }
            var header = lines[0].Replace("PeptideSequence", "Molecule")
                .Replace("Peptide", "Molecule")
                .Replace("Protein", "MoleculeListName")
                .Replace("MoleculeListId", "MoleculeListName")
                .Replace("MoleculeListNameName", "MoleculeListName")
                .Replace("MoleculeListNameId", "MoleculeListName")
                .Replace("GroupLabel","Label")
                .Replace("ProductChange", "ProductCharge") // Typo in some lists
                .Replace("Annotation", "Fragment Name");
            AssertEx.AreNotEqual(header, lines[0], $@"unknown header type ""{header}""");
            var headers = header.Split('\t');
            var molCol = headers.IndexOf(col => col.Equals("Molecule", StringComparison.OrdinalIgnoreCase));
            lines[0] = header + "\tChemicalFormula";
            if (addZ != 0)
            {
                lines[0] += "\tPrecursor Adduct";
            }
            var hasLabels = lines[0].Contains("\tPrecursorCharge\tLabel\t");
            var masscalc = new SequenceMassCalc(MassType.Monoisotopic);
            // Modify any peptides to look like just a name, e.g. PEPTIDER -> pep_PEPTIDER
            for (var line = 1; line < lines.Length; line++)
            {
                var text = lines[line];
                if (string.IsNullOrEmpty(text))
                {
                    continue; // Probably the last, empty, line
                }

                // Note heavy labels
                if (hasLabels)
                {
                    foreach (var z in Enumerable.Range(2, 5))
                    {
                        text = text.Replace($"\t{z}\theavy\t", $"\t[MC13+{z}H]\theavy\t");
                    }
                }

                var parts = text.Split('\t');
                var pep = molCol >= 0 ? parts[molCol] : null;
                for (var part = 0; part < parts.Length; part++)
                {
                    if (parts[part].Length >= 5 && Regex.IsMatch(parts[part].Substring(0, 5), @"^[ACDEFGHIKLMNPQRSTVWY]+$"))
                    {
                        parts[part] = RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator + parts[part];
                    }
                }

                // Insert chem formula
                lines[line] = string.Join("\t", parts) +
                              (string.IsNullOrEmpty(pep) ? string.Empty : ("\t" + masscalc.GetMolecularFormula(pep))) +
                              ((addZ != 0) ? ("\t" + Adduct.FromChargeProtonated(addZ).AsFormula()) : string.Empty);
            }
        }

        private static void RemoveColumn(string colName, IList<string> lines)
        {
            while(true) // Possibly a column appears twice
            {
                var cols = lines[0].Split('\t');
                var index = cols.IndexOf(c => c == colName);
                if (index > -1)
                {
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var values = lines[i].Split('\t').ToList();
                        values.RemoveAt(index);
                        lines[i] = string.Join("\t", values);
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private static  void RemoveConflictLines(string[] lines)
        {
            var noted = new Dictionary<string,string>();
            var headers = lines[0].Split('\t');
            var irtCol = headers.IndexOf(c => c.StartsWith("NormalizedRetentionTime"));
            for (var i = 1; i < lines.Length; i++)
            {
                var columns = lines[i].Split('\t');
                var pep = columns.First(c =>
                    c.StartsWith(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator));
                var rt = columns[irtCol].Trim();
                if (noted.TryGetValue(pep, out var rtNoted))
                {
                    if (!Equals(rt, rtNoted))
                    {
                        lines[i] = string.Empty; // Conflict, eliminate it
                    }
                }
                else
                {
                    noted.Add(pep, rt);
                }
            }
        }

        public static SrmDocument ConvertToSmallMolecules(SrmDocument doc, ref string docPath, IEnumerable<string> dataPaths,
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
            docPath = DocPathConvertedToSmallMolecules(docPath);
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

        public static string SMALL_MOL_CONVERSION_TAG = @"_converted_to_small_molecules";
        public static string DocPathConvertedToSmallMolecules(string docPath)
        {
            return docPath.Replace(".sky", SMALL_MOL_CONVERSION_TAG + ".sky");
        }

    }
}
