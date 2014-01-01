/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for FastaImporterTest
    /// </summary>
    [TestClass]
    public class FastaImporterTest : AbstractUnitTest
    {
        [TestMethod]
        public void ProteinListTest()
        {
            // Too few columns
            const char sep = TextUtil.SEPARATOR_TSV;
            AssertEx.ThrowsException<LineColNumberedIoException>(() =>
                FastaImporter.ToFasta("PROT1\nPROT2\nPROT2", sep));
            // Invalid sequence
            string invalidSeq = PROTEINLIST_CLIPBOARD_TEXT.Replace("MDAL", "***");
            AssertEx.ThrowsException<LineColNumberedIoException>(() =>
                FastaImporter.ToFasta(invalidSeq, sep));

            string fastaText = FastaImporter.ToFasta(PROTEINLIST_CLIPBOARD_TEXT, sep);
            var reader = new StringReader(fastaText);
            int countProt = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(">"))
                    countProt++;
                else
                    Assert.IsTrue(FastaSequence.IsExSequence(line.Trim('*')));
            }
            Assert.AreEqual(3, countProt);
        }

        private const string PROTEINLIST_CLIPBOARD_TEXT =
            @"IPI:IPI00187591.3|SWISS-PROT:Q4V8C5-1|ENSEMBL:ENSRNOP00000023455	MDALEEESFALSFSSASDAEFDAVVGCLEDIIMDAEFQLLQRSFMDKYYQEFEDTEENKLTYTPIFNEYISLVEKYIEEQLLERIPGFNMAAFTTTLQHHKDEVAGDIFDMLLTFTDFLAFKEMFLDYRAEKEGRGLDLSSGLVVTSLCKSSSTPASQNNLRH
IPI:IPI00187593.1|SWISS-PROT:P23977|ENSEMBL:ENSRNOP00000024015;ENSRNOP00000047272|REFSEQ:NP_036826	MSKSKCSVGPMSSVVAPAKESNAVGPREVELILVKEQNGVQLTNSTLINPPQTPVEAQERETWSKKIDFLLSVIGFAVDLANVWRFPYLCYKNGGGAFLVPYLLFMVIAGMPLFYMELALGQFNREGAAGVWKICPVLKGVGFTVILISFYVGFFYNVIIAWALHYFFSSFTMDLPWIHCNNTWNSPNCSDAHASNSSDGLGLNDTFGTTPAAEYFERGVLHLHQSRGIDDLGPPRWQLTACLVLVIVLLYFSLWKGVKTSGKVVWITATMPYVVLTALLLRGVTLPGAMDGIRAYLSVDFYRLCEASVWIDAATQVCFSLGVGFGVLIAFSSYNKFTNNCYRDAIITTSINSLTSFSSGFVVFSFLGYMAQKHNVPIRDVATDGPGLIFIIYPEAIATLPLSSAWAAVFFLMLLTLGIDSAMGGMESVITGLVDEFQLLHRHRELFTLGIVLATFLLSLFCVTNGGIYVFTLLDHFAAGTSILFGVLIEAIGVAWFYGVQQFSDDIKQMTGQRPNLYWRLCWKLVSPCFLLYVVVVSIVTFRPPHYGAYIFPDWANALGWIIATSSMAMVPIYATYKFCSLPGSFREKLAYAITPEKDHQLVDRGEVRQFTLRHWLLL
IPI:IPI00187596.1|SWISS-PROT:P23978|ENSEMBL:ENSRNOP00000009705|REFSEQ:NP_077347	MATDNSKVADGQISTEVSEAPVASDKPKTLVVKVQKKAGDLPDRDTWKGRFDFLMSCVGYAIGLGNVWRFPYLCGKNGGGAFLIPYFLTLIFAGVPLFLLECSLGQYTSIGGLGVWKLAPMFKGVGLAAAVLSFWLNIYYIVIISWAIYYLYNSFTTTLPWKQCDNPWNTDRCFSNYSLVNTTNMTSAVVEFWERNMHQMTDGLDKPGQIRWPLAITLAIAWVLVYFCIWKGVGWTGKVVYFSATYPYIMLIILFFRGVTLPGAKEGILFYITPNFRKLSDSEVWLDAATQIFFSYGLGLGSLIALGSYNSFHNNVYRDSIIVCCINSCTSMFAGFVIFSIVGFMAHVTKRSIADVAASGPGLAFLAYPEAVTQLPISPLWAILFFSMLLMLGIDSQFCTVEGFITALVDEYPRLLRNRRELFIAAVCIVSYLIGLSNITQGGIYVFKLFDYYSASGMSLLFLVFFECVSISWFYGVNRFYDNIQEMVGSRPCIWWKLCWSFFTPIIVAGVFLFSAVQMTPLTMGSYVFPKWGQGVGWLMALSSMVLIPGYMAYMFLTLKGSLKQRLQVMIQPSEDIVRPENGPEQPQAGSSASKEAYI";

    }
}