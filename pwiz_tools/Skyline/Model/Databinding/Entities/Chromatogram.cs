/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class Chromatogram
    {
        private Lazy<ChromatogramInfo> _chromatogramInfo;
        public Chromatogram(ChromatogramGroup chromatogramGroup, Transition transition)
        {
            ChromatogramGroup = chromatogramGroup;
            Transition = transition;
            _chromatogramInfo = new Lazy<ChromatogramInfo>(GetChromatogramInfo);
        }

        [Browsable(false)]
        public ChromatogramGroup ChromatogramGroup { get; private set; }
        [Browsable(false)]
        public Transition Transition { get; private set; }

        [Format(Formats.Mz, NullValue = TextUtil.EXCEL_NA)]
        public double? ChromatogramPrecursorMz
        {
            get { return _chromatogramInfo.Value == null ? (double?) null : _chromatogramInfo.Value.PrecursorMz; }
        }

        [Format(Formats.Mz, NullValue = TextUtil.EXCEL_NA)]
        public double? ChromatogramProductMz 
        {
            get { return _chromatogramInfo.Value == null ? (double?) null : _chromatogramInfo.Value.ProductMz; } 
        }

        [Format(Formats.Mz, NullValue = TextUtil.EXCEL_NA)]
        public double? ChromatogramExtractionWidth
        {
            get
            {
                return _chromatogramInfo.Value == null ? null : _chromatogramInfo.Value.ExtractionWidth;
            } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? ChromatogramIonMobility { get { return _chromatogramInfo.Value == null ? null : _chromatogramInfo.Value.IonMobility; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? ChromatogramIonMobilityExtractionWidth { get { return _chromatogramInfo.Value == null ? null : _chromatogramInfo.Value.IonMobilityExtractionWidth; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public string ChromatogramIonMobilityUnits
        {
            get
            {
                if (_chromatogramInfo.Value == null)
                    return null;
                return IonMobilityFilter.IonMobilityUnitsL10NString(_chromatogramInfo.Value.IonMobilityUnits);
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public ChromSource? ChromatogramSource { get { return _chromatogramInfo.Value == null ? (ChromSource?)null : _chromatogramInfo.Value.Source; } }

        [Expensive]
        [ChildDisplayName("Raw{0}")]
        public Data RawData
        {
            get
            {
                var timeIntensitiesGroup = ChromatogramGroup.ReadTimeIntensitiesGroup();
                if (timeIntensitiesGroup is RawTimeIntensities)
                {
                    return new Data(timeIntensitiesGroup.TransitionTimeIntensities[_chromatogramInfo.Value.TransitionIndex], GetLazyMsDataFileScanIds());
                }
                return null;
            }
        }

        [Expensive]
        [ChildDisplayName("Interpolated{0}")]
        public Data InterpolatedData
        {
            get
            {
                var timeIntensitiesGroup = ChromatogramGroup.ReadTimeIntensitiesGroup();
                if (null == timeIntensitiesGroup)
                {
                    return null;
                }
                var rawTimeIntensities = timeIntensitiesGroup as RawTimeIntensities;
                if (null != rawTimeIntensities)
                {
                    var interpolatedTimeIntensities = rawTimeIntensities
                        .TransitionTimeIntensities[_chromatogramInfo.Value.TransitionIndex]
                        .Interpolate(rawTimeIntensities.GetInterpolatedTimes(), rawTimeIntensities.InferZeroes);
                    return new Data(interpolatedTimeIntensities, GetLazyMsDataFileScanIds());
                }
                return new Data(timeIntensitiesGroup.TransitionTimeIntensities[_chromatogramInfo.Value.TransitionIndex], GetLazyMsDataFileScanIds());
            }
        }

        private ChromatogramInfo GetChromatogramInfo()
        {
            var chromatogramGroupInfo = ChromatogramGroup.ChromatogramGroupInfo;
            if (null == chromatogramGroupInfo)
            {
                return null;
            }
            float tolerance = (float) Transition.DataSchema.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var chromatogramInfos = chromatogramGroupInfo.GetAllTransitionInfo(Transition.DocNode, tolerance,
                ChromatogramGroup.PrecursorResult.GetResultFile().Replicate.ChromatogramSet.OptimizationFunction, TransformChrom.raw);
            return chromatogramInfos.GetChromatogramForStep(0);
        }

        private Lazy<MsDataFileScanIds> GetLazyMsDataFileScanIds()
        {
            return new Lazy<MsDataFileScanIds>(ChromatogramGroup.ReadMsDataFileScanIds);
        }

        public class Data
        {
            private TimeIntensities _timeIntensities;
            private Lazy<MsDataFileScanIds> _scanIds;
            public Data(TimeIntensities timeIntensities, Lazy<MsDataFileScanIds> scanIds)
            {
                _timeIntensities = timeIntensities;
                _scanIds = scanIds;
            }
            [Format(NullValue = TextUtil.EXCEL_NA)]
            public int NumberOfPoints { get { return _timeIntensities.NumPoints; } }
            [Format(Formats.RETENTION_TIME)]
            public FormattableList<float> Times { get { return new FormattableList<float>(_timeIntensities.Times); } }
            [Format(Formats.PEAK_AREA)]
            public FormattableList<float> Intensities { get { return new FormattableList<float>(_timeIntensities.Intensities); } }
            [Format(Formats.MASS_ERROR)]
            public FormattableList<float> MassErrors { get { return new FormattableList<float>(_timeIntensities.MassErrors); }}

            public FormattableList<string> SpectrumIds
            {
                get
                {
                    if (_timeIntensities.ScanIds == null || _scanIds == null)
                    {
                        return null;
                    }

                    var scanIds = _scanIds.Value;
                    if (scanIds == null)
                    {
                        return null;
                    }

                    return new FormattableList<string>(_timeIntensities.ScanIds
                        .Select(index => scanIds.GetMsDataFileSpectrumId(index)).ToArray());
                }
            }

            public override string ToString()
            {
                return string.Format(Resources.Data_ToString__0__points, NumberOfPoints);
            }
        }

        public override string ToString()
        {
            if (_chromatogramInfo.Value == null)
            {
                return TextUtil.EXCEL_NA;
            }

            return string.Format(@"{0}: {1}/{2}", ChromatogramSource,
                ChromatogramPrecursorMz.Value.ToString(Formats.Mz, CultureInfo.CurrentCulture),
                ChromatogramProductMz.Value.ToString(Formats.Mz, CultureInfo.CurrentCulture));
        }

#if false         // TODO(nicksh): peaks
        public class Peak
        {
            public double RetentionTime { get; private set; }
            public double StartTime { get; private set; }
            public double EndTime { get; private set; }
            public double Area { get; private set; }
            public double BackgroundArea { get; private set; }
            public double Height { get; private set; }
            public double Fwhm { get; private set; }
            public int? PointsAcross { get; private set; }
            public bool FwhmDegenerate { get; private set; }
            public bool ForcedIntegration { get; private set; }
            public PeakIdentification PeakIdentification { get; private set; }
        }
#endif
    }
}
