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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Chromatogram results summary of a single <see cref="PeptideDocNode"/> calculated
    /// from its <see cref="TransitionGroupDocNode"/> children.
    /// </summary>
    public sealed class PeptideChromInfo : ChromInfo
    {
        private ReadOnlyCollection<PeptideLabelRatio> _labelRatios;

        public PeptideChromInfo(ChromFileInfoId fileId, float peakCountRatio, float? retentionTime,
                IList<PeptideLabelRatio> labelRatios)
            : base(fileId)
        {
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            LabelRatios = labelRatios;
        }

        public float PeakCountRatio { get; private set; }
        public float? RetentionTime { get; private set; }
        public IList<PeptideLabelRatio> LabelRatios
        {
            get { return _labelRatios; }
            private set { _labelRatios = MakeReadOnly(value); }
        }

        #region object overrides

        public bool Equals(PeptideChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   other.PeakCountRatio == PeakCountRatio &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   ArrayUtil.EqualsDeep(other.LabelRatios, LabelRatios);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PeakCountRatio.GetHashCode();
                result = (result*397) ^ (RetentionTime.HasValue ? RetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ LabelRatios.GetHashCodeDeep();
                return result;
            }
        }

        #endregion
    }

    public struct PeptideLabelRatio
    {
        public PeptideLabelRatio(IsotopeLabelType labelType, IsotopeLabelType standardType,
            float? ratio, float? ratioStdev) : this()
        {
            LabelType = labelType;
            StandardType = standardType;
            Ratio = ratio;
            RatioStdev = ratioStdev;
        }

        public IsotopeLabelType LabelType { get; private set; }
        public IsotopeLabelType StandardType { get; private set; }
        public float? Ratio { get; private set; }
        public float? RatioStdev { get; private set; }

        #region object overrides

        public bool Equals(PeptideLabelRatio other)
        {
            return Equals(other.LabelType, LabelType) &&
                Equals(other.StandardType, StandardType) &&
                other.Ratio == Ratio &&
                other.RatioStdev == RatioStdev;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof (PeptideLabelRatio)) return false;
            return Equals((PeptideLabelRatio) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = LabelType.GetHashCode();
                result = (result*397) ^ StandardType.GetHashCode();
                result = (result*397) ^ Ratio.GetHashCode();
                result = (result*397) ^ RatioStdev.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Chromatogram results summary of a single <see cref="TransitionGroupDocNode"/> from
    /// a single raw file of a single replicate.
    /// </summary>
    public sealed class TransitionGroupChromInfo : ChromInfo
    {
        private ReadOnlyCollection<float?> _ratios;
        private ReadOnlyCollection<float?> _ratioStdevs;

        public TransitionGroupChromInfo(ChromFileInfoId fileId,
                                        int optimizationStep,
                                        float peakCountRatio,
                                        float? retentionTime,
                                        float? startTime,
                                        float? endTime,
                                        float? fwhm,
                                        float? area,
                                        float? backgroundArea,
                                        IList<float?> ratios,
                                        IList<float?> stdevs,
                                        int? truncated,
                                        bool identified,
                                        float? libraryDotProduct,
                                        float? isotopeDotProduct,
                                        Annotations annotations,
                                        bool userSet)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            StartRetentionTime = startTime;
            EndRetentionTime = endTime;
            Fwhm = fwhm;
            Area = area;
            BackgroundArea = backgroundArea;
            Ratios = ratios;
            RatioStdevs = stdevs;
            Truncated = truncated;
            Identified = identified;
            LibraryDotProduct = libraryDotProduct;
            IsotopeDotProduct = isotopeDotProduct;
            Annotations = annotations;
            UserSet = userSet;
        }

        public int OptimizationStep { get; private set; }

        public float PeakCountRatio { get; private set; }
        public float? RetentionTime { get; private set; }
        public float? StartRetentionTime { get; private set; }
        public float? EndRetentionTime { get; private set; }
        public float? Fwhm { get; private set; }
        public float? Area { get; private set; }
        public float? BackgroundArea { get; private set; }
        public float? Ratio { get { return _ratios[0]; } }
        public IList<float?> Ratios
        {
            get { return _ratios; }
            private set { _ratios = MakeReadOnly(value); }
        }
        public float? RatioStdev { get { return _ratioStdevs[0]; } }
        public IList<float?> RatioStdevs
        {
            get { return _ratioStdevs; }
            private set { _ratioStdevs = MakeReadOnly(value); }
        }
        public int? Truncated { get; private set; }
        public bool Identified { get; private set; }
        public float? LibraryDotProduct { get; private set; }
        public float? IsotopeDotProduct { get; private set; }
        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public bool UserSet { get; private set; }

        public bool IsUserModified { get { return UserSet || !Annotations.IsEmpty; } }

        #region Property change methods

        public TransitionGroupChromInfo ChangeRatios(IList<float?> prop, IList<float?> stdev)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.Ratios = prop;
                                                     im.RatioStdevs = stdev;
                                                 });
        }

        public TransitionGroupChromInfo ChangeAnnotations(Annotations annotations)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.Annotations = annotations;
                                                     im.UserSet = im.UserSet || !annotations.IsEmpty;
                                                 });
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionGroupChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   other.PeakCountRatio == PeakCountRatio &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   other.StartRetentionTime.Equals(StartRetentionTime) &&
                   other.EndRetentionTime.Equals(EndRetentionTime) &&
                   other.Fwhm.Equals(Fwhm) &&
                   ArrayUtil.EqualsDeep(other.Ratios, Ratios) &&
                   ArrayUtil.EqualsDeep(other.RatioStdevs, RatioStdevs) &&
                   other.Truncated.Equals(Truncated) &&
                   other.Identified.Equals(Identified) &&
                   other.LibraryDotProduct.Equals(LibraryDotProduct) &&
                   other.IsotopeDotProduct.Equals(IsotopeDotProduct) &&
                   other.Annotations.Equals(Annotations) &&
                   other.UserSet.Equals(UserSet);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionGroupChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PeakCountRatio.GetHashCode();
                result = (result*397) ^ (RetentionTime.HasValue ? RetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (StartRetentionTime.HasValue ? StartRetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (EndRetentionTime.HasValue ? EndRetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (Fwhm.HasValue ? Fwhm.Value.GetHashCode() : 0);
                result = (result*397) ^ Ratios.GetHashCodeDeep();
                result = (result*397) ^ RatioStdevs.GetHashCodeDeep();
                result = (result*397) ^ (Truncated.HasValue ? Truncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Identified.GetHashCode();
                result = (result*397) ^ (LibraryDotProduct.HasValue ? LibraryDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ (IsotopeDotProduct.HasValue ? IsotopeDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ UserSet.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Chromatogram results summary of a single <see cref="TransitionDocNode"/> from
    /// a single raw file of a single replicate.
    /// </summary>
    public sealed class TransitionChromInfo : ChromInfo
    {
        private ReadOnlyCollection<float?> _ratios;

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, ChromPeak peak,
            IList<float?> ratios, Annotations annotations, bool userSet)
            : this(fileId, optimizationStep, peak.RetentionTime, peak.StartTime, peak.EndTime,
                   peak.Area, peak.BackgroundArea, peak.Height, peak.Fwhm,
                   peak.IsFwhmDegenerate, peak.IsTruncated, peak.IsIdentified,
                   ratios, annotations, userSet)
        {            
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, float retentionTime,
                                   float startRetentionTime, float endRetentionTime,
                                   float area, float backgroundArea, float height,
                                   float fwhm, bool fwhmDegenerate, bool? truncated, bool identified,
                                   IList<float?> ratios, Annotations annotations, bool userSet)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            RetentionTime = retentionTime;
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
            Area = area;
            BackgroundArea = backgroundArea;
            Height = height;
            Fwhm = fwhm;
            IsFwhmDegenerate = fwhmDegenerate;
            IsTruncated = truncated;
            IsIdentified = identified;
            Ratios = ratios;
            Annotations = annotations;
            UserSet = userSet;
        }

        /// <summary>
        /// Set to the number of steps from the regression value for a
        /// transition attribute which can be optimized or calculated using
        /// a linear regression (e.g. CE, DP, CV)
        /// </summary>
        public int OptimizationStep { get; private set; }

        public float RetentionTime { get; private set; }
        public float StartRetentionTime { get; private set; }
        public float EndRetentionTime { get; private set; }
        public float Area { get; private set; }
        public float BackgroundArea { get; private set; }
        public float Height { get; private set; }
        public float Fwhm { get; private set; }
        public bool IsFwhmDegenerate { get; private set; }
        public bool? IsTruncated { get; private set; }
        public bool IsIdentified { get; private set; }
        public int Rank { get; private set; }

        /// <summary>
        /// Set after creation at the peptide results calculation level
        /// </summary>
        public IList<float?> Ratios
        {
            get { return _ratios; }
            private set { _ratios = MakeReadOnly(value); }
        }
        public float? Ratio { get { return _ratios[0]; } }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public bool UserSet { get; private set; }

        public bool IsUserModified { get { return UserSet || !Annotations.IsEmpty; } }

        public bool IsEmpty { get { return EndRetentionTime == 0; } }

        public bool Equivalent(ChromFileInfoId fileId, int step, ChromPeak peak)
        {
            return ReferenceEquals(fileId, FileId) &&
                   step == OptimizationStep &&
                   peak.RetentionTime == RetentionTime &&
                   peak.StartTime == StartRetentionTime &&
                   peak.EndTime == EndRetentionTime &&
                   peak.Area == Area &&
                   peak.BackgroundArea == BackgroundArea &&
                   peak.Height == Height &&
                   peak.Fwhm == Fwhm &&
                   peak.IsFwhmDegenerate == IsFwhmDegenerate &&
                   peak.IsTruncated == IsTruncated &&
                   peak.IsIdentified == IsIdentified;
        }

        #region Property change methods

        public TransitionChromInfo ChangePeak(ChromPeak peak, bool userSet)
        {
            var chromInfo = ImClone(this);
            chromInfo.RetentionTime = peak.RetentionTime;
            chromInfo.StartRetentionTime = peak.StartTime;
            chromInfo.EndRetentionTime = peak.EndTime;
            chromInfo.Area = peak.Area;
            chromInfo.BackgroundArea = peak.BackgroundArea;
            chromInfo.Height = peak.Height;
            chromInfo.Fwhm = peak.Fwhm;
            chromInfo.IsFwhmDegenerate = peak.IsFwhmDegenerate;
            chromInfo.IsTruncated = peak.IsTruncated;
            chromInfo.IsIdentified = peak.IsIdentified;
            chromInfo.UserSet = userSet;
            return chromInfo;
        }

        public TransitionChromInfo ChangeRatios(IList<float?> prop)
        {
            return ChangeProp(ImClone(this), im => im.Ratios = prop);
        }

        public TransitionChromInfo ChangeRank(int prop)
        {
            return ChangeProp(ImClone(this), im => im.Rank = prop);
        }

        public TransitionChromInfo ChangeAnnotations(Annotations annotations)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.Annotations = annotations;
                                                     im.UserSet = im.UserSet || !annotations.IsEmpty;
                                                 });
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   other.RetentionTime == RetentionTime &&
                   other.StartRetentionTime == StartRetentionTime &&
                   other.EndRetentionTime == EndRetentionTime &&
                   other.Area == Area &&
                   other.BackgroundArea == BackgroundArea &&
                   other.Height == Height &&
                   other.Fwhm == Fwhm &&
                   other.IsFwhmDegenerate.Equals(IsFwhmDegenerate) &&
                   Equals(other.IsTruncated, IsTruncated) &&
                   other.IsIdentified.Equals(IsIdentified) &&
                   other.Rank == Rank &&
                   ArrayUtil.EqualsDeep(other.Ratios, Ratios) &&
                   other.OptimizationStep.Equals(OptimizationStep) &&
                   other.Annotations.Equals(Annotations) &&
                   other.UserSet.Equals(UserSet);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ RetentionTime.GetHashCode();
                result = (result*397) ^ StartRetentionTime.GetHashCode();
                result = (result*397) ^ EndRetentionTime.GetHashCode();
                result = (result*397) ^ Area.GetHashCode();
                result = (result*397) ^ BackgroundArea.GetHashCode();
                result = (result*397) ^ Height.GetHashCode();
                result = (result*397) ^ Fwhm.GetHashCode();
                result = (result*397) ^ IsFwhmDegenerate.GetHashCode();
                result = (result*397) ^ IsIdentified.GetHashCode();
                result = (result*397) ^ (IsTruncated.HasValue ? IsTruncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Rank;
                result = (result*397) ^ Ratios.GetHashCodeDeep();
                result = (result*397) ^ OptimizationStep.GetHashCode();
                result = (result*397) ^ Annotations.GetHashCode();
                result = (result*397) ^ UserSet.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Chromatogram results summary data for a single <see cref="DocNode"/>.
    /// This list will contain one element per replicate (i.e. full run of the nodes
    /// in this document), which may take one or more injections into the instrument to
    /// accomplish.
    /// 
    /// The set of injections is encapsulated in the <see cref="ChromatogramSet"/> class,
    /// and is not relevant to this class.  The elements in this class are ordered
    /// to correspond to the elements in the document's <see cref="ChromatogramSet"/> list
    /// in <see cref="SrmSettings.MeasuredResults"/>.  This collection will have the same
    /// number of items as the chromatograms list.
    /// </summary>
    public class Results<TItem> : OneOrManyList<ChromInfoList<TItem>>
//        VS Issue: https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=324473 (seems fixed)
        where TItem : ChromInfo
    {
        public Results(params ChromInfoList<TItem>[] elements)
            : base(elements)
        {
        }

        public Results(IList<ChromInfoList<TItem>> elements)
            : base(elements)
        {
        }

        public static Results<TItem> Merge(Results<TItem> resultsOld, List<IList<TItem>> chromInfoSet)
        {
            // Check for equal results in the same positions, and swap in the old
            // values if found to maintain reference equality.
            if (resultsOld != null)
            {
                for (int i = 0, len = Math.Min(resultsOld.Count, chromInfoSet.Count); i < len; i++)
                {
                    if (EqualsDeep(resultsOld[i], chromInfoSet[i]))
                        chromInfoSet[i] = resultsOld[i];
                }
            }
            var listInfo = chromInfoSet.ConvertAll(l => l as ChromInfoList<TItem> ??
                                                        (l != null ? new ChromInfoList<TItem>(l) : null));
            if (ArrayUtil.ReferencesEqual(listInfo, resultsOld))
                return resultsOld;

            return new Results<TItem>(listInfo);
        }

        public static bool EqualsDeep(Results<TItem> resultsOld, Results<TItem> results)
        {
            if (resultsOld == null && results == null)
                return true;
            if (resultsOld == null || results == null)
                return false;
            if (resultsOld.Count != results.Count)
                return false;
            for (int i = 0, len = results.Count; i < len; i++)
            {
                if (!EqualsDeep(resultsOld[i], results[i]))
                    return false;
            }
            return true;
        }

        private static bool EqualsDeep(IList<TItem> chromInfosOld, IList<TItem> chromInfos)
        {
            if (!ArrayUtil.EqualsDeep(chromInfosOld, chromInfos))
                return false;
            if (chromInfos == null)
                return true;

            // If the arrays are otherwise equal, check the FileIds
            for (int i = 0; i < chromInfos.Count; i++)
            {
                if (!ReferenceEquals(chromInfosOld[i].FileId, chromInfos[i].FileId))
                    return false;
            }
            return true;
        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var result in this)
            {
                if (result == null)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    float? val = getVal(chromInfo);
                    if (!val.HasValue)
                        continue;

                    valTotal += val.Value;
                    valCount++;
                }
            }

            if (valCount == 0)
                return null;

            return (float)(valTotal / valCount);            
        }

        public float? GetBestPeakValue(Func<TItem, RatedPeakValue> getVal)
        {
            double ratingBest = double.MinValue;
            float? valBest = null;

            foreach (var result in this)
            {
                if (result == null)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    RatedPeakValue rateVal = getVal(chromInfo);
                    if (rateVal.Rating > ratingBest)
                    {
                        ratingBest = rateVal.Rating;
                        valBest = rateVal.Value;
                    }
                }
            }

            return valBest;
        }

        public void Validate(SrmSettings settings)
        {
            var chromatogramSets = settings.MeasuredResults.Chromatograms;
            if (chromatogramSets.Count != Count)
                throw new InvalidDataException(string.Format("DocNode results count {0} does not match document results count {1}.", Count, chromatogramSets.Count));

            for (int i = 0; i < chromatogramSets.Count; i++)
            {
                var chromList = this[i];
                if (chromList == null)
                    continue;

                var chromatogramSet = chromatogramSets[i];
                if (chromList.Any(chromInfo => chromatogramSet.IndexOfId(chromInfo.FileId) == -1))
                {
                    throw new InvalidDataException(string.Format("DocNode peak info found for file with no match in document results."));
                }
            }
        }
    }

    public struct RatedPeakValue
    {
        public RatedPeakValue(double rating, float? value) : this()
        {
            Rating = rating;
            Value = value;
        }

        public double Rating { get; private set; }
        public float? Value { get; private set; }
    }

    /// <summary>
    /// The set of all measured results for a single <see cref="DocNode"/> in a single replicate.
    /// There will usually be just one measurement, but in case of operator error,
    /// on a multi-injection replicate there may be more.  Also, a multi-injection replicate
    /// with an unlabeled internal standard will have measurements for that standard
    /// in every file.
    /// </summary>
    public class ChromInfoList<TItem> : OneOrManyList<TItem>
//        VS Issue: https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=324473
//        where T : ChromInfo
    {
        public ChromInfoList(params TItem[] elements)
            : base(elements)
        {
        }

        public ChromInfoList(IList<TItem> elements)
            : base(elements)
        {
        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var chromInfo in this)
            {
                if (Equals(chromInfo, default(TItem)))
                    continue;
                float? val = getVal(chromInfo);
                if (!val.HasValue)
                    continue;

                valTotal += val.Value;
                valCount++;
            }

            if (valCount == 0)
                return null;

            return (float)(valTotal / valCount);
        }
    }

    /// <summary>
    /// Base class for a single measured result for a single <see cref="DocNode"/>
    /// in a single file of a single replicate.
    /// </summary>
    public abstract class ChromInfo : Immutable
    {
        protected ChromInfo(ChromFileInfoId fileId)
        {
            FileId = fileId;
        }

        public ChromFileInfoId FileId { get; private set; }
        public int FileIndex { get { return FileId.GlobalIndex; } }

        #region object overrides

        public bool Equals(ChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            // TODO: This is not very strong equality, since all FileIds are equal
            //       It would be better to check reference equality, but this would
            //       break document equality tests across serialization/deserialization
            //       At the momement, we rely on it being very unlikely that two
            //       peaks from different files are exactly equal.
            return Equals(other.FileId, FileId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ChromInfo)) return false;
            return Equals((ChromInfo) obj);
        }

        public override int GetHashCode()
        {
            return FileId.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("FileId = {0}", FileId.GlobalIndex);
        }

        #endregion
    }
}