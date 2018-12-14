/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Serialization
{
    public sealed class CompactFormatOption
    {        
        // ReSharper disable LocalizableElement
        public static readonly CompactFormatOption ALWAYS = new CompactFormatOption("always", 
            ()=>Resources.CompactFormatOption_ALWAYS_Always, 
            doc=>true);
        public static readonly CompactFormatOption NEVER = new CompactFormatOption("never", 
            ()=>Resources.CompactFormatOption_NEVER_Never, 
            doc=>false);
        public static readonly CompactFormatOption ONLY_FOR_LARGE_FILES = new CompactFormatOption("largefilesonly", 
            ()=>Resources.CompactFormatOption_ONLY_FOR_LARGE_FILES_Only_for_large_files, 
            doc=>doc.MoleculeTransitionCount > 1000);
        // ReSharper restore LocalizableElement

        public static readonly IList<CompactFormatOption> ALL_VALUES =
            ImmutableList.ValueOf(new[] {NEVER, ONLY_FOR_LARGE_FILES, ALWAYS});
        public static readonly CompactFormatOption DEFAULT = ONLY_FOR_LARGE_FILES;
        private readonly Func<string> _getDisplayNameFunc;
        private readonly Func<SrmDocument, bool> _useCompactFormatFunc;
        private CompactFormatOption(string name, Func<string> getDisplayName, 
            Func<SrmDocument, bool> useCompactFormatFunc)
        {
            Name = name;
            _getDisplayNameFunc = getDisplayName;
            _useCompactFormatFunc = useCompactFormatFunc;
        }

        public string Name { get; private set; }

        public override string ToString()
        {
            return _getDisplayNameFunc();
        }

        public bool UseCompactFormat(SrmDocument document)
        {
            return _useCompactFormatFunc(document);
        }

        public static CompactFormatOption Parse(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DEFAULT;
            }
            foreach (var value in ALL_VALUES)
            {
                if (string.Equals(value.Name, name))
                {
                    return value;
                }
            }
            return DEFAULT;
        }

        public static CompactFormatOption FromSettings()
        {
            return Parse(Settings.Default.CompactFormatOption);
        }
    }
}
