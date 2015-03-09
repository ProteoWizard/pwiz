/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataAnalysis.FoldChange
{
    public class FoldChangeCalculator<TRun, TFeature, TSubject>
    {
        public static FoldChangeDataSet MakeDataSet(IList<DataRow> dataRows)
        {
            var features = GetUniqueList(dataRows.Select(row => row.Feature));
            var featureIndexes = MakeIndexDictionary(features);
            var runs = GetUniqueList(dataRows.Select(row => row.Run));
            var runIndexes = MakeIndexDictionary(runs);
            var subjectEntries = dataRows.Select(row => 
                new KeyValuePair<TSubject, bool>(row.Subject, row.Control)).ToArray();
            var uniqueSubjectEntries = GetUniqueList(subjectEntries);
            var subjectIndexes = MakeIndexDictionary(uniqueSubjectEntries);
            IList<int> subjects = subjectEntries.Select(entry => subjectIndexes[entry]).ToArray();
            var subjectControls = uniqueSubjectEntries.Select(entry => entry.Value).ToArray();

            return new FoldChangeDataSet(
                dataRows.Select(row => row.Abundance).ToArray(),
                dataRows.Select(row=>featureIndexes[row.Feature]).ToArray(),
                dataRows.Select(row=>runIndexes[row.Run]).ToArray(),
                subjects, 
                subjectControls);
        }

        public static List<T> GetUniqueList<T>(IEnumerable<T> list)
        {
            var result = new List<T>();
            var hashSet = new HashSet<T>();
            bool foundNull = false;
            foreach (var item in list)
            {
                if (ReferenceEquals(item, null))
                {
                    if (!foundNull)
                    {
                        foundNull = true;
                        // ReSharper disable ExpressionIsAlwaysNull
                        result.Add(item);
                        // ReSharper restore ExpressionIsAlwaysNull
                    }
                } 
                else if (hashSet.Add(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        private static IDictionary<T, int> MakeIndexDictionary<T>(IList<T> list)
        {
            var result = new Dictionary<T, int>();
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(list[i], i);
            }
            return result;
        }

        public class DataRow
        {
            public double Abundance { get; set; }
            public bool Control { get; set; }
            public TRun Run { get; set; }
            public TFeature Feature { get; set; }
            public TSubject Subject { get; set; }
        }
    }
}
