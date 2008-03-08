//
// PeakData.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
// 
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PEAKDATA_HPP_
#define _PEAKDATA_HPP_


#include "CalibrationParameters.hpp"
#include <vector>
#include <string>
#include <map>


namespace pwiz {
namespace data {
namespace peakdata {


const int PeakDataFormatVersion_Major = 1;
const int PeakDataFormatVersion_Minor = 0;


struct Peak
{
    // general peak info
    double mz;
    double intensity;
    double area;
    double error; 

    // FT-specific info
    double frequency;
    double phase;
    double decay;

    Peak() : mz(0), intensity(0), area(0), error(0), 
             frequency(0), phase(0), decay(0) 
    {}
};


std::ostream& operator<<(std::ostream& os, const Peak& pd);


struct PeakFamily 
{
    double mzMonoisotopic;
    int charge;
    std::vector<Peak> peaks;

    PeakFamily() : mzMonoisotopic(0), charge(0) {}
    double sumAmplitude() const {return 0;}
    double sumArea() const {return 0;}

    void printSimple(std::ostream& os) const; // TODO: remove at some point
};


std::ostream& operator<<(std::ostream& os, const PeakFamily& pd);


struct Scan
{
    int scanNumber;
    double retentionTime;
    double observationDuration;
    CalibrationParameters calibrationParameters;
    std::vector<PeakFamily> peakFamilies;
    
    Scan() : scanNumber(0), retentionTime(0), observationDuration(0) {}

    void printSimple(std::ostream& os) const; // TODO: remove at some point
};


std::ostream& operator<<(std::ostream& os, const Scan& pd);


struct Software
{
    std::string name;
    std::string version;
    std::string source;

    typedef std::vector<std::pair<std::string,std::string> > Parameters;
    Parameters parameters;
};


struct PeakData
{
    std::string sourceFilename;
    Software software; 
    std::vector<Scan> scans;

    // temp XML writing for peakaboo
    void writeXML(std::ostream& os) const;
};


// xml serialization
std::istream& operator>>(std::istream& is, PeakData& pd);
std::ostream& operator<<(std::ostream& os, const PeakData& pd);


} // namespace peakdata 
} // namespace data 
} // namespace pwiz


#endif // _PEAKDATA_HPP_

