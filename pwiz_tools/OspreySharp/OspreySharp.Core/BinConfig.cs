namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Configuration for m/z binning used in spectral matching.
    /// Maps to osprey-core/src/types.rs BinConfig.
    /// </summary>
    public struct BinConfig
    {
        public double BinWidth { get; set; }
        public double BinOffset { get; set; }
        public double InverseBinWidth { get; set; }
        public double OneMinusOffset { get; set; }
        public double MaxMz { get; set; }
        public int NBins { get; set; }

        /// <summary>
        /// Creates a BinConfig for unit-resolution instruments.
        /// </summary>
        public static BinConfig UnitResolution()
        {
            double binWidth = 1.0005079;
            double offset = 0.4;
            double maxMz = 2000.0;
            double inverseBinWidth = 1.0 / binWidth;
            double oneMinusOffset = 1.0 - offset;
            int nBins = (int)(maxMz * inverseBinWidth + oneMinusOffset) + 1;

            return new BinConfig
            {
                BinWidth = binWidth,
                BinOffset = offset,
                InverseBinWidth = inverseBinWidth,
                OneMinusOffset = oneMinusOffset,
                MaxMz = maxMz,
                NBins = nBins
            };
        }

        /// <summary>
        /// Creates a BinConfig for high-resolution accurate mass instruments.
        /// </summary>
        public static BinConfig HRAM()
        {
            double binWidth = 0.02;
            double offset = 0.0;
            double maxMz = 2000.0;
            double inverseBinWidth = 1.0 / binWidth;
            double oneMinusOffset = 1.0 - offset;
            int nBins = (int)(maxMz * inverseBinWidth + oneMinusOffset) + 1;

            return new BinConfig
            {
                BinWidth = binWidth,
                BinOffset = offset,
                InverseBinWidth = inverseBinWidth,
                OneMinusOffset = oneMinusOffset,
                MaxMz = maxMz,
                NBins = nBins
            };
        }

        /// <summary>
        /// Converts an m/z value to a bin index.
        /// </summary>
        public int MzToBin(double mz)
        {
            return (int)(mz * InverseBinWidth + OneMinusOffset);
        }

        /// <summary>
        /// Converts a bin index back to an m/z value.
        /// </summary>
        public double BinToMz(int bin)
        {
            return (bin - OneMinusOffset) / InverseBinWidth;
        }
    }
}
