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
        private readonly IModifyDocumentContainer _documentContainer;

        public TransitionSettingsControl(IModifyDocumentContainer documentContainer)
        {
            _documentContainer = documentContainer;
            InitializeComponent();
            SetFields(_documentContainer.Document.Settings.TransitionSettings);
            PeptideIonTypes = PeptideIonTypes.Union(new[] { IonType.precursor, IonType.y }).ToArray(); // Add p, y if not already set
            InitialPeptideIonTypes = PeptideIonTypes.ToArray();
        }

        public TransitionFilterAndLibrariesSettings FilterAndLibrariesSettings
        {
            get { return new TransitionFilterAndLibrariesSettings(this); }
        }

        public class TransitionFilterAndLibrariesSettings
        {
            private static string FixWhitespace(string adducts)
            {
                return string.Join(@", ", adducts.Split(',').Select(s => s.Trim()));
            }

            public TransitionFilterAndLibrariesSettings(TransitionSettingsControl control)
                : this(FixWhitespace(control.txtPeptidePrecursorCharges.Text),
                    FixWhitespace(control.txtPrecursorIonCharges.Text), FixWhitespace(control.txtIonTypes.Text),
                    control.ExclusionUseDIAWindow, control.IonMatchTolerance, control.MinIonCount, control.IonCount)
            {
            }

            public TransitionFilterAndLibrariesSettings(string peptidePrecursorCharges, string peptideIonCharges,
                string peptideIonTypes, bool exclusionUseDiaWindow, double ionMatchTolerance, int minIonCount,
                int ionCount)
            {
                PeptidePrecursorCharges = peptidePrecursorCharges;
                PeptideIonCharges = peptideIonCharges;
                PeptideIonTypes = peptideIonTypes;
                ExclusionUseDIAWindow = exclusionUseDiaWindow;
                IonMatchTolerance = ionMatchTolerance;
                MinIonCount = minIonCount;
                IonCount = ionCount;
            }

            public static TransitionFilterAndLibrariesSettings GetDefault(TransitionSettings transitionSettings)
            {
                return new TransitionFilterAndLibrariesSettings(transitionSettings.Filter.PeptidePrecursorChargesString,
                    transitionSettings.Filter.PeptideProductChargesString,
                    transitionSettings.Filter.PeptideIonTypesString, transitionSettings.Filter.ExclusionUseDIAWindow,
                    transitionSettings.Libraries.IonMatchTolerance, transitionSettings.Libraries.MinIonCount,
                    transitionSettings.Libraries.IonCount);
            }

            [Track]
            public string PeptidePrecursorCharges { get; private set; }
            [Track]
            public string PeptideIonCharges { get; private set; }
            [Track]
            public string PeptideIonTypes { get; private set; }
            [Track]
            public bool ExclusionUseDIAWindow { get; private set; }
            [Track]
            public double IonMatchTolerance { get; private set; }
            [Track]
            public int MinIonCount { get; private set; }
            [Track]
            public int IonCount { get; private set; }
        }

        public void SetFields(TransitionSettings settings)
        {
            PeptidePrecursorCharges = settings.Filter.PeptidePrecursorCharges.ToArray();
            PeptideIonCharges = settings.Filter.PeptideProductCharges.ToArray();
            PeptideIonTypes = settings.Filter.PeptideIonTypes.ToArray();
            ExclusionUseDIAWindow = settings.Filter.ExclusionUseDIAWindow;
            IonMatchTolerance = settings.Libraries.IonMatchTolerance;
            MinIonCount = settings.Libraries.MinIonCount;
            IonCount = settings.Libraries.IonCount;
        }

        public Adduct[] PeptidePrecursorCharges
        {
            set { txtPeptidePrecursorCharges.Text = value.ToString(@", "); }
        }

        public Adduct[] PeptideIonCharges
        {
            set { txtPrecursorIonCharges.Text = value.ToString(@", "); }
        }

        public IonType[] InitialPeptideIonTypes // For recovering inital settings when user is messing with Full Scan MS1 settings
        {
            get;
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

        public double IonMatchTolerance
        {
            get { return double.Parse(txtTolerance.Text, LocalizationHelper.CurrentCulture); }
            set { txtTolerance.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public int MinIonCount
        {
            get { return txtMinIonCount.Text == string.Empty ? 0 : int.Parse(txtMinIonCount.Text, LocalizationHelper.CurrentCulture); }
            set { txtMinIonCount.Text = value != 0 ? value.ToString(LocalizationHelper.CurrentCulture) : string.Empty; }
        }

        public int IonCount
        {
            get { return int.Parse(txtIonCount.Text, LocalizationHelper.CurrentCulture); }
            set { txtIonCount.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public void Initialize(ImportPeptideSearchDlg.Workflow workflow)
        {
            if (workflow != ImportPeptideSearchDlg.Workflow.dia)
            {
                int offset = lblTolerance.Top - cbExclusionUseDIAWindow.Top;
                Array.ForEach(new Control[] {lblTolerance, txtTolerance, lblToleranceUnits, lblIonCount, txtIonCount, lblIonCountUnits}, c => c.Top -= offset);
                cbExclusionUseDIAWindow.Hide();
            }
            // If these are just the document defaults, use something more appropriate for DIA
            else
            {
                var settingsCurrent = _documentContainer.Document.Settings.TransitionSettings;
                var settings = settingsCurrent;
                var defSettings = SrmSettingsList.GetDefault().TransitionSettings;
                if (Equals(settings.Filter, defSettings.Filter))
                {
                    settings = settings.ChangeFilter(settings.Filter
                        .ChangePeptidePrecursorCharges(new[] { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                        .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED })
                        .ChangePeptideIonTypes(new[] { IonType.y, IonType.b, IonType.precursor }));
                }
                if (Equals(settings.Libraries, defSettings.Libraries))
                {
                    settings = settings.ChangeLibraries(settings.Libraries.ChangeIonMatchTolerance(0.05)
                        .ChangeIonCount(6)
                        .ChangeMinIonCount(6));
                }
                if (!ReferenceEquals(settings, settingsCurrent))
                    SetFields(settings);
            }
        }

        public static bool ValidateAdductListTextBox(MessageBoxHelper helper, TextBox control, bool proteomic,
            int minCharge, int maxCharge, out Adduct[] val)
        {
            val = proteomic ?
                ArrayUtil.Parse(control.Text, Adduct.FromStringAssumeProtonated, TextUtil.SEPARATOR_CSV, new Adduct[0]) : // Treat "1" as protonated, proteomic [M+H]
                ArrayUtil.Parse(control.Text, Adduct.FromStringAssumeChargeOnly, TextUtil.SEPARATOR_CSV, new Adduct[0]);  // Treat "1" as [M+]
            if (val.Length > 0 && val.All(adduct=>TransitionFilter.IsChargeInRange(adduct, minCharge, maxCharge, proteomic)))
            {
                return true;
            }

            string message;
            if (proteomic)
            {
                message = Resources.TransitionSettingsControl_ValidateAdductListTextBox__0__must_contain_a_comma_separated_list_of_integers_describing_charge_states_between__1__and__2__;
            }
            else
            {
                message = Resources
                    .MessageBoxHelper_ValidateAdductListTextBox__0__must_contain_a_comma_separated_list_of_adducts_or_integers_describing_charge_states_with_absolute_values_from__1__to__2__;
            }
            helper.ShowTextBoxError(control, message, null, minCharge, maxCharge);
            val = new Adduct[0];
            return false;
        }

        public TransitionSettings GetTransitionSettings(Form parent)
        {
            var helper = new MessageBoxHelper(parent);
            TransitionSettings settings = _documentContainer.Document.Settings.TransitionSettings;

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

            int minIonCount = settings.Libraries.MinIonCount;
            if (string.IsNullOrEmpty(txtMinIonCount.Text))
                minIonCount = 0;
            else if (!helper.ValidateNumberTextBox(txtMinIonCount, 0, TransitionLibraries.MAX_ION_COUNT, out minIonCount))
                return null;

            int ionCount = settings.Libraries.IonCount;
            if (!helper.ValidateNumberTextBox(txtIonCount, TransitionLibraries.MIN_ION_COUNT, TransitionLibraries.MAX_ION_COUNT, out ionCount))
                return null;

            if (minIonCount > ionCount)
            {
                helper.ShowTextBoxError(txtIonCount, string.Format(Resources.TransitionLibraries_DoValidate_Library_ion_count_value__0__must_not_be_less_than_min_ion_count_value__1__,
                                                                   ionCount, minIonCount));
                return null;
            }

            TransitionLibraryPick pick = settings.Libraries.Pick != TransitionLibraryPick.none ? settings.Libraries.Pick : TransitionLibraryPick.all;
            var libraries = new TransitionLibraries(ionMatchTolerance, minIonCount, ionCount, pick);
            Helpers.AssignIfEquals(ref libraries, settings.Libraries);

            return new TransitionSettings(settings.Prediction, filter, libraries, settings.Integration, settings.Instrument, settings.FullScan);
        }
    }
}
