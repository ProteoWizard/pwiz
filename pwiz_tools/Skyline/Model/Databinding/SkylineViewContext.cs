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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
namespace pwiz.Skyline.Model.Databinding
{
    public class SkylineViewContext : AbstractViewContext
    {
        public SkylineViewContext(SkylineDataSchema dataSchema, IEnumerable<RowSourceInfo> rowSources) : base(dataSchema, rowSources)
        {
            ApplicationIcon = Resources.Skyline;
        }

        public SkylineViewContext(ColumnDescriptor parentColumn, IEnumerable rowSource)
            : base(
                parentColumn.DataSchema,
                new[] {new RowSourceInfo(rowSource, GetDefaultViewInfo(parentColumn))})
        {
            ApplicationIcon = Resources.Skyline;
        }

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

        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons)
        {
            return MessageBox.Show(owner, message, Program.Name, messageBoxButtons);
        }

        public override bool RunLongJob(Control owner, Action<IProgressMonitor> job)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                var status = longWaitDlg.PerformWork(owner == null ? null : owner.TopLevelControl, 1000, job);
                return status.IsComplete;
            }
        }

        public bool Export(Control owner, ViewInfo viewInfo)
        {
            using (var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = GetExportDirectory(),
                OverwritePrompt = true,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_CSV, TextUtil.FILTER_TSV),
                FileName = GetDefaultExportFilename(viewInfo),
            })
            {
                // TODO: If document has been saved, initial directory should be document directory
                if (saveFileDialog.ShowDialog(owner.TopLevelControl) == DialogResult.Cancel)
                {
                    return false;
                }
                char separator = saveFileDialog.FilterIndex == 2
                    ? TextUtil.SEPARATOR_TSV
                    : TextUtil.GetCsvSeparator(DataSchema.DataSchemaLocalizer.FormatProvider);
                return ExportToFile(owner, viewInfo, saveFileDialog.FileName, new DsvWriter(DataSchema.DataSchemaLocalizer.FormatProvider, separator));
            }
        }

        public bool ExportToFile(Control owner, ViewInfo viewInfo, string fileName, DsvWriter dsvWriter)
        {
            bool success = false;
            try
            {
                using (var saver = new FileSaver(fileName))
                {
                    if (!saver.CanSave(owner))
                        return false;


                    using (
                        var longWait = new LongWaitDlg
                        {
                            Text = Resources.ExportReportDlg_ExportReport_Generating_Report
                        })
                    {
                        var action = new Action<IProgressMonitor>(broker =>
                        {
                            var status = new ProgressStatus(Resources.ExportReportDlg_ExportReport_Building_report);
                            broker.UpdateProgress(status);
                            using (var writer = new StreamWriter(saver.SafeName))
                            {
                                Export(broker, ref status, viewInfo, writer, dsvWriter);
                                writer.Close();
                            }
                            saver.Commit();
                            broker.UpdateProgress(status.Complete());
                            success = true;
                        });
                        longWait.PerformWork(owner, 1500, action);
                    }
                    if (success)
                    {
                        SetExportDirectory(Path.GetDirectoryName(fileName));
                    }
                    return success;
                }
            }
            catch (Exception x)
            {
                Trace.TraceWarning("Error exporting to file: {0}", x); // Not L10N
                MessageDlg.ShowWithException(owner,
                    string.Format(Resources.ExportReportDlg_ExportReport_Failed_exporting_to, fileName, x.Message), x);
                return false;
            }
        }

        public bool Export(IProgressMonitor progressMonitor, ref ProgressStatus status, ViewInfo viewInfo, TextWriter writer, DsvWriter dsvWriter)
        {
            progressMonitor = progressMonitor ?? new CommandWaitBroker(new StringWriter(), status);
            using (var bindingListSource = new BindingListSource())
            {
                bindingListSource.SetViewContext(this, viewInfo);
                if (progressMonitor.IsCanceled)
                {
                    return false;
                }
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(95)
                    .ChangeMessage(Resources.ExportReportDlg_ExportReport_Writing_report));

                WriteData(progressMonitor, writer, bindingListSource, dsvWriter);
                if (progressMonitor.IsCanceled)
                    return false;

                writer.Flush();
                progressMonitor.UpdateProgress(status.Complete());
            }
            return true;
        }


        // Most of these strings are internal values, not user facing
        // ReSharper disable NonLocalizedString
        private static string GetColXml(string colName)
        {
            return string.Format("<column name='{0}'/>", colName);
        }
        /// <summary>
        /// Generate a report specification suitable to the current document that
        /// recreates the content in the Edit|Insert|TransitionList window
        /// </summary>
        private static ViewSpec GetTransitionListReportSpec(SkylineDataSchema dataSchema)
        {
            bool docHasCustomIons = dataSchema.Document.CustomIonCount != 0;
            bool docHasProteins = dataSchema.Document.PeptideCount != 0 ||
                                  dataSchema.Document.MoleculeCount == 0; // Empty doc is assumed proteomic
            // Different report content and name for different document types
            string name;
            if (docHasCustomIons && docHasProteins)
                name = Resources.SkylineViewContext_GetTransitionListReportSpec_Mixed_Transition_List;
            else if (docHasCustomIons)
                name = Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List;
            else
                name = Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List;
            var xml = String.Format(
                "<ReportSpecList> <report name='{0}' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>", name);
            xml += GetColXml("Precursor.Peptide.Protein.Name");
            if (docHasProteins)
                xml += GetColXml("Precursor.Peptide.ModifiedSequence");
            if (docHasCustomIons)
            {
                xml += GetColXml("Precursor.Peptide.IonName");
                xml += GetColXml("Precursor.Peptide.IonFormula");
            }
            xml += GetColXml("Precursor.Mz");
            xml += GetColXml("Precursor.Charge");
            xml += GetColXml("Precursor.CollisionEnergy");
            if (docHasCustomIons)
            {
                xml += GetColXml("Precursor.ExplicitCollisionEnergy");
                xml += GetColXml("Precursor.Peptide.ExplicitRetentionTime");
                xml += GetColXml("Precursor.Peptide.ExplicitRetentionTimeWindow");
                // Note: not including drift time info by default
            }
            xml += GetColXml("ProductMz");
            xml += GetColXml("ProductCharge");
            if (docHasProteins)
                xml += GetColXml("FragmentIon");
            if (docHasCustomIons)
                xml += GetColXml("ProductIonFormula");
            if (docHasProteins)
            {
                xml += GetColXml("FragmentIonType");
                xml += GetColXml("FragmentIonOrdinal");
                xml += GetColXml("CleavageAa");
                xml += GetColXml("LossNeutralMass");
                xml += GetColXml("Losses");
            }
            if (docHasProteins)
            {
                xml += GetColXml("LibraryRank");
                xml += GetColXml("LibraryIntensity");
                xml += GetColXml("IsotopeDistIndex");
                xml += GetColXml("IsotopeDistRank");
                xml += GetColXml("IsotopeDistProportion");
                xml += GetColXml("FullScanFilterWidth");
                xml += GetColXml("IsDecoy");
                xml += GetColXml("ProductDecoyMzShift");
            }
            xml += "  </report> </ReportSpecList>";
            var viewSpec =
                ReportSharing.DeserializeReportList(new MemoryStream(Encoding.UTF8.GetBytes(xml)))[0].ViewSpec;
            return viewSpec;
        }

        public override IEnumerable<ViewSpec> CustomViews
        {
            get
            {
                var viewSpecs = base.CustomViews.ToList();
                foreach (var convertedView in ConvertReports(Settings.Default.ReportSpecList))
                {
                    if (null == GetRowSourceInfo(convertedView))
                    {
                        continue;
                    }
                    if (viewSpecs.All(viewSpec => viewSpec.Name != convertedView.Name))
                    {
                        viewSpecs.Add(convertedView);
                    }
                }
                if (RowSources.Any(rowSource => rowSource.RowType == typeof (Entities.Transition)))
                {
                    // Add the appropriate "Transition List" report if it's not already in the settings
                    var spec = GetTransitionListReportSpec((SkylineDataSchema)DataSchema);
                    if (viewSpecs.All(viewSpec => viewSpec.Name != spec.Name))
                        viewSpecs.Add(spec);
                }
                return viewSpecs;
            }
        }

        protected IEnumerable<ViewSpec> ConvertReports(ReportSpecList reportSpecs)
        {
            var converter = new ReportSpecConverter((SkylineDataSchema)DataSchema);
            return converter.ConvertAll(reportSpecs);
        }

        protected override ViewSpecList GetViewSpecList()
        {
            return ViewSettings.ViewSpecList;
        }

        protected override void SaveViewSpecList(ViewSpecList viewSpecList)
        {
            ViewSettings.ViewSpecList = viewSpecList;
        }

        public static ViewInfo GetDefaultViewInfo(ColumnDescriptor columnDescriptor)
        {
            ViewSpec viewSpec = GetDefaultViewSpec(columnDescriptor);
            bool addAnnotations = false;
            if (columnDescriptor.PropertyType == typeof (TransitionResult))
            {
                viewSpec = viewSpec.SetColumns(new[]
                {
                    new ColumnSpec().SetName("PrecursorResult.PeptideResult.ResultFile.Replicate"),
                    new ColumnSpec().SetName("Note"),
                    new ColumnSpec().SetName("RetentionTime"),
                    new ColumnSpec().SetName("Fwhm"),
                    new ColumnSpec().SetName("StartTime"),
                    new ColumnSpec().SetName("EndTime"),
                    new ColumnSpec().SetName("Area"),
                    new ColumnSpec().SetName("Background"),
                    new ColumnSpec().SetName("AreaRatio"),
                    new ColumnSpec().SetName("Height"),
                    new ColumnSpec().SetName("PeakRank"),
                });
                addAnnotations = true;
            } 
            else if (columnDescriptor.PropertyType == typeof (PrecursorResult))
            {
                viewSpec = viewSpec.SetColumns(new[]
                {
                    new ColumnSpec().SetName("PeptideResult.ResultFile.Replicate"),
                    new ColumnSpec().SetName("Note"), 
                    new ColumnSpec().SetName("PrecursorPeakFoundRatio"),
                    new ColumnSpec().SetName("BestRetentionTime"),
                    new ColumnSpec().SetName("MaxFwhm"),
                    new ColumnSpec().SetName("MinStartTime"),
                    new ColumnSpec().SetName("MaxEndTime"),
                    new ColumnSpec().SetName("TotalArea"),
                    new ColumnSpec().SetName("TotalBackground"),
                    new ColumnSpec().SetName("TotalAreaRatio"),
                    new ColumnSpec().SetName("MaxHeight"),
                    new ColumnSpec().SetName("LibraryDotProduct"),
                    new ColumnSpec().SetName("IsotopeDotProduct"),
                });
                addAnnotations = true;
            }
            else if (columnDescriptor.PropertyType == typeof (PeptideResult))
            {
                viewSpec = viewSpec.SetColumns(new[]
                {
                    new ColumnSpec().SetName("ResultFile.Replicate"),
                    new ColumnSpec().SetName("PeptidePeakFoundRatio"), 
                    new ColumnSpec().SetName("PeptideRetentionTime"), 
                    new ColumnSpec().SetName("RatioToStandard"), 
                });
                
                var skylineDataSchema = (SkylineDataSchema)columnDescriptor.DataSchema;
                PropertyPath propertyPathReplicate = PropertyPath.Parse("ResultFile.Replicate");
                viewSpec = viewSpec.SetColumns(viewSpec.Columns.Concat(
                    skylineDataSchema.GetAnnotations(typeof (Replicate))
                        .Select(pd => new ColumnSpec(propertyPathReplicate.Property(pd.Name))))
                    );
            }
            else
            {
                var columnsToRemove = new HashSet<PropertyPath>();
                bool addRoot = false;
                bool docHasCustomIons = ((SkylineDataSchema)columnDescriptor.DataSchema).Document.CustomIonCount != 0;
                bool docHasOnlyCustomIons = docHasCustomIons && ((SkylineDataSchema)columnDescriptor.DataSchema).Document.PeptideCount == 0;
                
                if (columnDescriptor.PropertyType == typeof(Protein))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("Name"));
                    if (docHasOnlyCustomIons)
                    {
                        // Peptide-oriented fields that make no sense in a small molecule context
                        columnsToRemove.Add(PropertyPath.Root.Property("Accession"));
                        columnsToRemove.Add(PropertyPath.Root.Property("PreferredName"));
                        columnsToRemove.Add(PropertyPath.Root.Property("Gene"));
                        columnsToRemove.Add(PropertyPath.Root.Property("Species"));
                        columnsToRemove.Add(PropertyPath.Root.Property("Sequence"));
                    }
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Entities.Peptide))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("Sequence"));
                    columnsToRemove.Add(PropertyPath.Root.Property("PreviousAa"));
                    columnsToRemove.Add(PropertyPath.Root.Property("NextAa"));
                    if (docHasOnlyCustomIons)
                    {
                        // Peptide-oriented fields that make no sense in a small molecule context
                        columnsToRemove.Add(PropertyPath.Root.Property("ModifiedSequence"));
                        columnsToRemove.Add(PropertyPath.Root.Property("BeginPos"));
                        columnsToRemove.Add(PropertyPath.Root.Property("EndPos"));
                        columnsToRemove.Add(PropertyPath.Root.Property("MissedCleavages"));
                    }
                    if (!docHasCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("IonName"));
                        columnsToRemove.Add(PropertyPath.Root.Property("IonFormula"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ExplicitRetentionTime"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ExplicitRetentionTimeWindow"));
                    }
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Precursor))
                {
                    if (docHasOnlyCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("ModifiedSequence"));
                        columnsToRemove.Add(PropertyPath.Root.Property("IsDecoy"));
                    }
                    if (!docHasCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("ExplicitCollisionEnergy"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ExplicitDriftTimeMsec"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ExplicitDriftTimeHighEnergyOffsetMsec"));
                    }
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Entities.Transition))
                {
                    if (docHasOnlyCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("FragmentIonType"));  // Not interesting - always "custom"
                        columnsToRemove.Add(PropertyPath.Root.Property("FragmentIon")); // Not interesting - only one product per precursor for small molecules
                        columnsToRemove.Add(PropertyPath.Root.Property("FragmentIonOrdinal")); // Doesn't mean anything for non-peptides
                        columnsToRemove.Add(PropertyPath.Root.Property("CleavageAa")); // Doesn't mean anything for non-peptides
                        columnsToRemove.Add(PropertyPath.Root.Property("LossNeutralMass")); // Doesn't mean anything for non-peptides
                        columnsToRemove.Add(PropertyPath.Root.Property("Losses")); // Doesn't mean anything for non-peptides
                        columnsToRemove.Add(PropertyPath.Root.Property("IsDecoy")); // Doesn't mean anything for non-peptides
                        columnsToRemove.Add(PropertyPath.Root.Property("ProductDecoyMzShift")); // Doesn't mean anything for non-peptides
                    }
                    if (!docHasCustomIons)
                    {
                        // Stuff that only applies to small molecules
                        columnsToRemove.Add(PropertyPath.Root.Property("ProductIonFormula"));
                    }
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Replicate))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("Name"));
                    addRoot = true;
                }
                viewSpec = viewSpec.SetSublistId(GetReplicateSublist(columnDescriptor.PropertyType));
                if (addRoot)
                {
                    viewSpec = viewSpec.SetColumns(new[] { new ColumnSpec(PropertyPath.Root) }.Concat(viewSpec.Columns));
                }
                viewSpec = viewSpec.SetColumns(viewSpec.Columns
                    .Where(columnSpec => !columnsToRemove.Contains(columnSpec.PropertyPath)));
            }
            if (addAnnotations)
            {
                var skylineDataSchema = (SkylineDataSchema) columnDescriptor.DataSchema;
                viewSpec = viewSpec.SetColumns(viewSpec.Columns.Concat(
                    skylineDataSchema.GetAnnotations(columnDescriptor.PropertyType)
                        .Select(pd => new ColumnSpec(PropertyPath.Root.Property(pd.Name)))));
            }
            return new ViewInfo(columnDescriptor, viewSpec);
        }

        public static PropertyPath GetReplicateSublist(Type rowType)
        {
            if (rowType == typeof(SkylineDocument))
            {
                return PropertyPath.Root.Property("Replicates").LookupAllItems();
            }
            if (rowType == typeof(Replicate))
            {
                return PropertyPath.Root.Property("Files").LookupAllItems();
            }
            return PropertyPath.Root.Property("Results").LookupAllItems();
        }
        // ReSharper restore NonLocalizedString


        public static bool IsNumeric(Type type)
        {
            return type == typeof (int)
                   || type == typeof (int?)
                   || type == typeof (double)
                   || type == typeof (double?);
        }

        public override void ExportViews(Control owner, IEnumerable<ViewSpec> views)
        {
            using (var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)
            })
            {
                saveFileDialog.ShowDialog(owner);
                if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
                    var viewSpecList = new ViewSpecList(views);
                    using (FileSaver fs = new FileSaver(saveFileDialog.FileName))
                    {
                        if (!fs.CanSave(owner))
                            return;

                        using (FileStream stream = File.OpenWrite(fs.SafeName))
                        {
                            xmlSerializer.Serialize(stream, viewSpecList);
                            stream.Close();
                            fs.Commit();
                        }
                    }
                }
            }
        }

        public override void ImportViews(Control owner)
        {
            using (var importDialog = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports,
                        ReportSpecList.EXT_REPORTS)
            })
            {
                importDialog.ShowDialog(owner);
                if (string.IsNullOrEmpty(importDialog.FileName))
                {
                    return;
                }
                ViewSpec[] views;
                try
                {
                    views = LoadViews(importDialog.FileName).ToArray();
                }
                catch (Exception x)
                {
                    new MessageBoxHelper(owner.FindForm()).ShowXmlParsingError(string.Format(Resources.SkylineViewContext_ImportViews_Failure_loading__0__, importDialog.FileName),
                                                                 importDialog.FileName, x.InnerException);
                    return;
                }
                if (views.Length == 0)
                {
                    ShowMessageBox(owner, Resources.SkylineViewContext_ImportViews_No_views_were_found_in_that_file_, MessageBoxButtons.OK);
                    return;
                }
                var currentViews = CustomViews.ToList();
                var conflicts = new HashSet<string>(views.Select(view => view.Name));
                conflicts.IntersectWith(currentViews.Select(view=>view.Name));
                if (conflicts.Count > 0)
                {
                    var multipleMessage = TextUtil.LineSeparate(Resources.ShareListDlg_ImportFile_The_following_names_already_exist, string.Empty,
                                                    "{0}", string.Empty, Resources.ShareListDlg_ImportFile_Do_you_want_to_replace_them); // Not L10N
                    string messageFormat = conflicts.Count == 1 ?
                    Resources.ShareListDlg_ImportFile_The_name__0__already_exists_Do_you_want_to_replace_it :
                    multipleMessage;
                    var result = MessageBox.Show(string.Format(messageFormat, TextUtil.LineSeparate(conflicts)),
                                                 Program.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.None,
                                                 MessageBoxDefaultButton.Button2);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            currentViews = currentViews.Where(view => !conflicts.Contains(view.Name)).ToList();
                            break;
                    }
                }
                foreach (var view in views)
                {
                    // ReSharper disable once SimplifyLinqExpression
                    if (!currentViews.Any(currentView => currentView.Name == view.Name))
                    {
                        currentViews.Add(view);
                    }
                }
                SetCustomViews(currentViews);
            }
        }

        /// <summary>
        /// For testing
        /// </summary>
        /// <param name="fileName"></param>
        public int ImportViewsFromFile(string fileName)
        {
            ViewSpec[] views = LoadViews(fileName).ToArray();
            if (views.Length == 0)
            {
                return 0;
            }
            int count = 0;
            var currentViews = CustomViews.ToList();
            foreach (var view in views)
            {
                // ReSharper disable once SimplifyLinqExpression
                if (!currentViews.Any(currentView => currentView.Name == view.Name))
                {
                    currentViews.Add(view);
                    count++;
                }
            }
            SetCustomViews(currentViews);
            return count;
        }

        public void SaveSettingsList(IEnumerable<ReportOrViewSpec> reportOrViewSpecList)
        {
            if (null == reportOrViewSpecList)
            {
                return;
            }
            SaveViews(reportOrViewSpecList.Select(item=>item.ViewSpec));
        }

        public void SaveViews(IEnumerable<ViewSpec> viewSpecs)
        {
            SaveViewSpecList(new ViewSpecList(viewSpecs));
        }

        protected IEnumerable<ViewSpec> LoadViews(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var reportOrViewSpecs = ReportSharing.DeserializeReportList(stream);
                return ReportSharing.ConvertAll(reportOrViewSpecs, ((SkylineDataSchema) DataSchema).Document);
            }
        }

        public RowSourceInfo MakeRowSourceInfo<T>(IList<T> rows) where T : SkylineObject
        {
            return new RowSourceInfo(rows, GetDefaultViewInfo(ColumnDescriptor.RootColumn(DataSchema, typeof(T))));
        }

        public void SetRowSources(IList<RowSourceInfo> rowSources)
        {
            RowSources = rowSources;
        }

        public static IEnumerable<RowSourceInfo> GetDocumentGridRowSources(SkylineDataSchema dataSchema)
        {
            yield return MakeRowSource(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins, new Proteins(dataSchema));
            yield return MakeRowSource(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides, new Peptides(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors, new Precursors(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions, new Transitions(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates, new ReplicateList(dataSchema));
            yield return new RowSourceInfo(typeof(SkylineDocument), new SkylineDocument[0], new ViewInfo[0]);
        }

        public static IEnumerable<RowSourceInfo> GetAllRowSources(SkylineDataSchema dataSchema)
        {
            return GetDocumentGridRowSources(dataSchema);
        }
        private static RowSourceInfo MakeRowSource<T>(SkylineDataSchema dataSchema, string name, IEnumerable<T> rows)
        {
            var parentColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(T));
            var viewInfo = new ViewInfo(parentColumn, GetDefaultViewInfo(parentColumn).GetViewSpec().SetName(name));
            return new RowSourceInfo(rows, viewInfo);
        }

        protected override TColumn InitializeColumn<TColumn>(TColumn column, PropertyDescriptor propertyDescriptor)
        {
            column = base.InitializeColumn(column, propertyDescriptor);
            // Disable sorting by clicking on column headers because DataboundGridForm shows
            // context menu on left click.
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
            return column;
        }
    }
}
