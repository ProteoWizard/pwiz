//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "IPIFASTADatabase.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::proteome;


ostream* os_ = 0;


void writeTestEntries(ostream& os)
{
    os << ">IPI:IPI00000001.2|SWISS-PROT:O95793-1|..." << endl;
    os << "MSQVQVQVQNPSAALSGSQILNKNQSLLSQPLMSIPSTTSSLPSENAGRPIQNSALPSAS" << endl;
    os << "ITSTSAAAESITPTVELNALCMKLGKKPMYKPVDPYSRMQSTYNYNMRGGAYPPRYFYPF" << endl;
    os << "PVPPLLYQVELSVGGQQFNGKGKTRQAAKHDAAAKALRILQNEPLPERLEVNGRESEEEN" << endl;
    os << "LNKSEISQVFEIALKRNLPVNFEVARESGPPHMKNFVTKVSVGEFVGEGEGKSKKISKKN" << endl;
    os << "AAIAVLEELKKLPPLPAVERVKPRIKKKTKPIVKPQTSPEYGQGINPISRLAQIQQAKKE" << endl;
    os << "KEPEYTLLTERGLPRRREFVMQVKVGNHTAEGTGTNKKVAKRNAAENMLEILGFKVPQAQ" << endl;
    os << "PTKPALKSEEKTPIKKPGDGRKVTFFEPGSGDENGTSNKEDEFRMPYLSHQQLPAGILPM" << endl;
    os << "VPEVAQAVGVSQGHHTKDFTRAAPNPAKATVTAMIARELLYGGTSPTAETILKNNISSGH" << endl;
    os << "VPHGPLTRPSEQLDYLSRVQGFQVEYKDFPKNNKNEFVSLINCSSQPPLISHGIGKDVES" << endl;
    os << "CHDMAALNILKLLSELDQQSTEMPRTGNGPMSVCGRC" << endl;
    os << ">IPI:IPI00000005.1|SWISS-PROT:P01111|..." << endl;
    os << "MTEYKLVVVGAGGVGKSALTIQLIQNHFVDEYDPTIEDSYRKQVVIDGETCLLDILDTAG" << endl;
    os << "QEEYSAMRDQYMRTGEGFLCVFAINNSKSFADINLYREQIKRVKDSDDVPMVLVGNKCDL" << endl;
    os << "PTRTVDTKQAHELAKSYGIPFIETSAKTRQGVEDAFYTLVREIRQYRMKKLNSSDDGTQG" << endl;
    os << "CMGLPCVVM" << endl;
}


void test()
{
    string filename = "IPIFASTADatabaseTest.test.txt";
    ofstream os(filename.c_str());
    writeTestEntries(os);
    os.close();

    IPIFASTADatabase db(filename);
    unit_assert(db.records().size() == 2);

    IPIFASTADatabase::const_iterator it = db.records().begin();
    unit_assert(it->id == 1);
    unit_assert(it->faID == "IPI:IPI00000001.2");
    unit_assert(it->sequence.size() == 577);
    unit_assert(it->sequence.find("PVPPLL") == 120);
    if (os_) *os_ << *it << endl;

    ++it;
    unit_assert(it->id == 5);
    unit_assert(it->faID == "IPI:IPI00000005.1");
    unit_assert(it->sequence.size() == 189);
    unit_assert(it->sequence.find("PTRTVD") == 120);
    if (os_) *os_ << *it << endl;

    boost::filesystem::remove(filename);
}


void testRealDatabase()
{
    IPIFASTADatabase db("ipi.HUMAN.fasta");
    cout << "record count: " << db.records().size() << endl;
   
    if (!db.records().empty())
    {
        const IPIFASTADatabase::Record* record = &db.records().back();
        cout << *record << endl;
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "IPIFASTADatabaseTest\n";
        test();
        //testRealDatabase();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

