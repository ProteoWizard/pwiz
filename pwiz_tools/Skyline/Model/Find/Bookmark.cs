/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using JetBrains.Annotations;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using SkylineTool;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Remembers a document location in a Skyline Document
    /// </summary>
    public class Bookmark : Immutable
    {
        public static readonly Bookmark ROOT = new Bookmark(IdentityPath.ROOT);
        public Bookmark(IdentityPath identityPath) : this(identityPath, null, null, 0)
        {
        }
        public Bookmark(IdentityPath identityPath, int? replicateIndex, ChromFileInfoId chromFileInfoId, int optStep)
        {
            IdentityPath = identityPath ?? IdentityPath.ROOT;
            ReplicateIndex = replicateIndex;
            ChromFileInfoId = chromFileInfoId;
            OptStep = optStep;
        }

        public bool Equals(Bookmark other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.IdentityPath, IdentityPath)
                   && Equals(ReplicateIndex, other.ReplicateIndex)
                   && ReferenceEquals(other.ChromFileInfoId, ChromFileInfoId)
                   && Equals(other.OptStep, OptStep);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Bookmark)) return false;
            return Equals((Bookmark) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = IdentityPath.GetHashCode();
                result = (result*397) ^ ReplicateIndex.GetHashCode();
                result = (result * 397) ^ (ChromFileInfoId != null ? ChromFileInfoId.GetHashCode() : 0);
                result = (result*397) ^ OptStep.GetHashCode();
                return result;
            }
        }

        public bool IsRoot
        {
            get { return IdentityPath.IsRoot && !ReplicateIndex.HasValue; }
        }
        [NotNull]
        public IdentityPath IdentityPath { get; private set; }
        public Bookmark ChangeIdentityPath(IdentityPath value)
        {
            return ChangeProp(ImClone(this), im => im.IdentityPath = value);
        }

        public int? ReplicateIndex
        {
            get; private set;
        }

        public ChromFileInfoId ChromFileInfoId { get; private set;}

        public Bookmark ChangeResult(int replicateIndex, [NotNull] ChromFileInfoId fileId, int optStep)
        {
            if (fileId == null)
            {
                throw new ArgumentNullException(nameof(fileId));
            }
            return ChangeProp(ImClone(this), im =>
            {

                im.ReplicateIndex = replicateIndex;
                im.ChromFileInfoId = fileId;
                im.OptStep = optStep;
            });
        }

        public Bookmark ClearResult()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ReplicateIndex = null;
                im.ChromFileInfoId = null;
                im.OptStep = 0;
            });
        }
        public int OptStep { get; private set; }
        public static Bookmark ToBookmark(DocumentLocation documentLocation, SrmDocument document)
        {
            Bookmark bookmark = ROOT;
            if (documentLocation.IdPath.Any())
            {
                IdentityPath identityPath = IdentityPath.ToIdentityPath(documentLocation.IdPath, document);
                if (null == identityPath)
                {
                    throw new ArgumentException(@"Unable to find target node " + documentLocation.IdPathToString());
                }
                bookmark = bookmark.ChangeIdentityPath(identityPath);
            }

            if (!documentLocation.ChromFileId.HasValue)
            {
                return bookmark;
            }
            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults != null)
            {
                for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                {
                    var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                    var chromFileInfo =
                        chromatogramSet.MSDataFileInfos.FirstOrDefault(
                            fileInfo => fileInfo.Id.GlobalIndex == documentLocation.ChromFileId);
                    if (null != chromFileInfo)
                    {
                        return bookmark.ChangeResult(replicateIndex, chromFileInfo.FileId,
                            documentLocation.OptStep ?? 0);
                    }

                }
            }
            throw new ArgumentException(@"Unable to find file id " + documentLocation.ChromFileId);
        }
    }
}
