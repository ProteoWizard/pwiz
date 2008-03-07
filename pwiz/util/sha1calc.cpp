//
// sha1calc.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SHA1Calculator.hpp"
#include "unit.hpp"
#include <iostream>
#include <fstream>
#include <string>


using namespace std;
using namespace pwiz::util;


int main(int argc, char* argv[])
{
    try
    {
        if (argc<2) throw runtime_error("Usage: sha1calc filename"); 
        cout << SHA1Calculator::hashFile(argv[1]) << endl;
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
}

