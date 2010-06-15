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


#define PWIZ_SOURCE

#include "PeakDetectorMatchedFilter.hpp"
#include "TruncatedLorentzian.hpp"
#include "TruncatedLorentzianParameters.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/chemistry/IsotopeEnvelopeEstimator.hpp"
#include "pwiz/utility/math/MatchedFilter.hpp"
#include "pwiz/utility/math/round.hpp"
#include "pwiz/utility/misc/Timer.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace frequency {


using namespace util;
using namespace math;
using namespace chemistry;
using namespace data;
using namespace data::peakdata;


namespace detail {

class TruncatedLorentzianKernel
{
    public:

    TruncatedLorentzianKernel(double T, bool computeMagnitude = false)
    :   tl_(T), computeMagnitude_(computeMagnitude)
    {
        p_.T = p_.tau = T; 
        p_.alpha = 1;
        p_.f0 = 0;
    }

    complex<double> operator()(double frequency) const
    {
        complex<double> value = tl_(frequency, p_.parameters()); 
        return computeMagnitude_ ? abs(value) : value;
    }

    typedef MatchedFilter::DxCD space_type;

    private:
    TruncatedLorentzian tl_;
    bool computeMagnitude_;
    TruncatedLorentzianParameters p_;
};

typedef MatchedFilter::KernelTraits<TruncatedLorentzianKernel>::correlation_data_type 
    CorrelationData;

} // detail


class PeakDetectorMatchedFilterImpl : public PeakDetectorMatchedFilter
{
    public:

    PeakDetectorMatchedFilterImpl(const Config& config);
    virtual const Config& config() const {return config_;} 
    virtual void findPeaks(const FrequencyData& fd, Scan& result) const;

    virtual void findPeaks(const FrequencyData& fd, 
                           Scan& result,
                           vector<Score>& scores) const;

    private:

    Config config_;
    ostream* log_;

    void analyzePeak(double frequency, 
                     const FrequencyData& fd,
                     const detail::CorrelationData& correlationData,
                     vector<Score>& goodScores) const;

    void calculateScore(Score& score,
                        const FrequencyData& fd,
                        const detail::CorrelationData& correlationData) const;

    void collapseScores(const vector<Score>& scores, vector<Score>& result) const;
};


auto_ptr<PeakDetectorMatchedFilter> 
PWIZ_API_DECL PeakDetectorMatchedFilter::create(const Config& config)
{
    return auto_ptr<PeakDetectorMatchedFilter>(new PeakDetectorMatchedFilterImpl(config));
}


PeakDetectorMatchedFilterImpl::PeakDetectorMatchedFilterImpl(const Config& config)
:   config_(config),
    log_(config.log)
{
    if (!config.isotopeEnvelopeEstimator)
        throw runtime_error("[PeakDetectorMatchedFilter] Null IsotopeEnvelopeEstimator*.");
} 


void PeakDetectorMatchedFilterImpl::findPeaks(const FrequencyData& fd,
                                              Scan& result) const
{
    vector<Score> scores;
    findPeaks(fd, result, scores);
}


