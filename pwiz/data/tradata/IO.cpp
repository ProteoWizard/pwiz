//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

namespace pwiz {
namespace tradata {
namespace IO {


using namespace minimxml;
using namespace minimxml::SAXParser;
//using namespace util;


static const int TRAML_VERSION_PRERELEASE = 0;
static const int TRAML_VERSION_1_0 = 1;


template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objects, 
               const string& label, bool writeCountAttribute = false)
{
    if (!objects.empty())
    {
        XMLWriter::Attributes attributes;
        if (writeCountAttribute)
            attributes.push_back(make_pair("count", lexical_cast<string>(objects.size())));
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objects.begin(); it!=objects.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }
}

template <typename object_type>
void writePtrList(minimxml::XMLWriter& writer, const vector<object_type>& objectPtrs, 
                  const string& label, bool writeCountAttribute = false)
{
    if (!objectPtrs.empty())
    {
        XMLWriter::Attributes attributes;
        if (writeCountAttribute)
            attributes.push_back(make_pair("count", lexical_cast<string>(objectPtrs.size())));
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objectPtrs.begin(); it!=objectPtrs.end(); ++it)
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
        attributes.push_back(make_pair("unitAccession", cvTermInfo(userParam.units).id));
        attributes.push_back(make_pair("unitName", cvTermInfo(userParam.units).name));
    }

    writer.startElement("UserParam", attributes, XMLWriter::EmptyElement);
}


struct HandlerUserParam : public SAXParser::Handler
{
    UserParam* userParam;
    HandlerUserParam(UserParam* _userParam = 0) : userParam(_userParam) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "UserParam")
            throw runtime_error(("[IO::HandlerUserParam] Unexpected element name: " + name).c_str());

        if (!userParam)
            throw runtime_error("[IO::HandlerUserParam] Null UserParam.");

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
    attributes.push_back(make_pair("cvRef", cvTermInfo(cvParam.cvid).prefix()));
    attributes.push_back(make_pair("accession", cvTermInfo(cvParam.cvid).id));
    attributes.push_back(make_pair("name", cvTermInfo(cvParam.cvid).name));
    attributes.push_back(make_pair("value", cvParam.value));
    if (cvParam.units != CVID_Unknown)
    {
        attributes.push_back(make_pair("unitCvRef", cvTermInfo(cvParam.units).prefix()));
        attributes.push_back(make_pair("unitAccession", cvTermInfo(cvParam.units).id));
        attributes.push_back(make_pair("unitName", cvTermInfo(cvParam.units).name));
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
            cvParam->cvid = cvTermInfo(accession).cvid;

        getAttribute(attributes, "value", cvParam->value);

        string unitAccession;
        getAttribute(attributes, "unitAccession", unitAccession);
        if (!unitAccession.empty())
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


PWIZ_API_DECL void writeParamContainer(minimxml::XMLWriter& writer, const ParamContainer& pc)
{
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
        else if (name == "UserParam")
        {
            paramContainer->userParams.push_back(UserParam()); 
            handlerUserParam_.userParam = &paramContainer->userParams.back();
            return Status(Status::Delegate, &handlerUserParam_);
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
// Contact
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Contact& c)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", c.id));
    writer.startElement("Contact", attributes);
    writeParamContainer(writer, c);
    writer.endElement();
}


//
// Publication
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Publication& p)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", p.id));
    writer.startElement("Publication", attributes);
    writeParamContainer(writer, p);
    writer.endElement();
}


//
// Interpretation
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Interpretation& x)
{
    writer.startElement("Interpretation");
    writeParamContainer(writer, x);
    writer.endElement();
}


//
// RetentionTime
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const RetentionTime& x)
{
    XMLWriter::Attributes attributes;

    if (x.softwarePtr.get())
    {
        attributes.push_back(make_pair("softwareRef", x.softwarePtr->id));
    }
    writer.startElement("RetentionTime", attributes);
    writeParamContainer(writer, x);
    writer.endElement();
}


