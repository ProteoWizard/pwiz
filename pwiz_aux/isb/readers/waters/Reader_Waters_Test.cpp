#include "Reader_Waters.hpp"
#include "data/msdata/TextWriter.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testAccept(const string& rawpath)
{
    if (os_) *os_ << "testAccept(): " << rawpath << endl;

    Reader_Waters reader;
    bool accepted = reader.accept(rawpath, "");
    if (os_) *os_ << "accepted: " << boolalpha << accepted << endl;

    #ifdef _MSC_VER
    unit_assert(accepted);
    #else // _MSC_VER
    unit_assert(!accepted);
    #endif // _MSC_VER
}


void testRead(const string& rawpath)
{
    if (os_) *os_ << "testRead(): " << rawpath << endl;

    // read RAW file into MSData object

    Reader_Waters reader;
    MSData msd;
    reader.read(rawpath, "dummy", msd);

    // make assertions about msd

    //if (os_) TextWriter(*os_,0)(msd); 

    unit_assert(msd.run.spectrumListPtr.get());
    SpectrumList& sl = *msd.run.spectrumListPtr;
    if (os_) *os_ << "spectrum list size: " << sl.size() << endl;
    //unit_assert(sl.size() == 48);

    SpectrumPtr spectrum = sl.spectrum(0, true);
    if (os_) TextWriter(*os_,0)(*spectrum); 
    unit_assert(spectrum->index == 0);
    unit_assert(spectrum->id == "S1,1");
    unit_assert(spectrum->nativeID == "1,1");
    unit_assert(sl.spectrumIdentity(0).index == 0);
    unit_assert(sl.spectrumIdentity(0).id == "S1,1");
    unit_assert(sl.spectrumIdentity(0).nativeID == "1,1");
    //unit_assert(spectrum->cvParam(MS_ms_level).valueAs<int>() == 1); 

    vector<MZIntensityPair> data;
    spectrum->getMZIntensityPairs(data);
    //unit_assert(data.size() == 19914);

    spectrum = sl.spectrum(1, true);
    if (os_) TextWriter(*os_,0)(*spectrum); 
    unit_assert(spectrum->index == 1);
    unit_assert(spectrum->id == "S1,2"); // derived from scan number
    unit_assert(spectrum->nativeID == "1,2"); // scan number
    unit_assert(sl.spectrumIdentity(1).index == 1);
    unit_assert(sl.spectrumIdentity(1).id == "S1,2");
    unit_assert(sl.spectrumIdentity(1).nativeID == "1,2");
    //unit_assert(spectrum->cvParam(MS_ms_level).valueAs<int>() == 1); 

    spectrum->getMZIntensityPairs(data);
    //unit_assert(data.size() == 19800);

    spectrum = sl.spectrum(2, true);
    if (os_) TextWriter(*os_,0)(*spectrum); 
    unit_assert(spectrum->index == 2);
    unit_assert(spectrum->id == "S1,3"); // scan number
    unit_assert(sl.spectrumIdentity(2).index == 2);
    unit_assert(sl.spectrumIdentity(2).id == "S1,3");
    unit_assert(sl.spectrumIdentity(2).nativeID == "1,3");
    //unit_assert(spectrum->cvParam(MS_ms_level).valueAs<int>() == 2); 
 
    spectrum->getMZIntensityPairs(data);
    //unit_assert(data.size() == 485);
 
    spectrum = sl.spectrum(5, true);
    unit_assert(sl.spectrumIdentity(5).index == 5);
    unit_assert(sl.spectrumIdentity(5).id == "S1,6");
    unit_assert(sl.spectrumIdentity(5).nativeID == "1,6");
    //unit_assert(spectrum->spectrumDescription.precursors.size() == 1);

    // test file-level metadata 
    unit_assert(msd.fileDescription.fileContent.hasCVParam(MS_MSn_spectrum));
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
    return 0; // TODO: acquire some Waters test data

    try
    {
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpaths.push_back(argv[i]);
        }

        if (rawpaths.size()!=1)
            throw runtime_error("Usage: Reader_Waters_Test [-v] rawpath"); 
            
        test(rawpaths[0]);
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

