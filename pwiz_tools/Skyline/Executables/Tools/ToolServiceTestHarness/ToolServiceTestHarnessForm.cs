using System.Reflection;
using SkylineTool;

namespace ToolServiceTestHarness
{
    public partial class ToolServiceTestHarnessForm : Form
    {
        private readonly string _arg1OriginalLabel;
        private readonly string _arg2OriginalLabel;
        private readonly MethodInfo[] _methodInfos;
        public ToolServiceTestHarnessForm()
        {
            InitializeComponent();
            _methodInfos = typeof(IToolService).GetMethods().ToArray();
            foreach (var method in _methodInfos)
            {
                comboMethod.Items.Add(method.Name);
            }

            _arg1OriginalLabel = lblArgument1.Text;
            _arg2OriginalLabel = lblArgument2.Text;
        }

        private void comboMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            var methodInfo = GetSelectedMethodInfo();
            var parameters = methodInfo?.GetParameters() ?? Array.Empty<ParameterInfo>();
            if (parameters.Length > 0)
            {
                lblArgument1.Text = parameters[0].Name;
                tbxArgument1.Enabled = true;
            }
            else
            {
                lblArgument1.Text = _arg1OriginalLabel;
                tbxArgument1.Enabled = false;
            }

            if (parameters.Length > 1)
            {
                lblArgument2.Text = parameters[1].Name;
                tbxArgument2.Enabled = true;
            }
            else
            {
                lblArgument2.Text = _arg2OriginalLabel;
                tbxArgument2.Enabled = false;
            }

            btnInvokeMethod.Enabled = methodInfo != null;
        }

        public MethodInfo? GetSelectedMethodInfo()
        {
            if (comboMethod.SelectedIndex < 0 || comboMethod.SelectedIndex >= _methodInfos.Length)
            {
                return null;
            }

            return _methodInfos[comboMethod.SelectedIndex];
        }

        private void btnInvokeMethod_Click(object sender, EventArgs e)
        {
            var method = GetSelectedMethodInfo();
            if (method == null)
            {
                return;
            }

            var arguments = new List<object?>();
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                if (TryConvertArgument(tbxArgument1, parameters[0].ParameterType, out var value))
                {
                    arguments.Add(value);
                }
                else
                {
                    return;
                }
            }

            if (parameters.Length > 1)
            {
                if (TryConvertArgument(tbxArgument2, parameters[1].ParameterType, out var value))
                {
                    arguments.Add(value);
                }
                else
                {
                    return;
                }
            }

            try
            {
                var client = new RemoteClient(tbxConnection.Text);
                var result = client.RemoteCallName(method.Name, arguments.ToArray());
                tbxResult.Text = result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                tbxResult.Text = ex.ToString();
            }
        }

        public string ConnectionName
        {
            get
            {
                return tbxConnection.Text;
            }
            set
            {
                tbxConnection.Text = value;
            }
        }

        public bool TryConvertArgument(TextBox textBox, Type targetType, out object? value)
        {
            var argumentConverter = new ArgumentConverter();
            try
            {
                value = argumentConverter.ConvertArgument(textBox.Text, targetType);
                return true;
            }
            catch (Exception ex)
            {
                value = null;
                MessageBox.Show(ex.Message);
                if (ex is LineNumberException argConvEx)
                {
                    textBox.SelectionLength = 0;
                    textBox.SelectionStart = textBox.GetFirstCharIndexFromLine(argConvEx.LineNumber);
                }

                textBox.Focus();
                return false;
            }
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message);
        }

        private void btnPaste1_Click(object sender, EventArgs e)
        {
            tbxArgument1.Text = Clipboard.GetText();
        }
    }
}