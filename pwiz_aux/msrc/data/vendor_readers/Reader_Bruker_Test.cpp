//
// Reader_Bruker_Test.cpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "Reader_Bruker.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testAccept(const string& rawpath)
{
    if (os_) *os_ << "testAccept(): " << rawpath << endl;

    Reader_Bruker reader;
    bool accepted = reader.accept(rawpath, "");
    if (os_) *os_ << "accepted: " << boolalpha << accepted << endl;

    unit_assert(accepted);
}


void testRead(const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    // read file into MSData object
    Reader_Bruker reader;
    MSData msd;
    reader.read(rawpath, "dummy", msd);
    if (os_) TextWriter(*os_,0)(msd);

    // test file-level metadata
    unit_assert(!msd.run.spectrumListPtr->empty());
    CVParam nativeIdFormat = msd.fileDescription.fileContent.cvParamChild(MS_nativeID_format);

    // test that file type was identified correctly
    bfs::path sourcePath(rawpath);
    if (bfs::exists(sourcePath / "Analysis.baf"))
        unit_assert(nativeIdFormat.cvid == MS_Bruker_BAF_nativeID_format);
    else if (bfs::exists(sourcePath / "Analysis.yep"))
        unit_assert(nativeIdFormat.cvid == MS_Bruker_Agilent_YEP_nativeID_format);
    else
    {
        string sourceDirectory = *(--sourcePath.end());
        if (bfs::exists(sourcePath / (sourceDirectory.substr(0, sourceDirectory.length()-2) + ".u2")))
        {
            unit_assert(nativeIdFormat.cvid == MS_scan_number_only_nativeID_format);
            unit_assert(msd.fileDescription.fileContent.hasCVParam(MS_EMR_spectrum));
        }
        else
            unit_assert(nativeIdFormat.cvid == MS_Bruker_FID_nativeID_format);
    }

    // make assertions about msd depending on file type
    switch (nativeIdFormat.cvid)
    {
        default:
            throw runtime_error("invalid or missing Bruker NativeID format in fileContent");

        case MS_Bruker_BAF_nativeID_format:
        case MS_Bruker_Agilent_YEP_nativeID_format:
            for (size_t i=0; i < msd.run.spectrumListPtr->size(); ++i)
                unit_assert(msd.run.spectrumListPtr->spectrum(i)->id == "scan=" + lexical_cast<string>(i+1));
            break;

        case MS_Bruker_FID_nativeID_format:
            for (size_t i=0; i < msd.run.spectrumListPtr->size(); ++i)
                unit_assert(msd.run.spectrumListPtr->spectrum(i)->id == "file=" + msd.fileDescription.sourceFilePtrs[i]->id);
            break;

        case MS_scan_number_only_nativeID_format:
            //for (size_t i=0; i < msd.run.spectrumListPtr->size(); ++i)
            //    unit_assert(msd.run.spectrumListPtr->spectrum(i)->id == "scan=" + lexical_cast<string>(i+1000000));
            break;
    }
}


void test(const string& rawpath)
{
    testAccept(rawpath);
    
    #ifdef _MSC_VER
    testRead(rawpath);
    #else
    if (os_) *os_ << "Not MSVC -- nothing to do.\n";
    #endif // _MSC_VER
}


int main(int argc, char* argv[])
{
    try
    {
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpaths.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: Reader_Bruker_Test [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            test(rawpaths[i]);
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
