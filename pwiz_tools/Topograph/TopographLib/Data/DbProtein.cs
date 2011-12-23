using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProtein : DbEntity<DbProtein>
    {
        private static readonly MD5 Md5 = MD5.Create();
        private String _sequence;
        private byte[] _md5Hash;
        public virtual byte[] SequenceHash { 
            get
            {
                lock(this)
                {
                    return _md5Hash;
                }
            }
            set
            {
                // no op
            }
        }
        public virtual String Sequence 
        { 
            get
            {
                return _sequence;
            } 
            set
            {
                _sequence = value;
                _md5Hash = Md5.ComputeHash(ArrayConverter.ToBytes(_sequence.ToCharArray()));
            } 
        }
    }
}
