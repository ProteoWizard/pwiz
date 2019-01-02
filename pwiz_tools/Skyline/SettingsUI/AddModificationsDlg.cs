using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class AddModificationsDlg : FormEx
    {
        private enum ModType { structural, heavy }

        private class ListBoxModification
        {
            public StaticMod Mod { get; private set; }
            public ListBoxModification(StaticMod mod)
            {
                Mod = mod;
            }

            public override string ToString()
            {
                var aa = Mod.AAs != null ? Mod.AAs.FirstOrDefault() : '\0';
                var mass = Math.Round(AbstractModificationMatcher.GetDefaultModMass(aa, Mod), MassModification.MAX_PRECISION_TO_MATCH);
                var definition = string.Format(@"{0}[{1}]", Mod.AAs, mass);
                return string.Format(Resources.AbstractModificationMatcherFoundMatches__0__equals__1__, Mod.Name, definition);
            }
        }

        public LibKeyModificationMatcher Matcher { get; private set; }
        private readonly LibKey[] _libKeys;
        private readonly HashSet<StaticMod> _userDefinedTypedMods;
        private readonly IsotopeLabelType _defaultHeavyLabelType;

        private SrmSettings _settings;
        private PeptideModifications MatcherPepMods
        {
            get { return Matcher.MatcherPepMods; }
            set { Matcher.MatcherPepMods = value; }
        }
        private IEnumerable<StaticMod> MatcherHeavyMods { get { return MatcherPepMods.GetModifications(_defaultHeavyLabelType); } }
        private static StaticModList DefaultStatic { get { return Settings.Default.StaticModList; } }
        private static HeavyModList DefaultHeavy { get { return Settings.Default.HeavyModList; } }

        public int NumMatched { get { return listMatched.Items.Count; } }
        public int NumUnmatched { get { return listUnmatched.Items.Count; } }
        public StaticMod[] NewDocumentModsStatic { get; private set; }
        public StaticMod[] NewDocumentModsHeavy { get; private set; }

        public AddModificationsDlg(SrmSettings settings, Library library)
        {
            InitializeComponent();

            Matcher = new LibKeyModificationMatcher();
            _libKeys = library.Keys.ToArray();
            _userDefinedTypedMods = new HashSet<StaticMod>();
            _settings = settings;
            NewDocumentModsStatic = new StaticMod[0];
            NewDocumentModsHeavy = new StaticMod[0];

            var mods = _settings.PeptideSettings.Modifications;
            foreach (var type in mods.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered
                if (!ReferenceEquals(type, IsotopeLabelType.light) && _defaultHeavyLabelType == null)
                    _defaultHeavyLabelType = type;

                foreach (var mod in mods.GetModificationsByName(type.Name).Modifications.Where(m => !m.IsUserSet))
                    _userDefinedTypedMods.Add(mod);
            }

            var staticMods = new TypedModifications(IsotopeLabelType.light, mods.StaticModifications);
            var heavyMods = new TypedModifications(_defaultHeavyLabelType, mods.GetModifications(_defaultHeavyLabelType));

            foreach (var mod in staticMods.Modifications.Union(heavyMods.Modifications))
                _userDefinedTypedMods.Add(mod);

            _userDefinedTypedMods.UnionWith(DefaultStatic);
            _userDefinedTypedMods.UnionWith(DefaultHeavy);

            UpdateModificationMatches();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            menuAdd.Show(btnAdd, 0, btnAdd.Height + 1);
        }

        private void UpdateModificationMatches()
        {
            // Update matcher
            Matcher.ClearMatches();
            Matcher.CreateMatches(_settings, _libKeys, DefaultStatic, DefaultHeavy);

            // Update UI
            listMatched.Items.Clear();
            listUnmatched.Items.Clear();

            var allMods = MatcherPepMods.StaticModifications.Union(MatcherHeavyMods);
            foreach (var match in allMods.Where(mod => !_userDefinedTypedMods.Any(mod.Equivalent)))
                listMatched.Items.Add(new ListBoxModification(match), CheckState.Unchecked);

            var unmatched = Matcher.UnmatchedSequences;
            splitContainer.Panel2Collapsed = !unmatched.Any();
            foreach (var uninterpretedMod in unmatched)
                listUnmatched.Items.Add(uninterpretedMod);

            if (listMatched.Items.Count <= 3)
            {
                cbSelectAll.Visible = false;
                listMatched.Height += cbSelectAll.Bottom - listMatched.Bottom;
            }
        }

        private void addSelectedModificationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StaticMod[] structuralMods, heavyMods;
            GetListModifications(CheckState.Checked, out structuralMods, out heavyMods);
            _settings = AddModifications(_settings, structuralMods, ModType.structural);
            _settings = AddModifications(_settings, heavyMods, ModType.heavy);

            UpdateModificationMatches();
            NewDocumentModsStatic = NewDocumentModsStatic.Concat(structuralMods).ToArray();
            NewDocumentModsHeavy = NewDocumentModsHeavy.Concat(heavyMods).ToArray();
        }

        private void addStructuralModificationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var newMod = DefaultStatic.EditItem(this, null, DefaultStatic, null);
            if (newMod != null)
            {
                NewDocumentModsStatic = NewDocumentModsStatic.Concat(new[] {newMod}).ToArray();

                _settings = AddModifications(_settings, new[] { newMod }, ModType.structural);
                UpdateModificationMatches();
            }
        }

        private void addHeavyModificationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var newMod = DefaultHeavy.EditItem(this, null, DefaultHeavy, null);
            if (newMod != null)
            {
                NewDocumentModsHeavy = NewDocumentModsHeavy.Concat(new[] {newMod}).ToArray();

                _settings = AddModifications(_settings, new[] { newMod }, ModType.heavy);
                UpdateModificationMatches();
            }
        }

        private void GetListModifications(CheckState checkState, out StaticMod[] structuralMods, out StaticMod[] heavyMods)
        {
            var structuralModsList = new List<StaticMod>();
            var heavyModsList = new List<StaticMod>();
            var getChecked = checkState == CheckState.Checked;
            foreach (ListBoxModification mod in listMatched.Items)
            {
                if (getChecked != listMatched.CheckedItems.Contains(mod))
                    continue;

                if (MatcherPepMods.StaticModifications.Contains(mod.Mod))
                    structuralModsList.Add(mod.Mod);
                else
                    heavyModsList.Add(mod.Mod);
            }
            structuralMods = structuralModsList.ToArray();
            heavyMods = heavyModsList.ToArray();
        }

        private SrmSettings AddModifications(SrmSettings settings, StaticMod[] mods, ModType type)
        {
            _userDefinedTypedMods.UnionWith(mods);

            PeptideModifications peptideModifications = settings.PeptideSettings.Modifications;
            if (type == ModType.structural)
            {
                peptideModifications = peptideModifications.ChangeStaticModifications(
                    peptideModifications.StaticModifications.Concat(mods).ToArray());
            }
            else
            {
                peptideModifications = peptideModifications.AddHeavyModifications(mods);
            }
            return settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(peptideModifications));
        }

        private void cbSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            SelectAll(cbSelectAll.Checked);
        }

        public void SelectAll(bool check)
        {
            for (var i = 0; i < listMatched.Items.Count; ++i)
                listMatched.SetItemChecked(i, check);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            StaticMod[] removeStructuralMods, removeHeavyMods;
            GetListModifications(CheckState.Unchecked, out removeStructuralMods, out removeHeavyMods);

            foreach (var remMod in removeStructuralMods.Concat(removeHeavyMods))
            {
                foreach (var match in Matcher.Matches.ToArray())
                {
                    if ((match.Value.StructuralMod != null && match.Value.StructuralMod.Equivalent(remMod)) ||
                        (match.Value.HeavyMod != null && match.Value.HeavyMod.Equivalent(remMod)))
                    {
                        Matcher.Matches.Remove(match.Key);
                    }
                }
            }

            MatcherPepMods = MatcherPepMods.ChangeStaticModifications(MatcherPepMods.StaticModifications.Where(mod => !removeStructuralMods.Contains(mod)).ToList());
            MatcherPepMods = MatcherPepMods.RemoveHeavyModifications(removeHeavyMods);
            DialogResult = DialogResult.OK;
        }

        public void OkDialogAll()
        {
            SelectAll(true);
            OkDialog();
        }
    }
}
