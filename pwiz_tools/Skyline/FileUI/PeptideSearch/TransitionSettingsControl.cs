using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class TransitionSettingsControl : UserControl
    {
        public TransitionSettingsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;
            TransitionSettings settings = SkylineWindow.DocumentUI.Settings.TransitionSettings;

            InitializeComponent();

            PrecursorCharges = settings.Filter.PrecursorCharges.ToArray();
            IonCharges = settings.Filter.ProductCharges.ToArray();
            IonTypes = settings.Filter.IonTypes.Union(new[] {IonType.precursor, IonType.y}).ToArray(); // Add p, y if not already set
            ExclusionUseDIAWindow = settings.Filter.ExclusionUseDIAWindow;
            IonMatchTolerance = settings.Libraries.IonMatchTolerance;
            IonCount = settings.Libraries.IonCount;
        }

        private SkylineWindow SkylineWindow { get; set; }

        public int[] PrecursorCharges
        {
            get { return ArrayUtil.Parse(txtPrecursorCharges.Text, Convert.ToInt32, TextUtil.SEPARATOR_CSV, new int[0]); }
            set { txtPrecursorCharges.Text = value.ToArray().ToString(", "); } // Not L10N
        }

        public int[] IonCharges
        {
            get { return ArrayUtil.Parse(txtPrecursorCharges.Text, Convert.ToInt32, TextUtil.SEPARATOR_CSV, new int[0]); }
            set { txtIonCharges.Text = value.ToArray().ToString(", "); } // Not L10N
        }

        public IonType[] IonTypes
        {
            get { return TransitionFilter.ParseTypes(txtIonTypes.Text, new IonType[0]); }
            set { txtIonTypes.Text = TransitionFilter.ToStringIonTypes(value, true); }
        }

        public bool ExclusionUseDIAWindow
        {
            get { return cbExclusionUseDIAWindow.Checked; }
            set { cbExclusionUseDIAWindow.Checked = value; }
        }

        public double IonMatchTolerance { set { txtTolerance.Text = value.ToString(LocalizationHelper.CurrentCulture); } }
        public int IonCount { set { txtIonCount.Text = value.ToString(LocalizationHelper.CurrentCulture); } }

        public void Initialize(ImportPeptideSearchDlg.Workflow workflow)
        {
            if (workflow != ImportPeptideSearchDlg.Workflow.dia)
            {
                int offset = lblTolerance.Top - cbExclusionUseDIAWindow.Top;
                Array.ForEach(new Control[] {lblTolerance, txtTolerance, lblToleranceUnits, lblIonCount, txtIonCount, lblIonCountUnits}, c => c.Top -= offset);
                cbExclusionUseDIAWindow.Hide();
            }
        }

        public TransitionSettings GetTransitionSettings(Form parent)
        {
            var helper = new MessageBoxHelper(parent);
            TransitionSettings settings = SkylineWindow.DocumentUI.Settings.TransitionSettings;

            // Validate and store filter settings
            int[] precursorCharges;
            if (!helper.ValidateNumberListTextBox(txtPrecursorCharges, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out precursorCharges))
                return null;
            precursorCharges = precursorCharges.Distinct().ToArray();

            int[] productCharges;
            if (!helper.ValidateNumberListTextBox(txtIonCharges, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE, out productCharges))
                return null;
            productCharges = productCharges.Distinct().ToArray();

            IonType[] types = IonTypes;
            if (types.Length == 0)
            {
                helper.ShowTextBoxError(txtIonTypes, Resources.TransitionSettingsUI_OkDialog_Ion_types_must_contain_a_comma_separated_list_of_ion_types_a_b_c_x_y_z_and_p_for_precursor);
                return null;
            }
            types = types.Distinct().ToArray();

            bool exclusionUseDIAWindow = cbExclusionUseDIAWindow.Visible && cbExclusionUseDIAWindow.Checked;
            var filter = new TransitionFilter(precursorCharges, productCharges, types, settings.Filter.FragmentRangeFirstName, settings.Filter.FragmentRangeLastName,
                                              settings.Filter.MeasuredIons, settings.Filter.PrecursorMzWindow, exclusionUseDIAWindow, settings.Filter.AutoSelect);
            Helpers.AssignIfEquals(ref filter, settings.Filter);

            // Validate and store library settings
            double ionMatchTolerance;
            if (!helper.ValidateDecimalTextBox(txtTolerance, TransitionLibraries.MIN_MATCH_TOLERANCE, TransitionLibraries.MAX_MATCH_TOLERANCE, out ionMatchTolerance))
                return null;

            int ionCount = settings.Libraries.IonCount;
            if (!helper.ValidateNumberTextBox(txtIonCount, TransitionLibraries.MIN_ION_COUNT, TransitionLibraries.MAX_ION_COUNT, out ionCount))
                return null;

            TransitionLibraryPick pick = (settings.Libraries.Pick != TransitionLibraryPick.none) ? settings.Libraries.Pick : TransitionLibraryPick.all;
            var libraries = new TransitionLibraries(ionMatchTolerance, ionCount, pick);
            Helpers.AssignIfEquals(ref libraries, settings.Libraries);

            return new TransitionSettings(settings.Prediction, filter, libraries, settings.Integration, settings.Instrument, settings.FullScan);
        }
    }
}
