//
// IO.cpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "boost/lexical_cast.hpp"
#include <stdexcept>
#include <functional>
#include <iostream>


namespace pwiz {
namespace mziddata {
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
        //attributes.push_back(make_pair("count", lexical_cast<string>(objects.size())));
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
        //attributes.push_back(make_pair("count", lexical_cast<string>(objectPtrs.size())));
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
// addIdAttributes
//
// Adds attributes for IdentifiableType child classes.
void addIdAttributes(const IdentifiableType& id, XMLWriter::Attributes& attributes)
{
    attributes.push_back(make_pair("id", id.id));
    if (!id.name.empty())
        attributes.push_back(make_pair("name", id.name));
}

//
// BibliographicReference
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& br)
{
    XMLWriter::Attributes attributes;
    writer.startElement("BibliographicReference", attributes, XMLWriter::EmptyElement);
    //writer.endElement();
}


struct HandlerBibliographicReference : public SAXParser::Handler
{
    BibliographicReference* br;
    HandlerBibliographicReference(BibliographicReference* _br = 0) : br(_br) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "BibliographicReference")
            throw runtime_error(("[IO::HandlerBibliographicReference] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, BibliographicReference& br)
{
    HandlerBibliographicReference handler(&br);
    SAXParser::parse(is, handler);
}


//
// DBSequence
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequencePtr ds)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", ds->id));
    attributes.push_back(make_pair("length", ds->length));
    attributes.push_back(make_pair("accession", ds->accession));
    attributes.push_back(make_pair("SearchDatabase_ref", ds->SearchDatabase_ref));
    
    writer.startElement("DBSequence", attributes);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("seq");
    writer.characters(ds->seq);
    writer.endElement();
    writer.popStyle();

    writeParamContainer(writer, ds->paramGroup);
    writer.endElement();
}


struct HandlerDBSequence : public SAXParser::Handler
{
    DBSequence* ds;
    HandlerDBSequence(DBSequence* _ds = 0) : ds(_ds) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "DBSequence")
            throw runtime_error(("[IO::HandlerDBSequence] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, DBSequencePtr ds)
{
    // TODO throw exception if pointer is NULL
    HandlerDBSequence handler(ds.get());
    SAXParser::parse(is, handler);
}


//
// SequenceCollection
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SequenceCollection& sc)
{
    XMLWriter::Attributes attributes;
    writer.startElement("SequenceCollection", attributes);
    for(vector<DBSequencePtr>::const_iterator it=sc.dbSequences.begin(); it!=sc.dbSequences.end(); it++)
        write(writer, *it);
    // TODO write out Peptides.
    writer.endElement();
}


struct HandlerSequenceCollection : public SAXParser::Handler
{
    SequenceCollection* sc;
    HandlerSequenceCollection(SequenceCollection* _sc = 0) : sc(_sc) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "SequenceCollection")
            throw runtime_error(("[IO::HandlerSequenceCollection] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, SequenceCollection& sc)
{
    HandlerSequenceCollection handler(&sc);
    SAXParser::parse(is, handler);
}


//
// Affiliations
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         const Affiliations& affiliations)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("Organization_ref", affiliations.organization_ref));

    writer.startElement("affiliations", attributes, XMLWriter::EmptyElement);
}


struct HandlerAffiliations : public SAXParser::Handler
{
    Affiliations* aff;
    HandlerAffiliations(Affiliations* _aff = 0) : aff(_aff) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "affiliations")
            throw runtime_error(("[IO::HandlerAffiliations] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Affiliations& aff)
{
    HandlerAffiliations handler(&aff);
    SAXParser::parse(is, handler);
}


//
// Person
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         XMLWriter::Attributes attributes,
                         const Person* pp)
{
    writer.startElement("Person", attributes);
    for(vector<Affiliations>::const_iterator it=pp->affiliations.begin();
        it != pp->affiliations.end();
        it++)
        write(writer, *it);
    writer.endElement();
}


struct HandlerPerson : public SAXParser::Handler
{
    Person* per;
    HandlerPerson(Person* _per = 0) : per(_per) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "Person")
            throw runtime_error(("[IO::HandlerPerson] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Person* pp)
{
    // TODO add throw if pointer is NULL
    HandlerPerson handler(pp);
    SAXParser::parse(is, handler);
}


//
// Organization
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         XMLWriter::Attributes attributes,
                         const Organization* op)
{
    writer.startElement("Organization", attributes);
    writer.endElement();
}


struct HandlerOrganization : public SAXParser::Handler
{
    Organization* org;
    HandlerOrganization(Organization* _org = 0) : org(_org) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "Organization")
            throw runtime_error(("[IO::HandlerOrganization] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Organization* op)
{
    // TODO add throw if pointer is NULL
    HandlerOrganization handler(op);
    SAXParser::parse(is, handler);
}

//
// Contact
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactPtr cp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(*cp, attributes);
    if (!cp->address.empty())
        attributes.push_back(make_pair("address", cp->address));
    if (!cp->phone.empty())
        attributes.push_back(make_pair("phone", cp->phone));
    if (!cp->email.empty())
        attributes.push_back(make_pair("email", cp->email));
    if (!cp->fax.empty())
        attributes.push_back(make_pair("fax", cp->fax));
    if (!cp->tollFreePhone.empty())
        attributes.push_back(make_pair("tollFreePhone", cp->tollFreePhone));

    if (dynamic_cast<Person*>(cp.get()) != NULL)
    {
        write(writer, attributes, (const Person*)cp.get());
    }
    if (dynamic_cast<Organization*>(cp.get())!= NULL)
    {
        write(writer, attributes, (const Organization*)cp.get());
    }
}


struct HandlerContact : public SAXParser::Handler
{
    Contact* ct;
    HandlerContact(Contact* _ct = 0) : ct(_ct) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Person")
        {
            // Delegate to Person handler
        }
        else if (name == "Organization")
        {
            // Delegate to Organization handler
        }
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ContactPtr cp)
{
    // TODO add throw if pointer DNE
    HandlerContact handler(cp.get());
    SAXParser::parse(is, handler);
}


//
// ContactRole
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactRole& cr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("Contact_ref", cr.Contact_ref));

    writer.startElement("ContactRole", attributes);
    writer.startElement("role");
    writeParamContainer(writer, cr.role);
    writer.endElement();
    writer.endElement();
}


struct HandlerContactRole : public SAXParser::Handler
{
    ContactRole* cr;
    HandlerContactRole(ContactRole* _cr = 0) : cr(_cr) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "ContactRole")
            throw runtime_error(("[IO::HandlerContactRole] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ContactRole& cr)
{
    HandlerContactRole handler(&cr);
    SAXParser::parse(is, handler);
}

//
// AnalysisSoftware
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSoftware& anal)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(anal, attributes);
    attributes.push_back(make_pair("version", anal.version));
    attributes.push_back(make_pair("URI", anal.URI));

    writer.startElement("AnalysisSoftware", attributes);

    write(writer, anal.contactRole);
    
    writer.startElement("SoftwareName");
    writeParamContainer(writer, anal.softwareName);
    writer.endElement();

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("Customizations");
    writer.characters(anal.customizations);
    writer.endElement();
    writer.popStyle();
    
    writer.endElement();
}

struct HandlerAnalysisSoftware : public SAXParser::Handler
{
    AnalysisSoftware* anal;
    HandlerAnalysisSoftware(AnalysisSoftware* _anal = 0) : anal(_anal) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, AnalysisSoftware& anal)
{
    HandlerAnalysisSoftware handler(&anal);
    SAXParser::parse(is, handler);
}

//
// Analysis
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Analysis& anal)
{
    XMLWriter::Attributes attributes;

    writer.startElement("AnalysisCollection", attributes);
    writer.endElement();
}

struct HandlerAnalysis : public SAXParser::Handler
{
    Analysis* anal;
    HandlerAnalysis(Analysis* _anal = 0) : anal(_anal) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, Analysis& anal)
{
    HandlerAnalysis handler(&anal);
    SAXParser::parse(is, handler);
}

//
// AnalysisProtocol
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisProtocol& anal)
{
    XMLWriter::Attributes attributes;

    writer.startElement("AnalysisProtocolCollection", attributes);
    writer.endElement();
}

