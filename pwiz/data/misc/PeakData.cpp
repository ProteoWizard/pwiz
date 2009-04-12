//
// PeakData.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
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

#include "PeakData.hpp"
#include "boost/lexical_cast.hpp"
#include <complex>
#include <iterator>
#include <iostream>

namespace pwiz {
namespace data {
namespace peakdata {


using namespace std;
using namespace pwiz::minimxml;
using namespace minimxml::SAXParser;
using boost::lexical_cast;


//
// Peak
//


Peak::Peak()
:   mz(0), retentionTime(0), scanNumber(0), intensity(0), area(0), error(0),
    frequency(0), phase(0), decay(0) 
{}


bool Peak::operator==(const Peak& that) const
{
    return mz == that.mz &&
           retentionTime == that.retentionTime &&
           scanNumber == that.scanNumber &&
           intensity == that.intensity &&
           area == that.area &&
           error == that.error &&
           frequency == that.frequency &&
           phase == that.phase &&
           decay == that.decay;
}


bool Peak::operator!=(const Peak& that) const
{
    return !(*this==that);
}


void Peak::write(minimxml::XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("mz", lexical_cast<string>(mz)));
    attributes.push_back(make_pair("retentionTime", lexical_cast<string>(retentionTime)));
    attributes.push_back(make_pair("scanNumber", lexical_cast<string>(scanNumber)));
    attributes.push_back(make_pair("intensity", lexical_cast<string>(intensity)));
    attributes.push_back(make_pair("area", lexical_cast<string>(area)));
    attributes.push_back(make_pair("error", lexical_cast<string>(error)));
    attributes.push_back(make_pair("frequency", lexical_cast<string>(frequency)));
    attributes.push_back(make_pair("phase", lexical_cast<string>(phase)));
    attributes.push_back(make_pair("decay", lexical_cast<string>(decay)));
    writer.startElement("peak", attributes, XMLWriter::EmptyElement);
}


SAXParser::Handler::Status HandlerPeak::startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
{
        if (name != "peak")
            throw runtime_error(("[HandlerPeak] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "mz", peak->mz);
        getAttribute(attributes, "retentionTime", peak->retentionTime);
        getAttribute(attributes, "scanNumber", peak->scanNumber);
        getAttribute(attributes, "intensity", peak->intensity);
        getAttribute(attributes, "area", peak->area);
        getAttribute(attributes, "error", peak->error);
        getAttribute(attributes, "frequency", peak->frequency);
        getAttribute(attributes, "phase", peak->phase);
        getAttribute(attributes, "decay", peak->decay);

        return Status::Ok;

}



void Peak::read(istream& is)
{
    HandlerPeak handler(this);
    SAXParser::parse(is, handler);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Peak& peak)
{
    XMLWriter writer(os);
    peak.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, Peak& peak)
{
    peak.read(is);
    return is;
}


//
// PeakFamily
//


void PeakFamily::write(XMLWriter& writer) const
{  
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("mzMonoisotopic", boost::lexical_cast<string>(mzMonoisotopic)));
    attributes.push_back(make_pair("charge", boost::lexical_cast<string>(charge)));
    attributes.push_back(make_pair("score", boost::lexical_cast<string>(score)));
  
    writer.startElement("peakFamily", attributes);

    XMLWriter::Attributes attributes_pks;
    attributes_pks.push_back(make_pair("count", boost::lexical_cast<string>(peaks.size())));

    writer.startElement("peaks", attributes_pks);
    for (vector<Peak>::const_iterator it = peaks.begin(); it != peaks.end(); ++it)
      {
        it->write(writer);

      }

    writer.endElement();

    writer.endElement();

}

struct HandlerPeakFamily : public SAXParser::Handler
{
    PeakFamily* peakFamily;
    HandlerPeakFamily(PeakFamily* _peakFamily = 0) : peakFamily(_peakFamily){}

    virtual Status startElement(const string& name,
                  const Attributes& attributes,
                  stream_offset position)
    {
      if(name == "peakFamily")
        {
            getAttribute(attributes, "mzMonoisotopic", peakFamily->mzMonoisotopic);
            getAttribute(attributes, "charge", peakFamily->charge);
            getAttribute(attributes, "score", peakFamily->score);   

            return Handler::Status::Ok; 

        }

      else if (name == "peaks")
        { 
            getAttribute(attributes, "count", _peaksCount);
            return Handler::Status::Ok;
    
        }

      else 
        {
            if (name != "peak")
              {
                throw runtime_error(("[HandlerPeakFamily] Unexpected element found: " + name).c_str());
                return Handler::Status::Done;
              }

            peakFamily->peaks.push_back(Peak());
            _handlerPeak.peak = &(peakFamily->peaks.back());
      
            return Handler::Status(Status::Delegate, &_handlerPeak);
          }

      if (_peaksCount != peakFamily->peaks.size())
        {
            throw runtime_error("[HandlerPeakFamily] <peaks count> != peakFamily->peaks.size()");
            return Handler::Status::Done;
        }
    }

private:

    HandlerPeak _handlerPeak;
    size_t _peaksCount;
};

void PeakFamily::read(istream& is)
{

    HandlerPeakFamily handlerPeakFamily(this);
    SAXParser::parse(is, handlerPeakFamily);


}


bool PeakFamily::operator==(const PeakFamily& that) const
{

    return mzMonoisotopic == that.mzMonoisotopic &&
      charge == that.charge &&
      score == that.score&&
      peaks == that.peaks;

}

bool PeakFamily::operator!=(const PeakFamily& that) const
{
    return !(*this == that);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const PeakFamily& pf)
{
    XMLWriter writer(os);
    pf.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, PeakFamily& peakFamily)
{
    peakFamily.read(is);
    return is;
}


//
// Scan 
//


void Scan::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;

    attributes.push_back(make_pair("index", boost::lexical_cast<string>(index)));
    attributes.push_back(make_pair("nativeID", boost::lexical_cast<string>(nativeID)));
    attributes.push_back(make_pair("scanNumber", boost::lexical_cast<string>(scanNumber)));
    attributes.push_back(make_pair("retentionTime", boost::lexical_cast<string>(retentionTime)));
    attributes.push_back(make_pair("observationDuration", boost::lexical_cast<string>(observationDuration)));

    writer.startElement("scan", attributes);

    XMLWriter::Attributes attributes_cp;

    attributes_cp.push_back(make_pair("A", boost::lexical_cast<string>(calibrationParameters.A)));
    attributes_cp.push_back(make_pair("B",boost::lexical_cast<string>(calibrationParameters.B)));

    writer.startElement("calibrationParameters",attributes_cp,XMLWriter::EmptyElement);

    XMLWriter::Attributes attributes_pf;

    attributes_pf.push_back(make_pair("count", boost::lexical_cast<string>(peakFamilies.size())));

    writer.startElement("peakFamilies", attributes_pf);

    for (vector<PeakFamily>::const_iterator it = peakFamilies.begin(); it != peakFamilies.end(); ++it)
      it->write(writer);

    writer.endElement();

    writer.endElement();

}


struct HandlerScan : public SAXParser::Handler
{
    Scan* scan;
    HandlerScan(Scan* _scan = 0) : scan(_scan){}

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
    