namespace {


inline bool areCloseEnough(double a, double b, double epsilon)
{
    return (abs(a-b) < epsilon);
}


// RAW/mzXML scans can have holes when the signal drops below some threshold.
// When we get FrequencyData that comes from these scans, we must fill in the 
// holes when creating the SampledData object.  The holes seem to be marked
// by 4 zero samples on either side.  Filling in the holes requires the following:
// 1) calculation of the frequency step in the data
// 2) recognizing a hole as we walk through the data by comparing the actual frequency
//    to the calculated frequency of each sample
// 3) accounting for the fact that real data may not be perfectly evenly spaced


double calculateFrequencyStep(const FrequencyData& fd)
{
    double first = 0;
    double sum = 0;
    int count = 0;

    for (FrequencyData::const_iterator it=fd.data().begin()+1; it!=fd.data().end(); ++it) 
    {
        if (it->y == 0.) continue; // don't step in the holes 
        double step = it->x - (it-1)->x;
        if (first == 0.) first = step;
        sum += step; 
        ++count;
    } 
    
    if (count == 0)
        throw runtime_error("[PeakDetectorMatchedFilter::calculateFrequencyStep()] Don't know what to do.");

    double mean = sum/count;

    // sanity check: first step and mean step should be very close  
    if (!areCloseEnough(first, mean, .03))
    {
        cerr << "first: " << first << endl;
        cerr << "mean: " << mean << endl;
        throw runtime_error("[PeakDetectorMatchedFilter::calculateFrequencyStep()] I am insane!");
    }

    return mean; 
}


MatchedFilter::SampledData<MatchedFilter::DxCD> createSampledData(const FrequencyData& fd)
{
    using namespace MatchedFilter;
    typedef SampledData<DxCD> Result;

    if (fd.data().empty())
        throw runtime_error("[PeakDetectorMatchedFilter::createSampledData()] fd empty.");

    Result result;
    result.domain = make_pair(fd.data().front().x, fd.data().back().x);

    double step = calculateFrequencyStep(fd);
    int sampleCount = (int)round(result.domainWidth()/step) + 1;
    result.samples.resize(sampleCount);

    /*
    cout << setprecision(12);
    cout << "step: " << step << endl;
    cout << "sampleCount: " << sampleCount << endl;
    */

    // copy samples into result, filling in holes as necessary
    FrequencyData::const_iterator from = fd.data().begin();
    Result::samples_type::iterator to = result.samples.begin(); 
    for (int index=0; index<sampleCount && from!=fd.data().end(); ++index, ++to)
    {
        double f = result.domain.first + step * index;

        // Close enough means "within slightly more than half a step"; 
        // this allows the real frequencies and idea frequencies to get 
        // back in sync.
        if (areCloseEnough(f, from->x, step*.55)) 
        {
            //cout << setw(20) << f << setw(20) << from->x << setw(40) << from->y << endl;
            *to = from++->y; // not a hole
        }
        else
        {
            //cout << setw(20) << f << endl;
            *to = 0; // Fulfillment -- celebrate the unholiness!
        }
    }

    return result;
}


void findPeaksAux(const detail::CorrelationData& correlationData, 
                  double minMagnitude, double maxAngle,
                  vector<double>& result)
{
    if (correlationData.samples.empty()) 
        throw runtime_error("[PeakDetectorMatchedFilter::findPeaksAux()] No correlations.");

    double minNorm = minMagnitude*minMagnitude; 
    double maxTan2Angle = pow(tan(maxAngle*M_PI/180), 2);

    int index = 1;

    for (detail::CorrelationData::samples_type::const_iterator it=correlationData.samples.begin()+1; 
        it+1!=correlationData.samples.end(); ++it, ++index)
    if (norm(it->dot) >= minNorm &&             // magnitude >= minMagnitude
        it->tan2angle <= maxTan2Angle &&        // angle <= maxAngle,
        norm(it->dot) > norm((it-1)->dot) && 
        norm(it->dot) > norm((it+1)->dot))      // magnitude local maximum
    {
        double frequency = correlationData.domain.first + correlationData.dx()*index; 
        result.push_back(frequency);
    }
}


bool hasLowerMonoisotopicFrequency(const PeakDetectorMatchedFilter::Score& a, 
                                   const PeakDetectorMatchedFilter::Score& b)
{
    return a.monoisotopicFrequency < b.monoisotopicFrequency;
}


peakdata::PeakFamily score2peakFamily(const PeakDetectorMatchedFilter::Score& score)
{
    using namespace pwiz::data::peakdata;    
    
    PeakFamily peakFamily;

/*
    peakFamily.peaks.push_back(Peak());
    Peak& peak = peakFamily.peaks.back();
    
    peak.frequency = score.monoisotopicFrequency;
    peak.amplitude = abs(score.monoisotopicIntensity);
    peak.phase = arg(score.monoisotopicIntensity);
*/

    peakFamily.charge = score.charge;
    peakFamily.peaks = score.peaks; // TODO: avoid this copy of vector
    peakFamily.mzMonoisotopic = !peakFamily.peaks.empty() ? peakFamily.peaks[0].mz : 0;
    peakFamily.score = score.value;

    return peakFamily;
}


} // namespace


