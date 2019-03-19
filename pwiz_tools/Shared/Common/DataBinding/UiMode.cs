using System;
using System.Drawing;

namespace pwiz.Common.DataBinding
{
    public struct UiMode
    {
        public static readonly UiMode EMPTY = default(UiMode);
        private readonly Func<string> _getLabelFunc;
        private readonly string _name;
        public UiMode(string name, Image image, Func<string> getLabelFunc)
        {
            _name = name;
            Image = image;
            _getLabelFunc = getLabelFunc;
        }
        public string Name
        {
            get { return _name ?? string.Empty; }
        }
        public Image Image { get; }
        public string Label
        {
            get { return _getLabelFunc?.Invoke() ?? string.Empty; }
        }

        public bool Equals(UiMode other)
        {
            return string.Equals(_name, other._name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is UiMode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }

        public static bool operator ==(UiMode left, UiMode right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UiMode left, UiMode right)
        {
            return !left.Equals(right);
        }
    }
}
