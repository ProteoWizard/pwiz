using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class OptimizeTransitionsDlg : DataboundGridForm
    {
        private SkylineDataSchema _dataSchema;
        private List<Row> _rowList = new List<Row>();
        private BindingList<Row> _bindingList;

        public OptimizeTransitionsDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _bindingList = new BindingList<Row>(_rowList);
            var viewContext = new SkylineViewContext(_dataSchema, MakeRowSourceInfos());
            BindingListSource.SetViewContext(viewContext);
            Text = TabText = "Optimize Transitions";
        }

        public SkylineWindow SkylineWindow { get; }

        private IList<RowSourceInfo> MakeRowSourceInfos()
        {
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(Row));
            return ImmutableList.Singleton(new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                SkylineViewContext.GetDefaultViewInfo(rootColumn)));
        }

        public class Row : SkylineObject
        {
            public Row(Model.Databinding.Entities.Peptide molecule) : base(molecule.DataSchema)
            {
                Molecule = molecule;
            }

            [InvariantDisplayName("Peptide", InUiMode = UiModes.PROTEOMIC)]
            public Model.Databinding.Entities.Peptide Molecule { get; private set; }
        }
    }
}
