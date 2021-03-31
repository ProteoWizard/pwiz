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

#include "IO.hpp"
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "SpectrumWorkerThreads.hpp"

namespace pwiz {
namespace msdata {
namespace IO {


using namespace minimxml;
using namespace minimxml::SAXParser;
using namespace util;


template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objectPtrs, const string& label)
{
    if (!objectPtrs.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.add("count", objectPtrs.size());
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it = objectPtrs.begin(); it != objectPtrs.end(); ++it)
            write(writer, **it);
        writer.endElement();
    }
}


//
// CV
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(cv.id));
    attributes.add("fullName", cv.fullName);
    attributes.add("version", cv.version);
    attributes.add("URI", cv.URI);
    writer.startElement("cv", attributes, XMLWriter::EmptyElement);
}


struct HandlerCV : public SAXParser::Handler
{
    CV* cv;
    HandlerCV(CV* _cv = 0) : cv(_cv) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "cv")
            throw runtime_error(("[IO::HandlerCV] Unexpected element name: " + name).c_str());
        decode_xml_id(getAttribute(attributes, "id", cv->id));
        getAttribute(attributes, "fullName", cv->fullName);
        getAttribute(attributes, "version", cv->version);
        getAttribute(attributes, "URI", cv->URI);
        return Status::Ok;
    }

};


PWIZ_API_DECL void read(std::istream& is, CV& cv)
{
    HandlerCV handler(&cv);
    SAXParser::parse(is, handler);
}
    

//
// UserParam
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const UserParam& userParam)
{
    XMLWriter::Attributes attributes;
    attributes.add("name", userParam.name);
    if (!userParam.value.empty())
        attributes.add("value", userParam.value);
    if (!userParam.type.empty())
        attributes.add("type", userParam.type);
    if (userParam.units != CVID_Unknown)
    {
        attributes.add("unitAccession", cvTermInfo(userParam.units).id);
        attributes.add("unitName", cvTermInfo(userParam.units).name);
    }

    writer.startElement("userParam", attributes, XMLWriter::EmptyElement);
}


struct HandlerUserParam : public SAXParser::Handler
{
    UserParam* userParam;
    HandlerUserParam(UserParam* _userParam = 0) : userParam(_userParam) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "userParam")
            throw runtime_error(("[IO::HandlerUserParam] Unexpected element name: " + name).c_str());

        if (!userParam)
            throw runtime_error("[IO::HandlerUserParam] Null userParam.");

        getAttribute(attributes, "name", userParam->name);
        getAttribute(attributes, "value", userParam->value);
        getAttribute(attributes, "type", userParam->type);

        string unitAccession;
        getAttribute(attributes, "unitAccession", unitAccession);
        if (!unitAccession.empty())
            userParam->units = cvTermInfo(unitAccession).cvid;

        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, UserParam& userParam)
{
    HandlerUserParam handler(&userParam);
    SAXParser::parse(is, handler);
}
    

//
// CVParam
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CVParam& cvParam)
{
    XMLWriter::Attributes attributes;
    attributes.add("cvRef", cvTermInfo(cvParam.cvid).prefix());
    attributes.add("accession", cvTermInfo(cvParam.cvid).id);
    attributes.add("name", cvTermInfo(cvParam.cvid).name);
    attributes.add("value", cvParam.value);
    if (cvParam.units != CVID_Unknown)
    {
        attributes.add("unitCvRef", cvTermInfo(cvParam.units).prefix());
        attributes.add("unitAccession", cvTermInfo(cvParam.units).id);
        attributes.add("unitName", cvTermInfo(cvParam.units).name);
    }
    writer.startElement("cvParam", attributes, XMLWriter::EmptyElement);
}


struct HandlerCVParam : public SAXParser::Handler
{
    CVParam* cvParam;

    HandlerCVParam(CVParam* _cvParam = 0) :  cvParam(_cvParam) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "cvParam")
            throw runtime_error(("[IO::HandlerCVParam] Unexpected element name: " + name).c_str());

        if (!cvParam)
            throw runtime_error("[IO::HandlerCVParam] Null cvParam."); 

        const char *accession = getAttribute(attributes, "accession",  NoXMLUnescape); 
        if (accession)
            cvParam->cvid = cvTermInfo(accession).cvid;

        getAttribute(attributes, "value", cvParam->value);

        const char *unitAccession = getAttribute(attributes, "unitAccession", NoXMLUnescape); 
        if (unitAccession)
            cvParam->units = cvTermInfo(unitAccession).cvid;

        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, CVParam& cvParam)
{
    HandlerCVParam handler(&cvParam);
    SAXParser::parse(is, handler);
}


//
// ParamContainer
//
//
// note: These are auxilliary functions to be called by ParamContainer subclasses
//


PWIZ_API_DECL void writeParamGroupRef(minimxml::XMLWriter& writer, const ParamGroup& paramGroup)
{
    XMLWriter::Attributes attributes;
    attributes.add("ref", paramGroup.id);
    writer.startElement("referenceableParamGroupRef", attributes, XMLWriter::EmptyElement);
}


PWIZ_API_DECL void writeParamContainer(minimxml::XMLWriter& writer, const ParamContainer& pc)
{
    for (vector<ParamGroupPtr>::const_iterator it=pc.paramGroupPtrs.begin(); 
         it!=pc.paramGroupPtrs.end(); ++it)
         writeParamGroupRef(writer, **it);

    for (vector<CVParam>::const_iterator it=pc.cvParams.begin(); 
         it!=pc.cvParams.end(); ++it)
         write(writer, *it);

    for (vector<UserParam>::const_iterator it=pc.userParams.begin(); 
         it!=pc.userParams.end(); ++it)
         write(writer, *it);
}


struct HandlerParamContainer : public SAXParser::Handler
{
    ParamContainer* paramContainer;

    HandlerParamContainer(ParamContainer* _paramContainer = 0)
    :   paramContainer(_paramContainer)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!paramContainer)
            throw runtime_error("[IO::HandlerParamContainer] Null paramContainer.");

        if (name == "cvParam")
        {
            paramContainer->cvParams.push_back(CVParam()); 
            handlerCVParam_.cvParam = &paramContainer->cvParams.back();
            return Status(Status::Delegate, &handlerCVParam_);
        }
        else if (name == "userParam")
        {
            paramContainer->userParams.push_back(UserParam()); 
            handlerUserParam_.userParam = &paramContainer->userParams.back();
            return Status(Status::Delegate, &handlerUserParam_);
        }
        else if (name == "referenceableParamGroupRef")
        {
            // note: placeholder
            string id;
            decode_xml_id(getAttribute(attributes, "ref", id));
            if (!id.empty())
                paramContainer->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup(id))); 
            return Status::Ok;
        }

        throw runtime_error(("[IO::HandlerParamContainer] Unknown element " + name).c_str()); 
    }

    private:

    HandlerCVParam handlerCVParam_;
    HandlerUserParam handlerUserParam_;
};

    
struct HandlerNamedParamContainer : public HandlerParamContainer
{
    const string name_;

    HandlerNamedParamContainer(const string& name, ParamContainer* paramContainer = 0)
    :   HandlerParamContainer(paramContainer), name_(name)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == name_)
            return Status::Ok;

        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


//
// ParamGroup
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ParamGroup& paramGroup)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(paramGroup.id));
    writer.startElement("referenceableParamGroup", attributes);
    writeParamContainer(writer, paramGroup);
    writer.endElement();
}


struct HandlerParamGroup : public HandlerParamContainer
{
    ParamGroup* paramGroup;

    HandlerParamGroup(ParamGroup* _paramGroup = 0) 
    :   paramGroup(_paramGroup)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!paramGroup)
            throw runtime_error("[IO::HandlerParamGroup] Null paramGroup.");

        if (name == "referenceableParamGroup")
        {
            decode_xml_id(getAttribute(attributes, "id", paramGroup->id));
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = paramGroup;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, ParamGroup& paramGroup)
{
    HandlerParamGroup handler(&paramGroup);
    SAXParser::parse(is, handler);
}


//
// FileContent
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FileContent& fc)
{
    writer.startElement("fileContent");
    writeParamContainer(writer, fc);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, FileContent& fc)
{
    HandlerNamedParamContainer handler("fileContent", &fc);
    SAXParser::parse(is, handler);
}


//
// SourceFile
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(sf.id));
    attributes.add("name", sf.name);
    attributes.add("location", sf.location);
    writer.startElement("sourceFile", attributes);
    writeParamContainer(writer, sf);
    writer.endElement();
}


void writeSourceFileRef(minimxml::XMLWriter& writer, const SourceFile& sourceFile)
{
    XMLWriter::Attributes attributes;
    attributes.add("ref", encode_xml_id_copy(sourceFile.id));
    writer.startElement("sourceFileRef", attributes, XMLWriter::EmptyElement);
}


struct HandlerSourceFile : public HandlerParamContainer
{
    SourceFile* sourceFile;

    HandlerSourceFile(SourceFile* _sourceFile = 0) 
    :   sourceFile(_sourceFile)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sourceFile)
            throw runtime_error("[IO::HandlerSourceFile] Null sourceFile.");

        if (name == "sourceFile")
        {
            decode_xml_id(getAttribute(attributes, "id", sourceFile->id));
            getAttribute(attributes, "name", sourceFile->name);
            getAttribute(attributes, "location", sourceFile->location);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = sourceFile;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, SourceFile& sf)
{
    HandlerSourceFile handler(&sf);
    SAXParser::parse(is, handler);
}


//
// Contact
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Contact& c)
{
    writer.startElement("contact");
    writeParamContainer(writer, c);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, Contact& c)
{
    HandlerNamedParamContainer handler("contact", &c);
    SAXParser::parse(is, handler);
}


//
// FileDescription 
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FileDescription& fd)
{
    writer.startElement("fileDescription");
    write(writer, fd.fileContent);
    
    writeList(writer, fd.sourceFilePtrs, "sourceFileList");

    for (vector<Contact>::const_iterator it=fd.contacts.begin(); 
         it!=fd.contacts.end(); ++it)
         write(writer, *it);
    writer.endElement();
}