      if(name == "scan")
        {

            getAttribute(attributes, "index", scan->index);
            getAttribute(attributes, "nativeID", scan->nativeID);
            getAttribute(attributes, "scanNumber", scan->scanNumber);
            getAttribute(attributes, "retentionTime", scan->retentionTime);
            getAttribute(attributes, "observationDuration", scan->observationDuration);

            return Handler::Status::Ok;
       
        }
    

      else if(name == "calibrationParameters")
        {
            getAttribute(attributes, "A", scan->calibrationParameters.A);
            getAttribute(attributes, "B", scan->calibrationParameters.B);
            return Handler::Status::Ok;

        }

      else if(name == "peakFamilies")
        {
            getAttribute(attributes,"count", _peakFamilyCount);
            return Handler::Status::Ok;

        }

      else
        {
            if(name != "peakFamily")
              {
                throw runtime_error(("[HandlerScan] Unexpected element name : " + name).c_str());
                return Handler::Status::Done;
              }
    
            scan->peakFamilies.push_back(PeakFamily());
            PeakFamily& peakFamily = scan->peakFamilies.back();
            _handlerPeakFamily.peakFamily = &peakFamily;
            return Handler::Status(Status::Delegate,&_handlerPeakFamily);
        }

      if( _peakFamilyCount != scan->peakFamilies.size())
        {
            throw runtime_error("[HandlerScan] <peakFamilies count> != scan->peakFamilies.size()");
            return Handler::Status::Done;
     
        }

    }
  
private:

