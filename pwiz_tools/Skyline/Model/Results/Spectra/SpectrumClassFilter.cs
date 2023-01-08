using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [XmlRoot(XML_ROOT)]
    public class SpectrumClassFilter : Immutable, IXmlSerializable, IComparable, IComparable<SpectrumClassFilter>
    {
        public static readonly SpectrumClassFilter EMPTY = new SpectrumClassFilter(ImmutableList.Empty<FilterSpec>());

        public static SpectrumClassFilter EmptyToNull(SpectrumClassFilter spectrumClassFilter)
        {
            return true == spectrumClassFilter?.IsEmpty ? null : spectrumClassFilter;
        }
        public const string XML_ROOT = "spectrum_filter";
        public SpectrumClassFilter(IEnumerable<FilterSpec> filterSpecs)
        {
            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public ImmutableList<FilterSpec> FilterSpecs { get; private set; }

        public bool IsEmpty
        {
            get { return FilterSpecs.Count == 0; }
        }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            var dataSchema = new DataSchema();
            var clauses = new List<Predicate<SpectrumMetadata>>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    throw new InvalidOperationException(string.Format("No such spectrum column {0}",
                        filterSpec.ColumnId));
                }

                var filterPredicate = filterSpec.Predicate.MakePredicate(dataSchema, spectrumClassColumn.ValueType);
                clauses.Add(spectrum=>filterPredicate(spectrumClassColumn.GetValue(spectrum)));
            }

            return spectrum =>
            {
                for (int i = 0; i < clauses.Count; i++)
                {
                    if (!clauses[i](spectrum))
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        protected bool Equals(SpectrumClassFilter other)
        {
            return FilterSpecs.Equals(other.FilterSpecs);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumClassFilter) obj);
        }

        public override int GetHashCode()
        {
            return FilterSpecs.GetHashCode();
        }

        private SpectrumClassFilter()
        {

        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private enum EL
        {
            filter
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (FilterSpecs != null)
            {
                throw new InvalidOperationException();
            }

            var filterSpecs = new List<FilterSpec>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (true)
                {
                    if (reader.IsStartElement(EL.filter))
                    {
                        filterSpecs.Add(FilterSpec.ReadXml(reader));
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        reader.ReadEndElement();
                        break;
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var filterSpec in FilterSpecs)
            {
                writer.WriteStartElement(EL.filter);
                filterSpec.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static SpectrumClassFilter Deserialize(XmlReader xmlReader)
        {
            return xmlReader.Deserialize(new SpectrumClassFilter());
        }

        public int CompareTo(SpectrumClassFilter other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = FilterSpecs.Count.CompareTo(other.FilterSpecs.Count);
            if (result != 0)
            {
                return result;
            }
            Assume.AreEqual(FilterSpecs.Count, other.FilterSpecs.Count);
            for (int i = 0; i < FilterSpecs.Count; i++)
            {
                result = FilterSpecs[i].ColumnId.CompareTo(other.FilterSpecs[i].ColumnId);
                if (result == 0)
                {
                    result = StringComparer.Ordinal.Compare(FilterSpecs[i].Operation.OpName,
                        other.FilterSpecs[i].Operation.OpName);
                }

                if (result == 0)
                {
                    result = StringComparer.Ordinal.Compare(FilterSpecs[i].Predicate.InvariantOperandText,
                        other.FilterSpecs[i].Predicate.InvariantOperandText);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        int IComparable.CompareTo(object obj)
        {
            return CompareTo((SpectrumClassFilter) obj);
        }

        public static string GetOperandDisplayText(DataSchema dataSchema, FilterSpec filterSpec)
        {
            var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
            if (spectrumClassColumn == null)
            {
                return filterSpec.Predicate.InvariantOperandText;
            }

            var operandType = filterSpec.Operation.GetOperandType(dataSchema, spectrumClassColumn.ValueType);
            if (operandType == null)
            {
                return null;
            }

            try
            {
                var value = filterSpec.Predicate.GetOperandValue(dataSchema, spectrumClassColumn.ValueType);
                if (value == null)
                {
                    return string.Empty;
                }

                return spectrumClassColumn.FormatAbbreviatedValue(value);
            }
            catch
            {
                return filterSpec.Predicate.GetOperandDisplayText(dataSchema, spectrumClassColumn.ValueType);
            }
        }

        public string GetAbbreviatedText()
        {
            var dataSchema = new DataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var clauses = new List<string>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    clauses.Add(TextUtil.SpaceSeparate(filterSpec.Column, filterSpec.Operation?.DisplayName, filterSpec.Predicate.InvariantOperandText));
                }
                else
                {
                    var clauseText = new StringBuilder(spectrumClassColumn.GetAbbreviatedColumnName());
                    var opText = filterSpec.Operation.ShortDisplayName;
                    if (char.IsLetterOrDigit(opText[0]) || char.IsLetterOrDigit(opText[opText.Length - 1]))
                    {
                        clauseText.Append(@" ");
                        clauseText.Append(opText);
                        clauseText.Append(@" ");
                    }
                    else
                    {
                        clauseText.Append(opText);
                    }

                    clauseText.Append(GetOperandDisplayText(dataSchema, filterSpec));
                    clauses.Add(clauseText.ToString().Trim());
                }
            }

            return string.Join(" AND ", clauses);
        }

        public ChromatogramGroupInfo FilterChromatogramGroupInfo(SrmSettings srmSettings, ChromatogramGroupInfo chromatogramGroupInfo)
        {
            var resultFileMetaData = srmSettings.MeasuredResults?.GetResultFileMetaData(chromatogramGroupInfo.FilePath);
            if (resultFileMetaData == null)
            {
                return chromatogramGroupInfo;
            }

            var header = chromatogramGroupInfo.Header;
            header = new ChromGroupHeaderInfo(header.Precursor, 0, header.NumTransitions, 0, header.NumPeaks, 0,
                0, header.MaxPeakIndex, header.NumPeaks, 0, 0, 0, header.Flags,
                header.StartTime, header.EndTime, header.CollisionalCrossSection, header.IonMobilityUnits);
            var chromTransitions = Enumerable.Range(0, header.NumTransitions)
                .Select(chromatogramGroupInfo.GetChromTransitionLocal).ToList();
            var timeIntensitiesGroup =
                FilterTimeIntensitiesGroup(resultFileMetaData, chromTransitions, chromatogramGroupInfo.TimeIntensitiesGroup);
            var peaks = Enumerable.Range(0, header.NumTransitions).SelectMany(chromatogramGroupInfo.GetPeaks).ToList();
            return new ChromatogramGroupInfo(header, chromatogramGroupInfo.CachedFile, chromTransitions, peaks, timeIntensitiesGroup);
        }

        public TimeIntensitiesGroup FilterTimeIntensitiesGroup(ResultFileMetaData resultFileMetaData,
            IList<ChromTransition> chromTransitions,
            TimeIntensitiesGroup timeIntensitiesGroup)
        {
            var predicate = MakePredicate();
            var timeIntensitiesList = new List<TimeIntensities>();
            if (chromTransitions != null)
            {
                Assume.AreEqual(chromTransitions.Count, timeIntensitiesGroup.TransitionTimeIntensities.Count);
            }
            for (int iTransition = 0; iTransition < timeIntensitiesGroup.TransitionTimeIntensities.Count; iTransition++)
            {
                var timeIntensities = timeIntensitiesGroup.TransitionTimeIntensities[iTransition];
                if (chromTransitions != null)
                {
                    var chromTransition = chromTransitions[iTransition];
                    if (chromTransition.Source == ChromSource.ms1)
                    {
                        timeIntensitiesList.Add(timeIntensities);
                        continue;
                    }
                }
                timeIntensitiesList.Add(FilterTimeIntensities(resultFileMetaData, predicate, timeIntensities));
            }
            if (timeIntensitiesGroup is RawTimeIntensities rawTimeIntensities)
            {
                return new RawTimeIntensities(timeIntensitiesList, rawTimeIntensities.InterpolationParams)
                    .ChangeTimeIntervals(rawTimeIntensities.TimeIntervals);
            }

            if (timeIntensitiesGroup is InterpolatedTimeIntensities interpolatedTimeIntensities)
            {
                return new InterpolatedTimeIntensities(timeIntensitiesList,
                    interpolatedTimeIntensities.TransitionChromSources);
            }

            return new RawTimeIntensities(timeIntensitiesList, null);
        }

        public static TimeIntensities FilterTimeIntensities(ResultFileMetaData resultFileMetaData,
            Predicate<SpectrumMetadata> predicate,
            TimeIntensities timeIntensities)
        {
            if (timeIntensities.ScanIds == null)
            {
                return timeIntensities;
            }

            var times = new List<float>();
            var intensities = new List<float>();
            List<float> massErrors = null;
            if (timeIntensities.MassErrors != null)
            {
                massErrors = new List<float>();
            }
            var scanIds = new List<int>();
            for (int i = 0; i < timeIntensities.NumPoints; i++)
            {
                var spectrumMetadata = resultFileMetaData.SpectrumMetadatas[timeIntensities.ScanIds[i]];
                if (spectrumMetadata == null || !predicate(spectrumMetadata))
                {
                    continue;
                }
                times.Add(timeIntensities.Times[i]);
                intensities.Add(timeIntensities.Intensities[i]);
                massErrors?.Add(timeIntensities.MassErrors[i]);
                scanIds.Add(timeIntensities.ScanIds[i]);
            }

            return new TimeIntensities(times, intensities, massErrors, scanIds);
        }
    }
}
