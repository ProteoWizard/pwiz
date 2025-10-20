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
using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// A map from spectrum source files in a library to alignment functions
    /// </summary>
    public class Alignments
    {
        public static readonly Alignments EMPTY =
            new Alignments(LibraryFiles.EMPTY, Array.Empty<KeyValuePair<string, PiecewiseLinearMap>>());
        private Dictionary<string, PiecewiseLinearMap> _alignmentFunctions;

        public Alignments(LibraryFiles libraryFiles, IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> alignmentFunctions)
        {
            _alignmentFunctions = new Dictionary<string, PiecewiseLinearMap>();
            List<string> files = null;
            if (libraryFiles == null)
            {
                files = new List<string>();
            }

            foreach (var entry in alignmentFunctions)
            {
                if (entry.Value != null)
                {
                    _alignmentFunctions.Add(entry.Key, entry.Value);
                    files?.Add(entry.Key);
                }
            }

            LibraryFiles = libraryFiles ?? new LibraryFiles(files);
        }

        public AlignmentFunction GetAlignmentFunction(string name, bool forward)
        {
            _alignmentFunctions.TryGetValue(name, out var result);
            return result?.ToAlignmentFunction(forward);
        }

        public AlignmentFunction GetAlignmentFunction(MsDataFileUri msDataFileUri, bool forward)
        {
            int index = LibraryFiles.FindIndexOf(msDataFileUri);
            if (index < 0)
            {
                return null;
            }

            return _alignmentFunctions[LibraryFiles[index]]?.ToAlignmentFunction(forward);
        }

        public bool ContainsFile(string file)
        {
            return _alignmentFunctions.ContainsKey(file);
        }

        public IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> GetAllAlignmentFunctions()
        {
            foreach (var file in LibraryFiles)
            {
                if (_alignmentFunctions.TryGetValue(file, out var piecewiseLinearMap))
                {
                    yield return new KeyValuePair<string, PiecewiseLinearMap>(file, piecewiseLinearMap);
                }
            }
        }
        
        public LibraryFiles LibraryFiles { get; private set; }

        protected bool Equals(Alignments other)
        {
            return CollectionUtil.EqualsDeep(_alignmentFunctions, other._alignmentFunctions) && LibraryFiles.Equals(other.LibraryFiles);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Alignments)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CollectionUtil.GetHashCodeDeep(_alignmentFunctions) * 397) ^ LibraryFiles.GetHashCode();
            }
        }
    }
}
