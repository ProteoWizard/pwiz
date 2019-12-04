/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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
