/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
    [TestClass]
    public class ModifiedSequenceTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that the peptide with the heavy form "C[+57]PEPTIDER[+10]" works correctly.
        /// </summary>
        [TestMethod]
        public void TestLabeledPrecursorWithImplicitMod()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(new StringReader(DOC_CPETIDER));
            AssertEx.VerifyModifiedSequences(doc);
        }

        const string DOC_CPETIDER = @"<srm_settings format_version='3.73' software_version='Skyline (64-bit) '>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl Cysteine' aminoacid='C' formula='C2H3ON' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6)15N(4) (R)' aminoacid='R' label_13C='true' label_15N='true' unimod_id='267' short_name='+10' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
  </settings_summary>
  <peptide_list label_name='Library Peptides' websearch_status='X' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='CPEPTIDER' modified_sequence='C[+57.0]PEPTIDER' calc_neutral_pep_mass='1115.491724' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='0' modification_name='Carbamidomethyl Cysteine' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6)15N(4) (R)' mass_diff='+10' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1115.491724' precursor_mz='558.753138' collision_energy='27.583929' modified_sequence='C[+57.0]PEPTIDER'>
        <transition fragment_type='a' fragment_ordinal='3' calc_neutral_mass='358.131091' product_charge='1' cleavage_aa='E' loss_neutral_mass='0'>
          <precursor_mz>558.753138</precursor_mz>
          <product_mz>359.138367</product_mz>
          <collision_energy>27.583929</collision_energy>
        </transition>
      </precursor>
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1125.499993' precursor_mz='563.757272' collision_energy='27.583929' modified_sequence='C[+57.0]PEPTIDER[+10.0]'>
        <transition fragment_type='a' fragment_ordinal='3' calc_neutral_mass='358.131091' product_charge='1' cleavage_aa='E' loss_neutral_mass='0'>
          <precursor_mz>563.757272</precursor_mz>
          <product_mz>359.138367</product_mz>
          <collision_energy>27.583929</collision_energy>
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";

        /// <summary>
        /// Verifies that the peptide "A[+56.0]R[+28.0]TK[+56.0]QTAR[+10.0]" works correctly.
        /// Makes sure that the [+10.0] only gets applied to the Argenine that has the explicit modification on it.
        /// </summary>
        [TestMethod]
        public void TestExplicitAndHeavyModifications()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(new StringReader(DOC_ARTKQTAR));
            AssertEx.VerifyModifiedSequences(doc);
        }

        private const string DOC_ARTKQTAR = @"<srm_settings format_version='3.73' software_version='Skyline (64-bit) '>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Dimethyl (R)' aminoacid='R' variable='true' formula='C2H4' unimod_id='36' short_name='2Me' />
          <static_modification name='PropionylNT' terminus='N' formula='C3H4O1' explicit_decl='true' />
          <static_modification name='Propionylation' aminoacid='K' formula='C3H4O1' explicit_decl='true' />
          <static_modification name='UbiqPropStub' aminoacid='K' formula='C7H10O3N2' explicit_decl='true' />
          <static_modification name='Dimethyl' aminoacid='K' formula='C2H4' explicit_decl='true' />
          <static_modification name='Acetyl' formula='C2O1H2' explicit_decl='true' />
          <static_modification name='Trimethyl (K)' aminoacid='K' formula='H6C3' explicit_decl='true' />
          <static_modification name='MethylPropionyl' aminoacid='K' formula='C4H6O' explicit_decl='true' />
          <static_modification name='DoublePropionyl(NTK)' aminoacid='K' terminus='N' formula='C6H8O2' explicit_decl='true' />
          <static_modification name='NTPropUbLys' aminoacid='K' terminus='N' formula='C10H14O4N2' explicit_decl='true' />
          <static_modification name='PropionylNT_LysAc1' aminoacid='K' terminus='N' formula='C5H6O2' explicit_decl='true' />
          <static_modification name='PropionylNT_LysMe1' aminoacid='K' terminus='N' formula='C7O2H10' explicit_decl='true' />
          <static_modification name='PropionylNT_LysMe2' aminoacid='K' formula='C5H8O' explicit_decl='true' />
          <static_modification name='PropionylNY_LysMe3' aminoacid='K' terminus='N' formula='C6H10O' explicit_decl='true' />
          <static_modification name='Phospho' formula='PO3H1' explicit_decl='true' />
          <static_modification name='PropionylPhospo' formula='PO4H5C3' explicit_decl='true' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6)15N(4) (R)' aminoacid='R' label_13C='true' label_15N='true' unimod_id='267' short_name='+10' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
  </settings_summary>
  <peptide_list label_name='Library Peptides' websearch_status='X' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='ARTKQTAR' modified_sequence='A[+56.0]R[+28.0]TK[+56.0]QTAR' calc_neutral_pep_mass='1070.619642' num_missed_cleavages='1'>
      <variable_modifications>
        <variable_modification index_aa='0' modification_name='PropionylNT' mass_diff='+56' />
        <variable_modification index_aa='1' modification_name='Dimethyl (R)' mass_diff='+28' />
        <variable_modification index_aa='3' modification_name='Propionylation' mass_diff='+56' />
      </variable_modifications>
      <explicit_modifications>
        <explicit_heavy_modifications>
          <explicit_modification index_aa='7' modification_name='Label:13C(6)15N(4) (R)' mass_diff='+10' />
        </explicit_heavy_modifications>
      </explicit_modifications>
      <precursor charge='2' calc_neutral_mass='1070.619642' precursor_mz='536.317097' collision_energy='26.305075' modified_sequence='A[+56.0]R[+28.0]TK[+56.0]QTAR'>
        <transition fragment_type='y' fragment_ordinal='6' calc_neutral_mass='759.423902' product_charge='1' cleavage_aa='T' loss_neutral_mass='0'>
          <precursor_mz>536.317097</precursor_mz>
          <product_mz>760.431178</product_mz>
        </transition>
      </precursor>
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1080.627911' precursor_mz='541.321232' collision_energy='26.305075' modified_sequence='A[+56.0]R[+28.0]TK[+56.0]QTAR[+10.0]'>
        <transition fragment_type='y' fragment_ordinal='6' calc_neutral_mass='769.432171' product_charge='1' cleavage_aa='T' loss_neutral_mass='0'>
          <precursor_mz>541.321232</precursor_mz>
          <product_mz>770.439447</product_mz>
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";

        /// <summary>
        /// Tests that the peptide sequence "A[+56.0]GLQFPVGR[+10.0]" works.
        /// </summary>
        [TestMethod]
        public void TestVariableAndExplicitHeavyMod()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(new StringReader(DOC_AGLQFPVGR));
            AssertEx.VerifyModifiedSequences(doc);
        }

        private const string DOC_AGLQFPVGR = @"<srm_settings format_version='3.73' software_version='Skyline (64-bit) '>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl Cysteine' aminoacid='C' formula='C2H3ON' />
          <static_modification name='PropionylNT' terminus='N' variable='true' formula='C3H4O1' />
          <static_modification name='Dimethyl (R)' aminoacid='R' variable='true' formula='C2H4' unimod_id='36' short_name='2Me' />
          <static_modification name='Propionylation' aminoacid='K' variable='true' formula='C3H4O1' />
          <static_modification name='UbiqPropStub' aminoacid='K' variable='true' formula='C7H10O3N2' />
          <static_modification name='Dimethyl' aminoacid='K' variable='true' formula='C2H4' />
          <static_modification name='Trimethyl (K)' aminoacid='K' variable='true' formula='H6C3' />
          <static_modification name='MethylPropionyl' aminoacid='K' variable='true' formula='C4H6O' />
          <static_modification name='DoublePropionyl(NTK)' aminoacid='K' terminus='N' variable='true' formula='C6H8O2' />
          <static_modification name='PropionylNT_LysMe1' aminoacid='K' terminus='N' variable='true' formula='C7O2H10' />
          <static_modification name='NTPropUbLys' aminoacid='K' terminus='N' variable='true' formula='C10H14O4N2' />
          <static_modification name='PropionylNT_LysMe2' aminoacid='K' variable='true' formula='C5H8O' />
          <static_modification name='PropionylNT_LysAc1' aminoacid='K' terminus='N' variable='true' formula='C5H6O2' />
          <static_modification name='PropionylNY_LysMe3' aminoacid='K' terminus='N' variable='true' formula='C6H10O' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6)15N(4) (R)' aminoacid='R' label_13C='true' label_15N='true' unimod_id='267' short_name='+10' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
    <transition_settings>
    </transition_settings>
    <data_settings document_guid='c6b7dd1f-54e5-415f-96c6-3d1912b6a171'>
    </data_settings>
  </settings_summary>
  <peptide_list label_name='Library Peptides' websearch_status='X' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='AGLQFPVGR' modified_sequence='A[+56.0]GLQFPVGR' calc_neutral_pep_mass='999.550165' num_missed_cleavages='0'>
      <variable_modifications>
        <variable_modification index_aa='0' modification_name='PropionylNT' mass_diff='+56' />
      </variable_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6)15N(4) (R)' mass_diff='+10' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='999.550165' precursor_mz='500.782359' collision_energy='24.279594' modified_sequence='A[+56.0]GLQFPVGR'>
        <transition fragment_type='y' fragment_ordinal='6' calc_neutral_mass='702.381309' product_charge='1' cleavage_aa='Q' loss_neutral_mass='0'>
          <precursor_mz>500.782359</precursor_mz>
          <product_mz>703.388585</product_mz>
          <collision_energy>24.279594</collision_energy>
          <transition_lib_info rank='3' intensity='74.92' />
        </transition>
      </precursor>
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1009.558434' precursor_mz='505.786493' collision_energy='24.279594' modified_sequence='A[+56.0]GLQFPVGR[+10.0]'>
        <transition fragment_type='y' fragment_ordinal='6' calc_neutral_mass='712.389578' product_charge='1' cleavage_aa='Q' loss_neutral_mass='0'>
          <precursor_mz>505.786493</precursor_mz>
          <product_mz>713.396854</product_mz>
          <collision_energy>24.279594</collision_energy>
          <transition_lib_info rank='3' intensity='74.92' />
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";

        [TestMethod]
        public void TestExplicitStaticImplicitHeavy()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(
                new StringReader(DOC_AGLQFPVGR_EXPLICITSTATIC));
            AssertEx.VerifyModifiedSequences(doc);
        }

        private const string DOC_AGLQFPVGR_EXPLICITSTATIC =
            @"<srm_settings format_version='3.73' software_version='Skyline (64-bit) '>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl Cysteine' aminoacid='C' formula='C2H3ON' />
          <static_modification name='PropionylNT' terminus='N' formula='C3H4O1' />
          <static_modification name='Propionylation' aminoacid='K' formula='C3H4O1' />
          <static_modification name='MethylPropionyl' aminoacid='K' formula='C4H6O' />
          <static_modification name='Dimethyl' aminoacid='K' formula='C2H4' />
          <static_modification name='Trimethyl (K)' aminoacid='K' formula='H6C3' />
          <static_modification name='Acetyl' formula='C2O1H2' />
          <static_modification name='DoublePropionyl(NTK)' aminoacid='K' terminus='N' formula='C6H8O2' />
          <static_modification name='PropionylNT_LysMe1' aminoacid='K' terminus='N' formula='C7O2H10' />
          <static_modification name='PropionylNT_LysMe2' aminoacid='K' formula='C5H8O' />
          <static_modification name='PropionylNY_LysMe3' aminoacid='K' terminus='N' formula='C6H10O' />
          <static_modification name='PropionylNT_LysAc1' aminoacid='K' terminus='N' formula='C5H6O2' />
          <static_modification name='Phospho' formula='PO3H1' />
          <static_modification name='NTPropUbLys' aminoacid='K' terminus='N' formula='C10H14O4N2' />
          <static_modification name='UbiqPropStub' aminoacid='K' formula='C7H10O3N2' />
          <static_modification name='Dimethyl (R)' aminoacid='R' variable='true' formula='C2H4' unimod_id='36' short_name='2Me' />
          <static_modification name='PropionylPhospo' formula='PO4H5C3' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6)15N(4) (C-term R)' aminoacid='R' terminus='C' label_13C='true' label_15N='true' />
        </heavy_modifications>
      </peptide_modifications>
      <quantification weighting='none' fit='linear_through_zero' normalization='none' />
    </peptide_settings>
    <transition_settings>
    </transition_settings>
    <data_settings document_guid='355c410e-9e13-4241-a9c6-e678162b346c'>
    </data_settings>
  </settings_summary>
  <peptide_list label_name='Library Peptides' websearch_status='X' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='AGLQFPVGR' modified_sequence='A[+56.0]GLQFPVGR' calc_neutral_pep_mass='999.550165' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='0' modification_name='PropionylNT' mass_diff='+56' />
        </explicit_static_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6)15N(4) (C-term R)' mass_diff='+10' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1009.558434' precursor_mz='505.786493' auto_manage_children='false' collision_energy='24.279594' modified_sequence='A[+56.0]GLQFPVGR[+10.0]'>
        <transition fragment_type='y' fragment_ordinal='6' calc_neutral_mass='712.389578' product_charge='1' cleavage_aa='Q' loss_neutral_mass='0'>
          <precursor_mz>505.786493</precursor_mz>
          <product_mz>713.396854</product_mz>
          <collision_energy>24.279594</collision_energy>
        </transition>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";

        [TestMethod]
        public void TestMultipleExplicitModsPerAminoAcid()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(
                new StringReader(DOC_MULTIPLE_CYSTEINE_MODS));
            AssertEx.VerifyModifiedSequences(doc);
        }

        private const string DOC_MULTIPLE_CYSTEINE_MODS =
            @"<srm_settings format_version='4.11' software_version='Skyline (64-bit) '>
  <settings_summary name='Default'>
    <peptide_settings>
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl (C)' aminoacid='C' formula='H3C2NO' explicit_decl='true' unimod_id='4' short_name='CAM' />
          <static_modification name='TEV' aminoacid='C' formula='C25H42N10O6' explicit_decl='true' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Tev_heavy' aminoacid='C' formula='C&apos;5N&apos;-C5N' explicit_decl='true' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
    <transition_settings>
    </transition_settings>
  </settings_summary>
  <protein name='sp|P12814|ACTN1_HUMAN' description='Alpha-actinin-1 OS=Homo sapiens GN=ACTN1 PE=1 SV=2' accession='P12814' gene='ACTN1' species='Homo sapiens' preferred_name='ACTN1_HUMAN' websearch_status='X' auto_manage_children='false'>
    <sequence>
        MDHYDSQQTN DYMQPEEDWD RDLLLDPAWE KQQRKTFTAW CNSHLRKAGT
        QIENIEEDFR DGLKLMLLLE VISGERLAKP ERGKMRVHKI SNVNKALDFI
        ASKGVKLVSI GAEEIVDGNV KMTLGMIWTI ILRFAIQDIS VEETSAKEGL
        LLWCQRKTAP YKNVNIQNFH ISWKDGLGFC ALIHRHRPEL IDYGKLRKDD
        PLTNLNTAFD VAEKYLDIPK MLDAEDIVGT ARPDEKAIMT YVSSFYHAFS
        GAQKAETAAN RICKVLAVNQ ENEQLMEDYE KLASDLLEWI RRTIPWLENR
        VPENTMHAMQ QKLEDFRDYR RLHKPPKVQE KCQLEINFNT LQTKLRLSNR
        PAFMPSEGRM VSDINNAWGC LEQVEKGYEE WLLNEIRRLE RLDHLAEKFR
        QKASIHEAWT DGKEAMLRQK DYETATLSEI KALLKKHEAF ESDLAAHQDR
        VEQIAAIAQE LNELDYYDSP SVNARCQKIC DQWDNLGALT QKRREALERT
        EKLLETIDQL YLEYAKRAAP FNNWMEGAME DLQDTFIVHT IEEIQGLTTA
        HEQFKATLPD ADKERLAILG IHNEVSKIVQ TYHVNMAGTN PYTTITPQEI
        NGKWDHVRQL VPRRDQALTE EHARQQHNER LRKQFGAQAN VIGPWIQTKM
        EEIGRISIEM HGTLEDQLSH LRQYEKSIVN YKPKIDQLEG DHQLIQEALI
        FDNKHTNYTM EHIRVGWEQL LTTIARTINE VENQILTRDA KGISQEQMNE
        FRASFNHFDR DHSGTLGPEE FKACLISLGY DIGNDPQGEA EFARIMSIVD
        PNRLGVVTFQ AFIDFMSRET ADTDTADQVM ASFKILAGDK NYITMDELRR
        ELPPDQAEYC IARMAPYTGP DSVPGALDYM SFSTALYGES DL</sequence>
    <peptide auto_manage_children='false' sequence='TFTAWCNSHLR' modified_sequence='TFTAWC[+57.021464]NSHLR' start='35' end='46' prev_aa='K' next_aa='K' calc_neutral_pep_mass='1391.640454' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='5' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </explicit_static_modifications>
      </explicit_modifications>
      <precursor charge='3' calc_neutral_mass='1391.640454' precursor_mz='464.887427' auto_manage_children='false' collision_energy='0' modified_sequence='TFTAWC[+57.0]NSHLR' />
    </peptide>
    <peptide auto_manage_children='false' sequence='EGLLLWCQR' modified_sequence='EGLLLWC[+578.328879]QR' start='147' end='156' prev_aa='K' next_aa='K' calc_neutral_pep_mass='1694.903879' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='6' modification_name='TEV' mass_diff='+578.3' />
        </explicit_static_modifications>
        <explicit_heavy_modifications>
          <explicit_modification index_aa='6' modification_name='Tev_heavy' mass_diff='+6' />
        </explicit_heavy_modifications>
      </explicit_modifications>
      <precursor charge='2' calc_neutral_mass='1694.903879' precursor_mz='848.459215' auto_manage_children='false' collision_energy='0' modified_sequence='EGLLLWC[+578.3]QR' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1700.917688' precursor_mz='851.46612' auto_manage_children='false' collision_energy='0' modified_sequence='EGLLLWC[+584.3]QR' />
    </peptide>
  </protein>
