namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A DIA MS2 spectrum with isolation window. Maps to osprey-core/src/types.rs Spectrum.
    /// </summary>
    public class Spectrum
    {
        public uint ScanNumber { get; set; }
        public double RetentionTime { get; set; }
        public double PrecursorMz { get; set; }
        public IsolationWindow IsolationWindow { get; set; }
        public double[] Mzs { get; set; }
        public float[] Intensities { get; set; }

        public int Count { get { return Mzs.Length; } }
        public bool IsEmpty { get { return Count == 0; } }

        /// <summary>
        /// Returns true if the given m/z is contained within this spectrum's isolation window.
        /// </summary>
        public bool ContainsPrecursor(double mz)
        {
            return IsolationWindow.Contains(mz);
        }
    }
}
