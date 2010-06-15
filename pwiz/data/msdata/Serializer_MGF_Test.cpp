//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#include "Serializer_MGF.hpp"
#include "Serializer_mzML.hpp"
#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::msdata;


ostream* os_ = 0;


void initializeTinyMGF(MSData& msd)
{
    FileContent& fc = msd.fileDescription.fileContent;
    fc.set(MS_MSn_spectrum);
    fc.set(MS_centroid_spectrum);

    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->set(MS_multiple_peak_list_nativeID_format);
    // TODO: sourceFile->set(MS_Matrix_Science_MGF_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s20 = *spectrumList->spectra[0];
    s20.id = "index=0";
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
    precursor.selectedIons[0].set(MS_peak_intensity, 120053, MS_number_of_counts);
    precursor.selectedIons[0].set(MS_charge_state, 2);

    s20.scanList.set(MS_no_combination);
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

} // initializeTinyMGF()


void testWriteRead(const MSData& msd)
{
    Serializer_MGF serializer;

    ostringstream oss;
    serializer.write(oss, msd);

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    MSData msd2;
    serializer.read(iss, msd2);

    DiffConfig diffConfig;
    diffConfig.ignoreIdentity = true;
    diffConfig.ignoreMetadata = true;
    diffConfig.ignoreChromatograms = true;

    Diff<MSData, DiffConfig> diff(msd, msd2, diffConfig);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);

    if (os_)
    {
        *os_ << "msd2:\n";
        Serializer_mzML mzmlSerializer;
        mzmlSerializer.write(*os_, msd2);
        *os_ << endl;

        *os_ << "msd2::";
        TextWriter write(*os_);
        write(msd2);
        
        *os_ << endl;
    }
}


void testWriteRead()
{
    MSData msd;
    initializeTinyMGF(msd);

    testWriteRead(msd);
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

