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


#define PWIZ_SOURCE

#include "Serializer_mzML.hpp"
#include "IO.hpp"
#include "SpectrumList_mzML.hpp"
#include "ChromatogramList_mzML.hpp"
#include "SHA1OutputObserver.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#ifndef WITHOUT_MZMLB
#include "mzmlb/Connection_mzMLb.hpp"
using namespace pwiz::msdata::mzmlb;
#endif

namespace pwiz {
namespace msdata {


using minimxml::XMLWriter;
using boost::iostreams::stream_offset;
using namespace pwiz::util;
using namespace pwiz::minimxml;


class Serializer_mzML::Impl
{
    public:

    Impl(const Config& config)
    :   config_(config)
    {}

    void write(ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
               bool useWorkerThreads, bool continueOnError) const;

    void read(shared_ptr<istream> is, MSData& msd) const;

    private:
    Config config_; 
};


namespace {

void writeSpectrumIndex(XMLWriter& xmlWriter, 
                const SpectrumListPtr& spectrumListPtr,
                const vector<stream_offset>& positions)
{
    XMLWriter::Attributes indexAttributes;
    indexAttributes.push_back(make_pair("name", "spectrum"));        
    xmlWriter.startElement("index", indexAttributes);
    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
    if (spectrumListPtr.get() && spectrumListPtr->size() > 0)
    {

        if (spectrumListPtr->size() != positions.size())
            throw runtime_error("[Serializer_mzML::writeSpectrumIndex()] Sizes differ.");

        int skipped = 0;
        for (unsigned int i=0; i<positions.size(); ++i)
        {
            if (positions[i] < 0)
            {
                ++skipped;
                continue;
            }

            SpectrumIdentity spectrum = spectrumListPtr->spectrumIdentity(i);
            if (skipped > 0)
                spectrum.index -= skipped;

            XMLWriter::Attributes attributes;
            attributes.push_back(make_pair("idRef", spectrum.id));
            if (!spectrum.spotID.empty())
                attributes.push_back(make_pair("spotID", spectrum.spotID));

            xmlWriter.startElement("offset", attributes);
            xmlWriter.characters(lexical_cast<string>(positions[i]));
            xmlWriter.endElement();
        }
    }
    xmlWriter.popStyle();
    xmlWriter.endElement(); 
}

void writeChromatogramIndex(XMLWriter& xmlWriter, 
                const ChromatogramListPtr& chromatogramListPtr,
                const vector<stream_offset>& positions)
{
    XMLWriter::Attributes indexAttributes;
    indexAttributes.push_back(make_pair("name", "chromatogram"));        
    xmlWriter.startElement("index", indexAttributes);
    xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);
    if (chromatogramListPtr.get() && chromatogramListPtr->size() > 0)
    {

        if (chromatogramListPtr->size() != positions.size())
            throw runtime_error("[Serializer_mzML::WriteChromatogramIndex()] sizes differ.");

        int skipped = 0;
        for (unsigned int i=0; i<positions.size(); ++i)
        {
            if (positions[i] < 0)
            {
                ++skipped;
                continue;
            }

            ChromatogramIdentity chromatogram = chromatogramListPtr->chromatogramIdentity(i);
            if (skipped > 0)
                chromatogram.index -= skipped;

            XMLWriter::Attributes Attributes;
            Attributes.push_back(make_pair("idRef", chromatogram.id));        

            xmlWriter.startElement("offset", Attributes);
            xmlWriter.characters(lexical_cast<string>(positions[i]));
            xmlWriter.endElement();
        }
    }
    xmlWriter.popStyle();
    xmlWriter.endElement(); 
}

} // namespace


void Serializer_mzML::Impl::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
    bool useWorkerThreads, bool continueOnError) const
{
    // instantiate XMLWriter

    SHA1OutputObserver sha1OutputObserver;
    XMLWriter::Config xmlConfig;
    xmlConfig.outputObserver = &sha1OutputObserver;
    XMLWriter xmlWriter(os, xmlConfig);

    string xmlData = "version=\"1.0\" encoding=\"utf-8\"";
    xmlWriter.processingInstruction("xml", xmlData);

    // <indexedmzML> start

#ifndef WITHOUT_MZMLB
    boost::iostreams::stream<Connection_mzMLb>* mzMLb_os = dynamic_cast<boost::iostreams::stream<Connection_mzMLb>*>(&os);
    if (config_.indexed && !mzMLb_os)
#else    
    if (config_.indexed)
#endif
    {
        XMLWriter::Attributes attributes; 
        attributes.push_back(make_pair("xmlns", "http://psi.hupo.org/ms/mzml"));
        attributes.push_back(make_pair("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"));
        attributes.push_back(make_pair("xsi:schemaLocation", "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.2_idx.xsd"));
        
        xmlWriter.startElement("indexedmzML", attributes);
        attributes.clear();
    }

    // <mzML>

    vector<stream_offset> spectrumPositions;
    vector<stream_offset> chromatogramPositions;
    BinaryDataEncoder::Config bdeConfig = config_.binaryDataEncoderConfig;
    bdeConfig.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian; // mzML always little endian
    IO::write(xmlWriter, msd, bdeConfig, &spectrumPositions, &chromatogramPositions, iterationListenerRegistry, useWorkerThreads, continueOnError);

    // don't write indexes if writing was cancelled
    if (iterationListenerRegistry && IterationListener::Status_Cancel == iterationListenerRegistry->broadcastUpdateMessage(IterationListener::UpdateMessage(0, 0, "writing indexes")))
        return;

    // <indexedmzML> end
    
#ifndef WITHOUT_MZMLB
    if (mzMLb_os)
    {
        if (!spectrumPositions.empty())
        {
            (*mzMLb_os)->write("mzML_spectrumIndex", &spectrumPositions[0], spectrumPositions.size());
            
            ostringstream idRefs;
            ostringstream spotIDs;
            bool hasSpotIDs = false;
 
            for (unsigned int i = 0; i < spectrumPositions.size() - 1; ++i)
            {
                const SpectrumIdentity& si = msd.run.spectrumListPtr->spectrumIdentity(i);
                idRefs.write(si.id.c_str(), si.id.size() + 1);

                if(!si.spotID.empty())
                {
                    spotIDs.write(si.spotID.c_str(), si.spotID.size() + 1);
                    hasSpotIDs = true;
                }
            }
            
            (*mzMLb_os)->write("mzML_spectrumIndex_idRef", idRefs.str().c_str(), idRefs.str().size());
            
            if (hasSpotIDs)
                (*mzMLb_os)->write("mzML_spectrumIndex_spotID", spotIDs.str().c_str(), spotIDs.str().size());
        }
        
        if (!chromatogramPositions.empty())
        {
            (*mzMLb_os)->write("mzML_chromatogramIndex", &chromatogramPositions[0], chromatogramPositions.size());
            
            ostringstream idRefs;
            for (unsigned int i = 0; i < chromatogramPositions.size() - 1; ++i)
            {
                const ChromatogramIdentity& ci = msd.run.chromatogramListPtr->chromatogramIdentity(i);
                idRefs.write(ci.id.c_str(), ci.id.size() + 1);
            }
            
            (*mzMLb_os)->write("mzML_chromatogramIndex_idRef", idRefs.str().c_str(), idRefs.str().size());
        }
    }
    else if (config_.indexed)
#else
    if (config_.indexed)
#endif
    {
        stream_offset indexListOffset = xmlWriter.positionNext();

        XMLWriter::Attributes attributes; 
        attributes.push_back(make_pair("count", "2"));
        xmlWriter.startElement("indexList", attributes);

        writeSpectrumIndex(xmlWriter, msd.run.spectrumListPtr, spectrumPositions);
        writeChromatogramIndex(xmlWriter, msd.run.chromatogramListPtr, chromatogramPositions);

        xmlWriter.endElement(); // indexList

        xmlWriter.pushStyle(XMLWriter::StyleFlag_InlineInner);

        xmlWriter.startElement("indexListOffset");
        xmlWriter.characters(lexical_cast<string>(indexListOffset));
        xmlWriter.endElement(); 
        
        xmlWriter.startElement("fileChecksum");
        xmlWriter.characters(sha1OutputObserver.hash());
        xmlWriter.endElement(); 

        xmlWriter.popStyle();

        xmlWriter.endElement(); // indexedmzML
    }
}


