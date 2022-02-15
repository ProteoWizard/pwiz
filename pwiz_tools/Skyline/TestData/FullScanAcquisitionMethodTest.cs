/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class FullScanAcquisitionMethodTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTargetedAcquisitionMethod()
        {
            var doc = GetTestDocument(FullScanAcquisitionMethod.Targeted, null);
            var spectrumFilter = new SpectrumFilter(doc, MsDataFilePath.EMPTY, null);
            VerifyFilterPairs(spectrumFilter, 280.66, "ELVIS", "LIVES");
            VerifyFilterPairs(spectrumFilter, 280.18, "QLVIS");
            VerifyFilterPairs(spectrumFilter, 280.19, "KLVIS");
        }

        [TestMethod]
        public void TestPrmAcquisitionMethod()
        {
            foreach (var acquisitionMethod in new[]
                {FullScanAcquisitionMethod.PRM, FullScanAcquisitionMethod.SureQuant})
            {
                var doc = GetTestDocument(acquisitionMethod, null);
                var spectrumFilter = new SpectrumFilter(doc, MsDataFilePath.EMPTY, null);
                VerifyFilterPairs(spectrumFilter, 280.66, "ELVIS", "LIVES");
                VerifyFilterPairs(spectrumFilter, 280.18, "QLVIS", "KLVIS");
                VerifyFilterPairs(spectrumFilter, 280.19, "QLVIS", "KLVIS");
            }
        }

        private void VerifyFilterPairs(SpectrumFilter spectrumFilter, double precursorMz, params string[] expectedTargets)
        {
            var msDataSpectrum = new MsDataSpectrum()
            {
                Precursors = ImmutableList<MsPrecursor>.Singleton(new MsPrecursor {PrecursorMz = new SignedMz(precursorMz)}),
                Level = 2,
            };
            var extractedSpectra = spectrumFilter.Extract(0, new[] {msDataSpectrum}).ToList();
            var matchedTargets = extractedSpectra.Select(spectrum => spectrum.Target.ToString()).ToList();
            CollectionAssert.AreEquivalent(expectedTargets, matchedTargets);
        }

        private SrmDocument GetTestDocument(FullScanAcquisitionMethod acquisitionMethod, IsolationScheme isolationScheme)
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(new StringReader(DOCUMENT_XML));
            doc = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                fullScan.ChangeAcquisitionMethod(acquisitionMethod, isolationScheme)));
            return doc;
        }

        private static string DOCUMENT_XML = @"<srm_settings format_version='21.1'>
  <settings_summary name='Default'>
    <peptide_settings>
      <enzyme name='Trypsin' cut='KR' no_cut='P' sense='C' />
    </peptide_settings>
    <transition_settings>
      <transition_prediction precursor_mass_type='Monoisotopic' fragment_mass_type='Monoisotopic' optimize_by='None' />
      <transition_filter precursor_charges='2' product_charges='1' precursor_adducts='[M+]' product_adducts='[M-]' fragment_types='p,y' small_molecule_fragment_types='f,p' fragment_range_first='m/z &gt; precursor' fragment_range_last='3 ions' precursor_mz_window='0' auto_select='true' />
      <transition_instrument min_mz='150' max_mz='2000' mz_match_tolerance='0.055' />
      <transition_full_scan acquisition_method='Targeted' product_mass_analyzer='orbitrap' product_res='7500' product_res_mz='400' precursor_isotopes='Count' precursor_isotope_filter='2' precursor_mass_analyzer='orbitrap' precursor_res='200000' precursor_res_mz='400'/>
    </transition_settings>
  </settings_summary>
  <peptide_list label_name='peptides1' websearch_status='X#Upeptides1' auto_manage_children='false'>
    <peptide sequence='ELVIS' modified_sequence='ELVIS' calc_neutral_pep_mass='559.321728' num_missed_cleavages='0'>
      <precursor charge='2' calc_neutral_mass='559.321728' precursor_mz='280.66814' collision_energy='0' modified_sequence='ELVIS'>
        <transition fragment_type='precursor' isotope_dist_rank='1' isotope_dist_proportion='0.72684294'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>280.66814</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='precursor' mass_index='1' isotope_dist_rank='2' isotope_dist_proportion='0.201618314'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>281.169662</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='4' calc_neutral_mass='430.279135' product_charge='1' cleavage_aa='L' loss_neutral_mass='0'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>431.286411</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='3' calc_neutral_mass='317.195071' product_charge='1' cleavage_aa='V' loss_neutral_mass='0'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>318.202347</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
    </peptide>
    <peptide sequence='LIVES' modified_sequence='LIVES' calc_neutral_pep_mass='559.321728' num_missed_cleavages='0'>
      <precursor charge='2' calc_neutral_mass='559.321728' precursor_mz='280.66814' collision_energy='0' modified_sequence='LIVES'>
        <transition fragment_type='precursor' isotope_dist_rank='1' isotope_dist_proportion='0.72684294'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>280.66814</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='precursor' mass_index='1' isotope_dist_rank='2' isotope_dist_proportion='0.201618314'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>281.169662</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='4' calc_neutral_mass='446.237664' product_charge='1' cleavage_aa='I' loss_neutral_mass='0'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>447.24494</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='3' calc_neutral_mass='333.1536' product_charge='1' cleavage_aa='V' loss_neutral_mass='0'>
          <precursor_mz>280.66814</precursor_mz>
          <product_mz>334.160876</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
    </peptide>
    <peptide sequence='KLVIS' modified_sequence='KLVIS' calc_neutral_pep_mass='558.374098' num_missed_cleavages='1'>
      <precursor charge='2' calc_neutral_mass='558.374098' precursor_mz='280.194325' collision_energy='0' modified_sequence='KLVIS'>
        <transition fragment_type='precursor' isotope_dist_rank='1' isotope_dist_proportion='0.719293058'>
          <precursor_mz>280.194325</precursor_mz>
          <product_mz>280.194325</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='precursor' mass_index='1' isotope_dist_rank='2' isotope_dist_proportion='0.2068601'>
          <precursor_mz>280.194325</precursor_mz>
          <product_mz>280.69582</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='4' calc_neutral_mass='430.279135' product_charge='1' cleavage_aa='L' loss_neutral_mass='0'>
          <precursor_mz>280.194325</precursor_mz>
          <product_mz>431.286411</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='3' calc_neutral_mass='317.195071' product_charge='1' cleavage_aa='V' loss_neutral_mass='0'>
          <precursor_mz>280.194325</precursor_mz>
          <product_mz>318.202347</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
    </peptide>
    <peptide sequence='QLVIS' modified_sequence='QLVIS' calc_neutral_pep_mass='558.337713' num_missed_cleavages='0'>
      <precursor charge='2' calc_neutral_mass='558.337713' precursor_mz='280.176132' collision_energy='0' modified_sequence='QLVIS'>
        <transition fragment_type='precursor' isotope_dist_rank='1' isotope_dist_proportion='0.7258393'>
          <precursor_mz>280.176132</precursor_mz>
          <product_mz>280.176132</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='precursor' mass_index='1' isotope_dist_rank='2' isotope_dist_proportion='0.201064155'>
          <precursor_mz>280.176132</precursor_mz>
          <product_mz>280.677618</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='4' calc_neutral_mass='430.279135' product_charge='1' cleavage_aa='L' loss_neutral_mass='0'>
          <precursor_mz>280.176132</precursor_mz>
          <product_mz>431.286411</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
        <transition fragment_type='y' fragment_ordinal='3' calc_neutral_mass='317.195071' product_charge='1' cleavage_aa='V' loss_neutral_mass='0'>
          <precursor_mz>280.176132</precursor_mz>
          <product_mz>318.202347</product_mz>
          <collision_energy>0</collision_energy>
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";
    }
}
