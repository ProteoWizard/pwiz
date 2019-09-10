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
    }
}
