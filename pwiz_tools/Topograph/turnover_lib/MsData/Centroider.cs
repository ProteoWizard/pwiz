using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.MsData
{
    public class Centroider
    {
        public Centroider(IList<double> mzs, IList<double> intensities)
        {
            Mzs = mzs;
            Intensities = intensities;
        }

        public IList<double> Mzs { get; private set; }
        public IList<double> Intensities { get; private set; }

        public void GetCentroidedData(out double[] centroidedMzs, out double[] centroidedIntensities)
        {
            var centroidedMzsList = new List<double>();
            var centroidedIntensitiesList = new List<double>();
            
            bool increasing = true;
            double currentMz = 0;
            double currentIntensity = 0;
            double lastIntensity = 0;
            for (int i = 0; i < Mzs.Count(); i++)
            {
                var intensity = Intensities[i];
                if (intensity < lastIntensity)
                {
                    increasing = false;
                }
                else
                {
                    if (!increasing)
                    {
                        if (currentIntensity > 0)
                        {
                            centroidedMzsList.Add(currentMz);
                            centroidedIntensitiesList.Add(currentIntensity);
                        }
                        currentIntensity = 0;
                    }
                    increasing = true;
                }
                var nextIntensity = currentIntensity + intensity;
                if (nextIntensity > 0)
                {
                    var mz = Mzs[i];
                    currentMz = (currentMz * currentIntensity + mz * intensity) / nextIntensity;
                    currentIntensity = nextIntensity;
                }
                lastIntensity = intensity;
            }
            if (currentIntensity > 0)
            {
                centroidedMzsList.Add(currentMz);
                centroidedIntensitiesList.Add(currentIntensity);
            }
            centroidedMzs = centroidedMzsList.ToArray();
            centroidedIntensities = centroidedIntensitiesList.ToArray();
        }
    }
}
