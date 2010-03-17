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


#include "TraDataFile.hpp"
#include "Diff.hpp"
#include "IO.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/filter/gzip.hpp>
#include <boost/iostreams/device/file_descriptor.hpp>
#include <boost/iostreams/copy.hpp>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::tradata;


ostream* os_ = 0;


string filenameBase_ = "temp.TraDataFileTest";

void hackInMemoryTraData(TraData& td)
{
    // remove metadata ptrs appended on read
    //vector<SourceFilePtr>& sfs = msd.fileDescription.sourceFilePtrs;
    //if (!sfs.empty()) sfs.erase(sfs.end()-1);
    vector<SoftwarePtr>& sws = td.softwarePtrs;
    if (!sws.empty()) sws.erase(sws.end()-1);
}

void test()
{
    TraDataFile::WriteConfig writeConfig;

    if (os_) *os_ << "test()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + ".1";
    string filename2 = filenameBase_ + ".2";

    {
        // create TraData object in memory
        TraData tiny;
        examples::initializeTiny(tiny);

        // write to file #1 (static)
        TraDataFile::write(tiny, filename1, writeConfig);

        // read back into an TraDataFile object
        TraDataFile td1(filename1);
        hackInMemoryTraData(td1);

        // compare
        Diff<TraData, DiffConfig> diff(tiny, td1);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

        // write to file #2 (member)
        td1.write(filename2, writeConfig);

        // read back into another TraDataFile object
        TraDataFile td2(filename2);
        hackInMemoryTraData(td2);

        // compare
        diff(tiny, td2);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);

	    // now give the gzip read a workout
	    bio::filtering_istream tinyGZ(bio::gzip_compressor() | bio::file_descriptor_source(filename1));
        bio::copy(tinyGZ, bio::file_descriptor_sink(filename1+".gz", ios::out|ios::binary));

        TraDataFile td3(filename1);
        hackInMemoryTraData(td3);

        // compare
        diff(tiny, td3);
        if (diff && os_) *os_ << diff << endl;
        unit_assert(!diff);
	}

    // remove temp files
    boost::filesystem::remove(filename1);
    boost::filesystem::remove(filename2);
    boost::filesystem::remove(filename1 + ".gz");
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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
