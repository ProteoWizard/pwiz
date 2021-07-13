//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::util;
using namespace pwiz::msdata;
namespace bfs = boost::filesystem;


void flush(SpectrumListSimple& sl, const string& nativeID, const string& msLevel, 
           const vector<MZIntensityPair>& pairs)
{
    // fill in a new Spectrum and append it to the SpectrumList
    SpectrumPtr spectrum(new Spectrum);
    spectrum->index = sl.size();
    spectrum->id = "scan=" + nativeID;
    spectrum->set(MS_ms_level, msLevel);
    spectrum->setMZIntensityPairs(pairs, MS_number_of_detector_counts);
    sl.spectra.push_back(spectrum);
}


void txt2mzml(const char* filenameIn, const char* filenameOut)
{
    // open input file

    ifstream is(filenameIn);
    if (!is)
        throw runtime_error(("[txt2mzml] Unable to open file " + string(filenameIn)).c_str());

    // allocate MSData object and SpectrumListSimple

    MSData msd;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    msd.run.spectrumListPtr = sl;

    // fill in some metadata

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filenameIn);
    sourceFile->id = "text_data";
    sourceFile->name = BFS_STRING(p.leaf());
    sourceFile->location = string("file://") + BFS_COMPLETE(p.branch_path()).string();
    string sha1 = SHA1Calculator::hashFile(filenameIn);
    sourceFile->cvParams.push_back(CVParam(MS_SHA_1, sha1));
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.id = "run";

    // keep a simple state machine, flushing out cache to
    // the MSData object when the nativeID changes

    // cache

    string nativeID;
    string msLevel;
    vector<MZIntensityPair> pairs;

    while (is)
    {
        // parse stream one line at a time 

        string buffer;
        getlinePortable(is, buffer);

        // if we're done, flush one last time and break

        if (!is)
        {
            flush(*sl, nativeID, msLevel, pairs);
            break;
        }

        // tokenize the line

        istringstream iss(buffer);
        vector<string> tokens;
        copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

        if (tokens.empty() || tokens[0]=="#") continue;

        if (tokens.size() != 4 || 
            tokens[1].size()<3 ||
            tokens[1].substr(0,2)!="ms")
            throw runtime_error((buffer + "\n[txt2mzml] Bad format.").c_str());

        // flush cache when nativeID changes

        if (tokens[0] != nativeID)
        {
            if (!nativeID.empty())
                flush(*sl, nativeID, msLevel, pairs);

            nativeID = tokens[0];
            msLevel = tokens[1].substr(2);
            pairs.clear();
        }

        // append m/z-intensity pair to cached binary data

        pairs.push_back(MZIntensityPair(lexical_cast<double>(tokens[2]),
                                        lexical_cast<double>(tokens[3])));
    }

    // write out mzML

    MSDataFile::write(msd, filenameOut);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc != 3)
        {
            cout << "Usage: txt2mzml fileIn fileOut\n"
                 << "Read text spectrum data from fileIn, and write mzML to fileOut.\n"
                 << "\n"
                 << "Input format:\n"
                 << "# scanNumber       msLevel           m/z     intensity\n"
                 << "           1           ms1      204.7596       1422.17\n"
                 << "           1           ms1      204.7598       3215.49\n"
                 << "         [...]\n"
                 << "           2           ms1     1999.7273         26.49\n"
                 << "           2           ms1     1999.8182          0.00\n"
                 << "\n" 
                 << "https://github.com/ProteoWizard\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        const char* filenameIn = argv[1];
        const char* filenameOut = argv[2];
        txt2mzml(filenameIn, filenameOut);

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

