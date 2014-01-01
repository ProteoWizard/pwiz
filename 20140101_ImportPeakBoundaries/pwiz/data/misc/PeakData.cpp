//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include <complex>
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace data {
namespace peakdata {


using namespace pwiz::minimxml;
using namespace pwiz::math;
using namespace minimxml::SAXParser;


//
// Peak
//


PWIZ_API_DECL Peak::Peak(double _mz, double _retentionTime)
: id(0),  mz(_mz), retentionTime(_retentionTime), intensity(0), area(0), error(0)
{}


PWIZ_API_DECL double Peak::getAttribute(Attribute attribute) const
{
    Attributes::const_iterator it = attributes.find(attribute);
    if (it == attributes.end()) 
        throw runtime_error("[Peak::getAttribute] Attribute not found.");
    return it->second;
}


PWIZ_API_DECL bool Peak::operator==(const Peak& that) const
{
    bool result = (id == that.id &&
                   mz == that.mz &&
                   retentionTime == that.retentionTime &&
                   intensity == that.intensity &&
                   area == that.area &&
                   error == that.error &&
                   data == that.data);

    if (!result) return false;

    // check attributes

    for (Attributes::const_iterator it=attributes.begin(); it!=attributes.end(); ++it)
    {
        Attributes::const_iterator jt=that.attributes.find(it->first);
        if (jt==that.attributes.end() || jt->second!=it->second) return false;
    }

    for (Attributes::const_iterator it=that.attributes.begin(); it!=that.attributes.end(); ++it)
    {
        Attributes::const_iterator jt=attributes.find(it->first);
        if (jt==attributes.end() || jt->second!=it->second) return false;
    }

    return true;
}


PWIZ_API_DECL bool Peak::operator!=(const Peak& that) const
{
    return !(*this==that);
}


namespace {

struct AttributeNameEntry
{
    Peak::Attribute attribute;
    const char* name;
};


AttributeNameEntry attributeNameTable_[] = 
{
    {Peak::Attribute_Frequency, "frequency"},
    {Peak::Attribute_Phase, "phase"},
    {Peak::Attribute_Decay, "decay"}
};


const size_t attributeNameTableSize_ = sizeof(attributeNameTable_)/sizeof(AttributeNameEntry);


map<Peak::Attribute,string> attributeNameMap_;
map<string,Peak::Attribute> nameAttributeMap_;


void initializeAttributeNameMaps()
{
    attributeNameMap_.clear();
    nameAttributeMap_.clear();

    for (const AttributeNameEntry* p=attributeNameTable_;
         p!=attributeNameTable_+attributeNameTableSize_; ++p)
    {
        attributeNameMap_[p->attribute] = p->name;
        nameAttributeMap_[p->name] = p->attribute;
    }
}


string attributeToString(Peak::Attribute attribute)
{
    if (attributeNameMap_.empty()) initializeAttributeNameMaps(); 
    
    map<Peak::Attribute,string>::const_iterator it=attributeNameMap_.find(attribute);
    if (it == attributeNameMap_.end())
        throw runtime_error("[PeakData::attributeToString()] Attribute not found.");

    return it->second;
}


Peak::Attribute stringToAttribute(string name)
{
    if (nameAttributeMap_.empty()) initializeAttributeNameMaps(); 
    
    map<string,Peak::Attribute>::const_iterator it=nameAttributeMap_.find(name);
    if (it == nameAttributeMap_.end())
        throw runtime_error(("[PeakData::stringToAttribute()] Unknown attribute: " + name).c_str());

    return it->second;
}


} // namespace


PWIZ_API_DECL void Peak::write(minimxml::XMLWriter& writer) const
{
    XMLWriter::Attributes xmlAttributes;
    xmlAttributes.push_back(make_pair("id", lexical_cast<string>(id)));
    xmlAttributes.push_back(make_pair("mz", lexical_cast<string>(mz)));
    xmlAttributes.push_back(make_pair("retentionTime", lexical_cast<string>(retentionTime)));
    xmlAttributes.push_back(make_pair("intensity", lexical_cast<string>(intensity)));
    xmlAttributes.push_back(make_pair("area", lexical_cast<string>(area)));
    xmlAttributes.push_back(make_pair("error", lexical_cast<string>(error)));

    for (Attributes::const_iterator it=attributes.begin(); it!=attributes.end(); ++it)
        xmlAttributes.push_back(make_pair(attributeToString(it->first), 
                                          lexical_cast<string>(it->second)));

    if (data.empty())
    {
        writer.startElement("peak", xmlAttributes, XMLWriter::EmptyElement);
    }
    else
    {
        writer.startElement("peak", xmlAttributes);
        writer.startElement("data");
        ostringstream oss;
        copy(data.begin(), data.end(), ostream_iterator<OrderedPair>(oss, " "));
        writer.characters(oss.str());
        writer.endElement();
        writer.endElement();
    }
}


SAXParser::Handler::Status HandlerPeak::startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
{
    if (!peak)
        throw runtime_error("[PeakData::HandlerPeak::startElement()]  Null peak.");

    if (name == "peak")
    {
        for (Attributes::attribute_list::const_iterator it=attributes.begin(); it!=attributes.end(); ++it)
        {
            if (it->matchName("id")) peak->id = lexical_cast<int>(it->getValue());
            else if (it->matchName("mz")) peak->mz = lexical_cast<double>(it->getValue(NoXMLUnescape)); 
            else if (it->matchName("retentionTime")) peak->retentionTime = lexical_cast<double>(it->getValue(NoXMLUnescape));
            else if (it->matchName("intensity")) peak->intensity = lexical_cast<double>(it->getValue(NoXMLUnescape));
            else if (it->matchName("area")) peak->area = lexical_cast<double>(it->getValue(NoXMLUnescape));
            else if (it->matchName("error")) peak->error = lexical_cast<double>(it->getValue(NoXMLUnescape));
            else
            {
                Peak::Attribute a = stringToAttribute(it->getName());
                peak->attributes[a] = lexical_cast<double>(it->getValue(NoXMLUnescape)); 
            }
        }

        return Status::Ok;
    }
    else if (name == "data")
    {
        return Status::Ok;
    }

    throw runtime_error(("[HandlerPeak] Unexpected element name: " + name).c_str());
}
    

SAXParser::Handler::Status HandlerPeak::characters(const SAXParser::saxstring& text,
                                                   stream_offset position)
{
    if (!peak)
        throw runtime_error("[PeakData::HandlerPeak::characters()]  Null peak.");

    peak->data.clear();
    istringstream iss(text.c_str());
    copy(istream_iterator<OrderedPair>(iss), istream_iterator<OrderedPair>(), back_inserter(peak->data));
    return Status::Ok;
}


PWIZ_API_DECL void Peak::read(istream& is)
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


PWIZ_API_DECL void PeakFamily::write(XMLWriter& writer) const
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

PWIZ_API_DECL void PeakFamily::read(istream& is)
{

    HandlerPeakFamily handlerPeakFamily(this);
    SAXParser::parse(is, handlerPeakFamily);


}


PWIZ_API_DECL bool PeakFamily::operator==(const PeakFamily& that) const
{

    return mzMonoisotopic == that.mzMonoisotopic &&
      charge == that.charge &&
      score == that.score&&
      peaks == that.peaks;

}

PWIZ_API_DECL bool PeakFamily::operator!=(const PeakFamily& that) const
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


PWIZ_API_DECL void Scan::write(XMLWriter& writer) const
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


PWIZ_API_DECL void Scan::read(istream& is)
{
    HandlerScan handlerScan(this);
    parse(is, handlerScan);

}


PWIZ_API_DECL bool Scan::operator==(const Scan& scan) const
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


PWIZ_API_DECL bool Scan::operator!=(const Scan& scan) const
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


PWIZ_API_DECL void Software::Parameter::write(XMLWriter& writer) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("name", boost::lexical_cast<string>(name)));
    attributes.push_back(make_pair("value", boost::lexical_cast<string>(value)));
    writer.startElement("parameter", attributes, XMLWriter::EmptyElement);
}