//
// Configuration
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Configuration& x)
{
    XMLWriter::Attributes attributes;

    if (x.contactPtr.get() && !x.contactPtr->empty())
        attributes.push_back(make_pair("contactRef", x.contactPtr->id));
    if (x.instrumentPtr.get() && !x.instrumentPtr->empty())
        attributes.push_back(make_pair("instrumentRef", x.instrumentPtr->id));

    writer.startElement("Configuration", attributes);
    writeParamContainer(writer, x);
    for (size_t i=0; i < x.validations.size(); ++i)
    {
        const Validation& v = x.validations[i];
        writer.startElement("Validation");
        writeParamContainer(writer, v);
        writer.endElement();
    }
    writer.endElement();
}


struct HandlerConfiguration : public HandlerParamContainer
{
    Configuration* configuration;

    HandlerConfiguration(Configuration* configuration = 0) 
    :   configuration(configuration), handlerValidation_("Validation")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!configuration)
            throw runtime_error("[IO::HandlerConfiguration] Null configuration.");

        if (name == "Configuration")
        {
            // note: placeholder
            string contactRef;
            getAttribute(attributes, "contactRef", contactRef);
            if (!contactRef.empty())
                configuration->contactPtr = ContactPtr(new Contact(contactRef));
        
             // note: placeholder
            string instrumentRef;
            getAttribute(attributes, "instrumentRef", instrumentRef);
            if (!instrumentRef.empty())
                configuration->instrumentPtr = InstrumentPtr(new Instrument(instrumentRef));

            return Status::Ok;
        }
        else if (name == "Validation")
        {
            configuration->validations.push_back(Validation());
            handlerValidation_.paramContainer = &configuration->validations.back();
            return Status(Status::Delegate, &handlerValidation_);
        }

        HandlerParamContainer::paramContainer = configuration;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerNamedParamContainer handlerValidation_;
};


PWIZ_API_DECL void read(std::istream& is, Configuration& x)
{
    HandlerConfiguration handler(&x);
    SAXParser::parse(is, handler);
}


//
// Prediction
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Prediction& x)
{
    XMLWriter::Attributes attributes;

    if (x.contactPtr.get() && !x.contactPtr->empty())
        attributes.push_back(make_pair("contactRef", x.contactPtr->id));
    if (x.softwarePtr.get() && !x.softwarePtr->empty())
        attributes.push_back(make_pair("softwareRef", x.softwarePtr->id));

    writer.startElement("Prediction", attributes);
    writeParamContainer(writer, x);
    writer.endElement();
}


struct HandlerPrediction : public HandlerParamContainer
{
    Prediction* prediction;

    HandlerPrediction(Prediction* prediction = 0) 
    :   prediction(prediction)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!prediction)
            throw runtime_error("[IO::HandlerPrediction] Null prediction.");

        if (name == "Prediction")
        {
            // note: placeholder
            string contactRef;
            getAttribute(attributes, "contactRef", contactRef);
            if (!contactRef.empty())
                prediction->contactPtr = ContactPtr(new Contact(contactRef));
        
             // note: placeholder
            string softwareRef;
            getAttribute(attributes, "softwareRef", softwareRef);
            if (!softwareRef.empty())
                prediction->softwarePtr = SoftwarePtr(new Software(softwareRef));

            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = prediction;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Prediction& x)
{
    HandlerPrediction handler(&x);
    SAXParser::parse(is, handler);
}


//
// Instrument
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Instrument& instrument)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", instrument.id));
    writer.startElement("Instrument", attributes);
    writeParamContainer(writer, instrument);
    writer.endElement();
}


struct HandlerInstrument : public HandlerParamContainer
{
    Instrument* instrument;

    HandlerInstrument(Instrument* instrument = 0) : instrument(instrument) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!instrument)
            throw runtime_error("[IO::HandlerInstrument] Null instrument.");

        if (name == "Instrument")
        {
            getAttribute(attributes, "id", instrument->id);
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = instrument;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Instrument& instrument)
{
    HandlerInstrument handler(&instrument);
    SAXParser::parse(is, handler);
}


