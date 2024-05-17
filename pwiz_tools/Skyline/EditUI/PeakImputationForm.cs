using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MSAmanda.Utils;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;

namespace pwiz.Skyline.EditUI
{
    public partial class PeakImputationForm : DataboundGridForm
    {
        private List<Row> _rows;
        private BindingList<Row> _rowsBindingList;

        public PeakImputationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _rows = new List<Row>();
            _rowsBindingList = new BindingList<Row>(_rows);
            var dataSchema = new SkylineWindowDataSchema(skylineWindow);
            var rowSource = BindingListRowSource.Create(_rowsBindingList);
            var viewContext = new SkylineViewContext(ColumnDescriptor.RootColumn(dataSchema, typeof(Row)), rowSource);
            BindingListSource.SetViewContext(viewContext);
        }

        public SkylineWindow SkylineWindow { get; }

        public class Row
        {
            public Row(Peptide peptide)
            {
                Peptide = peptide;
            }

            public Peptide Peptide { get; }
        }
    }
}
