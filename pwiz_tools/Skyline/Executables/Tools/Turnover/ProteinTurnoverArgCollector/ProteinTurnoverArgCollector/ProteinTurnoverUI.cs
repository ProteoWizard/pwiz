using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using ProteinTurnoverArgCollector.Properties;

namespace ProteinTurnoverArgCollector
{
// ReSharper disable once InconsistentNaming
    public partial class ProteinTurnoverUI : Form
    {
        public string[] Arguments { get; private set; }
        private string dietEnrichmentDefault = "99";
        private string avgTurnoverDefault = "0";
        private string IDPDefault = "0";
        private string folderNameDefault = "Data";
        
        private string dietEnrichmentRange = "(0,99]";
        private string avgTurnoverRange = "[0,1)";
        private string IDPRange = "[0,1)";



        public ProteinTurnoverUI(string[] oldArguments)
        {
            InitializeComponent();
            Arguments = oldArguments;
            if (Arguments != null)
            {
                Double dietEnrichmentAsPercent = Double.Parse(Arguments[(int) ArgumentIndices.diet_enrichment]) * 100;
                Arguments[(int)ArgumentIndices.diet_enrichment] = dietEnrichmentAsPercent.ToString();
            }

        }

        /// <summary>
        /// Display previous/default arguments in form when loaded. Add tooltips
        /// </summary>
        private void ProteinTurnoverUI_Load(object sender, System.EventArgs e)
        {
            if (Arguments != null)
            {
                textDietEnrichment.Text = Arguments[(int)ArgumentIndices.diet_enrichment];
                textAverageTurnover.Text = Arguments[(int)ArgumentIndices.average_turnover];
                textIDP.Text = Arguments[(int) ArgumentIndices.IDP];
                textFolderName.Text = Arguments[(int) ArgumentIndices.folder_name];
            }
            else
            {
                textDietEnrichment.Text = dietEnrichmentDefault;
                textAverageTurnover.Text = avgTurnoverDefault;
                textIDP.Text = IDPDefault;
                textFolderName.Text = folderNameDefault;
            }

        }

        /// <summary>
        /// "Ok" button click event.  If VerifyArgument() returns true will generate arguments.
        /// </summary>
        private void btnOk_Click(object sender, System.EventArgs e)
        {
            if (VerifyArguments())
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
        }

        /// <summary>
        /// "Cancel" button click event.  Closes form without generating arguments.
        /// </summary>
        private void btnCancel_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

        }

        /// <summary>
        /// Run before arguments are generated and can return an error message to
        /// the user.  If it returns true arguments will be generated.
        /// </summary>
        private bool VerifyArguments()
        {

            // Checks number text boxes
            List<TextBox> inputBoxes = new List<TextBox>{
                //textResolution,
                textDietEnrichment,
                textAverageTurnover,
                textIDP
            };
            List<string> inputRanges = new List<string> {
                dietEnrichmentRange,
                avgTurnoverRange,
                IDPRange
            };


            for (int i = 0; i < inputBoxes.Count; i++)
            {
                if (!VerifyInputNumber(inputBoxes[i], inputRanges[i]))
                {
                    return false;
                }
            }

            if (textFolderName.Text == "")
            {
                MessageBox.Show(Resources.ProteinTurnoverUI_VerifyArguments_Folder_name_cannot_be_blank);
            } else if (textFolderName.Text.Contains(" "))
            {
                MessageBox.Show(Resources.ProteinTurnoverUI_VerifyArguments_Folder_name_cannot_contain_spaces);
            }

            return true;
        }


        /// <summary>
        /// Checks that a texbox is a valid double within input range
        /// </summary>
        private bool VerifyInputNumber(TextBox inputBox, string inputRange)
        {
            double number;
            try
            {
                number = double.Parse(inputBox.Text);
            }
            catch (System.FormatException)
            {
                MessageBox.Show(Resources.ProteinTurnoverUI_VerifyArguments_Input_must_be_number);
                ActiveControl = inputBox;
                return false;
            }

            bool minInclusive = inputRange[0] == '[';
            bool maxInclusive = inputRange[inputRange.Length - 1] == ']';
            string[] range = inputRange.Substring(1, inputRange.Length - 2).Split(',');
            string min = range[0];
            string max = range[1];
            bool inRange = true;

            if (Double.TryParse(min, out double minDouble))
            {
                if (number < minDouble || (number == minDouble && !minInclusive))
                {
                    inRange = false;
                }
            }

            if (Double.TryParse(max, out double maxDouble))
            {
                if (number > maxDouble || (number == maxDouble && !maxInclusive))
                {
                    inRange = false;
                }
            }

            if (!inRange)
            {
                string textBounds = String.Format("between {0} and {1}", min, max);
                textBounds = min == "" ? String.Format("less than {0}", max) : textBounds;
                textBounds = max == "" ? String.Format("greater than {0}", min) : textBounds;

                MessageBox.Show(String.Format("The number {0} must be ", inputBox.Text) + textBounds);
            }

            return inRange;
        }

        /// <summary>
        /// Generates an Arguments[] for the values of the user inputs.
        /// Number of Arguments is defined in TestToolUtil.cs
        /// </summary>
        public void GenerateArguments()
        {
            Arguments = new string[Constants.ARGUMENT_COUNT];
            Arguments[(int) ArgumentIndices.diet_enrichment] = (Double.Parse(textDietEnrichment.Text) / 100).ToString();
            Arguments[(int) ArgumentIndices.average_turnover] = textAverageTurnover.Text;
            Arguments[(int) ArgumentIndices.IDP] = textIDP.Text;
            Arguments[(int) ArgumentIndices.folder_name] = textFolderName.Text;
        }
    }

    public class ArgCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            using (var dlg = new ProteinTurnoverUI(oldArgs))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                dlg.StartPosition = FormStartPosition.WindowsDefaultLocation;
                return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
            }
        }
    }
}