void PeakDetectorMatchedFilterImpl::findPeaks(const FrequencyData& fd, 
                                              Scan& result,
                                              vector<Score>& scores) const
{
    using namespace MatchedFilter;

    const data::CalibrationParameters& cp = fd.calibrationParameters();

    SampledData<DxCD> sampledData = createSampledData(fd);

    detail::TruncatedLorentzianKernel kernel(fd.observationDuration(), config_.useMagnitudeFilter); 

    detail::CorrelationData correlationData =
        computeCorrelationData(sampledData,
                               kernel,
                               config_.filterSampleRadius,
                               config_.filterMatchRate);

    // get initial list of peaks 

    if (fd.noiseFloor() == 0)
    {
        cerr << "[PeakDetectorMatchedFilter::findPeaks()] Warning: noise floor == 0.\n";
        return;
    }

    double minMagnitude = fd.noiseFloor() * config_.peakThresholdFactor;
    vector<double> peaks;

    findPeaksAux(correlationData, minMagnitude, config_.peakMaxCorrelationAngle, peaks);

    if (log_)
    {
        *log_ << "[PeakDetectorMatchedFilter]\n";

        if (config_.logDetailLevel > 0) 
        {
            *log_ << setprecision(12)
                  << "<sampledData>\n" << sampledData << "</sampledData>\n"
                  << "<correlationData>\n" << correlationData << "</correlationData>\n"
                  << endl;
        }

        *log_ << fixed << setprecision(4)
              << "filterMatchRate: " << config_.filterMatchRate << endl
              << "filterSampleRadius: " << config_.filterSampleRadius << endl
              << "peakThresholdFactor: " << config_.peakThresholdFactor << endl
              << "peakMaxCorrelationAngle: " << config_.peakMaxCorrelationAngle << endl
              << "isotopeThresholdFactor: " << config_.isotopeThresholdFactor << endl
              << "monoisotopicPeakThresholdFactor: " << config_.monoisotopicPeakThresholdFactor << endl
              << "isotopeMaxChargeState: " << config_.isotopeMaxChargeState << endl
              << "isotopeMaxNeutronCount: " << config_.isotopeMaxNeutronCount << endl
              << "collapseRadius: " << config_.collapseRadius << endl
              << "useMagnitudeFilter: " << boolalpha << config_.useMagnitudeFilter << endl
              << "logDetailLevel: " << config_.logDetailLevel << endl
              << endl
              << "noiseFloor: " << fd.noiseFloor() << endl
              << "peakThreshold: " << minMagnitude << endl
              << "isotopeThreshold: " << fd.noiseFloor() * config_.isotopeThresholdFactor << endl
              << "monoisotopicPeakThreshold: " << fd.noiseFloor() * config_.monoisotopicPeakThresholdFactor << endl
              << "\n****\n"
              << "initial peak list: " << peaks.size() << endl;

        *log_ << "    frequency       m/z      abs(dot)       angle\n"; 


        for (vector<double>::iterator it=peaks.begin(); it!=peaks.end(); ++it)
            *log_ << setw(14) << *it
                  << setw(12) << cp.mz(*it) 
                  << setw(12) << abs(correlationData.sample(*it).dot) 
                  << setw(12) << correlationData.sample(*it).angle()
                  << endl;
    }

    // analyze and create list of the good ones 
    vector<Score> goodScores;
    for (vector<double>::iterator it=peaks.begin(); it!=peaks.end(); ++it)
        analyzePeak(*it, fd, correlationData, goodScores);

    // sort good scores by monoisotopic frequency
    sort(goodScores.begin(), goodScores.end(), hasLowerMonoisotopicFrequency);

    if (log_)
    {
        *log_ << "****\nscores:\n" << fixed << setprecision(2);
        for (vector<Score>::const_iterator it=goodScores.begin(); it!=goodScores.end(); ++it)
            *log_ << setw(8) << cp.mz(it->frequency) << " " << *it << endl;
    }

    // remove any redundancies and fill in scores

    scores.clear();
    collapseScores(goodScores, scores);

    // normalize scores to noise floor
    for (vector<Score>::iterator it=scores.begin(); it!=scores.end(); ++it)
        it->value /= fd.noiseFloor();

    if (log_)
    {
        *log_ << "collapsed scores:\n";
        for (vector<Score>::const_iterator it=scores.begin(); it!=scores.end(); ++it)
            *log_ << setw(8) << cp.mz(it->frequency) << " " << *it << endl;
        *log_ << endl;
    }

    // fill in PeakData structure

    result.scanNumber = fd.scanNumber(); 
    result.retentionTime = fd.retentionTime();
    result.observationDuration = fd.observationDuration();
    result.calibrationParameters = fd.calibrationParameters();
    result.peakFamilies.clear();

    transform(scores.rbegin(), scores.rend(), back_inserter(result.peakFamilies), score2peakFamily);
}