struct HandlerIndexedMZML : public SAXParser::Handler
{
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "indexedmzML")
            return Status::Done;

        throw runtime_error(("[SpectrumList_mzML::HandlerIndexedMZML] Unexpected element name: " + name).c_str());
    }
};


void Serializer_mzML::Impl::read(shared_ptr<istream> is, MSData& msd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_mzML::read()] Bad istream.");

    is->seekg(0);

    if (config_.indexed)
    {
        HandlerIndexedMZML handler;
        SAXParser::parse(*is, handler); 
    }

    IO::read(*is, msd, IO::IgnoreSpectrumList);
    Index_mzML_Ptr indexPtr(new Index_mzML(is, msd));
    msd.run.spectrumListPtr = SpectrumList_mzML::create(is, msd, indexPtr);
    msd.run.chromatogramListPtr = ChromatogramList_mzML::create(is, msd, indexPtr);
}


//
// Serializer_mzML
//


PWIZ_API_DECL Serializer_mzML::Serializer_mzML(const Config& config)
:   impl_(new Impl(config))
{}


PWIZ_API_DECL void Serializer_mzML::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
    bool useWorkerThreads, bool continueOnError) const
  
{
    return impl_->write(os, msd, iterationListenerRegistry, useWorkerThreads, continueOnError);
}


PWIZ_API_DECL void Serializer_mzML::read(shared_ptr<istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Serializer_mzML::Config& config)
{
    os << config.binaryDataEncoderConfig 
       << " indexed=\"" << boolalpha << config.indexed << "\"";
    return os;
}


} // namespace msdata
} // namespace pwiz


