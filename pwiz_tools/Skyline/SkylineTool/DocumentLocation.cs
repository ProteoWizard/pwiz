/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace SkylineTool
{
    [Serializable]
    public class DocumentLocation
    {
        public DocumentLocation(IEnumerable<int> idPath)
        {
            IdPath = idPath.ToList();
        }
        protected DocumentLocation(DocumentLocation documentLocation)
        {
            IdPath = documentLocation.IdPath;
            ChromFileId = documentLocation.ChromFileId;
            ReplicateIndex = documentLocation.ReplicateIndex;
            OptStep = documentLocation.OptStep;
        }

        public IList<int> IdPath { get; private set; }
        public DocumentLocation SetIdPath(IEnumerable<int> value)
        {
            return new DocumentLocation(this){IdPath= value.ToList()};
        }
        public int? ChromFileId { get; private set; }
        public DocumentLocation SetChromFileId(int? value)
        {
            return new DocumentLocation(this){ChromFileId = value};
        }
        public int? ReplicateIndex { get; private set; }

        public DocumentLocation SetReplicateIndex(int? value)
        {
            return new DocumentLocation(this){ReplicateIndex = value};
        }
        public int? OptStep { get; private set; }

        public DocumentLocation SetOptStep(int? value)
        {
            return new DocumentLocation(this){OptStep = value};
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            pushValue(parts, "chromFileId", ChromFileId); // Not L10N
            pushValue(parts, "replicateIndex", ReplicateIndex); // Not L10N
            pushValue(parts, "optStep", OptStep); // Not L10N
            string result = string.Join("/", IdPath); // Not L10N
            if (parts.Any())
            {
                result += "?" + string.Join("&", parts); // Not L10N
            }
            return result;
        }

        protected bool Equals(DocumentLocation other)
        {
            return EqualsDeep(IdPath, other.IdPath) 
                && ChromFileId == other.ChromFileId 
                && ReplicateIndex == other.ReplicateIndex 
                && OptStep == other.OptStep;
        }

        public static bool EqualsDeep<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            if (values1.Count != values2.Count)
                return false;
            for (int i = 0; i < values1.Count; i++)
            {
                if (!Equals(values1[i], values2[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DocumentLocation) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = GetHashCodeDeep(IdPath, v => v.GetHashCode());
                hashCode = (hashCode*397) ^ ChromFileId.GetHashCode();
                hashCode = (hashCode*397) ^ ReplicateIndex.GetHashCode();
                hashCode = (hashCode*397) ^ OptStep.GetHashCode();
                return hashCode;
            }
        }

        public static int GetHashCodeDeep<TItem>(IList<TItem> values, Func<TItem, int> getHashCode)
        {
            unchecked
            {
                int result = 0;
                foreach (TItem value in values)
                    result = (result * 397) ^ (!Equals(value, default(TItem)) ? getHashCode(value) : 0);
                return result;
            }
        }

        public static DocumentLocation Parse(string str)
        {
            int ichQuery = str.IndexOf('?');
            DocumentLocation documentLocation;
            if (ichQuery < 0)
            {
                documentLocation = new DocumentLocation(ParseIdPath(str));
            }
            else
            {
                documentLocation = new DocumentLocation(ParseIdPath(str.Substring(0, ichQuery)));
                NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(str.Substring(ichQuery + 1));
                documentLocation = documentLocation
                    .SetChromFileId(GetIntValue(nameValueCollection, "chromFileId")) // Not L10N
                    .SetReplicateIndex(GetIntValue(nameValueCollection, "replicateIndex")) // Not L10N
                    .SetOptStep(GetIntValue(nameValueCollection, "optStep")); // Not L10N
            }
            return documentLocation;
        }

        private static int? GetIntValue(NameValueCollection nameValueCollection, string key)
        {
            string value = nameValueCollection.Get(key);
            if (null != value)
            {
                return Convert.ToInt32(value);
            }
            return null;
        }

        private static IList<int> ParseIdPath(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return new List<int>();
            }
            return str.Split('/').Select(part=>Convert.ToInt32(part)).ToList();
        }

        public string IdPathToString()
        {
            return string.Join("/", IdPath); // Not L10N
        }

        private static void pushValue(List<string> parts, string key, int? value)
        {
            if (value.HasValue)
            {
                parts.Add(key + '=' + value);
            }
        }
    }
}