//
// Software
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Software& software)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", software.id));
    attributes.push_back(make_pair("version", software.version));
    writer.startElement("Software", attributes);
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
            throw runtime_error("[IO::HandlerSoftware] Null Software.");

        if (name == "Software")
        {
            getAttribute(attributes, "id", software->id);
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
// Protein
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinPtr& proteinPtr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", proteinPtr->id));

    writer.startElement("Protein", attributes);
    writeParamContainer(writer, *proteinPtr);
    if (!proteinPtr->sequence.empty())
    {
        writer.startElement("Sequence");
        writer.characters(proteinPtr->sequence);
        writer.endElement();
    }
    writer.endElement();
}

    
struct HandlerProtein : public HandlerParamContainer
{
    Protein* protein;

    HandlerProtein(Protein* protein = 0)
    :   protein(protein)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!protein)
            throw runtime_error("[IO::HandlerProtein] Null Protein.");

        if (name == "Protein")
        {
            getAttribute(attributes, "id", protein->id);

            return Status::Ok;
        }
        else if (name == "Sequence")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = protein;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        protein->sequence = text.c_str();
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Protein& protein)
{
    HandlerProtein handler(&protein);
    SAXParser::parse(is, handler);
}


//
// Protein
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& modification)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("location", lexical_cast<string>(modification.location)));
    attributes.push_back(make_pair("monoisotopicMassDelta", lexical_cast<string>(modification.monoisotopicMassDelta)));
    attributes.push_back(make_pair("averageMassDelta", lexical_cast<string>(modification.averageMassDelta)));

    writer.startElement("Modification", attributes);
    writeParamContainer(writer, modification);
    writer.endElement();
}

    
struct HandlerModification : public HandlerParamContainer
{
    Modification* modification;

    HandlerModification(Modification* modification = 0)
    :   modification(modification)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!modification)
            throw runtime_error("[IO::HandlerModification] Null modification.");

        if (name == "Modification")
        {
            getAttribute(attributes, "location", modification->location);
            getAttribute(attributes, "monoisotopicMassDelta", modification->monoisotopicMassDelta);
            getAttribute(attributes, "averageMassDelta", modification->averageMassDelta);

            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = modification;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Modification& modification)
{
    HandlerModification handler(&modification);
    SAXParser::parse(is, handler);
}


//
// Peptide
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptidePtr& peptidePtr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", peptidePtr->id));
    attributes.push_back(make_pair("sequence", peptidePtr->sequence));

    writer.startElement("Peptide", attributes);
    writeParamContainer(writer, *peptidePtr);

    BOOST_FOREACH(const ProteinPtr& p, peptidePtr->proteinPtrs)
    {
        attributes.clear();
        attributes.push_back(make_pair("ref", p->id));
        writer.startElement("ProteinRef", attributes, XMLWriter::EmptyElement);
    }

    BOOST_FOREACH(const Modification& m, peptidePtr->modifications)
    {
        write(writer, m);
    }

    writeList(writer, peptidePtr->retentionTimes, "RetentionTimeList");

    if (!peptidePtr->evidence.empty())
    {
        writer.startElement("Evidence");
        writeParamContainer(writer, peptidePtr->evidence);
        writer.endElement();
    }

    writer.endElement();
}

    
struct HandlerPeptide : public HandlerParamContainer
{
    Peptide* peptide;

