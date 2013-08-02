using System.Windows.Forms;

namespace TestArgCollector
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
    }

    public class ArgCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            return new[] {"test", "args", "collector"}; // Not L10N
        }
    }
}
