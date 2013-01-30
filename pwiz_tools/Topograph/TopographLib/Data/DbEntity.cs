/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace pwiz.Topograph.Data
{
    public interface IDbEntity
    {
        long? Id { get; set; }
        Type GetEntityType();
    }
    public abstract class DbEntity<T> : IDbEntity 
        where T : DbEntity<T>
    {
        public virtual long? Id 
        { 
            get; set;
        }
        public virtual long GetId()
        {
            // ReSharper disable PossibleInvalidOperationException
            return Id.Value;
            // ReSharper restore PossibleInvalidOperationException
        }
        public override int GetHashCode()
        {
            return typeof(T).GetHashCode() ^ Id.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (Id == null)
            {
                return ReferenceEquals(this, obj);
            }
            var that = obj as T;
            if (that == null)
            {
                return false;
            }
            return Id.Equals(that.Id);
        }
        public virtual Type GetEntityType()
        {
            return typeof (T);
        }
        [DataMember]
        public virtual int Version { get; set; }
        public virtual T Detach()
        {
            if (GetType() == typeof(T))
            {
                return (T)MemberwiseClone();
            }
            var serializer = new DataContractJsonSerializer(typeof(DbMsDataFile));
            var stream = new MemoryStream();
            serializer.WriteObject(stream, this);
            stream.Seek(0, SeekOrigin.Begin);
            return (T) serializer.ReadObject(stream);
        }
    }
}
