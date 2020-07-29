using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DocumentSettingsDlg : FormEx
    {
        public enum TABS { annotations, lists, group_comparisons, reports }
        private readonly SettingsListBoxDriver<AnnotationDef> _annotationsListBoxDriver;
        private readonly SettingsListBoxDriver<GroupComparisonDef> _groupComparisonsListBoxDriver;
        private readonly SettingsListBoxDriver<ListData> _listsListBoxDriver;
        private XmlMappedList<string, MetadataRuleSet> _ruleSets;
        private DataSettings _originalSettings;

        public DocumentSettingsDlg(IDocumentUIContainer documentContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            DocumentContainer = documentContainer;
            var dataSettings = DocumentContainer.Document.Settings.DataSettings;
            _annotationsListBoxDriver = new SettingsListBoxDriver<AnnotationDef>(
                checkedListBoxAnnotations, Settings.Default.AnnotationDefList);
            _annotationsListBoxDriver.LoadList(dataSettings.AnnotationDefs);
            _groupComparisonsListBoxDriver = new SettingsListBoxDriver<GroupComparisonDef>(
                checkedListBoxGroupComparisons, Settings.Default.GroupComparisonDefList);
            _groupComparisonsListBoxDriver.LoadList(dataSettings.GroupComparisonDefs);
            var listDataList = new ListDefList();
            listDataList.AddRange(Settings.Default.ListDefList);
            listDataList.AddRange(dataSettings.Lists);
            _listsListBoxDriver = new SettingsListBoxDriver<ListData>(
                checkedListBoxLists, listDataList);
            _listsListBoxDriver.LoadList(dataSettings.Lists);
            var dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            chooseViewsControl.ViewContext = new SkylineViewContext(dataSchema, new RowSourceInfo[0]);
            chooseViewsControl.ShowCheckboxes = true;
            chooseViewsControl.CheckedViews = dataSettings.ViewSpecList.ViewSpecs.Select(
                viewSpec => PersistedViews.MainGroup.Id.ViewName(viewSpec.Name));
            _ruleSets = new XmlMappedList<string, MetadataRuleSet>();
            _ruleSets.AddRange(Settings.Default.MetadataRuleSets);
            _ruleSets.AddRange(dataSettings.MetadataRuleSets);
            _originalSettings = dataSettings;
            UpdateRuleSets(dataSettings.MetadataRuleSets.Select(ruleSet=>ruleSet.Name).ToHashSet());
        }

        public IDocumentUIContainer DocumentContainer { get; private set; }

        public DataSettings GetDataSettings(DataSettings dataSettings)
        {
            var selectedViews = new HashSet<string>(chooseViewsControl.CheckedViews
                .Where(viewName=>viewName.GroupId.Equals(PersistedViews.MainGroup.Id))
                .Select(viewName=>viewName.Name));
            var viewSpecs = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id)
                .Filter(view => selectedViews.Contains(view.Name));
            var ruleSets = checkedListBoxRuleSets.CheckedIndices.OfType<int>()
                .Select(index => _ruleSets[index]);
            return dataSettings.ChangeAnnotationDefs(_annotationsListBoxDriver.Chosen)
                .ChangeGroupComparisonDefs(_groupComparisonsListBoxDriver.Chosen)
                .ChangeViewSpecList(viewSpecs)
                .ChangeListDefs(_listsListBoxDriver.Chosen)
                .ChangeExtractedMetadata(ruleSets);
        }

        private void btnAddAnnotation_Click(object sender, System.EventArgs e)
        {
            AddAnnotation();
        }

        public void AddAnnotation()
        {
            using (var editDlg = new DefineAnnotationDlg(Settings.Default.AnnotationDefList))
            {
                if (editDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var chosen = _annotationsListBoxDriver.Chosen.ToList();
                    var annotationDef = editDlg.GetAnnotationDef();
                    chosen.Add(annotationDef);
                    Settings.Default.AnnotationDefList.Add(annotationDef);
                    _annotationsListBoxDriver.LoadList(chosen);
                }
            }
        }

        private void btnEditAnnotationList_Click(object sender, System.EventArgs e)
        {
            EditAnnotationList();
        }

        public void EditAnnotationList()
        {
            _annotationsListBoxDriver.EditList();
        }

        public CheckedListBox AnnotationsCheckedListBox { get { return checkedListBoxAnnotations; }}

        private void btnAddGroupComparison_Click(object sender, System.EventArgs e)
        {
            AddGroupComparison();
        }

        public void AddGroupComparison()
        {
            using (var editDlg = new EditGroupComparisonDlg(
                DocumentContainer,
                GroupComparisonDef.EMPTY,
                Settings.Default.GroupComparisonDefList))
            {
                if (editDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var chosen = _groupComparisonsListBoxDriver.Chosen.ToList();
                    Settings.Default.GroupComparisonDefList.Add(editDlg.GroupComparisonDef);
                    chosen.Add(editDlg.GroupComparisonDef);
                    _groupComparisonsListBoxDriver.LoadList(chosen);
                }
            }
        }

        public CheckedListBox GroupComparisonsCheckedListBox { get { return checkedListBoxGroupComparisons; } }

        private void btnEditGroupComparisonList_Click(object sender, System.EventArgs e)
        {
            EditGroupComparisonList();
        }

        public void EditGroupComparisonList()
        {
            _groupComparisonsListBoxDriver.EditList(DocumentContainer);
        }

        private void btnOK_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var chosenLists = new HashSet<string>(_listsListBoxDriver.Chosen.Select(data => data.ListName));
            var removedListsWithData = DocumentContainer.Document.Settings.DataSettings.Lists
                .Where(list => !chosenLists.Contains(list.ListName) && list.RowCount > 0).ToArray();
            if (removedListsWithData.Any())
            {
                string message;
                if (removedListsWithData.Length == 1)
                {
                    var list = removedListsWithData[0];
                    message = string.Format(
                        Resources.DocumentSettingsDlg_OkDialog_The_list___0___has__1__items_in_it__If_you_remove_that_list_from_your_document__those_items_will_be_deleted__Are_you_sure_you_want_to_delete_those_items_from_that_list_,
                        list.ListName, list.RowCount);
                }
                else
                {
                    message = TextUtil.LineSeparate(new[]
                        {
                            Resources.DocumentSettingsDlg_OkDialog_The_following_lists_have_items_in_them_which_will_be_deleted_when_you_remove_the_lists_from_your_document_
                        }
                        .Concat(removedListsWithData.Select(list =>
                            string.Format(Resources.DocumentSettingsDlg_OkDialog_List___0___with__1__items, list.ListName, list.RowCount)))
                        .Concat(new[]{Resources.DocumentSettingsDlg_OkDialog_Are_you_sure_you_want_to_delete_those_items_from_those_lists_}));
                }
                if (MultiButtonMsgDlg.Show(this, message, MultiButtonMsgDlg.BUTTON_OK) == DialogResult.Cancel)
                {
                    return;
                }
            }

            if (!ValidateMetadataRules())
            {
                return;
            }

            Settings.Default.ListDefList.Clear();
            Settings.Default.ListDefList.AddRange(_listsListBoxDriver.List.Select(list=>list.DeleteAllRows()));

            Settings.Default.MetadataRuleSets.Clear();
            Settings.Default.MetadataRuleSets.AddRange(_ruleSets);
            DialogResult = DialogResult.OK;
        }

        public bool ValidateMetadataRules()
        {
            var newDataSettings = GetDataSettings(DocumentContainer.DocumentUI.Settings.DataSettings);
            if (!newDataSettings.MetadataRuleSets.Any() || Equals(_originalSettings, newDataSettings))
            {
                return true;
            }
            var document = DocumentContainer.DocumentUI;
            document = document.ChangeSettings(document.Settings.ChangeDataSettings(newDataSettings));
            MetadataExtractor.ApplyRules(document, null, out CommonException<MetadataExtractor.RuleSetError> error);
            if (error != null)
            {
                string message =
                    string.Format(
                        "An error occurred while applying the rule '{0}'. Do you want to continue with the change to the Document Settings?",
                        error.ExceptionDetail.RuleName);
                var alertDlg = new AlertDlg(message, MessageBoxButtons.OKCancel) { Exception = error };
                if (alertDlg.ShowAndDispose(this) == DialogResult.Cancel)
                {
                    return false;
                }
            }

            return true;
        }

        public TabControl GetTabControl()
        {
            return tabControl;
        }

        public void SelectTab(TABS tab)
        {
            tabControl.SelectedIndex = (int) tab;
        }

        public ChooseViewsControl ChooseViewsControl { get { return chooseViewsControl; } }

        private void btnAddList_Click(object sender, System.EventArgs e)
        {
            AddList();
        }

        public void AddList()
        {
            using (var editDlg = new ListDesigner(ListData.EMPTY, _listsListBoxDriver.List))
            {
                if (editDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var chosen = _listsListBoxDriver.Chosen.ToList();
                    var listDef = editDlg.GetListDef();
                    chosen.Add(listDef);
                    _listsListBoxDriver.List.Add(editDlg.GetListDef());
                    _listsListBoxDriver.LoadList(chosen);
                }
            }
            
        }

        private void btnManageLists_Click(object sender, System.EventArgs e)
        {
            ManageListDefs();
        }

        public void ManageListDefs()
        {
            _listsListBoxDriver.EditList();
        }

        private void btnEditReportList_Click(object sender, System.EventArgs e)
        {
            EditReportList();
        }

        public void EditReportList()
        {
            var dataSchema = new SkylineDataSchema(DocumentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewContext = new DocumentGridViewContext(dataSchema) {EnablePreview = true};
            using (var manageViewsForm = new ManageViewsForm(viewContext))
            {
                manageViewsForm.ShowDialog(this);
            }
        }

        private void btnAddReport_Click(object sender, System.EventArgs e)
        {
            NewReport();
        }

        public void NewReport()
        {
            var dataSchema = new SkylineDataSchema(DocumentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewContext = new DocumentGridViewContext(dataSchema) { EnablePreview = true };
            var newView = viewContext.NewView(this, PersistedViews.MainGroup);
            if (newView != null)
            {
                chooseViewsControl.SelectView(newView.Name);
                chooseViewsControl.CheckedViews = chooseViewsControl.CheckedViews
                    .Append(PersistedViews.MainGroup.Id.ViewName(newView.Name));
            }
        }

        private void btnAddRuleSet_Click(object sender, System.EventArgs e)
        {
            AddResultFileRule();
        }

        public void EditRuleSet()
        {
            var selectedIndex = checkedListBoxRuleSets.SelectedIndex;
            if (selectedIndex < 0)
            {
                return;
            }

            var editedRule = ShowRuleSetEditor(_ruleSets[selectedIndex]);
            if (editedRule == null)
            {
                return;
            }

            _ruleSets[selectedIndex] = editedRule;
            UpdateRuleSets(checkedListBoxRuleSets.CheckedIndices.Cast<int>().Select(i=>_ruleSets[i].Name).ToHashSet());
        }

        public void AddResultFileRule()
        {
            var newRule = ShowRuleSetEditor(new MetadataRuleSet(typeof(ResultFile)));
            if (newRule == null)
            {
                return;
            }

            var checkedNames = checkedListBoxRuleSets.CheckedIndices.Cast<int>().Select(i => _ruleSets[i].Name)
                .ToHashSet();
            checkedNames.Add(newRule.Name);
            _ruleSets.Add(newRule);
            UpdateRuleSets(checkedNames);
        }

        public MetadataRuleSet ShowRuleSetEditor(MetadataRuleSet ruleSet)
        {
            using (var dlg = new MetadataRuleSetEditor(DocumentContainer, ruleSet, _ruleSets))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    return dlg.RuleSet;
                }
                else
                {
                    return null;
                }
            }
        }

        public void UpdateMetadataButtons()
        {
            btnEditRule.Enabled = checkedListBoxRuleSets.SelectedIndices.Count == 1;
            btnDeleteRules.Enabled = checkedListBoxRuleSets.SelectedIndices.Count != 0;
        }

        private void checkedListBoxResultFileRules_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            UpdateMetadataButtons();
        }

        private void btnEditRule_Click(object sender, System.EventArgs e)
        {
            EditRuleSet();
        }

        private void UpdateRuleSets(HashSet<string> checkedNames)
        {
            var newNames = _ruleSets.Select(rule => rule.Name).ToArray();
            checkedListBoxRuleSets.BeginUpdate();
            try
            {
                if (!newNames.SequenceEqual(checkedListBoxRuleSets.Items.Cast<string>()))
                {
                    checkedListBoxRuleSets.Items.Clear();
                    checkedListBoxRuleSets.Items.AddRange(newNames);
                }
                for (int i = 0; i < newNames.Length; i++)
                {
                    checkedListBoxRuleSets.SetItemChecked(i, checkedNames.Contains(newNames[i]));
                }

                if (checkedListBoxRuleSets.SelectedIndex < 0 && checkedListBoxRuleSets.Items.Count > 0)
                {
                    checkedListBoxRuleSets.SelectedIndex = 0;
                }
            }
            finally
            {
                checkedListBoxRuleSets.EndUpdate();
            }
        }

        private void btnDeleteRules_Click(object sender, System.EventArgs e)
        {
            var namesToDelete = checkedListBoxRuleSets.SelectedIndices.Cast<int>().Select(i => _ruleSets[i].Name)
                .ToHashSet();
            if (namesToDelete.Count == 0)
            {
                return;
            }
            string message;
            if (namesToDelete.Count == 1)
            {
                message = string.Format("Are you sure you want to delete the rule '{0}'?", namesToDelete.First());
            }
            else
            {
                message = string.Format("Are you sure you want to delete these {0} rules?", namesToDelete.Count);
            }

            if (DialogResult.OK != new AlertDlg(message, MessageBoxButtons.OKCancel).ShowAndDispose(this))
            {
                return;
            }

            var checkedNames = checkedListBoxRuleSets.CheckedIndices.Cast<int>().Select(i => _ruleSets[i].Name)
                .ToHashSet();
            foreach (var nameToDelete in namesToDelete)
            {
                _ruleSets.Remove(_ruleSets[nameToDelete]);
            }
            UpdateRuleSets(checkedNames);
        }
    }
}
