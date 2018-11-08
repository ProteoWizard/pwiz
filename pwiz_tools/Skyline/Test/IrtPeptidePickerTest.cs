/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IrtPeptidePickerTest : AbstractUnitTest
    {
        private MeasuredPeptide BuildMeasuredPeptide(string seq, double rt)
        {
            return new MeasuredPeptide(new Target(seq), rt);
        }

        [TestMethod]
        public void TestIrtPeptidePicker()
        {
            var peptides = new List<MeasuredPeptide> // 104 CiRT peptides
            {
                BuildMeasuredPeptide("YDSTHGR", 5.45),
                BuildMeasuredPeptide("AFEEAEK", 17.95),
                BuildMeasuredPeptide("SAPSTGGVK", 23.01),
                BuildMeasuredPeptide("LAEQAER", 23.82),
                BuildMeasuredPeptide("HVFGQAAK", 24.76),
                BuildMeasuredPeptide("EDAANNYAR", 25.51),
                BuildMeasuredPeptide("EHAALEPR", 25.82),
                BuildMeasuredPeptide("VSTEVDAR", 26.95),
                BuildMeasuredPeptide("IHETNLK", 27.07),
                BuildMeasuredPeptide("EGIPPDQQR", 27.88),
                BuildMeasuredPeptide("EIQTAVR", 29.01),
                BuildMeasuredPeptide("APGFGDNR", 29.63),
                BuildMeasuredPeptide("GC[+57.0]EVVVSGK", 29.63),
                BuildMeasuredPeptide("DSYVGDEAQSK", 29.70),
                BuildMeasuredPeptide("MLSC[+57.0]AGADR", 29.82),
                BuildMeasuredPeptide("FDDGAGGDNEVQR", 32.32),
                BuildMeasuredPeptide("C[+57.0]ATITPDEAR", 32.95),
                BuildMeasuredPeptide("AGFAGDDAPR", 33.13),
                BuildMeasuredPeptide("DNTGYDLK", 33.38),
                BuildMeasuredPeptide("NMSVIAHVDHGK", 35.95),
                BuildMeasuredPeptide("EIAQDFK", 36.38),
                BuildMeasuredPeptide("DAHQSLLATR", 36.88),
                BuildMeasuredPeptide("GEEILSGAQR", 37.76),
                BuildMeasuredPeptide("DLMAC[+57.0]AQTGSGK", 38.88),
                BuildMeasuredPeptide("SGQGAFGNMC[+57.0]R", 39.20),
                BuildMeasuredPeptide("TLSDYNIQK", 41.26),
                BuildMeasuredPeptide("AILGSVER", 41.82),
                BuildMeasuredPeptide("NIVEAAAVR", 42.13),
                BuildMeasuredPeptide("HLTGEFEK", 42.95),
                BuildMeasuredPeptide("VVPGYGHAVLR", 43.63),
                BuildMeasuredPeptide("HLQLAIR", 44.07),
                BuildMeasuredPeptide("FDNLYGC[+57.0]R", 44.26),
                BuildMeasuredPeptide("QTVAVGVIK", 44.32),
                BuildMeasuredPeptide("ELIIGDR", 45.38),
                BuildMeasuredPeptide("LVLVGDGGTGK", 45.57),
                BuildMeasuredPeptide("VAVVAGYGDVGK", 47.51),
                BuildMeasuredPeptide("TTIFSPEGR", 47.70),
                BuildMeasuredPeptide("AQIWDTAGQER", 48.45),
                BuildMeasuredPeptide("VPAINVNDSVTK", 48.88),
                BuildMeasuredPeptide("STELLIR", 49.07),
                BuildMeasuredPeptide("FPFAANSR", 49.51),
                BuildMeasuredPeptide("AVANQTSATFLR", 49.82),
                BuildMeasuredPeptide("IGGIGTVPVGR", 51.38),
                BuildMeasuredPeptide("HITIFSPEGR", 51.51),
                BuildMeasuredPeptide("AFLIEEQK", 52.07),
                BuildMeasuredPeptide("ELISNASDALDK", 52.26),
                BuildMeasuredPeptide("NLQYYDISAK", 53.82),
                BuildMeasuredPeptide("YLVLDEADR", 54.57),
                BuildMeasuredPeptide("GVLLYGPPGTGK", 54.76),
                BuildMeasuredPeptide("IC[+57.0]DFGLAR", 54.95),
                BuildMeasuredPeptide("GVLMYGPPGTGK", 55.01),
                BuildMeasuredPeptide("LPLQDVYK", 55.07),
                BuildMeasuredPeptide("EC[+57.0]ADLWPR", 55.20),
                BuildMeasuredPeptide("LILIESR", 55.20),
                BuildMeasuredPeptide("DVQEIFR", 55.63),
                BuildMeasuredPeptide("TIVMGASFR", 55.82),
                BuildMeasuredPeptide("IGPLGLSPK", 55.88),
                BuildMeasuredPeptide("TTVEYLIK", 55.95),
                BuildMeasuredPeptide("HFSVEGQLEFR", 56.57),
                BuildMeasuredPeptide("GVC[+57.0]TEAGMYALR", 57.01),
                BuildMeasuredPeptide("IYGFYDEC[+57.0]K", 57.07),
                BuildMeasuredPeptide("TIAMDGTEGLVR", 57.82),
                BuildMeasuredPeptide("ESTLHLVLR", 57.95),
                BuildMeasuredPeptide("GNHEC[+57.0]ASINR", 58.20),
                BuildMeasuredPeptide("AVFPSIVGRPR", 58.26),
                BuildMeasuredPeptide("NLLSVAYK", 58.88),
                BuildMeasuredPeptide("EVDIGIPDATGR", 60.13),
                BuildMeasuredPeptide("AGLQFPVGR", 60.26),
                BuildMeasuredPeptide("STTTGHLIYK", 61.01),
                BuildMeasuredPeptide("FTVDLPK", 61.07),
                BuildMeasuredPeptide("FDLMYAK", 61.26),
                BuildMeasuredPeptide("LVIVGDGAC[+57.0]GK", 61.38),
                BuildMeasuredPeptide("FVIGGPQGDAGLTGR", 62.32),
                BuildMeasuredPeptide("YAWVLDK", 62.51),
                BuildMeasuredPeptide("LQIWDTAGQER", 62.88),
                BuildMeasuredPeptide("ATIGADFLTK", 64.13),
                BuildMeasuredPeptide("IGLFGGAGVGK", 64.38),
                BuildMeasuredPeptide("GILLYGPPGTGK", 64.57),
                BuildMeasuredPeptide("QAVDVSPLR", 64.88),
                BuildMeasuredPeptide("LYQVEYAFK", 65.13),
                BuildMeasuredPeptide("DELTLEGIK", 66.07),
                BuildMeasuredPeptide("EMVELPLR", 66.32),
                BuildMeasuredPeptide("VC[+57.0]ENIPIVLC[+57.0]GNK", 66.82),
                BuildMeasuredPeptide("FELSGIPPAPR", 67.88),
                BuildMeasuredPeptide("GILFVGSGVSGGEEGAR", 68.26),
                BuildMeasuredPeptide("EAYPGDVFYLHSR", 70.76),
                BuildMeasuredPeptide("LIEDFLAR", 71.20),
                BuildMeasuredPeptide("DLTDYLMK", 72.88),
                BuildMeasuredPeptide("FQSLGVAFYR", 73.13),
                BuildMeasuredPeptide("FTQAGSEVSALLGR", 74.13),
                BuildMeasuredPeptide("FELTGIPPAPR", 74.20),
                BuildMeasuredPeptide("ADTLDPALLRPGR", 75.38),
                BuildMeasuredPeptide("DAGTIAGLNVLR", 76.01),
                BuildMeasuredPeptide("IINEPTAAAIAYGLDK", 76.26),
                BuildMeasuredPeptide("SNYNFEKPFLWLAR", 76.26),
                BuildMeasuredPeptide("SYELPDGQVITIGNER", 77.26),
                BuildMeasuredPeptide("LGANSLLDLVVFGR", 79.57),
                BuildMeasuredPeptide("FEELC[+57.0]ADLFR", 80.70),
                BuildMeasuredPeptide("ATAGDTHLGGEDFDNR", 85.45),
                BuildMeasuredPeptide("VLPSIVNEVLK", 86.51),
                BuildMeasuredPeptide("YFPTQALNFAFK", 93.32),
                BuildMeasuredPeptide("DSTLIMQLLR", 97.32),
                BuildMeasuredPeptide("DWNVDLIPK", 98.01),
                BuildMeasuredPeptide("TTPSYVAFTDTER", 105.82)
            };
            var picker = new CalibrateIrtDlg.IrtPeptidePicker(peptides);
            var startBinCount = picker.BinCount;
            Assert.AreEqual(peptides.Count/CalibrateIrtDlg.IrtPeptidePicker.BIN_FACTOR, startBinCount);
            Assert.AreEqual(peptides.Count, picker.PeptideCount);
            var startCorrelation = picker.R;
            const int filterCount = 15;
            picker.Filter(filterCount);
            Assert.IsTrue(picker.R > startCorrelation);
            Assert.IsTrue(picker.BinCount < startBinCount);
            Assert.AreEqual(filterCount, picker.PeptideCount);
        }
    }
}
