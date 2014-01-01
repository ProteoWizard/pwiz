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


#define PWIZ_SOURCE

#include "PeakFamilyDetectorFT.hpp"
#include "pwiz/analysis/frequency/PeakDetectorMatchedFilter.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/utility/chemistry/IsotopeEnvelopeEstimator.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::chemistry;
using namespace pwiz::data;
using namespace pwiz::frequency;


//
// PeakFamilyDetectorFT::Impl
//


class PeakFamilyDetectorFT::Impl
{
    public:

    Impl(const Config& config);

    void detect(const MZIntensityPair* begin,
                const MZIntensityPair* end,
                vector<PeakFamily>& result);

    private:

    Config config_;

    auto_ptr<IsotopeEnvelopeEstimator> isotopeEnvelopeEstimator_;
    auto_ptr<PeakDetectorMatchedFilter> pdmf_;

    auto_ptr<FrequencyData> createFrequencyData(const MZIntensityPair* begin,
                                                const MZIntensityPair* end) const;
};


namespace {

void readSecretConfigFile(PeakDetectorMatchedFilter::Config& config)
{
    using namespace std;

    ifstream is("secret_config.txt");
    if (!is) return;

    cout << "Reading secret_config.txt...";

    map<string,string> attributes;
    
    while (is)
    {
        string name, value;
        is >> name >> value;
        if (!is) break;
        attributes[name] = value;
    }

    if (attributes.count("filterMatchRate"))
        config.filterMatchRate = atoi(attributes["filterMatchRate"].c_str());
    if (attributes.count("filterSampleRadius"))
        config.filterSampleRadius = atoi(attributes["filterSampleRadius"].c_str());
    if (attributes.count("peakThresholdFactor"))
        config.peakThresholdFactor = atof(attributes["peakThresholdFactor"].c_str());
    if (attributes.count("peakMaxCorrelationAngle"))
        config.peakMaxCorrelationAngle = atof(attributes["peakMaxCorrelationAngle"].c_str());
    if (attributes.count("isotopeThresholdFactor"))
        config.isotopeThresholdFactor = atof(attributes["isotopeThresholdFactor"].c_str());
    if (attributes.count("monoisotopicPeakThresholdFactor"))
        config.monoisotopicPeakThresholdFactor = atof(attributes["monoisotopicPeakThresholdFactor"].c_str());
    if (attributes.count("isotopeMaxChargeState"))
        config.isotopeMaxChargeState = atoi(attributes["isotopeMaxChargeState"].c_str());
    if (attributes.count("isotopeMaxNeutronCount"))
        config.isotopeMaxNeutronCount = atoi(attributes["isotopeMaxNeutronCount"].c_str());
    if (attributes.count("collapseRadius"))
        config.collapseRadius = atof(attributes["collapseRadius"].c_str());

    cout << "done.\n" << flush;
}

PeakDetectorMatchedFilter::Config getPeakDetectorConfiguration()
{
    PeakDetectorMatchedFilter::Config config;
    config.filterMatchRate = 4;
    config.filterSampleRadius = 2;
    config.peakThresholdFactor = 4;
    config.peakMaxCorrelationAngle = 30;
    config.isotopeThresholdFactor = 4;
    config.monoisotopicPeakThresholdFactor = 1.5;
    config.isotopeMaxChargeState = 6;
    config.isotopeMaxNeutronCount = 5;
    config.collapseRadius = 15;
    config.useMagnitudeFilter = true;
    config.logDetailLevel = 1;

    readSecretConfigFile(config);

    return config;
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

} // namespace


PeakFamilyDetectorFT::Impl::Impl(const Config& config)
:   config_(config)
{
    // instantiate IsotopeEnvelopeEstimator
    isotopeEnvelopeEstimator_ = createIsotopeEnvelopeEstimator();    

    // fill in PeakDetectorMatchedFilter::Config structure
    PeakDetectorMatchedFilter::Config pdmfConfig = getPeakDetectorConfiguration();
    pdmfConfig.isotopeEnvelopeEstimator = isotopeEnvelopeEstimator_.get();
    pdmfConfig.log = config.log;

    // instantiate PeakDetector
    pdmf_ = PeakDetectorMatchedFilter::create(pdmfConfig);
}


void PeakFamilyDetectorFT::Impl::detect(const MZIntensityPair* begin,
                                        const MZIntensityPair* end,
                                        vector<PeakFamily>& result)
{
    if (!begin || !end || begin==end) return; 

    // convert mass data to frequency data

    auto_ptr<FrequencyData> fd = createFrequencyData(begin, end);
    if (!fd.get() || fd->data().empty())
        throw NoDataException(); 

    // find peaks in the frequency data

    peakdata::Scan scan;
    pdmf_->findPeaks(*fd, scan);
    result = scan.peakFamilies; 
}


auto_ptr<FrequencyData> 
PeakFamilyDetectorFT::Impl::createFrequencyData(const MZIntensityPair* begin,
                                                const MZIntensityPair* end) const
{
    auto_ptr<FrequencyData> fd(new FrequencyData);

    // convert mass/intensity pairs to frequency/intensity pairs
    
    for (const MZIntensityPair* it=end-1; it>=begin; --it)
        fd->data().push_back(FrequencyDatum(config_.cp.frequency(it->mz), it->intensity));

    // fill in metadata

    fd->observationDuration(fd->observationDurationEstimatedFromData());
    fd->calibrationParameters(config_.cp);
    fd->analyze();

    // noise floor calculation must account for fact that there may be holes in mass data!

    fd->noiseFloor(fd->cutoffNoiseFloor());

    // log if requested
   
    if (config_.log)
    {
        *config_.log << setprecision(6) << fixed
                     << "[MassPeakDetector::createFrequencyData()]\n"
                     << "mzLow: " << begin->mz << endl 
                     << "mzHigh: " << (end-1)->mz << endl 
                     << "A: " << config_.cp.A << endl 
                     << "B: " << config_.cp.B << endl 
                     << "observationDuration: " << fd->observationDuration() << endl 
                     << "noiseFloor: " << fd->noiseFloor() << endl
                     << "<data>" << endl
                     << "#       m/z           freq          intensity\n";

        for (const MZIntensityPair* it=end-1; it>=begin; --it)
            *config_.log << setw(15) << it->mz
                         << setw(15) << config_.cp.frequency(it->mz)
                         << setw(15) << it->intensity
                         << endl;
        
        *config_.log << "</data>" << endl << endl;
    } 

    return fd;
}


//
// PeakFamilyDetectorFT
//


PWIZ_API_DECL PeakFamilyDetectorFT::PeakFamilyDetectorFT(const Config& config)
:   impl_(new Impl(config))
{}


PWIZ_API_DECL
void PeakFamilyDetectorFT::detect(const MZIntensityPair* begin,
                                  const MZIntensityPair* end,
                                  vector<PeakFamily>& result)
{
    impl_->detect(begin, end, result);
}


} // namespace analysis 
} // namespace pwiz