void PeakDetectorMatchedFilterImpl::analyzePeak(double frequency, 
                                                const FrequencyData& fd,
                                                const detail::CorrelationData& correlationData,
                                                vector<Score>& goodScores) const
{
    const data::CalibrationParameters& cp = fd.calibrationParameters();

    if (log_)
        *log_ << "****\nAnalyzing peak: " << frequency 
              << " (" << cp.mz(frequency) << ")\n";

    // find the best charge state and neutron count

    Score best;

    for (int charge=1; charge<=config_.isotopeMaxChargeState; charge++)
    for (int neutronCount=0; neutronCount<=config_.isotopeMaxNeutronCount; neutronCount++)
    {
        Score current(frequency, charge, neutronCount);
        calculateScore(current, fd, correlationData);

        if (current.value > best.value)
            best = current;

        // hack: to account for low abundance 1-neutron peaks
        else if (current.value > best.value*.9 && 
                 current.charge > best.charge &&
                 current.peakCount > best.peakCount)
            best = current;
    }

    // if our best score beats our thresholds, append to goodScores 

    double scoreThreshold = fd.noiseFloor() * config_.isotopeThresholdFactor;
    double monoisotopicThreshold = fd.noiseFloor() * config_.monoisotopicPeakThresholdFactor;

    if (best.value >= scoreThreshold && 
        abs(best.monoisotopicIntensity) >= monoisotopicThreshold)
    {
        if (best.peakCount == 1)
        {
            best.charge = 0; // we don't actually know the charge state if there's only one peak

            //if (best.neutronCount > 0) cout << "[PeakDetectorMatchedFilterImpl::analyzePeak()] Warning: Lonely peak with neutronCount>0.\n";
        }

        goodScores.push_back(best);
    }

    if (log_)
    {
        *log_ << "****\n"
              << "frequency: " << best.frequency << endl
              << "maxScore: " << best.value << endl
              << "bestNeutronCount: " << best.neutronCount << endl
              << "bestCharge: " << best.charge << endl;
    }
}


namespace {
const double neutronMass_ = 1.008665;
} // namespace