struct HandlerFileDescription : public SAXParser::Handler
{
    FileDescription* fileDescription;

    HandlerFileDescription(FileDescription* _fileDescription = 0)
    :   fileDescription(_fileDescription),
        handlerFileContent_("fileContent"),
        handlerContact_("contact")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!fileDescription)
            throw runtime_error("[IO::HandlerFileDescription] Null fileDescription.");
        
        if (name == "fileDescription")
        {
            return Status::Ok;
        }
        else if (name == "fileContent")
        {
            handlerFileContent_.paramContainer = &fileDescription->fileContent;
            return Status(Status::Delegate, &handlerFileContent_);
        }
        else if (name == "sourceFileList")
        {
            return Status::Ok;
        }
        else if (name == "sourceFile")
        {
            fileDescription->sourceFilePtrs.push_back(SourceFilePtr(new SourceFile));
            handlerSourceFile_.sourceFile = fileDescription->sourceFilePtrs.back().get();
            return Status(Status::Delegate, &handlerSourceFile_);
        }
        else if (name == "contact")
        {
            fileDescription->contacts.push_back(Contact());
            handlerContact_.paramContainer = &fileDescription->contacts.back();
            return Status(Status::Delegate, &handlerContact_);
        }

        throw runtime_error(("[IO::HandlerFileDescription] Unknown element " + name).c_str()); 
    }

    private:

    HandlerNamedParamContainer handlerFileContent_;
    HandlerSourceFile handlerSourceFile_;
    HandlerNamedParamContainer handlerContact_;
};

 
PWIZ_API_DECL void read(std::istream& is, FileDescription& fd)
{
    HandlerFileDescription handler(&fd);
    SAXParser::parse(is, handler);
}


//
// Sample
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sample)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(sample.id));
    attributes.add("name", sample.name);
    writer.startElement("sample", attributes);
    writeParamContainer(writer, sample);
    writer.endElement();
}


struct HandlerSample : public HandlerParamContainer
{
    Sample* sample;

    HandlerSample(Sample* _sample = 0) 
    :   sample(_sample)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sample)
            throw runtime_error("[IO::HandlerSample] Null sample.");

        if (name == "sample")
        {
            decode_xml_id(getAttribute(attributes, "id", sample->id));
            getAttribute(attributes, "name", sample->name);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = sample;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Sample& sample)
{
    HandlerSample handler(&sample);
    SAXParser::parse(is, handler);
}


//
// Component (Source, Analyzer, Detector)
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Component& component)
{
    XMLWriter::Attributes attributes;
    attributes.add("order", component.order);
    switch (component.type)
    {
        case ComponentType_Source:
            writer.startElement("source", attributes);
            break;
        case ComponentType_Analyzer:
            writer.startElement("analyzer", attributes);
            break;
        case ComponentType_Detector:
            writer.startElement("detector", attributes);
            break;
        case ComponentType_Unknown:
            throw runtime_error("[IO::write] Unknown component type.");
    }
    writeParamContainer(writer, component);
    writer.endElement();
}

    
struct HandlerComponent : public HandlerParamContainer
{
    Component* component;

    HandlerComponent(Component* _component = 0)
    :   component(_component)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!component)
            throw runtime_error("[IO::HandlerComponent] Null component.");

        if (name=="source" ||
            name=="analyzer" ||
            name=="detector")
        {
            getAttribute(attributes, "order", component->order);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = component;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Component& component)
{
    HandlerComponent handler(&component);
    SAXParser::parse(is, handler);
}


//
// ComponentList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ComponentList& componentList)
{
    if (componentList.empty()) // componentList not required by schema
        return;

    int count = (int) componentList.size();

    XMLWriter::Attributes attributes;
    attributes.add("count", count);

    writer.startElement("componentList", attributes);
    for (size_t i=0; i < componentList.size(); ++i)
        write(writer, componentList[i]);
    writer.endElement();
}


struct HandlerComponentList : public SAXParser::Handler
{
    ComponentList* componentList;
    HandlerComponentList(ComponentList* _componentList = 0) : componentList(_componentList) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!componentList)
            throw runtime_error("[IO::HandlerComponentList] Null componentList.");

        if (name == "componentList")
        {
            return Status::Ok;
        }
        else if (name == "source")
        {
            componentList->push_back(Component(ComponentType_Source, 1));
            handlerComponent_.component = &componentList->back();
            return Status(Status::Delegate, &handlerComponent_);
        }
        else if (name == "analyzer")
        {
            componentList->push_back(Component(ComponentType_Analyzer, 1));
            handlerComponent_.component = &componentList->back();
            return Status(Status::Delegate, &handlerComponent_);
        }
        else if (name == "detector")
        {
            componentList->push_back(Component(ComponentType_Detector, 1));
            handlerComponent_.component = &componentList->back();
            return Status(Status::Delegate, &handlerComponent_);
        }

        throw runtime_error(("[IO::HandlerComponentList] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerComponent handlerComponent_;
};


PWIZ_API_DECL void read(std::istream& is, ComponentList& componentList)
{
    HandlerComponentList handler(&componentList);
    SAXParser::parse(is, handler);
}


//
// Software
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Software& software)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(software.id));
    attributes.add("version", software.version);
    writer.startElement("software", attributes);
    writeParamContainer(writer, software);
    writer.endElement();
}


struct HandlerSoftware : public HandlerParamContainer
{
    Software* software;

    HandlerSoftware(Software* _software = 0) : software(_software) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!software)
            throw runtime_error("[IO::HandlerSoftware] Null software.");

        if (name == "software")
        {
            decode_xml_id(getAttribute(attributes, "id", software->id));
            getAttribute(attributes, "version", software->version);
            return Status::Ok;
        }

        // mzML 1.0
        else if (version == 1 && name == "softwareParam")
        {
            string accession;
            getAttribute(attributes, "accession", accession);
            if (!accession.empty())
                software->set(cvTermInfo(accession).cvid);

            getAttribute(attributes, "version", software->version);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = software;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Software& software)
{
    HandlerSoftware handler(&software);
    SAXParser::parse(is, handler);
}


//
// InstrumentConfiguration
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const InstrumentConfiguration& instrumentConfiguration)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(instrumentConfiguration.id));
    writer.startElement("instrumentConfiguration", attributes);

    writeParamContainer(writer, instrumentConfiguration);
    if (!instrumentConfiguration.componentList.empty()) // optional element
        write(writer, instrumentConfiguration.componentList);

    if (instrumentConfiguration.softwarePtr.get())
    {
        attributes.clear();
        attributes.add("ref", encode_xml_id_copy(instrumentConfiguration.softwarePtr->id));
        writer.startElement("softwareRef", attributes, XMLWriter::EmptyElement);
    }

    writer.endElement();
}


struct HandlerInstrumentConfiguration : public HandlerParamContainer
{
    InstrumentConfiguration* instrumentConfiguration;
    HandlerInstrumentConfiguration(InstrumentConfiguration* _instrumentConfiguration = 0) 
    :   instrumentConfiguration(_instrumentConfiguration)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!instrumentConfiguration)
            throw runtime_error("[IO::HandlerInstrumentConfiguration] Null instrumentConfiguration.");

        if (name == "instrumentConfiguration")
        {
            decode_xml_id(getAttribute(attributes, "id", instrumentConfiguration->id));
            return Status::Ok;
        }
        else if (name == "componentList")
        {
            handlerComponentList_.componentList = &instrumentConfiguration->componentList;
            return Status(Status::Delegate, &handlerComponentList_);
        }
        else if (name == "softwareRef")
        {
            // note: placeholder
            string ref;
            decode_xml_id(getAttribute(attributes, "ref", ref));
            if (!ref.empty())
                instrumentConfiguration->softwarePtr = SoftwarePtr(new Software(ref));
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = instrumentConfiguration;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerComponentList  handlerComponentList_;
};


PWIZ_API_DECL void read(std::istream& is, InstrumentConfiguration& instrumentConfiguration)
{
    HandlerInstrumentConfiguration handler(&instrumentConfiguration);
    SAXParser::parse(is, handler);
}


//
// ProcessingMethod
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProcessingMethod& processingMethod)
{
    XMLWriter::Attributes attributes;
    attributes.add("order", processingMethod.order);
    if (processingMethod.softwarePtr.get())
        attributes.add("softwareRef", encode_xml_id_copy(processingMethod.softwarePtr->id)); 

    writer.startElement("processingMethod", attributes);
    writeParamContainer(writer, processingMethod);
    writer.endElement();
}

    
struct HandlerProcessingMethod : public HandlerParamContainer
{
    ProcessingMethod* processingMethod;
    string defaultSoftwareRef; 

    HandlerProcessingMethod(ProcessingMethod* _processingMethod = 0)
    :   processingMethod(_processingMethod)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!processingMethod)
            throw runtime_error("[IO::HandlerProcessingMethod] Null processingMethod.");

        if (name == "processingMethod")
        {
            getAttribute(attributes, "order", processingMethod->order);

            // note: placeholder
            string softwareRef;
            decode_xml_id(getAttribute(attributes, "softwareRef", softwareRef));
            if (!softwareRef.empty())
                processingMethod->softwarePtr = SoftwarePtr(new Software(softwareRef));
            else if (!defaultSoftwareRef.empty())
                processingMethod->softwarePtr = SoftwarePtr(new Software(defaultSoftwareRef));

            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = processingMethod;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, ProcessingMethod& processingMethod)
{
    HandlerProcessingMethod handler(&processingMethod);
    SAXParser::parse(is, handler);
}


//
// DataProcessing
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataProcessing& dataProcessing)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(dataProcessing.id));

    writer.startElement("dataProcessing", attributes);

    for (vector<ProcessingMethod>::const_iterator it=dataProcessing.processingMethods.begin(); 
         it!=dataProcessing.processingMethods.end(); ++it)
         write(writer, *it);
    
    writer.endElement();
}

    
struct HandlerDataProcessing : public HandlerParamContainer
{
    DataProcessing* dataProcessing;