    HandlerPeakFamily _handlerPeakFamily;
    size_t _peakFamilyCount;
};


void Scan::read(istream& is)
{
    HandlerScan handlerScan(this);
    parse(is, handlerScan);

}


bool Scan::operator==(const Scan& scan) const
{
    return index == scan.index &&
      nativeID == scan.nativeID &&
      scanNumber == scan.scanNumber &&
      retentionTime == scan.retentionTime &&
      observationDuration == scan.observationDuration &&
      calibrationParameters.A == scan.calibrationParameters.A &&
      calibrationParameters.B == scan.calibrationParameters.B &&
      peakFamilies == scan.peakFamilies;

}


bool Scan::operator!=(const Scan& scan) const
{
    return !(*this == scan);

}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Scan& scan)
{
    XMLWriter writer(os);
    scan.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, Scan& scan)
{
    scan.read(is);
    return is;
}


//
// Software
//


void Software::Parameter::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("name", boost::lexical_cast<string>(name)));
    attributes.push_back(make_pair("value", boost::lexical_cast<string>(value)));
    writer.startElement("parameter", attributes, XMLWriter::EmptyElement);
}


void Software::write(XMLWriter& writer) const

{
    XMLWriter::Attributes attributes;

    attributes.push_back(make_pair("name", boost::lexical_cast<string>(name)));
    attributes.push_back(make_pair("version", boost::lexical_cast<string>(version)));
    attributes.push_back(make_pair("source", boost::lexical_cast<string>(source)));
    writer.startElement("software", attributes);

    XMLWriter::Attributes attributes_p;

    attributes_p.push_back(make_pair("count", boost::lexical_cast<string>(parameters.size())));
    writer.startElement("parameters", attributes_p);
 
    for (vector<Parameter>::const_iterator it = parameters.begin();
         it != parameters.end(); ++it)
      {
          it->write(writer);
      }

    writer.endElement();
    writer.endElement();
}


struct HandlerParameter : public SAXParser::Handler
{
    Software::Parameter* parameter;
    HandlerParameter(Software::Parameter* _parameter = 0) : parameter(_parameter){}

    virtual Status startElement(const string& current, const Attributes& attributes, stream_offset position)
    {
        if(current != "parameter")
          {
            throw runtime_error(("[HandlerParameter] Something strange in the neighborhood ... " + current).c_str());

          }

        getAttribute(attributes,"name", parameter->name);
        getAttribute(attributes,"value", parameter->value);
    
        return Status::Ok;

    }

};


struct HandlerSoftware : public SAXParser::Handler
{
    Software* software;
    HandlerSoftware(Software* _software = 0) : software(_software) {}

    virtual Status startElement(const string& current, const Attributes& attributes, stream_offset position)
    {
      if(current == "software")
        {
            getAttribute(attributes, "name", software->name);
            getAttribute(attributes, "version", software->version);
            getAttribute(attributes, "source", software->source);

            return Status::Ok;
        }
    
      else if(current == "parameters")
        {
            getAttribute(attributes, "count", _parameterCount);
            return Status::Ok;
        }

      else 
        {
            if(current != "parameter")
              {
                throw runtime_error(("[HandlerSoftware] Unexpected element found : " + current).c_str());
                return Status::Done;
              }

            software->parameters.push_back(Software::Parameter());
            _handlerParameter.parameter = &(software->parameters.back());
            return Status(Status::Delegate, &_handlerParameter);
        }

      if (_parameterCount != software->parameters.size())  
        {
            throw runtime_error("[HandlerSoftware] <parameters count> != software-> parameters.size() ");
            return Status::Done;
        }
    }

private:

    HandlerParameter _handlerParameter;
    size_t _parameterCount;
};


void Software::Parameter::read(istream& is) 
{
    HandlerParameter handlerParameter(this);
    parse(is, handlerParameter);

}


void Software::read(istream& is)
{
    HandlerSoftware handlerSoftware(this);
    parse(is, handlerSoftware);

}


bool Software::Parameter::operator==(const Software::Parameter& that) const
{
    return name == that.name &&
      value == that.value;

}


bool Software::Parameter::operator!=(const Software::Parameter& that) const
{
    return !(*this == that);

}


bool Software::operator==(const Software& that) const
{
  return name == that.name &&
    version == that.version &&
    source == that.source &&
    parameters == that.parameters;

}


bool Software::operator!=(const Software& that) const
{
  return !(*this == that);

}


//
// PeakData
//


