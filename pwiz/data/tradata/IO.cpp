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
#include "boost/lexical_cast.hpp"
#include <stdexcept>
#include <functional>
#include <iostream>


namespace pwiz {
namespace tradata {
namespace IO {


using namespace std;
using namespace minimxml;
using namespace minimxml::SAXParser;
//using namespace util;
using boost::lexical_cast;
using boost::shared_ptr;


template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objects, 
               const string& label)
{
    if (!objects.empty())
    {
        XMLWriter::Attributes attributes;
        attributes.push_back(make_pair("count", lexical_cast<string>(objects.size())));
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objects.begin(); it!=objects.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }
}

template <typename object_type>
void writePtrList(minimxml::XMLWriter& writer, const vector<object_type>& objectPtrs, 
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
        else if (name == "userParam")
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
    writer.startElement("contact", attributes);
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
    writer.startElement("publication", attributes);
    writeParamContainer(writer, p);
    writer.endElement();
}


//
// Interpretation
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Interpretation& x)
{
    XMLWriter::Attributes attributes;
    if (!x.productSeries.empty())
        attributes.push_back(make_pair("productSeries", x.productSeries));
    attributes.push_back(make_pair("productOrdinal", lexical_cast<string>(x.productOrdinal)));
    if (!x.productAdjustment.empty())
        attributes.push_back(make_pair("productAdjustment", x.productAdjustment));
    attributes.push_back(make_pair("mzDelta", lexical_cast<string>(x.mzDelta)));
    attributes.push_back(make_pair("primary", lexical_cast<string>(x.primary)));
    writer.startElement("interpretation", attributes);
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
    if (x.contactPtr.get() && !x.contactPtr->empty())
        attributes.push_back(make_pair("instrumentRef", x.instrumentPtr->id));

    writer.startElement("configuration", attributes);
    writeParamContainer(writer, x);
    for (size_t i=0; i < x.validations.size(); ++i)
    {
        const Validation& v = x.validations[i];
        attributes.clear();
        if (!v.transitionSource.empty())
            attributes.push_back(make_pair("transitionSource", v.transitionSource));
        attributes.push_back(make_pair("recommendedTransitionRank", lexical_cast<string>(v.recommendedTransitionRank)));
        attributes.push_back(make_pair("relativeIntensity", lexical_cast<string>(v.relativeIntensity)));
        attributes.push_back(make_pair("intensityRank", lexical_cast<string>(v.intensityRank)));
        writer.startElement("validation", attributes);
        writer.endElement();
    }
    writer.endElement();
}


//
// RetentionTime
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const RetentionTime& x)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("normalizationStandard", x.normalizationStandard));
    attributes.push_back(make_pair("normalizedRetentionTime", lexical_cast<string>(x.normalizedRetentionTime)));
    attributes.push_back(make_pair("localRetentionTime", lexical_cast<string>(x.localRetentionTime)));
    attributes.push_back(make_pair("predictedRetentionTime", lexical_cast<string>(x.predictedRetentionTime)));
    if (x.predictedRetentionTimeSoftwarePtr.get())
    {
        attributes.push_back(make_pair("predictedRetentionTimeSoftwareRef", x.predictedRetentionTimeSoftwarePtr->id));
    }
    writer.startElement("retentionTime", attributes);
    writeParamContainer(writer, x);
    writer.endElement();
}


struct HandlerRetentionTime : public HandlerParamContainer
{
    RetentionTime* retentionTime;

    HandlerRetentionTime(RetentionTime* retentionTime = 0) 
    :   retentionTime(retentionTime)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!retentionTime)
            throw runtime_error("[IO::HandlerRetentionTime] Null retentionTime.");

        if (name == "retentionTime")
        {
            getAttribute(attributes, "normalizationStandard", retentionTime->normalizationStandard);
            getAttribute(attributes, "normalizedRetentionTime", retentionTime->normalizedRetentionTime);
            getAttribute(attributes, "localRetentionTime", retentionTime->localRetentionTime);
            getAttribute(attributes, "predictedRetentionTime", retentionTime->predictedRetentionTime);

            string ref;
            getAttribute(attributes, "predictedRetentionTimeSoftwareRef", ref);
            if (!ref.empty())
                retentionTime->predictedRetentionTimeSoftwarePtr = SoftwarePtr(new Software(ref));

            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = retentionTime;
        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, RetentionTime& x)
{
    HandlerRetentionTime handler(&x);
    SAXParser::parse(is, handler);
}


