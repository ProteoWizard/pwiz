/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding.AuditLog;
using pwiz.Skyline.Controls.Databinding.RowActions;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Databinding
{
    public class SkylineViewContext : AbstractViewContext
    {
        private ViewChangeListener _viewChangeListener;
        public SkylineViewContext(SkylineDataSchema dataSchema, IEnumerable<RowSourceInfo> rowSources) : base(dataSchema, rowSources)
        {
            ApplicationIcon = Resources.Skyline;
        }

        public SkylineViewContext(ColumnDescriptor parentColumn, IRowSource rowSource)
            : base(
                parentColumn.DataSchema,
                new[] {new RowSourceInfo(rowSource, GetDefaultViewInfo(parentColumn))})
        {
            ApplicationIcon = Resources.Skyline;
        }

        public override IEnumerable<ViewGroup> ViewGroups
        {
            get
            {
                return new[]
                {
                    PersistedViews.MainGroup,
                    PersistedViews.ExternalToolsGroup,
                };
            }
        }

        public override ViewGroup DefaultViewGroup
        {
            get { return PersistedViews.MainGroup; }
        }

        public override ViewSpecList GetViewSpecList(ViewGroupId viewGroup)
        {
            return base.GetViewSpecList(viewGroup)
                ?? SortViewSpecList(Settings.Default.PersistedViews.GetViewSpecList(viewGroup)) 
                ?? ViewSpecList.EMPTY;
        }

        private ViewSpecList SortViewSpecList(ViewSpecList viewSpecList)
        {
            var viewSpecs = viewSpecList.ViewSpecs.ToArray();
            var stringComparer = StringComparer.Create(SkylineDataSchema.DataSchemaLocalizer.FormatProvider, true);
            Array.Sort(viewSpecs, (v1,v2)=>stringComparer.Compare(v1.Name, v2.Name));
            return new ViewSpecList(viewSpecs, viewSpecList.ViewLayouts);
        }

        public override void AddOrReplaceViews(ViewGroupId groupId, IEnumerable<ViewSpecLayout> viewSpecs)
        {
            var viewSpecsArray = ImmutableList.ValueOf(viewSpecs);
            if (Equals(groupId, PersistedViews.MainGroup.Id))
            {
                ChangeDocumentViewSpecList(viewSpecList => viewSpecList.AddOrReplaceViews(viewSpecsArray));
            }
            base.AddOrReplaceViews(groupId, viewSpecsArray);
        }

        public override void DeleteViews(ViewGroupId groupId, IEnumerable<string> viewNames)
        {
            var viewNameSet = new HashSet<string>(viewNames);
            if (Equals(groupId, PersistedViews.MainGroup.Id))
            {
                ChangeDocumentViewSpecList(viewSpecList => viewSpecList.DeleteViews(viewNameSet));
            }
            base.DeleteViews(groupId, viewNameSet);
        }

        public override bool TryRenameView(ViewGroupId groupId, string oldName, string newName)
        {
            if (!base.TryRenameView(groupId, oldName, newName))
            {
                return false;
            }
            if (Equals(groupId, PersistedViews.MainGroup.Id))
            {
                ChangeDocumentViewSpecList(viewSpecList => viewSpecList.RenameView(oldName, newName));
            }
            return true;
        }

        protected override void WriteDataWithStatus(IProgressMonitor progressMonitor, ref IProgressStatus status, TextWriter writer,
            RowItemEnumerator rowItemEnumerator, char separator)
        {
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(rowItemEnumerator.ItemProperties);
            if (true != replicatePivotColumns?.HasConstantAndVariableColumns())
            {
                base.WriteDataWithStatus(progressMonitor, ref status, writer, rowItemEnumerator, separator);
                return;
            }
            var dsvWriter = CreateDsvWriter(separator, rowItemEnumerator.ColumnFormats);

            // Filter columns for main grid to be written out.
            var filteredColumnDescriptors = rowItemEnumerator.ItemProperties.OfType<ColumnPropertyDescriptor>()
                .Where(columnDescriptor => !replicatePivotColumns.IsConstantColumn(columnDescriptor)).ToList();

            // Count main grid columns to add spaces.
            var replicateVariablePropertyCounts = filteredColumnDescriptors.GroupBy(column => column.PivotKey ?? PivotKey.EMPTY)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Count());
            // Create header line with property column and replicate headers.
            var propertyCaption = LocalizationHelper.CallWithCulture(DataSchema.DataSchemaLocalizer.Language,
                () => Model.Databinding.DatabindingResources.SkylineViewContext_WriteDataWithStatus_Property);
            var headerLine = Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[PivotKey.EMPTY] - 1)
                .Prepend(propertyCaption).ToList();
            var propertyLineDictionary = new Dictionary<PropertyPath, List<string>>();
            var propertyLines = new List<List<string>> { headerLine };
            var allRowItems = rowItemEnumerator.GetRowItems();
            foreach (var group in replicatePivotColumns.GetReplicateColumnGroups())
            {
                // Add replicate to header line.
                headerLine.Add(group.Key.ReplicateName);
                headerLine.AddRange(Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[group.ToList()[0].PivotKey] - 1));

                foreach (var column in group)
                {
                    if (!replicatePivotColumns.IsConstantColumn(column) || column.DisplayColumn.ColumnDescriptor == null)
                    {
                        continue;
                    }

                    var propertyPath = column.DisplayColumn.PropertyPath;
                    if (!propertyLineDictionary.TryGetValue(propertyPath, out var propertyLine))
                    {
                        // Create a new row if property line does not exist yet.
                        var propertyDisplayName = column.DisplayColumn.ColumnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
                        propertyLine = Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[PivotKey.EMPTY] - 1).Prepend(propertyDisplayName).ToList();
                        propertyLineDictionary[propertyPath] = propertyLine;
                        propertyLines.Add(propertyLine);
                    }

                    // Add value to property line.
                    var rowItem = allRowItems.FirstOrDefault(item => column.GetValue(item) != null);
                    var formattedValue = dsvWriter.GetFormattedValue(rowItem, column);
                    propertyLine.AddRange(Enumerable.Repeat(string.Empty, replicateVariablePropertyCounts[column.PivotKey] - 1).Prepend(formattedValue));
                }
            }

            // Write pivot replicate data.
            foreach (var line in propertyLines)
            {
                dsvWriter.WriteRowValues(writer, line);
            }
            writer.WriteLine();
            var newRowItemEnumerator = new RowItemEnumerator(allRowItems, new ItemProperties(filteredColumnDescriptors), rowItemEnumerator.ColumnFormats);
            base.WriteDataWithStatus(progressMonitor, ref status, writer, newRowItemEnumerator, separator);
        }

        protected override void SaveViewSpecList(ViewGroupId viewGroup, ViewSpecList viewSpecList)
        {
            Settings.Default.PersistedViews.SetViewSpecList(viewGroup, viewSpecList);
            if (Equals(viewGroup, PersistedViews.MainGroup.Id))
            {
                ChangeDocumentViewSpecList(docViewSpecList =>
                {
                    var newViews = new Dictionary<string, ViewSpec>();
                    foreach (var viewSpec in viewSpecList.ViewSpecs)
                    {
                        newViews[viewSpec.Name] = viewSpec;
                    }
                    var newDocViews = new List<ViewSpec>();
                    var newLayouts = new List<ViewLayoutList>();
                    foreach (var oldDocView in docViewSpecList.ViewSpecs)
                    {
                        ViewSpec newDocView;
                        if (newViews.TryGetValue(oldDocView.Name, out newDocView))
                        {
                            newDocViews.Add(newDocView);
                            ViewLayoutList viewLayoutList = viewSpecList.GetViewLayouts(oldDocView.Name);
                            if (!viewLayoutList.IsEmpty)
                            {
                                newLayouts.Add(viewLayoutList);
                            }
                        }
                    }
                    return new ViewSpecList(newDocViews, newLayouts);
                });

                var skylineWindow = SkylineDataSchema.SkylineWindow;
                if (skylineWindow != null)
                {
                    skylineWindow.ModifyDocument(DatabindingResources.SkylineViewContext_SaveViewSpecList_Change_Document_Reports, doc =>
                    {
                        var oldViewNames = new HashSet<string>(
                            doc.Settings.DataSettings.ViewSpecList.ViewSpecs.Select(spec => spec.Name));
                        var newViewSpecList = viewSpecList.Filter(spec => oldViewNames.Contains(spec.Name));
                        if (Equals(newViewSpecList, doc.Settings.DataSettings.ViewSpecList))
                        {
                            return doc;
                        }
                        return doc.ChangeSettings(doc.Settings.ChangeDataSettings(
                            doc.Settings.DataSettings.ChangeViewSpecList(newViewSpecList)));
                    }, AuditLogEntry.SettingsLogFunction);
                }
            }
        }

        protected void ChangeDocumentViewSpecList(Func<ViewSpecList, ViewSpecList> changeViewSpecFunc)
        {
            var skylineWindow = SkylineDataSchema.SkylineWindow;
            if (skylineWindow != null)
            {
                skylineWindow.ModifyDocument(DatabindingResources.SkylineViewContext_ChangeDocumentViewSpec_Change_Document_Reports, doc =>
                {
                    var oldViewSpecList = doc.Settings.DataSettings.ViewSpecList;
                    var newViewSpecList = changeViewSpecFunc(oldViewSpecList);
                    if (Equals(newViewSpecList, oldViewSpecList))
                    {
                        return doc;
                    }
                    return doc.ChangeSettings(doc.Settings.ChangeDataSettings(
                        doc.Settings.DataSettings.ChangeViewSpecList(newViewSpecList)));
                }, AuditLogEntry.SettingsLogFunction);
            }
            
        }

        public SkylineDataSchema SkylineDataSchema { get { return (SkylineDataSchema) DataSchema; } }

        public override string GetExportDirectory()
        {
            return Settings.Default.ExportDirectory;
        }

        protected override string GetDefaultExportFilename(ViewInfo viewInfo)
        {
            return viewInfo.Name;
        }

        public override void SetExportDirectory(string value)
        {
            Settings.Default.ExportDirectory = value;
        }

        protected override IEnumerable<TabularFileFormat> ListAvailableExportFormats()
        {
            yield return new TabularFileFormat(TextUtil.GetCsvSeparator(DataSchema.DataSchemaLocalizer.FormatProvider),
                TextUtil.FILTER_CSV);
            yield return new TabularFileFormat('\t', TextUtil.FILTER_TSV);
        }

        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons, Exception exception)
        {
            return new AlertDlg(message, messageBoxButtons)
            {
                Exception = exception
            }.ShowAndDispose(FormUtil.FindTopLevelOwner(owner));
        }

        public override bool RunLongJob(Control owner, Action<CancellationToken, IProgressMonitor> job)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                var status = longWaitDlg.PerformWork(FormUtil.FindTopLevelOwner(owner), 1000, progressMonitor => job(longWaitDlg.CancellationToken, progressMonitor));
                return status.IsComplete;
            }
        }

        public override bool RunOnThisThread(Control owner, Action<CancellationToken, IProgressMonitor> job)
        {
            var longOperationRunner = new LongOperationRunner();
            bool finished = false;
            longOperationRunner.Run(longWaitBroker =>
            {
                var progressWaitBroker = new ProgressWaitBroker(progressMonitor=>job(longWaitBroker.CancellationToken, progressMonitor));
                progressWaitBroker.PerformWork(longWaitBroker);
                finished = !longWaitBroker.IsCanceled;
            });
            return finished;
        }

        protected override void SetClipboardText(Control owner, string text)
        {
            ClipboardHelper.SetClipboardText(owner, text);
        }

        public bool Export(Control owner, ViewInfo viewInfo)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.InitialDirectory = GetExportDirectory();
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.DefaultExt = TextUtil.EXT_CSV;
                saveFileDialog.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_CSV, TextUtil.FILTER_TSV);
                saveFileDialog.FileName = GetDefaultExportFilename(viewInfo);
                // TODO: If document has been saved, initial directory should be document directory
                if (saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner)) == DialogResult.Cancel)
                {
                    return false;
                }
                char separator = saveFileDialog.FilterIndex == 2
                    ? TextUtil.SEPARATOR_TSV
                    : TextUtil.GetCsvSeparator(DataSchema.DataSchemaLocalizer.FormatProvider);
                SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
                return ExportToFile(owner, viewInfo, saveFileDialog.FileName, separator);
            }
        }

        public bool IsInvariantLanguage()
        {
            return ReferenceEquals(DataSchema.DataSchemaLocalizer, DataSchemaLocalizer.INVARIANT);
        }

        public override DsvWriter CreateDsvWriter(char separator, ColumnFormats columnFormats)
        {
            var dsvWriter = base.CreateDsvWriter(separator, columnFormats);
            if (IsInvariantLanguage())
            {
                dsvWriter.NumberFormatOverride = Formats.RoundTrip;
            }

            return dsvWriter;
        }

        public DsvWriter GetDsvWriter(char separator)
        {
            return CreateDsvWriter(separator, null);
        }

        public bool ExportToFile(Control owner, ViewInfo viewInfo, string fileName, char separator)
        {
            try
            {
                return SafeWriteToFile(owner, fileName, stream =>
                {
                    bool success = false;
                    using (var longWait = new LongWaitDlg())
                    {
                        longWait.Text = DatabindingResources.ExportReportDlg_ExportReport_Generating_Report;
                        var action = new Action<IProgressMonitor>(progressMonitor =>
                        {
                            IProgressStatus status = new ProgressStatus(DatabindingResources.ExportReportDlg_ExportReport_Building_report);
                            progressMonitor.UpdateProgress(status);
                            using (var writer = new StreamWriter(stream))
                            {
                                success = Export(longWait.CancellationToken, progressMonitor, ref status, viewInfo, writer, separator);
                                writer.Close();
                            }
                            if (success)
                            {
                                progressMonitor.UpdateProgress(status.Complete());
                            }
                        });
                        longWait.PerformWork(owner, 1500, action);
                    }
                    return success;
                });
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(owner,
                    string.Format(DatabindingResources.ExportReportDlg_ExportReport_Failed_exporting_to, fileName, x.Message), x);
                return false;
            }
        }

        public bool Export(CancellationToken cancellationToken, IProgressMonitor progressMonitor,
            ref IProgressStatus status, ViewInfo viewInfo, TextWriter writer, char separator)
        {
            ViewLayout viewLayout = null;
            if (viewInfo.ViewGroup != null)
            {
                var viewLayoutList = GetViewLayoutList(viewInfo.ViewGroup.Id.ViewName(viewInfo.Name));
                if (viewLayoutList != null)
                {
                    viewLayout = viewLayoutList.DefaultLayout;
                }
            }

            return Export(cancellationToken, progressMonitor, ref status, viewInfo, viewLayout, writer, separator);
        }

        public bool Export(CancellationToken cancellationToken, IProgressMonitor progressMonitor, ref IProgressStatus status, ViewInfo viewInfo, ViewLayout viewLayout, TextWriter writer, char separator)
        {
            progressMonitor ??= new SilentProgressMonitor(cancellationToken);
            RowItemEnumerator rowItemEnumerator;
            using (var bindingListSource = new BindingListSource(cancellationToken))
            {
                bindingListSource.SetViewContext(this, viewInfo);
                if (viewLayout != null)
                {
                    foreach (var column in viewLayout.ColumnFormats)
                    {
                        bindingListSource.ColumnFormats.SetFormat(column.Item1, column.Item2);
                    }
                }

                rowItemEnumerator = RowItemEnumerator.FromBindingListSource(bindingListSource);
            }

            progressMonitor.UpdateProgress(status = status.ChangePercentComplete(5)
                .ChangeMessage(DatabindingResources.ExportReportDlg_ExportReport_Writing_report));
            WriteDataWithStatus(progressMonitor, ref status, writer, rowItemEnumerator, separator);
            if (progressMonitor.IsCanceled)
                return false;
            writer.Flush();
            progressMonitor.UpdateProgress(status = status.Complete());
            return true;
        }

        protected override bool SafeWriteToFile(Control owner, string fileName, Func<Stream, bool> writeFunc)
        {
            using (var fileSaver = new FileSaver(fileName, true))
            {
                if (!fileSaver.CanSave(owner))
                {
                    return false;
                }
                if (writeFunc(fileSaver.Stream))
                {
                    fileSaver.Commit();
                    return true;
                }
            }
            return false;
        }

        public static ViewInfo GetDefaultViewInfo(ColumnDescriptor columnDescriptor)
        {
            var builtInReports = new BuiltInReports(((SkylineDataSchema)columnDescriptor.DataSchema).Document);
            var columns = builtInReports.GetDefaultColumns(columnDescriptor.PropertyType);
            ViewSpec viewSpec;
            if (columns == null)
            {
                viewSpec = GetDefaultViewSpec(columnDescriptor);
            }
            else
            {
                viewSpec = new ViewSpec().SetRowType(columnDescriptor.PropertyType)
                    .SetColumns(columns.Select(pp => new ColumnSpec(pp)));
                viewSpec = viewSpec.SetSublistId(SublistPaths.GetReplicateSublist(columnDescriptor.PropertyType));
            }

            return new ViewInfo(columnDescriptor, viewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);
        }

        public override void ExportViews(Control owner, ViewSpecList viewSpecList)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.InitialDirectory = Settings.Default.ActiveDirectory;
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.Filter = TextUtil.FileDialogFilterAll(DatabindingResources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS);
                saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner));
                if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    ExportViewsToFile(owner, viewSpecList, saveFileDialog.FileName);
                }
            }
        }

        public override void ExportViewsToFile(Control owner, ViewSpecList viewSpecList, string fileName)
        {
            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
                SafeWriteToFile(owner, fileName, stream =>
                {
                    xmlSerializer.Serialize(stream, viewSpecList);
                    return true;
                });
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(owner,
                    string.Format(DatabindingResources.ExportReportDlg_ExportReport_Failed_exporting_to, fileName, x.Message), x);
            }
        }

        public override void ImportViews(Control owner, ViewGroup group)
        {
            using (var importDialog = new OpenFileDialog())
            {
                importDialog.InitialDirectory = Settings.Default.ActiveDirectory;
                importDialog.CheckPathExists = true;
                importDialog.Filter = TextUtil.FileDialogFilterAll(DatabindingResources.ExportReportDlg_ShowShare_Skyline_Reports,
                    ReportSpecList.EXT_REPORTS);
                importDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner));

                if (string.IsNullOrEmpty(importDialog.FileName))
                {
                    return;
                }
                ImportViewsFromFile(owner, group, importDialog.FileName);
            }
        }

        public override void ImportViewsFromFile(Control owner, ViewGroup group, string fileName)
        {
            ViewSpecList views;
            try
            {
                views = LoadViews(fileName);
            }
            catch (Exception x)
            {
                new MessageBoxHelper(owner.FindForm()).ShowXmlParsingError(
                    string.Format(DatabindingResources.SkylineViewContext_ImportViews_Failure_loading__0__, fileName),
                    fileName, x.InnerException ?? x);
                return;
            }
            if (!views.ViewSpecs.Any())
            {
                ShowMessageBox(owner, DatabindingResources.SkylineViewContext_ImportViews_No_views_were_found_in_that_file_,
                    MessageBoxButtons.OK, null);
                return;
            }
            CopyViewsToGroup(owner, group, views);
        }

        protected ViewSpecList LoadViews(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var reportOrViewSpecs = ReportSharing.DeserializeReportList(stream);
                return new ViewSpecList(ReportSharing.ConvertAll(reportOrViewSpecs, ((SkylineDataSchema) DataSchema).Document));
            }
        }

        public void SetRowSources(IList<RowSourceInfo> rowSources)
        {
            RowSources = rowSources;
        }

        public static IEnumerable<RowSourceInfo> GetDocumentGridRowSources(SkylineDataSchema dataSchema)
        {
            bool proteomic = dataSchema.DefaultUiMode == UiModes.PROTEOMIC;
            yield return MakeRowSource<Protein>(dataSchema, 
                proteomic ? Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins : DatabindingResources.SkylineViewContext_GetDocumentGridRowSources_Molecule_Lists,
                () => new Proteins(dataSchema));
            yield return MakeRowSource<Model.Databinding.Entities.Peptide>(dataSchema, 
                proteomic ? Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides : Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules,
                () => new Peptides(dataSchema, new[] {IdentityPath.ROOT}));
            yield return MakeRowSource<Precursor>(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors, 
                () => new Precursors(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource<Model.Databinding.Entities.Transition>(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions, 
                () => new Transitions(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource<Replicate>(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates, 
                () => new ReplicateList(dataSchema));
            yield return new RowSourceInfo(typeof(SkylineDocument), new StaticRowSource(new SkylineDocument[0]), new ViewInfo[0]);
        }

        private static RowSourceInfo MakeRowSource<T>(SkylineDataSchema dataSchema, string name, Func<IRowSource> rowProvider) where T : SkylineObject
        {
            var parentColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(T));
            var viewInfo = new ViewInfo(parentColumn, GetDefaultViewInfo(parentColumn).GetViewSpec().SetName(name));
            return new RowSourceInfo(rowProvider(), viewInfo);
        }

        protected override TColumn InitializeColumn<TColumn>(TColumn column, PropertyDescriptor propertyDescriptor)
        {
            column = base.InitializeColumn(column, propertyDescriptor);
            // Disable sorting by clicking on column headers because DataboundGridForm shows
            // context menu on left click.
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
            if (IsInvariantLanguage())
            {
                // When outputting floats and doubles as "Invariant" (e.g. for external tools) 
                // always use the Round Trip number format
                var type = propertyDescriptor.PropertyType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FormattableList<>))
                {
                    type = type.GenericTypeArguments[0];
                }
                if (type == typeof (float) ||
                    type == typeof (float?) ||
                    type == typeof (double) ||
                    type == typeof (double?))
                {
                    column.DefaultCellStyle.Format = Formats.RoundTrip;
                }
            }
            return column;
        }

        private static readonly IDictionary<string, Tuple<int, int>> _imageIndexes = new Dictionary<string, Tuple<int, int>>
        {
            // ReSharper disable RedundantNameQualifier
            // ReSharper disable AssignNullToNotNullAttribute
            {typeof (Model.Databinding.Entities.Protein).FullName, Tuple.Create(1, 6)},
            {typeof (Model.Databinding.Entities.Peptide).FullName, Tuple.Create(2, 7)},
            {typeof (Model.Databinding.Entities.Precursor).FullName, Tuple.Create(3, 3)},
            {typeof (Model.Databinding.Entities.Transition).FullName, Tuple.Create(4, 4)},
            {typeof (Model.Databinding.Entities.Replicate).FullName, Tuple.Create(5, 5)},
            {typeof (Model.AuditLog.Databinding.AuditLogRow).FullName, Tuple.Create(8, 8)}
            // ReSharper restore AssignNullToNotNullAttribute
            // ReSharper restore RedundantNameQualifier
        };

        public override Image[] GetImageList()
        {
            return new []
            {
                Resources.Folder,
                Resources.Protein,
                Resources.Peptide,
                Resources.TransitionGroup,
                Resources.Fragment,
                Resources.Replicate,
                Resources.MoleculeList,
                Resources.Molecule,
                Resources.AuditLog,
            };
        }

        public override int GetImageIndex(ViewSpec viewSpec)
        {
            Tuple<int, int> imageIndex;
            if (_imageIndexes.TryGetValue(viewSpec.RowSource ?? string.Empty, out imageIndex))
            {
                if (DataSchema.NormalizeUiMode(viewSpec.UiMode) == UiModes.PROTEOMIC)
                {
                    return imageIndex.Item1;
                }
                return imageIndex.Item2;
            }
            return -1;
        }

        public override event Action ViewsChanged
        {
            add
            {
                if (_viewChangeListener == null)
                {
                    _viewChangeListener = new ViewChangeListener(Settings.Default.PersistedViews, SkylineDataSchema);
                }
                _viewChangeListener.AddListener(value);
            }
            remove
            {
                _viewChangeListener.RemoveListener(value);
            }
        }

        private class ViewChangeListener : IDocumentChangeListener
        {
            private readonly PersistedViews _persistedViews;
            private readonly SkylineDataSchema _skylineDataSchema;
            private ViewSpecList _documentViews;
            private HashSet<Action> _listeners;
            public ViewChangeListener(PersistedViews persistedViews, SkylineDataSchema skylineDataSchema)
            {
                _persistedViews = persistedViews;
                _skylineDataSchema = skylineDataSchema;
            }

            public void AddListener(Action listener)
            {
                if (_listeners == null)
                {
                    if (null != _persistedViews)
                    {
                        _persistedViews.Changed += PersistedViewsOnChanged;
                    }
                    if (null != _skylineDataSchema)
                    {
                        _documentViews = _skylineDataSchema.Document.Settings.DataSettings.ViewSpecList;
                        _skylineDataSchema.Listen(this);
                    }
                    _listeners = new HashSet<Action>();
                }
                if (!_listeners.Add(listener))
                {
                    throw new InvalidOperationException(@"Listener already added");
                }
            }

            public void RemoveListener(Action listener)
            {
                if (!_listeners.Remove(listener))
                {
                    throw new InvalidOperationException(@"No such listener");
                }
                if (_listeners.Count == 0)
                {
                    if (null != _persistedViews)
                    {
                        _persistedViews.Changed -= PersistedViewsOnChanged;
                    }
                    if (null != _skylineDataSchema)
                    {
                        _skylineDataSchema.Unlisten(this);
                    }
                    _listeners = null;
                }
            }

            private void PersistedViewsOnChanged()
            {
                FireChanged();
            }

            void IDocumentChangeListener.DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                var viewSpecList = _skylineDataSchema.Document.Settings.DataSettings.ViewSpecList;
                if (!Equals(viewSpecList, _documentViews))
                {
                    _documentViews = viewSpecList;
                    FireChanged();
                }
            }

            private void FireChanged()
            {
                foreach (var listener in _listeners.ToArray())
                {
                    listener();
                }
            }
        }

        protected void DeleteSkylineDocNodes(BoundDataGridView dataGridView, IEnumerable<SkylineDocNode> docNodes)
        {
            DeleteNodesAction.DeleteSkylineDocNodes(SkylineDataSchema.SkylineWindow, dataGridView, docNodes);
        }

        public override IEnumerable<IUiModeInfo> AvailableUiModes
        {
            get
            {
                var skylineWindow = SkylineDataSchema.SkylineWindow;
                if (skylineWindow == null)
                {
                    return UiModes.AllModes;
                }

                return UiModes.AvailableModes(SkylineDataSchema.SkylineWindow.ModeUI);
            }
        }

        protected override DataGridViewColumn CreateCustomColumn(PropertyDescriptor propertyDescriptor)
        {
            Type columnType = null;

            // First, check for DataTypeSpecifierAttribute (marker type for properties with generic types like string)
            var dataTypeSpecifier = propertyDescriptor.Attributes.OfType<DataTypeSpecifierAttribute>().FirstOrDefault();
            if (dataTypeSpecifier != null)
            {
                PropertyTypeToColumnTypeMap.TryGetValue(dataTypeSpecifier.MarkerType, out columnType);
            }

            // Second, check UI-side mapping from property type to column type
            // Check exact type match first, then check if property type is assignable from mapped type (for base classes)
            if (columnType == null)
            {
                if (!PropertyTypeToColumnTypeMap.TryGetValue(propertyDescriptor.PropertyType, out columnType))
                {
                    // Check if property type derives from any mapped base type
                    foreach (var kvp in PropertyTypeToColumnTypeMap)
                    {
                        if (kvp.Key.IsAssignableFrom(propertyDescriptor.PropertyType))
                        {
                            columnType = kvp.Value;
                            break;
                        }
                    }
                }
            }

            // If we found a column type, instantiate it
            if (columnType != null)
            {
                try
                {
                    var constructor = columnType.GetConstructor(Array.Empty<Type>());
                    if (constructor != null)
                    {
                        return (DataGridViewColumn)constructor.Invoke(Array.Empty<object>());
                    }
                }
                catch (Exception exception)
                {
                    Assume.Fail(string.Format(@"Exception constructing column of type {0}:{1}", columnType, exception));
                }
            }

            // Fall back to base class implementation
            return base.CreateCustomColumn(propertyDescriptor);
        }

        /// <summary>
        /// UI-side mapping from property types to DataGridView column types.
        /// This mapping allows the UI to control which column types are used for specific property types,
        /// reducing backward dependencies from Model to UI.
        /// Marker types (used with DataTypeSpecifierAttribute) allow properties with generic types
        /// like string to specify which UI column type should be used without changing the property type.
        /// </summary>
        private static readonly Dictionary<Type, Type> PropertyTypeToColumnTypeMap = new Dictionary<Type, Type>
        {
            // Audit log types map to AuditLogColumn
            { typeof(AuditLogRow.AuditLogRowText), typeof(AuditLogColumn) },
            
            // Model types with distinct types map to their UI column types
            { typeof(NormalizationMethod), typeof(NormalizationMethodDataGridViewColumn) },
            { typeof(ListItem), typeof(ListLookupDataGridViewColumn) },
            { typeof(SampleType), typeof(SampleTypeDataGridViewColumn) },
            { typeof(StandardType), typeof(StandardTypeDataGridViewColumn) },
            
            // Marker types for properties with generic types (like string, bool)
            { typeof(SurrogateStandardName), typeof(SurrogateStandardDataGridViewColumn) },
            { typeof(AnnotationPropertyDescriptor.TrueFalseAnnotation), typeof(DataGridViewCheckBoxColumn) },
            { typeof(AnnotationPropertyDescriptor.ValueListAnnotation), typeof(AnnotationValueListDataGridViewColumn) }
        };

        public override bool CanDisplayView(ViewSpec viewSpec)
        {
            if (!base.CanDisplayView(viewSpec))
            {
                return false;
            }

            var reportUiMode = DataSchema.NormalizeUiMode(viewSpec.UiMode);
            switch (DataSchema.DefaultUiMode)
            {
                case UiModes.PROTEOMIC:
                    return reportUiMode != UiModes.SMALL_MOLECULES;
                case UiModes.SMALL_MOLECULES:
                    return reportUiMode != UiModes.PROTEOMIC;
            }

            return true;
        }

        protected override ViewEditor CreateViewEditor(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var viewEditor = base.CreateViewEditor(viewGroup, viewSpec);
            viewEditor.Alphabetical = Settings.Default.AlphabeticalReportEditor;
            viewEditor.Closed += ViewEditorClosed;
            return viewEditor;
        }

        private void ViewEditorClosed(object sender, EventArgs eventArgs)
        {
            var viewEditor = sender as ViewEditor;
            if (viewEditor?.DialogResult == DialogResult.OK)
            {
                Settings.Default.AlphabeticalReportEditor = viewEditor.Alphabetical;
            }
        }
    }
}
