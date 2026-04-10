namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Defines an m/z isolation window. Maps to osprey-core/src/types.rs IsolationWindow.
    /// </summary>
    public struct IsolationWindow
    {
        public double Center { get; set; }
        public double LowerOffset { get; set; }
        public double UpperOffset { get; set; }

        public IsolationWindow(double center, double lowerOffset, double upperOffset)
        {
            Center = center;
            LowerOffset = lowerOffset;
            UpperOffset = upperOffset;
        }

        /// <summary>
        /// Creates a symmetric isolation window centered at the given m/z.
        /// </summary>
        public static IsolationWindow Symmetric(double center, double halfWidth)
        {
            return new IsolationWindow(center, halfWidth, halfWidth);
        }

        public double LowerBound { get { return Center - LowerOffset; } }
        public double UpperBound { get { return Center + UpperOffset; } }
        public double Width { get { return LowerOffset + UpperOffset; } }

        /// <summary>
        /// Returns true if the given m/z falls within this window (half-open interval).
        /// </summary>
        public bool Contains(double mz)
        {
            return mz >= LowerBound && mz < UpperBound;
        }
    }
}
