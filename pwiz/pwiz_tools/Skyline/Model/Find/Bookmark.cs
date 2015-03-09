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
using pwiz.Skyline.Model.Results;
using SkylineTool;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Remembers a document location in a Skyline Document
    /// </summary>
    public class Bookmark
    {
        public static readonly Bookmark ROOT = new Bookmark();
        public Bookmark()
        {
            IdentityPath = IdentityPath.ROOT;
        }
        public Bookmark(Bookmark bookmark)
        {
            IdentityPath = bookmark.IdentityPath;
            ChromFileInfoId = bookmark.ChromFileInfoId;
        }
        public Bookmark(IdentityPath identityPath) : this(identityPath, null, 0)
        {
            
        }
        public Bookmark(IdentityPath identityPath, ChromFileInfoId chromFileInfoId, int optStep)
        {
            IdentityPath = identityPath ?? IdentityPath.ROOT;
            ChromFileInfoId = chromFileInfoId;
            OptStep = optStep;
        }

        public bool Equals(Bookmark other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.IdentityPath, IdentityPath)
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
                result = (result*397) ^ (ChromFileInfoId != null ? ChromFileInfoId.GetHashCode() : 0);
                result = (result*397) ^ OptStep.GetHashCode();
                return result;
            }
        }

        public bool IsRoot
        {
            get { return IdentityPath.IsRoot && ChromFileInfoId == null; }
        }
        public IdentityPath IdentityPath { get; private set; }
        public Bookmark ChangeIdentityPath(IdentityPath value)
        {
            return new Bookmark(this){IdentityPath = value ?? IdentityPath.ROOT};
        }
        public ChromFileInfoId ChromFileInfoId { get; private set;}
        public Bookmark ChangeChromFileInfoId(ChromFileInfoId value)
        {
            return new Bookmark(this){ChromFileInfoId = value};
        }
        public int OptStep { get; private set; }
        public Bookmark ChangeOptStep(int value)
        {
            return new Bookmark(this){OptStep = value};
        }

        public static Bookmark ToBookmark(DocumentLocation documentLocation, SrmDocument document)
        {
            Bookmark bookmark = new Bookmark();
            if (documentLocation.IdPath.Any())
            {
                IdentityPath identityPath = IdentityPath.ToIdentityPath(documentLocation.IdPath, document);
                if (null == identityPath)
                {
                    throw new ArgumentException("Unable to find target node " + documentLocation.IdPathToString()); // Not L10N
                }
                bookmark = bookmark.ChangeIdentityPath(identityPath);
            }
            if (documentLocation.ChromFileId.HasValue)
            {
                ChromFileInfoId chromFileInfoId = null;
                if (document.Settings.HasResults)
                {
                    foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
                    {
                        var chromFileInfo =
                            chromatogramSet.MSDataFileInfos.FirstOrDefault(
                                fileInfo => fileInfo.Id.GlobalIndex == documentLocation.ChromFileId);
                        if (null != chromFileInfo)
                        {
                            chromFileInfoId = chromFileInfo.FileId;
                        }
                    }
                }
                if (null == chromFileInfoId)
                {
                    throw new ArgumentException("Unable to find file id " + documentLocation.ChromFileId); // Not L10N
                }
                bookmark = bookmark.ChangeChromFileInfoId(chromFileInfoId);
            }
            if (documentLocation.OptStep.HasValue)
            {
                bookmark = bookmark.ChangeOptStep(documentLocation.OptStep.Value);
            }
            return bookmark;
        }
    }
}
