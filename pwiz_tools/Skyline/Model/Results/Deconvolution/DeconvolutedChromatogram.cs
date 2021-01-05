namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class DeconvolutedChromatogram
    {
        public DeconvolutedChromatogram(DeconvolutionKey key, TimeIntensities timeIntensities)
        {
            Key = key;
            TimeIntensities = timeIntensities;
        }

        public DeconvolutionKey Key { get; private set; }
        public TimeIntensities TimeIntensities { get; private set; }
    }
}
