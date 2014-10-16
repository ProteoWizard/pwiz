using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class DocumentLocation
    {
        public static readonly DocumentLocation ROOT = new DocumentLocation(ImmutableList.Empty<int>());

        public DocumentLocation(IdentityPath identityPath)
        {
            IdPath = ImmutableList.ValueOf(ToIdPath(identityPath));
        }
        private DocumentLocation(IEnumerable<int> idPath)
        {
            IdPath = ImmutableList.ValueOf(idPath);
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
            return new DocumentLocation(this){IdPath= ImmutableList.ValueOf(value)};
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
            return ArrayUtil.EqualsDeep(IdPath, other.IdPath) 
                && ChromFileId == other.ChromFileId 
                && ReplicateIndex == other.ReplicateIndex 
                && OptStep == other.OptStep;
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
                int hashCode = IdPath.GetHashCodeDeep();
                hashCode = (hashCode*397) ^ ChromFileId.GetHashCode();
                hashCode = (hashCode*397) ^ ReplicateIndex.GetHashCode();
                hashCode = (hashCode*397) ^ OptStep.GetHashCode();
                return hashCode;
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

        public Bookmark ToBookmark(SrmDocument document)
        {
            Bookmark bookmark = new Bookmark();
            if (IdPath.Any())
            {
                IdentityPath identityPath = ToIdentityPath(document, IdPath);
                if (null == identityPath)
                {
                    throw new ArgumentException("Unable to find target node " + IdPathToString(IdPath)); // Not L10N
                }
                bookmark = bookmark.ChangeIdentityPath(identityPath);
            }
            if (ChromFileId.HasValue)
            {
                ChromFileInfoId chromFileInfoId = null;
                if (document.Settings.HasResults)
                {
                    foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
                    {
                        var chromFileInfo =
                            chromatogramSet.MSDataFileInfos.FirstOrDefault(
                                fileInfo => fileInfo.Id.GlobalIndex == ChromFileId);
                        if (null != chromFileInfo)
                        {
                            chromFileInfoId = chromFileInfo.FileId;
                        }
                    }
                }
                if (null == chromFileInfoId)
                {
                    throw new ArgumentException("Unable to find file id " + ChromFileId); // Not L10N
                }
                bookmark = bookmark.ChangeChromFileInfoId(chromFileInfoId);
            }
            if (OptStep.HasValue)
            {
                bookmark = bookmark.ChangeOptStep(OptStep.Value);
            }
            return bookmark;
        }

        private static IdentityPath ToIdentityPath(SrmDocument document, IEnumerable<int> idPath)
        {
            IdentityPath identityPath = IdentityPath.ROOT;
            DocNode next = document;
            foreach (int globalIndex in idPath)
            {
                DocNodeParent parent = next as DocNodeParent;
                if (null == parent)
                {
                    return null;
                }
                next = null;
                foreach (var child in parent.Children)
                {
                    if (child.Id.GlobalIndex == globalIndex)
                    {
                        next = child;
                        break;
                    }
                }
                if (null == next)
                {
                    return null;
                }
                identityPath = new IdentityPath(identityPath, next.Id);
            }
            return identityPath;
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
                return ImmutableList.Empty<int>();
            }
            return ImmutableList.ValueOf(str.Split('/').Select(part=>Convert.ToInt32(part)));
        }

        private static IEnumerable<int> ToIdPath(IdentityPath identityPath)
        {
            if (identityPath.Depth < 0)
            {
                return new int[0];
            }
            return ToIdPath(identityPath.Parent).Concat(new [] {identityPath.Child.GlobalIndex});
        }

        private static string IdPathToString(IEnumerable<int> idPath)
        {
            return string.Join("/", idPath); // Not L10N
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
