//
// txt2mzml.cpp 
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include <iostream>
#include <fstream>
#include <iomanip>
#include <stdexcept>
#include <iterator>


using namespace std;
using namespace pwiz;
using namespace pwiz::msdata;
using boost::shared_ptr;
using boost::lexical_cast;


/*

This example program reads m/z-intensity pairs from a text column format into an
MSData structure, writing the resulting mzML out to console.

# scanNumber     msLevel         m/z   intensity
           1         ms1    204.7591        0.00
           1         ms1    204.7593        0.00
           1         ms1    204.7596     1422.17
           1         ms1    204.7598     3215.49
           1         ms1    204.7601     3887.36
           1         ms1    204.7604     2843.17
           1         ms1    204.7606      582.91
           1         ms1    204.7609        0.00
           1         ms1    204.7611        0.00

[...]

*/


void flush(SpectrumListSimple& sl, const string& nativeID, const string& msLevel, 
           const vector<MZIntensityPair>& pairs)
{
    cout << "flush: " << nativeID << " " << msLevel << " " << pairs.size() << endl;
}



void txt2mzml(const char* filename)
{
    // open input file

    ifstream is(filename);
    if (!is)
        throw runtime_error("[txt2mzml] Unable to open file.");

    // allocate MSData object and SpectrumListSimple

    MSData msd;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    msd.run.spectrumListPtr = sl;

    // keep a simple state machine with cache, flushing out to msd when the nativeID changes

    string nativeID;
    string msLevel;
    vector<MZIntensityPair> pairs;

    while (is)
    {
        string buffer;
        getline(is, buffer);

        if (!is)
        {
            flush(*sl, nativeID, msLevel, pairs);
            break;
        }

        if (buffer.empty() || buffer[0]=='#') continue;

        istringstream iss(buffer);
        vector<string> tokens;
        copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

        if (tokens.size() != 4 || 
            tokens[1].size()<3 ||
            tokens[1].substr(0,2)!="ms")
            throw runtime_error((buffer + "\n[txt2mzml] Bad format.").c_str());

        if (tokens[0] != nativeID)
        {
            if (!nativeID.empty())
                flush(*sl, nativeID, msLevel, pairs);

            nativeID = tokens[0];
            msLevel = tokens[1].substr(2);
            pairs.clear();
        }

        pairs.push_back(MZIntensityPair(lexical_cast<double>(tokens[2]),
                                        lexical_cast<double>(tokens[3])));
    }

    // write out mzML
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 2)
        {
            cout << "Usage: txt2mzml filename\n"
                 << "Read text spectrum data from file, and write mzML to console.\n\n"
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        const char* filename = argv[1];
        txt2mzml(filename);

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

