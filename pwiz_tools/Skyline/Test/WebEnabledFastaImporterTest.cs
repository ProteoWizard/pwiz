/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.ProteomeDatabase.Properties;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class WebEnabledFastaImporterTest : AbstractUnitTest
    {
        /// <summary>
        /// Ensures that the characters rejected by FastaSequence.ValidateSequence are the same
        /// as the ones rejected by WebEnabledFastaImporter.IsValidProteinSequenceChar
        /// </summary>
        [TestMethod]
        public void TestIsValidProteinSequenceChar()
        {
            // We only check the first 256 characters because the whole character range takes too long
            // to throw that many exceptions.
            for (int chValue = 1; chValue <= 256; chValue++)
            {
                char ch = (char)chValue;
                var sequence = new string(ch, 1);
                bool exceptionExpected;
                try
                {
                    FastaSequence.ValidateSequence(sequence);
                    exceptionExpected = false;
                }
                catch
                {
                    exceptionExpected = true;
                }
                Assert.AreEqual(!exceptionExpected, WebEnabledFastaImporter.IsValidProteinSequenceChar(ch));
            }
        }

        [TestMethod]
        public void TestImportInvalidFasta()
        {
            var importer = new WebEnabledFastaImporter();
            List<DbProtein> proteins = new List<DbProtein>();
            try
            {
                proteins.AddRange(importer.Import(new StringReader(STR_INVALID_FASTA)));
                Assert.Fail("Expected InvalidDataException to be thrown.");
            }
            catch (InvalidDataException invalidDataException)
            {
                string expectedMessage = string.Format(
                    Resources.WebEnabledFastaImporter_ValidateProteinSequence_A_protein_sequence_cannot_contain_the_character___0___at_line__1_,
                    '>', 16);
                Assert.AreEqual(expectedMessage, invalidDataException.Message);
            }
            Assert.AreEqual(1, proteins.Count);
        }

        [TestMethod]
        public void TestImportValidFasta()
        {
            var importer = new WebEnabledFastaImporter();
            List<DbProtein> proteins = new List<DbProtein>();
            proteins.AddRange(importer.Import(new StringReader(STR_VALID_FASTA)));
            Assert.AreEqual(3, proteins.Count);
            Assert.IsTrue(proteins.TrueForAll(p => !string.IsNullOrEmpty(p.Names.ToArray()[0].Gene))); // Test with and without OX= syntax
        }

        private const string STR_INVALID_FASTA =
            @">sp|P00724|INV2_YEAST Invertase 2 OS=Saccharomyces cerevisiae (strain ATCC 204508 / S288c) GN=SUC2 PE=1 SV=1
MLLQAFLFLLAGFAAKISASMTNETSDRPLVHFTPNKGWMNDPNGLWYDEKDAKWHLYFQ
YNPNDTVWGTPLFWGHATSDDLTNWEDQPIAIAPKRNDSGAFSGSMVVDYNNTSGFFNDT
IDPRQRCVAIWTYNTPESEEQYISYSLDGGYTFTEYQKNPVLAANSTQFRDPKVFWYEPS
QKWIMTAAKSQDYKIEIYSSDDLKSWKLESAFANEGFLGYQYECPGLIEVPTEQDPSKSY
WVMFISINPGAPAGGSFNQYFVGSFNGTHFEAFDNQSRVVDFGKDYYALQTFFNTDPTYG
SALGIAWASNWEYSAFVPTNPWRSSMSLVRKFSLNTEYQANPETELINLKAEPILNISNA
GPWSRFATNTTLTKANSYNVDLSNSTGTLEFELVYAVNTTQTISKSVFADLSLWFKGLED
PEEYLRMGFEVSASSFFLDRGNSKVKFVKENPYFTNRMSVNNQPFKSENDLSYYKVYGLL
DQNILELYFNDGDVVSTNTYFMTTGNALGSVNMTTGVDNLFYIDKFQVREVK
>sp|P10144|GRAB_HUMAN Granzyme B OS=Homo sapiens GN=GZMB PE=1 SV=2
MQPILLLLAFLLLPRADAGEIIGGHEAKPHSRPYMAYLMIWDQKSLKRCGGFLIRDDFVL
TAAHCWGSSINVTLGAHNIKEQEPTQQFIPVKRPIPHPAYNPKNFSNDIMLLQLERKAKR
TRAVQPLRLPSNKAQVKPGQTCSVAGWGQTAPLGKHSHTLQEVKMTVQEDRKCESDLRHY
YDSTIELCVGDPEIKKTSFKGDSGGPLVCNKVAQGIVSYGRNNGMPPRACTKVSSFVHWI
KKTMKRY>sp|P31946|1433B_HUMAN 14-3-3 protein beta/alpha OS=Homo sapiens GN=YWHAB PE=1 SV=3
MTMDKSELVQKAKLAEQAERYDDMAAAMKAVTEQGHELSNEERNLLSVAYKNVVGARRSS
WRVISSIEQKTERNEKKQQMGKEYREKIEAELQDICNDVLELLDKYLIPNATQPESKVFY
LKMKGDYFRYLSEVASGDNKQTTVSNSQQAYQEAFEISKKEMQPTHPIRLGLALNFSVFY
YEILNSPEKACSLAKTAFDEAIAELDTLNEESYKDSTLIMQLLRDNLTLWTSENQGDEGD
AGEGEN";
        private const string STR_VALID_FASTA =
            @">sp|P00724|INV2_YEAST Invertase 2 OS=Saccharomyces cerevisiae (strain ATCC 204508 / S288c) GN=SUC2 PE=1 SV=1
MLLQAFLFLLAGFAAKISASMTNETSDRPLVHFTPNKGWMNDPNGLWYDEKDAKWHLYFQ
YNPNDTVWGTPLFWGHATSDDLTNWEDQPIAIAPKRNDSGAFSGSMVVDYNNTSGFFNDT
IDPRQRCVAIWTYNTPESEEQYISYSLDGGYTFTEYQKNPVLAANSTQFRDPKVFWYEPS
QKWIMTAAKSQDYKIEIYSSDDLKSWKLESAFANEGFLGYQYECPGLIEVPTEQDPSKSY
WVMFISINPGAPAGGSFNQYFVGSFNGTHFEAFDNQSRVVDFGKDYYALQTFFNTDPTYG
SALGIAWASNWEYSAFVPTNPWRSSMSLVRKFSLNTEYQANPETELINLKAEPILNISNA
GPWSRFATNTTLTKANSYNVDLSNSTGTLEFELVYAVNTTQTISKSVFADLSLWFKGLED
PEEYLRMGFEVSASSFFLDRGNSKVKFVKENPYFTNRMSVNNQPFKSENDLSYYKVYGLL
DQNILELYFNDGDVVSTNTYFMTTGNALGSVNMTTGVDNLFYIDKFQVREVK
>sp|P10144|GRAB_HUMAN Granzyme B OS=Homo sapiens OX=9606 GN=GZMB PE=1 SV=2
MQPILLLLAFLLLPRADAGEIIGGHEAKPHSRPYMAYLMIWDQKSLKRCGGFLIRDDFVL
TAAHCWGSSINVTLGAHNIKEQEPTQQFIPVKRPIPHPAYNPKNFSNDIMLLQLERKAKR
TRAVQPLRLPSNKAQVKPGQTCSVAGWGQTAPLGKHSHTLQEVKMTVQEDRKCESDLRHY
YDSTIELCVGDPEIKKTSFKGDSGGPLVCNKVAQGIVSYGRNNGMPPRACTKVSSFVHWI
KKTMKRY
>sp|P31946|1433B_HUMAN 14-3-3 protein beta/alpha OS=Homo sapiens GN=YWHAB PE=1 SV=3
MTMDKSELVQKAKLAEQAERYDDMAAAMKAVTEQGHELSNEERNLLSVAYKNVVGARRSS
WRVISSIEQKTERNEKKQQMGKEYREKIEAELQDICNDVLELLDKYLIPNATQPESKVFY
LKMKGDYFRYLSEVASGDNKQTTVSNSQQAYQEAFEISKKEMQPTHPIRLGLALNFSVFY
YEILNSPEKACSLAKTAFDEAIAELDTLNEESYKDSTLIMQLLRDNLTLWTSENQGDEGD
AGEGEN";
    }
}
