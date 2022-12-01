using System;

namespace pwiz.Common.Progress
{
    public class ProgressSegments
    {
        private readonly IProgress _parent;
        public ProgressSegments(IProgress parent, int segmentCount)
        {
            _parent = parent;
            SegmentCount = segmentCount;
            CurrentSegment = -1;
        }

        public int CurrentSegment { get; set; }
        public int SegmentCount { get; set; }

        public IProgress Progress
        {
            get
            {
                if (SegmentCount <= 0)
                {
                    return _parent;
                }

                int currentSegment = Math.Max(0, Math.Min(SegmentCount - 1, CurrentSegment));
                return new SubtaskProgress(_parent, null, currentSegment * 100.0 / SegmentCount,
                    (currentSegment + 1) * 100.0 / SegmentCount);
            }
        }

        public IProgress NextSegment()
        {
            CurrentSegment++;
            var progress = Progress;
            progress.Value = 0;
            return progress;
        }

        public IProgress NextSegment(string message)
        {
            var progress = NextSegment();
            progress.Message = message;
            return progress;
        }
    }
}
