#include "RawFile.h"
#include <iostream>


using namespace std;
using namespace pwiz::raw;


int main(int argc, char* argv[])
{
    try
    {
        if (argc<2)
        {
            cout << "Usage: RawFileTest filename\n";
            return 1;
        }
        
        const char* filename = argv[1]; 

        RawFileLibrary rawFileLibrary;

        RawFilePtr rawfile(filename);
        rawfile->setCurrentController(Controller_MS, 1);

        cout << "name: " << rawfile->value(FileName) << endl;
        cout << "scanCount: " << rawfile->value(NumSpectra) << endl;

        return 0;
    }
    catch (RawEgg& egg)
    {
        cout << "Caught RawEgg: " << egg.error() << endl;
    }
    catch (exception& e)
    {
        cout << "Caught exception: " << e.what() << endl;
    }
    catch (...)
    {
        cout << "Caught unknown exception.\n";
    }

    return 1;
}


