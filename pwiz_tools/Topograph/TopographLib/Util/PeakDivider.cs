using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Util
{
    public class PeakDivider
    {
        public IList<int> DividePeaks(IList<double> intensities)
        {
            var intensityIndexes = new List<KeyValuePair<double, int>>();
            var peaks = new List<int>();
            for (int i = 0; i < intensities.Count; i++)
            {
                intensityIndexes.Add(new KeyValuePair<double, int>(intensities[i], i));
            }
            intensityIndexes.Sort((a, b) => -a.Key.CompareTo(b.Key));
            foreach (var entry in intensityIndexes)
            {
                var index = peaks.BinarySearch(entry.Value);
                if (index < 0)
                {
                    index = ~index;
                }
                if (1 == (index & 1))
                {
                    continue;
                }
                int min = index == 0 ? 0 : peaks[index - 1] + 1;
                int max = index == peaks.Count ? intensities.Count - 1: peaks[index] - 1;
                int start = entry.Value;
                while (start > min && intensities[start - 1] < intensities[start])
                {
                    start--;
                }
                int end = entry.Value;
                while (end < max && intensities[end + 1] < intensities[end])
                {
                    end++;
                }
                if (start == end)
                {
                    continue;
                }
                peaks.Insert(index, end);
                peaks.Insert(index, start);
            }
            return peaks;
        }
    }
}
