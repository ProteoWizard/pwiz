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
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "boost/lexical_cast.hpp"
#include <complex>
#include <iterator>


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
:   mz(0), intensity(0), area(0), error(0),
    frequency(0), phase(0), decay(0) 
{}


bool Peak::operator==(const Peak& that) const
{
    return mz == that.mz &&
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
    attributes.push_back(make_pair("intensity", lexical_cast<string>(intensity)));
    attributes.push_back(make_pair("area", lexical_cast<string>(area)));
    attributes.push_back(make_pair("error", lexical_cast<string>(error)));
    attributes.push_back(make_pair("frequency", lexical_cast<string>(frequency)));
    attributes.push_back(make_pair("phase", lexical_cast<string>(phase)));
    attributes.push_back(make_pair("decay", lexical_cast<string>(decay)));
    writer.startElement("peak", attributes, XMLWriter::EmptyElement);
}


struct HandlerPeak : public SAXParser::Handler
{
    Peak* peak;
    HandlerPeak(){}
    HandlerPeak(Peak* _peak) : peak(_peak) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "peak")
            throw runtime_error(("[HandlerPeak] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "mz", peak->mz);
        getAttribute(attributes, "intensity", peak->intensity);
        getAttribute(attributes, "area", peak->area);
        getAttribute(attributes, "error", peak->error);
        getAttribute(attributes, "frequency", peak->frequency);
        getAttribute(attributes, "phase", peak->phase);
        getAttribute(attributes, "decay", peak->decay);


        return Status::Ok;
    }
};

void Peak::read(istream& is)
{
    HandlerPeak handler(this);
    SAXParser::parse(is, handler);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Peak& peak)
{
    os << "<"
       << peak.mz << ","
       << peak.intensity << ","
       << peak.area << ","
       << peak.error << ","
       << peak.frequency << ","
       << peak.phase << ","
       << peak.decay << ">";

    return os;
}



//
// PeakFamily
//


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakFamily& peakFamily)
{
    os << "peakFamily ("
       << "mzMonoisotopic:" << peakFamily.mzMonoisotopic << " "
       << "charge:" << peakFamily.charge << " "
       << "score:" << peakFamily.score << " "
       << "peaks:" << peakFamily.peaks.size() << ")\n"; 

    copy(peakFamily.peaks.begin(), peakFamily.peaks.end(), ostream_iterator<Peak>(os, "\n")); 
    return os;
}

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
  HandlerPeakFamily(){}
  HandlerPeakFamily(PeakFamily* _peakFamily) : peakFamily(_peakFamily){}

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
  unsigned int _peaksCount;
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



//
// Scan 
//


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Scan& scan)
{
  os   << "index: " << scan.index 
       << "scan (#" << scan.nativeID
       << " rt:" << scan.retentionTime
       << " T:" << scan.observationDuration
       << " A:" << scan.calibrationParameters.A
       << " B:" << scan.calibrationParameters.B << ")\n";
    copy(scan.peakFamilies.begin(), scan.peakFamilies.end(), ostream_iterator<PeakFamily>(os, "")); 
    return os;
}

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
  HandlerScan(){}
  HandlerScan(Scan* _scan) : scan(_scan){}

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
  unsigned int _peakFamilyCount;
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
  HandlerParameter(){}
  HandlerParameter(Software::Parameter* _parameter) : parameter(_parameter){}

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
  HandlerSoftware(){}
  HandlerSoftware(Software* _software) : software(_software) {}

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
  unsigned int _parameterCount;
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
  unsigned int _scanCount;

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

} // namespace peakdata
} // namespace data
} // namespace pwiz


////////////////////
/////  below here is old stuff
////////////////////

//#include "util_old/MinimXML.hpp"

// note: boost/archive headers must precede boost/serialization headers
// (as of Boost v1.33.1)
#include "boost/archive/xml_oarchive.hpp"
#include "boost/archive/xml_iarchive.hpp"
#include "boost/serialization/vector.hpp"


namespace boost {
namespace serialization {


template <typename Archive>
void serialize(Archive& ar, pwiz::data::peakdata::Peak& peak, const unsigned int version)
{
    ar & make_nvp("mz", peak.mz);
    ar & make_nvp("frequency", peak.frequency);
    ar & make_nvp("intensity", peak.intensity);
    ar & make_nvp("phase", peak.phase);
    ar & make_nvp("decay", peak.decay);
    ar & make_nvp("error", peak.error);
    ar & make_nvp("area", peak.area);
}


template <typename Archive>
void serialize(Archive& ar, pwiz::data::peakdata::PeakFamily& peakFamily, const unsigned int version)
{
    ar & make_nvp("mzMonoisotopic", peakFamily.mzMonoisotopic);
    ar & make_nvp("charge", peakFamily.charge);
    ar & make_nvp("peaks", peakFamily.peaks);
}


template <typename Archive>
void serialize(Archive& ar, pwiz::data::CalibrationParameters& cp, const unsigned int version)
{
    ar & make_nvp("A", cp.A);
    ar & make_nvp("B", cp.B);
}


template <typename Archive>
void serialize(Archive& ar, pwiz::data::peakdata::Scan& scan, const unsigned int version)
{
    ar & make_nvp("nativeID", scan.nativeID);
    ar & make_nvp("retentionTime", scan.retentionTime);
    ar & make_nvp("observationDuration", scan.observationDuration);
    ar & make_nvp("calibrationParameters", scan.calibrationParameters);
    ar & make_nvp("peakFamilies", scan.peakFamilies);
}


template <typename Archive>
void serialize(Archive& ar, pwiz::data::peakdata::Software& software, const unsigned int version)
{
    ar & make_nvp("name", software.name);
    ar & make_nvp("version", software.version);
    ar & make_nvp("source", software.source);

    // don't know why archiving "map" chokes
    //ar & make_nvp("parameters", software.parameters);
}


template <typename Archive>
void serialize(Archive& ar, pwiz::data::peakdata::PeakData& pd, const unsigned int version)
{
    ar & make_nvp("sourceFilename", pd.sourceFilename);
    ar & make_nvp("software", pd.software);
    ar & make_nvp("scans", pd.scans);
}

} // namespace serialization
} // namespace boost


namespace pwiz {
namespace data {
namespace peakdata {


using namespace std;
//using namespace pwiz::util;


PWIZ_API_DECL void PeakFamily::printSimple(std::ostream& os) const
{
    if (peaks.empty())
        os << 0 << " " << complex<double>(0.) << " " << 0 << endl;
    else
        os << peaks[0].frequency << " "
           << polar(peaks[0].intensity, peaks[0].phase) << " "
           << charge << endl; 
}


PWIZ_API_DECL void Scan::printSimple(std::ostream& os) const
{
    for (vector<PeakFamily>::const_iterator it=peakFamilies.begin(); it!=peakFamilies.end(); ++it)
        it->printSimple(os);
}


using boost::serialization::make_nvp;
using boost::archive::xml_iarchive;
using boost::archive::xml_oarchive;


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakData& pd)
{
    xml_oarchive oa(os);
    oa << make_nvp("peakdata", pd);
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, PeakData& pd)
{
    xml_iarchive ia(is);
    ia >> make_nvp("peakdata", pd);
    return is;
}


} // namespace peakdata 
} // namespace data 
} // namespace pwiz



