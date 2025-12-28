using SkylineTool;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace ToolServiceTestHarness
{
    public partial class ToolServiceTestHarnessForm : Form
    {
        private readonly string _arg1OriginalLabel;
        private readonly string _arg2OriginalLabel;
        private MethodInfo[] _methodInfos = [];
        public ToolServiceTestHarnessForm()
        {
            InitializeComponent();
            UpdateMethodDropdown(typeof(IToolService));

            _arg1OriginalLabel = lblArgument1.Text;
            _arg2OriginalLabel = lblArgument2.Text;
        }

        private void UpdateMethodDropdown(Type type)
        {
            _methodInfos = [];
            comboMethod.Items.Clear();
            _methodInfos = type.GetMethods();
            comboMethod.Items.AddRange(_methodInfos.Select(object (method) =>method.Name).ToArray());
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
            string stringValue = textBox.Text;
            if (targetType == typeof(string))
            {
                value = stringValue;
                return true;
            }

#pragma warning disable CS0612 // "Obsolete"
            if (targetType == typeof(DocumentLocation))
            {
                try
                {
                    value = DocumentLocation.Parse(stringValue);
                    return true;
                }
                catch (Exception exception)
                {
                    HandleException(exception);
                    textBox.Focus();
                }
            }
#pragma warning restore CS0612

            if (targetType == typeof(string[]))
            {
                value = stringValue.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                return true;
            }

            value = null;
            ShowError(string.Format(Resources.ToolServiceTestHarnessForm_TryConvertArgument_Unsupported_argument_type__0_, targetType));
            return false;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message);
        }

        private void btnUpdateMethods_Click(object sender, EventArgs e)
        {
            try
            {
                var client = new RemoteClient(tbxConnection.Text);
                var processId = client.RemoteCallName(nameof(IToolService.GetProcessId), []) as int?;
                if (processId == null)
                {
                    ShowError("Unable to get process id from connection");
                    return;
                }

                var process = Process.GetProcessById(processId.Value);
                var skylineToolPath = Path.Combine(Path.GetDirectoryName(process.MainModule?.FileName ?? string.Empty)!, "SkylineTool.dll");
                if (!File.Exists(skylineToolPath))
                {
                    ShowError($"File '{skylineToolPath}' does not exist");
                    return;
                }

                AssemblyLoadContext? loadContext = null;
                try
                {
                    loadContext = new IsolatedAssemblyLoadContext();
                    var assembly = loadContext.LoadFromAssemblyPath(skylineToolPath);
                    var toolServiceTypeName = typeof(IToolService).FullName!;
                    var toolServiceType = assembly.GetType(toolServiceTypeName);
                    if (toolServiceType == null)
                    {
                        ShowError($"Unable to find type {toolServiceType} in {skylineToolPath}");
                        return;
                    }
                    UpdateMethodDropdown(toolServiceType);
                }
                finally
                {
                    loadContext?.Unload();
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        public void HandleException(Exception ex)
        {
            ShowError(ex.Message);
        }

        public class IsolatedAssemblyLoadContext() : AssemblyLoadContext(isCollectible: true)
        {
            protected override Assembly Load(AssemblyName assemblyName)
            {
                // Return null to let default context handle dependencies
                return null!;
            }
        }
    }
}