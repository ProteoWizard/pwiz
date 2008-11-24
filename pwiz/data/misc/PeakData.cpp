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
    HandlerPeak(Peak* _peak = 0) : peak(_peak) {}

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

struct HandlerPeakData : public SAXParser::Handler
{
  PeakData* peakData;
  HandlerPeakData(PeakData* _peakData) : peakData(_peakData) {}

  virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
  {
    if (name != "PeakData")
      throw runtime_error(("[HandlerPeakData] Unexpected element name: " + name).c_str());
    getAttribute(attributes, "sourceFilename", peakData->sourceFilename);

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


//
// PeakData 
//

} // namespace pwiz
} // namespace data
} // namespace peakdata


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


namespace {

/*
void writeNameValuePair(MinimXML::Writer& writer, const string& name, const string& value)
{
    writer.setStyleFlags(MinimXML::ElementOnSingleLine);
    writer.startElement(name);
    writer.data(value.c_str());
    writer.endElement();
    writer.setStyleFlags(0);
}

void writeNameValuePair(MinimXML::Writer& writer, const string& name, double value)
{
    ostringstream oss;
    oss << value;
    writeNameValuePair(writer, name, oss.str());
}
*/
/*
void writeCalibrationParameters(MinimXML::Writer& writer, const CalibrationParameters& cp)
{
    writer.pushAttribute("A", cp.A); 
    writer.pushAttribute("B", cp.B); 
    writer.startAndEndElement("calibrationParameters");
}

void writePeak(MinimXML::Writer& writer, const Peak& peak)
{
    writer.pushAttribute("mz", peak.mz);
    writer.pushAttribute("intensity", peak.intensity);
    writer.pushAttribute("area", peak.area);
    writer.pushAttribute("error", peak.error);
    writer.pushAttribute("frequency", peak.frequency);
    writer.pushAttribute("phase", peak.phase);
    writer.pushAttribute("decay", peak.decay);
    writer.startAndEndElement("peak");
}

void writePeakFamily(MinimXML::Writer& writer, const PeakFamily& peakFamily)
{
    writer.pushAttribute("mzMonoisotopic", peakFamily.mzMonoisotopic);
    writer.pushAttribute("charge", (long)peakFamily.charge);
    writer.startElement("peakFamily");

    writer.pushAttribute("count", (long)peakFamily.peaks.size());
    writer.startElement("peaks");
    for (vector<Peak>::const_iterator it=peakFamily.peaks.begin(); it!=peakFamily.peaks.end(); ++it)
        writePeak(writer, *it);
    writer.endElement();

    writer.endElement();
}

void writeScan(MinimXML::Writer& writer, const Scan& scan)
{
    writer.pushAttribute("index", scan.index);
    writer.pushAttribute("nativeID", (long)scan.nativeID);
    writer.pushAttribute("retentionTime", scan.retentionTime);
    writer.pushAttribute("observationDuration", scan.observationDuration);
    writer.startElement("scan");

    writeCalibrationParameters(writer, scan.calibrationParameters);

    writer.pushAttribute("count", (long)scan.peakFamilies.size());
    writer.startElement("peakFamilies");
    for (vector<PeakFamily>::const_iterator it=scan.peakFamilies.begin(); it!=scan.peakFamilies.end(); ++it)
        writePeakFamily(writer, *it);
    writer.endElement();

    writer.endElement();
}

void writeSoftware(MinimXML::Writer& writer, const Software& software)
{
    writer.pushAttribute("name", software.name);
    writer.pushAttribute("version", software.version);
    writer.pushAttribute("source", software.source);
    writer.setStyleFlags(MinimXML::AttributesOnMultipleLines);
    writer.startElement("software");
    writer.setStyleFlags(0);

    writer.pushAttribute("count", (long)software.parameters.size());
    writer.startElement("parameters");
    for (Software::Parameters::const_iterator it=software.parameters.begin();
         it!=software.parameters.end(); ++it)
    {
        writer.pushAttribute("name", it->first);
        writer.pushAttribute("value", it->second);
        writer.startAndEndElement("parameter");
    }
    writer.endElement();

    writer.endElement();
}
*/
} // namespace


void PeakData::write(XMLWriter& writer) const
{

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("sourceFilename",sourceFilename));

    writer.startElement("PeakData",attributes);
    writer.endElement();

/*
    auto_ptr<MinimXML::Writer> writer(MinimXML::Writer::create(os));
    writer->prolog();

    ostringstream versionString;
    versionString << PeakDataFormatVersion_Major << "." << PeakDataFormatVersion_Minor;

    writer->pushAttribute("version", versionString.str());
    writer->pushAttribute("sourceFilename", sourceFilename);
    writer->setStyleFlags(MinimXML::AttributesOnMultipleLines);
    writer->startElement("peakdata");
    writer->setStyleFlags(0);

    writeSoftware(*writer, software);

    // TODO: write generic writer for containers
    writer->pushAttribute("count", (long)scans.size());
    writer->startElement("scans");
    for (vector<Scan>::const_iterator it=scans.begin(); it!=scans.end(); ++it)
        writeScan(*writer, *it);
    writer->endElement();

    writer->endElement();
*/
}

void PeakData::read(istream& is)
{
  HandlerPeakData handlerPeakData(this);
  SAXParser::parse(is, handlerPeakData);
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



