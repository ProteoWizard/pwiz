using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Attributes
{
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