    HandlerDataProcessing(DataProcessing* _dataProcessing = 0)
    :   dataProcessing(_dataProcessing)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!dataProcessing)
            throw runtime_error("[IO::HandlerDataProcessing] Null dataProcessing.");

        if (name == "dataProcessing")
        {
            decode_xml_id(getAttribute(attributes, "id", dataProcessing->id));

            // mzML 1.0
            if (version == 1)
            {
                string softwareRef;
                getAttribute(attributes, "softwareRef", softwareRef);
                if (!softwareRef.empty())
                    handlerProcessingMethod_.defaultSoftwareRef = softwareRef;
            }

            return Status::Ok;
        }
        else if (name == "processingMethod")
        {
            dataProcessing->processingMethods.push_back(ProcessingMethod());
            handlerProcessingMethod_.processingMethod = &dataProcessing->processingMethods.back(); 
            return Status(Status::Delegate, &handlerProcessingMethod_);
        }

        throw runtime_error(("[IO::HandlerDataProcessing] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerProcessingMethod handlerProcessingMethod_;
};


PWIZ_API_DECL void read(std::istream& is, DataProcessing& dataProcessing)
{
    HandlerDataProcessing handler(&dataProcessing);
    SAXParser::parse(is, handler);
}


//
// Target
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Target& t)
{
    writer.startElement("target");
    writeParamContainer(writer, t);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, Target& t)
{
    HandlerNamedParamContainer handler("target", &t);
    SAXParser::parse(is, handler);
}


//
// ScanSettings
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanSettings& scanSettings)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(scanSettings.id));

    writer.startElement("scanSettings", attributes);

    if (!scanSettings.sourceFilePtrs.empty()) 
    {
        attributes.clear();
        attributes.add("count", scanSettings.sourceFilePtrs.size());
        writer.startElement("sourceFileRefList", attributes);
        for (vector<SourceFilePtr>::const_iterator it=scanSettings.sourceFilePtrs.begin(); 
             it!=scanSettings.sourceFilePtrs.end(); ++it)
             writeSourceFileRef(writer, **it);
        writer.endElement(); // sourceFileRefList
    }

    if (!scanSettings.targets.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.add("count", scanSettings.targets.size());
        writer.startElement("targetList", attributes);

        for (vector<Target>::const_iterator it=scanSettings.targets.begin(); 
             it!=scanSettings.targets.end(); ++it)
             write(writer, *it);

        writer.endElement(); // targetList
    }

    writer.endElement();
}

    
struct HandlerScanSettings : public HandlerParamContainer
{
    ScanSettings* scanSettings;

    HandlerScanSettings(ScanSettings* _scanSettings = 0)
    :   scanSettings(_scanSettings), 
        handlerTarget_("target")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!scanSettings)
            throw runtime_error("[IO::HandlerScanSettings] Null scanSettings.");

        if ((version == 1 && name == "acquisitionSettings") /* mzML 1.0 */ ||
            name == "scanSettings")
        {
            decode_xml_id(getAttribute(attributes, "id", scanSettings->id));
            return Status::Ok;
        }
        else if (name=="sourceFileRefList" || name=="targetList")
        {
            return Status::Ok;
        }
        else if (name=="sourceFileRef")
        {
            // note: placeholder
            string sourceFileRef;
            decode_xml_id(getAttribute(attributes, "ref", sourceFileRef));
            if (!sourceFileRef.empty())
                scanSettings->sourceFilePtrs.push_back(SourceFilePtr(new SourceFile(sourceFileRef)));
            return Status::Ok;
        }
        else if (name=="target")
        {
            scanSettings->targets.push_back(Target());
            handlerTarget_.paramContainer = &scanSettings->targets.back();
            return Status(Status::Delegate, &handlerTarget_);
        }

        throw runtime_error(("[IO::HandlerScanSettings] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerNamedParamContainer handlerTarget_;
};


PWIZ_API_DECL void read(std::istream& is, ScanSettings& scanSettings)
{
    HandlerScanSettings handler(&scanSettings);
    SAXParser::parse(is, handler);
}


//
// IsolationWindow
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IsolationWindow& isolationWindow)
{
    writer.startElement("isolationWindow");
    writeParamContainer(writer, isolationWindow);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, IsolationWindow& isolationWindow)
{
    HandlerNamedParamContainer handler("isolationWindow", &isolationWindow);
    SAXParser::parse(is, handler);
}
    

//
// SelectedIon
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SelectedIon& selectedIon)
{
    writer.startElement("selectedIon");
    writeParamContainer(writer, selectedIon);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, SelectedIon& selectedIon)
{
    HandlerNamedParamContainer handler("selectedIon", &selectedIon);
    SAXParser::parse(is, handler);
}
    

//
// Activation
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Activation& activation)
{
    writer.startElement("activation");
    writeParamContainer(writer, activation);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, Activation& activation)
{
    HandlerNamedParamContainer handler("activation", &activation);
    SAXParser::parse(is, handler);
}
    

//
// Precursor
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Precursor& precursor)
{
    XMLWriter::Attributes attributes;

    if (precursor.spectrumID.empty())
    {
        if (!precursor.externalSpectrumID.empty())
        {
            if (!precursor.sourceFilePtr.get())
                throw runtime_error("[IO::write] External spectrum references must refer to a source file");

            attributes.add("sourceFileRef", encode_xml_id_copy(precursor.sourceFilePtr->id)); 
            attributes.add("externalSpectrumID", precursor.externalSpectrumID); 
        }
    }
    else
        attributes.add("spectrumRef", precursor.spectrumID); // not an XML:IDREF

    writer.startElement("precursor", attributes);
    writeParamContainer(writer, precursor);

    if (!precursor.isolationWindow.empty())
    {
        writer.startElement("isolationWindow");
        writeParamContainer(writer, precursor.isolationWindow);
        writer.endElement(); // isolationWindow
    }

    if (!precursor.selectedIons.empty())
    {
        attributes.clear();
        attributes.add("count", precursor.selectedIons.size());
        writer.startElement("selectedIonList", attributes);

        for (vector<SelectedIon>::const_iterator it=precursor.selectedIons.begin(); 
             it!=precursor.selectedIons.end(); ++it)
        {
            writer.startElement("selectedIon");
            writeParamContainer(writer, *it);
            writer.endElement(); // selectedIon
        }

        writer.endElement(); // selectedIonList
    }

    writer.startElement("activation");
    writeParamContainer(writer, precursor.activation);
    writer.endElement(); // activation
    
    writer.endElement();
}

    
struct HandlerPrecursor : public HandlerParamContainer
{
    Precursor* precursor;
    const map<string,string>* legacyIdRefToNativeId;

    HandlerPrecursor(Precursor* _precursor = 0, const map<string, string>* legacyIdRefToNativeId = 0)
    :   precursor(_precursor), 
        legacyIdRefToNativeId(legacyIdRefToNativeId),
        handlerIsolationWindow_("isolationWindow"), 
        handlerSelectedIon_("selectedIon"), 
        handlerActivation_("activation")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[IO::HandlerPrecursor] Null precursor.");

        if (name == "precursor")
        {
            getAttribute(attributes, "spectrumRef", precursor->spectrumID); // not an XML:IDREF
            getAttribute(attributes, "externalSpectrumID", precursor->externalSpectrumID);

            // mzML 1.0
            if (version == 1 && legacyIdRefToNativeId && !precursor->spectrumID.empty())
            {
                map<string,string>::const_iterator itr = legacyIdRefToNativeId->find(precursor->spectrumID);
                if (itr != legacyIdRefToNativeId->end())
                    precursor->spectrumID = itr->second;
            }

            // note: placeholder
            string sourceFileRef;
            decode_xml_id(getAttribute(attributes, "sourceFileRef", sourceFileRef));
            if (!sourceFileRef.empty())
                precursor->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

            return Status::Ok;
        }
        else if (name == "isolationWindow")
        {
            handlerIsolationWindow_.paramContainer = &precursor->isolationWindow;
            return Status(Status::Delegate, &handlerIsolationWindow_);
        }
        else if (name == "selectedIon")
        {
            precursor->selectedIons.push_back(SelectedIon());
            handlerSelectedIon_.paramContainer = &precursor->selectedIons.back();
            return Status(Status::Delegate, &handlerSelectedIon_);
        }
        else if (name == "activation")
        {
            handlerActivation_.paramContainer = &precursor->activation;
            return Status(Status::Delegate, &handlerActivation_);
        }
        else if (name == "selectedIonList")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = precursor;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerNamedParamContainer handlerIsolationWindow_;
    HandlerNamedParamContainer handlerSelectedIon_;
    HandlerNamedParamContainer handlerActivation_;
};


PWIZ_API_DECL void read(std::istream& is, Precursor& precursor, const map<string,string>* legacyIdRefToNativeId)
{
    HandlerPrecursor handler(&precursor, legacyIdRefToNativeId);
    SAXParser::parse(is, handler);
}


//
// Product
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Product& product)
{
    XMLWriter::Attributes attributes;

    writer.startElement("product", attributes);

    if (!product.isolationWindow.empty())
    {
        writer.startElement("isolationWindow");
        writeParamContainer(writer, product.isolationWindow);
        writer.endElement(); // isolationWindow
    }

    writer.endElement();
}

    
struct HandlerProduct : public SAXParser::Handler
{
    Product* product;

    HandlerProduct(Product* _product = 0)
    :   product(_product), 
        handlerIsolationWindow_("isolationWindow")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!product)
            throw runtime_error("[IO::HandlerProduct] Null product.");

        if (name == "product")
        {
            return Status::Ok;
        }
        else if (name == "isolationWindow")
        {
            handlerIsolationWindow_.paramContainer = &product->isolationWindow;
            return Status(Status::Delegate, &handlerIsolationWindow_);
        }

        throw runtime_error(("[IO::HandlerProduct] Unknown element " + name).c_str()); 
    }

    private:
    HandlerNamedParamContainer handlerIsolationWindow_;
};