//
// Instrument
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Instrument& instrument)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", instrument.id));
    writer.startElement("instrument", attributes);
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

        if (name == "instrument")
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
    if (!proteinPtr->name.empty())
        attributes.push_back(make_pair("name", proteinPtr->name));
    if (!proteinPtr->accession.empty())
        attributes.push_back(make_pair("accession", proteinPtr->accession));
    if (!proteinPtr->description.empty())
        attributes.push_back(make_pair("description", proteinPtr->description));
    if (!proteinPtr->comment.empty())
        attributes.push_back(make_pair("comment", proteinPtr->comment));

    writer.startElement("protein", attributes);
    writeParamContainer(writer, *proteinPtr);
    if (!proteinPtr->sequence.empty())
    {
        writer.startElement("sequence");
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
            throw runtime_error("[IO::HandlerProtein] Null protein.");

        if (name == "protein")
        {
            getAttribute(attributes, "id", protein->id);
            getAttribute(attributes, "name", protein->name);
            getAttribute(attributes, "accession", protein->accession);
            getAttribute(attributes, "description", protein->description);
            getAttribute(attributes, "comment", protein->comment);

            return Status::Ok;
        }
        else if (name == "sequence")
        {
            return Status::Ok;
        }

        HandlerParamContainer::paramContainer = protein;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    virtual Status characters(const string& text, stream_offset position)
    {
        protein->sequence = text;
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Protein& protein)
{
    HandlerProtein handler(&protein);
    SAXParser::parse(is, handler);
}


//
// Peptide
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptidePtr& peptidePtr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", peptidePtr->id));
    if (!peptidePtr->groupLabel.empty())
        attributes.push_back(make_pair("groupLabel", peptidePtr->groupLabel));
    if (!peptidePtr->unmodifiedSequence.empty())
        attributes.push_back(make_pair("unmodifiedSequence", peptidePtr->unmodifiedSequence));
    if (!peptidePtr->modifiedSequence.empty())
        attributes.push_back(make_pair("modifiedSequence", peptidePtr->modifiedSequence));
    if (!peptidePtr->labelingCategory.empty())
        attributes.push_back(make_pair("labelingCategory", peptidePtr->labelingCategory));
    if (peptidePtr->proteinPtr.get())
        attributes.push_back(make_pair("proteinRef", peptidePtr->proteinPtr->id));

    writer.startElement("peptide", attributes);
    writeParamContainer(writer, *peptidePtr);
    write(writer, peptidePtr->retentionTime);
    writer.endElement();
}

    
struct HandlerPeptide : public HandlerParamContainer
{
    Peptide* peptide;

    HandlerPeptide(Peptide* peptide = 0)
    :   peptide(peptide)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!peptide)
            throw runtime_error("[IO::HandlerPeptide] Null peptide.");

        if (name == "peptide")
        {
            getAttribute(attributes, "id", peptide->id);
            getAttribute(attributes, "groupLabel", peptide->groupLabel);
            getAttribute(attributes, "unmodifiedSequence", peptide->unmodifiedSequence);
            getAttribute(attributes, "modifiedSequence", peptide->modifiedSequence);
            getAttribute(attributes, "labelingCategory", peptide->labelingCategory);

            // note: placeholder
            string proteinRef;
            getAttribute(attributes, "proteinRef", proteinRef);
            if (!proteinRef.empty())
                peptide->proteinPtr = ProteinPtr(new Protein(proteinRef));

            return Status::Ok;
        }
        else if (name == "retentionTime")
        {
            handlerRetentionTime_.retentionTime = &peptide->retentionTime;
            return Status(Status::Delegate, &handlerRetentionTime_);
        }

        HandlerParamContainer::paramContainer = peptide;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerRetentionTime handlerRetentionTime_;
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

    writer.startElement("compound", attributes);
    writeParamContainer(writer, *compoundPtr);
    write(writer, compoundPtr->retentionTime);
    writer.endElement();
}

    
struct HandlerCompound : public HandlerParamContainer
{
    Compound* compound;

