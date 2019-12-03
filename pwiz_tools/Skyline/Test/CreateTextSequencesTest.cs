/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CreateTextSequencesTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that amino acids that have both heavy and static modifications get displayed in the heavy modification color.
        /// </summary>
        [TestMethod]
        public void TestStaticAndHeavyTextSequences()
        {
            var document =
                (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(
                    new StringReader(STATIC_AND_HEAVY_MODIFICATIONS_DOCUMENT));
            var modFontHolder = new ModFontHolder(new Control());
            var oxidizedPeptide = document.Molecules.First();
            VerifyTextSequences(PeptideTreeNode.CreateTextSequences(oxidizedPeptide, document.Settings, "MPEPTIDE", null, modFontHolder),
                Tuple.Create("M", FontStyle.Bold | FontStyle.Underline, Color.Blue),
                Tuple.Create("PEPTIDE", FontStyle.Bold, Color.Blue)
            );

            var peptide = document.Molecules.Skip(1).First();
            VerifyTextSequences(PeptideTreeNode.CreateTextSequences(peptide, document.Settings, "MPEPTIDE", null, modFontHolder),
                Tuple.Create("MPEPTIDE", FontStyle.Bold, Color.Blue));
        }

        /// <summary>
        /// Verifies that neutral-loss only modifications do not get displayed
        /// </summary>
        [TestMethod]
        public void TestNeutralLossTextSequences()
        {
            var document =
                (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(
                    new StringReader(NEUTRAL_LOSS_DOCUMENT));
            var peptideDocNode = document.Molecules.First();
            var modFontHolder = new ModFontHolder(new Control());
            var textSequences = PeptideTreeNode.CreateTextSequences(peptideDocNode, document.Settings, "MGFGGTLEIK", null, modFontHolder);
            VerifyTextSequences(textSequences,
                Tuple.Create("M", FontStyle.Bold | FontStyle.Underline, Color.Black),
                Tuple.Create("GFGGTLEIK", FontStyle.Regular, Color.Black)
            );
        }

        private void VerifyTextSequences(IList<TextSequence> textSequences,
            params Tuple<string, FontStyle, Color>[] expected)
        {
            Assert.AreEqual(textSequences.Count, expected.Length);
            for (int i = 0; i < textSequences.Count; i++)
            {
                string message = "Mismatch at position " + i;
                Assert.AreEqual(expected[i].Item1, textSequences[i].Text, message);
                Assert.AreEqual(expected[i].Item2, textSequences[i].Font.Style, message);
                Assert.AreEqual(expected[i].Item3, textSequences[i].Color, message);
            }
        }

        private const string NEUTRAL_LOSS_DOCUMENT = @"<srm_settings format_version='4.22' software_version='Skyline 19.1'>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='2'>
        <static_modifications>
          <static_modification name='Carbamidomethyl (C)' aminoacid='C' formula='H3C2NO' unimod_id='4' short_name='CAM' />
          <static_modification name='Oxidation (M)' aminoacid='M' variable='true' formula='O' unimod_id='35' short_name='Oxi'>
            <potential_loss formula='H4COS' massdiff_monoisotopic='63.998285' massdiff_average='64.10701' />
          </static_modification>
          <static_modification name='Water Loss (D, E, S, T)' aminoacid='D, E, S, T'>
            <potential_loss formula='H2O' massdiff_monoisotopic='18.010565' massdiff_average='18.01528' />
          </static_modification>
          <static_modification name='Ammonia Loss (K, N, Q, R)' aminoacid='K, N, Q, R'>
            <potential_loss formula='NH3' massdiff_monoisotopic='17.026549' massdiff_average='17.03052' />
          </static_modification>
        </static_modifications>
        <heavy_modifications />
      </peptide_modifications>
    </peptide_settings>
  </settings_summary>
  <peptide_list label_name='Q15436|SC23A_HUMAN' websearch_status='X#UQ15436' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='MGFGGTLEIK' modified_sequence='M[+15.994915]GFGGTLEIK' calc_neutral_pep_mass='1067.532132' num_missed_cleavages='0' rt_calculator_score='31.3301'>
      <variable_modifications>
        <variable_modification index_aa='0' modification_name='Oxidation (M)' mass_diff='+16' />
      </variable_modifications>
    </peptide>
  </peptide_list>
</srm_settings>";

        private const string STATIC_AND_HEAVY_MODIFICATIONS_DOCUMENT =
            @"<srm_settings format_version='4.22' software_version='Skyline 19.1'>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl (C)' aminoacid='C' formula='H3C2NO' unimod_id='4' short_name='CAM' />
          <static_modification name='Oxidation (M)' aminoacid='M' variable='true' formula='O' unimod_id='35' short_name='Oxi'>
            <potential_loss formula='H4COS' massdiff_monoisotopic='63.998285' massdiff_average='64.10701' />
          </static_modification>
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:15N' label_15N='true' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
  </settings_summary>
  <peptide_list label_name='peptides1' websearch_status='X#Upeptides1' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='MPEPTIDE' modified_sequence='M[+15.994915]PEPTIDE' calc_neutral_pep_mass='946.395364' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='0' modification_name='Oxidation (M)' mass_diff='+16' />
        </explicit_static_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='0' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='1' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='2' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='3' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='4' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='5' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='6' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='7' modification_name='Label:15N' mass_diff='+1' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='1' calc_neutral_mass='946.395364' precursor_mz='947.40264' auto_manage_children='false' collision_energy='0' modified_sequence='M[+16.0]PEPTIDE' />
      <precursor charge='1' isotope_label='heavy' calc_neutral_mass='954.371644' precursor_mz='955.37892' auto_manage_children='false' collision_energy='0' modified_sequence='M[+17.0]P[+1.0]E[+1.0]P[+1.0]T[+1.0]I[+1.0]D[+1.0]E[+1.0]' />
    </peptide>
    <peptide auto_manage_children='false' sequence='MPEPTIDE' modified_sequence='MPEPTIDE' calc_neutral_pep_mass='930.400449' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='0' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='1' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='2' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='3' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='4' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='5' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='6' modification_name='Label:15N' mass_diff='+1' />
          <implicit_modification index_aa='7' modification_name='Label:15N' mass_diff='+1' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='1' calc_neutral_mass='930.400449' precursor_mz='931.407725' auto_manage_children='false' collision_energy='0' modified_sequence='MPEPTIDE' />
      <precursor charge='1' isotope_label='heavy' calc_neutral_mass='938.376729' precursor_mz='939.384005' auto_manage_children='false' collision_energy='0' modified_sequence='M[+1.0]P[+1.0]E[+1.0]P[+1.0]T[+1.0]I[+1.0]D[+1.0]E[+1.0]' />
    </peptide>
  </peptide_list>
</srm_settings>";
    }
}
