/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;

namespace pwiz.ProteomeDatabase.DataModel
{
    public class DbSubsequence 
    {
        public virtual string Sequence { get; set; }

        public virtual byte[] ProteinIdBytes { get; set; }

        public virtual long[] ProteinIds
        {
            get { return BytesToProteinIds(ProteinIdBytes); }
            set { ProteinIdBytes = ProteinIdsToBytes(value); }
        }

        public override bool Equals(Object o)
        {
            if (Sequence == null)
            {
                // ReSharper disable BaseObjectEqualsIsObjectEquals
                return base.Equals(o);
                // ReSharper restore BaseObjectEqualsIsObjectEquals
            }
            DbSubsequence that = o as DbSubsequence;
            if (that == null)
            {
                return false;
            }
            return StringComparer.Ordinal.Equals(Sequence, that.Sequence);
        }
        public override int GetHashCode()
        {
            if (Sequence == null)
            {
                // ReSharper disable BaseObjectGetHashCodeCallInGetHashCode
                return base.GetHashCode();
                // ReSharper restore BaseObjectGetHashCodeCallInGetHashCode
            }
            return StringComparer.Ordinal.GetHashCode(Sequence);
        }

        public static long[] BytesToProteinIds(byte[] proteinIdBytes)
        {
            var longs = new List<long>();
            var codedInputStream = new CodedInputStream(proteinIdBytes);
            while (!codedInputStream.IsAtEnd)
            {
                longs.Add(codedInputStream.ReadInt64());
            }
            return longs.ToArray();
        }

        public static byte[] ProteinIdsToBytes(long[] proteinIds)
        {
            var memoryStream = new MemoryStream();
            var codedOutputStream = new CodedOutputStream(memoryStream);
            foreach (var proteinId in proteinIds)
            {
                codedOutputStream.WriteInt64(proteinId);
            }
            codedOutputStream.Flush();
            return memoryStream.ToArray();
        }
    }
}
