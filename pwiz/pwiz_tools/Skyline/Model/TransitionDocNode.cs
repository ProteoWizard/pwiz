/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionDocNode : DocNode
    {
        public TransitionDocNode(Transition id,
                                 TransitionLosses losses,
                                 double massH,
                                 TransitionIsotopeDistInfo isotopeDistInfo,
                                 TransitionLibInfo libInfo)
            : this(id, Annotations.EMPTY, losses, massH, isotopeDistInfo, libInfo, null)
        {
        }

        public TransitionDocNode(Transition id,
                                 Annotations annotations,
                                 TransitionLosses losses,
                                 double massH,
                                 TransitionIsotopeDistInfo isotopeDistInfo,
                                 TransitionLibInfo libInfo,
                                 Results<TransitionChromInfo> results)
            : base(id, annotations)
        {
            Losses = losses;
            if (losses != null)
                massH -= losses.Mass;
            Mz = SequenceMassCalc.GetMZ(massH, id.Charge);
            if (id.DecoyMassShift.HasValue)
                Mz += id.DecoyMassShift.Value;
            IsotopeDistInfo = isotopeDistInfo;
            LibInfo = libInfo;
            Results = results;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        public TransitionLossKey Key { get { return new TransitionLossKey(Transition, Losses); } }

        public double Mz { get; private set; }

        public bool IsDecoy { get { return Transition.DecoyMassShift.HasValue; } }

        public TransitionLosses Losses { get; private set; }

        public bool HasLoss { get { return Losses != null; } }

        public double LostMass { get { return HasLoss ? Losses.Mass : 0; } }

        public bool IsLossPossible(int maxLossMods, IList<StaticMod> modsLossAvailable)
        {
            if (HasLoss)
            {
                var losses = Losses.Losses;
                if (losses.Count > maxLossMods)
                    return false;
                foreach (var loss in losses)
                {
                    // If the same precursor mod exists, then it will also have the
                    // loss in question, since modification equality depends on loss
                    // equality also.
                    if (!modsLossAvailable.Contains(loss.PrecursorMod))
                        return false;
                }
            }
            return true;
        }

        public string FragmentIonName
        {
            get
            {
                string ionName = Transition.FragmentIonName;
                return (HasLoss ? string.Format("{0} -{1}", ionName, Math.Round(Losses.Mass, 1)) : ionName);
            }
        }

        /// <summary>
        /// Returns true for a transition that would be filtered from MS1 in full-scan filtering.
        /// </summary>
        public bool IsMs1
        {
            get { return Transition.IsPrecursor() && Losses == null; }
        }

        public TransitionIsotopeDistInfo IsotopeDistInfo { get; private set; }

        public bool HasDistInfo { get { return IsotopeDistInfo != null; }}

        public static TransitionIsotopeDistInfo GetIsotopeDistInfo(Transition transition, IsotopeDistInfo isotopeDist)
        {
            if (isotopeDist == null || !transition.IsPrecursor())
                return null;
            return new TransitionIsotopeDistInfo(isotopeDist.GetRankI(transition.MassIndex),
                isotopeDist.GetProportionI(transition.MassIndex));
        }

        public static bool IsValidIsotopeTransition(Transition transition, IsotopeDistInfo isotopeDist)
        {
            if (isotopeDist == null || !transition.IsPrecursor())
                return true;
            int i = isotopeDist.MassIndexToPeakIndex(transition.MassIndex);
            return 0 <= i && i < isotopeDist.CountPeaks;
        }

        public TransitionLibInfo LibInfo { get; private set; }

        public bool HasLibInfo { get { return LibInfo != null; } }

        public static TransitionLibInfo GetLibInfo(Transition transition, double massH,
                                                   IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> ranks)
        {
            LibraryRankedSpectrumInfo.RankedMI rmi;
            if (ranks == null || !ranks.TryGetValue(SequenceMassCalc.GetMZ(massH, transition.Charge), out rmi))
                return null;
            return new TransitionLibInfo(rmi.Rank, rmi.Intensity);
        }

        public IEnumerable<TransitionChromInfo> ChromInfos
        {
            get
            {
                if (HasResults)
                {
                    foreach (var result in Results)
                    {
                        if (result == null)
                            continue;
                        foreach (var chromInfo in result)
                            yield return chromInfo;
                    }
                }
            }
        }

        public Results<TransitionChromInfo> Results { get; private set; }

        public bool HasResults { get { return Results != null; } }

        public IEnumerable<TransitionChromInfo> GetChromInfos(int? i)
        {
            if (!i.HasValue)
                return ChromInfos;
            var chromInfos = GetSafeChromInfo(i.Value);
            if (chromInfos != null)
                return chromInfos;
            return new TransitionChromInfo[0];
        }

        public ChromInfoList<TransitionChromInfo> GetSafeChromInfo(int i)
        {
            return (HasResults && Results.Count > i ? Results[i] : null);
        }

        private TransitionChromInfo GetChromInfoEntry(int i)
        {
            var result = GetSafeChromInfo(i);
            // CONSIDER: Also specify the file index and/or optimization step?
            if (result != null)
            {
                foreach (var chromInfo in result)
                {
                    if (chromInfo != null && chromInfo.OptimizationStep == 0)
                        return chromInfo;
                }
            }
            return null;
        }

        public float? GetPeakCountRatio(int i)
        {
            if (i == -1)
                return AveragePeakCountRatio;

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return GetPeakCountRatio(chromInfo);
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo =>
                                             chromInfo.OptimizationStep != 0 ?
                                                                                 (float?)null : GetPeakCountRatio(chromInfo));
            }
        }

        private static float GetPeakCountRatio(TransitionChromInfo chromInfo)
        {
            return chromInfo.Area > 0 ? 1 : 0;            
        }

        public float? GetPeakArea(int i)
        {
            if (i == -1)
                return AveragePeakArea;

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Area;
        }

        public float? AveragePeakArea
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep != 0
                                                              ? (float?) null
                                                              : chromInfo.Area);
            }
        }

        public bool IsUserModified
        {
            get
            {
                if (!Annotations.IsEmpty)
                    return true;
                return HasResults && Results.Where(l => l != null)
                                         .SelectMany(l => l)
                                         .Contains(chromInfo => chromInfo.IsUserModified);
            }
        }

        public int? GetPeakRank(int i)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Rank;            
        }

        public float? GetPeakAreaRatio(int i)
        {
            return GetPeakAreaRatio(i, 0);
        }

        public float? GetPeakAreaRatio(int i, int indexIS)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Ratios[indexIS];
        }

        private float? GetAverageResultValue(Func<TransitionChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
        }

        public DocNode EnsureChildren(TransitionGroupDocNode parent, SrmSettings settings)
        {
            // Make sure node points to correct parent.
            return ReferenceEquals(parent.TransitionGroup, Transition.Group)
                       ? this
                       : new TransitionDocNode(new Transition(parent.TransitionGroup,
                                                              Transition.IonType,
                                                              Transition.CleavageOffset,
                                                              Transition.MassIndex,
                                                              Transition.Charge),
                                               Annotations,
                                               Losses,
                                               0.0,
                                               IsotopeDistInfo,
                                               LibInfo,
                                               null) {Mz = Mz};
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            return TransitionTreeNode.DisplayText(this, settings);    
        }

        #region Property change methods

        public TransitionDocNode ChangeLibInfo(TransitionLibInfo prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.LibInfo = v, prop);
        }

        public TransitionDocNode ChangeResults(Results<TransitionChromInfo> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Results = v, prop);
        }

        public DocNode ChangePeak(int indexSet, ChromFileInfoId fileId, int step, ChromPeak peak, int ratioCount)
        {
            var listChromInfo = Results[indexSet];
            var listChromInfoNew = new List<TransitionChromInfo>();
            if (listChromInfo == null)
                listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount));
            else
            {
                bool peakAdded = false;
                foreach (var chromInfo in listChromInfo)
                {
                    // Replace an existing entry with same index values
                    if (ReferenceEquals(chromInfo.FileId, fileId) && chromInfo.OptimizationStep == step)
                    {
                        // Something is wrong, if the value has already been added (duplicate peak? out of order?)
                        Debug.Assert(!peakAdded);
                        listChromInfoNew.Add(chromInfo.ChangePeak(peak, true));
                        peakAdded = true;
                    }
                    else
                    {
                        // Entries should be ordered, so if the new entry has not been added
                        // when an entry past it is seen, then add the new entry.
                        if (!peakAdded &&
                            chromInfo.FileIndex >= fileId.GlobalIndex &&
                            chromInfo.OptimizationStep > step)
                        {
                            listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount));
                            peakAdded = true;
                        }
                        listChromInfoNew.Add(chromInfo);
                    }
                }                
                // Finally, make sure the peak is added
                if (!peakAdded)
                    listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount));
            }

            return ChangeResults((Results<TransitionChromInfo>)
                                 Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        private static TransitionChromInfo CreateChromInfo(ChromFileInfoId fileId, int step, ChromPeak peak, int ratioCount)
        {
            return new TransitionChromInfo(fileId, step, peak, new float?[ratioCount], Annotations.EMPTY, true);
        }

        public DocNode RemovePeak(int indexSet, ChromFileInfoId fileId)
        {
            var listChromInfo = Results[indexSet];
            if (listChromInfo == null)
                return this;
            var listChromInfoNew = new List<TransitionChromInfo>();
            foreach (var chromInfo in listChromInfo)
            {
                if (!ReferenceEquals(chromInfo.FileId, fileId))
                    listChromInfoNew.Add(chromInfo);
                else if (chromInfo.OptimizationStep == 0)
                    listChromInfoNew.Add(chromInfo.ChangePeak(ChromPeak.EMPTY, true));
            }
            if (listChromInfo.Count == listChromInfoNew.Count)
                return this;
            return ChangeResults((Results<TransitionChromInfo>)
                                 Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        public TransitionDocNode MergeUserInfo(SrmSettings settings, TransitionDocNode nodeTranMerge)
        {
            var result = this;
            var annotations = Annotations.Merge(nodeTranMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (TransitionDocNode)result.ChangeAnnotations(annotations);
            var resultsInfo = MergeResultsUserInfo(settings, nodeTranMerge.Results);
            if (!ReferenceEquals(resultsInfo, Results))
                result = result.ChangeResults(resultsInfo);
            return result;
        }


        private Results<TransitionChromInfo> MergeResultsUserInfo(
            SrmSettings settings, Results<TransitionChromInfo> results)
        {
            if (!HasResults)
                return Results;

            var dictFileIdToChromInfo = results.Where(l => l != null).SelectMany(l => l)
                                               // Merge everything that does not already exist (handled below),
                                               // as merging only user modified causes loss of information in
                                               // updates
                                               //.Where(i => i.IsUserModified)
                                               .ToDictionary(i => i.FileIndex);

            var listResults = new List<ChromInfoList<TransitionChromInfo>>();
            for (int i = 0; i < results.Count; i++)
            {
                List<TransitionChromInfo> listChromInfo = null;
                var chromSet = settings.MeasuredResults.Chromatograms[i];
                var chromInfoList = Results[i];
                foreach (var fileInfo in chromSet.MSDataFileInfos)
                {
                    TransitionChromInfo chromInfo;
                    if (!dictFileIdToChromInfo.TryGetValue(fileInfo.FileIndex, out chromInfo))
                        continue;
                    if (listChromInfo == null)
                    {
                        listChromInfo = new List<TransitionChromInfo>();
                        if (chromInfoList != null)
                            listChromInfo.AddRange(chromInfoList);
                    }
                    int iExist = listChromInfo.IndexOf(chromInfoExist =>
                                                       ReferenceEquals(chromInfoExist.FileId, chromInfo.FileId) &&
                                                       chromInfoExist.OptimizationStep == chromInfo.OptimizationStep);
                    if (iExist == -1)
                        listChromInfo.Add(chromInfo);
                    else if (chromInfo.IsUserModified)
                        listChromInfo[iExist] = chromInfo;
                }
                if (listChromInfo != null)
                    chromInfoList = new ChromInfoList<TransitionChromInfo>(listChromInfo);
                listResults.Add(chromInfoList);
            }
            if (ArrayUtil.ReferencesEqual(listResults, Results))
                return Results;
            return new Results<TransitionChromInfo>(listResults);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.Mz == Mz &&
                   Equals(obj.IsotopeDistInfo, IsotopeDistInfo) &&
                   Equals(obj.LibInfo, LibInfo) &&
                   Equals(obj.Results, Results);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Mz.GetHashCode();
                result = (result*397) ^ (IsotopeDistInfo != null ? IsotopeDistInfo.GetHashCode() : 0);
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}