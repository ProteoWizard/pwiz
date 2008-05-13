//
// IO.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include "utility/minimxml/SAXParser.hpp"
#include "boost/lexical_cast.hpp"
#include <stdexcept>
#include <functional>


namespace pwiz {
namespace msdata {
namespace IO {


using namespace std;
using namespace minimxml;
using namespace minimxml::SAXParser;
using boost::lexical_cast;
using boost::shared_ptr;


//
// CV
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", cv.id));
    attributes.push_back(make_pair("fullName", cv.fullName));
    attributes.push_back(make_pair("version", cv.version));
    attributes.push_back(make_pair("URI", cv.URI));
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
        getAttribute(attributes, "id", cv->id);
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
    attributes.push_back(make_pair("name", userParam.name));
    if (!userParam.value.empty())
        attributes.push_back(make_pair("value", userParam.value));
    if (!userParam.type.empty())
        attributes.push_back(make_pair("type", userParam.type));
    if (userParam.units != CVID_Unknown)
    {
        attributes.push_back(make_pair("unitAccession", cvinfo(userParam.units).id));
        attributes.push_back(make_pair("unitName", cvinfo(userParam.units).name));
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
            userParam->units = cvinfo(unitAccession).cvid;

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
    attributes.push_back(make_pair("cvRef", cvinfo(cvParam.cvid).id.substr(0,2)));
    attributes.push_back(make_pair("accession", cvinfo(cvParam.cvid).id));
    attributes.push_back(make_pair("name", cvinfo(cvParam.cvid).name));
    attributes.push_back(make_pair("value", cvParam.value));
    if (cvParam.units != CVID_Unknown)
    {
        attributes.push_back(make_pair("unitAccession", cvinfo(cvParam.units).id));
        attributes.push_back(make_pair("unitName", cvinfo(cvParam.units).name));
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

        string accession;
        getAttribute(attributes, "accession", accession);
        if (!accession.empty())
            cvParam->cvid = cvinfo(accession).cvid;

        getAttribute(attributes, "value", cvParam->value);

        string unitAccession;
        getAttribute(attributes, "unitAccession", unitAccession);
        if (!unitAccession.empty())
            cvParam->units = cvinfo(unitAccession).cvid;

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
    attributes.push_back(make_pair("ref", paramGroup.id));
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
            getAttribute(attributes, "ref", id);
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
    attributes.push_back(make_pair("id", paramGroup.id));
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
            getAttribute(attributes, "id", paramGroup->id);
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
    attributes.push_back(make_pair("id", sf.id));
    attributes.push_back(make_pair("name", sf.name));
    attributes.push_back(make_pair("location", sf.location));
    writer.startElement("sourceFile", attributes);
    writeParamContainer(writer, sf);
    writer.endElement();
}


void writeSourceFileRef(minimxml::XMLWriter& writer, const SourceFile& sourceFile)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("ref", sourceFile.id));
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
            getAttribute(attributes, "id", sourceFile->id);
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

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(fd.sourceFilePtrs.size())));
    writer.startElement("sourceFileList", attributes);

    for (vector<SourceFilePtr>::const_iterator it=fd.sourceFilePtrs.begin(); 
         it!=fd.sourceFilePtrs.end(); ++it)
         write(writer, **it);

    writer.endElement();

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
    attributes.push_back(make_pair("id", sample.id));
    attributes.push_back(make_pair("name", sample.name));
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
            getAttribute(attributes, "id", sample->id);
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


PWIZ_API_DECL void writeComponent(minimxml::XMLWriter& writer, const Component& component, const string& label)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("order", lexical_cast<string>(component.order)));
    writer.startElement(label, attributes);
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

        if (name=="source" && dynamic_cast<Source*>(component) ||
            name=="analyzer" && dynamic_cast<Analyzer*>(component) ||
            name=="detector" && dynamic_cast<Detector*>(component))
        {
            getAttribute(attributes, "order", component->order);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = component;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Source& source)
{
    writeComponent(writer, source, "source");
}


PWIZ_API_DECL void read(std::istream& is, Source& source)
{
    HandlerComponent handler(&source);
    SAXParser::parse(is, handler);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Analyzer& analyzer)
{
    writeComponent(writer, analyzer, "analyzer");
}


PWIZ_API_DECL void read(std::istream& is, Analyzer& analyzer)
{
    HandlerComponent handler(&analyzer);
    SAXParser::parse(is, handler);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Detector& detector)
{
    writeComponent(writer, detector, "detector");
}


