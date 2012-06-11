//
// hello_pwiz.cpp
//

#include "pwiz/data/msdata/MSDataFile.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::msdata;


void doSomething(MSData& msd)
{
    // manipulate your MSData object here

    SpectrumListPtr sl = msd.run.spectrumListPtr;

    if (sl.get())
    {
        cout << "# of spectra: " << sl->size() << endl;
    }
}


void hello(const string& filename)
{
    cout << "Hello, pwiz!\n";

    // create the MSData object in memory
    MSDataFile msd(filename);

    doSomething(msd);

    string filenameOut = filename + ".out";
    cout << "Writing file " << filenameOut << endl;
    MSDataFile::write(msd, filenameOut /*,MSDataFile::Format_mzXML*/); // uncomment if you want mzXML
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 2)
            throw runtime_error("Usage: hello_pwiz filename"); 
        
        hello(argv[1]);

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

