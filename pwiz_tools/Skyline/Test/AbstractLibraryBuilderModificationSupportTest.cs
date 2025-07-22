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

        public LibraryBuilderModificationSupport Support => new LibraryBuilderModificationSupport(MODEL_SUPPORTED_MODIFICATION_INDICES);

        private static Dictionary<ModificationType, PredictionSupport> MODEL_SUPPORTED_MODIFICATION_INDICES =
            new Dictionary<ModificationType, PredictionSupport>

            {
                {
                    new ModificationType(1,"001:MOD1", "TestMod1", "TestOnly"), PredictionSupport.ALL
                },
                {
                    new ModificationType(2,"002:MOD2", "TestMod2", "TestOnly"), PredictionSupport.FRAGMENTATION
                },
                {
                    new ModificationType(3, "003:MOD3", "TestMod3", "TestOnly"), PredictionSupport.RETENTION_TIME
                },
                {
                    new ModificationType(4, "004:MOD4", "TestMod4", "TestOnly"), PredictionSupport.CCS
                }

            };

        IList<ModificationType> Ms2SupportedList => new List<ModificationType>
        {
            new ModificationType(1, "001:MOD1", "TestMod1", "TestOnly"),
            new ModificationType(2, "002:MOD2", "TestMod2", "TestOnly")
        };

        IList<ModificationType> RtSupportedList => new List<ModificationType>
        {
            new ModificationType(1, "001:MOD1", "TestMod1", "TestOnly"),
            new ModificationType(3, "003:MOD3", "TestMod3", "TestOnly")
        };
        IList<ModificationType> CcsSupportedList => new List<ModificationType>
        {
            new ModificationType(1, "001:MOD1", "TestMod1", "TestOnly"),
            new ModificationType(4, "004:MOD4", "TestMod4", "TestOnly")
        };
       
        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_Constructor_PopulatesPredictionSupport_Correctly()
        {
            // Assert
            Assert.IsNotNull(Support._predictionSupport);
            Assert.AreEqual(Support._predictionSupport.Count, 4); // MOD1, MOD2, MOD3, MOD4

            Assert.IsTrue(Support._predictionSupport["001"].Fragmentation);
            Assert.IsTrue(Support._predictionSupport["001"].RetentionTime);
            Assert.IsTrue(Support._predictionSupport["001"].Ccs);

            Assert.IsTrue(Support._predictionSupport["002"].Fragmentation);
            Assert.IsFalse(Support._predictionSupport["002"].RetentionTime);
            Assert.IsFalse(Support._predictionSupport["002"].Ccs);

            Assert.IsFalse(Support._predictionSupport["003"].Fragmentation);
            Assert.IsTrue(Support._predictionSupport["003"].RetentionTime);
            Assert.IsFalse(Support._predictionSupport["003"].Ccs);

            Assert.IsFalse(Support._predictionSupport["004"].Fragmentation);
            Assert.IsFalse(Support._predictionSupport["004"].RetentionTime);
            Assert.IsTrue(Support._predictionSupport["004"].Ccs);
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_IsMs2SupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.IsMs2SupportedMod("001:MOD1"));
            Assert.IsTrue(Support.IsMs2SupportedMod("002:MOD2"));
            Assert.IsFalse(Support.IsMs2SupportedMod("003:MOD3"));
            Assert.IsFalse(Support.IsMs2SupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_IsRtSupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.IsRtSupportedMod("001:MOD1"));
            Assert.IsTrue(Support.IsRtSupportedMod("003:MOD3"));
            Assert.IsFalse(Support.IsRtSupportedMod("002:MOD2"));
            Assert.IsFalse(Support.IsRtSupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_IsCcsSupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.IsCcsSupportedMod("001:MOD1"));
            Assert.IsTrue(Support.IsCcsSupportedMod("004:MOD4"));
            Assert.IsFalse(Support.IsCcsSupportedMod("002:MOD2"));
            Assert.IsFalse(Support.IsCcsSupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_AreAllModelsSupported_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.AreAllModelsSupported("001:MOD1")); // Supports all
            Assert.IsFalse(Support.AreAllModelsSupported("002:MOD2")); // Only MS2
            Assert.IsFalse(Support.AreAllModelsSupported("003:MOD3")); // Only RT
            Assert.IsFalse(Support.AreAllModelsSupported("004:MOD4")); // Only CCS
            Assert.IsFalse(Support.AreAllModelsSupported("999:UNKNOWN"));
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PeptideHasOnlyMs2SupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[MOD1:001]R[MOD2:002]")); // Both MS2 supported
            Assert.IsFalse(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[MOD3:003]")); // MOD3 not MS2 supported
            Assert.IsFalse(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PeptideHasOnlyRtSupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[MOD1:001]R[MOD3:003]")); // Both RT supported
            Assert.IsFalse(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[MOD2:002]")); // MOD2 not RT supported
            Assert.IsFalse(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyRtSupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PeptideHasOnlyCcsSupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[MOD1:001]R[MOD4:004]")); // Both CCS supported
            Assert.IsFalse(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[MOD2:002]")); // MOD2 not CCS supported
            Assert.IsFalse(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(Support.PeptideHasOnlyCcsSupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PopulatePredictionModificationSupport_EmptyList_DoesNotThrow()
        {
            // Arrange
            var support = new LibraryBuilderModificationSupport(new Dictionary<ModificationType, PredictionSupport>());

            // Assert
            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PopulatePredictionModificationSupport_NullList_HandlesGracefully()
        {
            // Arrange
            var support = new LibraryBuilderModificationSupport(null);

            // Assert
            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void AbstractLibraryBuilderModificationSupportTests_PeptideHasOnlyMs2SupportedMod_MalformedPeptide_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[INVALID]")); // No valid numeric accession
            Assert.IsTrue(Support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[]")); // Empty brackets
        }
    }
}