void PeakData::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("sourceFilename",sourceFilename));

    writer.startElement("peakData",attributes);
    software.write(writer);
  
    XMLWriter::Attributes attributes_scans;
    attributes_scans.push_back(make_pair("count", boost::lexical_cast<string>(scans.size())));
    writer.startElement("scans", attributes_scans);

    vector<Scan>::const_iterator it = scans.begin();
    for(; it != scans.end(); ++it)
        it->write(writer);
    writer.endElement();
    writer.endElement();
}


struct HandlerPeakData : public SAXParser::Handler
{
    PeakData* peakData;
    HandlerPeakData(PeakData* _peakData) : peakData(_peakData) {}

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
      if (name == "peakData")
        {
            getAttribute(attributes, "sourceFilename", peakData->sourceFilename);
            return Status::Ok;
        }
    
      else if (name == "software")
        {
            _handlerSoftware.software = &(peakData->software);
            return Status(Status::Delegate, &_handlerSoftware);
        }
    
      else if (name == "scans")
        {
            getAttribute(attributes, "count", _scanCount);
            return Status::Ok;
        }
      else 
        {
            if (name != "scan")
              {
                throw runtime_error(("[HandlerPeakData] Unexpected element found : " + name).c_str());
                return Status::Done;
              }
    
            peakData->scans.push_back(Scan());
            _handlerScan.scan = &(peakData->scans.back());
            return Status(Status::Delegate, &_handlerScan); 
        }
      
    
      if(_scanCount != peakData->scans.size())
        {
            throw runtime_error("[HandlerPeakData] <scans count> != peakData->scans.size()");
            return Status::Done;

        }
      
    }

private:

    HandlerSoftware _handlerSoftware;
    HandlerScan _handlerScan;
    size_t _scanCount;

};


void PeakData::read(istream& is)
{
    HandlerPeakData handlerPeakData(this);
    SAXParser::parse(is, handlerPeakData);
}


bool PeakData::operator==(const PeakData& that) const
{
    return sourceFilename == that.sourceFilename &&
      software == that.software &&
      scans == that.scans;
}


bool PeakData::operator!=(const PeakData& that) const
{
    return !(*this == that);
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakData& pd)
{
    XMLWriter writer(os);
    pd.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, PeakData& pd)
{
    pd.read(is);
    return is;
}

///
/// Peakel
///

void Peakel::calculateMetadata()
{
    // wipe out any metadata that may have been set
    mz = 0;
    retentionTime = 0;
    mzVariance = 0;
    maxIntensity = 0;
    totalIntensity = 0;

    // calculate intensity metadata
    vector<Peak>::iterator calc_intensity_it = peaks.begin();
    for(; calc_intensity_it != peaks.end(); ++calc_intensity_it)
        {
            if (calc_intensity_it->intensity > maxIntensity) maxIntensity = calc_intensity_it->intensity;
            totalIntensity += calc_intensity_it->intensity;
        }

    //ignore peakels of total intensity zero in rt calculations to avoid div by zero
    // peakels will still be reported in feature
    
    
    // calculate mz and retentionTime mean
    vector<Peak>::iterator calc_mean_it = peaks.begin();
    for(; calc_mean_it != peaks.end(); ++calc_mean_it)
        {
            mz += calc_mean_it->mz;
            if (totalIntensity != 0) retentionTime += (calc_mean_it->retentionTime)*(calc_mean_it->intensity)/totalIntensity; //weighted average
         
        }

    mz = mz / peaks.size();

    // calculate mz variance
    vector<Peak>::iterator calc_var_it = peaks.begin();
    for(; calc_var_it != peaks.end(); ++calc_var_it)
        {
            mzVariance += (calc_var_it->mz - mz)*(calc_var_it->mz - mz);
        }

    mzVariance = mzVariance / peaks.size();

    return;
}


void Peakel::write(pwiz::minimxml::XMLWriter& xmlWriter) const
{

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("mz", boost::lexical_cast<string>(mz)));
    attributes.push_back(make_pair("retentionTime",boost::lexical_cast<string>(retentionTime)));
    attributes.push_back(make_pair("maxIntensity", boost::lexical_cast<string>(maxIntensity)));
    attributes.push_back(make_pair("totalIntensity", boost::lexical_cast<string>(totalIntensity)));
    attributes.push_back(make_pair("mzVariance", boost::lexical_cast<string>(mzVariance)));

    xmlWriter.startElement("peakel", attributes);
    
    XMLWriter::Attributes attributes_p;
    attributes_p.push_back(make_pair("count", boost::lexical_cast<string>(peaks.size())));

    xmlWriter.startElement("peaks", attributes_p);
    
    vector<Peak>::const_iterator peak_it = peaks.begin();
    for(; peak_it != peaks.end(); ++peak_it) 
        peak_it->write(xmlWriter);      
  
    xmlWriter.endElement();
    xmlWriter.endElement();
}

