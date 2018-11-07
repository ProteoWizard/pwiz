//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

    //SourceFilePtr sourceFile(new SourceFile);
    //sourceFile->set(MS_multiple_peak_list_nativeID_format);
    // TODO: sourceFile->set(MS_Matrix_Science_MGF_format);
    //msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s20 = *spectrumList->spectra[0];
    s20.id = "index=0";
    s20.index = 0;
    s20.set(MS_spectrum_title, s20.id);

    s20.set(MS_MSn_spectrum);
    s20.set(MS_ms_level, 2);

    s20.set(MS_centroid_spectrum);
    s20.set(MS_positive_scan);
    s20.set(MS_lowest_observed_m_z, 0.0);
    s20.set(MS_highest_observed_m_z, 18.0);
    s20.set(MS_base_peak_m_z, 0.0);
    s20.set(MS_base_peak_intensity, 20.0);
    s20.set(MS_total_ion_current, 110.0);

    s20.precursors.resize(1);
    Precursor& s20precursor = s20.precursors.front();
    s20precursor.selectedIons.resize(1);
    s20precursor.selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
    s20precursor.selectedIons[0].set(MS_peak_intensity, 120053.0, MS_number_of_detector_counts);
    s20precursor.selectedIons[0].set(MS_charge_state, 2);

    s20.scanList.set(MS_no_combination);
    s20.scanList.scans.push_back(Scan());
    Scan& s20scan = s20.scanList.scans.back();
    s20scan.set(MS_scan_start_time, 4.0, UO_second);

    s20.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
    BinaryData<double>& s20_mz = s20.getMZArray()->data;
    BinaryData<double>& s20_intensity = s20.getIntensityArray()->data;

    for (int i=0; i<10; i++)
        s20_mz.push_back(i*2);

    for (int i=0; i<10; i++)
        s20_intensity.push_back((10-i)*2);

    s20.defaultArrayLength = s20_mz.size();


    Spectrum& s21 = *spectrumList->spectra[1];
    s21.id = "index=1";
    s21.index = 1;
    s21.set(MS_spectrum_title, s21.id);

    s21.set(MS_MSn_spectrum);
    s21.set(MS_ms_level, 2);

    s21.set(MS_centroid_spectrum);
    s21.set(MS_negative_scan);
    s21.set(MS_lowest_observed_m_z, 3.0);
    s21.set(MS_highest_observed_m_z, 30.0);
    s21.set(MS_base_peak_m_z, 3.0);
    s21.set(MS_base_peak_intensity, 30.0);
    s21.set(MS_total_ion_current, 165.0);

    s21.precursors.resize(1);
    Precursor& s21precursor = s21.precursors.front();
    s21precursor.selectedIons.resize(1);
    s21precursor.selectedIons[0].set(MS_selected_ion_m_z, 424.24, MS_m_z);
    s21precursor.selectedIons[0].set(MS_peak_intensity, 4242.0, MS_number_of_detector_counts);
    s21precursor.selectedIons[0].cvParams.push_back(CVParam(MS_possible_charge_state, 2));
    s21precursor.selectedIons[0].cvParams.push_back(CVParam(MS_possible_charge_state, 3));

    s21.scanList.set(MS_no_combination);
    s21.scanList.scans.push_back(Scan());
    Scan& s21scan = s21.scanList.scans.back();
    s21scan.set(MS_scan_start_time, 42.0, UO_second);

    s21.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
    BinaryData<double>& s21_mz = s21.getMZArray()->data;
    BinaryData<double>& s21_intensity = s21.getIntensityArray()->data;

    for (int i=1; i<=10; i++)
        s21_mz.push_back(i*3);

    for (int i=0; i<10; i++)
        s21_intensity.push_back((10-i)*3);

    s21.defaultArrayLength = s21_mz.size();

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
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testWriteRead();
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