    HandlerCompound(Compound* compound = 0)
    :   compound(compound)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!compound)
            throw runtime_error("[IO::HandlerCompound] Null compound.");

        if (name == "compound")
        {
            getAttribute(attributes, "id", compound->id);

            return Status::Ok;
        }
        else if (name == "retentionTime")
        {
            handlerRetentionTime_.retentionTime = &compound->retentionTime;
            return Status(Status::Delegate, &handlerRetentionTime_);
        }

        HandlerParamContainer::paramContainer = compound;
        return HandlerParamContainer::startElement(name, attributes, position);
    }

    private:
    HandlerRetentionTime handlerRetentionTime_;
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
    attributes.push_back(make_pair("name", transition.name));
    if (transition.peptidePtr.get())
        attributes.push_back(make_pair("peptideRef", transition.peptidePtr->id));
    if (transition.compoundPtr.get())
        attributes.push_back(make_pair("compoundRef", transition.compoundPtr->id));

    writer.startElement("transition", attributes);

    attributes.clear();
    attributes.push_back(make_pair("mz", lexical_cast<string>(transition.precursor.mz)));
    attributes.push_back(make_pair("charge", lexical_cast<string>(transition.precursor.charge)));
    writer.startElement("precursor", attributes);
    writer.endElement();

    attributes.clear();
    attributes.push_back(make_pair("mz", lexical_cast<string>(transition.product.mz)));
    attributes.push_back(make_pair("charge", lexical_cast<string>(transition.product.charge)));
    writer.startElement("product", attributes);
    writer.endElement();

    writeList(writer, transition.interpretationList, "interpretationList");
    writeList(writer, transition.configurationList, "configurationList");

    writer.endElement();
}

    
struct HandlerTransition : public SAXParser::Handler
{
    Transition* transition;

    HandlerTransition(Transition* _transition = 0)
    :   transition(_transition)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!transition)
            throw runtime_error("[IO::HandlerTransition] Null transition.");

        if (name == "transition")
        {
            getAttribute(attributes, "name", transition->name);

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
        }
        else if (name == "interpretationList" ||
                 name == "configurationList")
        {
        }
        else if (name == "interpretation")
        {
            transition->interpretationList.push_back(Interpretation());
            getAttribute(attributes, "productSeries", transition->interpretationList.back().productSeries);
            getAttribute(attributes, "productOrdinal", transition->interpretationList.back().productOrdinal);
            getAttribute(attributes, "productAdjustment", transition->interpretationList.back().productAdjustment);
            getAttribute(attributes, "mzDelta", transition->interpretationList.back().mzDelta);
            getAttribute(attributes, "primary", transition->interpretationList.back().primary);
        }
        else if (name == "configuration")
        {
            transition->configurationList.push_back(Configuration());

            // note: placeholder
            string contactRef;
            getAttribute(attributes, "contactRef", contactRef);
            if (!contactRef.empty())
                transition->configurationList.back().contactPtr = ContactPtr(new Contact(contactRef));
        
             // note: placeholder
            string instrumentRef;
            getAttribute(attributes, "instrumentRef", instrumentRef);
            if (!instrumentRef.empty())
                transition->configurationList.back().instrumentPtr = InstrumentPtr(new Instrument(instrumentRef));
        }
        else if (name == "validation")
        {
            transition->configurationList.back().validations.push_back(Validation());
            getAttribute(attributes, "recommendedTransitionRank", transition->configurationList.back().validations.back().recommendedTransitionRank);
            getAttribute(attributes, "transitionSource", transition->configurationList.back().validations.back().transitionSource);
            getAttribute(attributes, "relativeIntensity", transition->configurationList.back().validations.back().relativeIntensity);
            getAttribute(attributes, "intensityRank", transition->configurationList.back().validations.back().intensityRank);
        }
        else if (name == "precursor")
        {
            getAttribute(attributes, "mz", transition->precursor.mz);
            getAttribute(attributes, "charge", transition->product.charge);
        }
        else if (name == "product")
        {
            getAttribute(attributes, "mz", transition->product.mz);
            getAttribute(attributes, "charge", transition->product.charge);
        }
        else if (name == "prediction")
        {
            getAttribute(attributes, "recommendedTransitionRank", transition->prediction.recommendedTransitionRank);
            getAttribute(attributes, "transitionSource", transition->prediction.transitionSource);
            getAttribute(attributes, "relativeIntensity", transition->prediction.relativeIntensity);
            getAttribute(attributes, "intensityRank", transition->prediction.intensityRank);

            // note: placeholder
            string softwareRef;
            getAttribute(attributes, "softwareRef", softwareRef);
            if (!softwareRef.empty())
                transition->prediction.softwarePtr = SoftwarePtr(new Software(softwareRef));

            // note: placeholder
            string contactRef;
            getAttribute(attributes, "contactRef", contactRef);
            if (!contactRef.empty())
                transition->prediction.contactPtr = ContactPtr(new Contact(contactRef));
        }
        else
            throw runtime_error("[IO::HandlerTransition] unknown element \"" + name + "\"");

        return Status::Ok;
    }
};


