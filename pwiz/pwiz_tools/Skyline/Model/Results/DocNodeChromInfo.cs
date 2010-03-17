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
        public PeptideChromInfo(int fileIndex, float peakCountRatio,
                                float? retentionTime, float? ratioToStandard)
            : base(fileIndex)
        {
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            RatioToStandard = ratioToStandard;
        }

        public float PeakCountRatio { get; private set; }
        public float? RetentionTime { get; private set; }
        public float? RatioToStandard { get; private set; }

        #region object overrides

        public bool Equals(PeptideChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   other.PeakCountRatio == PeakCountRatio &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   other.RatioToStandard.Equals(RatioToStandard);
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
                result = (result*397) ^ (RatioToStandard.HasValue ? RatioToStandard.Value.GetHashCode() : 0);
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
        public TransitionGroupChromInfo(int fileIndex,
                                        int optimizationStep,
                                        float peakCountRatio,
                                        float? retentionTime,
                                        float? startTime,
                                        float? endTime,
                                        float? fwhm,
                                        float? area,
                                        float? backgroundArea,
                                        float? ratio,
                                        float? stdev,
                                        float? libraryDotProduct,
                                        Annotations annotations,
                                        bool userSet)
            : base(fileIndex)
        {
            OptimizationStep = optimizationStep;
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            StartRetentionTime = startTime;
            EndRetentionTime = endTime;
            Fwhm = fwhm;
            Area = area;
            BackgroundArea = backgroundArea;
            Ratio = ratio;
            RatioStdev = stdev;
            LibraryDotProduct = libraryDotProduct;
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
        public float? Ratio { get; private set; }
        public float? RatioStdev { get; private set; }
        public float? LibraryDotProduct { get; private set; }
        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public bool UserSet { get; private set; }

        #region Property change methods

        public TransitionGroupChromInfo ChangeRatio(float? prop, float? stdev)
        {
            var im = ImClone(this);
            im.Ratio = prop;
            im.RatioStdev = stdev;
            return im;
        }

        public TransitionGroupChromInfo ChangeAnnotations(Annotations annotations)
        {
            return ChangeProp(ImClone(this), 
                (im, v) => { im.Annotations = v; im.UserSet = im.UserSet || !v.IsEmpty; }, 
                annotations);
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
                   other.Ratio == Ratio &&
                   other.LibraryDotProduct.Equals(LibraryDotProduct) &&
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
                result = (result*397) ^ (Ratio.HasValue ? Ratio.Value.GetHashCode() : 0);
                result = (result*397) ^ (LibraryDotProduct.HasValue ? LibraryDotProduct.Value.GetHashCode() : 0);
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
        public TransitionChromInfo(int fileIndex, int optimizationStep, ChromPeak peak, float? ratio, bool userSet)
            : this(fileIndex, optimizationStep, peak.RetentionTime, peak.StartTime, peak.EndTime,
                   peak.Area, peak.BackgroundArea, peak.Height, peak.Fwhm,
                   peak.IsFwhmDegenerate, ratio, Annotations.Empty, userSet)
        {            
        }

        public TransitionChromInfo(int fileIndex, int optimizationStep, float retentionTime,
                                   float startRetentionTime, float endRetentionTime,
                                   float area, float backgroundArea, float height,
                                   float fwhm, bool fwhmDegenerate, float? ratio,
                                   Annotations annotations, bool userSet)
            : base(fileIndex)
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
            Ratio = ratio;
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
        public int Rank { get; private set; }

        /// <summary>
        /// Set after creation at the peptide results calculation level
        /// </summary>
        public float? Ratio { get; private set; }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public bool UserSet { get; private set; }

        public bool IsEmpty { get { return EndRetentionTime == 0; } }

        public bool Equivalent(int fileIndex, int step, ChromPeak peak)
        {
            return fileIndex == FileIndex &&
                   step == OptimizationStep &&
                   peak.RetentionTime == RetentionTime &&
                   peak.StartTime == StartRetentionTime &&
                   peak.EndTime == EndRetentionTime &&
                   peak.Area == Area &&
                   peak.BackgroundArea == BackgroundArea &&
                   peak.Height == Height &&
                   peak.Fwhm == Fwhm &&
                   peak.IsFwhmDegenerate == IsFwhmDegenerate;
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
            chromInfo.UserSet = userSet;
            return chromInfo;
        }

        public TransitionChromInfo ChangeRatio(float? prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Ratio = v, prop);
        }

        public TransitionChromInfo ChangeRank(int prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.Rank = v, prop);
        }

        public TransitionChromInfo ChangeAnnotations(Annotations annotations)
        {
            return ChangeProp(ImClone(this), (im, v) =>
                                                 {
                                                     im.Annotations = v;
                                                     im.UserSet = im.UserSet || !v.IsEmpty;
                                                 }, annotations);
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
                   other.Rank == Rank &&
                   other.Ratio.Equals(Ratio) &&
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
                result = (result*397) ^ Rank;
                result = (result*397) ^ (Ratio.HasValue ? Ratio.Value.GetHashCode() : 0);
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
    public class Results<T> : OneOrManyList<ChromInfoList<T>>
//        VS Issue: https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=324473
//        where T : ChromInfo
    {
        public Results(params ChromInfoList<T>[] elements)
            : base(elements)
        {
        }

        public Results(IList<ChromInfoList<T>> elements)
            : base(elements)
        {
        }

        public static Results<T> Merge(Results<T> resultsOld, List<IList<T>> chromInfoSet)
        {
            // Check for equal results in the same positions, and swap in the old
            // values if found to maintain reference equality.
            if (resultsOld != null)
            {
                for (int i = 0, len = Math.Min(resultsOld.Count, chromInfoSet.Count); i < len; i++)
                {
                    if (ArrayUtil.EqualsDeep(chromInfoSet[i], resultsOld[i]))
                        chromInfoSet[i] = resultsOld[i];
                }
            }
            var listInfo = chromInfoSet.ConvertAll(l => l as ChromInfoList<T> ??
                                                        (l != null ? new ChromInfoList<T>(l) : null));
            if (ArrayUtil.ReferencesEqual(listInfo, resultsOld))
                return resultsOld;

            return new Results<T>(listInfo);
        }

        public float? GetAverageValue(Func<T, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var result in this)
            {
                if (result == null)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(T)))
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
    }

    /// <summary>
    /// The set of all measured results for a single <see cref="DocNode"/> in a single replicate.
    /// There will usually be just one measurement, but in case of operator error,
    /// on a multi-injection replicate there may be more.  Also, a multi-injection replicate
    /// with an unlabeled internal standard will have measurements for that standard
    /// in every file.
    /// </summary>
    public class ChromInfoList<T> : OneOrManyList<T>
//        VS Issue: https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=324473
//        where T : ChromInfo
    {
        public ChromInfoList(params T[] elements)
            : base(elements)
        {
        }

        public ChromInfoList(IList<T> elements)
            : base(elements)
        {
        }
    }

    /// <summary>
    /// Base class for a single measured result for a single <see cref="DocNode"/>
    /// in a single file of a single replicate.
    /// </summary>
    public abstract class ChromInfo : Immutable
    {
        protected ChromInfo(int fileIndex)
        {
            FileIndex = fileIndex;
        }

        public int FileIndex { get; private set; }

        #region object overrides

        public bool Equals(ChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.FileIndex == FileIndex;
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
            return FileIndex;
        }

        #endregion
    }
}