PWIZ_API_DECL void read(std::istream& is, Product& product)
{
    HandlerProduct handler(&product);
    SAXParser::parse(is, handler);
}


//
// ScanWindow
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanWindow& scanWindow)
{
    writer.startElement("scanWindow");
    writeParamContainer(writer, scanWindow);
    writer.endElement();
}


PWIZ_API_DECL void read(std::istream& is, ScanWindow& scanWindow)
{
    HandlerNamedParamContainer handler("scanWindow", &scanWindow);
    SAXParser::parse(is, handler);
}
    

//
// Scan
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Scan& scan, const MSData& msd)
{
    XMLWriter::Attributes attributes;

    if (scan.spectrumID.empty())
    {
        if (!scan.externalSpectrumID.empty())
        {
            if (!scan.sourceFilePtr.get())
                throw runtime_error("[IO::write] External spectrum references must refer to a source file");

            attributes.add("sourceFileRef", encode_xml_id_copy(scan.sourceFilePtr->id)); 
            attributes.add("externalSpectrumID", scan.externalSpectrumID); 
        }
    }
    else
        attributes.add("spectrumRef", scan.spectrumID); // not an XML:IDREF

    // don't write the instrumentConfigurationRef if it's set to the default
    const InstrumentConfigurationPtr& defaultIC = msd.run.defaultInstrumentConfigurationPtr;
    if (scan.instrumentConfigurationPtr.get() &&
        (!defaultIC.get() || scan.instrumentConfigurationPtr != defaultIC))
        attributes.add("instrumentConfigurationRef", encode_xml_id_copy(scan.instrumentConfigurationPtr->id));

    writer.startElement("scan", attributes);
    writeParamContainer(writer, scan);
    
    if (!scan.scanWindows.empty())
    {
        attributes.clear();
        attributes.add("count", scan.scanWindows.size());
        writer.startElement("scanWindowList", attributes);
        
        for (vector<ScanWindow>::const_iterator it=scan.scanWindows.begin(); 
             it!=scan.scanWindows.end(); ++it)
             write(writer, *it);
     
        writer.endElement();
    }

    writer.endElement();
}

    
struct HandlerScan : public HandlerParamContainer
{
    Scan* scan;

    HandlerScan(Scan* _scan = 0)
    :   scan(_scan), handlerScanWindow_("scanWindow")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!scan)
            throw runtime_error("[IO::HandlerScan] Null scan.");

        if (name != "cvParam") 
        { // most common, but not handled here
            if (name == "scan")
            {
                getAttribute(attributes, "spectrumRef", scan->spectrumID); // not an XML:IDREF
                getAttribute(attributes, "externalSpectrumID", scan->externalSpectrumID);

                // note: placeholder
                string sourceFileRef;
                decode_xml_id(getAttribute(attributes, "sourceFileRef", sourceFileRef));
                if (!sourceFileRef.empty())
                    scan->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

                // note: placeholder
                string instrumentConfigurationRef;
                decode_xml_id(getAttribute(attributes, "instrumentConfigurationRef", instrumentConfigurationRef));
                if (!instrumentConfigurationRef.empty())
                    scan->instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration(instrumentConfigurationRef));
                return Status::Ok;
            }
            else if (version == 1 && name == "acquisition")
            {
                // note: spectrumRef, externalNativeID, and externalSpectrumID are mutually exclusive
                getAttribute(attributes, "spectrumRef", scan->spectrumID); // not an XML:IDREF
                if (scan->spectrumID.empty())
                {
                    string externalNativeID;
                    getAttribute(attributes, "externalNativeID", externalNativeID);
                    if (externalNativeID.empty())
                        getAttribute(attributes, "externalSpectrumID", scan->externalSpectrumID);
                    else
                        try
                        {
                            lexical_cast<int>(externalNativeID);
                            //cerr << "[IO::HandlerScan] Warning - mzML 1.0: <acquisition>::externalNativeID\n";
                            scan->externalSpectrumID = "scan=" + externalNativeID;
                        }
                        catch(exception&)
                        {
                            //cerr << "[IO::HandlerScan] Warning - mzML 1.0: non-integral <acquisition>::externalNativeID; externalSpectrumID format unknown\n";
                            scan->externalSpectrumID = externalNativeID;
                        }
                }

                // note: placeholder
                string sourceFileRef;
                decode_xml_id(getAttribute(attributes, "sourceFileRef", sourceFileRef));
                if (!sourceFileRef.empty())
                    scan->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

                return Status::Ok;
            }
            else if (name == "scanWindowList")
            {
                return Status::Ok;
            }
            else if (name == "scanWindow")
            {
                scan->scanWindows.push_back(ScanWindow());
                handlerScanWindow_.paramContainer = &scan->scanWindows.back();
                return Status(Status::Delegate, &handlerScanWindow_);
            }
        } // end if not cvParam

        HandlerParamContainer::paramContainer = scan;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerNamedParamContainer handlerScanWindow_;
};


PWIZ_API_DECL void read(std::istream& is, Scan& scan)
{
    HandlerScan handler(&scan);
    SAXParser::parse(is, handler);
}


//
// ScanList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ScanList& scanList, const MSData& msd)
{
    XMLWriter::Attributes attributes;
    attributes.add("count", scanList.scans.size());
    writer.startElement("scanList", attributes);
    writeParamContainer(writer, scanList);
    
    for (vector<Scan>::const_iterator it=scanList.scans.begin(); 
         it!=scanList.scans.end(); ++it)
         write(writer, *it, msd);
    
    writer.endElement();
}

    
struct HandlerScanList : public HandlerParamContainer
{
    ScanList* scanList;

    HandlerScanList(ScanList* _scanList = 0)
    :   scanList(_scanList)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!scanList)
            throw runtime_error("[IO::HandlerScanList] Null scanList.");

        if (name == "scanList" || name == "acquisitionList")
        {
            return Status::Ok;
        }
        else if (name == "scan" || name == "acquisition")
        {
            scanList->scans.push_back(Scan());
            handlerScan_.version = version;
            handlerScan_.scan = &scanList->scans.back(); 
            return Status(Status::Delegate, &handlerScan_);
        }

        HandlerParamContainer::paramContainer = scanList;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerScan handlerScan_;
};


PWIZ_API_DECL void read(std::istream& is, ScanList& scanList)
{
    HandlerScanList handler(&scanList);
    SAXParser::parse(is, handler);
}


//
// BinaryData
//

template <typename BinaryDataArrayType>
void writeBinaryDataArray(minimxml::XMLWriter& writer, const BinaryDataArrayType& binaryDataArray, const BinaryDataEncoder::Config& config)
{
    BinaryDataEncoder::Config usedConfig = config;
    map<CVID, BinaryDataEncoder::Precision>::const_iterator overrideItr = config.precisionOverrides.find(binaryDataArray.cvParamChild(MS_binary_data_array).cvid);
    if (overrideItr != config.precisionOverrides.end())
        usedConfig.precision = overrideItr->second;
    map<CVID, BinaryDataEncoder::Numpress>::const_iterator n_overrideItr = config.numpressOverrides.find(binaryDataArray.cvParamChild(MS_binary_data_array).cvid);
    if (n_overrideItr != config.numpressOverrides.end())
        usedConfig.numpress = n_overrideItr->second;

    BinaryDataEncoder encoder(usedConfig);
    string encoded;
    encoder.encode(binaryDataArray.data, encoded);
    usedConfig = encoder.getConfig(); // config may have changed if numpress error was excessive

    XMLWriter::Attributes attributes;

    // primary array types can never override the default array length
    if (!binaryDataArray.hasCVParam(MS_m_z_array) &&
        !binaryDataArray.hasCVParam(MS_time_array) &&
        !binaryDataArray.hasCVParam(MS_intensity_array))
    {
        attributes.add("arrayLength", binaryDataArray.data.size());
    }

    attributes.add("encodedLength", encoded.size());
    if (binaryDataArray.dataProcessingPtr.get())
        attributes.add("dataProcessingRef", encode_xml_id_copy(binaryDataArray.dataProcessingPtr->id));

    writer.startElement("binaryDataArray", attributes);

    if (BinaryDataEncoder::Numpress_None == usedConfig.numpress)
    {
        if (usedConfig.precision == BinaryDataEncoder::Precision_32)
            write(writer, typeid(BinaryDataArrayType) == typeid(IntegerDataArray) ? MS_32_bit_integer : MS_32_bit_float);
        else
            write(writer, typeid(BinaryDataArrayType) == typeid(IntegerDataArray) ? MS_64_bit_integer : MS_64_bit_float);
    }

    if (usedConfig.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian)
        throw runtime_error("[IO::write()] mzML: must use little endian encoding.");

	bool zlib = false; // Handle numpress+zlib
    switch (usedConfig.compression) {
        case BinaryDataEncoder::Compression_None:
            if (BinaryDataEncoder::Numpress_None == usedConfig.numpress)
                write(writer, MS_no_compression);
            break;
        case BinaryDataEncoder::Compression_Zlib:
			zlib = true;
			if (BinaryDataEncoder::Numpress_None == usedConfig.numpress)
				write(writer, MS_zlib_compression);
            break;
        default:
            throw runtime_error("[IO::write()] Unsupported compression method.");
            break;
    }
    switch (usedConfig.numpress) {
        case BinaryDataEncoder::Numpress_Linear:
            write(writer, MS_32_bit_float); // This should actually be ignored by any reader since numpress defines word size and format, but it makes output standards-compliant and is pretty close to true anyway
			write(writer, zlib ? MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression : MS_MS_Numpress_linear_prediction_compression);
            break;
        case BinaryDataEncoder::Numpress_Pic:
            write(writer, MS_32_bit_integer); // This should actually be ignored by any reader since numpress defines word size and format, but it makes output standards-compliant and is pretty close to true anyway
			write(writer, zlib ? MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression : MS_MS_Numpress_positive_integer_compression);
            break;
        case BinaryDataEncoder::Numpress_Slof:
            write(writer, MS_32_bit_float); // This should actually be ignored by any reader since numpress defines word size and format, but it makes output standards-compliant and is pretty close to true anyway
			write(writer, zlib ? MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression : MS_MS_Numpress_short_logged_float_compression);
            break;
        case BinaryDataEncoder::Numpress_None:
            break;
        default:
            throw runtime_error("[IO::write()] Unsupported numpress method.");
            break;
    }

    writeParamContainer(writer, binaryDataArray); 

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("binary");
    writer.characters(encoded, false); // base64 doesn't use any reserved characters
    writer.endElement();
    writer.popStyle();

    writer.endElement();
}


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const BinaryDataArray& binaryDataArray, const BinaryDataEncoder::Config& config)
{
    writeBinaryDataArray(writer, binaryDataArray, config);
}

PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const IntegerDataArray& binaryDataArray, const BinaryDataEncoder::Config& config)
{
    writeBinaryDataArray(writer, binaryDataArray, config);
}


struct HandlerBinaryDataArray : public HandlerParamContainer
{
    std::vector<BinaryDataArrayPtr>* binaryDataArrayPtrs;
    std::vector<IntegerDataArrayPtr>* integerDataArrayPtrs;
    const MSData* msd;
    size_t defaultArrayLength;
    BinaryDataEncoder::Config config;
    ParamContainer paramContainer;
    DataProcessingPtr dataProcessingPtr;
    CVID cvidBinaryDataType;

    HandlerBinaryDataArray(std::vector<BinaryDataArrayPtr>* binaryDataArrayPtrs = 0, std::vector<IntegerDataArrayPtr>* integerDataArrayPtrs = 0, const MSData* _msd = 0)
      : binaryDataArrayPtrs(binaryDataArrayPtrs),
        integerDataArrayPtrs(integerDataArrayPtrs),
        msd(_msd),
        defaultArrayLength(0),
        arrayLength_(0),
        encodedLength_(0)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "cvParam") // most common, but not handled here
        { 
            if (name == "binaryDataArray")
            {
                paramContainer.clear();

                // note: placeholder
                string dataProcessingRef;
                decode_xml_id(getAttribute(attributes, "dataProcessingRef", dataProcessingRef));
                if (!dataProcessingRef.empty())
                    dataProcessingPtr.reset(new DataProcessing(dataProcessingRef));
                else
                    dataProcessingPtr.reset();

                getAttribute(attributes, "encodedLength", encodedLength_, NoXMLUnescape);
                getAttribute(attributes, "arrayLength", arrayLength_, NoXMLUnescape, defaultArrayLength);

                return Status::Ok;
            }
            else if (name == "binary")
            {
                if (msd) References::resolve(paramContainer, *msd);
                config = getConfig();

                switch (cvidBinaryDataType)
                {
                    case MS_32_bit_float:
                    case MS_64_bit_float:
                    {
                        BinaryDataArrayPtr binaryDataArray = boost::make_shared<BinaryDataArray>();
                        binaryDataArrayPtrs->emplace_back(binaryDataArray);
                        swap(static_cast<ParamContainer&>(*binaryDataArray), paramContainer);
                        binaryDataArray->dataProcessingPtr = dataProcessingPtr;
                    }
                    break;

                    case MS_32_bit_integer:
                    case MS_64_bit_integer:
                    {
                        IntegerDataArrayPtr binaryDataArray = boost::make_shared<IntegerDataArray>();
                        integerDataArrayPtrs->emplace_back(binaryDataArray);
                        swap(static_cast<ParamContainer&>(*binaryDataArray), paramContainer);
                        binaryDataArray->dataProcessingPtr = dataProcessingPtr;
                    }
                    break;

                    case CVID_Unknown:
                    default:
                        throw runtime_error("[IO::HandlerBinaryDataArray] Unknown binary data type.");
                }

                return Status::Ok;
            }
        } // end if not cvParam

        HandlerParamContainer::paramContainer = &paramContainer;
        return HandlerParamContainer::startElement(name, attributes, position);
    }


    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        BinaryDataEncoder encoder(config);

        switch (cvidBinaryDataType)
        {
            case MS_32_bit_float:
            case MS_64_bit_float:
                {
                    auto& binaryDataArray = binaryDataArrayPtrs->back();
                    encoder.decode(text.c_str(), text.length(), binaryDataArray->data);

                    if (binaryDataArray->data.size() != arrayLength_)
                        throw runtime_error((format("[IO::HandlerBinaryDataArray] At position %d: expected array of size %d, but decoded array is actually size %d.")
                                                % position % arrayLength_ % binaryDataArray->data.size()).str());
                }
                break;

            case MS_32_bit_integer:
            case MS_64_bit_integer:
                {
                    auto& binaryDataArray = integerDataArrayPtrs->back();
                    encoder.decode(text.c_str(), text.length(), binaryDataArray->data);

                    if (binaryDataArray->data.size() != arrayLength_)
                        throw runtime_error((format("[IO::HandlerBinaryDataArray] At position %d: expected array of size %d, but decoded array is actually size %d.")
                            % position % arrayLength_ % binaryDataArray->data.size()).str());

                    swap(static_cast<ParamContainer&>(*binaryDataArray), paramContainer);
                    binaryDataArray->dataProcessingPtr = dataProcessingPtr;
                }
                break;

            case CVID_Unknown:
            default:
                throw runtime_error("[IO::HandlerBinaryDataArray] Unknown binary data type.");
        }

        if (text.length() != encodedLength_)
            throw runtime_error("[IO::HandlerBinaryDataArray] At position " + lexical_cast<string>(position) + ": encoded lengths differ."); 

        return Status::Ok;
    }

    private:

    size_t arrayLength_;
    size_t encodedLength_;

    CVID extractCVParam(ParamContainer& container, CVID cvid)
    {
        vector<CVParam>& params = container.cvParams;
        vector<CVParam>::iterator it = find_if(params.begin(), params.end(), 
                                               CVParamIsChildOf(cvid));

        CVID result = CVID_Unknown;

        if (it != params.end())
        {   
            // found the cvid in container -- erase the CVParam
            result = it->cvid;
            params.erase(it);
        }
        else
        {
            // didn't find it -- search recursively, but don't erase anything
            CVParam temp = container.cvParamChild(cvid);
            result = temp.cvid;
        }

        return result;
    }

    void extractCVParams(ParamContainer& container, CVID cvid, vector<CVID> &results)
    {
        vector<CVParam>& params = container.cvParams;
        vector<CVParam>::iterator it;
        while ((it = find_if(params.begin(), params.end(),CVParamIsChildOf(cvid))) != params.end())
        {   
            // found the cvid in container -- erase the CVParam
            results.push_back(it->cvid);
            params.erase(it);
        }

        // also search recursively, but don't erase anything
        vector<CVParam> CVParams = container.cvParamChildren(cvid);
        BOOST_FOREACH(const CVParam& cvParam, CVParams)
            results.push_back(cvParam.cvid);
    }

    BinaryDataEncoder::Config getConfig()
    {
        BinaryDataEncoder::Config config;

        //
        // Note: these CVParams are really info about the encoding, and not 
        // part of the BinaryDataArray.  We look at them to see how to decode the data,
        // and remove them from the BinaryDataArray struct (extractCVParam does the removal).
        //

        cvidBinaryDataType = extractCVParam(paramContainer, MS_binary_data_type);
 
        // handle mix of zlib and numpress compression
        CVID cvidCompressionType;
        config.compression = BinaryDataEncoder::Compression_None;
        config.numpress = BinaryDataEncoder::Numpress_None;
        vector<CVID> children;
        extractCVParams(paramContainer, MS_binary_data_compression_type, children);
        BOOST_FOREACH(cvidCompressionType,children)
        {
            switch (cvidCompressionType)
            {
                case MS_no_compression:
                    config.compression = BinaryDataEncoder::Compression_None;
                    break;
                case MS_zlib_compression:
                    config.compression = BinaryDataEncoder::Compression_Zlib;
                    break;
				case MS_MS_Numpress_linear_prediction_compression:
					config.numpress = BinaryDataEncoder::Numpress_Linear;
					break;
				case MS_MS_Numpress_positive_integer_compression:
					config.numpress = BinaryDataEncoder::Numpress_Pic;
					break;
				case MS_MS_Numpress_short_logged_float_compression:
					config.numpress = BinaryDataEncoder::Numpress_Slof;
					break;
				case MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression:
					config.numpress = BinaryDataEncoder::Numpress_Linear;
					config.compression = BinaryDataEncoder::Compression_Zlib;
					break;
				case MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression:
					config.numpress = BinaryDataEncoder::Numpress_Pic;
					config.compression = BinaryDataEncoder::Compression_Zlib;
					break;
				case MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression:
					config.numpress = BinaryDataEncoder::Numpress_Slof;
					config.compression = BinaryDataEncoder::Compression_Zlib;
					break;
				default:
                    throw runtime_error("[IO::HandlerBinaryDataArray] Unknown compression type.");
            }
        }

        // if numpress is on, make sure output is directed to BinaryDataArray instead of IntegerDataArray
        if (BinaryDataEncoder::Numpress_None != config.numpress)
            switch (cvidBinaryDataType)
            {
                case MS_32_bit_integer:
                    cvidBinaryDataType = MS_32_bit_float;
                    break;
                case MS_64_bit_integer:
                    cvidBinaryDataType = MS_64_bit_float;
                    break;
                case CVID_Unknown:
                    throw runtime_error("[IO::HandlerBinaryDataArray] Unknown binary data type.");
                default:
                    break;
            }

        switch (cvidBinaryDataType)
        {
            case MS_32_bit_float:
            case MS_32_bit_integer:
                if (BinaryDataEncoder::Numpress_None == config.numpress)
                    config.precision = BinaryDataEncoder::Precision_32;
                break;
            case MS_64_bit_float:
            case MS_64_bit_integer:
                config.precision = BinaryDataEncoder::Precision_64;
                break;
            case CVID_Unknown:
                if (BinaryDataEncoder::Numpress_None == config.numpress) // 32 vs 64 bit is meaningless in numpress
                    throw runtime_error("[IO::HandlerBinaryDataArray] Missing binary data type.");
                break;
            default:
                throw runtime_error("[IO::HandlerBinaryDataArray] Unknown binary data type.");
        }

        return config;
    }
};


