//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "Serializer_FASTA.hpp"
#include "Diff.hpp"
#include "examples.hpp"
#include "pwiz/data/common/BinaryIndexStream.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::data;


ostream* os_ = 0;


void testWriteRead(const Serializer_FASTA::Config& config)
{
    ProteomeData pd;
    examples::initializeTiny(pd);

    Serializer_FASTA serializer(config);

    ostringstream oss;
    serializer.write(oss, pd, NULL);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    ProteomeData pd2;
    serializer.read(iss, pd2);

    Diff<ProteomeData, DiffConfig> diff(pd, pd2);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}


void testWriteRead()
{
    if (os_) *os_ << "testWriteRead() MemoryIndex" << endl;
    Serializer_FASTA::Config config;
    testWriteRead(config);

    if (os_) *os_ << "testWriteRead() BinaryIndexStream" << endl;
    shared_ptr<stringstream> indexStringStream(new stringstream);
    config.indexPtr.reset(new BinaryIndexStream(indexStringStream));
    testWriteRead(config);
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
