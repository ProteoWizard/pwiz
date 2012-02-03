using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLifeRowDataForm : WorkspaceForm
    {
        public HalfLifeRowDataForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            var viewContext = new TopographViewContext(workspace, typeof(HalfLifeCalculator.ProcessedRowData));
            boundDataGridView1.RowSource = new HalfLifeCalculator.ProcessedRowData[0];
            boundDataGridView1.BindingListView.ViewInfo = new ViewInfo(viewContext.ParentColumn, viewContext.BuiltInViewSpecs.First());
            navBar1.ViewContext = viewContext;
        }

        public IList<HalfLifeCalculator.ProcessedRowData> RowDatas
        {
            get { return (IList<HalfLifeCalculator.ProcessedRowData>)boundDataGridView1.RowSource; }
            set { boundDataGridView1.RowSource = value; }
        }

        public string Peptide
        {
            get { return tbxPeptide.Text; }
            set { tbxPeptide.Text = value; }
        }
        public string Protein
        {
            get { return tbxProtein.Text; }
            set { tbxProtein.Text = value; }
        }
        public string Cohort
        {
            get { return tbxCohort.Text; }
            set { tbxCohort.Text = value; }
        }
    }
}
