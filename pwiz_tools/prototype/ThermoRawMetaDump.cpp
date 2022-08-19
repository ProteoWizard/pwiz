//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2013 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz_aux/msrc/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"


using namespace pwiz::util;
namespace thermo = pwiz::vendor_api::Thermo;


int main(int argc, const char* argv[])
{
    if (argc < 2)
    {
        cerr << "ThermoRawMetaDump extracts methods, tunes, and headers from RAW files.\n";
        cerr << "Usage: ThermoRawMetaDump --scanTrailers <RAW filemask> <another RAW filemask>" << endl;
        return 1;
    }

    try
    {
        vector<string> filemasks(argv+1, argv+argc);

        bool scanTrailers = false;
        vector<bfs::path> filenames;
        BOOST_FOREACH(const string& filemask, filemasks)
        {
            if (filemask == "--scanTrailers")
            {
                scanTrailers = true;
                continue;
            }

            expand_pathmask(bfs::path(filemask), filenames);
            if (!filenames.size())
                throw runtime_error("no files found matching filemask \"" + filemask + "\"");
        }

        BOOST_FOREACH(const bfs::path& filename, filenames)
        {
            try
            {
                thermo::RawFilePtr rawfilePtr = thermo::RawFile::create(filename.string());
                thermo::RawFile* rawfile = rawfilePtr->getRawByThread(0);

                cout << " ==== Instrument methods for " << filename.filename() << " ====" << endl;
                vector<string> instrumentMethods = rawfile->getInstrumentMethods();
                for (int i=0; i < instrumentMethods.size(); ++i)
                    cout << instrumentMethods[i] << endl << endl;
                cout << " ==== " << endl << endl;
                
                rawfile->setCurrentController(thermo::Controller_MS, 1);

                // loop until there are no more segments
                /*for(int segment=0;; ++segment)
                {
                    try
                    {
                        vector<string> tuneData = rawfile->getTuneData(segment);
                        cout << " ==== Tune data for " << filename.filename() << " segment " << (segment+1) << " ====" << endl;
                        for (int i=0; i < tuneData->size(); ++i)
                            cout << tuneData->label(i) << " " << tuneData->value(i) << "\n";
                        cout << " ==== " << endl << endl;
                    } catch(thermo::RawEgg&)
                    {
                        break;
                    }
                }*/

                cout << " ==== Sample/file/header information for " << filename.filename() << " ====" << endl;
                /*for (int i=0; i < (int) thermo::ValueID_Double_Count; ++i)
                    if (rawfile->value((thermo::ValueID_Double) i) > 0)
                        cout << rawfile->name((thermo::ValueID_Double) i) << ": " << lexical_cast<string>(rawfile->value((thermo::ValueID_Double) i)) << "\n";
                for (int i=0; i < (int) thermo::ValueID_Long_Count; ++i)
                    if (rawfile->value((thermo::ValueID_Long) i) > 0)
                        cout << rawfile->name((thermo::ValueID_Long) i) << ": " << lexical_cast<string>(rawfile->value((thermo::ValueID_Long) i)) << "\n";
                for (int i=0; i < (int) thermo::ValueID_String_Count; ++i)
                    if (!rawfile->value((thermo::ValueID_String) i).empty())
                        cout << rawfile->name((thermo::ValueID_String) i) << ": " << rawfile->value((thermo::ValueID_String) i) << "\n";*/
                auto instData = rawfile->getInstrumentData();
                cout << "AxisLabelX:" << instData.AxisLabelX << endl;
                cout << "AxisLabelY:" << instData.AxisLabelY << endl;
                cout << "Flags:" << instData.Flags << endl;
                cout << "HardwareVersion:" << instData.HardwareVersion << endl;
                cout << "Model:" << instData.Model << endl;
                cout << "Name:" << instData.Name << endl;
                cout << "SerialNumber:" << instData.SerialNumber << endl;
                cout << "SoftwareVersion:" << instData.SoftwareVersion << endl;
                cout << "Units:" << instData.Units << endl;
                cout << "CreationDate: " << rawfile->getCreationDate().to_string() << endl;
                cout << " ==== " << endl << endl;

                if (!scanTrailers)
                    continue;


                cout << " ==== Scan trailers for " << filename.filename() << " ====" << endl;
                long numSpectra = rawfile->getLastScanNumber();
                for (long i = 1; i <= numSpectra; ++i)
                {
                    thermo::ScanInfoPtr scanInfo = rawfile->getScanInfo(i);
                    cout << i << " {" << scanInfo->scanSegmentNumber() << ", " << scanInfo->scanSegmentNumber() << "} FILTER " << scanInfo->filter() << "\n";
                    for (long j = 0; j < scanInfo->trailerExtraSize(); ++j)
                        cout << i << " {" << scanInfo->scanSegmentNumber() << ", " << scanInfo->scanSegmentNumber() << "} " << scanInfo->trailerExtraLabel(j) << scanInfo->trailerExtraValue(j) << "\n";
                }
            }
            catch (exception& e)
            {
                cerr << "Error: " << e.what() << endl;
            }
            catch (...)
            {
                cerr << "Unknown exception." << endl;
            }
        }
        return 0;
    }
    catch (exception& e)
    {
        cerr << "Error: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception." << endl;
    }

    return 1;
}
