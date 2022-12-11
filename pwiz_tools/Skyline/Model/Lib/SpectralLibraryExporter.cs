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

using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;

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
                            ProcessTransitionGroup(spectra, nodePepGroup, nodePep, nodeTranGroup, i);
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
                IrtDb.CreateIrtDb(path).UpdatePeptides(rCalcIrt.GetDbIrtPeptides().ToList(), progressMonitor, ref status);
            }
        }

        private void ProcessTransitionGroup(IDictionary<LibKey, SpectrumMzInfo> spectra,
            PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, int replicateIndex)
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
            var maxApexMs1 = float.MinValue;
            string chromFileName = null;
            double? mobilityMs1 = null; // Track MS1 ion mobility in order to derive high energy ion mobility offset value
            foreach (var nodeTran in nodeTranGroup.Transitions)
            {
                var chromInfos = nodeTran.GetSafeChromInfo(replicateIndex);
                if (chromInfos.IsEmpty)
                    continue;
                var chromInfo = chromInfos.First(info => info.OptimizationStep == 0);
                if (chromInfo.Area == 0)
                    continue;
                if (nodeTran.IsMs1)
                {
                    // Track MS1 ion mobility in order to derive high energy ion mobility offset value
                    if (chromInfo.Height > maxApexMs1)
                    {
                        maxApexMs1 = chromInfo.Height;
                        mobilityMs1 = chromInfo.IonMobility.IonMobility.Mobility;
                    }
                    continue;
                }
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
                    var mobility = chromInfo.IonMobility.IonMobility;
                    var mobilityHighEnergyOffset = 0.0;
                    if (mobilityMs1.HasValue && mobility.HasValue && mobility.Mobility != mobilityMs1)
                    {
                        // Note any difference in MS1 and MS2 ion mobilities - the "high energy ion mobility offset"
                        mobilityHighEnergyOffset = mobility.Mobility.Value - mobilityMs1.Value;
                        mobility = mobility.ChangeIonMobility(mobilityMs1);
                    }
                    im = IonMobilityAndCCS.GetIonMobilityAndCCS(mobility, chromInfo.IonMobility.CollisionalCrossSectionSqA ?? imGroup.CollisionalCrossSection, mobilityHighEnergyOffset);
                }
            }
            if (chromFileName == null)
                return;
            SpectrumMzInfo spectrumMzInfo;
            var isBest = replicateIndex == nodePep.BestResult;
            if (!spectra.TryGetValue(key, out spectrumMzInfo) || isBest)
            {
                spectrumMzInfo = new SpectrumMzInfo
                {
                    SourceFile = DocumentFilePath,
                    Key = key,
                    PrecursorMz = nodeTranGroup.PrecursorMz,
                    SpectrumPeaks = new SpectrumPeaksInfo(mi.ToArray()),
                    RetentionTimes = spectrumMzInfo?.RetentionTimes ?? new List<SpectrumMzInfo.IonMobilityAndRT>(),
                    IonMobility = im,
                    Protein = nodePepGroup.Name,
                    RetentionTime = rt
                };
                spectra[key] = spectrumMzInfo;
            }
            spectrumMzInfo.RetentionTimes.Add(new SpectrumMzInfo.IonMobilityAndRT(chromFileName, im, rt, isBest));
        }
    }
}
