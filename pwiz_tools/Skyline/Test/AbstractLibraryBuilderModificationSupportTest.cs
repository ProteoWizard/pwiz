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

        public LibraryBuilderModificationSupport Support => new LibraryBuilderModificationSupport(TestModEL_SUPPORTED_TestModIFICATION_INDICES);

        private static Dictionary<ModificationType, PredictionSupport> TestModEL_SUPPORTED_TestModIFICATION_INDICES =
            new Dictionary<ModificationType, PredictionSupport>

            {
                {
                    new ModificationType(1,"TestMod1", "TestOnly"), PredictionSupport.ALL
                },
                {
                    new ModificationType(2, "TestMod2", "TestOnly"), PredictionSupport.FRAGMENTATION
                },
                {
                    new ModificationType(3, "TestMod3", "TestOnly"), PredictionSupport.RETENTION_TIME
                },
                {
                    new ModificationType(4, "TestMod4", "TestOnly"), PredictionSupport.CCS
                }

            };
       
        [TestMethod]
        public void PredictionSupportTest()
        {
            // Assert
            Assert.IsNotNull(Support._predictionSupport);
            Assert.AreEqual(Support._predictionSupport.Count, 4); // TestMod1, TestMod2, TestMod3, TestMod4

            Assert.IsTrue(Support._predictionSupport["1"].Fragmentation);
            Assert.IsTrue(Support._predictionSupport["1"].RetentionTime);
            Assert.IsTrue(Support._predictionSupport["1"].Ccs);

            Assert.IsTrue(Support._predictionSupport["2"].Fragmentation);
            Assert.IsFalse(Support._predictionSupport["2"].RetentionTime);
            Assert.IsFalse(Support._predictionSupport["2"].Ccs);

            Assert.IsFalse(Support._predictionSupport["3"].Fragmentation);
            Assert.IsTrue(Support._predictionSupport["3"].RetentionTime);
            Assert.IsFalse(Support._predictionSupport["3"].Ccs);

            Assert.IsFalse(Support._predictionSupport["4"].Fragmentation);
            Assert.IsFalse(Support._predictionSupport["4"].RetentionTime);
            Assert.IsTrue(Support._predictionSupport["4"].Ccs);

            var support = new LibraryBuilderModificationSupport(new Dictionary<ModificationType, PredictionSupport>());

            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void SupportedModsTest()
        {
            // Act & Assert
            Assert.IsTrue(Support.IsMs2SupportedMod("1:TestMod1"));
            Assert.IsTrue(Support.IsMs2SupportedMod("2:TestMod2"));
            Assert.IsFalse(Support.IsMs2SupportedMod("3:TestMod3"));
            Assert.IsFalse(Support.IsMs2SupportedMod("999:UNKNOWN"));

            Assert.IsTrue(Support.IsRtSupportedMod("1:TestMod1"));
            Assert.IsFalse(Support.IsRtSupportedMod("2:TestMod2"));
            Assert.IsTrue(Support.IsRtSupportedMod("3:TestMod3"));
            Assert.IsFalse(Support.IsRtSupportedMod("999:UNKNOWN"));
  
            Assert.IsTrue(Support.IsCcsSupportedMod("1:TestMod1"));
            Assert.IsFalse(Support.IsCcsSupportedMod("2:TestMod2"));
            Assert.IsTrue(Support.IsCcsSupportedMod("4:TestMod4"));
            Assert.IsFalse(Support.IsCcsSupportedMod("999:UNKNOWN"));
   
            Assert.IsTrue(Support.AreAllModelsSupported("1:TestMod1")); // Supports all
            Assert.IsFalse(Support.AreAllModelsSupported("2:TestMod2")); // Only MS2
            Assert.IsFalse(Support.AreAllModelsSupported("3:TestMod3")); // Only RT
            Assert.IsFalse(Support.AreAllModelsSupported("4:TestMod4")); // Only CCS
            Assert.IsFalse(Support.AreAllModelsSupported("999:UNKNOWN"));
  
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