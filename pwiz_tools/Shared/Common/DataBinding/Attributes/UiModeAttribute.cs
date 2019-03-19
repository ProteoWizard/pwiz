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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Attributes
{
    /// <summary>
    /// Base class for attributes that may apply in a subset of UI modes.
    /// </summary>
    public abstract class InUiModesAttribute : Attribute
    {
        private ImmutableList<string> _uiModes = ImmutableList<string>.EMPTY;
        private ImmutableList<string> _exceptInUiModes = ImmutableList<string>.EMPTY;

        public string InUiMode
        {
            get { return _uiModes.Count == 1 ? _uiModes.First() : null; }
            set { _uiModes = ImmutableList.Singleton(value);}
        }

        public IList<string> InUiModes
        {
            get { return _uiModes; }
            set
            {
                _uiModes = ImmutableList.ValueOfOrEmpty(value);
            }
        }

        public string ExceptInUiMode
        {
            get { return _exceptInUiModes.Count == 1 ? _exceptInUiModes.First() : null; }
            set
            {
                _exceptInUiModes = ImmutableList.Singleton(value);
            }
        }

        public IList<string> ExceptInUiModes
        {
            get { return _exceptInUiModes; }
            set
            {
                _exceptInUiModes = ImmutableList.ValueOfOrEmpty(value);
            }
        }

        public bool AppliesInUiMode(string uiMode)
        {
            if (_uiModes.Any())
            {
                return _uiModes.Contains(uiMode);
            }

            return !_exceptInUiModes.Contains(uiMode);
        }
    }
}
