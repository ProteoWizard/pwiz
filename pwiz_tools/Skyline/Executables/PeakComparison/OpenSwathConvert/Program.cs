/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace OpenSwathConvert
{
    class Program
    {
        static void Main(string[] args)
        {

            if ( args.Length < 2)
            {
                Console.Error.WriteLine("Usage: OpenSwathConvert <OpenSwath file>[...] <output file>");
                Console.Error.WriteLine("       Converts one or more OpenSwath outputs to a form Skyline can read for peak picking comparison");
                return;
            }
            RunConversion(args.Take(args.Length-2), args[args.Length-1]);
        }

        // ReSharper disable InconsistentNaming
        private const string TRANSITION_GROUP = "transition_group_id";
        private const string FILE_NAME = "filename";
        private const string RUN_ID = "run_id";
        private const string MS_FILE_TYPE = ".wiff";
        private const char SEPARATOR = TextUtil.SEPARATOR_TSV;

        private static void RunConversion(IEnumerable<string> individualInput, string combinedOutput)
        {
            using (var writer = new StreamWriter(combinedOutput))
            {
                bool first = true;
                var fields = new List<string>();
                int currentFileCount = 0;
                foreach (var inputFile in individualInput)
                {
                    using (var reader = new StreamReader(inputFile))
                    {
                        fields = TranscribeAndModifyFile(writer, reader, fields, first, currentFileCount);
                    }
                    first = false;
                    ++currentFileCount;
                }
                writer.Close();
            }
        }

        private static List<string> TranscribeAndModifyFile(StreamWriter writer, TextReader reader, List<string> fields, bool first, int currentFileCount)
        {
            var fileReader = new DsvFileReader(reader, SEPARATOR);
            if (first)
            {
                fields = fileReader.FieldNames;
                for (int i = 0; i < fields.Count; ++i)
                {
                    if (i > 0)
                        writer.Write(SEPARATOR);
                    writer.WriteDsvField(fileReader.FieldNames[i], SEPARATOR);
                }
                writer.WriteLine();
            }

            Debug.Assert(Equals(fileReader.NumberOfFields, fields.Count));
            for (int i = 0; i < fields.Count; ++i)
            {
                Debug.Assert(Equals(fileReader.FieldNames[i], fields[i]));
            }

            while (fileReader.ReadLine() != null)
            {
                for (int i = 0; i < fields.Count; ++i)
                {
                    string modifiedField = fileReader.GetFieldByIndex(i);
                    switch (fileReader.FieldNames[i])
                    {
                        case FILE_NAME:
                            modifiedField = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(modifiedField)) + MS_FILE_TYPE;
                            break;
                        case TRANSITION_GROUP:
                            modifiedField = modifiedField + currentFileCount;
                            break;
                        case RUN_ID:
                            modifiedField = currentFileCount.ToString(CultureInfo.CurrentCulture);
                            break;
                    }
                    if (i > 0)
                        writer.Write(SEPARATOR);
                    writer.WriteDsvField(modifiedField, SEPARATOR);
                }
                writer.WriteLine();
            }
            return fields;
        }
    }
    
}