struct HandlerAnalysisProtocol : public SAXParser::Handler
{
    AnalysisProtocol* anal;
    HandlerAnalysisProtocol(AnalysisProtocol* _anal = 0) : anal(_anal) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, AnalysisProtocol& anal)
{
    HandlerAnalysisProtocol handler(&anal);
    SAXParser::parse(is, handler);
}

//
// AnalysisSampleCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSampleCollection& asc)
{
    XMLWriter::Attributes attributes;

    writer.startElement("AnalysisSampleCollection", attributes);
    writer.endElement();
}

struct HandlerAnalysisSampleCollection : public SAXParser::Handler
{
    AnalysisSampleCollection* asc;
    HandlerAnalysisSampleCollection(AnalysisSampleCollection* _asc = 0) : asc(_asc) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, AnalysisSampleCollection& asc)
{
    HandlerAnalysisSampleCollection handler(&asc);
    SAXParser::parse(is, handler);
}

//
// DataCollectionPtr
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollectionPtr& dcp)
{
    XMLWriter::Attributes attributes;

    writer.startElement("DataCollection", attributes);
    writer.endElement();
}

struct HandlerDataCollectionPtr : public SAXParser::Handler
{
    DataCollectionPtr* dcp;
    HandlerDataCollectionPtr(DataCollectionPtr* _dcp = 0) : dcp(_dcp) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, DataCollectionPtr& dcp)
{
    HandlerDataCollectionPtr handler(&dcp);
    SAXParser::parse(is, handler);
}

