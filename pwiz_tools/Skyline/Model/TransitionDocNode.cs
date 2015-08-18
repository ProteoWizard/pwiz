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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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
            if (id.IsCustom())
                Mz = BioMassCalc.CalculateIonMz(massH, id.Charge);
            else
                Mz = SequenceMassCalc.GetMZ(massH, id.Charge) + SequenceMassCalc.GetPeptideInterval(id.DecoyMassShift);
            IsotopeDistInfo = isotopeDistInfo;
            LibInfo = libInfo;
            Results = results;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        public TransitionLossKey Key(TransitionGroupDocNode parent)
        {
            return new TransitionLossKey(parent, this, Losses);
        }

        public TransitionLossEquivalentKey EquivalentKey(TransitionGroupDocNode parent)
        {
            return new TransitionLossEquivalentKey(parent, this, Losses); 
        }

        public double Mz { get; private set; }

        public double GetIonMass()
        {
            return Transition.IsCustom()
                ? BioMassCalc.CalculateIonMassFromMz(Mz, Transition.Charge)
                : SequenceMassCalc.GetMH(Mz, Transition.Charge);            
        }

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
            get { return GetFragmentIonName(LocalizationHelper.CurrentCulture); }
        }

        public string GetFragmentIonName(CultureInfo cultureInfo)
        {
            string ionName = Transition.GetFragmentIonName(cultureInfo);
            return (HasLoss ? string.Format("{0} -{1}", ionName, Math.Round(Losses.Mass, 1)) : ionName); // Not L10N
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

        public static TransitionIsotopeDistInfo GetIsotopeDistInfo(Transition transition, TransitionLosses losses, IsotopeDistInfo isotopeDist)
        {
            if (isotopeDist == null || !transition.IsPrecursor() || losses != null)
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

        public int? ResultsRank { get; private set; }

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
            if (chromInfo != null && chromInfo.Rank > 0)
                return chromInfo.Rank;
            return null;
        }

        public int? GetRank(int? i, bool useResults)
        {
            if (useResults && HasResults)
            {
                if (i.HasValue)
                    return GetPeakRank(i.Value);
                else
                    return ResultsRank;
            }
            else if (!useResults && HasLibInfo && LibInfo.Rank > 0)
                return LibInfo.Rank;
            return null;
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
            return chromInfo.GetRatio(indexIS);
        }

        private float? GetAverageResultValue(Func<TransitionChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
        }

        /// <summary>
        /// Return product's neutral mass rounded for XML I/O
        /// </summary>
        public double GetIonPersistentNeutralMass()
        {
            double ionMass = GetIonMass();
            return Transition.IsCustom() ? Math.Round(ionMass, SequenceMassCalc.MassPrecision) : SequenceMassCalc.PersistentNeutral(ionMass);
        }



        public DocNode EnsureChildren(TransitionGroupDocNode parent, SrmSettings settings)
        {
            // Make sure node points to correct parent.
            if  (ReferenceEquals(parent.TransitionGroup, Transition.Group))
                return this;

            var transition = Transition.IsCustom()
                ? new Transition(parent.TransitionGroup,
                    Transition.Charge,
                    Transition.MassIndex,
                    Transition.CustomIon,
                    Transition.IonType)
                : new Transition(parent.TransitionGroup,
                                Transition.IonType,
                                Transition.CleavageOffset,
                                Transition.MassIndex,
                                Transition.Charge);

            return new TransitionDocNode(transition,
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

        public string PrimaryCustomIonEquivalenceKey
        {
            get { return Transition.CustomIon.PrimaryEquivalenceKey; }
        }

        public string SecondaryCustomIonEquivalenceKey
        {
            get { return Transition.CustomIon.SecondaryEquivalenceKey; }
        }

        public class CustomIonEquivalenceComparer : IComparer<TransitionDocNode>
        {
            public int Compare(TransitionDocNode left, TransitionDocNode right)
            {
                if (left.Transition.IsPrecursor() != right.Transition.IsPrecursor())
                    return left.Transition.IsPrecursor() ? -1 : 1;  // Precursors come first
                if (!string.IsNullOrEmpty(left.PrimaryCustomIonEquivalenceKey) && !string.IsNullOrEmpty(right.PrimaryCustomIonEquivalenceKey))
                    return string.CompareOrdinal(left.PrimaryCustomIonEquivalenceKey, right.PrimaryCustomIonEquivalenceKey);
                if (!string.IsNullOrEmpty(left.SecondaryCustomIonEquivalenceKey) && !string.IsNullOrEmpty(right.SecondaryCustomIonEquivalenceKey))
                    return string.CompareOrdinal(left.SecondaryCustomIonEquivalenceKey, right.SecondaryCustomIonEquivalenceKey);
                return right.Mz.CompareTo(left.Mz); // Decreasing mz sort
            }
        }

        #region Property change methods

        public TransitionDocNode ChangeLibInfo(TransitionLibInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.LibInfo = prop);
        }

        public TransitionDocNode ChangeResults(Results<TransitionChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im => im.Results = prop);
        }

        public TransitionDocNode ChangeResultsRank(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.ResultsRank = prop);
        }

        public DocNode ChangePeak(int indexSet, ChromFileInfoId fileId, int step, ChromPeak peak, int ratioCount, UserSet userSet)
        {
            if (Results == null)
                return this;

            var listChromInfo = Results[indexSet];
            var listChromInfoNew = new List<TransitionChromInfo>();
            if (listChromInfo == null)
                listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount, userSet));
            else
            {
                bool peakAdded = false;
                foreach (var chromInfo in listChromInfo)
                {
                    // Replace an existing entry with same index values
                    if (ReferenceEquals(chromInfo.FileId, fileId) && chromInfo.OptimizationStep == step)
                    {
                        // Something is wrong, if the value has already been added (duplicate peak? out of order?)
                        if (peakAdded)
                        {
                            throw new InvalidDataException(string.Format(Resources.TransitionDocNode_ChangePeak_Duplicate_or_out_of_order_peak_in_transition__0_,
                                                              FragmentIonName));
                        }
                        
                        // If the target peak is exactly the same as the proposed change and userSet is not overriding,
                        // simply return the original node unchanged
                        if (chromInfo.EquivalentTolerant(fileId, step, peak) && !chromInfo.UserSet.IsOverride(userSet))
                            return this;

                        listChromInfoNew.Add(chromInfo.ChangePeak(peak, userSet));
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
                            listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount, userSet));
                            peakAdded = true;
                        }
                        listChromInfoNew.Add(chromInfo);
                    }
                }
                // Finally, make sure the peak is added
                if (!peakAdded)
                    listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ratioCount, userSet));
            }

            return ChangeResults((Results<TransitionChromInfo>)
                                 Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        private static TransitionChromInfo CreateChromInfo(ChromFileInfoId fileId, int step, ChromPeak peak, int ratioCount, UserSet userSet)
        {
            return new TransitionChromInfo(fileId, step, peak, new float?[ratioCount], Annotations.EMPTY, userSet);
        }

        public DocNode RemovePeak(int indexSet, ChromFileInfoId fileId, UserSet userSet)
        {
            bool peakChanged = false;
            var listChromInfo = Results[indexSet];
            if (listChromInfo == null)
                return this;
            var listChromInfoNew = new List<TransitionChromInfo>();
            foreach (var chromInfo in listChromInfo)
            {
                if (!ReferenceEquals(chromInfo.FileId, fileId))
                    listChromInfoNew.Add(chromInfo);
                else if (chromInfo.OptimizationStep == 0)
                {
                    if (!chromInfo.Equivalent(fileId, 0, ChromPeak.EMPTY))
                    {
                        listChromInfoNew.Add(chromInfo.ChangePeak(ChromPeak.EMPTY, userSet));
                        peakChanged = true;
                    }
                    else
                        listChromInfoNew.Add(chromInfo);
                }
            }
            if (listChromInfo.Count == listChromInfoNew.Count && !peakChanged)
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
            var equal =  base.Equals(obj) && obj.Mz == Mz &&
                   Equals(obj.IsotopeDistInfo, IsotopeDistInfo) &&
                   Equals(obj.LibInfo, LibInfo) &&
                   Equals(obj.Results, Results);
            return equal;  // For debugging convenience
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