SAXParser::Handler::Status HandlerPeakel::startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
      if (name == "peakel")
        {
            getAttribute(attributes,"mz", peakel->mz);
            getAttribute(attributes,"retentionTime", peakel->retentionTime);
            getAttribute(attributes,"maxIntensity", peakel->maxIntensity);
            getAttribute(attributes,"totalIntensity", peakel->totalIntensity);
            getAttribute(attributes,"mzVariance", peakel->mzVariance);
            return Handler::Status::Ok;
        }

      else if (name=="peaks")
        {     
            getAttribute(attributes,"count", _peakCount);
            return Status::Ok;
        }

      else
        {
          if (name != "peak")
            {
                throw runtime_error(("[HandlerPeakel] Unexpected element name: " + name).c_str());
                return Status::Done;

            }

          else
            {

                peakel->peaks.push_back(Peak());
                _handlerPeak.peak = &(peakel->peaks.back());
                return Status(Status::Delegate, &_handlerPeak);
              
            }

        }
      
      if (_peakCount != peakel->peaks.size())
        {
            throw runtime_error("[HandlerPeakel] <peaks count> != peakel->peaks.size()");
            return Status::Done;

        }

}

void Peakel::read(istream& is)
{
    HandlerPeakel handlerPeakel(this);
    parse(is, handlerPeakel);

}

bool Peakel::operator==(const Peakel& that) const
{
    return mz == that.mz &&
      retentionTime == that.retentionTime &&
      maxIntensity == that.maxIntensity &&
      totalIntensity == that.totalIntensity &&
      mzVariance == that.mzVariance &&
      peaks == that.peaks;

}

bool Peakel::operator!=(const Peakel& that) const
{
    return !(*this == that);

}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Peakel& peakel)
{
    XMLWriter writer(os);
    peakel.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, Peakel& peakel)
{
    peakel.read(is);
    return is;
}

///
/// Feature
///


void Feature::calculateMetadata()
{
    // wipe out any metadata that may have been set
    mzMonoisotopic = 0;
    retentionTime = 0;
    rtVariance = 0;
    totalIntensity = 0;

    // calculate metadata of each peakel
    
    vector<Peakel>::iterator calc_pkl_it = peakels.begin();
    for(; calc_pkl_it != peakels.end(); ++calc_pkl_it)
        {   
            calc_pkl_it->calculateMetadata();
           
        }

    // write mzMonoisotopic (mz of first peakel)
    mzMonoisotopic = peakels.begin()->mz;

    // calculate totalIntensity and maxIntensity 
    vector<Peakel>::iterator calc_intensity_it = peakels.begin();
    for(; calc_intensity_it != peakels.end(); ++calc_intensity_it)
        {
            totalIntensity += calc_intensity_it->totalIntensity;
           
        }

    // calculate retentionTime (weighted mean of peakel retentionTimes)
    vector<Peakel>::iterator calc_mean_it = peakels.begin();
    for(; calc_mean_it != peakels.end(); ++calc_mean_it)
        {
            retentionTime += (calc_mean_it->retentionTime) * (calc_mean_it->totalIntensity)/totalIntensity; //weighted average

        }
    
    // calculate rtVariance ( variance of peakel retentionTimes)
    vector<Peakel>::iterator calc_var_it = peakels.begin();
    for(; calc_var_it != peakels.end(); ++calc_var_it)
        {
            rtVariance += (calc_var_it->retentionTime - retentionTime) * (calc_var_it->retentionTime - retentionTime);
            
        }

    rtVariance = rtVariance / peakels.size();

    return;

}



void Feature::write(pwiz::minimxml::XMLWriter& xmlWriter) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", boost::lexical_cast<string>(id)));
    attributes.push_back(make_pair("mzMonoisotopic", boost::lexical_cast<string>(mzMonoisotopic)));
    attributes.push_back(make_pair("retentionTime", boost::lexical_cast<string>(retentionTime)));
    attributes.push_back(make_pair("ms1_5", ms1_5));
    attributes.push_back(make_pair("ms2", ms2));
    attributes.push_back(make_pair("charge", boost::lexical_cast<string>(charge)));
    attributes.push_back(make_pair("totalIntensity", boost::lexical_cast<string>(totalIntensity)));
    attributes.push_back(make_pair("rtVariance", boost::lexical_cast<string>(rtVariance)));

    xmlWriter.startElement("feature",attributes);

    XMLWriter::Attributes attributes_pkl;
    attributes_pkl.push_back(make_pair("count", boost::lexical_cast<string>(peakels.size())));
  
    xmlWriter.startElement("peakels",attributes_pkl);
  
    vector<Peakel>::const_iterator pkl_it = peakels.begin();
    for(; pkl_it != peakels.end(); ++pkl_it)
        pkl_it->write(xmlWriter);

    xmlWriter.endElement();
    xmlWriter.endElement();

}

