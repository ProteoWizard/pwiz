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
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class IrtPeptidePickerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestIrtPeptidePicker()
        {
            var peptides = new List<MeasuredPeptide> // 104 CiRT peptides
            {
                new MeasuredPeptide("YDSTHGR", 5.45),
                new MeasuredPeptide("AFEEAEK", 17.95),
                new MeasuredPeptide("SAPSTGGVK", 23.01),
                new MeasuredPeptide("LAEQAER", 23.82),
                new MeasuredPeptide("HVFGQAAK", 24.76),
                new MeasuredPeptide("EDAANNYAR", 25.51),
                new MeasuredPeptide("EHAALEPR", 25.82),
                new MeasuredPeptide("VSTEVDAR", 26.95),
                new MeasuredPeptide("IHETNLK", 27.07),
                new MeasuredPeptide("EGIPPDQQR", 27.88),
                new MeasuredPeptide("EIQTAVR", 29.01),
                new MeasuredPeptide("APGFGDNR", 29.63),
                new MeasuredPeptide("GC[+57.0]EVVVSGK", 29.63),
                new MeasuredPeptide("DSYVGDEAQSK", 29.70),
                new MeasuredPeptide("MLSC[+57.0]AGADR", 29.82),
                new MeasuredPeptide("FDDGAGGDNEVQR", 32.32),
                new MeasuredPeptide("C[+57.0]ATITPDEAR", 32.95),
                new MeasuredPeptide("AGFAGDDAPR", 33.13),
                new MeasuredPeptide("DNTGYDLK", 33.38),
                new MeasuredPeptide("NMSVIAHVDHGK", 35.95),
                new MeasuredPeptide("EIAQDFK", 36.38),
                new MeasuredPeptide("DAHQSLLATR", 36.88),
                new MeasuredPeptide("GEEILSGAQR", 37.76),
                new MeasuredPeptide("DLMAC[+57.0]AQTGSGK", 38.88),
                new MeasuredPeptide("SGQGAFGNMC[+57.0]R", 39.20),
                new MeasuredPeptide("TLSDYNIQK", 41.26),
                new MeasuredPeptide("AILGSVER", 41.82),
                new MeasuredPeptide("NIVEAAAVR", 42.13),
                new MeasuredPeptide("HLTGEFEK", 42.95),
                new MeasuredPeptide("VVPGYGHAVLR", 43.63),
                new MeasuredPeptide("HLQLAIR", 44.07),
                new MeasuredPeptide("FDNLYGC[+57.0]R", 44.26),
                new MeasuredPeptide("QTVAVGVIK", 44.32),
                new MeasuredPeptide("ELIIGDR", 45.38),
                new MeasuredPeptide("LVLVGDGGTGK", 45.57),
                new MeasuredPeptide("VAVVAGYGDVGK", 47.51),
                new MeasuredPeptide("TTIFSPEGR", 47.70),
                new MeasuredPeptide("AQIWDTAGQER", 48.45),
                new MeasuredPeptide("VPAINVNDSVTK", 48.88),
                new MeasuredPeptide("STELLIR", 49.07),
                new MeasuredPeptide("FPFAANSR", 49.51),
                new MeasuredPeptide("AVANQTSATFLR", 49.82),
                new MeasuredPeptide("IGGIGTVPVGR", 51.38),
                new MeasuredPeptide("HITIFSPEGR", 51.51),
                new MeasuredPeptide("AFLIEEQK", 52.07),
                new MeasuredPeptide("ELISNASDALDK", 52.26),
                new MeasuredPeptide("NLQYYDISAK", 53.82),
                new MeasuredPeptide("YLVLDEADR", 54.57),
                new MeasuredPeptide("GVLLYGPPGTGK", 54.76),
                new MeasuredPeptide("IC[+57.0]DFGLAR", 54.95),
                new MeasuredPeptide("GVLMYGPPGTGK", 55.01),
                new MeasuredPeptide("LPLQDVYK", 55.07),
                new MeasuredPeptide("EC[+57.0]ADLWPR", 55.20),
                new MeasuredPeptide("LILIESR", 55.20),
                new MeasuredPeptide("DVQEIFR", 55.63),
                new MeasuredPeptide("TIVMGASFR", 55.82),
                new MeasuredPeptide("IGPLGLSPK", 55.88),
                new MeasuredPeptide("TTVEYLIK", 55.95),
                new MeasuredPeptide("HFSVEGQLEFR", 56.57),
                new MeasuredPeptide("GVC[+57.0]TEAGMYALR", 57.01),
                new MeasuredPeptide("IYGFYDEC[+57.0]K", 57.07),
                new MeasuredPeptide("TIAMDGTEGLVR", 57.82),
                new MeasuredPeptide("ESTLHLVLR", 57.95),
                new MeasuredPeptide("GNHEC[+57.0]ASINR", 58.20),
                new MeasuredPeptide("AVFPSIVGRPR", 58.26),
                new MeasuredPeptide("NLLSVAYK", 58.88),
                new MeasuredPeptide("EVDIGIPDATGR", 60.13),
                new MeasuredPeptide("AGLQFPVGR", 60.26),
                new MeasuredPeptide("STTTGHLIYK", 61.01),
                new MeasuredPeptide("FTVDLPK", 61.07),
                new MeasuredPeptide("FDLMYAK", 61.26),
                new MeasuredPeptide("LVIVGDGAC[+57.0]GK", 61.38),
                new MeasuredPeptide("FVIGGPQGDAGLTGR", 62.32),
                new MeasuredPeptide("YAWVLDK", 62.51),
                new MeasuredPeptide("LQIWDTAGQER", 62.88),
                new MeasuredPeptide("ATIGADFLTK", 64.13),
                new MeasuredPeptide("IGLFGGAGVGK", 64.38),
                new MeasuredPeptide("GILLYGPPGTGK", 64.57),
                new MeasuredPeptide("QAVDVSPLR", 64.88),
                new MeasuredPeptide("LYQVEYAFK", 65.13),
                new MeasuredPeptide("DELTLEGIK", 66.07),
                new MeasuredPeptide("EMVELPLR", 66.32),
                new MeasuredPeptide("VC[+57.0]ENIPIVLC[+57.0]GNK", 66.82),
                new MeasuredPeptide("FELSGIPPAPR", 67.88),
                new MeasuredPeptide("GILFVGSGVSGGEEGAR", 68.26),
                new MeasuredPeptide("EAYPGDVFYLHSR", 70.76),
                new MeasuredPeptide("LIEDFLAR", 71.20),
                new MeasuredPeptide("DLTDYLMK", 72.88),
                new MeasuredPeptide("FQSLGVAFYR", 73.13),
                new MeasuredPeptide("FTQAGSEVSALLGR", 74.13),
                new MeasuredPeptide("FELTGIPPAPR", 74.20),
                new MeasuredPeptide("ADTLDPALLRPGR", 75.38),
                new MeasuredPeptide("DAGTIAGLNVLR", 76.01),
                new MeasuredPeptide("IINEPTAAAIAYGLDK", 76.26),
                new MeasuredPeptide("SNYNFEKPFLWLAR", 76.26),
                new MeasuredPeptide("SYELPDGQVITIGNER", 77.26),
                new MeasuredPeptide("LGANSLLDLVVFGR", 79.57),
                new MeasuredPeptide("FEELC[+57.0]ADLFR", 80.70),
                new MeasuredPeptide("ATAGDTHLGGEDFDNR", 85.45),
                new MeasuredPeptide("VLPSIVNEVLK", 86.51),
                new MeasuredPeptide("YFPTQALNFAFK", 93.32),
                new MeasuredPeptide("DSTLIMQLLR", 97.32),
                new MeasuredPeptide("DWNVDLIPK", 98.01),
                new MeasuredPeptide("TTPSYVAFTDTER", 105.82)
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
