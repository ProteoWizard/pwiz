/*
 * From MSDN Forum:
 * http://social.msdn.microsoft.com/forums/en-US/winformsdatacontrols/thread/12eb59d3-e687-4e36-93ab-bf6741954d39/
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
                var pc = new PropertyComparer<T>(property, direction);
                items.Sort(pc);
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

        #region PropertyComparer<TKey>

        internal class PropertyComparer<TKey> : IComparer<TKey>
        {
            /*
            * The following code contains code implemented by Rockford Lhotka:
            * msdn.microsoft.com/library/default.asp?url=/library/en-us/dnadvnet/html/vbnet01272004.asp" 
            */
            private readonly ListSortDirection _direction;
            private readonly PropertyDescriptor _property;

            public PropertyComparer(PropertyDescriptor property, ListSortDirection direction)
            {
                _property = property;
                _direction = direction;
            }

            #region IComparer<TKey> Members

            public int Compare(TKey xVal, TKey yVal)
            {
                /* Get property values */
                object xValue = GetPropertyValue(xVal, _property);
                object yValue = GetPropertyValue(yVal, _property);
                /* Determine sort order */
                if (_direction == ListSortDirection.Ascending)
                {
                    return CompareAscending(xValue, yValue);
                }
                else
                {
                    return CompareDescending(xValue, yValue);
                }
            }

            #endregion

            public bool Equals(TKey xVal, TKey yVal)
            {
                return xVal.Equals(yVal);
            }

            public int GetHashCode(TKey obj)
            {
                return obj.GetHashCode();
            }

            /* Compare two property values of any type */

            private static int CompareAscending(object xValue, object yValue)
            {
                int result;
                // Null sorts before objects with values.
                if (xValue == null)
                {
                    result = (yValue == null) ? 0 : -1;
                }
                else if (yValue == null)
                {
                    result = 1;
                }
                else
                {
                    var value = xValue as IComparable;
                    if (value != null)
                    {
                        /* If values implement IComparer */
                        result = value.CompareTo(yValue);
                    }
                    else if (xValue.Equals(yValue))
                    {
                        /* If values don't implement IComparer but are equivalent */
                        result = 0;
                    }
                    else
                    {
                        /* Values don't implement IComparer and are not equivalent, so compare as string values */
                        result = String.Compare(xValue.ToString(), yValue.ToString(), StringComparison.Ordinal);
                    }
                }
                /* Return result */
                return result;
            }

            private static int CompareDescending(object xValue, object yValue)
            {
                /* Return result adjusted for ascending or descending sort order ie
                   multiplied by 1 for ascending or -1 for descending */
                return CompareAscending(xValue, yValue)*-1;
            }

            private static object GetPropertyValue(TKey value, PropertyDescriptor property)
            {
                /* Get property */
                return property.GetValue(value);
            }
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