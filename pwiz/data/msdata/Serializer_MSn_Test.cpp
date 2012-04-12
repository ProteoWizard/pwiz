//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

// TODO: once write is implemented for each MSn_Type_MS2, test write and read

#include "Serializer_MSn.hpp"
#include "Serializer_mzML.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


void initializeTinyMSn(MSData& msd)
{
    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);
    fc.set(MS_centroid_spectrum);

    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->set(MS_scan_number_only_nativeID_format);
    sourceFile->set(MS_MS2_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s20 = *spectrumList->spectra[0];
    s20.id = "scan=1";
    s20.index = 0;

    s20.set(MS_MSn_spectrum);
    s20.set(MS_ms_level, 2);

    s20.set(MS_centroid_spectrum);
    s20.set(MS_lowest_observed_m_z, 0);
    s20.set(MS_highest_observed_m_z, 18);
    s20.set(MS_base_peak_m_z, 0);
    s20.set(MS_base_peak_intensity, 20);
    s20.set(MS_total_ion_current, 110);

    s20.precursors.resize(1);
    Precursor& precursor = s20.precursors.front();
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
    precursor.selectedIons[0].set(MS_possible_charge_state, 2);
    precursor.isolationWindow.set(MS_isolation_window_target_m_z, 445.34, MS_m_z);

    s20.scanList.scans.push_back(Scan());
    Scan& s20scan = s20.scanList.scans.back();
    s20scan.set(MS_scan_start_time, 4, UO_second);

    s20.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
    vector<double>& s20_mz = s20.getMZArray()->data;
    vector<double>& s20_intensity = s20.getIntensityArray()->data;

    for (int i=0; i<10; i++)
        s20_mz.push_back(i*2);

    for (int i=0; i<10; i++)
        s20_intensity.push_back((10-i)*2);

    s20.defaultArrayLength = s20_mz.size();

} // initializeTinyMSn()


void testWriteRead(const MSData& msd)
{
    Serializer_MSn serializer(MSn_Type_MS2);

    ostringstream oss;
    serializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    MSData msd2;
    shared_ptr<istream> iss(new istringstream(oss.str()));
    serializer.read(iss, msd2);

    DiffConfig config;
    config.ignoreMetadata = true;
    Diff<MSData, DiffConfig> diff(msd, msd2, config);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);

}

void testWriteReadBMS2(const MSData& msd)
{
    Serializer_MSn serializer(MSn_Type_BMS2);

    ostringstream oss;
    serializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    MSData msd2;
    shared_ptr<istream> iss(new istringstream(oss.str()));
    serializer.read(iss, msd2);

    DiffConfig config;
    config.ignoreMetadata = true;
    config.ignoreDataProcessing = true;
    Diff<MSData, DiffConfig> diff(msd, msd2, config);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}

void testWriteReadCMS2(const MSData& msd)
{
    Serializer_MSn serializer(MSn_Type_CMS2);

    ostringstream oss;
    serializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    MSData msd2;
    shared_ptr<istream> iss(new istringstream(oss.str()));
    serializer.read(iss, msd2);

    DiffConfig config;
    config.ignoreMetadata = true;
    config.ignoreDataProcessing = true;
    Diff<MSData, DiffConfig> diff(msd, msd2, config);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}

void testWriteRead()
{
    MSData msd;
    initializeTinyMSn(msd);

    testWriteRead(msd);
    testWriteReadBMS2(msd);
    testWriteReadCMS2(msd);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testWriteRead();
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