PWIZ_API_DECL void read(std::istream& is, Detector& detector)
{
    HandlerComponent handler(&detector);
    SAXParser::parse(is, handler);
}


//
// ComponentList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ComponentList& componentList)
{
    int count = 0;
    if (componentList.source.order) count++;
    if (componentList.analyzer.order) count++;
    if (componentList.detector.order) count++;

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(count)));

    writer.startElement("componentList", attributes);
    if (componentList.source.order) write(writer, componentList.source); 
    if (componentList.analyzer.order) write(writer, componentList.analyzer); 
    if (componentList.detector.order) write(writer, componentList.detector); 
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
            handlerComponent_.component = &componentList->source;
            return Status(Status::Delegate, &handlerComponent_);
        }
        else if (name == "analyzer")
        {
            handlerComponent_.component = &componentList->analyzer;
            return Status(Status::Delegate, &handlerComponent_);
        }
        else if (name == "detector")
        {
            handlerComponent_.component = &componentList->detector;
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
    attributes.push_back(make_pair("id", software.id));
    writer.startElement("software", attributes);

    attributes.clear();
    const CVInfo& info = cvinfo(software.softwareParam.cvid);
    attributes.push_back(make_pair("cvRef", info.id.substr(0,2)));
    attributes.push_back(make_pair("accession", info.id));
    attributes.push_back(make_pair("name", info.name));
    attributes.push_back(make_pair("version", software.softwareParamVersion));
    writer.startElement("softwareParam", attributes, XMLWriter::EmptyElement);

    writer.endElement();
}


struct HandlerSoftware : public SAXParser::Handler
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
            getAttribute(attributes, "id", software->id);
            return Status::Ok;
        }
        else if (name == "softwareParam")
        {
            string accession;
            getAttribute(attributes, "accession", accession);
            if (!accession.empty())
                software->softwareParam.cvid = cvinfo(accession).cvid;

            getAttribute(attributes, "version", software->softwareParamVersion);
            return Status::Ok;
        }

        throw runtime_error(("[IO::HandlerSoftware] Unexpected element name: " + name).c_str());
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
    attributes.push_back(make_pair("id", instrumentConfiguration.id));
    writer.startElement("instrumentConfiguration", attributes);

    writeParamContainer(writer, instrumentConfiguration);
    write(writer, instrumentConfiguration.componentList);

    if (instrumentConfiguration.softwarePtr.get())
    {
        attributes.clear();
        attributes.push_back(make_pair("ref", instrumentConfiguration.softwarePtr->id));
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
            getAttribute(attributes, "id", instrumentConfiguration->id);
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
            getAttribute(attributes, "ref", ref);
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
    attributes.push_back(make_pair("order", lexical_cast<string>(processingMethod.order)));
    writer.startElement("processingMethod", attributes);
    writeParamContainer(writer, processingMethod);
    writer.endElement();
}

    
struct HandlerProcessingMethod : public HandlerParamContainer
{
    ProcessingMethod* processingMethod;

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
    attributes.push_back(make_pair("id", lexical_cast<string>(dataProcessing.id)));
    if (dataProcessing.softwarePtr.get())
        attributes.push_back(make_pair("softwareRef", dataProcessing.softwarePtr->id)); 

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
            getAttribute(attributes, "id", dataProcessing->id);

            // note: placeholder
            string softwareRef;
            getAttribute(attributes, "softwareRef", softwareRef);
            if (!softwareRef.empty())
                dataProcessing->softwarePtr = SoftwarePtr(new Software(softwareRef));

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
// AcquisitionSettings
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AcquisitionSettings& acquisitionSettings)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", lexical_cast<string>(acquisitionSettings.id)));
    if (acquisitionSettings.instrumentConfigurationPtr.get())
        attributes.push_back(make_pair("instrumentConfigurationRef", acquisitionSettings.instrumentConfigurationPtr->id)); 

    writer.startElement("acquisitionSettings", attributes);

    if (!acquisitionSettings.sourceFilePtrs.empty()) 
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(acquisitionSettings.sourceFilePtrs.size())));
        writer.startElement("sourceFileRefList", attributes);
        for (vector<SourceFilePtr>::const_iterator it=acquisitionSettings.sourceFilePtrs.begin(); 
             it!=acquisitionSettings.sourceFilePtrs.end(); ++it)
             writeSourceFileRef(writer, **it);
        writer.endElement(); // sourceFileRefList
    }

    if (!acquisitionSettings.targets.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.push_back(make_pair("count", lexical_cast<string>(acquisitionSettings.targets.size())));
        writer.startElement("targetList", attributes);

        for (vector<Target>::const_iterator it=acquisitionSettings.targets.begin(); 
             it!=acquisitionSettings.targets.end(); ++it)
             write(writer, *it);

        writer.endElement(); // targetList
    }

    writer.endElement();
}

    
struct HandlerAcquisitionSettings : public HandlerParamContainer
{
    AcquisitionSettings* acquisitionSettings;