PWIZ_API_DECL void Software::write(XMLWriter& writer) const

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


PWIZ_API_DECL void Software::Parameter::read(istream& is) 
{
    HandlerParameter handlerParameter(this);
    parse(is, handlerParameter);

}


PWIZ_API_DECL void Software::read(istream& is)
{
    HandlerSoftware handlerSoftware(this);
    parse(is, handlerSoftware);

}


PWIZ_API_DECL bool Software::Parameter::operator==(const Software::Parameter& that) const
{
    return name == that.name &&
      value == that.value;

}


PWIZ_API_DECL bool Software::Parameter::operator!=(const Software::Parameter& that) const
{
    return !(*this == that);

}


PWIZ_API_DECL bool Software::operator==(const Software& that) const
{
  return name == that.name &&
    version == that.version &&
    source == that.source &&
    parameters == that.parameters;

}


PWIZ_API_DECL bool Software::operator!=(const Software& that) const
{
  return !(*this == that);

}


//
// PeakData
//


PWIZ_API_DECL void PeakData::write(XMLWriter& writer) const
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


PWIZ_API_DECL void PeakData::read(istream& is)
{
    HandlerPeakData handlerPeakData(this);
    SAXParser::parse(is, handlerPeakData);
}


PWIZ_API_DECL bool PeakData::operator==(const PeakData& that) const
{
    return sourceFilename == that.sourceFilename &&
      software == that.software &&
      scans == that.scans;
}


