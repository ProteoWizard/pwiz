using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class DataRowsChangedEventArgs : EventArgs
    {
        private static readonly object[] EmptyArray = new object[0];
        public DataRowsChangedEventArgs()
        {
            Added = Deleted = Changed = EmptyArray;
        }
        public ICollection<object> Added { get; set; }
        public ICollection<object> Deleted { get; set; }
        public ICollection<object> Changed { get; set;}
    }

    public delegate void DataRowsChangedEventHandler(object sender, DataRowsChangedEventArgs args);
}