PWIZ_API_DECL
void read(std::istream& is, Transition& transition)
{
    HandlerTransition handler(&transition);
    SAXParser::parse(is, handler);
}


//
// TraData
//


PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const TraData& td)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("xmlns", "http://psi.hupo.org/ms/mzml"));
    attributes.push_back(make_pair("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"));
    attributes.push_back(make_pair("xsi:schemaLocation", "http://psi.hupo.org/ms/traml http://www.peptideatlas.org/tmp/TraML/0.2/TraML0.2.xsd"));
    attributes.push_back(make_pair("version", td.version));

    writer.startElement("TraML", attributes);

    writeList(writer, td.cvs, "cvList");

    writePtrList(writer, td.contactPtrs, "contactList");
    writeList(writer, td.publications, "publicationList");
    writePtrList(writer, td.instrumentPtrs, "instrumentList");
    writePtrList(writer, td.softwarePtrs, "softwareList");
    writeList(writer, td.proteinPtrs, "proteinList");

    if (!td.peptidePtrs.empty() || !td.compoundPtrs.empty())
    {
        attributes.clear();
        attributes.push_back(make_pair("count", lexical_cast<string>(td.peptidePtrs.size() + td.compoundPtrs.size())));
        writer.startElement("compoundList", attributes);
        for (vector<PeptidePtr>::const_iterator it=td.peptidePtrs.begin(); it!=td.peptidePtrs.end(); ++it)
            write(writer, *it);
        for (vector<CompoundPtr>::const_iterator it=td.compoundPtrs.begin(); it!=td.compoundPtrs.end(); ++it)
            write(writer, *it);
        writer.endElement();
    }

    writeList(writer, td.transitions, "transitionList");

    writer.endElement();
}


struct HandlerTraData : public SAXParser::Handler
{
    TraData* td;

    HandlerTraData(TraData* td = 0) 
    :  td(td), handlerContact_("contact"), handlerPublication_("publication") 
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!td)
            throw runtime_error("[IO::HandlerTraData] Null td."); 

        if (name == "TraML")
        {
            getAttribute(attributes, "version", td->version);
            return Status::Ok;
        }
        else if (name == "cvList" || 
                 name == "contactList" || 
                 name == "publicationList" || 
                 name == "instrumentList" ||
                 name == "softwareList" ||
                 name == "proteinList" ||
                 name == "compoundList" ||
                 name == "transitionList")
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
        else if (name == "contact")
        {
            td->contactPtrs.push_back(ContactPtr(new Contact));
            getAttribute(attributes, "id", td->contactPtrs.back()->id);
            handlerContact_.paramContainer = td->contactPtrs.back().get();
            return Status(Status::Delegate, &handlerContact_);
        }
        else if (name == "publication")
        {
            td->publications.push_back(Publication());
            getAttribute(attributes, "id", td->publications.back().id);
            handlerPublication_.paramContainer = &td->publications.back();
            return Status(Status::Delegate, &handlerPublication_);
        }
        else if (name == "instrument")
        {
            td->instrumentPtrs.push_back(InstrumentPtr(new Instrument));
            handlerInstrument_.instrument = td->instrumentPtrs.back().get();
            return Status(Status::Delegate, &handlerInstrument_);
        }
        else if (name == "software")
        {
            td->softwarePtrs.push_back(SoftwarePtr(new Software));            
            handlerSoftware_.software = td->softwarePtrs.back().get();
            return Status(Status::Delegate, &handlerSoftware_);
        }        
        else if (name == "protein")
        {
            td->proteinPtrs.push_back(ProteinPtr(new Protein));            
            handlerProtein_.protein = td->proteinPtrs.back().get();
            return Status(Status::Delegate, &handlerProtein_);
        }
        else if (name == "peptide")
        {
            td->peptidePtrs.push_back(PeptidePtr(new Peptide));
            handlerPeptide_.peptide = td->peptidePtrs.back().get();
            return Status(Status::Delegate, &handlerPeptide_);
        }
        else if (name == "compound")
        {
            td->compoundPtrs.push_back(CompoundPtr(new Compound));
            handlerCompound_.compound = td->compoundPtrs.back().get();
            return Status(Status::Delegate, &handlerCompound_);
        }
        else if (name == "transition")
        {
            td->transitions.push_back(Transition());
            handlerTransition_.transition = &td->transitions.back();
            return Status(Status::Delegate, &handlerTransition_);
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