</srm_settings>";

        [TestMethod]
        public void TestExplicitHeavyImplicitStatic()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(
                new StringReader(DOC_EXPLICIT_HEAVY_IMPLICIT_STATIC));
            AssertEx.VerifyModifiedSequences(doc);
        }

        private const string DOC_EXPLICIT_HEAVY_IMPLICIT_STATIC = @"<srm_settings format_version='4.2' software_version='Skyline (64-bit) 4.2.0.19072'>
  <settings_summary name='Default'>
    <peptide_settings>
      <enzyme name='Trypsin' cut='KR' no_cut='P' sense='C' />
      <peptide_modifications max_variable_mods='8' max_neutral_losses='2'>
        <static_modifications>
          <static_modification name='Carbamidomethyl (C)' aminoacid='C' formula='H3C2NO' unimod_id='4' short_name='CAM' />
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6)15N(2) (K)' aminoacid='K' label_13C='true' label_15N='true' />
          <static_modification name='Label:13C(6)15N(4) (R)' aminoacid='R' label_13C='true' label_15N='true' unimod_id='267' short_name='+10' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
    <transition_settings>
    </transition_settings>
  </settings_summary>
  <peptide_list label_name='JPTQ6' websearch_status='X#UJPTQ6' auto_manage_children='false'>
    <peptide auto_manage_children='false' sequence='TIDAGCKPYMAPER' modified_sequence='TIDAGC[+57.021464]KPYMAPER' calc_neutral_pep_mass='1607.743598' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_heavy_modifications>
          <explicit_modification index_aa='13' modification_name='Label:13C(6)15N(4) (R)' mass_diff='+10' />
        </explicit_heavy_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='5' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
      </implicit_modifications>
      <precursor charge='3' calc_neutral_mass='1607.743598' precursor_mz='536.921809' collision_energy='27' modified_sequence='TIDAGC[+57.0]KPYMAPER'>
      </precursor>
      <precursor charge='3' isotope_label='heavy' calc_neutral_mass='1617.751867' precursor_mz='540.257898' collision_energy='27' modified_sequence='TIDAGC[+57.0]KPYMAPER[+10.0]'>
      </precursor>
    </peptide>
    <peptide sequence='TMDAGCKPYMAPER' modified_sequence='TMDAGC[+57.021464]KPYMAPER' calc_neutral_pep_mass='1625.700019' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_heavy_modifications>
          <explicit_modification index_aa='13' modification_name='Label:13C(6)15N(4) (R)' mass_diff='+10' />
        </explicit_heavy_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='5' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
      </implicit_modifications>
      <precursor charge='3' calc_neutral_mass='1625.700019' precursor_mz='542.907282' collision_energy='27' modified_sequence='TMDAGC[+57.0]KPYMAPER'>
      </precursor>
      <precursor charge='3' isotope_label='heavy' calc_neutral_mass='1635.708288' precursor_mz='546.243372' collision_energy='27' modified_sequence='TMDAGC[+57.0]KPYMAPER[+10.0]'>
      </precursor>
    </peptide>
  </peptide_list>
