using System;
using System.Collections.Generic;
using System.IO;
using AutoQC.Properties;

namespace AutoQC
{
    public class SprocopSettings: TabSettings
    {
        private bool RunSprocop { get; set; }
        public string RScriptPath { get; set; }

        public String ReportFilePath
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "SProCoP_report.skyr"); }
        }

        public string SProCoPrScript
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "QCplotsRgui2.R"); }
        }

        public override void InitializeFromDefaultSettings()
        { 
            RunSprocop = Settings.Default.RunSprocop;
            MainForm.cbRunsprocop.Checked = RunSprocop;

            RScriptPath = Settings.Default.RScriptPath;
            MainForm.textRScriptPath.Text = RScriptPath;

            if (!RunSprocop)
            {
                MainForm.groupBoxSprocop.Enabled = false;
            }
        }

        public override bool IsSelected()
        {
            return false;
        }

        public override bool ValidateSettings()
        {
            if (!RunSprocop)
            {
                Log("Will NOT run SProCoP.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(MainForm.textRScriptPath.Text))
            {
                LogErrorOutput("Please specify path to Rscript.exe.");
                return false;
            }
            return true;
        }

        public override void SaveSettings()
        {
            RunSprocop = MainForm.cbRunsprocop.Checked;
            Settings.Default.RunSprocop = RunSprocop;

            RScriptPath = MainForm.textRScriptPath.Text;
            Settings.Default.RScriptPath = RScriptPath;
        }

        public override IEnumerable<string> SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            string exportReportPath = Path.GetDirectoryName(ReportFilePath) + "\\" + "report.csv";
            var args =
                String.Format(
                    @""" --report-conflict-resolution=overwrite --report-add=""{0}"" --report-name=""{1}"" --report-file=""{2}""",
                    ReportFilePath, "SProCoP Input", exportReportPath);

            return new List<string> { args };

        }

//        public override Process GetProcess()
//        {
//            // string saveQcPath = Path.GetDirectoryName(importContext.GetResultsFilePath()) + "\\QC.pdf";
//            //                        var process2 = new Process();
//            //                        var startInfo2 = new ProcessStartInfo
//            //                        {
//            //                            FileName = config.RScriptPath,
//            //                            Arguments = String.Format(@"""{0}"" ""{1}"" {2} {3} 1 {4} ""{5}""", config.SProCoPrScript, exportReportPath, numericUpDownThreshold.Value, checkBoxIsHighRes.Checked ? 1:0, numericUpDownMMA.Value,saveQcPath),
//            //                            UseShellExecute = false
//            //                        };
//            //                        process2.StartInfo = startInfo2;
//            //                        process2.Start();
//            return null;
//
//        }
    }
}