PWIZ_API_DECL void read(std::istream& is, std::vector<BinaryDataArrayPtr>& binaryDataArrayPtrs, std::vector<IntegerDataArrayPtr>& integerDataArrayPtrs, const MSData* msd)
{
    HandlerBinaryDataArray handler(&binaryDataArrayPtrs, &integerDataArrayPtrs, msd);
    SAXParser::parse(is, handler);
}


//
// Spectrum
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Spectrum& spectrum, const MSData& msd, 
           const BinaryDataEncoder::Config& config)
{
    XMLWriter::Attributes attributes;
    attributes.add("index", spectrum.index);
    attributes.add("id", spectrum.id); // not an XML:ID
    if (!spectrum.spotID.empty())
        attributes.add("spotID", spectrum.spotID);
    attributes.add("defaultArrayLength", spectrum.defaultArrayLength);
    if (spectrum.dataProcessingPtr.get())
        attributes.add("dataProcessingRef", encode_xml_id_copy(spectrum.dataProcessingPtr->id));
    if (spectrum.sourceFilePtr.get())
        attributes.add("sourceFileRef", encode_xml_id_copy(spectrum.sourceFilePtr->id));

    writer.startElement("spectrum", attributes);

    writeParamContainer(writer, spectrum);

    write(writer, spectrum.scanList, msd);

    if (!spectrum.precursors.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.add("count", spectrum.precursors.size());
        writer.startElement("precursorList", attributes);
        
        for (vector<Precursor>::const_iterator it=spectrum.precursors.begin(); 
             it!=spectrum.precursors.end(); ++it)
             write(writer, *it);
     
        writer.endElement();
    }
   
    if (!spectrum.products.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.add("count", spectrum.products.size());
        writer.startElement("productList", attributes);
        
        for (vector<Product>::const_iterator it=spectrum.products.begin(); 
             it!=spectrum.products.end(); ++it)
             write(writer, *it);
     
        writer.endElement();
    }

    if (spectrum.binaryDataArrayPtrs.size() + spectrum.integerDataArrayPtrs.size() > 0)
    {
        attributes.clear();
        attributes.add("count", spectrum.binaryDataArrayPtrs.size() + spectrum.integerDataArrayPtrs.size());
        writer.startElement("binaryDataArrayList", attributes);

        for (const auto& itr : spectrum.binaryDataArrayPtrs)
            writeBinaryDataArray(writer, *itr, config);

        for (const auto& itr : spectrum.integerDataArrayPtrs)
            writeBinaryDataArray(writer, *itr, config);

        writer.endElement(); // binaryDataArrayList
    }

    writer.endElement(); // spectrum
}

    
struct HandlerSpectrum : public HandlerParamContainer
{
    BinaryDataFlag binaryDataFlag;
    Spectrum* spectrum;
    const SpectrumIdentityFromXML *spectrumID; 
    const map<string,string>* legacyIdRefToNativeId;
    const MSData* msd;

    HandlerSpectrum(BinaryDataFlag _binaryDataFlag,
                    Spectrum* _spectrum = 0,
                    const map<string,string>* legacyIdRefToNativeId = 0,
                    const MSData* _msd = 0,
                    const SpectrumIdentityFromXML *_spectrumID = 0)
    :   binaryDataFlag(_binaryDataFlag),
        spectrum(_spectrum),
        spectrumID(_spectrumID),
        legacyIdRefToNativeId(legacyIdRefToNativeId),
        msd(_msd),
        handlerPrecursor_(0, legacyIdRefToNativeId)
    {
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrum)
            throw runtime_error("[IO::HandlerSpectrum] Null spectrum.");

        if (name != "cvParam") // the most common, but not handled here
        { 
            if (name == "spectrum")
            {
                spectrum->sourceFilePosition = position;

                getAttribute(attributes, "index", spectrum->index);
                getAttribute(attributes, "spotID", spectrum->spotID);
                getAttribute(attributes, "defaultArrayLength", spectrum->defaultArrayLength);
                getAttribute(attributes, "id", spectrum->id); // not an XML:ID

                // mzML 1.0
                if (version == 1 && legacyIdRefToNativeId)
                {
                    map<string,string>::const_iterator itr = legacyIdRefToNativeId->find(spectrum->id);
                    if (itr != legacyIdRefToNativeId->end())
                        spectrum->id = itr->second;
                }

                // note: placeholder
                string dataProcessingRef;
                decode_xml_id(getAttribute(attributes, "dataProcessingRef", dataProcessingRef));
                if (!dataProcessingRef.empty())
                    spectrum->dataProcessingPtr = DataProcessingPtr(new DataProcessing(dataProcessingRef));

                // note: placeholder
                string sourceFileRef;
                decode_xml_id(getAttribute(attributes, "sourceFileRef", sourceFileRef));
                if (!sourceFileRef.empty())
                    spectrum->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

                return Status::Ok;
            }
            else if (version == 1 && name == "acquisitionList" /* mzML 1.0 */ || name == "scanList")
            {
                handlerScanList_.scanList = &spectrum->scanList;
                handlerScanList_.version = version;
                return Status(Status::Delegate, &handlerScanList_);
            }
            else if (name == "precursorList" || name == "productList")
            {
                return Status::Ok;
            }
            else if (name == "precursor")
            {
                spectrum->precursors.push_back(Precursor());
                handlerPrecursor_.precursor = &spectrum->precursors.back();
                handlerPrecursor_.version = version;
                return Status(Status::Delegate, &handlerPrecursor_);
            }
            else if (name == "product")
            {
                spectrum->products.push_back(Product());
                handlerProduct_.product = &spectrum->products.back();
                return Status(Status::Delegate, &handlerProduct_);
            }
            else if (name == "binaryDataArray")
            {
                if (binaryDataFlag == IgnoreBinaryData)
                    return Status::Done;

                handlerBinaryDataArray_.binaryDataArrayPtrs = &spectrum->binaryDataArrayPtrs;
                handlerBinaryDataArray_.integerDataArrayPtrs = &spectrum->integerDataArrayPtrs;
                handlerBinaryDataArray_.defaultArrayLength = spectrum->defaultArrayLength;
                handlerBinaryDataArray_.msd = msd;
                return Status(Status::Delegate, &handlerBinaryDataArray_);
            }
            else if (name == "binaryDataArrayList")
            {
                // pretty likely to come right back here and read the
                // binary data once the header info has been inspected, 
                // so note position
                if (spectrumID) {
                    spectrumID->sourceFilePositionForBinarySpectrumData = position; 
                }
                return Status::Ok;
            }
            else if (version == 1 && name == "spectrumDescription") // mzML 1.0
            {
                // read cvParams, userParams, and referenceableParamGroups in <spectrumDescription> into <spectrum>
                return Status::Ok;
            }
            else if (version == 1 && name == "scan") // mzML 1.0
            {
                spectrum->scanList.scans.push_back(Scan());
                handlerScan_.version = version;
                handlerScan_.scan = &spectrum->scanList.scans.back();
                return Status(Status::Delegate, &handlerScan_);
            }
        }  // end if name != cvParam
        HandlerParamContainer::paramContainer = spectrum;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerScanList handlerScanList_;
    HandlerPrecursor handlerPrecursor_;
    HandlerProduct handlerProduct_;
    HandlerBinaryDataArray handlerBinaryDataArray_;
    HandlerScan handlerScan_;
};


PWIZ_API_DECL void read(std::istream& is, Spectrum& spectrum,
                        BinaryDataFlag binaryDataFlag,
                        int version,
                        const map<string,string>* legacyIdRefToNativeId,
                        const MSData* msd,
                        const SpectrumIdentityFromXML *id)
{
    HandlerSpectrum handler(binaryDataFlag, &spectrum, legacyIdRefToNativeId, msd, id);
    handler.version = version;
    SAXParser::parse(is, handler);
}


//
// Chromatogram
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Chromatogram& chromatogram, 
           const BinaryDataEncoder::Config& config)
{
    XMLWriter::Attributes attributes;
    attributes.add("index", chromatogram.index);
    attributes.add("id", chromatogram.id); // not an XML:ID
    attributes.add("defaultArrayLength", chromatogram.defaultArrayLength);
    if (chromatogram.dataProcessingPtr.get())
        attributes.add("dataProcessingRef", encode_xml_id_copy(chromatogram.dataProcessingPtr->id));

    writer.startElement("chromatogram", attributes);

    writeParamContainer(writer, chromatogram);
    
    if (!chromatogram.precursor.empty())
        write(writer, chromatogram.precursor);
   
    if (!chromatogram.product.empty())
        write(writer, chromatogram.product);

    if (chromatogram.binaryDataArrayPtrs.size() + chromatogram.integerDataArrayPtrs.size() > 0)
    {
        attributes.clear();
        attributes.add("count", chromatogram.binaryDataArrayPtrs.size() + chromatogram.integerDataArrayPtrs.size());
        writer.startElement("binaryDataArrayList", attributes);

        for (const auto& itr : chromatogram.binaryDataArrayPtrs)
            writeBinaryDataArray(writer, *itr, config);

        for (const auto& itr : chromatogram.integerDataArrayPtrs)
            writeBinaryDataArray(writer, *itr, config);

        writer.endElement(); // binaryDataArrayList
    }

    writer.endElement(); // spectrum
}


