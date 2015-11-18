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
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Chromatogram results summary of a single <see cref="PeptideDocNode"/> calculated
    /// from its <see cref="TransitionGroupDocNode"/> children.
    /// </summary>
    public sealed class PeptideChromInfo : ChromInfo
    {
        private ImmutableList<PeptideLabelRatio> _labelRatios;

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
            private set { _labelRatios = value as ImmutableList<PeptideLabelRatio> ?? MakeReadOnly(value); }
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
            RatioValue ratio) : this()
        {
            LabelType = labelType;
            StandardType = standardType;
            Ratio = ratio;
        }

        public IsotopeLabelType LabelType { get; private set; }
        public IsotopeLabelType StandardType { get; private set; }
        public RatioValue Ratio { get; private set; }

        #region object overrides

        public bool Equals(PeptideLabelRatio other)
        {
            return Equals(other.LabelType, LabelType) &&
                Equals(other.StandardType, StandardType) &&
                Equals(other.Ratio, Ratio);
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
                result = (result*397) ^ (StandardType == null ? 0 : StandardType.GetHashCode());
                result = (result*397) ^ (Ratio == null ? 0 : Ratio.GetHashCode());
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
        private static readonly IList<RatioValue>[] EMPTY_RATIOS  = new ImmutableList<RatioValue>[4];

        static TransitionGroupChromInfo()
        {
            for (int i = 0; i < EMPTY_RATIOS.Length; i++)
            {
                EMPTY_RATIOS[i] = ImmutableList<RatioValue>.ValueOf(new RatioValue[i + 1]);
            }
        }

        public static IList<RatioValue> GetEmptyRatios(int countRatios)
        {
            int i = countRatios - 1;
            return i <= EMPTY_RATIOS.Length ? EMPTY_RATIOS[i] : new RatioValue[countRatios];
        }

        private ImmutableList<RatioValue> _ratios;

        public TransitionGroupChromInfo(ChromFileInfoId fileId,
                                        int optimizationStep,
                                        float peakCountRatio,
                                        float? retentionTime,
                                        float? startTime,
                                        float? endTime,
                                        float? fwhm,
                                        float? area,
                                        float? areaMs1,
                                        float? areaFragment,
                                        float? backgroundArea,
                                        float? backgroundAreaMs1,
                                        float? backgroundAreaFragment,
                                        float? height,
                                        IList<RatioValue> ratios,
                                        float? massError,
                                        int? truncated,
                                        PeakIdentification identified,
                                        float? libraryDotProduct,
                                        float? isotopeDotProduct,
                                        Annotations annotations,
                                        UserSet userSet)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            StartRetentionTime = startTime;
            EndRetentionTime = endTime;
            Fwhm = fwhm;
            Area = area;
            AreaMs1 = areaMs1;
            AreaFragment = areaFragment;
            BackgroundArea = backgroundArea;
            BackgroundAreaMs1 = backgroundAreaMs1;
            BackgroundAreaFragment = backgroundAreaFragment;
            Height = height;
            Ratios = ratios;
            MassError = massError;
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
        public float? AreaMs1 { get; private set; }
        public float? AreaFragment { get; private set; }
        public float? BackgroundArea { get; private set; }
        public float? BackgroundAreaMs1 { get; private set; }
        public float? BackgroundAreaFragment { get; private set; }
        public float? Height { get; private set; }
        public float? Ratio { get { return _ratios[0] == null ? (float?) null : _ratios[0].Ratio; } }
        public IList<RatioValue> Ratios
        {
            get { return _ratios; }
            private set { _ratios = value as ImmutableList<RatioValue> ?? MakeReadOnly(value); }
        }
        public float? MassError { get; private set; }
        public int? Truncated { get; private set; }
        public PeakIdentification Identified { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }
        public float? LibraryDotProduct { get; private set; }
        public float? IsotopeDotProduct { get; private set; }
        public Annotations Annotations { get; private set; }

        public RatioValue GetRatio(int index)
        {
            return index != RATIO_INDEX_GLOBAL_STANDARDS
                       ? _ratios[index]
                       : _ratios[_ratios.Count - 1];
        }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public UserSet UserSet { get; private set; }

        public bool IsUserSetManual { get { return UserSet == UserSet.TRUE; } }

        public bool IsUserSetAuto { get { return UserSet == UserSet.IMPORTED || UserSet == UserSet.REINTEGRATED; } }

        public bool IsUserSetMatched { get { return UserSet == UserSet.MATCHED; }}

        public bool IsUserModified { get { return IsUserSetManual || !Annotations.IsEmpty; } }

        #region Property change methods

        public TransitionGroupChromInfo ChangeRatios(IList<RatioValue> prop)
        {
            return ChangeProp(ImClone(this), im => im.Ratios = prop);
        }

        public TransitionGroupChromInfo ChangeAnnotations(Annotations annotations)
        {
            if (Equals(annotations, Annotations))
                return this;
            return ChangeProp(ImClone(this), im => im.Annotations = annotations);
        }

        public TransitionGroupChromInfo ChangeUserSet(UserSet prop)
        {
            return ChangeProp(ImClone(this), im => im.UserSet = prop);
        }

        public TransitionGroupChromInfo ChangeLibraryDotProduct(float? prop)
        {
            return ChangeProp(ImClone(this), im => im.LibraryDotProduct = prop);
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
                   other.Area.Equals(Area) &&
                   other.AreaMs1.Equals(AreaMs1) &&
                   other.AreaFragment.Equals(AreaFragment) &&
                   other.BackgroundArea.Equals(BackgroundArea) &&
                   other.BackgroundAreaMs1.Equals(BackgroundAreaMs1) &&
                   other.BackgroundAreaFragment.Equals(BackgroundAreaFragment) &&
                   other.Height.Equals(Height) &&
                   ArrayUtil.EqualsDeep(other.Ratios, Ratios) &&
                   other.Truncated.Equals(Truncated) &&
                   other.Identified.Equals(Identified) &&
                   other.LibraryDotProduct.Equals(LibraryDotProduct) &&
                   other.IsotopeDotProduct.Equals(IsotopeDotProduct) &&
                   other.Annotations.Equals(Annotations) &&
                   other.OptimizationStep.Equals(OptimizationStep) &&
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
                result = (result*397) ^ (Area.HasValue ? Area.Value.GetHashCode() : 0);
                result = (result*397) ^ (AreaMs1.HasValue ? AreaMs1.Value.GetHashCode() : 0);
                result = (result*397) ^ (AreaFragment.HasValue ? AreaFragment.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundArea.HasValue ? BackgroundArea.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundAreaMs1.HasValue ? BackgroundAreaMs1.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundAreaFragment.HasValue ? BackgroundAreaFragment.Value.GetHashCode() : 0);
                result = (result*397) ^ (Height.HasValue ? Height.Value.GetHashCode() : 0);
                result = (result*397) ^ Ratios.GetHashCodeDeep();
                result = (result*397) ^ (Truncated.HasValue ? Truncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Identified.GetHashCode();
                result = (result*397) ^ (LibraryDotProduct.HasValue ? LibraryDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ (IsotopeDotProduct.HasValue ? IsotopeDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ OptimizationStep;
                result = (result*397) ^ Annotations.GetHashCode();
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
        private static readonly IList<float?>[] EMPTY_RATIOS  = new ImmutableList<float?>[4];

        static TransitionChromInfo()
        {
            for (int i = 0; i < EMPTY_RATIOS.Length; i++)
            {
                EMPTY_RATIOS[i] = ImmutableList<float?>.ValueOf(new float?[i + 1]);
            }
        }

        public static IList<float?> GetEmptyRatios(int countRatios)
        {
            int i = countRatios - 1;
            return i <= EMPTY_RATIOS.Length ? EMPTY_RATIOS[i] : new float?[countRatios];
        }

        private ImmutableList<float?> _ratios;

        public TransitionChromInfo(float startRetentionTime, float endRetentionTime)
            : base(null)
        {
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, ChromPeak peak,
            IList<float?> ratios, Annotations annotations, UserSet userSet)
            : this(fileId, optimizationStep, peak.MassError, peak.RetentionTime, peak.StartTime, peak.EndTime,
                   peak.Area, peak.BackgroundArea, peak.Height, peak.Fwhm,
                   peak.IsFwhmDegenerate, peak.IsTruncated, peak.Identified,
                   ratios, annotations, userSet)
        {            
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, float? massError,
                                   float retentionTime, float startRetentionTime, float endRetentionTime,
                                   float area, float backgroundArea, float height,
                                   float fwhm, bool fwhmDegenerate, bool? truncated,
                                   PeakIdentification identified, IList<float?> ratios,
                                   Annotations annotations, UserSet userSet)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            MassError = massError;
            RetentionTime = retentionTime;
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
            Area = area;
            BackgroundArea = backgroundArea;
            Height = height;
            Fwhm = fwhm;
            // Crawdad can set FWHM to NaN. Need to protect against that here.
            if (float.IsNaN(fwhm))
                Fwhm = 0;
            IsFwhmDegenerate = fwhmDegenerate;
            IsTruncated = truncated;
            Identified = identified;
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

        public float? MassError { get; private set; }
        public float RetentionTime { get; private set; }
        public float StartRetentionTime { get; private set; }
        public float EndRetentionTime { get; private set; }
        public float Area { get; private set; }
        public float BackgroundArea { get; private set; }
        public float Height { get; private set; }
        public float Fwhm { get; private set; }
        public bool IsFwhmDegenerate { get; private set; }
        public bool? IsTruncated { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }
        public PeakIdentification Identified { get; private set; }
        public int Rank { get; private set; }

        /// <summary>
        /// Set after creation at the peptide results calculation level
        /// </summary>
        public IList<float?> Ratios
        {
            get { return _ratios; }
            private set { _ratios = value as ImmutableList<float?> ?? MakeReadOnly(value); }
        }
        public float? Ratio { get { return _ratios[0]; } }

        public float? GetRatio(int index)
        {
            return index != RATIO_INDEX_GLOBAL_STANDARDS
                ? _ratios[index]
                : _ratios[_ratios.Count - 1];
        }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public UserSet UserSet { get; private set; }

        public bool IsUserSetManual { get { return UserSet == UserSet.TRUE; } }

        public bool IsUserSetAuto { get { return UserSet == UserSet.IMPORTED || UserSet == UserSet.REINTEGRATED; } }

        public bool IsUserSetMatched { get { return UserSet == UserSet.MATCHED; } }

        public bool IsUserModified { get { return !Equals(UserSet, UserSet.FALSE) || !Annotations.IsEmpty; } }

        public bool IsEmpty { get { return EndRetentionTime == 0; } }

        public bool Equivalent(ChromFileInfoId fileId, int step, ChromPeak peak)
        {
            return ReferenceEquals(fileId, FileId) &&
                   step == OptimizationStep &&
                   Equals(peak.MassError, MassError) &&
                   peak.RetentionTime == RetentionTime &&
                   peak.StartTime == StartRetentionTime &&
                   peak.EndTime == EndRetentionTime &&
                   peak.Area == Area &&
                   peak.BackgroundArea == BackgroundArea &&
                   peak.Height == Height &&
                   peak.Fwhm == Fwhm &&
                   peak.IsFwhmDegenerate == IsFwhmDegenerate &&
                   Equals(peak.IsTruncated, IsTruncated) &&
                   Equals(peak.Identified, Identified);
        }

        public bool EquivalentTolerant(ChromFileInfoId fileId, int step, ChromPeak peak)
        {
            const double tol = 1e-4;
            return ReferenceEquals(fileId, FileId) &&
                   step == OptimizationStep &&
                   Equals(peak.MassError, MassError) &&
                   IsEqualTolerant(peak.RetentionTime, RetentionTime, tol) &&
                   IsEqualTolerant(peak.StartTime, StartRetentionTime, tol) &&
                   IsEqualTolerant(peak.EndTime, EndRetentionTime, tol) &&
                   IsEqualTolerant(peak.Area, Area, tol * Area)  &&
                   IsEqualTolerant(peak.BackgroundArea, BackgroundArea, tol * BackgroundArea) &&
                   IsEqualTolerant(peak.Height, Height, tol * Height)  &&
                   (IsEqualTolerant(peak.Fwhm, Fwhm, tol * Fwhm)  || Equals(peak.Fwhm,Fwhm)) &&
                   Equals(peak.IsFwhmDegenerate, IsFwhmDegenerate) &&
                   Equals(peak.IsTruncated, IsTruncated) &&
                   Equals(peak.Identified, Identified);
        }

        public bool IsEqualTolerant(double a, double b, double tol)
        {
            return Math.Abs(a - b) <= tol;
        }

        #region Property change methods

        public TransitionChromInfo ChangePeak(ChromPeak peak, UserSet userSet)
        {
            var chromInfo = ImClone(this);
            chromInfo.MassError = peak.MassError;
            chromInfo.RetentionTime = peak.RetentionTime;
            chromInfo.StartRetentionTime = peak.StartTime;
            chromInfo.EndRetentionTime = peak.EndTime;
            chromInfo.Area = peak.Area;
            chromInfo.BackgroundArea = peak.BackgroundArea;
            chromInfo.Height = peak.Height;
            chromInfo.Fwhm = peak.Fwhm;
            // Crawdad can set FWHM to NaN. Need to protect against that here.
            if (float.IsNaN(peak.Fwhm))
                chromInfo.Fwhm = 0;
            chromInfo.IsFwhmDegenerate = peak.IsFwhmDegenerate;
            chromInfo.IsTruncated = peak.IsTruncated;
            chromInfo.Identified = peak.Identified;
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
            if (Equals(annotations, Annotations))
                return this;
            return ChangeProp(ImClone(this), im => im.Annotations = annotations);
        }

        public TransitionChromInfo ChangeUserSet(UserSet prop)
        {
            return ChangeProp(ImClone(this), im => im.UserSet = prop);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.MassError, MassError) &&
                   other.RetentionTime == RetentionTime &&
                   other.StartRetentionTime == StartRetentionTime &&
                   other.EndRetentionTime == EndRetentionTime &&
                   other.Area == Area &&
                   other.BackgroundArea == BackgroundArea &&
                   other.Height == Height &&
                   other.Fwhm == Fwhm &&
                   other.IsFwhmDegenerate.Equals(IsFwhmDegenerate) &&
                   Equals(other.IsTruncated, IsTruncated) &&
                   Equals(other.Identified, Identified) &&
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
                result = (result*397) ^ (MassError.HasValue ? MassError.Value.GetHashCode() : 0);
                result = (result*397) ^ RetentionTime.GetHashCode();
                result = (result*397) ^ StartRetentionTime.GetHashCode();
                result = (result*397) ^ EndRetentionTime.GetHashCode();
                result = (result*397) ^ Area.GetHashCode();
                result = (result*397) ^ BackgroundArea.GetHashCode();
                result = (result*397) ^ Height.GetHashCode();
                result = (result*397) ^ Fwhm.GetHashCode();
                result = (result*397) ^ IsFwhmDegenerate.GetHashCode();
                result = (result*397) ^ Identified.GetHashCode();
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

        public static Results<TItem> ChangeChromInfo(Results<TItem> results, ChromFileInfoId id, TItem newChromInfo)
        {
            var elements = new List<ChromInfoList<TItem>>();
            bool found = false;
            foreach (var replicate in results)
            {
                var chromInfoList = new List<TItem>();
                if (replicate == null)
                {
                    elements.Add(null);
                    continue;
                }
                foreach (var chromInfo in replicate)
                {
                    if (!ReferenceEquals(chromInfo.FileId, id))
                        chromInfoList.Add(chromInfo);
                    else
                    {
                        found = true;
                        chromInfoList.Add(newChromInfo);
                    }
                }
                elements.Add(new ChromInfoList<TItem>(chromInfoList));
            }
            if (!found)
                throw new InvalidOperationException(Resources.ResultsGrid_ChangeChromInfo_Element_not_found);
            return new Results<TItem>(elements);
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
            {
                throw new InvalidDataException(
                    string.Format(Resources.Results_Validate_DocNode_results_count__0__does_not_match_document_results_count__1__,
                                  Count, chromatogramSets.Count));
            }

            for (int i = 0; i < chromatogramSets.Count; i++)
            {
                var chromList = this[i];
                if (chromList == null)
                    continue;

                var chromatogramSet = chromatogramSets[i];
                if (chromList.Any(chromInfo => chromatogramSet.IndexOfId(chromInfo.FileId) == -1))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Results_Validate_DocNode_peak_info_found_for_file_with_no_match_in_document_results));
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

    public enum UserSet
    {
        TRUE,   // SET by manual integration
        FALSE,  // Best peak picked during results import
        IMPORTED,   // Import peak boundaries
        REINTEGRATED,   // Edit > Refine > Reintagrate
        MATCHED // Forced by peak matching when adding missing label type precursors
    }

    public static class UserSetExtension
    {
        private static readonly UserSet[] USER_SET_PRIORITY_LIST =
            {
                UserSet.FALSE,
                UserSet.TRUE,
                UserSet.IMPORTED,
                UserSet.REINTEGRATED,
                UserSet.MATCHED
            };

        public static UserSet GetBest(UserSet us1, UserSet us2, bool includeFalse = false)
        {
            for (int i = includeFalse ? 0 : 1; i < USER_SET_PRIORITY_LIST.Length; i++)
            {
                UserSet usCurrent = USER_SET_PRIORITY_LIST[i];
                if (us1 == usCurrent || us2 == usCurrent)
                    return usCurrent;
            }
            return UserSet.FALSE;            
        }

        public static bool IsOverride(this UserSet userSetPrimary, UserSet userSetSecondary)
        {
            var userSetBest = GetBest(userSetPrimary, userSetSecondary, true);
            // If primary is already best, then this is not an override
            if (userSetBest == userSetPrimary)
                return false;
            // Otherwise, only override MATCHED with anything and anything with FALSE
            // This keeps importing and reintegrating from resetting each other's peaks
            // when the specifide boundaries are not different.
            return (userSetPrimary == UserSet.MATCHED || userSetSecondary == UserSet.FALSE);
        }        
    }

    /// <summary>
    /// Base class for a single measured result for a single <see cref="DocNode"/>
    /// in a single file of a single replicate.
    /// </summary>
    public abstract class ChromInfo : Immutable
    {
        public const int RATIO_INDEX_GLOBAL_STANDARDS = -2;

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
            return String.Format("FileId = {0}", FileId.GlobalIndex); // Not L10N : For debugging
        }

        #endregion
    }
}