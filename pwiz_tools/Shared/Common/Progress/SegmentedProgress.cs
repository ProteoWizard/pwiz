namespace pwiz.Common.Progress
{
    public class SegmentedProgress : SubtaskProgress
    {
        public SegmentedProgress(IProgress parent, int currentSegment, int segmentCount) : base(parent, null, currentSegment * 100.0 / segmentCount, (currentSegment + 1) * 100.0 / segmentCount)
        {
            CurrentSegment = currentSegment;
            SegmentCount = segmentCount;
        }

        public int CurrentSegment { get; }
        public int SegmentCount { get; }

        public SegmentedProgress NextSegment()
        {
            int nextSegment = CurrentSegment + 1;
            if (nextSegment < SegmentCount)
            {
                var newProgress = new SegmentedProgress(Parent, nextSegment, SegmentCount);
                newProgress.Value = 0;
                return newProgress;
            }

            return this;
        }

        public static IProgress TryNextSegment(IProgress progress)
        {
            return (progress as SegmentedProgress)?.NextSegment() ?? progress;
        }
    }
}
