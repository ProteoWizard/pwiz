using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
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
        private readonly Dictionary<Control, Point> _originalLocations;

        public TransitionSettingsControl(IModifyDocumentContainer documentContainer)
        {
            _documentContainer = documentContainer;
            InitializeComponent();

            foreach (string item in TransitionFilter.GetFilterStartFragmentFinderLabels())
                comboRangeFrom.Items.Add(item);
            foreach (string item in TransitionFilter.GetFilterEndFragmentFinderLabels())
                comboRangeTo.Items.Add(item);

            SetFields(_documentContainer.Document.Settings.TransitionSettings);
            PeptideIonTypes = PeptideIonTypes.Union(new[] { IonType.precursor, IonType.y }).ToArray(); // Add p, y if not already set
            InitialPeptideIonTypes = PeptideIonTypes.ToArray();

            _originalLocations = Controls.Cast<Control>().ToDictionary(c => c, c => c.Location);
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
                    control.ExclusionUseDIAWindow, control.IonMatchMzTolerance, control.MinIonCount, control.IonCount,
                    control.IonRangeFrom, control.IonRangeTo, control.MinIonMz, control.MaxIonMz)
            {
            }

            public TransitionFilterAndLibrariesSettings(string peptidePrecursorCharges, string peptideIonCharges,
                string peptideIonTypes, bool exclusionUseDiaWindow, MzTolerance ionMatchMzTolerance, int minIonCount,
                int ionCount, string ionRangeFrom, string ionRangeTo, double minIonMz, double maxIonMz)
            {
                PeptidePrecursorCharges = peptidePrecursorCharges;
                PeptideIonCharges = peptideIonCharges;
                PeptideIonTypes = peptideIonTypes;
                ExclusionUseDIAWindow = exclusionUseDiaWindow;
                IonMatchMzTolerance = ionMatchMzTolerance;
                MinIonCount = minIonCount;
                IonCount = ionCount;
                IonRangeFrom = ionRangeFrom;
                IonRangeTo = ionRangeTo;
                MinIonMz = minIonMz;
                MaxIonMz = maxIonMz;
            }

            public static TransitionFilterAndLibrariesSettings GetDefault(TransitionSettings transitionSettings)
            {
                return new TransitionFilterAndLibrariesSettings(transitionSettings.Filter.PeptidePrecursorChargesString,
                    transitionSettings.Filter.PeptideProductChargesString,
                    transitionSettings.Filter.PeptideIonTypesString, transitionSettings.Filter.ExclusionUseDIAWindow,
                    transitionSettings.Libraries.IonMatchMzTolerance,
                    transitionSettings.Libraries.MinIonCount,
                    transitionSettings.Libraries.IonCount, transitionSettings.Filter.FragmentRangeFirst.Label,
                    transitionSettings.Filter.FragmentRangeLast.Label, transitionSettings.Instrument.MinMz,
                    transitionSettings.Instrument.MaxMz);
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
            public MzTolerance IonMatchMzTolerance { get; private set; }
            [Track]
            public int MinIonCount { get; private set; }
            [Track]
            public int IonCount { get; private set; }
            [Track]
            public string IonRangeFrom { get; private set; }
            [Track]
            public string IonRangeTo { get; private set; }
            [Track]
            public double MinIonMz { get; private set; }
            [Track]
            public double MaxIonMz { get; private set; }
        }

        public void SetFields(TransitionSettings settings)
        {
            PeptidePrecursorCharges = settings.Filter.PeptidePrecursorCharges.ToArray();
            PeptideIonCharges = settings.Filter.PeptideProductCharges.ToArray();
            PeptideIonTypes = settings.Filter.PeptideIonTypes.ToArray();
            IonMatchMzTolerance = settings.Libraries.IonMatchMzTolerance;
            MinIonCount = settings.Libraries.MinIonCount;
            IonCount = settings.Libraries.IonCount;
            // If library picking set to "all", make filter settings equivalent for "filter"
            if (settings.Libraries.Pick == TransitionLibraryPick.all)
            {
                comboRangeFrom.SelectedIndex = 0;
                comboRangeTo.SelectedIndex = 0;
                ExclusionUseDIAWindow = false;
            }
            else
            {
                ExclusionUseDIAWindow = settings.Filter.ExclusionUseDIAWindow;
                IonRangeFrom = settings.Filter.FragmentRangeFirst.Label;
                IonRangeTo = settings.Filter.FragmentRangeLast.Label;
            }
            MinIonMz = settings.Instrument.MinMz;
            MaxIonMz = settings.Instrument.MaxMz;
        }

        public bool IonFilter
        {
            get { return panelIonFilter.Visible; }
            set { panelIonFilter.Visible = value; }
        }

        public Adduct[] PeptidePrecursorCharges
        {
            set { txtPeptidePrecursorCharges.Text = value.ToString(@", "); }
        }

        public Adduct[] PeptideIonCharges
        {
            set { txtPrecursorIonCharges.Text = value.ToString(@", "); }
        }

        public IonType[] InitialPeptideIonTypes // For recovering initial settings when user is messing with Full Scan MS1 settings
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

        public MzTolerance IonMatchMzTolerance
        {
            get 
            { 
                return new MzTolerance(IonMatchTolerance, IonMatchToleranceUnits);
            }
            set
            {
                IonMatchTolerance = value.Value;
                IonMatchToleranceUnits = value.Unit;
            }
        }

        public double IonMatchTolerance
        {
            get { return double.Parse(txtTolerance.Text); }
            set { txtTolerance.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public MzTolerance.Units IonMatchToleranceUnits
        {
            get { return (MzTolerance.Units)comboMatchToleranceUnit.SelectedIndex; }
            set { comboMatchToleranceUnit.SelectedIndex = (int)value; }
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

        public string IonRangeFrom
        {
            get { return comboRangeFrom.SelectedItem.ToString(); }
            set
            {
                if (comboRangeFrom.Items.Contains(value))
                    comboRangeFrom.SelectedItem = value;
                else
                    comboRangeFrom.SelectedIndex = 0;
            }
        }

        public string IonRangeTo
        {
            get { return comboRangeTo.SelectedItem.ToString(); }
            set
            {
                if (comboRangeTo.Items.Contains(value))
                    comboRangeTo.SelectedItem = value;
                else
                    comboRangeTo.SelectedIndex = 0;
            }
        }

        public int MinIonMz
        {
            get { return int.Parse(txtMinMz.Text, LocalizationHelper.CurrentCulture); }
            set { txtMinMz.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public int MaxIonMz
        {
            get { return int.Parse(txtMaxMz.Text, LocalizationHelper.CurrentCulture); }
            set { txtMaxMz.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public void Initialize(ImportPeptideSearchDlg.Workflow workflow)
        {
            // Reset control locations, in case this isn't the first call to Initialize.
            foreach (var kvp in _originalLocations)
                kvp.Key.Location = kvp.Value;

            if (workflow != ImportPeptideSearchDlg.Workflow.dia)
            {
                var nextTop = Controls.Cast<Control>().Select(c => c.Top).Where(t => t > cbExclusionUseDIAWindow.Top).Min();
                int offset = nextTop - cbExclusionUseDIAWindow.Top;
                foreach (var control in Controls.Cast<Control>().Where(c => c.Top > cbExclusionUseDIAWindow.Top))
                    control.Top -= offset;
                cbExclusionUseDIAWindow.Hide();
            }
            switch (workflow)
            {
                case ImportPeptideSearchDlg.Workflow.dia:
                case ImportPeptideSearchDlg.Workflow.prm:
                    // If these are just the document defaults, use something more appropriate for DIA
                    var settingsCurrent = _documentContainer.Document.Settings.TransitionSettings;
                    var settings = settingsCurrent;
                    var defSettings = SrmSettingsList.GetDefault().TransitionSettings;
                    var defFilter = defSettings.Filter;
                    if (Equals(settings.Filter, defFilter) ||
                        // This UI itself can add precursor ions, so avoid detecting that as an important difference
                        // TODO(kaipot): Better to enable a way to get to the user document rather than the wizard document
                        Equals(settings.Filter, defFilter
                            .ChangePeptideIonTypes(defFilter.PeptideIonTypes.Union(new [] { IonType.precursor}).ToArray())
                            .ChangeSmallMoleculeIonTypes(defFilter.SmallMoleculeIonTypes.Union(new [] { IonType.precursor}).ToArray())
                            .ChangeExclusionUseDIAWindow(defFilter.ExclusionUseDIAWindow || workflow == ImportPeptideSearchDlg.Workflow.dia)))
                    {
                        settings = settings.ChangeFilter(settings.Filter
                            .ChangePeptidePrecursorCharges(new[] { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED })
                            .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED })
                            .ChangePeptideIonTypes(new[] { IonType.y, IonType.b, IonType.precursor })
                            .ChangeFragmentRangeFirstName(TransitionFilter.StartFragmentFinder.ION_3.GetKey())
                            .ChangeFragmentRangeLastName(TransitionFilter.EndFragmentFinder.LAST_ION.GetKey())
                            .ChangeMeasuredIons(Array.Empty<MeasuredIon>()));
                    }
                    else if (settings.Libraries.Pick == TransitionLibraryPick.all)
                    {
                        // Adjust filters to match "all" picking, since they are ignored in the current settings
                        settings = settings.ChangeFilter(settings.Filter
                            .ChangeFragmentRangeFirstName(TransitionFilter.StartFragmentFinder.ION_1.GetKey())
                            .ChangeFragmentRangeLastName(TransitionFilter.EndFragmentFinder.LAST_ION.GetKey())
                            .ChangeMeasuredIons(Array.Empty<MeasuredIon>())
                            .ChangeExclusionUseDIAWindow(false));
                    }

                    var libraries = settings.Libraries.ChangePick(TransitionLibraryPick.filter);    // Always apply the filter when the wizard is used
                    var defLibraries = defSettings.Libraries;
                    if (Equals(libraries.IonMatchMzTolerance, defLibraries.IonMatchMzTolerance))
                        libraries = libraries.ChangeIonMatchMzTolerance(new MzTolerance(0.05));
                    if (libraries.IonCount == defLibraries.IonCount)
                        libraries = libraries.ChangeIonCount(6);
                    if (libraries.MinIonCount == defLibraries.MinIonCount)
                        libraries = libraries.ChangeMinIonCount(Math.Min(6, libraries.IonCount));
                    if (!Equals(libraries, settings.Libraries))
                        settings = settings.ChangeLibraries(libraries);

                    var instrument = settings.Instrument;
                    var defInstrument = defSettings.Instrument;
                    if (instrument.MinMz == defInstrument.MinMz)
                        instrument = instrument.ChangeMinMz(50);
                    if (instrument.MaxMz == defInstrument.MaxMz)
                        instrument = instrument.ChangeMaxMz(2000);
                    if (!Equals(instrument, settings.Instrument))
                        settings = settings.ChangeInstrument(instrument);

                    if (!ReferenceEquals(settings, settingsCurrent))
                        SetFields(settings);
                    break;
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
                message = PeptideSearchResources.TransitionSettingsControl_ValidateAdductListTextBox__0__must_contain_a_comma_separated_list_of_integers_describing_charge_states_between__1__and__2__;
            }
            else
            {
                message = PeptideSearchResources
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
            string fragmentRangeFirst = TransitionFilter.GetStartFragmentNameFromLabel(
                IonFilter ? IonRangeFrom : settings.Filter.FragmentRangeFirst.Label);
            string fragmentRangeLast = TransitionFilter.GetEndFragmentNameFromLabel(
                IonFilter ? IonRangeTo : settings.Filter.FragmentRangeLast.Label);
            var measuredIons = settings.Filter.MeasuredIons;
            // If the base library settings were "all", clear the measured ion list to remain
            // consistent with switching to "filter" below.
            if (settings.Libraries.Pick == TransitionLibraryPick.all)
            {
                measuredIons = new List<MeasuredIon>();
            }
            var filter = new TransitionFilter(
                peptidePrecursorCharges,
                peptideProductCharges,
                peptideIonTypes,
                settings.Filter.SmallMoleculePrecursorAdducts,
                settings.Filter.SmallMoleculeFragmentAdducts,
                settings.Filter.SmallMoleculeIonTypes, 
                fragmentRangeFirst,
                fragmentRangeLast,
                measuredIons,
                settings.Filter.PrecursorMzWindow,
                exclusionUseDIAWindow,
                settings.Filter.AutoSelect);
            Helpers.AssignIfEquals(ref filter, settings.Filter);

            // Validate and store library settings
            MzTolerance.Units ionMatchToleranceUnit = (MzTolerance.Units)comboMatchToleranceUnit.SelectedIndex;

            double ionMatchTolerance;
            if (!helper.ValidateDecimalTextBox(txtTolerance, TransitionLibraries.MIN_MATCH_TOLERANCE, TransitionLibraries.GetMaxMatchTolerance(ionMatchToleranceUnit), out ionMatchTolerance))
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

            var minIonMz = settings.Instrument.MinMz;
            var maxIonMz = settings.Instrument.MaxMz;
            if (IonFilter)
            {
                if (!helper.ValidateNumberTextBox(txtMinMz, TransitionInstrument.MIN_MEASUREABLE_MZ, TransitionInstrument.MAX_MEASURABLE_MZ, out minIonMz))
                    return null;
                if (!helper.ValidateNumberTextBox(txtMaxMz, TransitionInstrument.MIN_MEASUREABLE_MZ, TransitionInstrument.MAX_MEASURABLE_MZ, out maxIonMz))
                    return null;
            }

            if (minIonMz > maxIonMz)
            {
                helper.ShowTextBoxError(txtMaxMz, string.Format(PeptideSearchResources.TransitionSettingsControl_GetTransitionSettings_Max_m_z__0__must_not_be_less_than_min_m_z__1__, maxIonMz, minIonMz));
                return null;
            }

            var instrument = new TransitionInstrument(minIonMz, maxIonMz, settings.Instrument.IsDynamicMin,
                settings.Instrument.MzMatchTolerance, settings.Instrument.MaxTransitions,
                settings.Instrument.MaxInclusions, settings.Instrument.MinTime, settings.Instrument.MaxTime);
            Helpers.AssignIfEquals(ref instrument, settings.Instrument);

            var libraries = new TransitionLibraries(new MzTolerance(ionMatchTolerance, ionMatchToleranceUnit), minIonCount, ionCount, TransitionLibraryPick.filter);
            Helpers.AssignIfEquals(ref libraries, settings.Libraries);

            return new TransitionSettings(settings.Prediction, filter, libraries, settings.Integration, instrument, settings.FullScan, settings.IonMobilityFiltering);
        }

        private void comboMatchToleranceUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (double.TryParse(txtTolerance.Text, out var matchTolerance))
            {
                if (IonMatchToleranceUnits == MzTolerance.Units.mz)
                    IonMatchTolerance = matchTolerance / 1000;
                else
                    IonMatchTolerance = matchTolerance * 1000;
            }
        }
    }
}
