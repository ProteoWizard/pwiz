using System;
using System.IO;
using AutoQC.Properties;

namespace AutoQC
{
    public class SprocopSettings: SettingsTab
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

        public override string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            var exportReportPath = Path.GetDirectoryName(ReportFilePath) + "\\" + "report.csv";
            var args =
                String.Format(
                    @""" --report-conflict-resolution=overwrite --report-add=""{0}"" --report-name=""{1}"" --report-file=""{2}""",
                    ReportFilePath, "SProCoP Input", exportReportPath);

            return args;

        }

        public override ProcessInfo RunBefore(ImportContext importContext)
        {
            return null;
        }

        public override ProcessInfo RunAfter(ImportContext importContext)
        {
            var saveQcPath = Path.GetDirectoryName(importContext.GetResultsFilePath()) + "\\QC.pdf";
            var args = String.Format(@"""{0}"" ""{1}"" {2} {3} 1 {4} ""{5}""",
                SProCoPrScript,
                ReportFilePath,
                MainForm.numericUpDownThreshold.Value,
                MainForm.checkBoxIsHighRes.Checked ? 1 : 0,
                MainForm.numericUpDownMMA.Value, saveQcPath);
            return new ProcessInfo(RScriptPath, args);
        }
    }
}