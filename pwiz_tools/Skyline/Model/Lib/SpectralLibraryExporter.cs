/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public class SpectralLibraryExporter
    {
        public SpectralLibraryExporter(SrmDocument document, string documentFilePath)
        {
            Document = document;
            DocumentFilePath = documentFilePath;
        }

        public string DocumentFilePath { get; private set; }
        public SrmDocument Document { get; private set; }

        public void ExportSpectralLibrary(string path, IProgressMonitor progressMonitor)
        {
            const string name = "exported";
            var spectra = new Dictionary<LibKey, SpectrumMzInfo>();
            foreach (var nodePepGroup in Document.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Molecules)
                {
                    foreach (var nodeTranGroup in nodePep.TransitionGroups)
                    {
                        for (var i = 0; i < Document.Settings.MeasuredResults.Chromatograms.Count; i++)
                        {
                            ProcessTransitionGroup(spectra, nodePep, nodeTranGroup, i);
                        }
                    }
                }
            }

            var rCalcIrt = Document.Settings.HasRTPrediction
                ? Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt
                : null;
            IProgressStatus status = new ProgressStatus();
            if (rCalcIrt != null && progressMonitor != null)
            {
                progressMonitor.UpdateProgress(status = status.ChangeSegments(0, 2));
            }

            using (var blibDb = BlibDb.CreateBlibDb(path))
            {
                var libSpec = new BiblioSpecLiteSpec(name, path);
                blibDb.CreateLibraryFromSpectra(libSpec, spectra.Values.ToList(), name, progressMonitor, ref status);
            }

            if (rCalcIrt != null)
            {
                IrtDb.CreateIrtDb(path).AddPeptides(progressMonitor, rCalcIrt.GetDbIrtPeptides().ToList(), ref status);
            }
        }

        private void ProcessTransitionGroup(IDictionary<LibKey, SpectrumMzInfo> spectra, 
            PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, int replicateIndex)
        {
            LibKey key;
            if (nodePep.IsProteomic)
            {
                var sequence = Document.Settings.GetPrecursorCalc(nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods)
                    .GetModifiedSequence(nodePep.Peptide.Target, SequenceModFormatType.lib_precision, false);
                key = new LibKey(sequence, nodeTranGroup.PrecursorAdduct.AdductCharge);
            }
            else
            {
                // For small molecules, the "modification" is expressed in the adduct
                key = new LibKey(nodeTranGroup.CustomMolecule.GetSmallMoleculeLibraryAttributes(), nodeTranGroup.PrecursorAdduct);
            }
            var mi = new List<SpectrumPeaksInfo.MI>();
            var rt = 0.0;
            var im = IonMobilityAndCCS.EMPTY;
            var imGroup = TransitionGroupIonMobilityInfo.EMPTY; // CCS may be available only at group level
            var groupChromInfos = nodeTranGroup.GetSafeChromInfo(replicateIndex);
            if (!groupChromInfos.IsEmpty)
            {
                var chromInfo = groupChromInfos.First(info => info.OptimizationStep == 0);
                imGroup = chromInfo.IonMobilityInfo;
            }
            var maxApex = float.MinValue;
            string chromFileName = null;
            foreach (var nodeTran in nodeTranGroup.Transitions)
            {
                if (nodeTran.IsMs1)
                    continue;
                var chromInfos = nodeTran.GetSafeChromInfo(replicateIndex);
                if (chromInfos.IsEmpty)
                    continue;
                var chromInfo = chromInfos.First(info => info.OptimizationStep == 0);
                if (chromInfo.Area == 0)
                    continue;
                if (chromFileName == null)
                {
                    var chromFileInfo = Document.Settings.MeasuredResults.Chromatograms[replicateIndex].MSDataFileInfos.FirstOrDefault(file => ReferenceEquals(file.Id, chromInfo.FileId));
                    if (chromFileInfo != null)
                        chromFileName = chromFileInfo.FilePath.GetFileName();
                }
                List<SpectrumPeakAnnotation> annotations = null;
                if (nodeTran.Transition.IsNonReporterCustomIon()) // CONSIDER(bspratt) include annotation for all non-peptide-fragment transitions?
                {
                    var smallMoleculeLibraryAttributes = nodeTran.Transition.CustomIon.GetSmallMoleculeLibraryAttributes();
                    var ion = new CustomIon(smallMoleculeLibraryAttributes, nodeTran.Transition.Adduct, nodeTran.GetMoleculeMass());
                    annotations = new List<SpectrumPeakAnnotation> { SpectrumPeakAnnotation.Create(ion, nodeTran.Annotations.Note) };
                }
                mi.Add(new SpectrumPeaksInfo.MI { Mz = nodeTran.Mz, Intensity = chromInfo.Area, Quantitative = nodeTran.ExplicitQuantitative, Annotations = annotations });
                if (chromInfo.Height > maxApex)
                {
                    maxApex = chromInfo.Height;
                    rt = chromInfo.RetentionTime;
                    im = IonMobilityAndCCS.GetIonMobilityAndCCS(chromInfo.IonMobility.IonMobility, chromInfo.IonMobility.CollisionalCrossSectionSqA ?? imGroup.CollisionalCrossSection, 0);
                }
            }
            if (chromFileName == null)
                return;
            SpectrumMzInfo spectrumMzInfo;
            if (!spectra.TryGetValue(key, out spectrumMzInfo))
            {
                spectrumMzInfo = new SpectrumMzInfo
                {
                    SourceFile = DocumentFilePath,
                    Key = key,
                    PrecursorMz = nodeTranGroup.PrecursorMz,
                    SpectrumPeaks = new SpectrumPeaksInfo(mi.ToArray()),
                    RetentionTimes = new List<SpectrumMzInfo.IonMobilityAndRT>(),
                    IonMobility = im,
                    RetentionTime = rt
                };
                spectra[key] = spectrumMzInfo;
            }
            var isBest = replicateIndex == nodePep.BestResult;
            if (isBest)
            {
                spectrumMzInfo.IonMobility = im;
                spectrumMzInfo.RetentionTime = rt;
            }
            spectrumMzInfo.RetentionTimes.Add(new SpectrumMzInfo.IonMobilityAndRT(chromFileName, im, rt, isBest));
        }

        public void ShowExportSpectralLibraryDialog(Control owner)
        {
            if (Document.MoleculeTransitionGroupCount == 0)
            {
                MessageDlg.Show(owner, Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_at_least_one_peptide_precursor_to_export_a_spectral_library_);
                return;
            }
            else if (!Document.Settings.HasResults)
            {
                MessageDlg.Show(owner, Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_results_to_export_a_spectral_library_);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Export_Spectral_Library,
                OverwritePrompt = true,
                DefaultExt = BiblioSpecLiteSpec.EXT,
                Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB)
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);

                if (dlg.ShowDialog(owner) == DialogResult.Cancel)
                    return;

                try
                {
                    using (var longWaitDlg = new LongWaitDlg
                    {
                        Text = Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Export_Spectral_Library,
                        Message = string.Format(Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Exporting_spectral_library__0____, Path.GetFileName(dlg.FileName))
                    })
                    {
                        longWaitDlg.PerformWork(owner, 800, monitor => ExportSpectralLibrary(dlg.FileName, monitor));
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(owner, TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Failed_exporting_spectral_library_to__0__, dlg.FileName), x.Message), x);
                }
            }
        }
    }
}
