//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is Greazy.
//
// The Initial Developer of the Original Code is Mike Kochen.
//
// Copyright 2013 Vanderbilt University
//
// Contributor(s):
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using System.Diagnostics;

namespace Greazy
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void PL_CheckedChanged(object sender, EventArgs e)
        {
            panel1.Enabled = PL.Checked;
        }

        private void SL_CheckedChanged(object sender, EventArgs e)
        {
            panel2.Enabled = SP.Checked;
        }

        private void CL_CheckedChanged(object sender, EventArgs e)
        {
            panel3.Enabled = CL.Checked;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (ppm.Checked == true)
            {
                ppmMS1.Enabled = true;
            }
            if (ppm.Checked == false)
            {
                ppmMS1.Enabled = false;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (Da.Checked == true)
            {
                DaMS1.Enabled = true;
            }
            if (Da.Checked == false)
            {
                DaMS1.Enabled = false;
            }
        }
        private void ppm2_CheckedChanged(object sender, EventArgs e)
        {
            if (ppm2.Checked == true)
            {
                ppmMS2.Enabled = true;
            }
            if (ppm2.Checked == false)
            {
                ppmMS2.Enabled = false;
            }
        }

        private void Da2_CheckedChanged(object sender, EventArgs e)
        {
            if (Da2.Checked == true)
            {
                DaMS2.Enabled = true;
            }
            if (Da2.Checked == false)
            {
                DaMS2.Enabled = false;
            }
        }

        private void chooseButton_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ofd.Filter = "All files (*.*)|*.*|mzML files (*.mzML)|*.mzML|mzXML files (*.mzXML)|*.mzXML|mgf files (*.mgf)|*.mgf|mg5 files (*.mg5)|*.mg5|raw files (*.raw)|*.raw";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                chosenFile.Text = "\"" + ofd.FileName + "\"";
            }
        }

        private void CreateLipidConfig()
        {
            var config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lipidConfig.txt");
            if (!File.Exists(config))
            {
                File.Create(config).Dispose();
            }
            var outfile = new StreamWriter(config);
            outfile.WriteLine("// Greazy Configuration File");
            outfile.WriteLine("// classification of lipids as defined by the LIPID MAPS initiative");
            outfile.WriteLine("GP = {0}", PL.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Cholines = {0}", PC.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Ethanolamines = {0}", PE.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Serines = {0}", PS.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Glycerols = {0}", PG.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Inositols = {0}", PI.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Phosphates = {0}", PA.Checked ? "Y" : "N");
            outfile.WriteLine("GP.InositolPhosphates = {0}", PIPs.Checked ? "Y" : "N");
            outfile.WriteLine("GP.LowerLengthLim1 = {0}", LengthRange1L.Text);
            outfile.WriteLine("GP.UpperLengthLim1 = {0}", LengthRange1U.Text);
            outfile.WriteLine("GP.LowerLengthLim2 = {0}", LengthRange2L.Text);
            outfile.WriteLine("GP.UpperLengthLim2 = {0}", LengthRange2U.Text);
            outfile.WriteLine("GP.LowerDoubleBondLim1 = {0}", DoubleBond1L.Text);
            outfile.WriteLine("GP.UpperDoubleBondLim1 = {0}", DoubleBond1U.Text);
            outfile.WriteLine("GP.LowerDoubleBondLim2 = {0}", DoubleBond2L.Text);
            outfile.WriteLine("GP.UpperDoubleBondLim2 = {0}", DoubleBond2U.Text);
            outfile.WriteLine("GP.Lyso1 = {0}", SN1lyso.Checked ? "Y" : "N");
            outfile.WriteLine("GP.Lyso2 = {0}", SN2lyso.Checked ? "Y" : "N");
            outfile.WriteLine("GP.AcylBond1 = {0}", SN1acyl.Checked ? "Y" : "N");
            outfile.WriteLine("GP.AcylBond2 = {0}", SN2acyl.Checked ? "Y" : "N");
            outfile.WriteLine("GP.EtherBond1 = {0}", SN1ether.Checked ? "Y" : "N");
            outfile.WriteLine("GP.EtherBond2 = {0}", SN2ether.Checked ? "Y" : "N");
            outfile.WriteLine("GP.evenOnly = {0}", GPevenOnly.Checked ? "Y" : "N");
            outfile.WriteLine("SP = {0}", SP.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Sphingosine = {0}", sphingosine.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Sphinganine = {0}", sphinganine.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Phytosphingosine = {0}", phytosphingosine.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Sphingadienine = {0}", phytosphingosine.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Cholines = {0}", SPC.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Inositols = {0}", SPI.Checked ? "Y" : "N");
            outfile.WriteLine("SP.Ethanolamines = {0}", SPE.Checked ? "Y" : "N");
            outfile.WriteLine("SP.LowerBack = {0}", backboneLengthL.Text);
            outfile.WriteLine("SP.UpperBack = {0}", backboneLengthU.Text);
            outfile.WriteLine("SP.LowerLength = {0}", FAlengthL.Text);
            outfile.WriteLine("SP.UpperLength = {0}", FAlengthU.Text);
            outfile.WriteLine("SP.LowerDoubleBond = {0}", FAdoubleBondsL.Text);
            outfile.WriteLine("SP.UpperDoubleBond = {0}", FAdoubleBondsU.Text);
            outfile.WriteLine("CL = {0}", CL.Checked ? "Y" : "N");
            outfile.WriteLine("CL.None = Y");
            //outfile.WriteLine("CL.Glucosyl = N");
            outfile.WriteLine("CL.Acyl = Y");
            outfile.WriteLine("CL.Ether = N");
            outfile.WriteLine("CL.LowerLengthLim1a = {0}", cardiolipinSN1lengthL.Text);
            outfile.WriteLine("CL.UpperLengthLim1a = {0}", cardiolipinSN1lengthU.Text);
            outfile.WriteLine("CL.LowerLengthLim2a = {0}", cardiolipinSN2lengthL.Text);
            outfile.WriteLine("CL.UpperLengthLim2a = {0}", cardiolipinSN2lengthU.Text);
            outfile.WriteLine("CL.LowerLengthLim1b = {0}", cardiolipinSN3lengthL.Text);
            outfile.WriteLine("CL.UpperLengthLim1b = {0}", cardiolipinSN3lengthU.Text);
            outfile.WriteLine("CL.LowerLengthLim2b = {0}", cardiolipinSN4lengthL.Text);
            outfile.WriteLine("CL.UpperLengthLim2b = {0}", cardiolipinSN4lengthU.Text);
            outfile.WriteLine("CL.LowerDoubleBondLim1a = {0}", cardiolipinSN1doubleBondsL.Text);
            outfile.WriteLine("CL.UpperDoubleBondLim1a = {0}", cardiolipinSN1doubleBondsU.Text);
            outfile.WriteLine("CL.LowerDoubleBondLim2a = {0}", cardiolipinSN2doubleBondsL.Text);
            outfile.WriteLine("CL.UpperDoubleBondLim2a = {0}", cardiolipinSN2doubleBondsU.Text);
            outfile.WriteLine("CL.LowerDoubleBondLim1b = {0}", cardiolipinSN3doubleBondsL.Text);
            outfile.WriteLine("CL.UpperDoubleBondLim1b = {0}", cardiolipinSN3doubleBondsU.Text);
            outfile.WriteLine("CL.LowerDoubleBondLim2b = {0}", cardiolipinSN4doubleBondsL.Text);
            outfile.WriteLine("CL.UpperDoubleBondLim2b = {0}", cardiolipinSN4doubleBondsU.Text);
            outfile.WriteLine("CL.Lyso1 = {0}", SN1lysoCL1.Checked ? "Y" : "N");
            outfile.WriteLine("CL.Lyso2 = {0}", SN2lysoCL1.Checked ? "Y" : "N");
            outfile.WriteLine("CL.Lyso3 = {0}", SN3lysoCL1.Checked ? "Y" : "N");
            outfile.WriteLine("CL.Lyso4 = {0}", SN4lysoCL1.Checked ? "Y" : "N");
            outfile.WriteLine("CL.evenOnly = {0}", CLevenOnly.Checked ? "Y" : "N");
            outfile.WriteLine("CL.double = {0}", checkBox1.Checked ? "Y" : "N");
            outfile.WriteLine("ESI.pos = {0}", positive.Checked ? "Y" : "N");
            outfile.WriteLine("ESI.neg = {0}", negative.Checked ? "Y" : "N");
            outfile.WriteLine("Add.Pro = Y");
            outfile.WriteLine("Add.Na = {0}", checkedListBox1.GetItemChecked(0) ? "Y" : "N");
            outfile.WriteLine("Add.NH4 = {0}", checkedListBox1.GetItemChecked(1) ? "Y" : "N");
            outfile.WriteLine("Add.Li = {0}", checkedListBox1.GetItemChecked(2) ? "Y" : "N");
            outfile.WriteLine("Add.K = {0}", checkedListBox1.GetItemChecked(3) ? "Y" : "N");
            outfile.WriteLine("Add.Depro = Y");
            outfile.WriteLine("Add.Cl = {0}", checkedListBox1.GetItemChecked(4) ? "Y" : "N");
            outfile.WriteLine("Add.HCOO = {0}", checkedListBox1.GetItemChecked(5) ? "Y" : "N");
            outfile.WriteLine("Add.CH3COO = {0}", checkedListBox1.GetItemChecked(6) ? "Y" : "N");
            outfile.WriteLine("preTolmz = {0}", Da.Checked ? "Y" : "N");
            outfile.WriteLine("mzTol = {0}", DaMS1.Text);
            outfile.WriteLine("ppmTol = {0}", ppmMS1.Text);
            outfile.WriteLine("fragTolmz = {0}", Da2.Checked ? "Y" : "N");
            outfile.WriteLine("mzFragTol = {0}", DaMS2.Text);
            outfile.WriteLine("ppmFragTol = {0}", ppmMS2.Text);
            outfile.WriteLine("PeakNumber = 40000");
            outfile.WriteLine("Factorials = Y");
            outfile.WriteLine("retTimeLow = 0");
            outfile.WriteLine("retTimeHigh = 1000000");
            outfile.WriteLine("intensityScore = {0}", peakOnly.Checked ? "0" : "1");

            outfile.Close();
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            CreateLipidConfig();

            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "greazy.exe"); // lib\\notlipidiff\\notlipidiff.exe   trunk\\build-nt-x86\\msvc-release\\notlipidiff.exe
            var program = new ProcessStartInfo(exePath);
            var filePath = chosenFile.Text;
            program.Arguments = filePath;
            Process.Start(program);
        }

        private void LipidLama_Click(object sender, EventArgs e)
        {
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LipidLama.exe"); // lib\\LipidLama\\LipidLama.exe   LipidLama\\Debug\\LipidLama.exe
            var program = new ProcessStartInfo(exePath);
            var temp = chosenFile.Text;
            int dot = temp.LastIndexOf('.');
            var dotless = temp.Substring(0, dot);
            var filePath = dotless + ".lama\"" ;
            program.Arguments = filePath + " " + cutoff.Text + " " + top.Text;
            Process.Start(program);
        }

        private void cutoff_TextChanged(object sender, EventArgs e)
        {

        }

        private void top_TextChanged(object sender, EventArgs e)
        {

        }


    }
}
