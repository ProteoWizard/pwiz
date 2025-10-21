/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LibraryAlignment
    {
        public LibraryAlignment(Library library, Alignments alignments)
        {
            Library = library;
            Alignments = alignments;
        }

        public Library Library { get; private set; }
        public Alignments Alignments { get; private set; }

        protected bool Equals(LibraryAlignment other)
        {
            return Equals(Library, other.Library) && Equals(Alignments, other.Alignments);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((LibraryAlignment)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Library != null ? Library.GetHashCode() : 0) * 397) ^ (Alignments != null ? Alignments.GetHashCode() : 0);
            }
        }

        public IEnumerable<double> GetNormalizedRetentionTimes(ICollection<string> spectrumSourceFiles, IList<Target> targets)
        {
            var times = Library.GetRetentionTimesWithSequences(Alignments.LibraryFiles, targets);
            if (times == null)
            {
                yield break;
            }

            for (int iFile = 0; iFile < times.Length; iFile++)
            {
                if (false == spectrumSourceFiles?.Contains(Library.LibraryFiles.FilePaths[iFile]))
                {
                    continue;
                }
                if (times[iFile].Count > 0)
                {
                    var alignment = Alignments.GetAlignmentFunction(Alignments.LibraryFiles[iFile], true);
                    if (alignment != null)
                    {
                        foreach (var time in times[iFile])
                        {
                            var normalizedTime = alignment.GetX(time);
                            // Console.Out.WriteLine("Normalizing time {0} in file {1} to {2}", time, Alignments.LibraryFiles[iFile], normalizedTime);
                            yield return normalizedTime;
                        }
                    }
                }
            }
        }
    }
}
