/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AbstractLibraryBuilderModificationSupportTests : AbstractUnitTest
    {
        private static readonly List<ModificationType> MODEL_SUPPORTED_MODS =
            new List<ModificationType>

            {
                AbstractDeepLibraryBuilder.GetUniModType(4, PredictionSupport.all), // Carbamidomethyl (C)
                AbstractDeepLibraryBuilder.GetUniModType(21, PredictionSupport.fragmentation), // Phospho
                AbstractDeepLibraryBuilder.GetUniModType(35, PredictionSupport.retention_time), // Oxidation
                AbstractDeepLibraryBuilder.GetUniModType(121, PredictionSupport.ccs) // GlyGly (a.k.a. GG)
            };

        private static readonly int ID0 = MODEL_SUPPORTED_MODS[0].Id;
        private static readonly int ID1 = MODEL_SUPPORTED_MODS[1].Id;
        private static readonly int ID2 = MODEL_SUPPORTED_MODS[2].Id;
        private static readonly int ID3 = MODEL_SUPPORTED_MODS[3].Id;
        private static readonly int ID_ACETYL = 1;

        private static readonly string ACC0 = MODEL_SUPPORTED_MODS[0].Accession;
        private static readonly string ACC1 = MODEL_SUPPORTED_MODS[1].Accession;
        private static readonly string ACC2 = MODEL_SUPPORTED_MODS[2].Accession;
        private static readonly string ACC3 = MODEL_SUPPORTED_MODS[3].Accession;

        [TestMethod]
        public void PredictionSupportTest()
        {
            var support = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_MODS);
            
            var supportNull = support.GetModificationType(0);

            Assert.IsNull(supportNull);

            var support0 = support.GetModificationType(ID0);
            var support1 = support.GetModificationType(ID1);
            var support2 = support.GetModificationType(ID2);
            var support3 = support.GetModificationType(ID3);

            Assert.IsNotNull(support0);
            Assert.IsNotNull(support1);
            Assert.IsNotNull(support2);
            Assert.IsNotNull(support3);
            
            Assert.IsTrue(support0.IsSupported(PredictionSupport.all));

            Assert.IsTrue(support1.IsSupported(PredictionSupport.fragmentation));
            Assert.IsFalse(support1.IsSupported(PredictionSupport.all));

            Assert.IsTrue(support2.IsSupported(PredictionSupport.retention_time));
            Assert.IsFalse(support2.IsSupported(PredictionSupport.all));

            Assert.IsTrue(support3.IsSupported(PredictionSupport.ccs));
            Assert.IsFalse(support3.IsSupported(PredictionSupport.all));

            support = new LibraryBuilderModificationSupport(new List<ModificationType>());
            
            Assert.IsNull(support.GetModificationType(ID0));
            Assert.IsNull(support.GetModificationType(ID1));
            Assert.IsNull(support.GetModificationType(ID2));
            Assert.IsNull(support.GetModificationType(ID3));
        }

        [TestMethod]
        public void SupportedModsTest()
        {
            var support = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_MODS);

            Assert.IsFalse(support.IsSupportedMod(null, PredictionSupport.fragmentation));
            Assert.IsFalse(support.IsSupportedMod(null, PredictionSupport.retention_time));
            Assert.IsFalse(support.IsSupportedMod(null, PredictionSupport.ccs));
            Assert.IsFalse(support.IsSupportedMod(null, PredictionSupport.all));

            Assert.IsFalse(support.IsSupportedMod(0, PredictionSupport.fragmentation));
            Assert.IsFalse(support.IsSupportedMod(0, PredictionSupport.retention_time));
            Assert.IsFalse(support.IsSupportedMod(0, PredictionSupport.ccs));
            Assert.IsFalse(support.IsSupportedMod(0, PredictionSupport.all));

            Assert.IsTrue(support.IsSupportedMod(ID0, PredictionSupport.fragmentation));
            Assert.IsTrue(support.IsSupportedMod(ID1, PredictionSupport.fragmentation));
            Assert.IsFalse(support.IsSupportedMod(ID2, PredictionSupport.fragmentation));
            Assert.IsFalse(support.IsSupportedMod(ID_ACETYL, PredictionSupport.fragmentation));

            Assert.IsTrue(support.IsSupportedMod(ID0, PredictionSupport.retention_time));
            Assert.IsFalse(support.IsSupportedMod(ID1, PredictionSupport.retention_time));
            Assert.IsTrue(support.IsSupportedMod(ID2, PredictionSupport.retention_time));
            Assert.IsFalse(support.IsSupportedMod(ID_ACETYL, PredictionSupport.retention_time));

            Assert.IsTrue(support.IsSupportedMod(ID0, PredictionSupport.ccs));
            Assert.IsFalse(support.IsSupportedMod(ID1, PredictionSupport.ccs));
            Assert.IsTrue(support.IsSupportedMod(ID3, PredictionSupport.ccs));
            Assert.IsFalse(support.IsSupportedMod(ID_ACETYL, PredictionSupport.ccs));

            Assert.IsTrue(support.IsSupportedMod(ID0, PredictionSupport.all));
            Assert.IsFalse(support.IsSupportedMod(ID1, PredictionSupport.all));
            Assert.IsFalse(support.IsSupportedMod(ID2, PredictionSupport.all));
            Assert.IsFalse(support.IsSupportedMod(ID3, PredictionSupport.all));
            Assert.IsFalse(support.IsSupportedMod(ID_ACETYL, PredictionSupport.all));

            Assert.IsTrue(support.PeptideHasOnlyMs2SupportedMod($"PEPTIDE[{ACC0}]R[{ACC1}]")); // Both MS2 supported
            Assert.IsFalse(support.PeptideHasOnlyMs2SupportedMod($"PEPTIDE[{ACC2}]")); // Mod index 2 does not support MS2
            Assert.IsFalse(support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(support.PeptideHasOnlyMs2SupportedMod("PEPTIDE")); // No mods
 
            Assert.IsTrue(support.PeptideHasOnlyRtSupportedMod($"PEPTIDE[{ACC0}]R[{ACC2}]")); // Both RT supported
            Assert.IsFalse(support.PeptideHasOnlyRtSupportedMod($"PEPTIDE[{ACC1}]")); // Mod index 1 does not support RT
            Assert.IsFalse(support.PeptideHasOnlyRtSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(support.PeptideHasOnlyRtSupportedMod("PEPTIDE")); // No mods
 
            Assert.IsTrue(support.PeptideHasOnlyCcsSupportedMod($"PEPTIDE[{ACC0}]R[{ACC3}]")); // Both CCS supported
            Assert.IsFalse(support.PeptideHasOnlyCcsSupportedMod($"PEPTIDE[{ACC1}]")); // Mod index 1 does not support CCS
            Assert.IsFalse(support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(support.PeptideHasOnlyCcsSupportedMod("PEPTIDE")); // No mods

            Assert.IsTrue(support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[INVALID]")); // No valid numeric accession
            Assert.IsTrue(support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[]")); // Empty brackets
        }
    }
}