    HandlerPeptide(Peptide* peptide = 0)
    :   peptide(peptide), handlerModification_("Modification"), handlerRetentionTime_("RetentionTime")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!peptide)
            throw runtime_error("[IO::HandlerPeptide] Null peptide.");

        if (name == "Peptide")
        {
            getAttribute(attributes, "id", peptide->id);
            getAttribute(attributes, "sequence", peptide->sequence);

            return Status::Ok;
        }
        else if (name == "ProteinRef")
        {
            // note: placeholder
            string proteinRef;
            getAttribute(attributes, "ref", proteinRef);
            if (!proteinRef.empty())
                peptide->proteinPtrs.push_back(ProteinPtr(new Protein(proteinRef)));
            return Status::Ok;
        }
        else if (name == "Modification")
        {
            peptide->modifications.push_back(Modification());
            getAttribute(attributes, "location", peptide->modifications.back().location);
            getAttribute(attributes, "monoisotopicMassDelta", peptide->modifications.back().monoisotopicMassDelta);
            getAttribute(attributes, "averageMassDelta", peptide->modifications.back().averageMassDelta);
            handlerModification_.paramContainer = &peptide->modifications.back();
            return Status(Status::Delegate, &handlerModification_);
        }
        else if (name == "RetentionTime")
        {
            peptide->retentionTimes.push_back(RetentionTime());

            // note: placeholder
            string softwareRef;
            getAttribute(attributes, "softwareRef", softwareRef);
            if (!softwareRef.empty())
                peptide->retentionTimes.back().softwarePtr = SoftwarePtr(new Software(softwareRef));

            handlerRetentionTime_.paramContainer = &peptide->retentionTimes.back();
            return Status(Status::Delegate, &handlerRetentionTime_);
        }
        else if (name == "Evidence")
        {
            HandlerParamContainer::paramContainer = &peptide->evidence;
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = peptide;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    HandlerNamedParamContainer handlerModification_;
    HandlerNamedParamContainer handlerRetentionTime_;
};


PWIZ_API_DECL void read(std::istream& is, Peptide& peptide)
{
    HandlerPeptide handler(&peptide);
    SAXParser::parse(is, handler);
}


//
// Compound
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CompoundPtr& compoundPtr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", compoundPtr->id));

    writer.startElement("Compound", attributes);
    writeParamContainer(writer, *compoundPtr);

    writeList(writer, compoundPtr->retentionTimes, "RetentionTimeList");

    writer.endElement();
}

    
struct HandlerCompound : public HandlerParamContainer
{
    Compound* compound;

    HandlerCompound(Compound* compound = 0)
    :   compound(compound), handlerRetentionTime_("RetentionTime")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!compound)
            throw runtime_error("[IO::HandlerCompound] Null compound.");

        if (name == "Compound")
        {
            getAttribute(attributes, "id", compound->id);

            return Status::Ok;
        }
        else if (name == "RetentionTime")
        {
            compound->retentionTimes.push_back(RetentionTime());

            // note: placeholder
            string softwareRef;
            getAttribute(attributes, "softwareRef", softwareRef);
            if (!softwareRef.empty())
                compound->retentionTimes.back().softwarePtr = SoftwarePtr(new Software(softwareRef));

            handlerRetentionTime_.paramContainer = &compound->retentionTimes.back();
            return Status(Status::Delegate, &handlerRetentionTime_);
        }

        HandlerParamContainer::paramContainer = compound;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerNamedParamContainer handlerRetentionTime_;
};


PWIZ_API_DECL void read(std::istream& is, Compound& compound)
{
    HandlerCompound handler(&compound);
    SAXParser::parse(is, handler);
}


//
// Transition
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Transition& transition)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", transition.id));
    if (transition.peptidePtr.get())
        attributes.push_back(make_pair("peptideRef", transition.peptidePtr->id));
    if (transition.compoundPtr.get())
        attributes.push_back(make_pair("compoundRef", transition.compoundPtr->id));

    writer.startElement("Transition", attributes);
    writeParamContainer(writer, transition);

    writer.startElement("Precursor");
    writeParamContainer(writer, transition.precursor);
    writer.endElement();

    writer.startElement("Product");
    writeParamContainer(writer, transition.product);
    writer.endElement();

    if (!transition.retentionTime.empty())
    {
        writer.startElement("RetentionTime");
        writeParamContainer(writer, transition.retentionTime);
        writer.endElement();
    }

    if (!transition.prediction.empty())
        write(writer, transition.prediction);

    writeList(writer, transition.interpretationList, "InterpretationList");
    writeList(writer, transition.configurationList, "ConfigurationList");

    writer.endElement();
}

    
struct HandlerTransition : public HandlerParamContainer
{
    Transition* transition;

