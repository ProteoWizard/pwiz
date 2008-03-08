//
// PeakDetectorMatchedFilter.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PEAKDETECTORMATCHEDFILTER_HPP_
#define _PEAKDETECTORMATCHEDFILTER_HPP_


#include "PeakDetector.hpp"
#include "proteome/IsotopeEnvelopeEstimator.hpp"
#include <memory>
#include <complex>


namespace pwiz {
namespace peaks {


/// MatchedFilter implementation of the PeakDetector interface. 

class PeakDetectorMatchedFilter : public PeakDetector
{
    public:

    /// structure for holding configuration
    struct Config
    {
        /// IsotopeEnvelopeEstimator pointer, must be valid for PeakDetector lifetime 
        const proteome::IsotopeEnvelopeEstimator* isotopeEnvelopeEstimator;

        /// number of filter correlations computed per frequency step 
        int filterMatchRate;

        /// number of filter samples taken on either side of 0
        int filterSampleRadius;

        /// noise floor multiple for initial peak reporting threshold 
        double peakThresholdFactor;

        /// maximum correlation angle (degrees) for initial peak reporting 
        double peakMaxCorrelationAngle;

        /// noise floor multiple for isotope filter threshold 
        double isotopeThresholdFactor;

        /// noise floor multiple for monoisotopic peak threshold 
        double monoisotopicPeakThresholdFactor;

        /// isotope filter maximum charge state to score
        int isotopeMaxChargeState;

        /// isotope filter maximum number of neutrons to score
        int isotopeMaxNeutronCount; 

        /// multiple peaks within this radius (Hz) are reported as single peak
        double collapseRadius; 

        /// use the magnitude of the peak shape filter kernel for finding peaks 
        bool useMagnitudeFilter;

        /// log detail level (0 == normal, 1 == extra)
        int logDetailLevel; 

        /// log stream (0 == no logging)
        std::ostream*  log;

        Config()
        :   isotopeEnvelopeEstimator(0),
            filterMatchRate(0),
            filterSampleRadius(0),
            peakThresholdFactor(0), 
            peakMaxCorrelationAngle(0),
            isotopeThresholdFactor(0),
            monoisotopicPeakThresholdFactor(0),
            isotopeMaxChargeState(0),
            isotopeMaxNeutronCount(0),
            collapseRadius(0),
            useMagnitudeFilter(false),
            logDetailLevel(0),
            log(0)
        {}
    };


    /// \name Instantiation 
    //@{

    /// create an instance.
    static std::auto_ptr<PeakDetectorMatchedFilter> create(const Config& config);

    virtual ~PeakDetectorMatchedFilter(){}
    //@}


    /// \name PeakDetector interface
    //@{
    virtual void findPeaks(const pwiz::data::FrequencyData& fd, 
                           pwiz::data::peakdata::Scan& result) const = 0; 
    //@}


    /// \name PeakDetectorMatchedFilter interface
    //@{

    /// access to the configuration
    virtual const Config& config() const = 0;

    /// structure for holding the matched filter calculation results
    struct Score
    {
        double frequency;
        int charge;
        int neutronCount;
        double value;

        double monoisotopicFrequency;
        std::complex<double> monoisotopicIntensity;
        int peakCount;

        std::vector<pwiz::data::peakdata::Peak> peaks;

        Score(double _f = 0, int _c = 0, int _n = 0)
        :   frequency(_f), charge(_c), neutronCount(_n),
            value(0), monoisotopicFrequency(0), monoisotopicIntensity(0),
            peakCount(0)
        {}
    };

    /// same as PeakDetector::findPeaks(), but provides additional Score information
    virtual void findPeaks(const pwiz::data::FrequencyData& fd, 
                           pwiz::data::peakdata::Scan& result,
                           std::vector<Score>& scores) const = 0; 

    //@}
};


std::ostream& operator<<(std::ostream& os, const PeakDetectorMatchedFilter::Score& a);


} // namespace peaks
} // namespace pwiz


#endif // _PEAKDETECTORMATCHEDFILTER_HPP_


