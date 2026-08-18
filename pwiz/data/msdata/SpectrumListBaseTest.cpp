//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#include "SpectrumListBase.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::msdata;
using namespace pwiz::util;


class MyBase : public SpectrumListBase
{
    public:
    virtual size_t size() const {return 0;}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {throw runtime_error("heh");}
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const {return SpectrumPtr();}

    // make sure we still compile -- error if setDataProcessingPtr() renamed to dataProcessingPtr()
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const {return dp_;}
};


void test()
{
    MyBase base;
    DataProcessingPtr dp(new DataProcessing("dp"));
    base.setDataProcessingPtr(dp);
    unit_assert(base.dataProcessingPtr().get() == dp.get());
}


// A list that reads a format some other tool wrote, standing in for SpectrumList_mzML and friends -
// they call ensureMzAscending on the way out of spectrum(), which is what these exercise.
class MyReaderList : public SpectrumListBase
{
    public:
    virtual size_t size() const {return spectra_.size();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {throw runtime_error("not needed");}

    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const
    {
        SpectrumPtr result = spectra_[index];
        ensureMzAscending(result);
        return result;
    }

    void add(const SpectrumPtr& spectrum) {spectrum->index = spectra_.size(); spectra_.push_back(spectrum);}

    private:
    vector<SpectrumPtr> spectra_;
};


SpectrumPtr makeSpectrum(const string& id, const vector<double>& mzs, const vector<double>& intensities,
                         CVID extraArrayType = CVID_Unknown)
{
    SpectrumPtr s(new Spectrum);
    s->id = id;
    s->set(MS_MS1_spectrum);
    s->set(MS_ms_level, 1);
    s->setMZIntensityArrays(mzs, intensities, MS_number_of_detector_counts);
    if (extraArrayType != CVID_Unknown)
    {
        BinaryDataArrayPtr extra(new BinaryDataArray);
        extra->set(extraArrayType);
        vector<double> values(mzs.size(), 1.0);
        extra->data.assign(values.begin(), values.end());
        s->binaryDataArrayPtrs.push_back(extra);
    }
    s->defaultArrayLength = mzs.size();
    return s;
}


vector<double> mzsOf(const SpectrumPtr& s)
{
    const BinaryData<double>& data = s->getMZArray()->data;
    return vector<double>(data.begin(), data.end());
}


vector<double> intensitiesOf(const SpectrumPtr& s)
{
    const BinaryData<double>& data = s->getIntensityArray()->data;
    return vector<double>(data.begin(), data.end());
}


void unsortedPeaks(vector<double>& mzs, vector<double>& intensities)
{
    mzs.clear(); intensities.clear();
    mzs.push_back(500.5); intensities.push_back(10);
    mzs.push_back(300.3); intensities.push_back(20);
    mzs.push_back(700.7); intensities.push_back(30);
    mzs.push_back(200.2); intensities.push_back(40);
}


// The defect this exists for: peaks stored in ascending intensity rather than ascending m/z. Every
// intensity must still come back attached to the m/z it arrived with - sorting the m/z axis alone
// would leave each value plausible and each pairing wrong, a worse failure than the unsorted input.
void testUnsortedSpectrumIsReordered()
{
    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    MyReaderList list;
    list.add(makeSpectrum("scan=1", mzs, intensities));

    SpectrumPtr s = list.spectrum(0, true);
    vector<double> outMzs = mzsOf(s), outIntensities = intensitiesOf(s);
    unit_assert_operator_equal(4, outMzs.size());
    unit_assert_equal(200.2, outMzs[0], 1e-9); unit_assert_equal(40, outIntensities[0], 1e-9);
    unit_assert_equal(300.3, outMzs[1], 1e-9); unit_assert_equal(20, outIntensities[1], 1e-9);
    unit_assert_equal(500.5, outMzs[2], 1e-9); unit_assert_equal(10, outIntensities[2], 1e-9);
    unit_assert_equal(700.7, outMzs[3], 1e-9); unit_assert_equal(30, outIntensities[3], 1e-9);
}


// A file already in m/z order comes through untouched and does not report itself repaired.
void testSortedSpectrumIsUntouched()
{
    vector<double> mzs, intensities;
    for (int i = 0; i < 20; ++i) {mzs.push_back(100.0 + i); intensities.push_back(20 - i);}

    MyReaderList list;
    list.add(makeSpectrum("scan=1", mzs, intensities));

    SpectrumPtr s = list.spectrum(0, true);
    unit_assert(mzsOf(s) == mzs);
    unit_assert(intensitiesOf(s) == intensities);
}


// The case a first-spectrum-only probe gets wrong. A short leading spectrum that happens to ascend
// says nothing about the writer - early scans can precede the sample and carry almost no peaks - so
// the checking has to continue until a spectrum with enough peaks settles it.
void testShortOrderedLeaderDoesNotSettleTheFile()
{
    vector<double> shortMzs, shortIntensities;
    shortMzs.push_back(150.1); shortIntensities.push_back(5);
    shortMzs.push_back(250.2); shortIntensities.push_back(7);
    shortMzs.push_back(350.3); shortIntensities.push_back(9);

    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    MyReaderList list;
    list.add(makeSpectrum("scan=1", shortMzs, shortIntensities));
    list.add(makeSpectrum("scan=2", mzs, intensities));

    unit_assert(mzsOf(list.spectrum(0, true)) == shortMzs);
    unit_assert_equal(200.2, mzsOf(list.spectrum(1, true))[0], 1e-9);
}


// Once a file is condemned it stays condemned, so a later spectrum that happens to ascend cannot
// switch the checking off and let the ones after it through in writer order.
void testCondemnedFileStaysCondemned()
{
    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    vector<double> longSortedMzs, longSortedIntensities;
    for (int i = 0; i < 20; ++i) {longSortedMzs.push_back(100.0 + i); longSortedIntensities.push_back(1);}

    MyReaderList list;
    list.add(makeSpectrum("scan=1", mzs, intensities));
    list.add(makeSpectrum("scan=2", longSortedMzs, longSortedIntensities));
    list.add(makeSpectrum("scan=3", mzs, intensities));

    list.spectrum(0, true);
    list.spectrum(1, true);
    unit_assert_equal(200.2, mzsOf(list.spectrum(2, true))[0], 1e-9);
}


// A combined ion mobility spectrum is legitimately ordered by m/z only within each mobility bin, so
// it must be left exactly as it is - a global sort would shred the bin structure.
void testCombinedIonMobilitySpectrumIsLeftAlone()
{
    vector<double> mzs, intensities;
    mzs.push_back(500.5); intensities.push_back(10);
    mzs.push_back(600.6); intensities.push_back(20);
    mzs.push_back(200.2); intensities.push_back(30);  // roll-over into the next mobility bin
    mzs.push_back(300.3); intensities.push_back(40);

    MyReaderList list;
    list.add(makeSpectrum("merged=1", mzs, intensities, MS_mean_inverse_reduced_ion_mobility_array));

    SpectrumPtr s = list.spectrum(0, true);
    unit_assert(mzsOf(s) == mzs);
    unit_assert(intensitiesOf(s) == intensities);
}


// The rollover guard has to run ahead of the verdict, not just ahead of the sort. An ion mobility
// spectrum with enough peaks to settle the question would otherwise vouch for a writer it says
// nothing about, switching the checking off for the rest of the file.
void testCombinedIonMobilitySpectrumDoesNotVouchForTheWriter()
{
    vector<double> imsMzs, imsIntensities;
    for (int i = 0; i < 20; ++i) {imsMzs.push_back(100.0 + i); imsIntensities.push_back(1);}

    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    MyReaderList list;
    list.add(makeSpectrum("merged=1", imsMzs, imsIntensities, MS_mean_inverse_reduced_ion_mobility_array));
    list.add(makeSpectrum("scan=2", mzs, intensities));

    unit_assert(mzsOf(list.spectrum(0, true)) == imsMzs);
    unit_assert_equal(200.2, mzsOf(list.spectrum(1, true))[0], 1e-9);
}


// Spectrum::getMZArray() returns a wavelength array too, so a diode-array trace would otherwise be
// judged as if its wavelength axis were m/z - and an ascending UV trace with enough points would
// settle the verdict and disable the repair for every real spectrum in the file.
void testWavelengthSpectrumIsNeitherSortedNorEvidence()
{
    vector<double> descending, intensities;
    for (int i = 0; i < 20; ++i) {descending.push_back(400.0 - i); intensities.push_back(1);}

    vector<double> mzs, mzIntensities;
    unsortedPeaks(mzs, mzIntensities);

    MyReaderList list;
    SpectrumPtr uv(new Spectrum);
    uv->id = "scan=1";
    uv->set(MS_EMR_spectrum);
    uv->setMZIntensityArrays(descending, intensities, MS_number_of_detector_counts);
    BinaryDataArrayPtr xArray = uv->getMZArray(); // held, because clearing the params below is what getMZArray matches on
    xArray->cvParams.clear();
    xArray->set(MS_wavelength_array);
    uv->defaultArrayLength = descending.size();
    list.add(uv);
    list.add(makeSpectrum("scan=2", mzs, mzIntensities));

    // the UV trace is left exactly as written, descending and all
    unit_assert(mzsOf(list.spectrum(0, true)) == descending);

    // and it neither condemned nor vouched for the file
    unit_assert_equal(200.2, mzsOf(list.spectrum(1, true))[0], 1e-9);
}


// Extra per-peak arrays are ordinary - signal-to-noise, baseline, resolution, charge - and none is
// an ordering axis. Every one has to travel with the peak it belongs to; an integer array is a
// separate member of Spectrum and is just as per-peak.
void testExtraPerPeakArraysTravelWithTheirPeaks()
{
    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    SpectrumPtr s = makeSpectrum("scan=1", mzs, intensities, MS_signal_to_noise_array);
    vector<double> snrs;
    snrs.push_back(5); snrs.push_back(6); snrs.push_back(7); snrs.push_back(8);
    s->binaryDataArrayPtrs.back()->data.assign(snrs.begin(), snrs.end());

    IntegerDataArrayPtr charge(new IntegerDataArray);
    charge->set(MS_charge_array);
    vector<int> charges;
    charges.push_back(1); charges.push_back(2); charges.push_back(3); charges.push_back(4);
    charge->data.assign(charges.begin(), charges.end());
    s->integerDataArrayPtrs.push_back(charge);

    // shorter than the peak count, so it holds no per-peak value and must be left exactly as it is.
    // Length is the only thing that tells the two kinds of extra array apart.
    BinaryDataArrayPtr notPerPeak(new BinaryDataArray);
    notPerPeak->set(MS_baseline_array);
    vector<double> baseline;
    baseline.push_back(111.1); baseline.push_back(222.2);
    notPerPeak->data.assign(baseline.begin(), baseline.end());
    s->binaryDataArrayPtrs.push_back(notPerPeak);

    MyReaderList list;
    list.add(s);
    SpectrumPtr out = list.spectrum(0, true);

    vector<double> outMzs = mzsOf(out);
    unit_assert_equal(200.2, outMzs[0], 1e-9);
    unit_assert_equal(700.7, outMzs[3], 1e-9);

    const BinaryData<double>& outSnr = out->binaryDataArrayPtrs[2]->data;
    unit_assert_equal(8, outSnr[0], 1e-9);   // 200.2
    unit_assert_equal(6, outSnr[1], 1e-9);   // 300.3
    unit_assert_equal(5, outSnr[2], 1e-9);   // 500.5
    unit_assert_equal(7, outSnr[3], 1e-9);   // 700.7

    const BinaryData<IntegerDataArray::value_type>& outCharge = out->integerDataArrayPtrs.back()->data;
    unit_assert_operator_equal(4, outCharge[0]);
    unit_assert_operator_equal(2, outCharge[1]);
    unit_assert_operator_equal(1, outCharge[2]);
    unit_assert_operator_equal(3, outCharge[3]);

    const BinaryData<double>& outBaseline = out->binaryDataArrayPtrs[3]->data;
    unit_assert_operator_equal(2, outBaseline.size());
    unit_assert_equal(111.1, outBaseline[0], 1e-9);
    unit_assert_equal(222.2, outBaseline[1], 1e-9);
}


// An SRM or SIM spectrum lists one point per transition in the order the method defined them, not
// peaks along an m/z continuum. That order is the meaningful one, so it must be left exactly as
// written even when the transitions were not set up in ascending m/z.
void testSrmSpectrumIsLeftAlone()
{
    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    SpectrumPtr s(new Spectrum);
    s->id = "sample=1 period=1 cycle=1 experiment=1";
    s->set(MS_SRM_spectrum);
    s->setMZIntensityArrays(mzs, intensities, MS_number_of_detector_counts);
    s->defaultArrayLength = mzs.size();

    MyReaderList list;
    list.add(s);

    SpectrumPtr out = list.spectrum(0, true);
    unit_assert(mzsOf(out) == mzs);
    unit_assert(intensitiesOf(out) == intensities);
}


// The rollover guard has to run ahead of the verdict here too. An SRM spectrum with enough
// transitions to settle the question by chance ascending would otherwise vouch for a writer it says
// nothing about, switching the checking off for a real spectrum later in the same file.
void testSrmSpectrumDoesNotVouchForTheWriter()
{
    vector<double> srmMzs, srmIntensities;
    for (int i = 0; i < 20; ++i) {srmMzs.push_back(100.0 + i); srmIntensities.push_back(1);}

    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    SpectrumPtr srm(new Spectrum);
    srm->id = "sample=1 period=1 cycle=1 experiment=1";
    srm->set(MS_SIM_spectrum);
    srm->setMZIntensityArrays(srmMzs, srmIntensities, MS_number_of_detector_counts);
    srm->defaultArrayLength = srmMzs.size();

    MyReaderList list;
    list.add(srm);
    list.add(makeSpectrum("scan=2", mzs, intensities));

    unit_assert(mzsOf(list.spectrum(0, true)) == srmMzs);
    unit_assert_equal(200.2, mzsOf(list.spectrum(1, true))[0], 1e-9);
}


// A metadata-only read carries the array objects with their cvParams and no data, and consumers do
// walk whole files that way. Such a spectrum says nothing about the writer - settling on one would
// switch the checking off for every real spectrum after it.
void testMetadataOnlySpectrumSettlesNothing()
{
    vector<double> mzs, intensities;
    unsortedPeaks(mzs, intensities);

    MyReaderList list;
    list.add(makeSpectrum("scan=1", vector<double>(), vector<double>()));
    list.add(makeSpectrum("scan=2", mzs, intensities));

    unit_assert_operator_equal(0, mzsOf(list.spectrum(0, true)).size());
    unit_assert_equal(200.2, mzsOf(list.spectrum(1, true))[0], 1e-9);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
        testUnsortedSpectrumIsReordered();
        testSortedSpectrumIsUntouched();
        testShortOrderedLeaderDoesNotSettleTheFile();
        testCondemnedFileStaysCondemned();
        testCombinedIonMobilitySpectrumIsLeftAlone();
        testCombinedIonMobilitySpectrumDoesNotVouchForTheWriter();
        testWavelengthSpectrumIsNeitherSortedNorEvidence();
        testSrmSpectrumIsLeftAlone();
        testSrmSpectrumDoesNotVouchForTheWriter();
        testExtraPerPeakArraysTravelWithTheirPeaks();
        testMetadataOnlySpectrumSettlesNothing();
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