    HandlerTransition(Transition* _transition = 0)
    :   transition(_transition),
        handlerInterpretation_("Interpretation"),
        handlerPrecursor_("Precursor"),
        handlerProduct_("Product"),
        handlerRetentionTime_("RetentionTime")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!transition)
            throw runtime_error("[IO::HandlerTransition] Null transition.");

        if (name == "Transition")
        {
            getAttribute(attributes, "id", transition->id);

            // note: placeholder
            string peptideRef;
            getAttribute(attributes, "peptideRef", peptideRef);
            if (!peptideRef.empty())
                transition->peptidePtr = PeptidePtr(new Peptide(peptideRef));

            // note: placeholder
            string compoundRef;
            getAttribute(attributes, "compoundRef", compoundRef);
            if (!compoundRef.empty())
                transition->compoundPtr = CompoundPtr(new Compound(compoundRef));

            return Status::Ok;
        }
        else if (name == "InterpretationList" ||
                 name == "ConfigurationList")
        {
            return Status::Ok;
        }
        else if (name == "Interpretation")
        {
            transition->interpretationList.push_back(Interpretation());
            handlerInterpretation_.paramContainer = &transition->interpretationList.back();
            return Status(Status::Delegate, &handlerInterpretation_);
        }
        else if (name == "Configuration")
        {
            transition->configurationList.push_back(Configuration());
            handlerConfiguration_.configuration = &transition->configurationList.back();
            return Status(Status::Delegate, &handlerConfiguration_);
        }
        else if (name == "Precursor")
        {
            handlerPrecursor_.paramContainer = &transition->precursor;
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "Product")
        {
            handlerProduct_.paramContainer = &transition->product;
            return Status(Status::Delegate, &handlerProduct_);
        }
        else if (name == "Prediction")
        {
            handlerPrediction_.prediction = &transition->prediction;
            return Status(Status::Delegate, &handlerPrediction_);
        }
        else if (name == "RetentionTime")
        {
            handlerRetentionTime_.paramContainer = &transition->retentionTime;
            return Status(Status::Delegate, &handlerRetentionTime_);
        }

        HandlerParamContainer::paramContainer = transition;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerNamedParamContainer handlerInterpretation_;
    HandlerConfiguration handlerConfiguration_;
    HandlerNamedParamContainer handlerPrecursor_;
    HandlerNamedParamContainer handlerProduct_;
    HandlerPrediction handlerPrediction_;
    HandlerNamedParamContainer handlerRetentionTime_;
};


PWIZ_API_DECL
void read(std::istream& is, Transition& transition)
{
    HandlerTransition handler(&transition);
    SAXParser::parse(is, handler);
}


//
// Target
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const Target& target)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", target.id));
    if (target.peptidePtr.get())
        attributes.push_back(make_pair("peptideRef", target.peptidePtr->id));
    if (target.compoundPtr.get())
        attributes.push_back(make_pair("compoundRef", target.compoundPtr->id));

    writer.startElement("Target", attributes);
    writeParamContainer(writer, target);

    writer.startElement("Precursor");
    writeParamContainer(writer, target.precursor);
    writer.endElement();

    if (!target.retentionTime.empty())
    {
        writer.startElement("RetentionTime");
        writeParamContainer(writer, target.retentionTime);
        writer.endElement();
    }

    writeList(writer, target.configurationList, "ConfigurationList");

    writer.endElement();
}

    
struct HandlerTarget : public HandlerParamContainer
{
    Target* target;

    HandlerTarget(Target* _target = 0)
    :   target(_target),
        handlerPrecursor_("Precursor"),
        handlerRetentionTime_("RetentionTime")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!target)
            throw runtime_error("[IO::HandlerTarget] Null target.");

        if (name == "Target")
        {
            getAttribute(attributes, "id", target->id);

            // note: placeholder
            string peptideRef;
            getAttribute(attributes, "peptideRef", peptideRef);
            if (!peptideRef.empty())
                target->peptidePtr = PeptidePtr(new Peptide(peptideRef));

            // note: placeholder
            string compoundRef;
            getAttribute(attributes, "compoundRef", compoundRef);
            if (!compoundRef.empty())
                target->compoundPtr = CompoundPtr(new Compound(compoundRef));

            return Status::Ok;
        }
        else if (name == "ConfigurationList")
        {
            return Status::Ok;
        }
        else if (name == "Configuration")
        {
            target->configurationList.push_back(Configuration());
            handlerConfiguration_.configuration = &target->configurationList.back();
            return Status(Status::Delegate, &handlerConfiguration_);
        }
        else if (name == "Precursor")
        {
            handlerPrecursor_.paramContainer = &target->precursor;
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "RetentionTime")
        {
            handlerRetentionTime_.paramContainer = &target->retentionTime;
            return Status(Status::Delegate, &handlerRetentionTime_);
        }

        HandlerParamContainer::paramContainer = target;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerConfiguration handlerConfiguration_;
    HandlerNamedParamContainer handlerPrecursor_;
    HandlerNamedParamContainer handlerRetentionTime_;
};


