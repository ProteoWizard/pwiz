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
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DigestionTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTooManyProteinMatches()
        {
            int proteinCount = 30000;
            Directory.CreateDirectory(TestContext.TestDir);
            var proteomeDbPath = Path.Combine(TestContext.TestDir, "manySimilarProteins.protdb");
            var proteomeDb = ProteomeDb.CreateProteomeDb(proteomeDbPath);
            StringWriter repetitiveFastaFile = new StringWriter();
            WriteRepetitiveFastaFile(repetitiveFastaFile, "ELVISLIVES", proteinCount);
            IProgressStatus progressStatus = new ProgressStatus();
            proteomeDb.AddFastaFile(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(repetitiveFastaFile.ToString()))), new SilentProgressMonitor(), ref progressStatus, false);
            var digestion = proteomeDb.GetDigestion();
            var proteins = digestion.GetProteinsWithSequence("VISLIV");
            Assert.AreEqual(proteinCount, proteins.Count);
        }

        private static void WriteRepetitiveFastaFile(TextWriter writer, String baseSequence, int proteinCount)
        {
            for (int i = 0; i < proteinCount; i++)
            {
                writer.WriteLine(">Protein" + i);
                writer.WriteLine(baseSequence + NumberToAminoAcidSequence(i));
            }
        }

        private static String NumberToAminoAcidSequence(int value)
        {
            String aminoAcidLetters = "ARNDCEQGHILKMFPSTWYV";
            StringBuilder result = new StringBuilder();
            while (value > 0)
            {
                result.Append(aminoAcidLetters[value % aminoAcidLetters.Length]);
                value /= aminoAcidLetters.Length;
            }
            return result.ToString();
        }
    }
}
