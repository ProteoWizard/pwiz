//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "Reader_FASTA.hpp"
//#include "Diff.hpp"
//#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/iostreams/positioning.hpp"
#include <iostream>
#include <fstream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::proteome;
using boost::shared_ptr;


ostream* os_ = 0;

const char* testFASTA =
"\n"
">ABC123 foo\n"
"ELVIS\n"
"LIVES\n"
"\n"
">BCD234 bar\r\n"
"ALIEN\r\n"
"\r\n"
"ATTACK\r\n"
">CDE345\r\n"
"DEATHRAY\r\n"
">DEF456 \n"
"GETKICKS\n"
"\n";



void testWriteRead(const ProteomeData& pd, const Reader_FASTA::Config& config)
{
    if (os_) *os_ << "testWriteRead() " << config << endl;

    /*Writer_FASTA fastaWriter;

    ostringstream oss;
    fastaWriter.write(oss, pd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    MSData msd2;
    mzxmlSerializer.read(iss, msd2);

    DiffConfig diffConfig;
    diffConfig.ignoreMetadata = true;
    diffConfig.ignoreChromatograms = true;

    Diff<MSData> diff(msd, msd2, diffConfig);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);

    if (os_)
    {
        *os_ << "msd2:\n";
        Serializer_mzML mzmlSerializer;
        mzmlSerializer.write(*os_, msd2);
        *os_ << endl;

        *os_ << "msd2::";
        TextWriter write(*os_);
        write(msd2);
        
        *os_ << endl;
    }*/
}


void testWriteRead()
{
    Reader_FASTA fastaReader;
    ProteomeData pd;
    shared_ptr<istringstream> iss(new istringstream(testFASTA));
    fastaReader.read("test.fasta", iss, pd);

    unit_assert(pd.id == "test.fasta");
    unit_assert(pd.proteinListPtr.get());

    ProteinList& pl = *pd.proteinListPtr;
    unit_assert(pl.size() == 4);

    ProteinPtr p0 = pl.protein(0);
    unit_assert(p0.get());
    unit_assert(p0->id == "ABC123");
    unit_assert(p0->description == "foo");
    unit_assert(p0->sequence() == "ELVISLIVES");

    ProteinPtr p1 = pl.protein(1);
    unit_assert(p1.get());
    unit_assert(p1->id == "BCD234");
    unit_assert(p1->description == "bar");
    unit_assert(p1->sequence() == "ALIENATTACK");

    ProteinPtr p2 = pl.protein(2);
    unit_assert(p2.get());
    unit_assert(p2->id == "CDE345");
    unit_assert(p2->description.empty());
    unit_assert(p2->sequence() == "DEATHRAY");

    ProteinPtr p3 = pl.protein(3);
    unit_assert(p3.get());
    unit_assert(p3->id == "DEF456");
    unit_assert(p3->description.empty());
    unit_assert(p3->sequence() == "GETKICKS");


    Reader_FASTA::Config config;
    testWriteRead(pd, config);

    config.indexed = false;
    testWriteRead(pd, config);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testWriteRead();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

