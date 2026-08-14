/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Applies <see cref="PeakBoundaryChange"/>s to a single molecule.
    /// <para>
    /// The chromatograms for a precursor are read from the .skyd file once, for all replicates at a time,
    /// and then reused for every change to that precursor. That matters when many peaks on the same molecule
    /// are being changed, as when importing a peak boundaries file: going through
    /// <see cref="SrmDocument.ChangePeak(IdentityPath,string,pwiz.CommonMsData.MsDataFileUri,Transition,double?,double?,UserSet,PeakIdentification?,bool)"/>
    /// instead would re-read and re-match the chromatograms for every change, and would rebuild the whole
    /// document each time.
    /// </para>
    /// <para>
    /// One integrator is used by one thread. Separate molecules can be integrated on separate threads,
    /// because the only thing shared between them is the read-only document.
    /// </para>
    /// </summary>
    public class MoleculeIntegrator
    {
        private static readonly Identity[] NONE_MATCHED = Array.Empty<Identity>();

        private readonly SrmDocument _document;
        private readonly PeptideDocNode _nodeMoleculeOriginal;
        /// <summary>
        /// Chromatograms for each precursor that has been changed, keyed on the precursor
        /// <see cref="Identity"/> and indexed by replicate
        /// </summary>
        private readonly Dictionary<ReferenceValue<Identity>, IList<IList<ChromatogramGroupInfo>>> _chromatogramGroupInfos
            = new Dictionary<ReferenceValue<Identity>, IList<IList<ChromatogramGroupInfo>>>();

        public MoleculeIntegrator(SrmDocument document, PeptideDocNode nodeMolecule)
        {
            _document = document;
            _nodeMoleculeOriginal = nodeMolecule;
            MoleculeDocNode = nodeMolecule;
            UserSet = UserSet.IMPORTED;
            ChangePeaks = true;
        }

        /// <summary>
        /// The molecule with all of the changes applied so far
        /// </summary>
        public PeptideDocNode MoleculeDocNode { get; private set; }

        /// <summary>
        /// How the changed peaks are marked as having been set. Defaults to <see cref="UserSet.IMPORTED"/>.
        /// </summary>
        public UserSet UserSet { get; set; }

        /// <summary>
        /// When false, only the annotations on a <see cref="PeakBoundaryChange"/> are applied and the peak
        /// boundaries themselves are left alone. Defaults to true.
        /// </summary>
        public bool ChangePeaks { get; set; }

        /// <summary>
        /// Passed through to
        /// <see cref="TransitionGroupDocNode.ChangePeak(DocSettings.SrmSettings,ChromatogramGroupInfo,int,ChromFileInfoId,DocSettings.OptimizableRegression,Transition,double?,double?,PeakIdentification,UserSet,bool)"/>.
        /// Defaults to false.
        /// </summary>
        public bool PreserveMissingPeaks { get; set; }

        /// <summary>
        /// Applies a change to every precursor of the molecule which matches its charge and which has
        /// results for its file.
        /// </summary>
        /// <returns>
        /// The <see cref="Identity"/> of each precursor that was changed, which is empty when no precursor
        /// of this molecule matched
        /// </returns>
        public IList<Identity> ApplyChange(PeakBoundaryChange change)
        {
            List<Identity> matched = null;
            var nodeMolecule = MoleculeDocNode;
            for (int i = 0; i < nodeMolecule.Children.Count; i++)
            {
                // Refetch the precursor each time, because applying a change replaces the molecule
                var nodeGroup = (TransitionGroupDocNode) nodeMolecule.Children[i];
                if (!change.MatchesAdduct(nodeGroup.TransitionGroup.PrecursorAdduct))
                    continue;
                if (!nodeGroup.ChromInfos.Any(info => ReferenceEquals(info.FileId, change.FileId)))
                    continue;
                var nodeGroupNew = ChangePeak(nodeGroup, change);
                if (!ReferenceEquals(nodeGroup, nodeGroupNew))
                    nodeMolecule = (PeptideDocNode) nodeMolecule.ReplaceChild(nodeGroupNew);
                if (matched == null)
                    matched = new List<Identity>();
                matched.Add(nodeGroup.Id);
            }
            MoleculeDocNode = nodeMolecule;
            return matched ?? (IList<Identity>) NONE_MATCHED;
        }

        private TransitionGroupDocNode ChangePeak(TransitionGroupDocNode nodeGroup, PeakBoundaryChange change)
        {
            var nodeGroupNew = nodeGroup;
            if (change.Annotations != null && change.Annotations.Count > 0)
                nodeGroupNew = nodeGroupNew.AddPrecursorAnnotations(change.FileId, change.Annotations);
            if (!ChangePeaks)
                return nodeGroupNew;

            var settings = _document.Settings;
            var chromatogramSet = settings.MeasuredResults.Chromatograms[change.ReplicateIndex];
            var identified = GetPeakIdentification(nodeGroup, chromatogramSet, change);
            var chromGroupInfo = FindChromatogramGroupInfo(nodeGroup, chromatogramSet, change);
            return (TransitionGroupDocNode) nodeGroupNew.ChangePeak(settings, chromGroupInfo,
                change.ReplicateIndex, change.FileId, chromatogramSet.OptimizationFunction, null,
                change.StartTime, change.EndTime, identified, UserSet, PreserveMissingPeaks);
        }

        private PeakIdentification GetPeakIdentification(TransitionGroupDocNode nodeGroup,
            ChromatogramSet chromatogramSet, PeakBoundaryChange change)
        {
            // A peak that is being removed is never identified
            if (!change.StartTime.HasValue || !change.EndTime.HasValue)
                return PeakIdentification.FALSE;
            return _document.GetPeakIdentification(_nodeMoleculeOriginal,
                nodeGroup.TransitionGroup.PrecursorAdduct, chromatogramSet, change.FilePath,
                change.StartTime.Value, change.EndTime.Value);
        }

        private ChromatogramGroupInfo FindChromatogramGroupInfo(TransitionGroupDocNode nodeGroup,
            ChromatogramSet chromatogramSet, PeakBoundaryChange change)
        {
            var chromInfos = GetChromatogramGroupInfos(nodeGroup)[change.ReplicateIndex];
            if (chromInfos.Count == 0)
            {
                throw new ArgumentOutOfRangeException(string.Format(
                    ModelResources.SrmDocument_ChangePeak_No_results_found_for_the_precursor__0__in_the_replicate__1__,
                    TransitionGroupDocNode.GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz, string.Empty),
                    chromatogramSet.Name));
            }
            var chromGroupInfo = chromInfos.FirstOrDefault(info => Equals(change.FilePath, info.FilePath));
            if (chromGroupInfo == null)
            {
                throw new ArgumentOutOfRangeException(string.Format(
                    ModelResources.SrmDocument_ChangePeak_No_results_found_for_the_precursor__0__in_the_file__1__,
                    TransitionGroupDocNode.GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz, string.Empty),
                    change.FilePath));
            }
            return chromGroupInfo;
        }

        /// <summary>
        /// Returns the chromatograms for a precursor, indexed by replicate, reading them from the .skyd file
        /// the first time the precursor is changed. All of the replicates are read at once, and their peaks
        /// are all read in a single pass, because a molecule whose peak boundaries are being set usually has
        /// them set in every replicate.
        /// </summary>
        private IList<IList<ChromatogramGroupInfo>> GetChromatogramGroupInfos(TransitionGroupDocNode nodeGroup)
        {
            IList<IList<ChromatogramGroupInfo>> chromatogramGroupInfos;
            if (_chromatogramGroupInfos.TryGetValue(nodeGroup.Id, out chromatogramGroupInfos))
                return chromatogramGroupInfos;

            var settings = _document.Settings;
            // Match against the original precursor: changing peaks never changes which chromatograms match
            var nodeGroupOriginal = (TransitionGroupDocNode) _nodeMoleculeOriginal.FindNode(nodeGroup.Id);
            chromatogramGroupInfos = settings.MeasuredResults.LoadChromatogramsForAllReplicates(
                _nodeMoleculeOriginal, nodeGroupOriginal,
                (float) settings.TransitionSettings.Instrument.MzMatchTolerance);
            ChromatogramGroupInfo.LoadPeaksForAll(chromatogramGroupInfos.SelectMany(list => list), false);
            _chromatogramGroupInfos.Add(nodeGroup.Id, chromatogramGroupInfos);
            return chromatogramGroupInfos;
        }
    }
}