PWIZ_API_DECL
void read(std::istream& is, Target& target)
{
    HandlerTarget handler(&target);
    SAXParser::parse(is, handler);
}


//
// TargetList
//


struct HandlerTargetList : public HandlerParamContainer
{
    TargetList* targets;

    HandlerTargetList(TargetList* _targets = 0)
    :   targets(_targets)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!targets)
            throw runtime_error("[IO::HandlerTargetList] Null target.");

        if (name == "TargetList")
        {
            return Status::Ok;
        }
        else if (name == "TargetExcludeList")
        {
            excludeTargets_ = true;
            return Status::Ok;
        }
        else if (name == "TargetIncludeList")
        {
            excludeTargets_ = false;
            return Status::Ok;
        }
        else if (name == "Target")
        {
            if (excludeTargets_)
            {
                targets->targetExcludeList.push_back(Target());
                handlerTarget_.target = &targets->targetExcludeList.back();
            }
            else
            {
                targets->targetIncludeList.push_back(Target());
                handlerTarget_.target = &targets->targetIncludeList.back();
            }
            return Status(Status::Delegate, &handlerTarget_);
        }

        HandlerParamContainer::paramContainer = targets;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    bool excludeTargets_;
    HandlerTarget handlerTarget_;
};


//
// TraData
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const TraData& td)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("xmlns", "http://psi.hupo.org/ms/traml"));
    attributes.push_back(make_pair("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"));
    attributes.push_back(make_pair("xsi:schemaLocation", "http://psi.hupo.org/ms/traml http://www.peptideatlas.org/tmp/TraML/" + td.version() + "/TraML" + td.version() + ".xsd"));
    attributes.push_back(make_pair("version", td.version()));

    writer.startElement("TraML", attributes);

    writeList(writer, td.cvs, "cvList");

    writePtrList(writer, td.contactPtrs, "ContactList");
    writeList(writer, td.publications, "PublicationList");
    writePtrList(writer, td.instrumentPtrs, "InstrumentList");
    writePtrList(writer, td.softwarePtrs, "SoftwareList");
    writeList(writer, td.proteinPtrs, "ProteinList");

    if (!td.peptidePtrs.empty() || !td.compoundPtrs.empty())
    {
        writer.startElement("CompoundList");
        for (vector<PeptidePtr>::const_iterator it=td.peptidePtrs.begin(); it!=td.peptidePtrs.end(); ++it)
            write(writer, *it);
        for (vector<CompoundPtr>::const_iterator it=td.compoundPtrs.begin(); it!=td.compoundPtrs.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }

    writeList(writer, td.transitions, "TransitionList");

    writer.startElement("TargetList");
    writeParamContainer(writer, td.targets);
    writeList(writer, td.targets.targetIncludeList, "TargetIncludeList");
    writeList(writer, td.targets.targetExcludeList, "TargetExcludeList");
    writer.endElement();

    writer.endElement();
}


struct HandlerTraData : public SAXParser::Handler
{
    TraData* td;

