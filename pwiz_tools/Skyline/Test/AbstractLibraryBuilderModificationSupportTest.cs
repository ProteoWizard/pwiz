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
        private LibraryBuilderModificationSupport _support;
        private IList<ModificationType> _ms2SupportedList;
        private IList<ModificationType> _rtSupportedList;
        private IList<ModificationType> _ccsSupportedList;

        [TestInitialize]
        public void SetUp()
        {
            _ms2SupportedList = new List<ModificationType>
            {
                new ModificationType("001:MOD1", "TestMod1", "TestOnly"),
                new ModificationType("002:MOD2", "TestMod2", "TestOnly")
            };
            _rtSupportedList = new List<ModificationType>
            {
                new ModificationType("001:MOD1", "TestMod1", "TestOnly"),
                new ModificationType("003:MOD3", "TestMod3", "TestOnly")
            };
            _ccsSupportedList = new List<ModificationType>
            {
                new ModificationType("001:MOD1", "TestMod1", "TestOnly"),
                new ModificationType("004:MOD4", "TestMod4", "TestOnly")
            };

            _support = new LibraryBuilderModificationSupport(_ms2SupportedList, _rtSupportedList, _ccsSupportedList);
        }

        [TestMethod]
        public void Constructor_PopulatesPredictionSupport_Correctly()
        {
            // Assert
            Assert.IsNotNull(_support._predictionSupport);
            Assert.AreEqual(_support._predictionSupport.Count, 4); // MOD1, MOD2, MOD3, MOD4

            Assert.IsTrue(_support._predictionSupport["001"].IsFragmentationPredictionSupported);
            Assert.IsTrue(_support._predictionSupport["001"].IsRetentionTimePredictionSupported);
            Assert.IsTrue(_support._predictionSupport["001"].IsCcsPredictionSupported);

            Assert.IsTrue(_support._predictionSupport["002"].IsFragmentationPredictionSupported);
            Assert.IsFalse(_support._predictionSupport["002"].IsRetentionTimePredictionSupported);
            Assert.IsFalse(_support._predictionSupport["002"].IsCcsPredictionSupported);

            Assert.IsFalse(_support._predictionSupport["003"].IsFragmentationPredictionSupported);
            Assert.IsTrue(_support._predictionSupport["003"].IsRetentionTimePredictionSupported);
            Assert.IsFalse(_support._predictionSupport["003"].IsCcsPredictionSupported);

            Assert.IsFalse(_support._predictionSupport["004"].IsFragmentationPredictionSupported);
            Assert.IsFalse(_support._predictionSupport["004"].IsRetentionTimePredictionSupported);
            Assert.IsTrue(_support._predictionSupport["004"].IsCcsPredictionSupported);
        }

        [TestMethod]
        public void IsMs2SupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.IsMs2SupportedMod("001:MOD1"));
            Assert.IsTrue(_support.IsMs2SupportedMod("002:MOD2"));
            Assert.IsFalse(_support.IsMs2SupportedMod("003:MOD3"));
            Assert.IsFalse(_support.IsMs2SupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void IsRtSupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.IsRtSupportedMod("001:MOD1"));
            Assert.IsTrue(_support.IsRtSupportedMod("003:MOD3"));
            Assert.IsFalse(_support.IsRtSupportedMod("002:MOD2"));
            Assert.IsFalse(_support.IsRtSupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void IsCcsSupportedMod_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.IsCcsSupportedMod("001:MOD1"));
            Assert.IsTrue(_support.IsCcsSupportedMod("004:MOD4"));
            Assert.IsFalse(_support.IsCcsSupportedMod("002:MOD2"));
            Assert.IsFalse(_support.IsCcsSupportedMod("999:UNKNOWN"));
        }

        [TestMethod]
        public void AreAllModelsSupported_ValidAccession_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.AreAllModelsSupported("001:MOD1")); // Supports all
            Assert.IsFalse(_support.AreAllModelsSupported("002:MOD2")); // Only MS2
            Assert.IsFalse(_support.AreAllModelsSupported("003:MOD3")); // Only RT
            Assert.IsFalse(_support.AreAllModelsSupported("004:MOD4")); // Only CCS
            Assert.IsFalse(_support.AreAllModelsSupported("999:UNKNOWN"));
        }

        [TestMethod]
        public void PeptideHasOnlyMs2SupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[MOD1:001]R[MOD2:002]")); // Both MS2 supported
            Assert.IsFalse(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[MOD3:003]")); // MOD3 not MS2 supported
            Assert.IsFalse(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void PeptideHasOnlyRtSupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.PeptideHasOnlyRtSupportedMod("PEPTIDE[MOD1:001]R[MOD3:003]")); // Both RT supported
            Assert.IsFalse(_support.PeptideHasOnlyRtSupportedMod("PEPTIDE[MOD2:002]")); // MOD2 not RT supported
            Assert.IsFalse(_support.PeptideHasOnlyRtSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(_support.PeptideHasOnlyRtSupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void PeptideHasOnlyCcsSupportedMod_ValidPeptide_ReturnsCorrectSupport()
        {
            // Act & Assert
            Assert.IsTrue(_support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[MOD1:001]R[MOD4:004]")); // Both CCS supported
            Assert.IsFalse(_support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[MOD2:002]")); // MOD2 not CCS supported
            Assert.IsFalse(_support.PeptideHasOnlyCcsSupportedMod("PEPTIDE[UNKNOWN:999]")); // Unknown mod
            Assert.IsTrue(_support.PeptideHasOnlyCcsSupportedMod("PEPTIDE")); // No mods
        }

        [TestMethod]
        public void PopulatePredictionModificationSupport_EmptyList_DoesNotThrow()
        {
            // Arrange
            var support = new LibraryBuilderModificationSupport(new List<ModificationType>(), new List<ModificationType>(), new List<ModificationType>());

            // Assert
            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void PopulatePredictionModificationSupport_NullList_HandlesGracefully()
        {
            // Arrange
            var support = new LibraryBuilderModificationSupport(null, null, null);

            // Assert
            Assert.IsNotNull(support._predictionSupport);
            Assert.AreEqual(support._predictionSupport.Count, 0);
        }

        [TestMethod]
        public void PeptideHasOnlyMs2SupportedMod_MalformedPeptide_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsTrue(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[INVALID]")); // No valid numeric accession
            Assert.IsTrue(_support.PeptideHasOnlyMs2SupportedMod("PEPTIDE[]")); // Empty brackets
        }
    }
}