PWIZ_API_DECL bool PeakData::operator!=(const PeakData& that) const
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


PWIZ_API_DECL Peakel::Peakel()
:   mz(0),
    retentionTime(0),
    maxIntensity(0),
    totalIntensity(0),
    mzVariance(0)
{}


PWIZ_API_DECL Peakel::Peakel(const Peak& peak)
:   mz(peak.mz),
    retentionTime(peak.retentionTime),
    maxIntensity(0),
    totalIntensity(0),
    mzVariance(0)
{
    peaks.push_back(peak);
}


PWIZ_API_DECL void Peakel::calculateMetadata()
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

    // ignore peakels of total intensity zero in rt calculations to avoid div by zero
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


PWIZ_API_DECL double Peakel::retentionTimeMin() const
{
    return peaks.empty() ? retentionTime : peaks.front().retentionTime;
}


PWIZ_API_DECL double Peakel::retentionTimeMax() const
{
    return peaks.empty() ? retentionTime : peaks.back().retentionTime;
}


PWIZ_API_DECL void Peakel::write(pwiz::minimxml::XMLWriter& xmlWriter) const
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


PWIZ_API_DECL void Peakel::read(istream& is)
{
    HandlerPeakel handlerPeakel(this);
    parse(is, handlerPeakel);
}


PWIZ_API_DECL bool Peakel::operator==(const Peakel& that) const
{
    return mz == that.mz &&
      retentionTime == that.retentionTime &&
      maxIntensity == that.maxIntensity &&
      totalIntensity == that.totalIntensity &&
      mzVariance == that.mzVariance &&
      peaks == that.peaks;

}


PWIZ_API_DECL bool Peakel::operator!=(const Peakel& that) const
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


///
/// Feature
///


PWIZ_API_DECL Feature::Feature()
:   mz(0), retentionTime(0), charge(0), totalIntensity(0), rtVariance(0), score(0), error(0)
{}


PWIZ_API_DECL void Feature::calculateMetadata()
{
    // wipe out any metadata that may have been set
    mz = 0;
    retentionTime = 0;
    rtVariance = 0;
    totalIntensity = 0;
    score = 0;
    error = 0;

    // calculate metadata of each peakel
    
    vector<PeakelPtr>::iterator calc_pkl_it = peakels.begin();
    for(; calc_pkl_it != peakels.end(); ++calc_pkl_it)
        {   
            (*calc_pkl_it)->calculateMetadata();
        }

    // write mz (mz of first peakel)
    mz = peakels.front()->mz;

    // calculate totalIntensity and maxIntensity 
    vector<PeakelPtr>::iterator calc_intensity_it = peakels.begin();
    for(; calc_intensity_it != peakels.end(); ++calc_intensity_it)
        {
            totalIntensity += (*calc_intensity_it)->totalIntensity;
        }

    // calculate retentionTime (weighted mean of peakel retentionTimes)
    vector<PeakelPtr>::iterator calc_mean_it = peakels.begin();
    for(; calc_mean_it != peakels.end(); ++calc_mean_it)
        {
            retentionTime += ((*calc_mean_it)->retentionTime) * ((*calc_mean_it)->totalIntensity)/totalIntensity; //weighted average
        }
    
    // calculate rtVariance ( variance of peakel retentionTimes)
    vector<PeakelPtr>::iterator calc_var_it = peakels.begin();
    for(; calc_var_it != peakels.end(); ++calc_var_it)
        {
            rtVariance += ((*calc_var_it)->retentionTime - retentionTime) * ((*calc_var_it)->retentionTime - retentionTime);
        }

    rtVariance = rtVariance / peakels.size();

    return;
}


namespace {
const int maxPeakelsToCheck_ = 2;
} // namespace


PWIZ_API_DECL double Feature::retentionTimeMin() const
{
    double result = retentionTime;
    
    vector<PeakelPtr>::const_iterator begin = peakels.begin();
    vector<PeakelPtr>::const_iterator end = peakels.end();
    if (end-begin>maxPeakelsToCheck_) end = begin+maxPeakelsToCheck_;

    for (vector<PeakelPtr>::const_iterator it=begin; it!=end; ++it)
    {
        double min = (*it)->retentionTimeMin();
        if (min < result) result = min;
    }
    
    return result;
}


