using SkylineTool;

namespace ToolServiceTestHarness
{
    public partial class ToolServiceTestHarnessForm : Form
    {
        public ToolServiceTestHarnessForm()
        {
            InitializeComponent();
            foreach (var method in typeof(IToolService).GetMethods())
            {
                comboMethod.Items.Add(method.Name);
            }
        }
    }
}