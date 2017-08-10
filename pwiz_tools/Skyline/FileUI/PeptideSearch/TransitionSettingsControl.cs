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

            PeptidePrecursorCharges = settings.Filter.PeptidePrecursorCharges.ToArray();
            PeptideIonCharges = settings.Filter.PeptideProductCharges.ToArray();
            PeptideIonTypes = settings.Filter.PeptideIonTypes.Union(new[] { IonType.precursor, IonType.y }).ToArray(); // Add p, y if not already set
            ExclusionUseDIAWindow = settings.Filter.ExclusionUseDIAWindow;
            IonMatchTolerance = settings.Libraries.IonMatchTolerance;
            IonCount = settings.Libraries.IonCount;
        }

        private SkylineWindow SkylineWindow { get; set; }

        public Adduct[] PeptidePrecursorCharges
        {
            set { txtPeptidePrecursorCharges.Text = value.ToArray().ToString(", "); } // Not L10N
        }

        public Adduct[] PeptideIonCharges
        {
            set { txtPrecursorIonCharges.Text = value.ToArray().ToString(", "); } // Not L10N
        }

        public IonType[] PeptideIonTypes
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

        public static bool ValidateAdductListTextBox(MessageBoxHelper helper, TextBox control, bool proteomic,
            int minCharge, int maxCharge, out Adduct[] val)
        {
            val = proteomic ?
                ArrayUtil.Parse(control.Text, Adduct.FromStringAssumeProtonated, TextUtil.SEPARATOR_CSV, new Adduct[0]) : // Treat "1" as protonated, proteomic [M+H]
                ArrayUtil.Parse(control.Text, Adduct.FromStringAssumeChargeOnly, TextUtil.SEPARATOR_CSV, new Adduct[0]);  // Treat "1" as [M+]
            if (val.Length > 0 && !val.Contains(i => minCharge > Math.Abs(i.AdductCharge) || Math.Abs(i.AdductCharge) > maxCharge))
            {
                return true;
            }
            helper.ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateAdductListTextBox__0__must_contain_a_comma_separated_list_of_adducts_or_integers_describing_charge_states_with_absolute_values_from__1__to__2__,
                null, minCharge, maxCharge);
            val = new Adduct[0];
            return false;
        }
        public static bool ValidateAdductListTextBox(MessageBoxHelper helper, TabControl tabControl, int tabIndex,
            TextBox control, bool proteomic, int min, int max, out Adduct[] val)
        {
            bool valid = ValidateAdductListTextBox(helper, control, proteomic, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();
            }
            return valid;
        }

        public TransitionSettings GetTransitionSettings(Form parent)
        {
            var helper = new MessageBoxHelper(parent);
            TransitionSettings settings = SkylineWindow.DocumentUI.Settings.TransitionSettings;

            // Validate and store filter settings
            Adduct[] peptidePrecursorCharges;
            if (!ValidateAdductListTextBox(helper, txtPeptidePrecursorCharges, true, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, out peptidePrecursorCharges))
                return null;
            peptidePrecursorCharges = peptidePrecursorCharges.Distinct().ToArray();

            Adduct[] peptideProductCharges;
            if (!ValidateAdductListTextBox(helper, txtPrecursorIonCharges, true, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE, out peptideProductCharges))
                return null;
            peptideProductCharges = peptideProductCharges.Distinct().ToArray();

            IonType[] peptideIonTypes = PeptideIonTypes;
            if (peptideIonTypes.Length == 0)
            {
                helper.ShowTextBoxError(txtIonTypes, Resources.TransitionSettingsUI_OkDialog_Ion_types_must_contain_a_comma_separated_list_of_ion_types_a_b_c_x_y_z_and_p_for_precursor);
                return null;
            }
            peptideIonTypes = peptideIonTypes.Distinct().ToArray();

            bool exclusionUseDIAWindow = cbExclusionUseDIAWindow.Visible && cbExclusionUseDIAWindow.Checked;
            var filter = new TransitionFilter(peptidePrecursorCharges, peptideProductCharges, peptideIonTypes,
                settings.Filter.SmallMoleculePrecursorAdducts, settings.Filter.SmallMoleculeFragmentAdducts, settings.Filter.SmallMoleculeIonTypes, 
                settings.Filter.FragmentRangeFirstName, settings.Filter.FragmentRangeLastName,
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