PWIZ_API_DECL double Feature::retentionTimeMax() const
{
    double result = retentionTime;
    
    vector<PeakelPtr>::const_iterator begin = peakels.begin();
    vector<PeakelPtr>::const_iterator end = peakels.end();
    if (end-begin>maxPeakelsToCheck_) end = begin+maxPeakelsToCheck_;

    for (vector<PeakelPtr>::const_iterator it=begin; it!=end; ++it)
    {
        double max = (*it)->retentionTimeMax();
        if (max > result) result = max;
    }
    
    return result;
}


PWIZ_API_DECL void Feature::write(pwiz::minimxml::XMLWriter& xmlWriter) const
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", boost::lexical_cast<string>(id)));
    attributes.push_back(make_pair("mz", boost::lexical_cast<string>(mz)));
    attributes.push_back(make_pair("retentionTime", boost::lexical_cast<string>(retentionTime)));
    attributes.push_back(make_pair("charge", boost::lexical_cast<string>(charge)));
    attributes.push_back(make_pair("totalIntensity", boost::lexical_cast<string>(totalIntensity)));
    attributes.push_back(make_pair("rtVariance", boost::lexical_cast<string>(rtVariance)));
    attributes.push_back(make_pair("score", boost::lexical_cast<string>(score)));
    attributes.push_back(make_pair("error", boost::lexical_cast<string>(error)));

    xmlWriter.startElement("feature",attributes);

    XMLWriter::Attributes attributes_pkl;
    attributes_pkl.push_back(make_pair("count", boost::lexical_cast<string>(peakels.size())));
  
    xmlWriter.startElement("peakels",attributes_pkl);
  
    vector<PeakelPtr>::const_iterator pkl_it = peakels.begin();
    for(; pkl_it != peakels.end(); ++pkl_it)
        (*pkl_it)->write(xmlWriter);

    xmlWriter.endElement();
    xmlWriter.endElement();
}

SAXParser::Handler::Status HandlerFeature::startElement(const string& name, const Attributes& attributes, stream_offset position)
{
      if (name == "feature")
        {
            getAttribute(attributes,"id", feature->id);
            getAttribute(attributes,"mz", feature->mz);
            getAttribute(attributes,"retentionTime", feature->retentionTime);
            getAttribute(attributes,"charge", feature->charge);
            getAttribute(attributes,"totalIntensity", feature->totalIntensity);
            getAttribute(attributes,"rtVariance", feature->rtVariance);
            getAttribute(attributes,"score", feature->score);
            getAttribute(attributes,"error", feature->error);

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
                feature->peakels.push_back(PeakelPtr(new Peakel));
                _handlerPeakel.peakel = feature->peakels.back().get();
                return Status(Status::Delegate, &_handlerPeakel);
            }
        }

      if (_peakelCount != feature->peakels.size())
        {
            throw runtime_error("[HandlerFeature]: <peakels count> != feature->peakels.size()");
            return Status::Done;
        }
}

PWIZ_API_DECL Feature::Feature(const MSIHandler::Record& record) 
{
    mz = record.mz;
    retentionTime = record.time;
    charge = record.charge;
    totalIntensity = record.intensity;	
    rtVariance = 0;
    score = 0;
    error = 0;
}

PWIZ_API_DECL void Feature::read(istream& is)
{
    HandlerFeature handlerFeature(this);
    parse(is, handlerFeature);
}


PWIZ_API_DECL bool Feature::operator==(const Feature& that) const
{
    bool result = (id == that.id &&
      mz == that.mz &&
      retentionTime == that.retentionTime &&
      charge == that.charge &&
      totalIntensity == that.totalIntensity &&
      rtVariance == that.rtVariance &&
      score == that.score &&
      error == that.error);

    if (!result) return false;
    if (peakels.size() != that.peakels.size()) return false;

    for (vector<PeakelPtr>::const_iterator it=peakels.begin(), jt=that.peakels.begin();
         it!=peakels.end(); ++it, ++jt)
        if (**it != **jt) return false;

    return true;
}


PWIZ_API_DECL bool Feature::operator!=(const Feature& that) const
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

PWIZ_API_DECL void FeatureFile::write(pwiz::minimxml::XMLWriter& xmlWriter) const

{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", boost::lexical_cast<string>(features.size())));

    xmlWriter.startElement("features",attributes);
    vector<FeaturePtr>::const_iterator feat_it = features.begin();
    for(; feat_it != features.end(); ++feat_it)
        (*feat_it)->write(xmlWriter);
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
            featureFile->features.push_back(FeaturePtr(new Feature));
            _handlerFeature.feature = featureFile->features.back().get();
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


PWIZ_API_DECL void FeatureFile::read(istream& is)
{
    
  HandlerFeatureFile handlerFeatureFile(this);   
  parse(is,handlerFeatureFile);
   
  return;
}

} // namespace peakdata 
} // namespace data 
} // namespace pwiz


