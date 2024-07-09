/*
 * Original author: Lucia Espona <espona .at. imsb.biol.ethz.ch>,
 *                  IMSB, ETHZ
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class DecoysTest : AbstractUnitTest
    {
        [TestMethod]
        public void GenerateDecoysTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"Test\DecoysTest.zip");

            string docPath = TestFilesDir.GetTestPath("SimpleDecoys.sky");
            SrmDocument simpleDecoysDoc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(simpleDecoysDoc, 0, 1, 18, 18, 56);

            // First check right number of decoy peptide groups and transtions are generated
            var refineSettings = new RefinementSettings();
            int numDecoys = simpleDecoysDoc.PeptideCount;
            var decoysDoc = refineSettings.GenerateDecoys(simpleDecoysDoc, numDecoys, DecoyGeneration.ADD_RANDOM);
            ValidateDecoys(simpleDecoysDoc, decoysDoc, false);

            // Second call to generate decoys to make sure that it removes the original Decoys group and generates a completely new one.
            var newDecoysDoc = refineSettings.GenerateDecoys(decoysDoc, numDecoys, DecoyGeneration.ADD_RANDOM);
            AssertEx.Cloned(decoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy),
                newDecoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy));

            // MS1 document with precursors and variable modifications, shuffled
            docPath = TestFilesDir.GetTestPath("Ms1FilterTutorial.sky");
            SrmDocument variableDecoysDoc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(variableDecoysDoc, 0, 11, 50, 51, 153);
            numDecoys = variableDecoysDoc.PeptideCount;
            var decoysVariableShuffle = refineSettings.GenerateDecoys(variableDecoysDoc, numDecoys, DecoyGeneration.SHUFFLE_SEQUENCE);
            ValidateDecoys(variableDecoysDoc, decoysVariableShuffle, true);

            // Document with explicit modifications, reversed
            SrmDocument docStudy7 = ResultsUtil.DeserializeDocument("Study7.sky", GetType());
            AssertEx.IsDocumentState(docStudy7, 0, 7, 11, 22, 66);
            numDecoys = docStudy7.PeptideCount;
            var decoysExplicitReverse = refineSettings.GenerateDecoys(docStudy7, numDecoys, DecoyGeneration.REVERSE_SEQUENCE);
            ValidateDecoys(docStudy7, decoysExplicitReverse, true);

            // Random mass shifts with precursors (used to throw an exception due to bad range check that assumed product ions only)
            while (true)
            {
                // As this is random we may need to make a few attempts before we get the values we need
                SrmDocument doc = ResultsUtil.DeserializeDocument(TestFilesDir.GetTestPath("precursor_decoy_test.sky"));
                AssertEx.IsDocumentState(doc, null, 1, 1, 2, 6);
                numDecoys = 2;
                var decoysRandom = refineSettings.GenerateDecoys(doc, numDecoys, DecoyGeneration.ADD_RANDOM);
                // Verify that we successfully set a precursor transition decoy mass outside the allowed range of product transitions
                if (decoysRandom.PeptideTransitions.Any(x => x.IsDecoy &&
                                                    (x.Transition.DecoyMassShift > Transition.MAX_PRODUCT_DECOY_MASS_SHIFT ||
                                                     x.Transition.DecoyMassShift < Transition.MIN_PRODUCT_DECOY_MASS_SHIFT)))
                    break;
            }
        }

        private static void ValidateDecoys(SrmDocument document, SrmDocument decoysDoc, bool modifiesSequences)
        {
            AssertEx.IsDocumentState(decoysDoc, 1, document.PeptideGroupCount + 1, document.PeptideCount*2,
                document.PeptideTransitionGroupCount*2, document.PeptideTransitionCount*2);

            // Check for the existence of the Decoys peptide group and that everything under it is marked as a decoy. 
            var nodePeptideGroupDecoy = decoysDoc.PeptideGroups.Single(nodePeptideGroup => nodePeptideGroup.IsDecoy);
            var dictModsToPep = document.Peptides.ToDictionary(nodePep => nodePep.ModifiedTarget);
            foreach (var nodePep in nodePeptideGroupDecoy.Peptides)
            {
                Assert.AreEqual(true, nodePep.IsDecoy);
                PeptideDocNode nodePepSource = null;
                if (!modifiesSequences)
                    Assert.IsNull(nodePep.SourceKey);
                else
                {
                    Assert.IsNotNull(nodePep.SourceKey, string.Format("Source key for {0}{1} is null", nodePep.ModifiedSequence,
                        nodePep.IsDecoy ? " - decoy" : string.Empty));
                    Assert.IsTrue(FastaSequence.IsExSequence(nodePep.SourceKey.Sequence));
                    Assert.AreEqual(nodePep.SourceKey.ModifiedSequence,
                        SequenceMassCalc.NormalizeModifiedSequence(nodePep.SourceKey.ModifiedSequence));
                    if (nodePep.HasExplicitMods)
                        Assert.IsNotNull(nodePep.SourceKey.ExplicitMods);
                    Assert.IsTrue(dictModsToPep.TryGetValue(nodePep.SourceModifiedTarget, out nodePepSource));
                    var sourceKey = new ModifiedSequenceMods(nodePepSource.Peptide.Sequence, nodePepSource.ExplicitMods);
                    Assert.AreEqual(sourceKey.ExplicitMods, nodePep.SourceExplicitMods);
                }
                for (int i = 0; i < nodePep.TransitionGroupCount; i++)
                {
                    var nodeGroup = nodePep.TransitionGroups.ElementAt(i);
                    Assert.AreEqual(true, nodeGroup.IsDecoy);
                    TransitionGroupDocNode nodeGroupSource = null;
                    double shift = SequenceMassCalc.GetPeptideInterval(nodeGroup.TransitionGroup.DecoyMassShift);
                    if (nodePepSource != null && nodeGroup.TransitionGroup.DecoyMassShift.HasValue)
                    {
                        nodeGroupSource = nodePepSource.TransitionGroups.ElementAt(i);
                        Assert.AreEqual(nodeGroupSource.PrecursorMz + shift, nodeGroup.PrecursorMz, SequenceMassCalc.MassTolerance);
                    }
                    for (int j = 0; j < nodeGroup.TransitionCount; j++)
                    {
                        var nodeTran = nodeGroup.Transitions.ElementAt(j);
                        Assert.IsTrue(nodeTran.IsDecoy);
                        if (nodeTran.Transition.IsPrecursor())
                        {
                            Assert.AreEqual(nodeGroup.TransitionGroup.DecoyMassShift, nodeTran.Transition.DecoyMassShift);
                            if (nodeGroupSource != null)
                            {
                                Assert.AreEqual(nodeGroupSource.Transitions.ElementAt(j).Mz + shift, nodeTran.Mz, SequenceMassCalc.MassTolerance);
                            }
                        }
                    }
                }
            }

            // Check that the resulting document persists correctly by passing the SrmDocument to AssertEx.IsSerializable().
            AssertEx.Serializable(decoysDoc);
        }

        /// <summary>
        /// Tests that creating decoys works even when the precursor resolution is high
        /// </summary>
        [TestMethod]
        public void TestHighResGenerateDecoys()
        {
            var originalDocument = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(new StringReader(strHighResDoc));
            var refinementSettings = new RefinementSettings
            {
                DecoysMethod = DecoyGeneration.SHUFFLE_SEQUENCE,
                NumberOfDecoys = 1
            };
            var docWithDecoys = refinementSettings.GenerateDecoys(originalDocument);
            var targetPeptide = docWithDecoys.Peptides.ElementAt(0);
            Assert.IsFalse(targetPeptide.IsDecoy);
            var decoyPeptide = docWithDecoys.Peptides.ElementAt(1);
            Assert.IsTrue(decoyPeptide.IsDecoy);
            Assert.AreEqual(targetPeptide.TransitionGroupCount, decoyPeptide.TransitionGroupCount);
            for (int iTransitionGroup = 0; iTransitionGroup < targetPeptide.TransitionGroupCount; iTransitionGroup++)
            {
                var targetTransitionGroup = targetPeptide.TransitionGroups.ElementAt(iTransitionGroup);
                var decoyTransitionGroup = decoyPeptide.TransitionGroups.ElementAt(iTransitionGroup);
                Assert.AreEqual(targetTransitionGroup.TransitionCount, decoyTransitionGroup.TransitionCount);
                for (int iTransition = 0; iTransition < targetTransitionGroup.TransitionCount; iTransition++)
                {
                    var targetTransition = targetTransitionGroup.Transitions.ElementAt(iTransition);
                    var decoyTransition = decoyTransitionGroup.Transitions.ElementAt(iTransition);
                    // Verify that the isotope distribution proportion for the decoy and target transitions are similar
                    Assert.AreEqual(targetTransition.IsotopeDistInfo.Proportion, decoyTransition.IsotopeDistInfo.Proportion, .001);
                }
            }
        }

        const string strHighResDoc = @"<srm_settings>
  <settings_summary name=""Default"">
    <peptide_settings>
      <peptide_modifications max_variable_mods=""3"" max_neutral_losses=""1"">
        <static_modifications>
          <static_modification name=""Carbamidomethyl (C)"" aminoacid=""C"" formula=""H3C2NO"" unimod_id=""4"" short_name=""CAM"" />
        </static_modifications>
        <heavy_modifications>
          <static_modification name=""Label:13C(6)15N(2) (K)"" aminoacid=""K"" label_13C=""true"" label_15N=""true"" unimod_id=""259"" short_name=""+08"" />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
    <transition_settings>
      <transition_prediction precursor_mass_type=""Monoisotopic"" fragment_mass_type=""Monoisotopic"" optimize_by=""None"" />
      <transition_filter precursor_charges=""2,3"" product_charges=""1"" precursor_adducts=""[M+H]"" product_adducts=""[M+]"" fragment_types=""y,b,p"" small_molecule_fragment_types=""f,p"" fragment_range_first=""m/z &gt; precursor"" fragment_range_last=""last ion"" precursor_mz_window=""0"" auto_select=""true"">
        <measured_ion name=""N-terminal to Proline"" cut=""P"" sense=""N"" min_length=""3"" />
      </transition_filter>
      <transition_instrument min_mz=""50"" max_mz=""2000"" mz_match_tolerance=""0.055"" />
      <transition_full_scan acquisition_method=""DDA"" product_mass_analyzer=""orbitrap"" product_res=""30000"" product_res_mz=""400"" precursor_isotopes=""Count"" precursor_isotope_filter=""3"" precursor_mass_analyzer=""orbitrap"" precursor_res=""120000"" precursor_res_mz=""400"" selective_extraction=""true"" retention_time_filter_type=""scheduling_windows"" retention_time_filter_length=""5""/>
    </transition_settings>
  </settings_summary>
  <peptide_list label_name=""CXL14_HUMAN"" auto_manage_children=""false"">
    <peptide auto_manage_children=""false"" sequence=""MVIITTK"" modified_sequence=""MVIITTK"" calc_neutral_pep_mass=""804.477911"" num_missed_cleavages=""0"">
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa=""6"" modification_name=""Label:13C(6)15N(2) (K)"" mass_diff=""+8"" />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge=""3"" calc_neutral_mass=""804.477911"" precursor_mz=""269.16658"" auto_manage_children=""false"" collision_energy=""0"" modified_sequence=""MVIITTK"">
        <bibliospec_spectrum_info library_name=""Spikemix"" count_measured=""1"" />
        <transition fragment_type=""precursor"" isotope_dist_rank=""1"" isotope_dist_proportion=""0.6024983"">
          <precursor_mz>269.16658</precursor_mz>
          <product_mz>269.16658</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type=""precursor"" mass_index=""1"" isotope_dist_rank=""2"" isotope_dist_proportion=""0.239984557"">
          <precursor_mz>269.16658</precursor_mz>
          <product_mz>269.500893</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type=""precursor"" mass_index=""2"" isotope_dist_rank=""3"" isotope_dist_proportion=""0.021506371"">
          <precursor_mz>269.16658</precursor_mz>
          <product_mz>269.834213</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
      <precursor charge=""3"" isotope_label=""heavy"" calc_neutral_mass=""812.49211"" precursor_mz=""271.837979"" auto_manage_children=""false"" collision_energy=""0"" modified_sequence=""MVIITTK[+8.0]"">
        <bibliospec_spectrum_info library_name=""Spikemix"" count_measured=""1"" />
        <transition fragment_type=""precursor"" isotope_dist_rank=""1"" isotope_dist_proportion=""0.628923059"">
          <precursor_mz>271.837979</precursor_mz>
          <product_mz>271.837979</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type=""precursor"" mass_index=""1"" isotope_dist_rank=""2"" isotope_dist_proportion=""0.20846796"">
          <precursor_mz>271.837979</precursor_mz>
          <product_mz>272.172296</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type=""precursor"" mass_index=""2"" isotope_dist_rank=""3"" isotope_dist_proportion=""0.0193446446"">
          <precursor_mz>271.837979</precursor_mz>
          <product_mz>272.505404</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";
    }
}