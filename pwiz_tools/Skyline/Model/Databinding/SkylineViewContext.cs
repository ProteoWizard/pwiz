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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Databinding.RowActions;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
namespace pwiz.Skyline.Model.Databinding
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
                    skylineWindow.ModifyDocument(Resources.SkylineViewContext_SaveViewSpecList_Change_Document_Reports, doc =>
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
                skylineWindow.ModifyDocument(Resources.SkylineViewContext_ChangeDocumentViewSpec_Change_Document_Reports, doc =>
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

        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons)
        {
            return new AlertDlg(message, messageBoxButtons).ShowAndDispose(FormUtil.FindTopLevelOwner(owner));
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
                if (saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner)) == DialogResult.Cancel)
                {
                    return false;
                }
                char separator = saveFileDialog.FilterIndex == 2
                    ? TextUtil.SEPARATOR_TSV
                    : TextUtil.GetCsvSeparator(DataSchema.DataSchemaLocalizer.FormatProvider);
                SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
                return ExportToFile(owner, viewInfo, saveFileDialog.FileName, GetDsvWriter(separator));
            }
        }

        public bool IsInvariantLanguage()
        {
            return ReferenceEquals(DataSchema.DataSchemaLocalizer, DataSchemaLocalizer.INVARIANT);
        }

        public DsvWriter GetDsvWriter(char separator)
        {
            DsvWriter dsvWriter = new DsvWriter(DataSchema.DataSchemaLocalizer.FormatProvider, DataSchema.DataSchemaLocalizer.Language, separator);
            if (IsInvariantLanguage())
            {
                dsvWriter.NumberFormatOverride = Formats.RoundTrip;
            }
            return dsvWriter;
        }

        public DsvWriter GetCsvWriter()
        {
            return GetDsvWriter(TextUtil.GetCsvSeparator(DataSchema.DataSchemaLocalizer.FormatProvider));
        }

        public bool ExportToFile(Control owner, ViewInfo viewInfo, string fileName, DsvWriter dsvWriter)
        {
            try
            {
                return SafeWriteToFile(owner, fileName, stream =>
                {
                    bool success = false;
                    using (
                        var longWait = new LongWaitDlg
                        {
                            Text = Resources.ExportReportDlg_ExportReport_Generating_Report
                        })
                    {
                        var action = new Action<IProgressMonitor>(progressMonitor =>
                        {
                            IProgressStatus status = new ProgressStatus(Resources.ExportReportDlg_ExportReport_Building_report);
                            progressMonitor.UpdateProgress(status);
                            using (var writer = new StreamWriter(stream))
                            {
                                success = Export(longWait.CancellationToken, progressMonitor, ref status, viewInfo, writer, dsvWriter);
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
                Trace.TraceWarning(@"Error exporting to file: {0}", x);
                MessageDlg.ShowWithException(owner,
                    string.Format(Resources.ExportReportDlg_ExportReport_Failed_exporting_to, fileName, x.Message), x);
                return false;
            }
        }

        public bool Export(CancellationToken cancellationToken, IProgressMonitor progressMonitor,
            ref IProgressStatus status, ViewInfo viewInfo, TextWriter writer, DsvWriter dsvWriter)
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

            return Export(cancellationToken, progressMonitor, ref status, viewInfo, viewLayout, writer, dsvWriter);
        }

        public bool Export(CancellationToken cancellationToken, IProgressMonitor progressMonitor, ref IProgressStatus status, ViewInfo viewInfo, ViewLayout viewLayout, TextWriter writer, DsvWriter dsvWriter)
        {
            progressMonitor = progressMonitor ?? new SilentProgressMonitor();
            using (var bindingListSource = new BindingListSource(cancellationToken))
            {
                bindingListSource.SetViewContext(this, viewInfo);
                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(5)
                    .ChangeMessage(Resources.ExportReportDlg_ExportReport_Writing_report));

                WriteDataWithStatus( progressMonitor, ref status, writer, bindingListSource, dsvWriter);
                if (progressMonitor.IsCanceled)
                    return false;

                writer.Flush();
                progressMonitor.UpdateProgress(status = status.Complete());
            }
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

        // ReSharper disable LocalizableElement
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
                var columnsToRemove = new HashSet<PropertyPath> 
                    {PropertyPath.Root.Property("Locator")};
                bool addRoot = false;
                bool docHasCustomIons = ((SkylineDataSchema)columnDescriptor.DataSchema).Document.CustomIonCount != 0;
                bool docHasOnlyCustomIons = docHasCustomIons && ((SkylineDataSchema)columnDescriptor.DataSchema).Document.PeptideCount == 0;
                
                if (columnDescriptor.PropertyType == typeof(Protein))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("Name"));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Protein.AutoSelectPeptides)));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Protein.ProteinSequenceCoverage)));
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
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Entities.Peptide.AutoSelectPrecursors)));
                    columnsToRemove.Add(PropertyPath.Root.Property("Sequence"));
                    columnsToRemove.Add(PropertyPath.Root.Property("SequenceLength"));
                    columnsToRemove.Add(PropertyPath.Root.Property("PreviousAa"));
                    columnsToRemove.Add(PropertyPath.Root.Property("NextAa"));
                    columnsToRemove.Add(PropertyPath.Root.Property("RetentionTimeCalculatorScore"));
                    columnsToRemove.Add(PropertyPath.Root.Property("DocumentLocation"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ConcentrationMultiplier"));
                    columnsToRemove.Add(PropertyPath.Root.Property("InternalStandardConcentration"));
                    columnsToRemove.Add(PropertyPath.Root.Property("CalibrationCurve"));
                    columnsToRemove.Add(PropertyPath.Root.Property("FiguresOfMerit"));
                    columnsToRemove.Add(PropertyPath.Root.Property("NormalizationMethod"));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Entities.Peptide.AutoSelectPrecursors)));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Entities.Peptide.AttributeGroupId)));
                    foreach (var prop in MoleculeAccessionNumbers.PREFERRED_ACCESSION_TYPE_ORDER)
                        columnsToRemove.Add(PropertyPath.Root.Property(prop)); // By default don't show CAS, InChI etc
                    if (docHasOnlyCustomIons)
                    {
                        // Peptide-oriented fields that make no sense in a small molecule context
                        columnsToRemove.Add(PropertyPath.Root.Property("ModifiedSequence"));
                        columnsToRemove.Add(PropertyPath.Root.Property(nameof(Entities.Peptide.FirstPosition)));
                        columnsToRemove.Add(PropertyPath.Root.Property(nameof(Entities.Peptide.LastPosition)));
                        columnsToRemove.Add(PropertyPath.Root.Property("MissedCleavages"));
                    }
                    if (!docHasCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("MoleculeName"));
                        columnsToRemove.Add(PropertyPath.Root.Property("MoleculeFormula"));
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
                        columnsToRemove.Add(PropertyPath.Root.Property("IonName"));
                        columnsToRemove.Add(PropertyPath.Root.Property("IonFormula"));
                        columnsToRemove.Add(PropertyPath.Root.Property("NeutralFormula"));
                        columnsToRemove.Add(PropertyPath.Root.Property("Adduct"));
                    }
                    columnsToRemove.Add(PropertyPath.Root.Property("CollisionEnergy"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ResultSummary"));
                    columnsToRemove.Add(PropertyPath.Root.Property("NeutralMass"));
                    columnsToRemove.Add(PropertyPath.Root.Property("TransitionCount"));
                    columnsToRemove.Add(PropertyPath.Root.Property("DeclusteringPotential"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LibraryScore1"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LibraryScore2"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LibraryScore3"));
                    columnsToRemove.Add(PropertyPath.Root.Property("IsDecoy"));
                    columnsToRemove.Add(PropertyPath.Root.Property("DecoyMzShift"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitDriftTimeMsec"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitCollisionalCrossSection"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitIonMobility"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitIonMobilityUnits"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitCompensationVoltage"));
                    columnsToRemove.Add(PropertyPath.Root.Property("PrecursorConcentration"));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Precursor.AutoSelectTransitions)));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Precursor.TargetQualitativeIonRatio)));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Precursor.LibraryIonMobility)));
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Entities.Transition))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitCollisionEnergy"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitDriftTimeHighEnergyOffsetMsec"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitIonMobilityHighEnergyOffset"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitCompensationVoltage"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitDeclusteringPotential"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitSLens"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ExplicitConeVoltage"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ResultSummary"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ProductNeutralMass"));
                    columnsToRemove.Add(PropertyPath.Root.Property("FragmentIonType"));
                    columnsToRemove.Add(PropertyPath.Root.Property("FragmentIonOrdinal"));
                    columnsToRemove.Add(PropertyPath.Root.Property("CleavageAa"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LossNeutralMass"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LossFormulas"));
                    columnsToRemove.Add(PropertyPath.Root.Property("IsDecoy"));
                    columnsToRemove.Add(PropertyPath.Root.Property("ProductDecoyMzShift"));
                    columnsToRemove.Add(PropertyPath.Root.Property("IsotopeDistIndex"));
                    columnsToRemove.Add(PropertyPath.Root.Property("IsotopeDistRank"));
                    columnsToRemove.Add(PropertyPath.Root.Property("FullScanFilterWidth"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LibraryRank"));
                    columnsToRemove.Add(PropertyPath.Root.Property("LibraryIntensity"));
                    columnsToRemove.Add(PropertyPath.Root.Property("IsotopeDistProportion"));

                    if (docHasOnlyCustomIons)
                    {
                        columnsToRemove.Add(PropertyPath.Root.Property("FragmentIon")); // Not interesting - only one product per precursor for small molecules
                        columnsToRemove.Add(PropertyPath.Root.Property("Losses")); // Doesn't mean anything for non-peptides
                    }
                    if (!docHasCustomIons)
                    {
                        // Stuff that only applies to small molecules
                        columnsToRemove.Add(PropertyPath.Root.Property("ProductIonFormula"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ProductNeutralFormula"));
                        columnsToRemove.Add(PropertyPath.Root.Property("ProductAdduct"));
                    }
                    addRoot = true;
                }
                else if (columnDescriptor.PropertyType == typeof(Replicate))
                {
                    columnsToRemove.Add(PropertyPath.Root.Property("Name"));
                    columnsToRemove.Add(PropertyPath.Root.Property("SampleDilutionFactor"));
                    columnsToRemove.Add(PropertyPath.Root.Property(nameof(Replicate.BatchName)));
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
            return new ViewInfo(columnDescriptor, viewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);
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

            if (rowType == typeof(Protein))
            {
                return PropertyPath.Root.Property(nameof(Protein.Results)).LookupAllItems().Property("Value")
                    .Property(nameof(Replicate.Files));
            }
            return PropertyPath.Root.Property("Results").LookupAllItems();
        }
        // ReSharper restore LocalizableElement

        public override void ExportViews(Control owner, ViewSpecList viewSpecList)
        {
            using (var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)
            })
            {
                saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner));
                if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    ExportViewsToFile(owner, viewSpecList, saveFileDialog.FileName);
                }
            }
        }

        public override void ExportViewsToFile(Control owner, ViewSpecList viewSpecList, string fileName)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            SafeWriteToFile(owner, fileName, stream =>
            {
                xmlSerializer.Serialize(stream, viewSpecList);
                return true;
            });
        }

        public override void ImportViews(Control owner, ViewGroup group)
        {
            using (var importDialog = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports,
                    ReportSpecList.EXT_REPORTS)
            })
            {
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
                    string.Format(Resources.SkylineViewContext_ImportViews_Failure_loading__0__, fileName),
                    fileName, x.InnerException ?? x);
                return;
            }
            if (!views.ViewSpecs.Any())
            {
                ShowMessageBox(owner, Resources.SkylineViewContext_ImportViews_No_views_were_found_in_that_file_,
                    MessageBoxButtons.OK);
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
                proteomic ? Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins : Resources.SkylineViewContext_GetDocumentGridRowSources_Molecule_Lists,
                () => new Proteins(dataSchema));
            yield return MakeRowSource<Entities.Peptide>(dataSchema, 
                proteomic ? Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides : Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules,
                () => new Peptides(dataSchema, new[] {IdentityPath.ROOT}));
            yield return MakeRowSource<Precursor>(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors, 
                () => new Precursors(dataSchema, new[] { IdentityPath.ROOT }));
            yield return MakeRowSource<Entities.Transition>(dataSchema, Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions, 
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
            {typeof (Entities.Protein).FullName, Tuple.Create(1, 6)},
            {typeof (Entities.Peptide).FullName, Tuple.Create(2, 7)},
            {typeof (Entities.Precursor).FullName, Tuple.Create(3, 3)},
            {typeof (Entities.Transition).FullName, Tuple.Create(4, 4)},
            {typeof (Entities.Replicate).FullName, Tuple.Create(5, 5)}
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
            };
        }

        public override int GetImageIndex(ViewSpec viewSpec)
        {
            Tuple<int, int> imageIndex;
            if (_imageIndexes.TryGetValue(viewSpec.RowSource, out imageIndex))
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
    }
}
