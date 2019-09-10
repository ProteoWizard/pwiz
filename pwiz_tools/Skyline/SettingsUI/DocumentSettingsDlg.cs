using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DocumentSettingsDlg : FormEx
    {
        private readonly SettingsListBoxDriver<AnnotationDef> _annotationsListBoxDriver;
        private readonly SettingsListBoxDriver<GroupComparisonDef> _groupComparisonsListBoxDriver;
        private readonly SettingsListBoxDriver<ListData> _listsListBoxDriver;

        public DocumentSettingsDlg(IDocumentUIContainer documentContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            DocumentContainer = documentContainer;
            _annotationsListBoxDriver = new SettingsListBoxDriver<AnnotationDef>(
                checkedListBoxAnnotations, Settings.Default.AnnotationDefList);
            _annotationsListBoxDriver.LoadList(
                DocumentContainer.Document.Settings.DataSettings.AnnotationDefs);
            _groupComparisonsListBoxDriver = new SettingsListBoxDriver<GroupComparisonDef>(
                checkedListBoxGroupComparisons, Settings.Default.GroupComparisonDefList);
            _groupComparisonsListBoxDriver.LoadList(
                DocumentContainer.Document.Settings.DataSettings.GroupComparisonDefs);
            var listDataList = new ListDefList();
            listDataList.AddRange(Settings.Default.ListDefList);
            listDataList.AddRange(documentContainer.Document.Settings.DataSettings.Lists);
            _listsListBoxDriver = new SettingsListBoxDriver<ListData>(
                checkedListBoxLists, listDataList);
            _listsListBoxDriver.LoadList(documentContainer.Document.Settings.DataSettings.Lists);
            var dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            chooseViewsControl.ViewContext = new SkylineViewContext(dataSchema, new RowSourceInfo[0]);
            chooseViewsControl.ShowCheckboxes = true;
            chooseViewsControl.CheckedViews =
                documentContainer.Document.Settings.DataSettings.ViewSpecList.ViewSpecs.Select(
                    viewSpec => PersistedViews.MainGroup.Id.ViewName(viewSpec.Name));
        }

        public IDocumentUIContainer DocumentContainer { get; private set; }

        public DataSettings GetDataSettings(DataSettings dataSettings)
        {
            var selectedViews = new HashSet<string>(chooseViewsControl.CheckedViews
                .Where(viewName=>viewName.GroupId.Equals(PersistedViews.MainGroup.Id))
                .Select(viewName=>viewName.Name));
            var viewSpecs = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id)
                .Filter(view => selectedViews.Contains(view.Name));
            return dataSettings.ChangeAnnotationDefs(_annotationsListBoxDriver.Chosen)
                .ChangeGroupComparisonDefs(_groupComparisonsListBoxDriver.Chosen)
                .ChangeViewSpecList(viewSpecs)
                .ChangeListDefs(_listsListBoxDriver.Chosen);
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
            Settings.Default.ListDefList.Clear();
            Settings.Default.ListDefList.AddRange(_listsListBoxDriver.List.Select(list=>list.DeleteAllRows()));
            DialogResult = DialogResult.OK;
        }

        public TabControl GetTabControl()
        {
            return tabControl;
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
    }
}
