//
// TruncatedLorentzianEstimator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _TRUNCATEDLORENTZIANESTIMATOR_HPP_
#define _TRUNCATEDLORENTZIANESTIMATOR_HPP_


#include "TruncatedLorentzianParameters.hpp"
#include "data/FrequencyData.hpp"
#include <memory>
#include <iosfwd>


namespace pwiz {
namespace peaks {


class TruncatedLorentzianEstimator
{
    public:

    static std::auto_ptr<TruncatedLorentzianEstimator> create();

    virtual TruncatedLorentzianParameters initialEstimate(const pwiz::data::FrequencyData& fd) const = 0;

    virtual TruncatedLorentzianParameters iteratedEstimate(const pwiz::data::FrequencyData& fd,
                                                           const TruncatedLorentzianParameters& tlp,
                                                           int iterationCount) const = 0;

    virtual double error(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;
    virtual double normalizedError(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;
    virtual double sumSquaresModel(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;

    virtual void log(std::ostream* os) = 0; // set log stream [default == &cout] 
    virtual void outputDirectory(const std::string& name) = 0; // set intermediate output [default=="" (none)]  

    virtual ~TruncatedLorentzianEstimator(){}
};


} // namespace peaks
} // namespace pwiz


#endif // _TRUNCATEDLORENTZIANESTIMATOR_HPP_ 