    HandlerAcquisitionSettings(AcquisitionSettings* _acquisitionSettings = 0)
    :   acquisitionSettings(_acquisitionSettings), 
        handlerTarget_("target")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!acquisitionSettings)
            throw runtime_error("[IO::HandlerAcquisitionSettings] Null acquisitionSettings.");

        if (name == "acquisitionSettings")
        {
            getAttribute(attributes, "id", acquisitionSettings->id);

            // note: placeholder
            string instrumentConfigurationRef;
            getAttribute(attributes, "instrumentConfigurationRef", instrumentConfigurationRef);
            if (!instrumentConfigurationRef.empty())
                acquisitionSettings->instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration(instrumentConfigurationRef));

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
            getAttribute(attributes, "ref", sourceFileRef);
            if (!sourceFileRef.empty())
                acquisitionSettings->sourceFilePtrs.push_back(SourceFilePtr(new SourceFile(sourceFileRef)));
            return Status::Ok;
     }
        else if (name=="target")
        {
            acquisitionSettings->targets.push_back(Target());
            handlerTarget_.paramContainer = &acquisitionSettings->targets.back();
            return Status(Status::Delegate, &handlerTarget_);
        }

        throw runtime_error(("[IO::HandlerAcquisitionSettings] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerNamedParamContainer handlerTarget_;
};


PWIZ_API_DECL void read(std::istream& is, AcquisitionSettings& acquisitionSettings)
{
    HandlerAcquisitionSettings handler(&acquisitionSettings);
    SAXParser::parse(is, handler);
}


//
// Acquisition
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Acquisition& acquisition)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("number", lexical_cast<string>(acquisition.number)));

    if (acquisition.sourceFilePtr.get())
        attributes.push_back(make_pair("sourceFileRef", acquisition.sourceFilePtr->id)); 

    if (!acquisition.spectrumID.empty())
        attributes.push_back(make_pair("spectrumRef", acquisition.spectrumID));

    writer.startElement("acquisition", attributes);

    writeParamContainer(writer, acquisition);
    
    writer.endElement();
}

    
struct HandlerAcquisition : public HandlerParamContainer
{
    Acquisition* acquisition;

    HandlerAcquisition(Acquisition* _acquisition = 0)
    :   acquisition(_acquisition)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!acquisition)
            throw runtime_error("[IO::HandlerAcquisition] Null acquisition.");

        if (name == "acquisition")
        {
            getAttribute(attributes, "number", acquisition->number);
            getAttribute(attributes, "spectrumRef", acquisition->spectrumID);

            // note: placeholder
            string sourceFileRef;
            getAttribute(attributes, "sourceFileRef", sourceFileRef);
            if (!sourceFileRef.empty())
                acquisition->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = acquisition;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerProcessingMethod handlerProcessingMethod_;
};


PWIZ_API_DECL void read(std::istream& is, Acquisition& acquisition)
{
    HandlerAcquisition handler(&acquisition);
    SAXParser::parse(is, handler);
}


//
// AcquisitionList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AcquisitionList& acquisitionList)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(acquisitionList.acquisitions.size())));
    writer.startElement("acquisitionList", attributes);
    writeParamContainer(writer, acquisitionList);
    
    for (vector<Acquisition>::const_iterator it=acquisitionList.acquisitions.begin(); 
         it!=acquisitionList.acquisitions.end(); ++it)
         write(writer, *it);
    
    writer.endElement();
}

    
struct HandlerAcquisitionList : public HandlerParamContainer
{
    AcquisitionList* acquisitionList;

    HandlerAcquisitionList(AcquisitionList* _acquisitionList = 0)
    :   acquisitionList(_acquisitionList)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!acquisitionList)
            throw runtime_error("[IO::HandlerAcquisitionList] Null acquisitionList.");

        if (name == "acquisitionList")
        {
            return Status::Ok;
        }
        else if (name == "acquisition")
        {
            acquisitionList->acquisitions.push_back(Acquisition());
            handlerAcquisition_.acquisition = &acquisitionList->acquisitions.back(); 
            return Status(Status::Delegate, &handlerAcquisition_);
        }

        HandlerParamContainer::paramContainer = acquisitionList;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerAcquisition handlerAcquisition_;
};


