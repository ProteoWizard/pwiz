/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Represents a starting location and direction for a search within a document.
    /// </summary>
    public class BookmarkStartPosition : IComparer<Bookmark>
    {
        private Position _startPosition;
        private long _totalPositionCount;
        public BookmarkStartPosition(SrmDocument document, Bookmark start, bool forward)
        {
            Document = document;
            Location = start;
            Forward = forward;
            _startPosition = GetPosition(start);
            _totalPositionCount = document.MoleculeGroupCount + document.MoleculeCount +
                                  document.MoleculeTransitionGroupCount + document.MoleculeTransitionCount;
            if (document.Settings.HasResults)
            {
                _totalPositionCount *= document.Settings.MeasuredResults.Chromatograms.Count + 1;
            }
        }

        public BookmarkStartPosition(SrmDocument document) : this(document, Bookmark.ROOT, true)
        {
        }

        public SrmDocument Document { get; }
        public Bookmark Location { get; }
        public bool Forward { get; }
        
        /// <summary>
        /// Compare two bookmarks to determine which one is closest to Start.
        /// </summary>
        public int Compare(Bookmark a, Bookmark b)
        {
            return ComparePositions(GetPosition(a), GetPosition(b));
        }

        private int ComparePositions(Position a, Position b)
        {
            int positionAComparedToStart = Math.Sign(Position.Compare(a, _startPosition));
            int positionBComparedToStart = Math.Sign(Position.Compare(b, _startPosition));
            if (positionAComparedToStart == 0)
            {
                if (positionBComparedToStart == 0)
                {
                    return 0;
                }
                return 1;
            }
            else if (positionBComparedToStart == 0)
            {
                return -1;
            }

            int result = -positionAComparedToStart.CompareTo(positionBComparedToStart);
            if (result == 0)
            {
                result = a.CompareTo(b);
            }

            if (!Forward)
            {
                result *= -1;
            }

            return result;
        }

        /// <summary>
        /// Returns what percentage through the document the specified bookmark is away from <see cref="Location"/>.
        /// </summary>
        public int GetPercentComplete(Bookmark bookmark)
        {
            var position = GetPosition(bookmark);
            int comparedToStart = ComparePositions(_startPosition, position);
            if (comparedToStart == 0)
            {
                return 100;
            }

            long current = AsLong(position);
            long start = AsLong(_startPosition);
            long difference = current - start;
            if (!Forward)
            {
                difference *= -1;
            }

            if (difference < 0)
            {
                difference += _totalPositionCount;
            }

            return (int) (100 * difference / _totalPositionCount);
        }

        private class Position : IComparable<Position>
        {
            public static Position ForBookmark(SrmDocument document, Bookmark bookmark)
            {
                if (document.FindNode(bookmark.IdentityPath) == null)
                {
                    return null;
                }

                int nodeIndex = document.GetNodePositions(bookmark.IdentityPath).Sum();
                int? fileIndex = null;
                if (bookmark.ReplicateIndex.HasValue)
                {
                    if (!document.Settings.HasResults || bookmark.ReplicateIndex.Value < 0 || bookmark.ReplicateIndex >=
                        document.Settings.MeasuredResults.Chromatograms.Count)
                    {
                        return null;
                    }

                    if (bookmark.ChromFileInfoId != null)
                    {
                        fileIndex = document.Settings.MeasuredResults
                            .Chromatograms[bookmark.ReplicateIndex.Value].IndexOfId(bookmark.ChromFileInfoId);
                        if (fileIndex < 0)
                        {
                            return null;
                        }
                    }
                }

                return new Position
                {
                    NodeIndex = nodeIndex,
                    Depth = bookmark.IdentityPath.Depth,
                    ReplicateIndex = bookmark.ReplicateIndex,
                    FileIndex = fileIndex,
                    OptStep = bookmark.OptStep
                };

            }
            private Position()
            {
            }
            public int NodeIndex { get; private set; }
            public int Depth { get; private set; }
            public int? ReplicateIndex { get; private set; }
            public int? FileIndex { get; private set; }
            public int OptStep { get; private set; }
            public int CompareTo(Position other)
            {
                return Compare(this, other);
            }
            public static int Compare(Position a, Position b)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }
                if (a == null)
                {
                    return -1;
                }
                if (b == null)
                {
                    return 1;
                }

                int result = a.NodeIndex.CompareTo(b.NodeIndex);
                if (result == 0)
                {
                    result = a.Depth.CompareTo(b.Depth);
                }
                if (result == 0)
                {
                    result = Nullable.Compare(a.ReplicateIndex, b.ReplicateIndex);
                }
                if (result == 0)
                {
                    result = Nullable.Compare(a.FileIndex, b.FileIndex);
                }
                if (result == 0)
                {
                    result = a.OptStep.CompareTo(b.OptStep);
                }
                return result;
            }
        }

        private Position GetPosition(Bookmark bookmark)
        {
            return Position.ForBookmark(Document, bookmark);
        }

        private long AsLong(Position position)
        {
            long value = position.NodeIndex;
            if (Document.Settings.HasResults)
            {
                value *= Document.Settings.MeasuredResults.Chromatograms.Count + 1;
                if (position.ReplicateIndex.HasValue)
                {
                    value += position.ReplicateIndex.Value + 1;
                }
            }
            return value;
        }

        /// <summary>
        /// Returns approximately how frequently a progress bar should be updated when using
        /// a <see cref="BookmarkEnumerator"/>.
        /// </summary>
        public long GetProgressUpdateFrequency()
        {
            return Math.Max(1, _totalPositionCount / 1000);
        }
    }
}
