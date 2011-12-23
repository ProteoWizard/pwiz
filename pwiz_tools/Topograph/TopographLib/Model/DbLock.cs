using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbLock : DbEntity<DbLock>
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
        public virtual LockType LockType { get; set; }
        public virtual long? WorkspaceId { get; set; }
        public virtual long? PeptideAnalysisId { get; set; }
        public virtual long? MsDataFileId { get; set; }
    }
    public enum LockType
    {
        chromatograms,
        results,
    }
    public class InvalidLockException : ApplicationException
    {
        
    }
}
