using System.Collections;
using System.Drawing;

namespace pwiz.Common.Colors
{
    public interface IColorScheme
    {
        void Recalibrate(IEnumerable values);
        Color? GetColor(object value);
    }
}
