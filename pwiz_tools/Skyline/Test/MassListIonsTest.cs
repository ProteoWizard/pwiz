/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MassListIonsTest : AbstractUnitTest
    {
        private static readonly MeasuredIon[] CUSTOM_IONS = {
            MeasuredIonList.TMT_133_H,
            MeasuredIonList.TMT_134_L,
            MeasuredIonList.TMT_134_H,
            MeasuredIonList.TMT_135
        };

        private const int PRECURSOR_COUNT = 5;

        /// <summary>
        /// Tests import of precursor isotopes and reporter ions through a transition list.
        /// </summary>
        [TestMethod]
        public void MassListSpecialIonsTest()
        {
            var text = TRANSITION_LIST_TEST
                .Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            // First try default settings for SRM which will fail isotopic precursors and custom ions
            // The monoisotopic precursor should still be accepted.
            var settings = SrmSettingsList.GetDefault();
            var doc = new SrmDocument(settings);
            VerifyImport(doc, text, 1, 0, PRECURSOR_COUNT + CUSTOM_IONS.Length - 1);
            
            // Add settings to support the isotopic precursors
            settings = settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 3, IsotopeEnrichmentsList.GetDefault()));
            doc = doc.ChangeSettings(settings);
            VerifyImport(doc, text, PRECURSOR_COUNT, 0, CUSTOM_IONS.Length);
            
            // Add the custom ions
            settings = settings.ChangeTransitionFilter(f => f.ChangeMeasuredIons(CUSTOM_IONS));
            doc = doc.ChangeSettings(settings);
            VerifyImport(doc, text, 5, CUSTOM_IONS.Length, 0);

            // Try precursor [M-1] first to make the importer decide
            // the product m/z column from the M-1 isotope m/z
            var lines = text.ReadLines().ToList();
            Assert.AreEqual(13, lines.Count);
            (lines[1], lines[2]) = (lines[2], lines[1]);    // Swap lines 2 and 3 - leaving header
            var textPMinusFirst = TextUtil.LineSeparate(lines);
            VerifyImport(doc, textPMinusFirst, 5, CUSTOM_IONS.Length, 0);

            // Try only custom ions with no precursors to make the importer decide
            // the product m/z column from a custom ion mass
            var linesNoP = text.ReadLines().ToList();
            linesNoP.RemoveRange(1, PRECURSOR_COUNT);
            var textNoP = TextUtil.LineSeparate(linesNoP);
            VerifyImport(doc, textNoP, 0, CUSTOM_IONS.Length, 0);
        }

        private static void VerifyImport(SrmDocument doc, string text, int precursorCount, int customCount, int errorCount)
        {
            var inputs = new MassListInputs(text, true);
            var progressMonitor = new SilentProgressMonitor();
            inputs.ReadLines(progressMonitor);
            var importer = new MassListImporter(doc, inputs, false);
            Assert.IsTrue(importer.PreImport(progressMonitor, null));
            var indices = importer.RowReader.Indices;
            Assert.AreEqual(13, indices.LibraryColumn); // Could be fixed to be -1
            indices.LibraryColumn = -1; // Because it is all #N/A
            Assert.AreEqual(4, indices.ExplicitCollisionEnergyColumn); // Could be fixed to be -1
            importer.RowReader.Indices.ExplicitCollisionEnergyColumn = -1; // Because it is all 0s
            var errorList = new List<TransitionImportErrorInfo>();
            var peptideList = importer.DoImport(progressMonitor, new Dictionary<string, FastaSequence>(),
                new List<MeasuredRetentionTime>(), new List<SpectrumMzInfo>(), errorList).ToList();
            Assert.AreEqual(errorCount, errorList.Count, TextUtil.LineSeparate(errorList.ConvertAll(te => te.ToString())));
            Assert.AreEqual(1, peptideList.Count);
            doc = doc.AddPeptideGroups(peptideList, true, IdentityPath.ROOT, out _, out _);
            AssertEx.IsDocumentState(doc, null, 1, 1, inputs.Lines.Count - 1 - errorCount);
            int seen = 0;
            foreach (var nodeTran in doc.PeptideTransitions)
            {
                if (seen < precursorCount)
                {
                    Assert.AreEqual(IonType.precursor, nodeTran.Transition.IonType);
                    Assert.AreEqual(precursorCount == 1 ? 0 : seen - 1, nodeTran.Transition.MassIndex);
                    Assert.IsTrue(doc.Settings.TransitionSettings.FullScan.IsEnabledMs ==
                                  (doc.Settings.GetChromSource(nodeTran) == ChromSource.ms1));
                }
                else if (seen < precursorCount + customCount)
                {
                    Assert.AreEqual(IonType.custom, nodeTran.Transition.IonType);
                    Assert.AreEqual(CUSTOM_IONS[seen - precursorCount].SettingsCustomIon.Name, nodeTran.Transition.CustomIon.Name);
                }
                else
                {
                    Assert.AreEqual(IonType.y, nodeTran.Transition.IonType);
                }

                seen++;
            }
        }

        private const string TRANSITION_LIST_TEST =
            @"Protein Name	Peptide Modified Sequence	Precursor Mz	Precursor Charge	Collision Energy	Product Mz	Product Charge	Fragment Ion	Fragment Ion Type	Fragment Ion Ordinal	Loss Neutral Mass	Losses	Library Rank	Library Intensity	Isotope Dist Index	Isotope Dist Rank	Isotope Dist Proportion	Full Scan Filter Width	Transition Is Decoy	Product Decoy Mz Shift
peptides1	PEPTIDER	478.737814	2	0	478.737814	2	precursor	precursor	8	0		#N/A	#N/A	0	1	0.592	0.009575	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	478.236361	2	precursor [M-1]	precursor	8	0		#N/A	#N/A	-1	5	0	0.009565	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	479.239266	2	precursor [M+1]	precursor	8	0		#N/A	#N/A	1	2	0.2926	0.009585	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	479.740545	2	precursor [M+2]	precursor	8	0		#N/A	#N/A	2	3	0.0898	0.009595	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	480.241799	2	precursor [M+3]	precursor	8	0		#N/A	#N/A	3	4	0.0205	0.009605	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	133.15121	1	TMT-133H	custom	#N/A	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	134.148245	1	TMT-134L	custom	#N/A	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	134.154565	1	TMT-134H	custom	#N/A	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	135.1516	1	TMT-135	custom	#N/A	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	730.372994	1	y6	y	6	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	633.32023	1	y5	y	5	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A
peptides1	PEPTIDER	478.737814	2	0	532.272552	1	y4	y	4	0		#N/A	#N/A	0	#N/A	#N/A	#N/A	False	#N/A";
    }
}