PWIZ_API_DECL void read(std::istream& is, AcquisitionList& acquisitionList)
{
    HandlerAcquisitionList handler(&acquisitionList);
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
    attributes.push_back(make_pair("spectrumRef", lexical_cast<string>(precursor.spectrumID)));
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
        attributes.push_back(make_pair("count", lexical_cast<string>(precursor.selectedIons.size())));
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

    HandlerPrecursor(Precursor* _precursor = 0)
    :   precursor(_precursor), 
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
            getAttribute(attributes, "spectrumRef", precursor->spectrumID);
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


PWIZ_API_DECL void read(std::istream& is, Precursor& precursor)
{
    HandlerPrecursor handler(&precursor);
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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Scan& scan)
{
    XMLWriter::Attributes attributes;
    if (scan.instrumentConfigurationPtr.get())
        attributes.push_back(make_pair("instrumentConfigurationRef", lexical_cast<string>(scan.instrumentConfigurationPtr->id)));
    writer.startElement("scan", attributes);
    writeParamContainer(writer, scan);
    
    if (!scan.scanWindows.empty())
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(scan.scanWindows.size())));
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

        if (name == "scan")
        {
            // note: placeholder
            string instrumentConfigurationRef;
            getAttribute(attributes, "instrumentConfigurationRef", instrumentConfigurationRef);
            if (!instrumentConfigurationRef.empty())
                scan->instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration(instrumentConfigurationRef));
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
// SpectrumDescription
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumDescription& spectrumDescription)
{
    writer.startElement("spectrumDescription");
    writeParamContainer(writer, spectrumDescription);
    
    if (!spectrumDescription.acquisitionList.empty())
        write(writer, spectrumDescription.acquisitionList);

    if (!spectrumDescription.precursors.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.push_back(make_pair("count", lexical_cast<string>(spectrumDescription.precursors.size())));
        writer.startElement("precursorList", attributes);
        
        for (vector<Precursor>::const_iterator it=spectrumDescription.precursors.begin(); 
             it!=spectrumDescription.precursors.end(); ++it)
             write(writer, *it);
     
        writer.endElement();
    }

    if (!spectrumDescription.scan.empty())
        write(writer, spectrumDescription.scan);

    writer.endElement();
}

    
struct HandlerSpectrumDescription : public HandlerParamContainer
{
    SpectrumDescription* spectrumDescription;

    HandlerSpectrumDescription(SpectrumDescription* _spectrumDescription = 0)
    :   spectrumDescription(_spectrumDescription)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumDescription)
            throw runtime_error("[IO::HandlerSpectrumDescription] Null spectrumDescription.");

        if (name == "spectrumDescription")
        {
            return Status::Ok;
        }
        else if (name == "acquisitionList")
        {
            handlerAcquisitionList_.acquisitionList = &spectrumDescription->acquisitionList;
            return Status(Status::Delegate, &handlerAcquisitionList_);
        }
        else if (name == "precursorList")
        {
            return Status::Ok;
        }
        else if (name == "precursor")
        {
            spectrumDescription->precursors.push_back(Precursor());
            handlerPrecursor_.precursor = &spectrumDescription->precursors.back();
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "scan")
        {
            handlerScan_.scan = &spectrumDescription->scan;
            return Status(Status::Delegate, &handlerScan_);
        }

        HandlerParamContainer::paramContainer = spectrumDescription;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerAcquisitionList handlerAcquisitionList_;
    HandlerPrecursor handlerPrecursor_;
    HandlerScan handlerScan_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumDescription& spectrumDescription)
{
    HandlerSpectrumDescription handler(&spectrumDescription);
    SAXParser::parse(is, handler);
}


//
// BinaryData
//

PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const BinaryDataArray& binaryDataArray,
           const BinaryDataEncoder::Config& config)
{
    BinaryDataEncoder::Config usedConfig = config;
    map<CVID, BinaryDataEncoder::Precision>::const_iterator overrideItr = config.precisionOverrides.find(binaryDataArray.cvParamChild(MS_binary_data_array).cvid);
    if (overrideItr != config.precisionOverrides.end())
        usedConfig.precision = overrideItr->second;

    BinaryDataEncoder encoder(usedConfig);
    string encoded;
    encoder.encode(binaryDataArray.data, encoded);

    XMLWriter::Attributes attributes;

    // primary array types can never override the default array length
    if (!binaryDataArray.hasCVParam(MS_m_z_array) &&
        !binaryDataArray.hasCVParam(MS_time_array) &&
        !binaryDataArray.hasCVParam(MS_intensity_array))
    {
        attributes.push_back(make_pair("arrayLength", lexical_cast<string>(binaryDataArray.data.size())));
    }

    attributes.push_back(make_pair("encodedLength", lexical_cast<string>(encoded.size())));
    if (binaryDataArray.dataProcessingPtr.get())
        attributes.push_back(make_pair("dataProcessingRef", binaryDataArray.dataProcessingPtr->id));

    writer.startElement("binaryDataArray", attributes);

    if (usedConfig.precision == BinaryDataEncoder::Precision_32)
        write(writer, MS_32_bit_float);
    else
        write(writer, MS_64_bit_float);

    if (usedConfig.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian)
        throw runtime_error("[IO::writeConfig()] mzML: must use little endian encoding.");

    if (usedConfig.compression == BinaryDataEncoder::Compression_None)
        write(writer, MS_no_compression);
    else if (config.compression == BinaryDataEncoder::Compression_Zlib)
        write(writer, MS_zlib_compression);
    else
        throw runtime_error("[IO::writeConfig()] Unsupported compression method.");

    writeParamContainer(writer, binaryDataArray);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("binary");
    writer.characters(encoded);
    writer.endElement();
    writer.popStyle();

    writer.endElement();
}


struct HandlerBinaryDataArray : public HandlerParamContainer
{
    BinaryDataArray* binaryDataArray;
    size_t defaultArrayLength;

    HandlerBinaryDataArray(BinaryDataArray* _binaryDataArray = 0)
    :   binaryDataArray(_binaryDataArray),
        defaultArrayLength(0),
        arrayLength_(0),
        encodedLength_(0)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!binaryDataArray)
            throw runtime_error("[IO::HandlerBinaryDataArray] Null binaryDataArray.");

        if (name == "binaryDataArray")
        {
            // note: placeholder
            string dataProcessingRef;
            getAttribute(attributes, "dataProcessingRef", dataProcessingRef);
            if (!dataProcessingRef.empty())
                binaryDataArray->dataProcessingPtr = DataProcessingPtr(new DataProcessing(dataProcessingRef));

            arrayLength_ = defaultArrayLength;
            encodedLength_ = 0;
            getAttribute(attributes, "arrayLength", arrayLength_);
            getAttribute(attributes, "encodedLength", encodedLength_);

            return Status::Ok;
        }
        else if (name == "binary")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = binaryDataArray;
        return HandlerParamContainer::startElement(name, attributes, position);
    }


    virtual Status characters(const std::string& text,
                              stream_offset position)
    {
        if (!binaryDataArray)
            throw runtime_error("[IO::HandlerBinaryDataArray] Null binaryDataArray."); 

        BinaryDataEncoder::Config config = getConfig();
        BinaryDataEncoder encoder(config);
        encoder.decode(text, binaryDataArray->data); 

        if (binaryDataArray->data.size() != arrayLength_)
            throw runtime_error("[IO::HandlerBinaryDataArray] Array lengths differ."); 

        if (text.size() != encodedLength_)
            throw runtime_error("[IO::HandlerBinaryDataArray] Encoded lengths differ."); 

        return Status::Ok;
    }

    private:

    size_t arrayLength_;
    size_t encodedLength_;

    CVID extractCVParam(CVID cvid)
    {
        if (!binaryDataArray)
            throw runtime_error("[IO::HandlerBinaryDataArray] Null binaryDataArray."); 

        vector<CVParam>& params = binaryDataArray->cvParams;
        vector<CVParam>::iterator it = find_if(params.begin(), params.end(), 
                                               CVParamIsChildOf(cvid));
        if (it == params.end())
            throw runtime_error("[IO::HandlerBinaryDataArray] Missing " + cvinfo(cvid).name);
        
        CVID result = it->cvid;
        params.erase(it);
        return result;
    }

    BinaryDataEncoder::Config getConfig()
    {
        BinaryDataEncoder::Config config;

        //
        // Note: these two CVParams are really info about the encoding, and not 
        // part of the BinaryDataArray.  We look at them to see how to decode the data,
        // and remove them from the BinaryDataArray struct.
        //

        CVID cvidBinaryDataType = extractCVParam(MS_binary_data_type);
        CVID cvidCompressionType = extractCVParam(MS_binary_data_compression_type);

        switch (cvidBinaryDataType)
        {
            case MS_32_bit_float:
                config.precision = BinaryDataEncoder::Precision_32;
                break;
            case MS_64_bit_float:
                config.precision = BinaryDataEncoder::Precision_64;
                break;
            default:
                throw runtime_error("[IO::HandlerBinaryDataArray] Unknown binary data type.");
        }

        switch (cvidCompressionType)
        {
            case MS_no_compression:
                config.compression = BinaryDataEncoder::Compression_None;
                break;
            case MS_zlib_compression:
                config.compression = BinaryDataEncoder::Compression_Zlib;
                break;
            default:
                throw runtime_error("[IO::HandlerBinaryDataArray] Unknown compression.");
        }

        return config;
    }
};