    HandlerTraData(TraData* td = 0) 
    :  td(td),
       handlerContact_("Contact"),
       handlerPublication_("Publication")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!td)
            throw runtime_error("[IO::HandlerTraData] Null td."); 

        if (name == "TraML")
        {
            // "http://psi.hupo.org/ms/traml http://psidev.info/files/ms/traML/xsd/traML<version>.xsd"
            string schemaLocation;
            getAttribute(attributes, "xsi:schemaLocation", schemaLocation);
            if (schemaLocation.empty())
                getAttribute(attributes, "version", td->version_); // fallback
            else
            {
                schemaLocation = schemaLocation.substr(schemaLocation.find(' ')+1);
                string xsdName = BFS_STRING(bfs::path(schemaLocation).filename());
                td->version_ = xsdName.substr(5, xsdName.length()-9); // read between "traML" and ".xsd"
            }

            version = TRAML_VERSION_PRERELEASE;
            //if (td->version_.find("1.0") == 0)
            //    version = TRAML_VERSION_1_0;

            return Status::Ok;
        }
        else if (name == "cvList" || 
                 name == "ContactList" || 
                 name == "PublicationList" || 
                 name == "InstrumentList" ||
                 name == "SoftwareList" ||
                 name == "ProteinList" ||
                 name == "CompoundList" ||
                 name == "TransitionList")
        {
            // ignore these, unless we want to validate the count attribute
            return Status::Ok;
        }
        else if (name == "cv")
        {
            td->cvs.push_back(CV()); 
            handlerCV_.cv = &td->cvs.back();
            return Status(Status::Delegate, &handlerCV_);
        }
        else if (name == "Contact")
        {
            td->contactPtrs.push_back(ContactPtr(new Contact));
            getAttribute(attributes, "id", td->contactPtrs.back()->id);
            handlerContact_.paramContainer = td->contactPtrs.back().get();
            return Status(Status::Delegate, &handlerContact_);
        }
        else if (name == "Publication")
        {
            td->publications.push_back(Publication());
            getAttribute(attributes, "id", td->publications.back().id);
            handlerPublication_.paramContainer = &td->publications.back();
            return Status(Status::Delegate, &handlerPublication_);
        }
        else if (name == "Instrument")
        {
            td->instrumentPtrs.push_back(InstrumentPtr(new Instrument));
            handlerInstrument_.instrument = td->instrumentPtrs.back().get();
            return Status(Status::Delegate, &handlerInstrument_);
        }
        else if (name == "Software")
        {
            td->softwarePtrs.push_back(SoftwarePtr(new Software));            
            handlerSoftware_.software = td->softwarePtrs.back().get();
            return Status(Status::Delegate, &handlerSoftware_);
        }        
        else if (name == "Protein")
        {
            td->proteinPtrs.push_back(ProteinPtr(new Protein));            
            handlerProtein_.protein = td->proteinPtrs.back().get();
            return Status(Status::Delegate, &handlerProtein_);
        }
        else if (name == "Peptide")
        {
            td->peptidePtrs.push_back(PeptidePtr(new Peptide));
            handlerPeptide_.peptide = td->peptidePtrs.back().get();
            return Status(Status::Delegate, &handlerPeptide_);
        }
        else if (name == "Compound")
        {
            td->compoundPtrs.push_back(CompoundPtr(new Compound));
            handlerCompound_.compound = td->compoundPtrs.back().get();
            return Status(Status::Delegate, &handlerCompound_);
        }
        else if (name == "Transition")
        {
            td->transitions.push_back(Transition());
            handlerTransition_.transition = &td->transitions.back();
            return Status(Status::Delegate, &handlerTransition_);
        }
        else if (name == "TargetList")
        {
            handlerTargetList_.targets = &td->targets;
            return Status(Status::Delegate, &handlerTargetList_);
        }

        throw runtime_error(("[IO::HandlerTraData] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerCV handlerCV_;
    HandlerNamedParamContainer handlerContact_;
    HandlerNamedParamContainer handlerPublication_;
    HandlerInstrument handlerInstrument_;
    HandlerSoftware handlerSoftware_;
    HandlerProtein handlerProtein_;
    HandlerPeptide handlerPeptide_;
    HandlerCompound handlerCompound_;
    HandlerTransition handlerTransition_;
    HandlerTargetList handlerTargetList_;
};


PWIZ_API_DECL
void read(std::istream& is, TraData& td)
{
    HandlerTraData handler(&td);
    SAXParser::parse(is, handler);
    References::resolve(td); 
}


} // namespace IO
} // namespace tradata
} // namespace pwiz
