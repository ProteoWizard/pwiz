using System.Collections.Generic;

namespace pwiz.Skyline.Model.Results
{
    public interface IChromDataReader
    {
        TimeIntensitiesGroup ReadTimeIntensities(ChromGroupHeaderInfo chromGroupHeaderInfo);
        IList<ChromPeak> ReadPeaks(ChromGroupHeaderInfo chromGroupHeaderInfo);
        IList<float> ReadScores(ChromGroupHeaderInfo chromGroupHeaderInfo);
    }

    public class StaticChromDataReader : IChromDataReader
    {
        private TimeIntensitiesGroup _timeIntensities;
        private IList<ChromPeak> _peaks;
        private IList<float> _scores;
        public StaticChromDataReader(TimeIntensitiesGroup timeIntensities, IList<ChromPeak> peaks,
            IList<float> scores)
        {
            _timeIntensities = timeIntensities;
            _peaks = peaks;
            _scores = scores;
        }

        public TimeIntensitiesGroup ReadTimeIntensities(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _timeIntensities;
        }

        public IList<ChromPeak> ReadPeaks(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _peaks;
        }

        public IList<float> ReadScores(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _scores;
        }
    }
}