SAXParser::Handler::Status HandlerFeature::startElement(const string& name, const Attributes& attributes, stream_offset position)

{
      if (name == "feature")
        {
            getAttribute(attributes,"id", feature->id);
            getAttribute(attributes,"mzMonoisotopic", feature->mzMonoisotopic);
            getAttribute(attributes,"retentionTime", feature->retentionTime);
            getAttribute(attributes,"ms1_5", feature->ms1_5);
            getAttribute(attributes, "ms2", feature->ms2);
            getAttribute(attributes,"charge", feature->charge);
            getAttribute(attributes,"totalIntensity", feature->totalIntensity);
            getAttribute(attributes,"rtVariance", feature->rtVariance);

            return Handler::Status::Ok;

        }

      else if (name=="peakels")
        {
            getAttribute(attributes,"count", _peakelCount);
            return Status::Ok;
        }

      else
        {
          if (name != "peakel")
            {
                throw runtime_error(("[HandlerFeature] Unexpected element name: " + name).c_str());
                return Status::Done;

            }

          else
            {
                feature->peakels.push_back(Peakel());
                _handlerPeakel.peakel = &(feature->peakels.back());
                return Status(Status::Delegate, &_handlerPeakel);

            }

        }

      if (_peakelCount != feature->peakels.size())
        {
            throw runtime_error("[HandlerFeature]: <peakels count> != feature->peakels.size()");
            return Status::Done;
        }

}

Feature::Feature(const MSIHandler::Record& record) 
{
  
    mzMonoisotopic = record.mz;
	retentionTime = record.time;
	charge = record.charge;
	totalIntensity = record.intensity;	
	rtVariance = 0;
   
}

void Feature::read(istream& is)
{

    HandlerFeature handlerFeature(this);
    parse(is, handlerFeature);



}


bool Feature::operator==(const Feature& that) const
{
    return id == that.id &&
      mzMonoisotopic == that.mzMonoisotopic &&
      retentionTime == that.retentionTime &&
      ms1_5 == that.ms1_5 &&
      ms2 == that.ms2 &&  
      charge == that.charge &&
      totalIntensity == that.totalIntensity &&
      rtVariance == that.rtVariance &&
      peakels == that.peakels;
}

bool Feature::operator!=(const Feature& that) const
{
    return !(*this==that);

}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Feature& feature)
{
    XMLWriter writer(os);
    feature.write(writer);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, Feature& feature)
{
    feature.read(is);
    return is;
}

void FeatureFile::write(pwiz::minimxml::XMLWriter& xmlWriter) const

{
    XMLWriter::Attributes attributes;
    xmlWriter.startElement("features",attributes);
    vector<Feature>::const_iterator feat_it = features.begin();
    for(; feat_it != features.end(); ++feat_it)
        feat_it->write(xmlWriter);
    xmlWriter.endElement();
    return;
}


struct HandlerFeatureFile : public SAXParser::Handler
{
    HandlerFeatureFile(FeatureFile* _featureFile = 0) : featureFile(_featureFile){}
    FeatureFile* featureFile;

    
    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        
      if (name == "features")
        {
            return Handler::Status::Ok;
        }

      else if (name=="feature")
        {
            // TODO implement getAttribute(attributes,"count", _peakelCount);
            Feature feature;
        
            featureFile->features.push_back(feature);
            _handlerFeature.feature = (&(featureFile->features.back()));
            return Status(Status::Delegate, &_handlerFeature);

        }
      
      else
          {
              throw runtime_error(("Unexpected element in FeatureFile: " + name).c_str());
              return Handler::Status::Done;
          }

    }



private:

    HandlerFeature _handlerFeature;


};

void FeatureFile::read(istream& is)
{
    
  HandlerFeatureFile handlerFeatureFile(this);   
  parse(is,handlerFeatureFile);
   
  return;
}

} // namespace peakdata 
} // namespace data 
} // namespace pwiz


