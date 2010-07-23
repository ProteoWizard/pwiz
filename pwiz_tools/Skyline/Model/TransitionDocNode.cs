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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    public class TransitionDocNode : DocNode
    {
        public TransitionDocNode(Transition id,
                                 TransitionLosses losses,
                                 double massH,
                                 TransitionLibInfo libInfo)
            : this(id, Annotations.EMPTY, losses, massH, libInfo, null)
        {
        }

        public TransitionDocNode(Transition id,
                                 Annotations annotations,
                                 TransitionLosses losses,
                                 double massH,
                                 TransitionLibInfo libInfo,
                                 Results<TransitionChromInfo> results)
            : base(id, annotations)
        {
            Losses = losses;
            if (losses != null)
                massH -= losses.Mass;
            Mz = SequenceMassCalc.GetMZ(massH, id.Charge);
            LibInfo = libInfo;
            Results = results;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        public TransitionLossKey Key { get { return new TransitionLossKey(Transition, Losses); } }

        public double Mz { get; private set; }

        public TransitionLosses Losses { get; private set; }

        public bool HasLoss { get { return Losses != null; } }

        public double LostMass { get { return HasLoss ? Losses.Mass : 0; } }

        public bool IsLossPossible(int maxLossMods, IEnumerable<StaticMod> modsLossAvailable)
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
                return GetAverageResultValue(chromInfo =>
                                             chromInfo.OptimizationStep != 0 ?
                                                                                 (float?)null : chromInfo.Area);
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
            return ReferenceEquals(parent.TransitionGroup, Transition.Group) ? this
                       : new TransitionDocNode(new Transition(parent.TransitionGroup, Transition.IonType, Transition.CleavageOffset, Transition.Charge), Annotations,
                                               Losses, 0.0, LibInfo, null) { Mz = Mz };
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

        public DocNode ChangePeak(int indexSet, int indexFile, int step, ChromPeak peak, int ratioCount)
        {
            var listChromInfo = Results[indexSet];
            var listChromInfoNew = new List<TransitionChromInfo>();
            if (listChromInfo == null)
                listChromInfoNew.Add(new TransitionChromInfo(indexFile, step, peak, new float?[ratioCount], true));
            else
            {
                bool peakAdded = false;
                foreach (var chromInfo in listChromInfo)
                {
                    // Replace an existing entry with same index values
                    if (chromInfo.FileIndex == indexFile && chromInfo.OptimizationStep == step)
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
                            chromInfo.FileIndex >= indexFile &&
                            chromInfo.OptimizationStep > step)
                        {
                            listChromInfoNew.Add(new TransitionChromInfo(indexFile, step, peak, new float?[ratioCount], true));
                            peakAdded = true;
                        }
                        listChromInfoNew.Add(chromInfo);
                    }
                }                
                // Finally, make sure the peak is added
                if (!peakAdded)
                    listChromInfoNew.Add(new TransitionChromInfo(indexFile, step, peak, new float?[ratioCount], true));
            }

            return ChangeResults((Results<TransitionChromInfo>)
                                 Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        public DocNode RemovePeak(int indexSet, int indexFile)
        {
            var listChromInfo = Results[indexSet];
            if (listChromInfo == null)
                return this;
            var listChromInfoNew = new List<TransitionChromInfo>();
            foreach (var chromInfo in listChromInfo)
            {
                if (chromInfo.FileIndex != indexFile)
                    listChromInfoNew.Add(chromInfo);
                else if (chromInfo.OptimizationStep == 0)
                    listChromInfoNew.Add(chromInfo.ChangePeak(ChromPeak.EMPTY, true));
            }
            if (listChromInfo.Count == listChromInfoNew.Count)
                return this;
            return ChangeResults((Results<TransitionChromInfo>)
                                 Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.Mz == Mz &&
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
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}