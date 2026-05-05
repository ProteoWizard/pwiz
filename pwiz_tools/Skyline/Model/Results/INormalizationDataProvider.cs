using System;
using System.Threading;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    public class NormalizationDataProvider
    {
        private NormalizationData _normalizationData;
        private RtLoessCurves _rtLoessCurves;
        private bool _treatMissingValuesAsZero;
        private double? _qValueCutoff;
        public NormalizationDataProvider(SrmDocument document, NormalizationData normalizationData, RtLoessCurves rtLoessCurves)
        {
            Document = document;
            _normalizationData = normalizationData;
            _rtLoessCurves = rtLoessCurves;
        }

        public NormalizationDataProvider(SrmDocument document) : this(document, null, null)
        {
            
        }

        public NormalizationDataProvider(SrmDocument document, bool treatMissingValuesAsZero, double? qValueCutoff) : this(document)
        {
            _treatMissingValuesAsZero = treatMissingValuesAsZero;
            _qValueCutoff = qValueCutoff;
        }

        public SrmDocument Document { get; }

        public NormalizationData GetNormalizationData()
        {
            if (_normalizationData == null)
            {
                var normalizationData = NormalizationData.GetNormalizationData(Document, _treatMissingValuesAsZero, _qValueCutoff);
                Interlocked.CompareExchange(ref _normalizationData, normalizationData, null);
            }

            return _normalizationData;
        }

        public RtLoessCurves GetRtLoessCurves()
        {
            if (_rtLoessCurves == null)
            {
                var rtLoessCurves = RtLoessCurves.LazyRtLoessCurves(Document).Value;
                Interlocked.CompareExchange(ref _rtLoessCurves, rtLoessCurves, null);
            }

            return _rtLoessCurves;
        }
    }
}