void PeakDetectorMatchedFilterImpl::calculateScore(Score& score,
                                                   const FrequencyData& fd,
                                                   const detail::CorrelationData& correlationData
                                                   ) const 
{
    // assume: frequency, charge, neutronCount have been set
    // calculate: remaining fields

    const data::CalibrationParameters& cp = fd.calibrationParameters();
    double mz = cp.mz(score.frequency);
    double neutralMass = Ion::neutralMass(mz, score.charge);

    if (log_)
    {
        *log_ << "****\n" << score.frequency << "  charge:" << score.charge
              << "  neutrons:" << score.neutronCount << "  m/z:" << mz << endl; 
    }

    // don't bother with really high m/z values
    if (mz > 10000)
        return;  

    // get isotope envelope estimate based on rough estimate of monoisotopic mass
    double monoisotopicMassEstimate = neutralMass - score.neutronCount * neutronMass_;
    chemistry::MassDistribution envelope = 
        config_.isotopeEnvelopeEstimator->isotopeEnvelope(monoisotopicMassEstimate);

    // calculate better estimate of monoisotopic mass after we have isotope envelope
    double delta = score.neutronCount * neutronMass_;  // rough estimate
    if (score.neutronCount < (int)envelope.size())
    {
        // best estimate based on isotope envelope
        delta = envelope[score.neutronCount].mass; 
    }
    else if (envelope.size() >= 2)
    {
        // estimate based on first 2 isotopes
        delta = (envelope[1].mass - envelope[0].mass) * score.neutronCount; 
    }

    double monoisotopicMass = neutralMass-delta;

    double normCorrelation = 0;
    double normAbundance = 0;

    for (int n=0; n<(int)envelope.size(); n++)
    {
        double filterMass = monoisotopicMass + envelope[n].mass;
        double filterMz = Ion::mz(filterMass, score.charge);
        double filterFrequency = cp.frequency(filterMz);
        double correlation = abs(correlationData.sample(filterFrequency).dot);
        double contribution = correlation * envelope[n].abundance;

        normCorrelation += correlation*correlation;
        normAbundance += envelope[n].abundance*envelope[n].abundance;

        score.value += contribution;

        const double contributionThreshold = 1; 
        if (contribution >= contributionThreshold &&
            score.peakCount == n) // consecutive peak count
            ++score.peakCount;
            
        FrequencyData::const_iterator it = fd.findNearest(filterFrequency);
        complex<double> intensity = it->y; // closest intensity sample (no interpolation)

        // save peak info

        Peak peak;
        peak.mz = filterMz;
        peak.intensity = abs(intensity);
        peak.area = correlation; // TODO: do a real calculation 
        peak.error = 0; // TODO: calculate this
        peak.attributes[Peak::Attribute_Frequency] = filterFrequency;
        peak.attributes[Peak::Attribute_Phase] = arg(intensity);
        peak.attributes[Peak::Attribute_Decay] = 0; // TODO: calculate this
        score.peaks.push_back(peak);        

        // save monoisotopic info

        if (n==0) 
        {
            score.monoisotopicFrequency = filterFrequency;
            score.monoisotopicIntensity = intensity;
        }

        // log

        if (log_) 
        {
            *log_ << "  " 
                << setw(7) << filterMass << "  " 
                << setw(7) << filterMz << "  " 
                << setw(10) << filterFrequency << "  " 
                << setw(5) << envelope[n] << "  "  
                << setw(12) << correlation << "  "
                << setw(12) << contribution << endl;
        }
    }

    double cosAngle = score.value/sqrt(normCorrelation)/sqrt(normAbundance);
    double angle = acos(cosAngle);

    if (log_)
    {
        *log_ << "score value: " << setw(11) << score.value << endl
              << "peak count: " << score.peakCount << endl
              << "abs correlation: " << sqrt(normCorrelation) << endl
              << "abs abundance: " << sqrt(normAbundance) << endl
              << "cos angle: " << cosAngle << endl
              << "angle: " << angle << endl;
    }
}


void PeakDetectorMatchedFilterImpl::collapseScores(const vector<Score>& scores, vector<Score>& result) const
{
    // assumption: scores are sorted by monoisotopic frequency 

    for (vector<Score>::const_iterator it=scores.begin(); it!=scores.end(); ++it)
    {
        Score* last = result.empty() ? 0 : &*(result.end()-1);

        if (last && abs(it->monoisotopicFrequency - last->monoisotopicFrequency) < config_.collapseRadius)
        {
            // collapse scores with close frequencies 
            if (it->value > last->value)
                *last = *it;
        }
        else 
        {
            result.push_back(*it); 
        }
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const PeakDetectorMatchedFilter::Score& a)
{
    os << a.frequency << " (" << a.charge << ", " << a.neutronCount << ") "
        << a.value << " " << a.monoisotopicFrequency << " " << a.monoisotopicIntensity;
    return os;
}


} // namespace frequency
} // namespace pwiz