</srm_settings>";

        [TestMethod]
        public void TestNeutralLossModifications()
        {
            var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(
                new StringReader(DOC_NEUTRAL_LOSSES));
            var peptideModifiedSequences = doc.Peptides.Select(peptideDocNode =>
                ModifiedSequence.GetModifiedSequence(doc.Settings, peptideDocNode, IsotopeLabelType.light)).ToList();
            var heavyPeptideModifiedSequences = doc.Peptides.Select(peptideDocNode =>
                ModifiedSequence.GetModifiedSequence(doc.Settings, peptideDocNode, IsotopeLabelType.heavy)).ToList();
            var fullNames = peptideModifiedSequences.Select(seq => seq.FullNames).ToList();
            CollectionAssert.AreEqual(new[]
            {
                "PEPTIDEK",
                "PEPT[Phospho (ST)]IDEK",
                "PEPTIDEC[Carbamidomethyl (C)]K",
                "PEPTIDEMC[Carbamidomethyl (C)]K",
                "PEPTIDEM[Oxidation (M)]C[Carbamidomethyl (C)]K",
                "PEPTIDEMK",
                "PEPTIDEM[Oxidation (M)]K",
                "PEPT[Phospho (ST)]IDEM[Oxidation (M)]K",
                "PEPTIDEMC[Carbamidomethyl (C)]R",
                "PEPTIDEM[Oxidation (M)]C[Carbamidomethyl (C)]R",
            }, fullNames);
            var heavyFullNames = heavyPeptideModifiedSequences.Select(seq => seq.FullNames).ToList();
            CollectionAssert.AreEqual(new []
            {
                "PEPTIDEK[Label:13C(6) (K)]",
                "PEPT[Phospho (ST)]IDEK[Label:13C(6) (K)]",
                "PEPTIDEC[Carbamidomethyl (C)]K[Label:13C(6) (K)]",
                "PEPTIDEMC[Carbamidomethyl (C)]K[Label:13C(6) (K)]",
                "PEPTIDEM[Oxidation (M)]C[Carbamidomethyl (C)]K[Label:13C(6) (K)]",
                "PEPTIDEMK[Label:13C(6) (K)]",
                "PEPTIDEM[Oxidation (M)]K[Label:13C(6) (K)]",
                "PEPT[Phospho (ST)]IDEM[Oxidation (M)]K[Label:13C(6) (K)]",
                "PEPTIDEMC[Carbamidomethyl (C)]R",
                "PEPTIDEM[Oxidation (M)]C[Carbamidomethyl (C)]R",
            }, heavyFullNames);
        }

        private const string DOC_NEUTRAL_LOSSES = @"<?xml version='1.0' encoding='utf-8'?>
