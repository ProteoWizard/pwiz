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

namespace PeakViewConvert
{
    class Program
    {
        static void Main(string[] args)
        {

            if (1 > args.Length || args.Length > 4)
            {
                Console.Error.WriteLine("Usage: PeakViewConvert <rt file> <score file> <qvalue file> <output file>");
                Console.Error.WriteLine("       Converts PeakView outputs to a form Skyline can read for peak picking comparison");
                return;
            }
            RunConversion(args[0], args[1], args[2], args[3]);
        }

        // ReSharper disable InconsistentNaming
        private const string PROTEIN = "Protein";
        private const string PEPTIDE = "Peptide";
        private const string LABEL = "Label";
        private const string PRECURSOR = "Precursor MZ";
        private const string CHARGE = "Precursor Charge";
        private const string RT = "RT";
        private const string DECOY = "Decoy";

        private static void RunConversion(string rtFile, string scoreFile, string qValueFile, string combinedFile)
        {
            using (var rtReader = new StreamReader(rtFile))
            using (var scoreReader = new StreamReader(scoreFile))
            using (var qValueReader = new StreamReader(qValueFile))
            using (var writer = new StreamWriter(combinedFile))
            {
                var rtPeakData = ImportFile(rtReader);
                var scorePeakData = ImportFile(scoreReader);
                var qValuePeakData = ImportFile(qValueReader);
                int nPeaks = rtPeakData.Count;
                Debug.Assert(Equals(nPeaks, scorePeakData.Count));
                Debug.Assert(Equals(nPeaks, qValuePeakData.Count));
                WriteHeader(writer);
                for (int i = 0; i < nPeaks; ++i)
                {
                    string file = rtPeakData[i].SimplifiedFile;
                    string modifiedSequence = rtPeakData[i].ModifiedSequence;
                    bool decoy = rtPeakData[i].Decoy;
                    Debug.Assert(Equals(decoy, scorePeakData[i].Decoy));
                    Debug.Assert(Equals(decoy, qValuePeakData[i].Decoy));
                    Debug.Assert(Equals(modifiedSequence, scorePeakData[i].ModifiedSequence));
                    Debug.Assert(Equals(modifiedSequence, qValuePeakData[i].ModifiedSequence));
                    double rt = rtPeakData[i].DataValue;
                    double score = scorePeakData[i].DataValue;
                    double qValue = qValuePeakData[i].DataValue;
                    WriteLine(writer, CultureInfo.CurrentCulture, file, modifiedSequence, rt, score, qValue, decoy);
                }
                writer.Close();
            }
        }

        private static void WriteLine(TextWriter writer,
                      CultureInfo cultureInfo,
                      string file,
                      string modifiedSequence,
                      double rt,
                      double score,
                      double qValue,
                      bool decoy)
        {
            const char separator = TextUtil.SEPARATOR_TSV;
            var fieldsArray = new List<string>
                {
                    file,
                    modifiedSequence,
                    Convert.ToString(rt, cultureInfo),
                    Convert.ToString(score, cultureInfo),
                    Convert.ToString(qValue, cultureInfo),
                    Convert.ToString(decoy, cultureInfo)
                };
            bool first = true;
            foreach (var name in fieldsArray)
            {
                if (first)
                    first = false;
                else
                    writer.Write(separator);
                writer.WriteDsvField(name, separator);
            }
            writer.WriteLine();
        }


        private static void WriteHeader(TextWriter writer)
        {
            const char separator = TextUtil.SEPARATOR_TSV;
            // ReSharper disable NonLocalizedString
            var namesArray = new List<string>
                {
                    "FileName",
                    "PeptideModifiedSequence",
                    "Apex",
                    "annotation_Score",
                    "annotation_QValue",
                    "decoy"
                };
            // ReSharper restore NonLocalizedString

            bool first = true;
            foreach (var name in namesArray)
            {
                if (first)
                    first = false;
                else
                    writer.Write(separator);
                writer.WriteDsvField(name, separator);
            }
            writer.WriteLine();
        }

        private static IList<PeakData> ImportFile(TextReader peakViewReader)
        {
            var peakDatas = new List<PeakData>();
            var msFileNames = new List<string>();
            var fileReader = new DsvFileReader(peakViewReader, TextUtil.SEPARATOR_TSV);
            Debug.Assert(Equals(fileReader.GetFieldIndex(PROTEIN), 0));
            Debug.Assert(Equals(fileReader.GetFieldIndex(PEPTIDE), 1));
            Debug.Assert(Equals(fileReader.GetFieldIndex(LABEL), 2));
            Debug.Assert(Equals(fileReader.GetFieldIndex(PRECURSOR), 3));
            Debug.Assert(Equals(fileReader.GetFieldIndex(CHARGE), 4));
            Debug.Assert(Equals(fileReader.GetFieldIndex(RT), 5));
            Debug.Assert(Equals(fileReader.GetFieldIndex(DECOY), 6));
            for (int i = 7; i < fileReader.NumberOfFields; ++i)
            {
                msFileNames.Add(fileReader.FieldNames[i]);
            }
            while (fileReader.ReadLine() != null)
            {
                string modifiedSequence = fileReader.GetFieldByName(PEPTIDE);
                string decoyString = fileReader.GetFieldByName(DECOY);
                bool decoy;
                var hasDecoyValue = bool.TryParse(decoyString, out decoy);
                Debug.Assert(hasDecoyValue);
                foreach (var msFileName in msFileNames)
                {
                    string dataFieldString = fileReader.GetFieldByName(msFileName);
                    double dataField;
                    var hasDataFieldValue = double.TryParse(dataFieldString, out dataField);
                    Debug.Assert(hasDataFieldValue);
                    var peakData = new PeakData(dataField, modifiedSequence, msFileName, decoy);
                    peakDatas.Add(peakData);
                }
            }
            return peakDatas;
        }

        public class PeakData
        {
            public PeakData(double dataValue, string modifiedSequence, string file, bool decoy)
            {
                DataValue = dataValue;
                ModifiedSequence = modifiedSequence;
                File = file;
                Decoy = decoy;
            }

            public double DataValue { get; private set; }
            public string ModifiedSequence { get; private set; }
            public string File { get; private set; }
            public bool Decoy { get; private set; }

            public string SimplifiedFile 
            {
                get
                {
                    const char paren = '(';
                    int firstParen = File.IndexOf(paren);
                    string substring = File.Substring(firstParen + 1);
                    int secondParen = substring.IndexOf(paren);
                    return substring.Substring(0, secondParen - 1);
                }
            }
        }
    }
    
}
