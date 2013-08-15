/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace pwiz.Topograph.Data
{
    public class IntegrationNote
    {
        private static readonly Dictionary<string, IntegrationNote> IntegrationNotes 
            = new Dictionary<string, IntegrationNote>();
        private static readonly List<IntegrationNote> LstIntegrationNotes = new List<IntegrationNote>();
        private IntegrationNote(string label)
        {
            Label = label;
            Ordinal = LstIntegrationNotes.Count;
            LstIntegrationNotes.Add(this);
            IntegrationNotes.Add(label, this);
        }

        [Browsable(false)]
        public string Label { get; private set;}
        [Browsable(false)]
        public int Ordinal { get; private set; }
        public override string ToString()
        {
            return Label;
        }
        public static IntegrationNote Parse(string integrationNote)
        {
            if (integrationNote == null)
            {
                return null;
            }
            IntegrationNote result;
            if (!IntegrationNotes.TryGetValue(integrationNote, out result))
            {
                return Error;
            }
            return result;
        }
        public static IEnumerable<IntegrationNote> ParseCollection(string integrationNotes)
        {
            var result = new List<IntegrationNote>();
            foreach (var s in integrationNotes.Split('|'))
            {
                IntegrationNote value;
                if (IntegrationNotes.TryGetValue(s, out value))
                {
                    result.Add(value);
                }
            }
            return result;
        }
        public static IList<IntegrationNote> Values()
        {
            return new ReadOnlyCollection<IntegrationNote>(LstIntegrationNotes);
        }
        public static string ToString(IntegrationNote integrationNote)
        {
            if (integrationNote == null)
            {
                return null;
            }
            return integrationNote.ToString();
        }
        public static string ToString(IEnumerable<IntegrationNote> integrationNotes)
        {
            return string.Join("|", integrationNotes.Select(n => n.ToString()).ToArray());
        }

        public static readonly IntegrationNote Manual = new IntegrationNote("Manual");
        public static readonly IntegrationNote Error = new IntegrationNote("Error");
        public static readonly IntegrationNote PeakNotFoundAtMs2Id = new IntegrationNote("PeakNotFoundAtMs2Id");
        public static readonly IntegrationNote Success = new IntegrationNote("Success");
    }
}