<srm_settings format_version='20.22' software_version='Skyline (64-bit : developer build) 20.2.1.422 (319e57e40)'>
  <settings_summary name='Default'>
    <peptide_settings>
      <enzyme name='Trypsin/P' cut='KR' no_cut='' sense='C' />
      <digest_settings max_missed_cleavages='0' />
      <peptide_prediction use_measured_rts='true' measured_rt_window='2' />
      <peptide_filter start='0' min_length='8' max_length='25' auto_select='true'>
        <peptide_exclusions />
      </peptide_filter>
      <peptide_libraries pick='library' />
      <peptide_modifications max_variable_mods='3' max_neutral_losses='1'>
        <static_modifications>
          <static_modification name='Carbamidomethyl (C)' aminoacid='C' formula='H3C2NO' unimod_id='4' short_name='CAM' />
          <static_modification name='Oxidation (M)' aminoacid='M' variable='true' formula='O' unimod_id='35' short_name='Oxi'>
            <potential_loss formula='H4COS' massdiff_monoisotopic='63.998285' massdiff_average='64.10701' />
          </static_modification>
          <static_modification name='Water Loss (D, E, S, T)' aminoacid='D, E, S, T'>
            <potential_loss formula='H2O' massdiff_monoisotopic='18.010565' massdiff_average='18.01528' />
          </static_modification>
          <static_modification name='Phospho (ST)' aminoacid='S, T' formula='HO3P' explicit_decl='true' unimod_id='21' short_name='Pho'>
            <potential_loss formula='H3O4P' massdiff_monoisotopic='97.976896' massdiff_average='97.995181' />
          </static_modification>
        </static_modifications>
        <heavy_modifications>
          <static_modification name='Label:13C(6) (K)' aminoacid='K' label_13C='true' unimod_id='188' short_name='+06' />
        </heavy_modifications>
      </peptide_modifications>
    </peptide_settings>
    <transition_settings>
      <transition_prediction precursor_mass_type='Monoisotopic' fragment_mass_type='Monoisotopic' optimize_by='None' />
      <transition_filter precursor_charges='2' product_charges='3' precursor_adducts='[M+H]' product_adducts='[M+]' fragment_types='y' small_molecule_fragment_types='f' fragment_range_first='m/z &gt; precursor' fragment_range_last='3 ions' precursor_mz_window='0' auto_select='true'>
        <measured_ion name='N-terminal to Proline' cut='P' sense='N' min_length='3' />
      </transition_filter>
      <transition_libraries ion_match_tolerance='0.5' min_ion_count='0' ion_count='3' pick_from='all' />
      <transition_integration />
      <transition_instrument min_mz='50' max_mz='1500' mz_match_tolerance='0.055' />
    </transition_settings>
    <data_settings document_guid='34ef7e1c-93c4-4ac6-a6c8-36f91812127b' audit_logging='true' />
  </settings_summary>
  <protein name='Protein' description='' websearch_status='X' auto_manage_children='false'>
    <sequence>PEPTIDEKPE PTIDECKPEP TIDEMCKPEP TIDEMKPEPT IDEMCR</sequence>
    <peptide sequence='PEPTIDEK' modified_sequence='PEPTIDEK' start='0' end='8' prev_aa='-' next_aa='P' calc_neutral_pep_mass='927.454927' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='7' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='927.454927' precursor_mz='464.73474' collision_energy='0' modified_sequence='PEPTIDEK' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='933.475056' precursor_mz='467.744804' collision_energy='0' modified_sequence='PEPTIDEK[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEK' modified_sequence='PEPT[+79.966331]IDEK' start='0' end='8' prev_aa='-' next_aa='P' calc_neutral_pep_mass='1007.421258' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='1' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
          <explicit_modification index_aa='3' modification_name='Phospho (ST)' mass_diff='+80' />
          <explicit_modification index_aa='5' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
          <explicit_modification index_aa='6' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
        </explicit_static_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='7' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1007.421258' precursor_mz='504.717905' collision_energy='0' modified_sequence='PEPT[+80.0]IDEK' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1013.441387' precursor_mz='507.72797' collision_energy='0' modified_sequence='PEPT[+80.0]IDEK[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDECK' modified_sequence='PEPTIDEC[+57.021464]K' start='8' end='17' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1087.485576' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='7' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1087.485576' precursor_mz='544.750064' collision_energy='0' modified_sequence='PEPTIDEC[+57.0]K' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1093.505705' precursor_mz='547.760128' collision_energy='0' modified_sequence='PEPTIDEC[+57.0]K[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMCK' modified_sequence='PEPTIDEMC[+57.021464]K' start='17' end='27' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1218.526061' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='8' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='9' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1218.526061' precursor_mz='610.270306' collision_energy='0' modified_sequence='PEPTIDEMC[+57.0]K' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1224.54619' precursor_mz='613.280371' collision_energy='0' modified_sequence='PEPTIDEMC[+57.0]K[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMCK' modified_sequence='PEPTIDEM[+15.994915]C[+57.021464]K' start='17' end='27' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1234.520976' num_missed_cleavages='0'>
      <variable_modifications>
        <variable_modification index_aa='7' modification_name='Oxidation (M)' mass_diff='+16' />
      </variable_modifications>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='8' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='9' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1234.520976' precursor_mz='618.267764' collision_energy='0' modified_sequence='PEPTIDEM[+16.0]C[+57.0]K' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1240.541105' precursor_mz='621.277828' collision_energy='0' modified_sequence='PEPTIDEM[+16.0]C[+57.0]K[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMK' modified_sequence='PEPTIDEMK' start='27' end='36' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1058.495412' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1058.495412' precursor_mz='530.254982' collision_energy='0' modified_sequence='PEPTIDEMK' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1064.515541' precursor_mz='533.265047' collision_energy='0' modified_sequence='PEPTIDEMK[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMK' modified_sequence='PEPTIDEM[+15.994915]K' start='27' end='36' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1074.490327' num_missed_cleavages='0'>
      <variable_modifications>
        <variable_modification index_aa='7' modification_name='Oxidation (M)' mass_diff='+16' />
      </variable_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1074.490327' precursor_mz='538.25244' collision_energy='0' modified_sequence='PEPTIDEM[+16.0]K' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1080.510456' precursor_mz='541.262504' collision_energy='0' modified_sequence='PEPTIDEM[+16.0]K[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMK' modified_sequence='PEPT[+79.966331]IDEM[+15.994915]K' start='27' end='36' prev_aa='K' next_aa='P' calc_neutral_pep_mass='1154.456658' num_missed_cleavages='0'>
      <explicit_modifications>
        <explicit_static_modifications>
          <explicit_modification index_aa='1' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
          <explicit_modification index_aa='3' modification_name='Phospho (ST)' mass_diff='+80' />
          <explicit_modification index_aa='5' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
          <explicit_modification index_aa='6' modification_name='Water Loss (D, E, S, T)' mass_diff='+0' />
          <explicit_modification index_aa='7' modification_name='Oxidation (M)' mass_diff='+16' />
        </explicit_static_modifications>
      </explicit_modifications>
      <implicit_modifications>
        <implicit_heavy_modifications>
          <implicit_modification index_aa='8' modification_name='Label:13C(6) (K)' mass_diff='+6' />
        </implicit_heavy_modifications>
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1154.456658' precursor_mz='578.235605' collision_energy='0' modified_sequence='PEPT[+80.0]IDEM[+16.0]K' />
      <precursor charge='2' isotope_label='heavy' calc_neutral_mass='1160.476787' precursor_mz='581.24567' collision_energy='0' modified_sequence='PEPT[+80.0]IDEM[+16.0]K[+6.0]' />
    </peptide>
    <peptide sequence='PEPTIDEMCR' modified_sequence='PEPTIDEMC[+57.021464]R' start='36' end='46' prev_aa='K' next_aa='-' calc_neutral_pep_mass='1246.532209' num_missed_cleavages='0'>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='8' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications />
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1246.532209' precursor_mz='624.27338' collision_energy='0' modified_sequence='PEPTIDEMC[+57.0]R' />
    </peptide>
    <peptide sequence='PEPTIDEMCR' modified_sequence='PEPTIDEM[+15.994915]C[+57.021464]R' start='36' end='46' prev_aa='K' next_aa='-' calc_neutral_pep_mass='1262.527124' num_missed_cleavages='0'>
      <variable_modifications>
        <variable_modification index_aa='7' modification_name='Oxidation (M)' mass_diff='+16' />
      </variable_modifications>
      <implicit_modifications>
        <implicit_static_modifications>
          <implicit_modification index_aa='8' modification_name='Carbamidomethyl (C)' mass_diff='+57' />
        </implicit_static_modifications>
        <implicit_heavy_modifications />
      </implicit_modifications>
      <precursor charge='2' calc_neutral_mass='1262.527124' precursor_mz='632.270838' collision_energy='0' modified_sequence='PEPTIDEM[+16.0]C[+57.0]R' />
    </peptide>
  </protein>
</srm_settings>";
    }
}