struct HandlerChromatogram : public HandlerParamContainer
{
    BinaryDataFlag binaryDataFlag;
    Chromatogram* chromatogram;

    HandlerChromatogram(BinaryDataFlag _binaryDataFlag,
                        Chromatogram* _chromatogram = 0)
    :   binaryDataFlag(_binaryDataFlag),
        chromatogram(_chromatogram)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!chromatogram)
            throw runtime_error("[IO::HandlerChromatogram] Null chromatogram.");

        if (name == "chromatogram")
        {
            chromatogram->sourceFilePosition = position;

            getAttribute(attributes, "id", chromatogram->id); // not an XML:ID
            getAttribute(attributes, "index", chromatogram->index);
            getAttribute(attributes, "defaultArrayLength", chromatogram->defaultArrayLength);

            // note: placeholder
            string dataProcessingRef;
            decode_xml_id(getAttribute(attributes, "dataProcessingRef", dataProcessingRef));
            if (!dataProcessingRef.empty())
                chromatogram->dataProcessingPtr = DataProcessingPtr(new DataProcessing(dataProcessingRef));

            return Status::Ok;
        }
        else if (name == "precursor")
        {
            handlerPrecursor_.precursor = &chromatogram->precursor;
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "product")
        {
            handlerProduct_.product = &chromatogram->product;
            return Status(Status::Delegate, &handlerProduct_);
        }
        else if (name == "binaryDataArray")
        {
            if (binaryDataFlag == IgnoreBinaryData)
                return Status::Done;

            handlerBinaryDataArray_.binaryDataArrayPtrs = &chromatogram->binaryDataArrayPtrs;
            handlerBinaryDataArray_.integerDataArrayPtrs = &chromatogram->integerDataArrayPtrs;
            handlerBinaryDataArray_.defaultArrayLength = chromatogram->defaultArrayLength;
            return Status(Status::Delegate, &handlerBinaryDataArray_);
        }
        else if (name == "binaryDataArrayList")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = chromatogram;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
        
    HandlerPrecursor handlerPrecursor_;
    HandlerProduct handlerProduct_;
    HandlerBinaryDataArray handlerBinaryDataArray_;
};


PWIZ_API_DECL void read(std::istream& is, Chromatogram& chromatogram,
                        BinaryDataFlag binaryDataFlag)
{
    HandlerChromatogram handler(binaryDataFlag, &chromatogram);
    SAXParser::parse(is, handler);
}


//
// SpectrumList
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const SpectrumList& spectrumList, const MSData& msd,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions,
           const IterationListenerRegistry* iterationListenerRegistry,
           bool useWorkerThreads)
{
    XMLWriter::Attributes attributes;
    attributes.add("count", spectrumList.size());

    if (spectrumList.dataProcessingPtr().get())
        attributes.push_back(make_pair("defaultDataProcessingRef", 
                                        spectrumList.dataProcessingPtr()->id));

    writer.startElement("spectrumList", attributes); // required by schema, even if empty

    SpectrumWorkerThreads spectrumWorkers(spectrumList, useWorkerThreads);

    for (size_t i=0; i<spectrumList.size(); i++)
    {
        // send progress updates, handling cancel

        IterationListener::Status status = IterationListener::Status_Ok;

        if (iterationListenerRegistry)
            status = iterationListenerRegistry->broadcastUpdateMessage(
                IterationListener::UpdateMessage(i, spectrumList.size(), "writing spectra"));

        if (status == IterationListener::Status_Cancel)
            break;
 
        // save write position

        if (spectrumPositions)
            spectrumPositions->push_back(writer.positionNext());

        // write the spectrum

        //SpectrumPtr spectrum = spectrumList.spectrum(i, true);
        SpectrumPtr spectrum = spectrumWorkers.processBatch(i);
        BOOST_ASSERT(spectrum->binaryDataArrayPtrs.empty() ||
                     spectrum->defaultArrayLength == spectrum->getMZArray()->data.size());
        if (spectrum->index != i) throw runtime_error("[IO::write(SpectrumList)] Bad index.");
        write(writer, *spectrum, msd, config);
    }

    writer.endElement();
}

    
struct HandlerSpectrumListSimple : public HandlerParamContainer
{
    SpectrumListSimple* spectrumListSimple;

    HandlerSpectrumListSimple(SpectrumListSimple* _spectrumListSimple = 0)
    :   spectrumListSimple(_spectrumListSimple),
        handlerSpectrum_(ReadBinaryData)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumListSimple)
            throw runtime_error("[IO::HandlerSpectrumListSimple] Null spectrumListSimple.");

        if (name == "spectrumList")
        {
            // note: placeholder
            string defaultDataProcessingRef;
            decode_xml_id(getAttribute(attributes, "defaultDataProcessingRef", defaultDataProcessingRef));
            if (!defaultDataProcessingRef.empty())
                spectrumListSimple->dp = DataProcessingPtr(new DataProcessing(defaultDataProcessingRef));

            return Status::Ok;
        }
        else if (name == "spectrum")
        {
            spectrumListSimple->spectra.push_back(SpectrumPtr(new Spectrum));
            handlerSpectrum_.version = version;
            handlerSpectrum_.spectrum = spectrumListSimple->spectra.back().get();
            return Status(Status::Delegate, &handlerSpectrum_);
        }

        throw runtime_error(("[IO::HandlerSpectrumListSimple] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerSpectrum handlerSpectrum_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumListSimple& spectrumListSimple)
{
    HandlerSpectrumListSimple handler(&spectrumListSimple);
    SAXParser::parse(is, handler);
}


//
// ChromatogramList
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const ChromatogramList& chromatogramList,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* chromatogramPositions,
           const IterationListenerRegistry* iterationListenerRegistry)
{
    if (chromatogramList.empty()) // chromatogramList not required by schema
        return;

    XMLWriter::Attributes attributes;
    attributes.add("count", chromatogramList.size());

    if (chromatogramList.dataProcessingPtr().get())
        attributes.push_back(make_pair("defaultDataProcessingRef", 
                                        chromatogramList.dataProcessingPtr()->id));

    writer.startElement("chromatogramList", attributes);

    for (size_t i=0; i<chromatogramList.size(); i++)
    {
        // send progress updates, handling cancel

        IterationListener::Status status = IterationListener::Status_Ok;

        if (iterationListenerRegistry)
            status = iterationListenerRegistry->broadcastUpdateMessage(
                IterationListener::UpdateMessage(i, chromatogramList.size(), "writing chromatograms"));

        if (status == IterationListener::Status_Cancel)
            break;
 
        // save write position

        if (chromatogramPositions)
            chromatogramPositions->push_back(writer.positionNext());

        // write the chromatogram

        ChromatogramPtr chromatogram = chromatogramList.chromatogram(i, true);
        if (chromatogram->index != i) throw runtime_error("[IO::write(ChromatogramList)] Bad index.");
        write(writer, *chromatogram, config);
    }

    writer.endElement();
}

    
struct HandlerChromatogramListSimple : public HandlerParamContainer
{
    ChromatogramListSimple* chromatogramListSimple;

    HandlerChromatogramListSimple(ChromatogramListSimple* _chromatogramListSimple = 0)
    :   chromatogramListSimple(_chromatogramListSimple),
        handlerChromatogram_(ReadBinaryData)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!chromatogramListSimple)
            throw runtime_error("[IO::HandlerChromatogramListSimple] Null chromatogramListSimple.");

        if (name == "chromatogramList")
        {
            // note: placeholder
            string defaultDataProcessingRef;
            decode_xml_id(getAttribute(attributes, "defaultDataProcessingRef", defaultDataProcessingRef));
            if (!defaultDataProcessingRef.empty())
                chromatogramListSimple->dp = DataProcessingPtr(new DataProcessing(defaultDataProcessingRef));

            return Status::Ok;
        }
        else if (name == "chromatogram")
        {
            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            handlerChromatogram_.chromatogram = chromatogramListSimple->chromatograms.back().get();
            return Status(Status::Delegate, &handlerChromatogram_);
        }

        throw runtime_error(("[IO::HandlerChromatogramListSimple] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerChromatogram handlerChromatogram_;
};


PWIZ_API_DECL void read(std::istream& is, ChromatogramListSimple& chromatogramListSimple)
{
    HandlerChromatogramListSimple handler(&chromatogramListSimple);
    SAXParser::parse(is, handler);
}


//
// Run
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Run& run, const MSData& msd,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions,
           vector<boost::iostreams::stream_offset>* chromatogramPositions,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
           bool useWorkerThreads)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", encode_xml_id_copy(run.id));

    // defaultInstrumentConfigurationPtr is mandatory for schematic validity;
    // at least one (possibly unknown) instrument configuration is mandatory for schematic validity;
    // therefore we set this attribute to a reasonable default if the client didn't set it
    if (run.defaultInstrumentConfigurationPtr.get())
        attributes.add("defaultInstrumentConfigurationRef", encode_xml_id_copy(run.defaultInstrumentConfigurationPtr->id));
    else if (!msd.instrumentConfigurationPtrs.empty())
        attributes.add("defaultInstrumentConfigurationRef", encode_xml_id_copy(msd.instrumentConfigurationPtrs.front()->id));
    else
        attributes.add("defaultInstrumentConfigurationRef", "IC");

    if (run.samplePtr.get())
        attributes.add("sampleRef", encode_xml_id_copy(run.samplePtr->id));
    if (!run.startTimeStamp.empty())
        attributes.add("startTimeStamp", run.startTimeStamp);
    if (run.defaultSourceFilePtr.get())
        attributes.add("defaultSourceFileRef", encode_xml_id_copy(run.defaultSourceFilePtr->id));
 
    writer.startElement("run", attributes);

    writeParamContainer(writer, run);

    bool hasSpectrumList = run.spectrumListPtr.get() && run.spectrumListPtr->size() > 0;
    bool hasChromatogramList = run.chromatogramListPtr.get() && run.chromatogramListPtr->size() > 0;

    if (hasSpectrumList)
        write(writer, *run.spectrumListPtr, msd, config, spectrumPositions, iterationListenerRegistry, useWorkerThreads);

    if (hasChromatogramList)
        write(writer, *run.chromatogramListPtr, config, chromatogramPositions, iterationListenerRegistry);

    writer.endElement();
}

    
struct HandlerRun : public HandlerParamContainer
{
    SpectrumListFlag spectrumListFlag;
    Run* run;

