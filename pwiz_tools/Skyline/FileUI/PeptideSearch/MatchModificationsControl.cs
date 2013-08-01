using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class MatchModificationsControl : UserControl
    {
        public enum ModType { structural, heavy };

        public MatchModificationsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Library _docLib;
        private LibKeyModificationMatcher _matcher;
        private HashSet<StaticMod> _userDefinedTypedMods;

        public IEnumerable<string> MatchedModifications
        {
            get
            {
                var mods = new List<string>();
                foreach (string item in modificationsListBox.Items)
                    mods.Add(item);
                return mods;
            }
        }

        public IEnumerable<string> UnmatchedModifications
        {
            get
            {
                var mods = new List<string>();
                foreach (string item in unmatchedListBox.Items)
                    mods.Add(item);
                return mods;
            }
        }

        public bool Initialize(Library docLib)
        {
            if (docLib == null)
                return false;

            _docLib = docLib;
            _userDefinedTypedMods = new HashSet<StaticMod>();

            InitializeUserDefinedTypedMods();
            GetModificationMatches();
            
            FillLists();
            return (modificationsListBox.Items.Count > 1 || unmatchedListBox.Items.Count > 1);
        }

        private void InitializeUserDefinedTypedMods()
        {
            IsotopeLabelType docDefHeavyLabelType = null;
            foreach (var type in SkylineWindow.Document.Settings.PeptideSettings.Modifications.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered.
                if (!ReferenceEquals(type, IsotopeLabelType.light) && docDefHeavyLabelType == null)
                    docDefHeavyLabelType = type;
                foreach (StaticMod mod in SkylineWindow.Document.Settings.PeptideSettings.Modifications.GetModificationsByName(type.Name).Modifications.
                         Where(m => !m.IsUserSet))
                    _userDefinedTypedMods.Add(mod);
            }

            var staticMods = new TypedModifications(IsotopeLabelType.light, Settings.Default.StaticModList);
            var heavyMods = new TypedModifications(docDefHeavyLabelType, Settings.Default.HeavyModList);

            foreach (StaticMod mod in staticMods.Modifications.Union(heavyMods.Modifications))
                _userDefinedTypedMods.Add(mod);
        }

        public void AddCheckedModifications()
        {
            if (_matcher == null || modificationsListBox.CheckedItems.Count == 0)
                return;

            // Get checked modifications
            var checkedMods = new List<KeyValuePair<AbstractModificationMatcher.AAModKey, AbstractModificationMatcher.AAModMatch>>();
            foreach (var match in _matcher.Matches)
            {
                foreach (string mod in modificationsListBox.CheckedItems)
                {
                    if (mod == MatchToString(match))
                    {
                        checkedMods.Add(match);
                        break;
                    }
                }
            }

            var newStructuralMods = new List<StaticMod>();
            var newHeavyMods = new List<TypedModifications> { new TypedModifications(IsotopeLabelType.heavy, new List<StaticMod>()) };
            foreach (var checkedMod in checkedMods)
            {
                if (checkedMod.Value.StructuralMod != null)
                    newStructuralMods.Add(checkedMod.Value.StructuralMod);
                else if (checkedMod.Value.HeavyMod != null)
                    newHeavyMods.First().Modifications.Add(checkedMod.Value.HeavyMod);
            }
            _matcher.MatcherPepMods = new PeptideModifications(newStructuralMods, newHeavyMods);

            // Update document modifications
            SrmSettings newSettings = SkylineWindow.Document.Settings.ChangePeptideModifications(
                mods => _matcher.SafeMergeImplicitMods(SkylineWindow.Document));
            SkylineWindow.ChangeSettings(newSettings, true, "Add checked modifications");
            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);
        }

        private string MatchToString(KeyValuePair<AbstractModificationMatcher.AAModKey, AbstractModificationMatcher.AAModMatch> match)
        {
            string modName = (match.Value.StructuralMod != null) ? match.Value.StructuralMod.Name : match.Value.HeavyMod.Name;
            return string.Format(Resources.AbstractModificationMatcherFoundMatches__0__equals__1__, modName, match.Key);
        }

        private void GetModificationMatches()
        {
            if (_matcher == null)
                _matcher = new LibKeyModificationMatcher();
            else
                _matcher.ClearMatches();

            _matcher.CreateMatches(SkylineWindow.Document.Settings, _docLib.Keys, Settings.Default.StaticModList, Settings.Default.HeavyModList);
        }

        private void FillLists()
        {
            GetModificationMatches();

            modificationsListBox.Items.Clear();
            unmatchedListBox.Items.Clear();

            foreach (var match in _matcher.Matches)
            {
                StaticMod matchMod = match.Value.StructuralMod ?? match.Value.HeavyMod;
                bool skipThis = false;
                foreach (var userMod in _userDefinedTypedMods)
                {
                    if (matchMod.Equivalent(userMod))
                    {
                        skipThis = true;
                        break;
                    }
                }
                if (skipThis)
                    continue;

                modificationsListBox.Items.Add(MatchToString(match), CheckState.Checked);
            }

            foreach (var uninterpretedMod in _matcher.UnmatchedSequences)
                unmatchedListBox.Items.Add(uninterpretedMod);
        }

        private void btnAddModification_Click(object sender, System.EventArgs e)
        {
            menuAddModification.Show(this, btnAddModification.Left, btnAddModification.Bottom + 1);
        }

        private void menuItemAddStructuralModification_Click(object sender, System.EventArgs e)
        {
            AddModification(ModType.structural);
        }

        private void menuItemAddHeavyModification_Click(object sender, System.EventArgs e)
        {
            AddModification(ModType.heavy);
        }

        public void AddModification(StaticMod mod, ModType type)
        {
            if (mod == null)
                return;

            _userDefinedTypedMods.Add(mod);

            PeptideSettings newPeptideSettings = SkylineWindow.Document.Settings.PeptideSettings;
            var newMods = new List<StaticMod>(
                    (type == ModType.structural ? newPeptideSettings.Modifications.StaticModifications : newPeptideSettings.Modifications.HeavyModifications)
                ) { mod };
            newPeptideSettings = (type == ModType.structural)
                                     ? newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeStaticModifications(newMods))
                                     : newPeptideSettings.ChangeModifications(newPeptideSettings.Modifications.ChangeHeavyModifications(newMods));

            SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideSettings(newPeptideSettings), true,
                string.Format("Add {0} modification {1}", type.ToString(), mod.Name));
            SkylineWindow.Document.Settings.UpdateDefaultModifications(false);

            FillLists();
        }

        public void AddModification(ModType type)
        {
            var newMod = (type == ModType.structural)
                             ? Settings.Default.StaticModList.EditItem(this, null, Settings.Default.StaticModList, null)
                             : Settings.Default.HeavyModList.EditItem(this, null, Settings.Default.HeavyModList, null);

            AddModification(newMod, type);
        }

        public void ChangeAll(bool check)
        {
            for (int i = 0; i < modificationsListBox.Items.Count; ++i)
                modificationsListBox.SetItemChecked(i, check);
        }
    }
}
