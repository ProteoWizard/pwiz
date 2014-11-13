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
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfPeakViewConvert : AbstractUnitTest
    {
        // [TestMethod]  // disabling this since the test file URLs don't exist
        public void ConvertPeakViewPerf()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set

            const string testFilesDir = @"D:\Processing\HeldManual\PeakView";
            string rtFile = Path.Combine(testFilesDir, "PeakViewRt.txt");
            string scoreFile = Path.Combine(testFilesDir, "PeakViewScore.txt");
            string qValueFile = Path.Combine(testFilesDir, "PeakViewFDR.txt");
            string combinedFile = Path.Combine(testFilesDir, "PeakView.txt");
            RunConversion(rtFile, scoreFile, qValueFile, combinedFile);
        }

        public const string PROTEIN = "Protein";
        public const string PEPTIDE = "Peptide";
        public const string LABEL = "Label";
        public const string PRECURSOR = "Precursor MZ";
        public const string CHARGE = "Precursor Charge";
        public const string RT = "RT";
        public const string DECOY = "Decoy";

        public void RunConversion(string rtFile, string scoreFile, string qValueFile, string combinedFile)
        {
            using (var rtReader = new StreamReader(rtFile))
            using (var scoreReader = new StreamReader(scoreFile))
            using (var qValueReader = new StreamReader(qValueFile))
            using (var fs = new FileSaver(combinedFile))
            using (var writer = new StreamWriter(fs.SafeName))
            {
                var rtPeakData = ImportFile(rtReader);
                var scorePeakData = ImportFile(scoreReader);
                var qValuePeakData = ImportFile(qValueReader);
                int nPeaks = rtPeakData.Count;
                Assert.AreEqual(nPeaks, scorePeakData.Count);
                Assert.AreEqual(nPeaks, qValuePeakData.Count);
                WriteHeader(writer);
                for (int i = 0; i < nPeaks; ++i)
                {
                    string file = rtPeakData[i].SimplifiedFile;
                    string modifiedSequence = rtPeakData[i].ModifiedSequence;
                    bool decoy = rtPeakData[i].Decoy;
                    Assert.AreEqual(decoy, scorePeakData[i].Decoy);
                    Assert.AreEqual(decoy, qValuePeakData[i].Decoy);
                    Assert.AreEqual(modifiedSequence, scorePeakData[i].ModifiedSequence);
                    Assert.AreEqual(modifiedSequence, qValuePeakData[i].ModifiedSequence);
                    double rt = rtPeakData[i].DataValue;
                    double score = scorePeakData[i].DataValue;
                    double qValue = qValuePeakData[i].DataValue;
                    WriteLine(writer, CultureInfo.CurrentCulture, file, modifiedSequence, rt, score, qValue, decoy);
                }
                writer.Close();
                fs.Commit();
            }
        }

        public void WriteLine(TextWriter writer,
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


        public void WriteHeader(TextWriter writer)
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

        public IList<PeakData> ImportFile(TextReader peakViewReader)
        {
            var peakDatas = new List<PeakData>();
            var msFileNames = new List<string>();
            var fileReader = new DsvFileReader(peakViewReader, TextUtil.SEPARATOR_TSV);
            Assert.AreEqual(fileReader.GetFieldIndex(PROTEIN), 0);
            Assert.AreEqual(fileReader.GetFieldIndex(PEPTIDE), 1);
            Assert.AreEqual(fileReader.GetFieldIndex(LABEL), 2);
            Assert.AreEqual(fileReader.GetFieldIndex(PRECURSOR), 3);
            Assert.AreEqual(fileReader.GetFieldIndex(CHARGE), 4);
            Assert.AreEqual(fileReader.GetFieldIndex(RT), 5);
            Assert.AreEqual(fileReader.GetFieldIndex(DECOY), 6);
            for (int i = 7; i < fileReader.NumberOfFields; ++i)
            {
                msFileNames.Add(fileReader.FieldNames[i]);
            }
            while (fileReader.ReadLine() != null)
            {
                string modifiedSequence = fileReader.GetFieldByName(PEPTIDE);
                string decoyString = fileReader.GetFieldByName(DECOY);
                bool decoy;
                Assert.IsTrue(bool.TryParse(decoyString, out decoy));
                foreach (var msFileName in msFileNames)
                {
                    string dataFieldString = fileReader.GetFieldByName(msFileName);
                    double dataField;
                    Assert.IsTrue(double.TryParse(dataFieldString, out dataField));
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
