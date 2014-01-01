//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#include "PeakDetectorMatchedFilter.hpp"
#include "PeakDetectorMatchedFilterTestData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::frequency;
using namespace pwiz::chemistry;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using std::norm;
using std::polar;


ostream* os_ = 0;


void initializeWithTestData(FrequencyData& fd)
{
    for (TestDatum* datum=testData_; datum<testData_+testDataSize_; datum++)
        fd.data().push_back(FrequencyDatum(datum->frequency, 
            complex<double>(datum->real, datum->imaginary))); 

    fd.observationDuration(testDataObservationDuration_);
    fd.calibrationParameters(CalibrationParameters(testDataCalibrationA_, testDataCalibrationB_));
    fd.analyze();
}


void testCreation(const IsotopeEnvelopeEstimator& isotopeEnvelopeEstimator)
{
    if (os_) *os_ << "testCreation()\n";
    const int filterMatchRate = 4;
    const int filterSampleRadius = 2;
    const double peakThresholdFactor = 0;
    const double peakMaxCorrelationAngle = 5;
    const double isotopeThresholdFactor = 666;
    const double monoisotopicPeakThresholdFactor = 777;

    PeakDetectorMatchedFilter::Config config;
    config.isotopeEnvelopeEstimator = &isotopeEnvelopeEstimator; 
    config.filterMatchRate = filterMatchRate;
    config.filterSampleRadius = filterSampleRadius;
    config.peakThresholdFactor = peakThresholdFactor;
    config.peakMaxCorrelationAngle = peakMaxCorrelationAngle;
    config.isotopeThresholdFactor = isotopeThresholdFactor;
    config.monoisotopicPeakThresholdFactor = monoisotopicPeakThresholdFactor;

    auto_ptr<PeakDetectorMatchedFilter> pd = PeakDetectorMatchedFilter::create(config);

    unit_assert(pd->config().filterMatchRate == filterMatchRate); 
    unit_assert(pd->config().filterSampleRadius == filterSampleRadius); 
    unit_assert(pd->config().peakThresholdFactor == peakThresholdFactor); 
    unit_assert(pd->config().peakMaxCorrelationAngle == peakMaxCorrelationAngle); 
    unit_assert(pd->config().isotopeThresholdFactor == isotopeThresholdFactor); 
    unit_assert(pd->config().monoisotopicPeakThresholdFactor == monoisotopicPeakThresholdFactor); 
}


void testFind(FrequencyData& fd, const IsotopeEnvelopeEstimator& isotopeEnvelopeEstimator)
{
    if (os_) *os_ << "testFind()\n";

    // fill in config structure
    PeakDetectorMatchedFilter::Config config;
    config.isotopeEnvelopeEstimator = &isotopeEnvelopeEstimator; 
    config.filterMatchRate = 4;
    config.filterSampleRadius = 2;
    config.peakThresholdFactor = 2;
    config.peakMaxCorrelationAngle = 30;
    config.isotopeThresholdFactor = 2;
    config.monoisotopicPeakThresholdFactor = 2;
    config.isotopeMaxChargeState = 6;
    config.isotopeMaxNeutronCount = 4;
    config.collapseRadius = 15;
    config.useMagnitudeFilter = false;
    config.logDetailLevel = 1;
    config.log = os_;

    // instantiate
    auto_ptr<PeakDetectorMatchedFilter> pd = PeakDetectorMatchedFilter::create(config);

    // find peaks
    PeakData data;
    data.scans.push_back(Scan());
    vector<PeakDetectorMatchedFilter::Score> scores;
    pd->findPeaks(fd, data.scans[0], scores);

    // report results
    if (os_) 
    {
        *os_ << "peaks found: " << data.scans[0].peakFamilies.size() << endl;
        *os_ << data.scans[0];
        *os_ << "scores: " << scores.size() << endl;
        copy(scores.begin(), scores.end(),
            ostream_iterator<PeakDetectorMatchedFilter::Score>(*os_, "\n"));
    }

    // assertions
    unit_assert(data.scans[0].peakFamilies.size() == 1);
    const PeakFamily& peakFamily = data.scans[0].peakFamilies.back(); 
    
    if (os_) *os_ << "peakFamily: " << peakFamily << endl;
    unit_assert(peakFamily.peaks.size() > 1);
    const Peak& peak = peakFamily.peaks[0];
    unit_assert_equal(peak.getAttribute(Peak::Attribute_Frequency), 159455, 1);
    unit_assert(peakFamily.charge == 2);
    
    unit_assert(scores.size() == 1);
    const PeakDetectorMatchedFilter::Score& score = scores.back();
    unit_assert(score.charge == peakFamily.charge);
    unit_assert(score.monoisotopicFrequency == peak.getAttribute(Peak::Attribute_Frequency));
    unit_assert_equal(norm(score.monoisotopicIntensity - 
                           polar(peak.intensity, peak.getAttribute(Peak::Attribute_Phase))),
                      0, 1e-14);
}


auto_ptr<IsotopeEnvelopeEstimator> createIsotopeEnvelopeEstimator()
{
    const double abundanceCutoff = .01;
    const double massPrecision = .1; 
    IsotopeCalculator isotopeCalculator(abundanceCutoff, massPrecision);

    IsotopeEnvelopeEstimator::Config config;
    config.isotopeCalculator = &isotopeCalculator;

    return auto_ptr<IsotopeEnvelopeEstimator>(new IsotopeEnvelopeEstimator(config));
}


void test()
{
    if (os_) *os_ << setprecision(12);

    auto_ptr<IsotopeEnvelopeEstimator> isotopeEnvelopeEstimator = createIsotopeEnvelopeEstimator();

    testCreation(*isotopeEnvelopeEstimator);

    FrequencyData fd;
    initializeWithTestData(fd);

    testFind(fd, *isotopeEnvelopeEstimator);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeakDetectorMatchedFilterTest\n";
        test();
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

