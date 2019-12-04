using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pwiz.Common.SystemUtil
{
    public class CharEnumeratorReader : TextReader
    {
        private int? _nextChar;
        private IEnumerator<char> _enumerator;
        public CharEnumeratorReader(IEnumerator<char> enumerator)
        {
            _enumerator = enumerator;
        }

        public override int Peek()
        {
            _nextChar = Read();
            return _nextChar.Value;
        }

        public override int Read()
        {
            if (_nextChar.HasValue)
            {
                var result = _nextChar.Value;
                _nextChar = null;
                return result;
            }

            if (!_enumerator.MoveNext())
            {
                return -1;
            }

            return _enumerator.Current;
        }

        public static CharEnumeratorReader FromLines(IEnumerable<string> lines)
        {
            var charEnumerator = lines.SelectMany(line => line + Environment.NewLine).GetEnumerator();
            return new CharEnumeratorReader(charEnumerator);
        }
    }
}
