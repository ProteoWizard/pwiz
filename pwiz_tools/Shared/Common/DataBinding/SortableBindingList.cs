/*
 * From MSDN Forum:
 * http://social.msdn.microsoft.com/forums/en-US/winformsdatacontrols/thread/12eb59d3-e687-4e36-93ab-bf6741954d39/
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding
{
    [Serializable]
    public class SortableBindingList<T> : BindingList<T>, ITypedList
    {
        private ListSortDirection _dir = ListSortDirection.Ascending;
        private bool _isSorted;
        [NonSerialized] private PropertyDescriptorCollection _shape;
        [NonSerialized] private PropertyDescriptor _sort;
        private bool _sortColumns;

        #region Constructor

        public SortableBindingList()
        {
            /* Default to sorted columns */
            _sortColumns = true;
            /* Get shape (only get public properties marked browsable true) */
            _shape = GetShape();
        }

        #endregion

        #region SortedBindingList<T> Column Sorting API

        public bool SortColumns
        {
            get { return _sortColumns; }
            set
            {
                if (value != _sortColumns)
                {
                    /* Set Column Sorting */
                    _sortColumns = value;
                    /* Set shape */
                    _shape = GetShape();
                    /* Fire MetaDataChanged */
                    OnListChanged(new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, -1));
                }
            }
        }

        #endregion

        #region BindingList<T> Public Sorting API

        public void Sort()
        {
            ApplySortCore(_sort, _dir);
        }

        public void Sort(string property)
        {
            /* Get the PD */
            _sort = FindPropertyDescriptor(property);
            /* Sort */
            ApplySortCore(_sort, _dir);
        }

        public void Sort(string property, ListSortDirection direction)
        {
            /* Get the sort property */
            _sort = FindPropertyDescriptor(property);
            _dir = direction;
            /* Sort */
            ApplySortCore(_sort, _dir);
        }

        #endregion

        #region BindingList<T> Sorting Overrides

        protected override bool SupportsSortingCore
        {
            get { return true; }
        }

        protected override bool IsSortedCore
        {
            get { return _isSorted; }
        }

        protected override void ApplySortCore(PropertyDescriptor property, ListSortDirection direction)
        {
            var items = Items as List<T>;
// ReSharper disable ConditionIsAlwaysTrueOrFalse
            if ((null != items) && (null != property))
// ReSharper restore ConditionIsAlwaysTrueOrFalse
            {
                bool reverseSort = direction == ListSortDirection.Descending;
                // Create tuples which consist of:
                // Item1: property value we are sorting on
                // Item2: the original row index (or negative of that if we are sorting descending)
                Tuple<object, int>[] propertyRowIndexes = Items.Select((item, index) => Tuple.Create(property.GetValue(item), 
                    reverseSort ? -index : index)).ToArray();
                // Sort the tuples. Because Item2 is the original row index, this is effectively a 
                // stable sort.
                try
                {
                    Array.Sort(propertyRowIndexes);
                }
                catch (Exception)
                {
                    // If there was a problem sorting the tuples, then just sort them as if they were strings.
                    propertyRowIndexes = propertyRowIndexes.Select(rowIndex 
                        => Tuple.Create(rowIndex.Item1 == null ? (object) null : rowIndex.Item1.ToString(), rowIndex.Item2))
                        .ToArray();
                    Array.Sort(propertyRowIndexes);
                }
                if (reverseSort)
                {
                    Array.Reverse(propertyRowIndexes);
                }
                // Replace the items in this list with the ones in sorted order.
                var newItems = propertyRowIndexes.Select(rowIndex => items[reverseSort ? -rowIndex.Item2 : rowIndex.Item2]).ToArray();
                items.Clear();
                items.AddRange(newItems);
                /* Set sorted */
                _isSorted = true;
            }
            else
            {
                /* Set sorted */
                _isSorted = false;
            }
        }

        protected override void RemoveSortCore()
        {
            _isSorted = false;
        }

        #endregion

        #region SortedBindingList<T> Private Sorting API

        private PropertyDescriptor FindPropertyDescriptor(string property)
        {
            PropertyDescriptor prop = null;
            if (null != _shape)
            {
                prop = _shape.Find(property, true);
            }
            return prop;
        }

        private PropertyDescriptorCollection GetShape()
        {
            /* Get shape (only get public properties marked browsable true) */
            PropertyDescriptorCollection pdc = TypeDescriptor.GetProperties(typeof (T),
                                                                            new Attribute[]
                                                                                {new BrowsableAttribute(true)});
            /* Sort if required */
            if (_sortColumns)
            {
                pdc = pdc.Sort();
            }
            return pdc;
        }

        #endregion

        #region ITypedList Implementation

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            PropertyDescriptorCollection pdc;
            if (null == listAccessors)
            {
                /* Return properties in sort order */
                pdc = _shape;
            }
            else
            {
                /* Return child list shape */
                pdc = ListBindingHelper.GetListItemProperties(listAccessors[0].PropertyType);
            }
            return pdc;
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            /* Not really used anywhere other than DT and the old DataGrid */
            return typeof (T).Name;
        }

        #endregion
    }
}