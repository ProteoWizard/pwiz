//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
// 
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
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

#ifndef _PEAKDATA_HPP_
#define _PEAKDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "CalibrationParameters.hpp"
#include "pwiz/utility/misc/MSIHandler.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/math/OrderedPair.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>
#include <string>


namespace pwiz {
namespace data {
namespace peakdata {


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace minimxml::SAXParser;


const int PeakDataFormatVersion_Major = 1;
const int PeakDataFormatVersion_Minor = 1;


struct PWIZ_API_DECL Peak
{
    int id;

    double mz;
    double retentionTime;

    double intensity;   // peak height
    double area;        // sum/total intensity
    double error;       // error in model fit

    std::vector<pwiz::math::OrderedPair> data;

    // optional attributes

    enum Attribute 
    {
        Attribute_Frequency, 
        Attribute_Phase,
        Attribute_Decay
    };

    typedef std::map<Attribute, double> Attributes; 
    Attributes attributes;

    Peak(double _mz = 0, double _retentionTime = 0);

    double getAttribute(Attribute attribute) const;

    bool operator==(const Peak& that) const;
    bool operator!=(const Peak& that) const;

    void write(minimxml::XMLWriter& writer) const;
    void read(std::istream& is);
};


struct HandlerPeak : public SAXParser::Handler
{
    Peak* peak;
    HandlerPeak(Peak* _peak = 0) : peak(_peak)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const std::string& name,
                                const Attributes& attributes,
                                stream_offset position);

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Peak& peak);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, Peak& peak);


struct PWIZ_API_DECL PeakFamily 
{
    double mzMonoisotopic;
    int charge;
    double score;
    std::vector<Peak> peaks;
    
    PeakFamily() : mzMonoisotopic(0), charge(0), score(0) {}
    double sumAmplitude() const {return 0;}
    double sumArea() const {return 0;}

    void write(minimxml::XMLWriter& writer) const;
    void read(std::istream& is);

    bool operator==(const PeakFamily& that) const;
    bool operator!=(const PeakFamily& that) const;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakFamily& peakFamily);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, PeakFamily& peakFamily);


struct PWIZ_API_DECL Scan
{
    size_t index;
    std::string nativeID;
    int scanNumber; // TODO: remove
    double retentionTime;
    double observationDuration;
    CalibrationParameters calibrationParameters;
    std::vector<PeakFamily> peakFamilies;

    Scan() : index(0), scanNumber(0), retentionTime(0), observationDuration(0) {}

    void write(minimxml::XMLWriter& writer) const;
    void read(std::istream& is);
  
    bool operator==(const Scan& scan) const;
    bool operator!=(const Scan& scan) const;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Scan& scan);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, Scan& scan);


struct PWIZ_API_DECL Software
{

    std::string name;
    std::string version;
    std::string source;

    struct PWIZ_API_DECL Parameter
    {
        std::string name;
        std::string value;
      
        Parameter() : name(""), value("") {};
        Parameter(std::string name_, std::string value_) : name(name_), value(value_) {}
        void write(minimxml::XMLWriter& xmlWriter) const;
        void read(std::istream& is);
        bool operator==(const Parameter& that) const;
        bool operator!=(const Parameter& that) const;

    };
   
    std::vector<Parameter> parameters;
    
    Software() : name(""), version(""), source(""), parameters(0) {}
    void write(minimxml::XMLWriter& xmlWriter) const;
    void read(std::istream& is);
  
    bool operator==(const Software& that) const;
    bool operator!=(const Software& that) const;
};


struct PWIZ_API_DECL PeakData
{
    std::string sourceFilename;
    Software software; 
    std::vector<Scan> scans;

    void write(pwiz::minimxml::XMLWriter& xmlWriter) const;
    void read(std::istream& is);

    bool operator==(const PeakData& that) const;
    bool operator!=(const PeakData& that) const;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakData& pd);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, PeakData& pd);


///
/// struct for an eluted peak (PEAK ELution)
/// 
struct PWIZ_API_DECL Peakel
{
    // metadata
    double mz;
    double retentionTime;
    double maxIntensity;
    double totalIntensity;
    double mzVariance;

    // peak data
    std::vector<Peak> peaks;
    
    // construction
    Peakel();
    Peakel(const Peak& peak);

    /// recalculates all metadata based on peak data
    void calculateMetadata();

    // retention times grabbed from peak data; assume peaks are ordered by retention time
    double retentionTimeMin() const;
    double retentionTimeMax() const;

    void write(pwiz::minimxml::XMLWriter& xmlWriter) const;
    void read(std::istream& is);
    
    bool operator==(const Peakel& that) const;
    bool operator!=(const Peakel& that) const;
};


typedef boost::shared_ptr<Peakel> PeakelPtr;


struct HandlerPeakel : public SAXParser::Handler
{
    Peakel* peakel;
    HandlerPeakel(Peakel* _peakel = 0) : peakel(_peakel){}
    virtual Status startElement(const std::string& name, const Attributes& attributes, stream_offset position);

    private:
    HandlerPeak _handlerPeak;
    size_t _peakCount;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Peakel& peakel);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, Peakel& peakel);


struct PWIZ_API_DECL Feature
{
    Feature();
    Feature(const MSIHandler::Record& record);

    std::string id; // assigned by feature detection, for easier lookup 
    double mz;
    double retentionTime;
    int charge;
    double totalIntensity;
    double rtVariance;
    double score;
    double error;
    std::vector<PeakelPtr> peakels;
    
    void calculateMetadata();

    // retention time range calculation based on first two Peakels
    double retentionTimeMin() const;
    double retentionTimeMax() const;

    void write(pwiz::minimxml::XMLWriter& xmlWriter) const;
    void read(std::istream& is);
  
    bool operator==(const Feature& that) const;
    bool operator!=(const Feature& that) const;

    // note: copy/assignment are shallow copy (same peakels)
};


typedef boost::shared_ptr<Feature> FeaturePtr;

 
struct HandlerFeature : public SAXParser::Handler // included in header file for accession by MatchData
{
    Feature* feature;
    HandlerFeature(Feature* _feature = 0) : feature(_feature){}

    virtual Status startElement(const std::string& name, const Attributes& attributes, stream_offset position);

    private:
    HandlerPeakel _handlerPeakel;
    size_t _peakelCount;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Feature& feature);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, Feature& feature);


struct PWIZ_API_DECL FeatureFile
{
    FeatureFile(){}
    std::vector<FeaturePtr> features;

    void write(pwiz::minimxml::XMLWriter& xmlWriter) const;
    void read(std::istream& is);

private:

    FeatureFile(FeatureFile&);
    FeatureFile operator=(FeatureFile&);

};


} // namespace peakdata 
} // namespace data 
} // namespace pwiz

#endif // _PEAKDATA_HPP_

