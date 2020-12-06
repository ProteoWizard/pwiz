using System.Collections;
using System.Drawing;

namespace pwiz.Common.Colors
{
    public interface IColorScheme
    {
        void AddValues(IEnumerable values);
        Color? GetColor(object value);
    }
}
