using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace pwiz.Common.Colors
{
    public class DiscreteColorScheme : IColorScheme
    {
        private IList<Color> _palette = ColorPalettes.LARGE_PALETTE;
        private IDictionary<object, int> _objectIndexes;

        public DiscreteColorScheme(int startingIndex)
        {
            StartingIndex = startingIndex;
        }

        public int StartingIndex { get; private set; }

        public void Calibrate(IEnumerable values)
        {
            var valueToIndex = new Dictionary<object, int>();
            var valuesByType = values.OfType<object>().Distinct().ToLookup(v => v.GetType());
            int index = StartingIndex;
            foreach (var grouping in valuesByType.OrderBy(group => group.Count()))
            {
                foreach (var v in grouping)
                {
                    valueToIndex.Add(v, index++);
                }
            }

            _objectIndexes = valueToIndex;
        }

        public Color? GetColor(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (_objectIndexes == null || !_objectIndexes.TryGetValue(value, out int index))
            {
                return ColorFromIndex(0);
            }

            return ColorFromIndex(index);
        }

        public Color ColorFromIndex(int i)
        {
            return _palette[Math.Abs((i + StartingIndex) % _palette.Count)];
        }
    }
}