//
// Provider
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Provider& provider)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(provider, attributes);
    
    writer.startElement("Provider", attributes);
    write(writer, provider.contactRole);
    writer.endElement();
}

struct HandlerProvider : public SAXParser::Handler
{
    Provider* p;
    HandlerProvider(Provider* _p = 0) : p(_p) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, Provider& provider)
{
    HandlerProvider handler(&provider);
    SAXParser::parse(is, handler);
}

//
// MzIdentML
//

PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const MzIdentML& mzid)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(mzid, attributes);
    attributes.push_back(make_pair("version", mzid.version));
    attributes.push_back(make_pair("creationDate", mzid.creationDate));

    writer.startElement("MzIdentML", attributes);

    writeList(writer, mzid.cvs, "cvList");
    writePtrList(writer, mzid.analysisSoftwareList, "AnalysisSoftwareList");
    write(writer, mzid.provider);
    write(writer, mzid.analysisSampleCollection);

    writeList(writer, mzid.auditCollection, "AuditCollection");

    write(writer, mzid.sequenceCollection);
    write(writer, mzid.analysisCollection);
    writePtrList(writer, mzid.analysisProtocolCollection, "AnalysisProtocolCollection");
    writeList(writer, mzid.dataCollection, "DataCollection" );
    writePtrList(writer, mzid.bibliographicReference,
    "BibliographicReference");

    writer.endElement();
}

struct HandlerMzIdentML : public SAXParser::Handler
{
    MzIdentML* mim;
    HandlerMzIdentML(MzIdentML* _mim = 0) : mim(_mim) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "mzIdentML")
            throw runtime_error(("[IO::HandlerMzIdentML] Unexpected element name: " + name).c_str());

        if (!mim)
            throw runtime_error("[IO::HandlerMzIdentML] Null mzIdentML.");

        getAttribute(attributes, "id", mim->id);
        getAttribute(attributes, "name", mim->name);
        getAttribute(attributes, "creationDate", mim->creationDate);

        // TODO read child tags
        if (name == "cvList")
        {
        }
        if (name == "cv")
        {
            mim->cvs.push_back(CV());
        }
        else if (name == "AnalysisSoftwareList")
        {
        }
        else if (name == "AnalysisSoftware")
        {
            mim->analysisSoftwareList.push_back(
                shared_ptr<AnalysisSoftware>(new AnalysisSoftware()));
        }
        else if (name == "Provider")
        {
        }
        else if (name == "AuditCollection")
        {
            // TODO implement AuditCollection flag for begin/end.
        }
        else if (name == "ReferenceableCollection")
        {
        }
        else if (name == "AnalysisSampleCollection")
        {
        }
        else if (name == "SequenceCollection")
        {
        }
        else if (name == "DBSequence")
        {
        }
        else if (name == "AnalysisCollection")
        {
        }
        else if (name == "SpectrumIdentification")
        {
            
        }
        else if (name == "ProteinDetection")
        {
        }
        else if (name == "AnalysisProtocolCollection")
        {
        }
        else if (name == "DataCollection")
        {
        }
        
        return Status::Ok;
    }

    private:
};

PWIZ_API_DECL void read(std::istream& is, MzIdentML& mziddata)
{
    HandlerMzIdentML handler(&mziddata);
    SAXParser::parse(is, handler);
}

} // namespace pwiz 
} // namespace mziddata 
} // namespace IO 
