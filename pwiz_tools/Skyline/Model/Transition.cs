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
using System.IO;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    public enum IonType
    {
        precursor = -1, a, b, c, x, y, z
    }

    public class TransitionDocNode : DocNode
    {
        public TransitionDocNode(Transition id, double massH, TransitionLibInfo libInfo)
            : this(id, Annotations.Empty, massH, libInfo, null)
        {
        }

        public TransitionDocNode(Transition id, Annotations annotations, double massH,
                TransitionLibInfo libInfo, Results<TransitionChromInfo> results)
            : base(id, annotations)
        {
            Mz = SequenceMassCalc.GetMZ(massH, id.Charge);
            LibInfo = libInfo;
            Results = results;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        public double Mz { get; private set; }

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
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Ratio;
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

        private float? GetAverageResultValue(Func<TransitionChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
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

        public DocNode ChangePeak(int indexSet, int indexFile, int step, ChromPeak peak)
        {
            var listChromInfo = Results[indexSet];
            var listChromInfoNew = new List<TransitionChromInfo>();
            if (listChromInfo == null)
                listChromInfoNew.Add(new TransitionChromInfo(indexFile, step, peak, null, true));
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
                            listChromInfoNew.Add(new TransitionChromInfo(indexFile, step, peak, null, true));
                            peakAdded = true;
                        }
                        listChromInfoNew.Add(chromInfo);
                    }
                }                
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

    public class Transition : Identity
    {
        public const int MIN_PRODUCT_CHARGE = 1;
        public const int MAX_PRODUCT_CHARGE = 3;

        /// <summary>
        /// Prioritized list of all possible product ion charges
        /// </summary>
        public static readonly int[] ALL_CHARGES = { 1, 2, 3 };

        /// <summary>
        /// Prioritize, paired list of all possible product ion types
        /// </summary>
        public static readonly IonType[] ALL_TYPES =
            {IonType.y, IonType.b, IonType.z, IonType.c, IonType.x, IonType.a};

        public static bool IsNTerminal(IonType type)
        {
            return type == IonType.a || type == IonType.b || type == IonType.c || type == IonType.precursor;
        }

        public static bool IsCTerminal(IonType type)
        {
            return type == IonType.x || type == IonType.y || type == IonType.z;
        }

        public static bool IsPrecursor(IonType type)
        {
            return type == IonType.precursor;
        }

        public static IonType[] GetTypePairs(ICollection<IonType> types)
        {
            var listTypes = new List<IonType>();
            for (int i = 0; i < ALL_TYPES.Length; i++)
            {
                if (types.Contains(ALL_TYPES[i]))
                {
                    if (i % 2 == 0)
                        i++;
                    listTypes.Add(ALL_TYPES[i - 1]);
                    listTypes.Add(ALL_TYPES[i]);
                }
            }
            return listTypes.ToArray();
        }

        public static char GetFragmentNTermAA(string sequence, int cleavageOffset)
        {
            return sequence[cleavageOffset + 1];
        }

        public static char GetFragmentCTermAA(string sequence, int cleavageOffset)
        {
            return sequence[cleavageOffset];
        }

        public static int OrdinalToOffset(IonType type, int ordinal, int len)
        {
            if (IsNTerminal(type))
                return ordinal - 1;
            else
                return len - ordinal - 1;
        }

        public static int OffsetToOrdinal(IonType type, int offset, int len)
        {
            if (IsNTerminal(type))
                return offset + 1;
            else
                return len - offset - 1;
        }

        public static string GetChargeIndicator(int charge)
        {
            return "++++++++++".Substring(0, charge);
        }

        private readonly TransitionGroup _group;

        /// <summary>
        /// Creates a precursor transition
        /// </summary>
        /// <param name="group">The <see cref="TransitionGroup"/> which the transition represents</param>
        public Transition(TransitionGroup group)
            : this(group, IonType.precursor, group.Peptide.Length - 1, group.PrecursorCharge)
        {            
        }

        public Transition(TransitionGroup group, IonType type, int offset, int charge)
        {
            _group = group;

            IonType = type;
            CleavageOffset = offset;
            Charge = charge;

            // Derived values
            Peptide peptide = group.Peptide;
            Ordinal = OffsetToOrdinal(type, offset, peptide.Length);
            AA = (IsNTerminal() ? peptide.Sequence[offset] :
                peptide.Sequence[offset + 1]);

            Validate();
        }

        public TransitionGroup Group
        {
            get { return _group; }
        }

        public int Charge { get; private set; }
        public IonType IonType { get; private set; }
        public int CleavageOffset { get; private set; }

        // Derived values
        public int Ordinal { get; private set; }
        public char AA { get; private set; }

        public string FragmentIonName
        {
            get
            {
                string ionName = IonType.ToString();
                if (!IsPrecursor())
                    ionName += Ordinal;
                return ionName;
            }
        }

        public bool IsNTerminal()
        {
            return IsNTerminal(IonType);
        }

        public bool IsCTerminal()
        {
            return IsCTerminal(IonType);
        }

        public bool IsPrecursor()
        {
            return IsPrecursor(IonType);
        }

        public char FragmentNTermAA
        {
            get { return GetFragmentNTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        public char FragmentCTermAA
        {
            get { return GetFragmentCTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        private void Validate()
        {
            if (IsPrecursor())
            {
                if (TransitionGroup.MIN_PRECURSOR_CHARGE > Charge || Charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                {
                    throw new InvalidDataException(string.Format("Precursor charge {0} must be between {1} and {2}.",
                        Charge, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));                    
                }

            }
            else if (MIN_PRODUCT_CHARGE > Charge || Charge > MAX_PRODUCT_CHARGE)
            {
                throw new InvalidDataException(string.Format("Product ion charge {0} must be between {1} and {2}.",
                                                             Charge, MIN_PRODUCT_CHARGE, MAX_PRODUCT_CHARGE));
            }

            if (Ordinal < 1)
                throw new InvalidDataException(string.Format("Fragment ordinal {0} may not be less than 1.", Ordinal));
            if (IsPrecursor())
            {
                if (Ordinal != Group.Peptide.Length)
                    throw new InvalidDataException(string.Format("Precursor ordinal must be the lenght of the peptide."));
            }
            else if (Ordinal > Group.Peptide.Length - 1)
            {
                throw new InvalidDataException(string.Format("Fragment ordinal {0} exceeds the maximum {1} for the peptide {2}.",
                                                             Ordinal, Group.Peptide.Length - 1, Group.Peptide.Sequence));
            }
        }

        #region object overrides

        public bool Equals(Transition obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._group, _group) &&
                Equals(obj.IonType, IonType) &&
                obj.CleavageOffset == CleavageOffset &&
                obj.Charge == Charge;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Transition)) return false;
            return Equals((Transition) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _group.GetHashCode();
                result = (result*397) ^ IonType.GetHashCode();
                result = (result*397) ^ CleavageOffset;
                result = (result*397) ^ Charge;
                return result;
            }
        }

        public override string ToString()
        {
            if (IsPrecursor())
                return "precursor" + GetChargeIndicator(Charge);

            return string.Format("{0} - {1}{2}{3}", AA,
                IonType.ToString().ToLower(), Ordinal, GetChargeIndicator(Charge));
        }

        #endregion // object overrides
    }
}