PWIZ_API_DECL void read(std::istream& is, BinaryDataArray& binaryDataArray)
{
    HandlerBinaryDataArray handler(&binaryDataArray);
    SAXParser::parse(is, handler);
}


//
// Spectrum
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Spectrum& spectrum, 
           const BinaryDataEncoder::Config& config)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("index", lexical_cast<string>(spectrum.index)));
    attributes.push_back(make_pair("id", spectrum.id));
    attributes.push_back(make_pair("nativeID", spectrum.nativeID));
    attributes.push_back(make_pair("defaultArrayLength", lexical_cast<string>(spectrum.defaultArrayLength)));
    if (spectrum.dataProcessingPtr.get())
        attributes.push_back(make_pair("dataProcessingRef", spectrum.dataProcessingPtr->id));
    if (spectrum.sourceFilePtr.get())
        attributes.push_back(make_pair("sourceFileRef", spectrum.sourceFilePtr->id));

    writer.startElement("spectrum", attributes);

    writeParamContainer(writer, spectrum);
    write(writer, spectrum.spectrumDescription);
    
    if (!spectrum.binaryDataArrayPtrs.empty())
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(spectrum.binaryDataArrayPtrs.size())));
        writer.startElement("binaryDataArrayList", attributes);

        for (vector<BinaryDataArrayPtr>::const_iterator it=spectrum.binaryDataArrayPtrs.begin(); 
             it!=spectrum.binaryDataArrayPtrs.end(); ++it)
             write(writer, **it, config);

        writer.endElement(); // binaryDataArrayList
    }

    writer.endElement(); // spectrum
}

    
struct HandlerSpectrum : public HandlerParamContainer
{
    BinaryDataFlag binaryDataFlag;
    Spectrum* spectrum;

    HandlerSpectrum(BinaryDataFlag _binaryDataFlag,
                    Spectrum* _spectrum = 0)
    :   binaryDataFlag(_binaryDataFlag),
        spectrum(_spectrum)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrum)
            throw runtime_error("[IO::HandlerSpectrum] Null spectrum.");

        if (name == "spectrum")
        {
            spectrum->sourceFilePosition = position;

            getAttribute(attributes, "id", spectrum->id);
            getAttribute(attributes, "index", spectrum->index);
            getAttribute(attributes, "nativeID", spectrum->nativeID);
            getAttribute(attributes, "defaultArrayLength", spectrum->defaultArrayLength);

            // note: placeholder
            string dataProcessingRef;
            getAttribute(attributes, "dataProcessingRef", dataProcessingRef);
            if (!dataProcessingRef.empty())
                spectrum->dataProcessingPtr = DataProcessingPtr(new DataProcessing(dataProcessingRef));

            // note: placeholder
            string sourceFileRef;
            getAttribute(attributes, "sourceFileRef", sourceFileRef);
            if (!sourceFileRef.empty())
                spectrum->sourceFilePtr = SourceFilePtr(new SourceFile(sourceFileRef));

            return Status::Ok;
        }
        else if (name == "spectrumDescription")
        {
            handlerSpectrumDescription_.spectrumDescription = &spectrum->spectrumDescription;
            return Status(Status::Delegate, &handlerSpectrumDescription_);
        }
        else if (name == "binaryDataArray")
        {
            if (binaryDataFlag == IgnoreBinaryData)
                return Status::Done;

            spectrum->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray()));
            handlerBinaryDataArray_.binaryDataArray = spectrum->binaryDataArrayPtrs.back().get();
            handlerBinaryDataArray_.defaultArrayLength = spectrum->defaultArrayLength;
            return Status(Status::Delegate, &handlerBinaryDataArray_);
        }
        else if (name == "binaryDataArrayList")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = spectrum;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerSpectrumDescription handlerSpectrumDescription_;
    HandlerBinaryDataArray handlerBinaryDataArray_;
};


