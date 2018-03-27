//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;
namespace bfs = boost::filesystem;

ostream* os_ = 0;

struct TestSpectrumInfo
{
    size_t	index;
    int		scanNumber;
    double	precursor_mz;
    size_t	num_peaks;
    double	first_peak_mz;
    double	intensity;
    string  charge_state;
    string  possible_charges;   // Space delimited list
};

const TestSpectrumInfo testSpectrum[] = 
{
    {4, 122, 576.82, 139, 176.381, 0.9, "", "2 3"},
    {6, 125, 785.72, 76,  333.224, 0.8, "", "2 3"}, 
    {2, 120, 508.95, 82,  261.342, 0.7, "", "2 3"}
};

void checkSpectrumInfo(SpectrumPtr s, size_t idx, int msLevel)
{
    // Get the current spectrum info
    SpectrumInfo* spec_info = new SpectrumInfo();
    unit_assert(spec_info);	
    spec_info->SpectrumInfo::update(*s, true);

    // Validate spectrum info matches what we expect
    unit_assert(spec_info->index == testSpectrum[idx].index);
    unit_assert(spec_info->scanNumber == testSpectrum[idx].scanNumber);
    unit_assert(spec_info->msLevel == msLevel);
    if (msLevel > 1) {
        unit_assert_equal(spec_info->precursors[0].mz, testSpectrum[idx].precursor_mz, 5e-2);
    }
    unit_assert(spec_info->data.size() == testSpectrum[idx].num_peaks);
    unit_assert_equal(spec_info->data.at(0).mz, testSpectrum[idx].first_peak_mz, 5e-1);
    unit_assert_equal(spec_info->data.at(0).intensity, testSpectrum[idx].intensity, 5e-1);

    // Print spectrum info
    if (os_)
    {
        *os_ << "spectrum index: " << spec_info->index << "\t"
             << "scan number: " << spec_info->scanNumber << "\t"
             << "level: " << spec_info->msLevel << "\t";
        if (msLevel > 1) {
            *os_ << "precursor mz: " << spec_info->precursors[0].mz << "\t";
        }
        *os_ << "num peaks: " << spec_info->data.size() << "\t"
             << "first peak mz: " << spec_info->data.at(0).mz << "\t"
             << "intenisity: " << spec_info->data.at(0).intensity << "\t"
             << "possible charges: ";
    }

    if (msLevel > 1)
    {
        Precursor& precur = s->precursors[0];
        SelectedIon& si = precur.selectedIons[0];

        // Since we are expecting "possible charge states", there shouldn't be a
        // MS_charge_state!
        unit_assert(si.cvParam(MS_charge_state).value.empty());

        // Check the possible charge states (expecting 2, values = 2,3)
        size_t numChargeStates = 0;
        BOOST_FOREACH(CVParam& param, si.cvParams)
        {
            if (param.cvid == MS_possible_charge_state)
            {
                // Assume charge is single digit
                unit_assert(string::npos != testSpectrum[idx].possible_charges.find(param.value));
                numChargeStates++;

                if (os_)
                {
                    *os_ << param.value << " ";
                }
            }
        }
        unit_assert(numChargeStates == 2);
    }

    if (os_)
    {
        *os_ << "\n";
    }
}

void test(const bfs::path& datadir, int msLevel)
{
    if (os_) *os_ << "test()\n";

    vector<string> filenames;
    if (msLevel == 1) {
        filenames.push_back("10-spec.ms1");
        filenames.push_back("10-spec.bms1");
        filenames.push_back("10-spec.cms1");
    } else if (msLevel == 2) {
        filenames.push_back("10-spec.ms2");
        filenames.push_back("10-spec.bms2");
        filenames.push_back("10-spec.bms2.gz");
        filenames.push_back("10-spec.cms2");
    } else {
        cerr << "Invalid MS level." << endl;
        return;
    }

    // look up these spectrum indexes
    size_t indexes[] = {4, 6, 2};
    size_t num_spec = sizeof(indexes)/sizeof(size_t);

    // open each file and look up the same spec
    vector<string>::const_iterator file_it = filenames.begin();
    for (; file_it != filenames.end(); ++file_it)
    {
        string filename = *file_it;
        MSDataFile data_file((datadir / filename).string());
        SpectrumListPtr all_spectra = data_file.run.spectrumListPtr;

        // initialize a SpectrumInfo object with a dummy spec
        unit_assert(data_file.run.spectrumListPtr.get());

        if (os_)
        {
            *os_ << "Found " << data_file.run.spectrumListPtr->size() << " spectra" 
             << " in " << filename << endl;
        }
        
        // for each index, get info and print
        for (size_t i=0; i<num_spec; i++)
        {
            SpectrumPtr cur_spec = all_spectra->spectrum(indexes[i], true);
            checkSpectrumInfo(cur_spec, i, msLevel);
        }

        // do the same thing for a list of scan numbers
        if (os_)
        {
            *os_ << "Use scan numbers to get the same spec." << endl;
        }
        
        size_t scan_nums[] = {122, 125, 120};
        num_spec = sizeof(scan_nums)/sizeof(size_t);
        for (size_t i=0; i<num_spec; i++)
        {
            string id_str = "scan=" + boost::lexical_cast<string>(scan_nums[i]);
            if (os_)
            {
                *os_ << "Looking for the " << i << "th scan num, " << scan_nums[i] 
                     << ", id " << id_str << endl;
            }
            
            size_t found_index = all_spectra->find(id_str);
            if (os_)
            {
                *os_ << "found_index = " << found_index << endl;
            }
            
            if (found_index == all_spectra->size())
            {
                if (os_)
                {	
                    *os_ << "Not found." << endl;
                }
            }
            else
            {
                SpectrumPtr cur_spec = all_spectra->spectrum(found_index, true);
                checkSpectrumInfo(cur_spec, i, msLevel);
            }
        }
    }
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;

        std::string srcparent(__FILE__);
        size_t pos = srcparent.find((bfs::path("pwiz") / "data").string());
        srcparent.resize(pos);

        bfs::path example_data_dir = srcparent + "example_data/";
        test(example_data_dir, 1);
        test(example_data_dir, 2);

    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}


