/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestA.MSstats
{
    public static class MsStatsTestUtil
    {
        public static TextReader GetTextReaderForManifestResource(Type type, string filename)
        {
            var stream = type.Assembly.GetManifestResourceStream(type, filename);
            Assert.IsNotNull(stream);
            return new StreamReader(stream);
        }

        public static IList<Dictionary<string, string>> ReadCsvFile(DsvFileReader fileReader)
        {
            var result = new List<Dictionary<string, string>>();
            while (null != fileReader.ReadLine())
            {
                Dictionary<string, string> row = new Dictionary<string, string>();
                for (int i = 0; i < fileReader.NumberOfFields; i++)
                {
                    var value = fileReader.GetFieldByIndex(i);
                    if (null != value)
                    {
                        row.Add(fileReader.FieldNames[i], value);
                    }
                }
                result.Add(row);
            }
            return result;
        }

        public static IDictionary<string, LinearFitResult> ReadExpectedResults(Type type, string resourceName)
        {
            var result = new Dictionary<string, LinearFitResult>();
            using (var reader = GetTextReaderForManifestResource(type, resourceName))
            {
                var csvReader = new DsvFileReader(reader, ',');
                while (null != csvReader.ReadLine())
                {
                    string protein = csvReader.GetFieldByName("Protein");
                    var linearFitResult = new LinearFitResult(Convert.ToDouble(csvReader.GetFieldByName("log2FC"), CultureInfo.InvariantCulture))
                        .SetStandardError(Convert.ToDouble(csvReader.GetFieldByName("SE"), CultureInfo.InvariantCulture))
                        .SetTValue(Convert.ToDouble(csvReader.GetFieldByName("Tvalue"), CultureInfo.InvariantCulture))
                        .SetDegreesOfFreedom(Convert.ToInt32(csvReader.GetFieldByName("DF"), CultureInfo.InvariantCulture))
                        .SetPValue(Convert.ToDouble(csvReader.GetFieldByName("pvalue"), CultureInfo.InvariantCulture));
                    result.Add(protein, linearFitResult);
                }
            }
            return result;
        }
    }
}