PWIZ_API_DECL void read(std::istream& is, Spectrum& spectrum,
          BinaryDataFlag binaryDataFlag)
{
    HandlerSpectrum handler(binaryDataFlag, &spectrum);
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
    attributes.push_back(make_pair("index", lexical_cast<string>(chromatogram.index)));
    attributes.push_back(make_pair("id", chromatogram.id));
    attributes.push_back(make_pair("nativeID", chromatogram.nativeID));
    attributes.push_back(make_pair("defaultArrayLength", lexical_cast<string>(chromatogram.defaultArrayLength)));
    if (chromatogram.dataProcessingPtr.get())
        attributes.push_back(make_pair("dataProcessingRef", chromatogram.dataProcessingPtr->id));

    writer.startElement("chromatogram", attributes);

    writeParamContainer(writer, chromatogram);
    
    if (!chromatogram.binaryDataArrayPtrs.empty())
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(chromatogram.binaryDataArrayPtrs.size())));
        writer.startElement("binaryDataArrayList", attributes);

        for (vector<BinaryDataArrayPtr>::const_iterator it=chromatogram.binaryDataArrayPtrs.begin(); 
             it!=chromatogram.binaryDataArrayPtrs.end(); ++it)
             write(writer, **it, config);

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

            getAttribute(attributes, "id", chromatogram->id);
            getAttribute(attributes, "index", chromatogram->index);
            getAttribute(attributes, "nativeID", chromatogram->nativeID);
            getAttribute(attributes, "defaultArrayLength", chromatogram->defaultArrayLength);

            // note: placeholder
            string dataProcessingRef;
            getAttribute(attributes, "dataProcessingRef", dataProcessingRef);
            if (!dataProcessingRef.empty())
                chromatogram->dataProcessingPtr = DataProcessingPtr(new DataProcessing(dataProcessingRef));

            return Status::Ok;
        }
        else if (name == "binaryDataArray")
        {
            if (binaryDataFlag == IgnoreBinaryData)
                return Status::Done;

            chromatogram->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray()));
            handlerBinaryDataArray_.binaryDataArray = chromatogram->binaryDataArrayPtrs.back().get();
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
void write(minimxml::XMLWriter& writer, const SpectrumList& spectrumList,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(spectrumList.size())));
    writer.startElement("spectrumList", attributes);

    for (size_t i=0; i<spectrumList.size(); i++)
    {
        if (spectrumPositions)
            spectrumPositions->push_back(writer.positionNext());
        SpectrumPtr spectrum = spectrumList.spectrum(i, true);
        if (spectrum->index != i) throw runtime_error("[IO::write(SpectrumList)] Bad index.");
        write(writer, *spectrum, config);
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
            return Status::Ok;
        }
        else if (name == "spectrum")
        {
            spectrumListSimple->spectra.push_back(SpectrumPtr(new Spectrum));
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
           vector<boost::iostreams::stream_offset>* chromatogramPositions)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("count", lexical_cast<string>(chromatogramList.size())));
    writer.startElement("chromatogramList", attributes);

    for (size_t i=0; i<chromatogramList.size(); i++)
    {
        if (chromatogramPositions)
            chromatogramPositions->push_back(writer.positionNext());
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
void write(minimxml::XMLWriter& writer, const Run& run,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions,
           vector<boost::iostreams::stream_offset>* chromatogramPositions)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", run.id));
    if (run.defaultInstrumentConfigurationPtr.get())
        attributes.push_back(make_pair("defaultInstrumentConfigurationRef", run.defaultInstrumentConfigurationPtr->id));
    if (run.samplePtr.get())
        attributes.push_back(make_pair("sampleRef", run.samplePtr->id));
    if (!run.startTimeStamp.empty())
        attributes.push_back(make_pair("startTimeStamp", run.startTimeStamp));

    writer.startElement("run", attributes);

    writeParamContainer(writer, run);

    if (!run.sourceFilePtrs.empty()) 
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(run.sourceFilePtrs.size())));
        writer.startElement("sourceFileRefList", attributes);
        for (vector<SourceFilePtr>::const_iterator it=run.sourceFilePtrs.begin(); 
             it!=run.sourceFilePtrs.end(); ++it)
             writeSourceFileRef(writer, **it);
        writer.endElement();
    }

    if (run.spectrumListPtr.get())
        write(writer, *run.spectrumListPtr, config, spectrumPositions);

    if (run.chromatogramListPtr.get())
        write(writer, *run.chromatogramListPtr, config, chromatogramPositions);

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
            getAttribute(attributes, "id", run->id);
            getAttribute(attributes, "startTimeStamp", run->startTimeStamp);

            // note: placeholder
            string defaultInstrumentConfigurationRef;
            getAttribute(attributes, "defaultInstrumentConfigurationRef", defaultInstrumentConfigurationRef);
            if (!defaultInstrumentConfigurationRef.empty())
                run->defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration(defaultInstrumentConfigurationRef));

            // note: placeholder
            string sampleRef;
            getAttribute(attributes, "sampleRef", sampleRef);
            if (!sampleRef.empty())
                run->samplePtr = SamplePtr(new Sample(sampleRef));

            return Status::Ok;
        }
        else if (name == "sourceFileRefList")
        {
            return Status::Ok;
        }
        else if (name == "sourceFileRef")
        {
            // note: placeholder
            string sourceFileRef;
            getAttribute(attributes, "ref", sourceFileRef);
            if (!sourceFileRef.empty())
                run->sourceFilePtrs.push_back(SourceFilePtr(new SourceFile(sourceFileRef)));
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


template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objectPtrs, 
               const string& label)
{
    if (!objectPtrs.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.push_back(make_pair("count", lexical_cast<string>(objectPtrs.size())));
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objectPtrs.begin(); it!=objectPtrs.end(); ++it)
            write(writer, **it);
        writer.endElement();
    }
}


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const MSData& msd,
           const BinaryDataEncoder::Config& config,
           vector<boost::iostreams::stream_offset>* spectrumPositions,
           vector<boost::iostreams::stream_offset>* chromatogramPositions)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("xmlns", "http://psi.hupo.org/schema_revision/mzML_0.99.11"));
    attributes.push_back(make_pair("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"));
    attributes.push_back(make_pair("xsi:schemaLocation", "http://psi.hupo.org/schema_revision/mzML_0.99.11 mzML0.99.11.xsd"));
    if (!msd.accession.empty())
        attributes.push_back(make_pair("accession", msd.accession));
    attributes.push_back(make_pair("id", msd.id));
    attributes.push_back(make_pair("version", msd.version));

    writer.startElement("mzML", attributes);

    if (!msd.cvs.empty())
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(msd.cvs.size())));
        writer.startElement("cvList", attributes);
        for (vector<CV>::const_iterator it=msd.cvs.begin(); it!=msd.cvs.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }

    write(writer, msd.fileDescription);

    writeList(writer, msd.paramGroupPtrs, "referenceableParamGroupList");
    writeList(writer, msd.samplePtrs, "sampleList");
    writeList(writer, msd.instrumentConfigurationPtrs, "instrumentConfigurationList");
    writeList(writer, msd.softwarePtrs, "softwareList");
    writeList(writer, msd.dataProcessingPtrs, "dataProcessingList");
    writeList(writer, msd.acquisitionSettingsPtrs, "acquisitionSettingsList");

    write(writer, msd.run, config, spectrumPositions, chromatogramPositions);

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
            getAttribute(attributes, "id", msd->id);
            getAttribute(attributes, "version", msd->version);
            return Status::Ok;
        }
        else if (name == "cvList" || 
                 name == "referenceableParamGroupList" ||
                 name == "sampleList" || 
                 name == "instrumentConfigurationList" || 
                 name == "softwareList" ||
                 name == "dataProcessingList" ||
                 name == "acquisitionSettingsList")
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
            handlerSoftware_.software = msd->softwarePtrs.back().get();
            return Status(Status::Delegate, &handlerSoftware_);
        }        
        else if (name == "dataProcessing")
        {
            msd->dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing));            
            handlerDataProcessing_.dataProcessing = msd->dataProcessingPtrs.back().get();
            return Status(Status::Delegate, &handlerDataProcessing_);
        }
        else if (name == "acquisitionSettings")
        {
            msd->acquisitionSettingsPtrs.push_back(AcquisitionSettingsPtr(new AcquisitionSettings));            
            handlerAcquisitionSettings_.acquisitionSettings = msd->acquisitionSettingsPtrs.back().get();
            return Status(Status::Delegate, &handlerAcquisitionSettings_);
        }
        else if (name == "run")
        {
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
    HandlerAcquisitionSettings handlerAcquisitionSettings_;
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


