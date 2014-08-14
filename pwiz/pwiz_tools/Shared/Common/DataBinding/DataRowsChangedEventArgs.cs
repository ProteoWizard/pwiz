using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class DataRowsChangedEventArgs : EventArgs
    {
        private static readonly IEntity[] EmptyArray = new IEntity[0];
        public DataRowsChangedEventArgs()
        {
            Added = Deleted = Changed = EmptyArray;
        }
        public ICollection<IEntity> Added { get; set; }
        public ICollection<IEntity> Deleted { get; set; }
        public ICollection<IEntity> Changed { get; set; }
    }

    public delegate void DataRowsChangedEventHandler(object sender, DataRowsChangedEventArgs args);
}
