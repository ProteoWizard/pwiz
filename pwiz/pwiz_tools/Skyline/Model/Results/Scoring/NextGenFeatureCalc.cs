/*
 * Original author: Dario Amodei <jegertso .at .u.washington.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
   /// <summary>
    /// Calculates the average mass error for one particular type of ion 
    /// </summary>
    public abstract class MassErrorCalc : SummaryPeakFeatureCalculator
    {
        protected MassErrorCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context,
                                           IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = MQuestHelpers.GetAnalyteGroups(summaryPeakData).ToArray();
            if (tranGroupPeakDatas.Length == 0)
                return float.NaN;

            var massErrors = new List<double>();
            var weights = new List<double>();
            foreach (var pdTran in tranGroupPeakDatas.SelectMany(pd => pd.TranstionPeakData))
            {
                if (!IsIonType(pdTran.NodeTran))
                    continue;
                var peakData = pdTran.PeakData;
                double? massError = peakData.MassError;
                if (massError.HasValue)
                {
                    massErrors.Add(MassErrorFunction(massError.Value));
                    weights.Add(GetWeight(peakData));
                }
            }
            if (massErrors.Count == 0)
                return float.NaN;
            if (weights.All(weight => weight == 0))
                weights = weights.ConvertAll(weight => 1.0);
            var massStats = new Statistics(massErrors);
            var weightsStats = new Statistics(weights);
            return (float) massStats.Mean(weightsStats);
        }

        protected double GetWeight(ISummaryPeakData peakData)
        {
            return peakData.Area;
        }

        public override bool IsReversedScore { get { return true; } }

        protected abstract bool IsIonType(TransitionDocNode nodeTran);

        protected abstract double MassErrorFunction(double massError);
    }

    /// <summary>
    /// Calculates the average mass error over product ions 
    /// </summary>
    public class NextGenProductMassErrorCalc : MassErrorCalc
    {
        public NextGenProductMassErrorCalc() : base("Product mass error") {}  // Not L10N

        public override string Name
        {
            get { return Resources.NextGenProductMassErrorCalc_NextGenProductMassErrorCalc_Product_mass_error; }
        }

        protected override bool IsIonType(TransitionDocNode nodeTran)
        {
            return nodeTran != null && !nodeTran.IsMs1;
        }

        protected override double MassErrorFunction(double massError)
        {
            return Math.Abs(massError);
        }
    }

    /// <summary>
    /// Calculates the average mass error over product ions 
    /// </summary>
    public class NextGenProductMassErrorSquaredCalc : MassErrorCalc
    {
        public NextGenProductMassErrorSquaredCalc() : base("Product mass error squared") { }  // Not L10N

        public override string Name
        {
            get { return Resources.NextGenProductMassErrorCalc_NextGenProductMassErrorCalc_Product_mass_error; }
        }

        protected override bool IsIonType(TransitionDocNode nodeTran)
        {
            return nodeTran != null && !nodeTran.IsMs1;
        }

        protected override double MassErrorFunction(double massError)
        {
            return massError * massError;
        }
    }

    /// <summary>
    /// Calculates the average mass error over precursor ions 
    /// </summary>
    public class NextGenPrecursorMassErrorCalc : MassErrorCalc
    {
        public NextGenPrecursorMassErrorCalc() : base("Precursor mass error") {} // Not L10N

        public override string Name
        {
            get { return Resources.NextGenPrecursorMassErrorCalc_NextGenPrecursorMassErrorCalc_Precursor_mass_error; }
        }

        protected override bool IsIonType(TransitionDocNode nodeTran)
        {
            return nodeTran != null && nodeTran.IsMs1;
        }

        protected override double MassErrorFunction(double massError)
        {
            return Math.Abs(massError);
        }
    }

    /// <summary>
    /// Calculates the isotope dot product (idotp) of the MS1 transitions.
    /// If there is more than one transition group in the peptide,
    /// computes the average of the idotp of the transition groups
    /// </summary>
    public class NextGenIsotopeDotProductCalc : SummaryPeakFeatureCalculator
    {
        public NextGenIsotopeDotProductCalc() : base("Precursor isotope dot product") { }

        public override string Name
        {
            get { return Resources.NextGenIsotopeDotProductCalc_NextGenIsotopeDotProductCalc_Precursor_isotope_dot_product; }
        }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = MQuestHelpers.GetAnalyteGroups(summaryPeakData).ToArray();

            if (tranGroupPeakDatas.Length == 0)
                return float.NaN;

            var isotopeDotProducts = new List<double>();
            var weights = new List<double>();
            foreach (var pdGroup in tranGroupPeakDatas)
            {
                var pds = pdGroup.TranstionPeakData.Where(pd => pd.NodeTran != null && pd.NodeTran.HasDistInfo).ToList();
                if (!pds.Any())
                    continue;
                var peakAreas = pds.Select(pd => (double)pd.PeakData.Area);
                var isotopeProportions = pds.Select(pd => (double)pd.NodeTran.IsotopeDistInfo.Proportion);
                var statPeakAreas = new Statistics(peakAreas);
                var statIsotopeProportions = new Statistics(isotopeProportions);
                var isotopeDotProduct = (float)statPeakAreas.NormalizedContrastAngleSqrt(statIsotopeProportions);
                double weight = statPeakAreas.Sum();
                if (double.IsNaN(isotopeDotProduct))
                    isotopeDotProduct = 0;
                isotopeDotProducts.Add(isotopeDotProduct);
                weights.Add(weight);
            }
            if (isotopeDotProducts.Count == 0)
                return float.NaN;
            // If all weights are zero, return zero instead of NaN
            if (weights.All(weight => weight == 0))
                return 0;
            var idotpStats = new Statistics(isotopeDotProducts);
            var weightsStats = new Statistics(weights);
            return (float)idotpStats.Mean(weightsStats);
        }

        public override bool IsReversedScore { get { return false; } }
    }


    /// <summary>
    /// Calculates the shape correlation score between MS1 ions and MS2 ions
    /// </summary>
    public class NextGenCrossWeightedShapeCalc : MQuestWeightedShapeCalc
    {
        public NextGenCrossWeightedShapeCalc() : base(Resources.NextGenCrossWeightedShapeCalc_NextGenCrossWeightedShapeCalc_Precursor_product_shape_score, "Precursor-product shape score") { }

        protected override IEnumerable<MQuestCrossCorrelation> FilterIons(IEnumerable<MQuestCrossCorrelation> crossCorrMatrix)
        {
            var crossCorrSelect = crossCorrMatrix.Where(xcorr => xcorr.TranPeakData1.NodeTran != null && xcorr.TranPeakData2.NodeTran != null)
                                                 .Where(xcorr => xcorr.TranPeakData1.NodeTran.IsMs1 ^ xcorr.TranPeakData2.NodeTran.IsMs1).ToList();
            if (!crossCorrSelect.ToList().Any())
                return crossCorrSelect;
            var areas = crossCorrSelect.Select(xcorr => xcorr.AreaSum).ToList();
            var maxIndex = areas.IndexOf(areas.Max());
            return crossCorrSelect.GetRange(maxIndex, 1);
        }
    }

    /// <summary>
    /// Calculates the signal to noise ratio in chromatographic space at the position of interest 
    /// </summary>
    public abstract class AbstractNextGenSignalNoiseCalc : DetailedPeakFeatureCalculator
    {
        protected AbstractNextGenSignalNoiseCalc(string name, string headerName) : base(name, headerName) { }

        protected override float Calculate(PeakScoringContext context,
                                           IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = MQuestHelpers.GetAnalyteGroups(summaryPeakData).ToArray();
            if (tranGroupPeakDatas.Length == 0)
                return float.NaN;
            var snValues = new List<double>();
            var weights = new List<double>();
            foreach (var pdTran in tranGroupPeakDatas.SelectMany(pd => pd.TranstionPeakData))
            {
                if (!IsIonType(pdTran.NodeTran))
                    continue;
                var peakData = pdTran.PeakData;
                if (peakData == null || peakData.Intensities == null || peakData.Intensities.Length == 0)
                    continue;
                double? snValue = GetSnValue(peakData);
                if (snValue.HasValue)
                {
                    snValues.Add(Math.Abs(snValue.Value));
                    weights.Add(GetWeight(peakData));
                }
            }
            if (snValues.Count == 0)
                return float.NaN;
            var snStats = new Statistics(snValues);
            var weightsStats = new Statistics(weights);
            if (weights.All(weight => weight == 0))
                return 0;
            return (float)snStats.Mean(weightsStats);
        }

        protected double GetWeight(ISummaryPeakData peakData)
        {
            return peakData.Area;
        }

        protected double? GetSnValue(IDetailedPeakData peakData)
        {
            const int halfRange = 500;
            var intensityList = new List<double>();
            int startNoiseCalc = Math.Max(peakData.StartIndex - halfRange, 0);
            int endNoiseCalc = Math.Min(peakData.EndIndex + halfRange, peakData.Intensities.Length);
            if (peakData.StartIndex > peakData.Intensities.Length || peakData.StartIndex == -1 || peakData.EndIndex == -1)
                return null;
            for (int i = startNoiseCalc; i < peakData.StartIndex; ++i)
                intensityList.Add(peakData.Intensities[i]);
            for (int i = peakData.EndIndex; i < endNoiseCalc; ++i)
                intensityList.Add(peakData.Intensities[i]);
            if (intensityList.Count == 0)
                return null;
            double peakHeight = Math.Max(peakData.Intensities[peakData.TimeIndex], 1);
            // If there is no medianNoise, set it to 1.0
            double medianNoise = Math.Max(GetMedian(intensityList), 1);
            if (peakHeight == 0)
                return 0;
            return Math.Log10(peakHeight / medianNoise);

        }

        protected double GetMedian(List<double> dataList)
        {
            if (dataList.Count == 0)
                return double.NaN;
            var dataStats = new Statistics(dataList);
            return dataStats.Median();
        }

        public override bool IsReversedScore { get { return false; } }

        protected abstract bool IsIonType(TransitionDocNode nodeTran);
    }

    public class NextGenSignalNoiseCalc : AbstractNextGenSignalNoiseCalc
    {
        public NextGenSignalNoiseCalc() : base(Resources.NextGenSignalNoiseCalc_NextGenSignalNoiseCalc_Signal_to_noise, "Signal to noise") { }  // Not L10N

        protected override bool IsIonType(TransitionDocNode nodeTran)
        {
            return nodeTran == null || !nodeTran.IsMs1;
        }
    }
}
