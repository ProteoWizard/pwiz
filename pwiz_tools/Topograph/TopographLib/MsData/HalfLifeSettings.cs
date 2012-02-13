using System.Xml.Serialization;

namespace pwiz.Topograph.MsData
{
    [XmlRoot("half_life_settings")]
    public struct HalfLifeSettings
    {
        public static readonly HalfLifeSettings Default = new HalfLifeSettings
                                                     {
                                                         NewlySynthesizedTracerQuantity = TracerQuantity.PartialLabelDistribution,
                                                         PrecursorPoolCalculation = PrecursorPoolCalculation.MedianPerSample,
                                                         SimpleLinearRegression = true,
                                                         CurrentPrecursorPool = 100,
                                                     };

        public bool ForceThroughOrigin { get; set; }
        public PrecursorPoolCalculation PrecursorPoolCalculation { get; set; }
        public TracerQuantity NewlySynthesizedTracerQuantity { get; set; }
        public EvviesFilterEnum EvviesFilter { get; set; }
        public double MinimumAuc { get; set; }
        public double MinimumDeconvolutionScore { get; set; }
        public double MinimumTurnoverScore { get; set; }
        public double InitialPrecursorPool { get; set; }
        public double CurrentPrecursorPool { get; set; }
        public bool ByProtein { get; set; }
        public bool BySample { get; set; }
        public bool SimpleLinearRegression { get; set; }

        public static double TryParseDouble(string strValue, double defaultValue)
        {
            double value;
            if (string.IsNullOrEmpty(strValue) || !double.TryParse(strValue, out value))
            {
                return defaultValue;
            }
            return value;
        }
    }
}