    HandlerRun(SpectrumListFlag _spectrumListFlag, Run* _run = 0)
    :   spectrumListFlag(_spectrumListFlag), run(_run)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!run)
            throw runtime_error("[IO::HandlerRun] Null run.");

        if (name == "run")
        {
            decode_xml_id(getAttribute(attributes, "id", run->id));
            getAttribute(attributes, "startTimeStamp", run->startTimeStamp);

            // note: placeholder
            string defaultInstrumentConfigurationRef;
            decode_xml_id(getAttribute(attributes, "defaultInstrumentConfigurationRef", defaultInstrumentConfigurationRef));
            if (!defaultInstrumentConfigurationRef.empty())
                run->defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration(defaultInstrumentConfigurationRef));

            // note: placeholder
            string sampleRef;
            decode_xml_id(getAttribute(attributes, "sampleRef", sampleRef));
            if (!sampleRef.empty())
                run->samplePtr = SamplePtr(new Sample(sampleRef));

            // note: placeholder
            string defaultSourceFileRef;
            decode_xml_id(getAttribute(attributes, "defaultSourceFileRef", defaultSourceFileRef));
            if (!defaultSourceFileRef.empty())
                run->defaultSourceFilePtr = SourceFilePtr(new SourceFile(defaultSourceFileRef));

            return Status::Ok;
        }
        else if (name == "spectrumList")
        {
            if (spectrumListFlag == IgnoreSpectrumList)
                return Status::Done;

            shared_ptr<SpectrumListSimple> temp(new SpectrumListSimple);
            handlerSpectrumListSimple_.spectrumListSimple = temp.get();
            run->spectrumListPtr = temp;
            return Status(Status::Delegate, &handlerSpectrumListSimple_);
        }
        else if (name == "chromatogramList")
        {
            shared_ptr<ChromatogramListSimple> temp(new ChromatogramListSimple);
            handlerChromatogramListSimple_.chromatogramListSimple = temp.get();
            run->chromatogramListPtr = temp;
            return Status(Status::Delegate, &handlerChromatogramListSimple_);
        }
        else if (version == 1 && name == "sourceFileRefList")
        {
            return Status::Ok;
        }
        else if (version == 1 && name == "sourceFileRef")
        {
            // note: placeholder
            string sourceFileRef;
            decode_xml_id(getAttribute(attributes, "ref", sourceFileRef));
            if (!sourceFileRef.empty())
                run->defaultSourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = run;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerSpectrumListSimple handlerSpectrumListSimple_;
    HandlerChromatogramListSimple handlerChromatogramListSimple_;
};


PWIZ_API_DECL
void read(std::istream& is, Run& run,
          SpectrumListFlag spectrumListFlag)
{
    HandlerRun handler(spectrumListFlag, &run);
    SAXParser::parse(is, handler);
}


//
// MSData
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const MSData& msd,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions,
           vector<boost::iostreams::stream_offset>* chromatogramPositions,
           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
           bool useWorkerThreads)
{
    XMLWriter::Attributes attributes;
    attributes.add("xmlns", "http://psi.hupo.org/ms/mzml");
    attributes.add("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
    attributes.add("xsi:schemaLocation", "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML" + msd.version() + ".xsd");
    if (!msd.accession.empty())
        attributes.add("accession", msd.accession);
    attributes.add("id", msd.id); // not an XML:ID
    attributes.add("version", msd.version());

    writer.startElement("mzML", attributes);

    if (!msd.cvs.empty())
    {
        attributes.clear();
        attributes.add("count", msd.cvs.size());
        writer.startElement("cvList", attributes);
        for (vector<CV>::const_iterator it=msd.cvs.begin(); it!=msd.cvs.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }

    write(writer, msd.fileDescription);

    writeList(writer, msd.paramGroupPtrs, "referenceableParamGroupList");
    writeList(writer, msd.samplePtrs, "sampleList");
    writeList(writer, msd.softwarePtrs, "softwareList");
    writeList(writer, msd.scanSettingsPtrs, "scanSettingsList");

    // instrumentConfigurationList and at least one instrumentConfiguration is mandatory for schematic validity
    if (msd.instrumentConfigurationPtrs.empty())
    {
        // the base term "instrument model" indicates the instrument is unknown
        vector<InstrumentConfigurationPtr> list(1, InstrumentConfigurationPtr(new InstrumentConfiguration("IC")));
        list.back()->set(MS_instrument_model);
        writeList(writer, list, "instrumentConfigurationList");
    }
    else
        writeList(writer, msd.instrumentConfigurationPtrs, "instrumentConfigurationList");

    writeList(writer, msd.allDataProcessingPtrs(), "dataProcessingList");

    write(writer, msd.run, msd, config, spectrumPositions, chromatogramPositions, iterationListenerRegistry, useWorkerThreads);

    writer.endElement();
}


struct HandlerMSData : public SAXParser::Handler
{
    MSData* msd;

    HandlerMSData(SpectrumListFlag spectrumListFlag, MSData* _msd = 0) 
    :  msd(_msd), handlerRun_(spectrumListFlag) 
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!msd)
            throw runtime_error("[IO::HandlerMSData] Null msd."); 

        if (name == "mzML")
        {
            getAttribute(attributes, "accession", msd->accession);
            getAttribute(attributes, "id", msd->id); // not an XML:ID

            // "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML<version>.xsd"
            string schemaLocation;
            getAttribute(attributes, "xsi:schemaLocation", schemaLocation);
            if (schemaLocation.empty())
                getAttribute(attributes, "version", msd->version_); // deprecated?
            else
            {
                schemaLocation = schemaLocation.substr(schemaLocation.find(' ')+1);
                string xsdName = BFS_STRING(bfs::path(schemaLocation).filename());
                msd->version_ = xsdName.substr(4, xsdName.length()-8); // read between "mzML" and ".xsd"
            }

            if (msd->version_.find("1.0") == 0)
                version = 1;

            return Status::Ok;
        }
        else if (name == "cvList" || 
                 name == "referenceableParamGroupList" ||
                 name == "sampleList" || 
                 name == "instrumentConfigurationList" || 
                 name == "softwareList" ||
                 name == "dataProcessingList" ||
                 (version == 1 && name == "acquisitionSettingsList") /* mzML 1.0 */ ||
                 name == "scanSettingsList")
        {
            // ignore these, unless we want to validate the count attribute
            return Status::Ok;
        }
        else if (name == "cv")
        {
            msd->cvs.push_back(CV());
            handlerCV_.cv = &msd->cvs.back();
            return Status(Status::Delegate, &handlerCV_);
        }
        else if (name == "fileDescription")
        {
            handlerFileDescription_.fileDescription = &msd->fileDescription;
            return Status(Status::Delegate, &handlerFileDescription_);
        }
        else if (name == "referenceableParamGroup")
        {
            msd->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup));
            handlerParamGroup_.paramGroup = msd->paramGroupPtrs.back().get();
            return Status(Status::Delegate, &handlerParamGroup_);
        }
        else if (name == "sample")
        {
            msd->samplePtrs.push_back(SamplePtr(new Sample));
            handlerSample_.sample = msd->samplePtrs.back().get();
            return Status(Status::Delegate, &handlerSample_);
        }
        else if (name == "instrumentConfiguration")
        {
            msd->instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration));
            handlerInstrumentConfiguration_.instrumentConfiguration = msd->instrumentConfigurationPtrs.back().get();
            return Status(Status::Delegate, &handlerInstrumentConfiguration_);
        }        
        else if (name == "software")
        {
            msd->softwarePtrs.push_back(SoftwarePtr(new Software));
            handlerSoftware_.version = version;
            handlerSoftware_.software = msd->softwarePtrs.back().get();
            return Status(Status::Delegate, &handlerSoftware_);
        }        
        else if (name == "dataProcessing")
        {
            msd->dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing));
            handlerDataProcessing_.version = version;
            handlerDataProcessing_.dataProcessing = msd->dataProcessingPtrs.back().get();
            return Status(Status::Delegate, &handlerDataProcessing_);
        }
        else if (version == 1 && name == "acquisitionSettings" /* mzML 1.0 */ ||
                 name == "scanSettings")
        {
            msd->scanSettingsPtrs.push_back(ScanSettingsPtr(new ScanSettings));
            handlerScanSettings_.version = version;
            handlerScanSettings_.scanSettings = msd->scanSettingsPtrs.back().get();
            return Status(Status::Delegate, &handlerScanSettings_);
        }
        else if (name == "run")
        {
            handlerRun_.version = version;
            handlerRun_.run = &msd->run;
            return Status(Status::Delegate, &handlerRun_);
        }

        throw runtime_error(("[IO::HandlerMSData] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerCV handlerCV_;
    HandlerFileDescription handlerFileDescription_;
    HandlerParamGroup handlerParamGroup_;
    HandlerSample handlerSample_;
    HandlerInstrumentConfiguration handlerInstrumentConfiguration_;
    HandlerSoftware handlerSoftware_;
    HandlerDataProcessing handlerDataProcessing_;
    HandlerScanSettings handlerScanSettings_;
    HandlerRun handlerRun_;
};


PWIZ_API_DECL
void read(std::istream& is, MSData& msd,
          SpectrumListFlag spectrumListFlag)
{
    HandlerMSData handler(spectrumListFlag, &msd);
    SAXParser::parse(is, handler);
    References::resolve(msd); 
}


} // namespace IO
} // namespace msdata
} // namespace pwiz


