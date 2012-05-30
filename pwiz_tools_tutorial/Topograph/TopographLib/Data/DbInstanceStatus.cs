using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbInstanceStatus : DbEntity<DbInstanceStatus>
    {
        public virtual DateTime TimeStamp { get; set; }
        public virtual byte[] InstanceIdBytes { get; set; }
        public virtual Guid? InstanceIdGuid
        {
            get
            {
                if (InstanceIdBytes == null)
                {
                    return null;
                }
                return new Guid(InstanceIdBytes);
            }
            set
            {
                InstanceIdBytes = value == null ? null : value.Value.ToByteArray();
            }
        }
        public virtual long CurrentChangeLogId { get; set; }
    }
}
