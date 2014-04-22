/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class ExportLiveReportDlg : FormEx
    {
        private const int indexLocalizedLanguage = 0;
        private const int indexInvariantLanguage = 1;
        private readonly SkylineViewContext _viewContext;
        private readonly IDocumentUIContainer _documentUiContainer;

        public ExportLiveReportDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _documentUiContainer = documentUIContainer;
            _viewContext = new DocumentGridViewContext(new SkylineDataSchema(documentUIContainer, GetDataSchemaLocalizer())) {EnablePreview = true};
            LoadList();
            Debug.Assert(indexLocalizedLanguage == comboLanguage.Items.Count);
            comboLanguage.Items.Add(CultureInfo.CurrentUICulture.DisplayName);
            Debug.Assert(indexInvariantLanguage == comboLanguage.Items.Count);
            comboLanguage.Items.Add(Resources.ExportLiveReportDlg_ExportLiveReportDlg_Invariant);
            comboLanguage.SelectedIndex = 0;
        }

        protected void LoadList()
        {
            LoadList(listboxReports.SelectedItem as ListItem);
        }

        private ListBox ListBox
        {
            get { return listboxReports; }
        }

        public void LoadList(ListItem selectedItemLast)
        {
            ListBox.BeginUpdate();
            ListBox.Items.Clear();
            foreach (var item in _viewContext.CustomViews)
            {
                int i = ListBox.Items.Add(new ListItem(item));

                // Select the previous selection if it is seen.
                if (null != selectedItemLast && selectedItemLast.ViewSpec.Name == item.Name)
                    ListBox.SelectedIndex = i;
            }
            ListBox.EndUpdate();
            ListBoxReportsOnSelectedIndexChanged();
        }

        private void listboxReports_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBoxReportsOnSelectedIndexChanged();
        }

        private void ListBoxReportsOnSelectedIndexChanged()
        {
            bool selReport = listboxReports.SelectedIndex != -1;
            btnPreview.Enabled = selReport;
            btnExport.Enabled = selReport;
        }

        public void OkDialog()
        {
            if (!ExportReport(null, ','))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        public void OkDialog(string fileName, char separator)
        {
            Settings.Default.ExportDirectory = Path.GetDirectoryName(fileName);

            if (!ExportReport(fileName, separator))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        private DataSchemaLocalizer GetDataSchemaLocalizer()
        {
            return InvariantLanguage
                ? DataSchemaLocalizer.INVARIANT
                : SkylineDataSchema.GetLocalizedSchemaLocalizer();
        }
       
        private bool ExportReport(string filename, char separator)
        {
            var dataSchema = new SkylineDataSchema(_documentUiContainer, GetDataSchemaLocalizer()).Clone();
            var viewContext = new SkylineViewContext(dataSchema,
                SkylineViewContext.GetDocumentGridRowSources(dataSchema));
            var viewInfo = viewContext.GetViewInfo(((ListItem) listboxReports.SelectedItem).ViewSpec);
            if (null == filename)
            {
                return viewContext.Export(this, viewInfo);
            }
            return viewContext.ExportToFile(this, viewInfo, filename, new DsvWriter(InvariantLanguage ? CultureInfo.InvariantCulture : LocalizationHelper.CurrentCulture, separator));
        }


        public ViewInfo GetReport()
        {
            var selectedItem = listboxReports.SelectedItem as ListItem;
            if (null == selectedItem)
            {
                return null;
            }
            var dataSchema = new SkylineDataSchema(_documentUiContainer, GetDataSchemaLocalizer());
            SkylineViewContext viewContext = new DocumentGridViewContext(dataSchema);
            return viewContext.GetViewInfo(selectedItem.ViewSpec);
        }

        public bool InvariantLanguage
        {
            get { return comboLanguage.SelectedIndex == indexInvariantLanguage; }
        }

        public void SetUseInvariantLanguage(bool useInvariantLanguage)
        {
            comboLanguage.SelectedIndex = useInvariantLanguage ? indexInvariantLanguage : indexLocalizedLanguage;
        }

        public class ListItem
        {
            public ListItem(ViewSpec viewSpec)
            {
                ViewSpec = viewSpec;
            }

            public ViewSpec ViewSpec { get; private set; }
            public override string ToString()
            {
                return ViewSpec.Name;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            ShowShare();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            Import(ShareListDlg<ReportOrViewSpecList, ReportOrViewSpec>.GetImportFileName(this,
                TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)));
        }

        private ReportOrViewSpecList GetViewSpecList()
        {
            var currentList = new ReportOrViewSpecList();
            foreach (var view in _viewContext.CustomViews)
            {
                currentList.Add(new ReportOrViewSpec(view));
            }
            return currentList;
        }

        private void SetViewSpecList(IEnumerable<ReportOrViewSpec> reportOrViewSpecList)
        {
            var newViews = new List<ViewSpec>();
            var converter = new ReportSpecConverter(new SkylineDataSchema(_documentUiContainer, DataSchemaLocalizer.INVARIANT));
            foreach (var item in reportOrViewSpecList)
            {
                if (null != item.ViewSpec)
                {
                    newViews.Add(item.ViewSpec);
                }
                else
                {
                    var viewInfo = converter.Convert(item.ReportSpec);
                    newViews.Add(viewInfo.ViewSpec);
                }
            }
            ViewSettings.ViewSpecList = new ViewSpecList(newViews);
        }

        public void Import(string fileName)
        {
            var list = GetViewSpecList();
            if (ShareListDlg<ReportOrViewSpecList, ReportOrViewSpec>.ImportFile(this,
                list, fileName))
            {
                SetViewSpecList(list);
                LoadList();
            }
        }

        private ReportOrViewSpecList GetCurrentList()
        {
            var currentList = new ReportOrViewSpecList();
            foreach (var view in _viewContext.CustomViews)
            {
                currentList.Add(new ReportOrViewSpec(view));
            }
            return currentList;
        }

        public void ShowShare()
        {
            CheckDisposed();
            var currentList = GetCurrentList();
            using (var dlg = new ShareListDlg<ReportOrViewSpecList, ReportOrViewSpec>(currentList)
            {
                Label = Resources.ExportReportDlg_ShowShare_Report_Definitions,
                Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)
            })
            {
                dlg.ShowDialog(this);
            }
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            var viewInfo = GetReport();
            if (null == viewInfo)
            {
                return;
            }
            SkylineDataSchema dataSchema = new SkylineDataSchema(_documentUiContainer, GetDataSchemaLocalizer());
            var form = new DocumentGridForm(new DocumentGridViewContext(dataSchema))
            {
                ViewInfo = viewInfo,
                Text = Resources.ExportLiveReportDlg_ShowPreview_Preview__ + viewInfo.Name,
                ShowViewsMenu = false,
            };
            form.Show(Owner);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            var list = GetCurrentList();
            var newList = list.EditList(this, new DocumentGridViewContext(new SkylineDataSchema(_documentUiContainer, GetDataSchemaLocalizer())) {EnablePreview = true});
            if (null != newList)
            {
                _viewContext.SaveSettingsList(newList);
            }
            LoadList();
        }

        public void CancelClick()
        {
            DialogResult = btnCancel.DialogResult;
        }

        public string ReportName
        {
            get
            {
                var listItem = listboxReports.SelectedItem as ListItem;
                return null == listItem ? null : listItem.ViewSpec.Name;
            }
            set
            {
                listboxReports.SelectedIndex =
                    listboxReports.Items.Cast<ListItem>().ToList().FindIndex(item => item.ViewSpec.Name == value);
            }
        }
    }
}
