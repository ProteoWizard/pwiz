using System.Collections.Generic;
using System.Drawing;
using ZedGraph;

namespace pwiz.MSGraph
{
    public class LabelBoundsCache
    {
        private class Label
        {
            public string Text;
            public float FontSize;

            private bool Equals(Label other)
            {
                return string.Equals(Text, other.Text) && FontSize.Equals(other.FontSize);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Label) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Text.GetHashCode()*397) ^ FontSize.GetHashCode();
                }
            }

            public override string ToString()
            {
                return FontSize + ", " + Text; // Not L10N
            }
        }

        private readonly Dictionary<Label, SizeF> _textBoxSizes = new Dictionary<Label, SizeF>();

        public RectangleF GetLabelBounds(TextObj textObj, GraphPane graphPane, Graphics graphics)
        {
            SizeF size;
            lock (_textBoxSizes)
            {
                var label = new Label {Text = textObj.Text, FontSize = textObj.FontSpec.Size};
                if (!_textBoxSizes.TryGetValue(label, out size))
                {
                    const float scaleFactor = 1.0f;

                    // This is a really expensive call, so we're caching its result across threads.
                    var coords = textObj.FontSpec.GetBox(
                        graphics, textObj.Text, 0, 0,
                        textObj.Location.AlignH, textObj.Location.AlignV, scaleFactor, new SizeF());

                    // Turn four points into a size.
                    var min = coords[0];
                    var max = min;
                    for (int i = 1; i < coords.Length; i++)
                    {
                        var point = coords[i];
                        if (min.X > point.X)
                            min.X = point.X;
                        if (min.Y > point.Y)
                            min.Y = point.Y;
                        if (max.X < point.X)
                            max.X = point.X;
                        if (max.Y < point.Y)
                            max.Y = point.Y;
                    }
                    size = new SizeF(max.X - min.X, max.Y - min.Y);

                    // Cache the result.
                    _textBoxSizes[label] = size;
                }
            }

            PointF pix = textObj.Location.Transform(graphPane);
            return new RectangleF(pix, size);
        }
    }
}
