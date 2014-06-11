using System.Globalization;
using System.Windows.Forms;
using ExampleArgCollector.Properties;

namespace ExampleArgCollector
{
// ReSharper disable once InconsistentNaming
    public partial class ExampleToolUI : Form
    {
        public string[] Arguments { get; private set; }

        public ExampleToolUI(string[] oldArguments)
        {
            InitializeComponent();
            comboBoxTest.SelectedIndex = 1;
            Arguments = oldArguments;
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
        /// Run before arguments are generated and can return an error message to
        /// the user.  If it returns true arguments will be generated.
        /// </summary>
        private bool VerifyArguments()
        {
            //if textBoxTest has no length it will error "Text Box must be filled", focus the
            //users cursor on textBoxTest, and return false.
            if (textBoxTest.TextLength == 0)
            {
                MessageBox.Show(Resources.ExampleToolUI_VerifyArguments_Text_Box_must_be_filled);
                textBoxTest.Focus();
                return false;
            }
            if (comboBoxTest.SelectedIndex == 0 & checkBoxTest.Checked)
            {
                MessageBox.Show(Resources.ExampleToolUI_VerifyArguments_If_option_1_is_selected_the_check_box_must_be_checked);
                return false;
            }
           
            return true;
        }

        /// <summary>
        /// Generates an Arguments[] for the values of the user inputs.
        /// Number of Arguments is defined in TestToolUtil.cs
        /// </summary>
        public void GenerateArguments()
        {
            Arguments = new string[Constants.ARGUMENT_COUNT];
            Arguments[(int) ArgumentIndices.check_box] = checkBoxTest.Checked ? Constants.TRUE_STRING: Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.text_box] = textBoxTest.Text.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) ArgumentIndices.combo_box] = comboBoxTest.SelectedIndex.ToString(CultureInfo.InvariantCulture);
        }

        private void btnCancel_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

        }
    }

    public class ArgCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {

            using (var dlg = new ExampleToolUI(oldArgs))
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
