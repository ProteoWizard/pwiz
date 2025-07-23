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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AbstractLibraryBuilderModificationSupportTests : AbstractUnitTest
    {

        public LibraryBuilderModificationSupport Support => new LibraryBuilderModificationSupport(MODEL_SUPPORTED_MODS);

        private static List<ModificationType> MODEL_SUPPORTED_MODS =
            new List<ModificationType>

            {
                {
                    new ModificationType(1,"TestMod1", "TestOnly", PredictionSupport.ALL)
                },
                {
                    new ModificationType(2, "TestMod2", "TestOnly", PredictionSupport.FRAGMENTATION)
                },
                {
                    new ModificationType(3, "TestMod3", "TestOnly", PredictionSupport.RETENTION_TIME)
                },
                {
                    new ModificationType(4, "TestMod4", "TestOnly", PredictionSupport.CCS)
                }

            };
       
        [TestMethod]
        public void PredictionSupportTest()
        {
            // Assert
            Assert.IsNotNull(Support._predictionSupport);
            Assert.AreEqual(Support._predictionSupport.Count, 4); // TestMod1, TestMod2, TestMod3, TestMod4

            var nullId = 0;
            var supportNull = Support._predictionSupport.FirstOrDefault(source => source.Id == nullId);

            Assert.IsNull(supportNull);

            var support0 = Support._predictionSupport.FirstOrDefault(source => source.Id == MODEL_SUPPORTED_MODS[0].Id);
            var support1 = Support._predictionSupport.FirstOrDefault(source => source.Id == MODEL_SUPPORTED_MODS[1].Id);
            var support2 = Support._predictionSupport.FirstOrDefault(source => source.Id == MODEL_SUPPORTED_MODS[2].Id);
            var support3 = Support._predictionSupport.FirstOrDefault(source => source.Id == MODEL_SUPPORTED_MODS[3].Id);

            Assert.IsNotNull(support0);
            Assert.IsNotNull(support1);
            Assert.IsNotNull(support2);
            Assert.IsNotNull(support3);
            
            Assert.IsTrue(support0!.SupportedModels.Fragmentation);
            Assert.IsTrue(support0!.SupportedModels.RetentionTime);
            Assert.IsTrue(support0!.SupportedModels.Ccs);

            Assert.IsTrue(support1!.SupportedModels.Fragmentation);
            Assert.IsFalse(support1!.SupportedModels.RetentionTime);
            Assert.IsFalse(support1!.SupportedModels.Ccs);

            Assert.IsFalse(support2!.SupportedModels.Fragmentation);
            Assert.IsTrue(support2!.SupportedModels.RetentionTime);
            Assert.IsFalse(support2!.SupportedModels.Ccs);

            Assert.IsFalse(support3!.SupportedModels.Fragmentation);
            Assert.IsFalse(support3!.SupportedModels.RetentionTime);
            Assert.IsTrue(support3!.SupportedModels.Ccs);

            var support = new LibraryBuilderModificationSupport(new List<ModificationType>());

            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void SupportedModsTest()
        {
            Assert.IsFalse(Support.IsMs2SupportedMod(null));
            Assert.IsFalse(Support.IsRtSupportedMod(null));
            Assert.IsFalse(Support.IsCcsSupportedMod(null));
            Assert.IsFalse(Support.AreAllModelsSupported(null));

            var nullId = 0;
            Assert.IsFalse(Support.IsMs2SupportedMod(nullId));
            Assert.IsFalse(Support.IsRtSupportedMod(nullId));
            Assert.IsFalse(Support.IsCcsSupportedMod(nullId));
            Assert.IsFalse(Support.AreAllModelsSupported(nullId));

            Assert.IsTrue(Support.IsMs2SupportedMod(1));
            Assert.IsTrue(Support.IsMs2SupportedMod(2));
            Assert.IsFalse(Support.IsMs2SupportedMod(3));
            Assert.IsFalse(Support.IsMs2SupportedMod(999));

            Assert.IsTrue(Support.IsRtSupportedMod(1));
            Assert.IsFalse(Support.IsRtSupportedMod(2));
            Assert.IsTrue(Support.IsRtSupportedMod(3));
            Assert.IsFalse(Support.IsRtSupportedMod(999));
  
            Assert.IsTrue(Support.IsCcsSupportedMod(1));
            Assert.IsFalse(Support.IsCcsSupportedMod(2));
            Assert.IsTrue(Support.IsCcsSupportedMod(4));
            Assert.IsFalse(Support.IsCcsSupportedMod(999));
   
            Assert.IsTrue(Support.AreAllModelsSupported(1)); // Supports all
            Assert.IsFalse(Support.AreAllModelsSupported(2)); // Only MS2
            Assert.IsFalse(Support.AreAllModelsSupported(3)); // Only RT
            Assert.IsFalse(Support.AreAllModelsSupported(4)); // Only CCS
            Assert.IsFalse(Support.AreAllModelsSupported(999));
  
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[TestMod1:1]R[TestMod2:2]")); // Both MS2 supported
            Assert.IsFalse(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[TestMod3:3]")); // TestMod3 not MS2 supported
            Assert.IsFalse(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE")); // No mods
 
            Assert.IsTrue(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[TestMod1:1]R[TestMod3:3]")); // Both RT supported
            Assert.IsFalse(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[TestMod2:2]")); // TestMod2 not RT supported
            Assert.IsFalse(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE")); // No mods
 
            Assert.IsTrue(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[TestMod1:1]R[TestMod4:4]")); // Both CCS supported
            Assert.IsFalse(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[TestMod2:2]")); // TestMod2 not CCS supported
            Assert.IsFalse(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE")); // No mods

            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[INVALID]")); // No valid numeric accession
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[]")); // Empty brackets
        }
    }
}