using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;


namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class CandidatePeakGroups : IRowSource
    {
        private ImmutableList<CandidatePeakGroup> _list = ImmutableList<CandidatePeakGroup>.EMPTY;

        public ImmutableList<CandidatePeakGroup> List
        {
            get
            {
                return _list;
            }
            set
            {
                _list = value;
                RowSourceChanged?.Invoke();
            }
        }

        public IEnumerable GetItems()
        {
            return _list;
        }

        public event Action RowSourceChanged;
    }
}
