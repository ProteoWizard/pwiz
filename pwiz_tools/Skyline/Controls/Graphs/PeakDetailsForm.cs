using System.Windows.Forms;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class PeakDetailsForm : Form
    {
        public PeakDetailsForm()
        {
            InitializeComponent();
        }

        public PeakIntegrator PeakIntegrator { get; private set; }
    }
}
