//
// TruncatedLorentzianParameters.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _TRUNCATEDLORENTZIANPARAMETERS_HPP_
#define _TRUNCATEDLORENTZIANPARAMETERS_HPP_


#include "TruncatedLorentzian.hpp"


namespace pwiz {
namespace peaks {


/// struct for holding parameters for a Truncated Lorentzian function
struct TruncatedLorentzianParameters
{
    double T;
    double tau;
    std::complex<double> alpha;
    double f0;
    
    TruncatedLorentzianParameters();
    TruncatedLorentzianParameters(const TruncatedLorentzianParameters& that);
    TruncatedLorentzianParameters(const std::string& filename);

    /// write out to file 
    void write(const std::string& filename) const;

    /// write samples to stream
    void writeSamples(std::ostream& os) const;

    /// write samples to stream
    void writeSamples(std::ostream& os, 
                      double frequencyStart, 
                      double frequencyStep, 
                      int sampleCount) const;

    /// returns parameters in format usable by TruncatedLorentzian class
    ublas::vector<double> parameters(double shift=0, std::complex<double> scale=1) const;

    /// reads in parameters from TruncatedLorentzian format
    void parameters(const ublas::vector<double>& value, double shift=0, std::complex<double> scale=1);
};


std::ostream& operator<<(std::ostream& os, const TruncatedLorentzianParameters& tlp);
bool operator==(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u);
bool operator!=(const TruncatedLorentzianParameters& t, const TruncatedLorentzianParameters& u);


} // namespace peaks
} // namespace pwiz


#endif // _TRUNCATEDLORENTZIANPARAMETERS_HPP_ 

