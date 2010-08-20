//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace mziddata {
namespace IO {

using namespace minimxml;
using namespace minimxml::SAXParser;
using namespace boost::logic;
//using namespace util;

static const int MZIDENTML_VERSION_1_0 = 0;

template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objects, 
               const string& label = "")
{
    if (!objects.empty())
    {
        XMLWriter::Attributes attributes;
        //attributes.push_back(make_pair("count", lexical_cast<string>(objects.size())));
        if (!label.empty())
            writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objects.begin(); it!=objects.end(); ++it)
            write(writer, *it);
        if (!label.empty())
            writer.endElement();
    }
}

template <typename object_type>
void writePtrList(minimxml::XMLWriter& writer, const vector<object_type>& objectPtrs, 
                  const string& label = "")
{
    if (!objectPtrs.empty())
    {
        XMLWriter::Attributes attributes;
        //attributes.push_back(make_pair("count", lexical_cast<string>(objectPtrs.size())));
        if (!label.empty())
        writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objectPtrs.begin(); it!=objectPtrs.end(); ++it)
            write(writer, **it);
        if (!label.empty())
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
// HandlerString
//
struct HandlerString : public SAXParser::Handler
{
    string* str;
    
    HandlerString(string* str_ = 0) : str(str_) {}
    virtual ~HandlerString() {}
    
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!str)
            throw runtime_error("[IO::HandlerNamedString] Null string.");

        return Status::Ok;
    }

    virtual Status characters(const string& text, stream_offset position)
    {
        *str = text;

        return Status::Ok;
    }

};

//
// IdentifiableType
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IdentifiableType& it)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(it, attributes);
    writer.startElement("FakeIdentifiableType", attributes, XMLWriter::EmptyElement);
}

struct HandlerIdentifiableType : public SAXParser::Handler
{
    IdentifiableType* id;
    HandlerIdentifiableType(IdentifiableType* _id = 0) : id(_id) {}
    virtual ~HandlerIdentifiableType() {}
    
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!id)
            throw runtime_error("[IO::HandlerIdentifiableType] Null IdentifiableType.");

        getAttribute(attributes, "id", id->id);
        getAttribute(attributes, "name", id->name);

        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, IdentifiableType& it)
{
    HandlerIdentifiableType handler(&it);
    SAXParser::parse(is, handler);
}

//
// addExternalDataAttributes
//
void addExternalDataAttributes(const ExternalData& ed, XMLWriter::Attributes& attributes)
{
    addIdAttributes(ed, attributes);
    attributes.push_back(make_pair("location", ed.location));
}
//
// BibliographicReference
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& br)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(br, attributes);
    attributes.push_back(make_pair("authors", br.authors));
    attributes.push_back(make_pair("publication", br.publication));
    attributes.push_back(make_pair("publisher", br.publisher));
    attributes.push_back(make_pair("editor", br.editor));
    attributes.push_back(make_pair("year", lexical_cast<string>(br.year)));
    attributes.push_back(make_pair("volume", br.volume));
    attributes.push_back(make_pair("issue", br.issue));
    attributes.push_back(make_pair("pages", br.pages));
    attributes.push_back(make_pair("title", br.title));
    
    writer.startElement("BibliographicReference", attributes, XMLWriter::EmptyElement);
}


struct HandlerBibliographicReference : public HandlerIdentifiableType
{
    BibliographicReference* br;
    HandlerBibliographicReference(BibliographicReference* _br = 0)
        : br(_br) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "BibliographicReference")
            throw runtime_error(("[IO::HandlerBibliographicReference] Unexpected element name: " + name).c_str());

        
        getAttribute(attributes, "authors", br->authors);
        getAttribute(attributes, "publication", br->publication);
        getAttribute(attributes, "publisher", br->publisher);
        getAttribute(attributes, "editor", br->editor);
        getAttribute(attributes, "year", br->year);
        getAttribute(attributes, "volume", br->volume);
        getAttribute(attributes, "issue", br->issue);
        getAttribute(attributes, "pages", br->pages);
        getAttribute(attributes, "title", br->title);

        HandlerIdentifiableType::id = br;
        return HandlerIdentifiableType::startElement(name, attributes, position);
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
    write(writer, *ds);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequence& ds)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(ds, attributes);
    //attributes.push_back(make_pair("id", ds.id));
    if (ds.length > 0)
        attributes.push_back(make_pair("length", lexical_cast<string>(ds.length)));
    attributes.push_back(make_pair("accession", ds.accession));
    if (ds.searchDatabasePtr.get())
        attributes.push_back(make_pair("SearchDatabase_ref", ds.searchDatabasePtr->id));
    
    writer.startElement("DBSequence", attributes);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("seq");
    writer.characters(ds.seq);
    writer.endElement();
    writer.popStyle();

    writeParamContainer(writer, ds.paramGroup);
    writer.endElement();
}


struct HandlerDBSequence : public HandlerIdentifiableType
{
    DBSequence* ds;
    bool inSeq;

    HandlerDBSequence(DBSequence* _ds = 0)
        : ds(_ds), inSeq(false) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!ds)
            throw runtime_error("[IO::HandlerDBSequence] Null DBSequence.");
        
        if (name == "DBSequence")
        {
            getAttribute(attributes, "length", ds->length);
            getAttribute(attributes, "accession", ds->accession);

            string value;
            getAttribute(attributes, "SearchDatabase_ref", value);
            ds->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase(value));
            HandlerIdentifiableType::id = ds;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "seq")
        {
            inSeq = true;
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &ds->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        
        return Status::Ok;
    }

    virtual Status characters(const string& text, 
                              stream_offset position)
    {
        if (inSeq)
            ds->seq = text;
        else
            throw runtime_error("[IO::HandlerDBSequence] Unexpected characters found");

        return Status::Ok;
    }

    
    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "seq")
            inSeq = false;

        return Status::Ok;
    }

    private:
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, DBSequencePtr ds)
{
    // TODO throw exception if pointer is NULL
    read(is, *ds);
}


PWIZ_API_DECL void read(std::istream& is, DBSequence& ds)
{
    HandlerDBSequence handler(&ds);
    SAXParser::parse(is, handler);
}


//
// Modification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ModificationPtr mod)
{
    if (!mod.get())
        throw runtime_error("[IO::write] ModificationPtr has Null value.");

    write(writer, *mod);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& mod)
{
    XMLWriter::Attributes attributes;
    if (mod.location > 0)
        attributes.push_back(make_pair("location", lexical_cast<string>(mod.location)));
    if (!mod.residues.empty())
        attributes.push_back(make_pair("residues", mod.residues));
    if (mod.avgMassDelta > 0)
        attributes.push_back(make_pair("avgMassDelta", lexical_cast<string>(mod.avgMassDelta)));
    //if (mod.monoisotopicMassDelta > 0)
    attributes.push_back(make_pair("monoisotopicMassDelta", lexical_cast<string>(mod.monoisotopicMassDelta)));


    XMLWriter::EmptyElementTag elementTag = mod.paramGroup.empty() ?
        XMLWriter::EmptyElement : XMLWriter::NotEmptyElement;
    writer.startElement("Modification", attributes, elementTag);
    if (!mod.paramGroup.empty())
    {
        writeParamContainer(writer, mod.paramGroup);
        writer.endElement();
    }
}


struct HandlerModification : public SAXParser::Handler
{
    Modification* mod;
    HandlerModification(Modification* _mod = 0) : mod(_mod) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Modification")
        {
            getAttribute(attributes, "location", mod->location);
            getAttribute(attributes, "residues", mod->residues);
            getAttribute(attributes, "avgMassDelta", mod->avgMassDelta);
            getAttribute(attributes, "monoisotopicMassDelta", mod->monoisotopicMassDelta);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &mod->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerModification] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, ModificationPtr mod)
{
    if (!mod.get())
        throw runtime_error("[IO::write] ModificationPtr has Null value.");

    read(is, *mod);
}

PWIZ_API_DECL void read(std::istream& is, Modification& mod)
{
    // TODO throw exception if pointer is NULL
    HandlerModification handler(&mod);
    SAXParser::parse(is, handler);
}

//
// SubstitutionModification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SubstitutionModification& sm)
{
    XMLWriter::Attributes attributes;
    if (!sm.originalResidue.empty())
        attributes.push_back(make_pair("originalResidue", sm.originalResidue));
    if (!sm.replacementResidue.empty())
        attributes.push_back(make_pair("replacementResidue", sm.replacementResidue));
    if (sm.location != 0)
        attributes.push_back(make_pair("location", boost::lexical_cast<string>(sm.location)));
    if (sm.avgMassDelta != 0)
        attributes.push_back(make_pair("avgMassDelta", boost::lexical_cast<string>(sm.avgMassDelta)));
    if (sm.monoisotopicMassDelta != 0)
        attributes.push_back(make_pair("monoisotopicMassDelta", boost::lexical_cast<string>(sm.monoisotopicMassDelta)));
    
    writer.startElement("SubstitutionModification", attributes, XMLWriter::EmptyElement);
}


struct HandlerSubstitutionModification : public SAXParser::Handler
{
    SubstitutionModification* subMod;
    HandlerSubstitutionModification(SubstitutionModification* _subMod = 0) : subMod(_subMod) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!subMod)
            throw runtime_error("[IO::HandlerSubstitutionModification] Null SubstitutionModification");
        
        if (name != "SubstitutionModification")
            throw runtime_error(("[IO::HandlerSubstitutionModification] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "originalResidue", subMod->originalResidue);
        getAttribute(attributes, "replacementResidue", subMod->replacementResidue);
        getAttribute(attributes, "location", subMod->location);
        getAttribute(attributes, "avgMassDelta", subMod->avgMassDelta);
        getAttribute(attributes, "monoisotopicMassDelta", subMod->monoisotopicMassDelta);
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, SubstitutionModification& sm)
{
    // TODO throw exception if pointer is NULL
    HandlerSubstitutionModification handler(&sm);
    SAXParser::parse(is, handler);
}


//
// Peptide
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptidePtr peptide)
{
    write(writer, *peptide);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Peptide& peptide)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(peptide, attributes);
    
    writer.startElement("Peptide", attributes);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("peptideSequence");
    writer.characters(peptide.peptideSequence);
    writer.endElement();
    writer.popStyle();

    if (!peptide.modification.empty())
        writeList(writer, peptide.modification);
    if (!peptide.substitutionModification.empty())
        write(writer, peptide.substitutionModification);

    writeParamContainer(writer, peptide.paramGroup);
    writer.endElement();
}


struct HandlerPeptide : public HandlerIdentifiableType
{
    bool inPeptideSequence;
    Peptide* peptide;
    HandlerPeptide(Peptide* _peptide = 0)
        : inPeptideSequence(false), peptide(_peptide) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!peptide)
            throw runtime_error("[IO::HandlerPeptide] Null Peptide.");
        
        if (name == "Peptide")
        {
            HandlerIdentifiableType::id = peptide;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "peptideSequence")
            inPeptideSequence = true;
        else if (name == "Modification")
        {
            peptide->modification.push_back(ModificationPtr(new Modification()));
            handlerModification_.mod = peptide->modification.back().get();
            return Status(Status::Delegate, &handlerModification_);
        }
        else if (name == "SubstitutionModification")
        {
            handlerSubstitutionModification_.subMod = &peptide->substitutionModification;
            return Status(Status::Delegate, &handlerSubstitutionModification_);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &peptide->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerPeptide] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }

    virtual Status characters(const string& text, 
                              stream_offset position)
    {
        if (inPeptideSequence)
            peptide->peptideSequence = text;
        else
            throw runtime_error("[IO::HandlerPeptide] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "peptideSequence")
            inPeptideSequence = false;

        return Status::Ok;
    }
    private:
    HandlerModification handlerModification_;
    HandlerSubstitutionModification handlerSubstitutionModification_;
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, PeptidePtr peptide)
{
    // TODO throw exception if pointer is NULL
    read(is, *peptide);
}


PWIZ_API_DECL void read(std::istream& is, Peptide& peptide)
{
    HandlerPeptide handler(&peptide);
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
    for(vector<PeptidePtr>::const_iterator it=sc.peptides.begin(); it!=sc.peptides.end(); it++)
        write(writer, *it);

    writer.endElement();
}


struct HandlerSequenceCollection : public SAXParser::Handler
{
    SequenceCollection* sc;
    HandlerSequenceCollection(SequenceCollection* _sc = 0)
        : sc(_sc) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sc)
            throw runtime_error("[IO::HandlerSequenceCollection] Null HandlerSequenceCollection");
        
        if (name == "SequenceCollection")
        {
            // Ignore 
        }
        else if (name == "DBSequence")
        {
            sc->dbSequences.push_back(DBSequencePtr(new DBSequence()));
            handlerDBSequence_.ds = sc->dbSequences.back().get();
            return Status(Status::Delegate, &handlerDBSequence_); 
        }
        else if (name == "Peptide")
        {
            sc->peptides.push_back(PeptidePtr(new Peptide()));
            handlerPeptide_.peptide = sc->peptides.back().get();
            return Status(Status::Delegate, &handlerPeptide_);
        }
        else
            throw runtime_error(("[IO::HandlerSequenceCollection] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }

    private:
    HandlerDBSequence handlerDBSequence_;
    HandlerPeptide handlerPeptide_;
};


PWIZ_API_DECL void read(std::istream& is, SequenceCollection& sc)
{
    HandlerSequenceCollection handler(&sc);
    SAXParser::parse(is, handler);
}


//
// Contact
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactPtr cp)
{
    if (!cp.get())
        throw runtime_error("[IO::write] Null valued ContactPtr.");
    
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

    if (dynamic_cast<Person*>(cp.get()))
    {
        write(writer, (const Person&)*cp);
        /*
        const Person* pp = (Person*)cp.get();

        attributes.push_back(make_pair("lastName", pp->lastName));
        attributes.push_back(make_pair("firstName", pp->firstName));
        attributes.push_back(make_pair("midInitials", pp->midInitials));

        writer.startElement("Person", attributes,
        XMLWriter::EmptyElement);
        */
    }
    if (dynamic_cast<Organization*>(cp.get()))
    {
        write(writer, (const Organization&)*cp);
        /*
        const Organization* opp = (Organization*)cp.get();
        writer.startElement("Organization", attributes);
        if (opp->parent.organizationPtr.get())
        {
            XMLWriter::Attributes oppAttributes;
            oppAttributes.push_back(make_pair("Organization_ref",
                                              opp->parent.organizationPtr->id));
            writer.startElement("Parent", oppAttributes, XMLWriter::EmptyElement);
        }
        writer.endElement();
        */
    }
}


//
// Contact
//

struct HandlerContact : public HandlerIdentifiableType
{
    Contact* c;
    HandlerContact(Contact* _c = 0) : c(_c) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!c)
            throw runtime_error("[IO::HandlerContact] Null Contact.");

        getAttribute(attributes, "address", c->address);
        getAttribute(attributes, "phone", c->phone);
        getAttribute(attributes, "email", c->email);
        getAttribute(attributes, "fax", c->fax);
        getAttribute(attributes, "tollFreePhone", c->tollFreePhone);

        HandlerIdentifiableType::id = c;
        return HandlerIdentifiableType::startElement(name, attributes, position);
    }
};

PWIZ_API_DECL void read(std::istream& is, ContactPtr cp)
{
    // TODO add throw if pointer DNE
    HandlerContact handler(cp.get());
    SAXParser::parse(is, handler);
}


//
// Affiliations
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         const Affiliations& affiliations)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("Organization_ref", affiliations.organizationPtr->id));

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

        getAttribute(attributes, "Organization_ref", aff->organizationPtr->id);
        
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
                         //XMLWriter::Attributes attributes,
                         const PersonPtr pp)
{
    if (!pp.get())
        throw runtime_error("[IO::write] PersonPtr has Null value.");

    write(writer, *pp);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         const Person& pp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pp, attributes);
    if (!pp.address.empty())
        attributes.push_back(make_pair("address", pp.address));
    if (!pp.phone.empty())
        attributes.push_back(make_pair("phone", pp.phone));
    if (!pp.email.empty())
        attributes.push_back(make_pair("email", pp.email));
    if (!pp.fax.empty())
        attributes.push_back(make_pair("fax", pp.fax));
    if (!pp.tollFreePhone.empty())
        attributes.push_back(make_pair("tollFreePhone", pp.tollFreePhone));
    
    attributes.push_back(make_pair("lastName", pp.lastName));
    attributes.push_back(make_pair("firstName", pp.firstName));
    attributes.push_back(make_pair("midInitials", pp.midInitials));
    
    writer.startElement("Person", attributes);
    for(vector<Affiliations>::const_iterator it=pp.affiliations.begin();
        it != pp.affiliations.end();
        it++)
        write(writer, *it);
    writer.endElement();
}


struct HandlerPerson : public HandlerContact
{
    Person* per;
    HandlerPerson(Person* _per = 0)
        : HandlerContact(_per), per(_per) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!per)
            throw runtime_error("[IO::HandlerPerson] Null Person.");
        
        if (name == "Person")
        {
            getAttribute(attributes, "lastName", per->lastName);
            getAttribute(attributes, "firstName", per->firstName);
            getAttribute(attributes, "midInitials", per->midInitials);
            HandlerContact::c = per;
            return HandlerContact::startElement(name, attributes, position);
        }
        else if (name == "affiliations")
        {
            per->affiliations.push_back(Affiliations());
            handlerAffiliations_.aff = &per->affiliations.back();
            return Status(Status::Delegate, &handlerAffiliations_);
        }
        else
            throw runtime_error(("[HandlerPerson] Unknown tag found: "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerAffiliations handlerAffiliations_;
};


PWIZ_API_DECL void read(std::istream& is, PersonPtr pp)
{
    if (!pp.get())
        throw runtime_error("[IO::read] PersonPtr has Null value.");
        
    read(is, *pp);
}

PWIZ_API_DECL void read(std::istream& is, Person& pp)
{
    HandlerPerson handler(&pp);
    SAXParser::parse(is, handler);
}


//
// Organization
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         //XMLWriter::Attributes attributes,
                         const OrganizationPtr op)
{
    if (!op.get())
        throw runtime_error("[IO::write] OrganizationPtr has Null value.");

    write(writer, *op);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer,
                         const Organization& op)
{
    XMLWriter::Attributes attributes;
    
    addIdAttributes(op, attributes);
    if (!op.address.empty())
        attributes.push_back(make_pair("address", op.address));
    if (!op.phone.empty())
        attributes.push_back(make_pair("phone", op.phone));
    if (!op.email.empty())
        attributes.push_back(make_pair("email", op.email));
    if (!op.fax.empty())
        attributes.push_back(make_pair("fax", op.fax));
    if (!op.tollFreePhone.empty())
        attributes.push_back(make_pair("tollFreePhone", op.tollFreePhone));

    XMLWriter::EmptyElementTag elementTag = op.parent.empty() ?
        XMLWriter::EmptyElement : XMLWriter::NotEmptyElement;
    writer.startElement("Organization", attributes, elementTag);
    if (op.parent.organizationPtr.get())
    {
        XMLWriter::Attributes opAttrs;
        opAttrs.push_back(make_pair("Organization_ref", op.parent.organizationPtr->id));
        writer.startElement("parent", opAttrs, XMLWriter::EmptyElement);
    }
    if (!op.parent.empty())
        writer.endElement();
}


struct HandlerOrganization : public HandlerContact
{
    Organization* org;
    HandlerOrganization(Organization* _org = 0)
        : HandlerContact(_org), org(_org) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!org)
            throw runtime_error("[HandlerOrganization] Null Organization.");
        
        if (name == "Organization")
        {
            HandlerContact::c = org;
            return HandlerContact::startElement(name, attributes, position);
        }
        else if (name == "parent")
        {
            string Organization_ref;
            getAttribute(attributes, "Organization_ref", Organization_ref);
            org->parent.organizationPtr = OrganizationPtr(new Organization(Organization_ref));
        }
        else
            throw runtime_error(("[IO::HandlerOrganization] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, OrganizationPtr op)
{
    read(is, *op);
}


PWIZ_API_DECL void read(std::istream& is, Organization& op)
{
    HandlerOrganization handler(&op);
    SAXParser::parse(is, handler);
}


struct HandlerContactVector : public SAXParser::Handler
{
    vector<ContactPtr>* c;
    HandlerContactVector(vector<ContactPtr>* _c = 0) : c(_c) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Person")
        {
            c->push_back(PersonPtr(new Person()));
            handlerPerson_.per = (Person*)c->back().get();
            return Status(Status::Delegate, &handlerPerson_);
        }
        else if (name == "Organization")
        {
            c->push_back(OrganizationPtr(new Organization()));
            handlerOrganization_.org = (Organization*)c->back().get();
            return Status(Status::Delegate, &handlerOrganization_);
        }
        
        return Status::Ok;
    }

    private:
    HandlerPerson handlerPerson_;
    HandlerOrganization handlerOrganization_;
};


//
// ContactRole
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ContactRole& cr)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("Contact_ref", cr.contactPtr->id));

    writer.startElement("ContactRole", attributes);
    writer.startElement("role");
    writeParamContainer(writer, cr.role);
    writer.endElement();

    writer.endElement();
}


struct HandlerContactRole : public HandlerParamContainer
{
    ContactRole* cr;
    HandlerContactRole(ContactRole* _cr = 0) : cr(_cr),
                                               handlerNamedParamContainer_("role")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!cr)
            throw runtime_error("NULL ContactRole");

        if (name == "ContactRole")
        {
            string Contact_ref;
            getAttribute(attributes, "Contact_ref", Contact_ref);
            cr->contactPtr = ContactPtr(new Contact(Contact_ref));
        }
        else if (name == "role")
        {
            handlerNamedParamContainer_.paramContainer = &cr->role;
            return Status(Status::Delegate, &handlerNamedParamContainer_);
        }
        else if (name == "cvParam")
        {
            //cr->role.cvParams.push_back(CVParam());
            //handlerCVParam_.cvParam = &cr->role.cvParams.back();
            //return Status(Status::Delegate, &handlerCVParam_);
        }
        else
            return HandlerParamContainer::startElement(name, attributes, position);
        
        
        return Status::Ok;
    }

    
    private:
    HandlerNamedParamContainer handlerNamedParamContainer_;
    HandlerParamContainer handlerParamContainer_;
    HandlerCVParam handlerCVParam_;
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
    if (!anal.version.empty())
        attributes.push_back(make_pair("version", anal.version));
    if (!anal.URI.empty())
        attributes.push_back(make_pair("URI", anal.URI));

    writer.startElement("AnalysisSoftware", attributes);

    if (anal.contactRolePtr.get() && !anal.contactRolePtr->empty())
        write(writer, *anal.contactRolePtr);
    
    writer.startElement("SoftwareName");
    writeParamContainer(writer, anal.softwareName);
    writer.endElement();

    if (!anal.customizations.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("Customizations");
        writer.characters(anal.customizations);
        writer.endElement();
        writer.popStyle();
    }
    
    writer.endElement();
}

struct HandlerAnalysisSoftware : public HandlerIdentifiableType
{
    AnalysisSoftware* anal;
    HandlerAnalysisSoftware(AnalysisSoftware* _anal = 0)
        : anal(_anal), handlerSoftwareName_("SoftwareName")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        //if (name != "AnalysisSoftware")
        //    throw runtime_error(("[IO::HandlerAnalysisSoftware] Unexpected element name: " + name).c_str());

        if (!anal)
            throw runtime_error("[IO::HandlerAnalysisSoftware] Null AnalysisSoftware.");

        if (name == "AnalysisSoftware")
        {
            getAttribute(attributes, "version", anal->version);
            getAttribute(attributes, "URI", anal->URI);
            getAttribute(attributes, "customizations", anal->customizations);

            HandlerIdentifiableType::id = anal;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "ContactRole")
        {
            anal->contactRolePtr = ContactRolePtr(new ContactRole());
            handlerContactRole_.cr = anal->contactRolePtr.get();
            return Status(Status::Delegate, &handlerContactRole_);
        }
        else if (name == "SoftwareName") 
        {
            handlerSoftwareName_.paramContainer = &anal->softwareName;
            return Status(Status::Delegate, &handlerSoftwareName_);
        }
        else if (name == "Customizations")
        {
            handlerString_.str = &anal->customizations;
            return Status(Status::Delegate, &handlerString_);
        }
        
        return Status::Ok;
    }

    private:
    HandlerContactRole handlerContactRole_;
    HandlerNamedParamContainer handlerSoftwareName_;
    HandlerString handlerString_;
    
};

PWIZ_API_DECL void read(std::istream& is, AnalysisSoftware& anal)
{
    HandlerAnalysisSoftware handler(&anal);
    SAXParser::parse(is, handler);
}

//
// SpectrumIdentification 
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentification& sip)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sip, attributes);
    if (sip.spectrumIdentificationProtocolPtr.get())
        attributes.push_back(make_pair("SpectrumIdentificationProtocol_ref",
                                       sip.spectrumIdentificationProtocolPtr->id));
    if (sip.spectrumIdentificationListPtr.get())
        attributes.push_back(make_pair("SpectrumIdentificationList_ref",
                                       sip.spectrumIdentificationListPtr->id));
    attributes.push_back(make_pair("activityDate", sip.activityDate));

    writer.startElement("SpectrumIdentification", attributes);

    for (vector<SpectraDataPtr>::const_iterator it=sip.inputSpectra.begin();
         it != sip.inputSpectra.end(); it++)
    {
        if (!(*it).get()) continue;

        attributes.clear();
        attributes.push_back(make_pair("SpectraData_ref", (*it)->id));
        writer.startElement("InputSpectra", attributes, XMLWriter::EmptyElement);
    }

    for (vector<SearchDatabasePtr>::const_iterator it=sip.searchDatabase.begin();
         it != sip.searchDatabase.end(); it++)
    {
        if (!(*it).get()) continue;
        
        attributes.clear();
        attributes.push_back(make_pair("SearchDatabase_ref", (*it)->id));
        writer.startElement("SearchDatabase", attributes, XMLWriter::EmptyElement);
    }
    
    writer.endElement();
}

struct HandlerSpectrumIdentification : public HandlerIdentifiableType
{
    SpectrumIdentification* spectrumId;
    HandlerSpectrumIdentification(SpectrumIdentification* _spectrumId = 0)
        : HandlerIdentifiableType(_spectrumId), spectrumId(_spectrumId) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumId)
            throw runtime_error("[IO::HandlerSpectrumIdentification] Null spectrumId.");
        
        if (name == "SpectrumIdentification")
        {
            string  value;
            getAttribute(attributes, "SpectrumIdentificationProtocol_ref", value);
            spectrumId->spectrumIdentificationProtocolPtr = SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol(value));

            value.clear();
            getAttribute(attributes, "SpectrumIdentificationList_ref", value);
            spectrumId->spectrumIdentificationListPtr = SpectrumIdentificationListPtr(new SpectrumIdentificationList(value));
            getAttribute(attributes, "activityDate", spectrumId->activityDate);

            HandlerIdentifiableType::id = spectrumId;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "InputSpectra")
        {
            string value;
            getAttribute(attributes, "SpectraData_ref", value);
            spectrumId->inputSpectra.push_back(
                SpectraDataPtr(new SpectraData(value)));
        }
        else if (name == "SearchDatabase")
        {
            string value;
            getAttribute(attributes, "SearchDatabase_ref", value);
            spectrumId->searchDatabase.push_back(
                SearchDatabasePtr(new SearchDatabase(value)));
        }
        
        return Status::Ok;
    }

    private:
    HandlerString handlerString_;
};

PWIZ_API_DECL void read(std::istream& is, SpectrumIdentification& anal)
{
    HandlerSpectrumIdentification handler(&anal);
    SAXParser::parse(is, handler);
}


//
// ProteinDetection
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetection& pd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pd, attributes);
    if (pd.proteinDetectionProtocolPtr.get())
        attributes.push_back(make_pair("ProteinDetectionProtocol_ref", pd.proteinDetectionProtocolPtr->id));
    if (pd.proteinDetectionListPtr.get())
        attributes.push_back(make_pair("ProteinDetectionList_ref", pd.proteinDetectionListPtr->id));
    attributes.push_back(make_pair("activityDate", pd.activityDate));
    
    writer.startElement("ProteinDetection", attributes);

    for (vector<SpectrumIdentificationListPtr>::const_iterator it=pd.inputSpectrumIdentifications.begin();
         it!=pd.inputSpectrumIdentifications.end(); it++)
    {
        if (!it->get())
            continue;
        
        attributes.clear();
        attributes.push_back(make_pair("SpectrumIdentificationList_ref", (*it)->id));
        writer.startElement("InputSpectrumIdentifications", attributes, XMLWriter::EmptyElement);
    }
    
    writer.endElement();
}


struct HandlerProteinDetection : public HandlerIdentifiableType
{
    ProteinDetection* pd;
    HandlerProteinDetection(ProteinDetection* _pd = 0) : pd(_pd) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pd)
            throw runtime_error("[IO::HandlerProteinDetection] Null ProteinDetection.");
        
        if (name == "ProteinDetection")
        {
            string value;
            getAttribute(attributes, "ProteinDetectionProtocol_ref", value);
            pd->proteinDetectionProtocolPtr = ProteinDetectionProtocolPtr(new ProteinDetectionProtocol(value));
            value.clear();
            getAttribute(attributes, "ProteinDetectionList_ref", value);
            pd->proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList(value));
            getAttribute(attributes, "activityDate", pd->activityDate);

            HandlerIdentifiableType::id = pd;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "InputSpectrumIdentifications")
        {
            string value;
            getAttribute(attributes, "SpectrumIdentificationList_ref",value);
            pd->inputSpectrumIdentifications.push_back(
                SpectrumIdentificationListPtr(
                    new SpectrumIdentificationList(value)));
        }
        else
            throw runtime_error(("[IO::HandlerProteinDetection] Unexpected element name: " + name).c_str());
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ProteinDetection& pd)
{
    HandlerProteinDetection handler(&pd);
    SAXParser::parse(is, handler);
}


//
// Material
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Material& mat)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(mat, attributes);
    
    writer.startElement("Material", attributes);
    if (!mat.contactRole.empty())
        write(writer, mat.contactRole);
    writeParamContainer(writer, mat.cvParams);
    writer.endElement();
}


struct HandlerMaterial : public HandlerIdentifiableType
{
    Material* mat;
    string tag;
    HandlerMaterial(Material* _mat = 0, const string& tag_ = "")
        : mat(_mat), tag(tag_) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "ContactRole")
        {
            handlerContactRole_.cr = &mat->contactRole;
            return Status(Status::Delegate, &handlerContactRole_);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &mat->cvParams;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else if (tag.empty() || name == tag)
        {
            HandlerIdentifiableType::id = mat;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }

        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
    HandlerContactRole handlerContactRole_;
};


PWIZ_API_DECL void read(std::istream& is, Material& mat)
{
    HandlerMaterial handler(&mat);
    SAXParser::parse(is, handler);
}


//
// Sample
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SamplePtr sample)
{
    if (!sample.get())
        throw runtime_error("[IO::write] Null SamplePtr value.");

    write(writer, *sample);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sample)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sample, attributes);
    
    writer.startElement("Sample", attributes);

    if (!sample.contactRole.empty())
        write(writer, sample.contactRole);
    writeParamContainer(writer, sample.cvParams);

    for(vector<Sample::subSample>::const_iterator it=sample.subSamples.begin();
        it != sample.subSamples.end(); it++)
        write(writer, *it);
    
    writer.endElement();
}


struct HandlerSample : public HandlerMaterial
{
    Sample* sample;
    HandlerSample(Sample* _sample = 0, const string& tag_ = "Sample")
        : HandlerMaterial(0, tag_), sample(_sample) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sample)
            throw runtime_error("[IO::HandlerSample] Null Sample");

        if (name == "ContactRole" ||
            name == "cvParam" ||
            name == "userParam" ||
            name == "Sample")
        {
            HandlerMaterial::mat = sample;
            return HandlerMaterial::startElement(name, attributes, position);
        }
        if (name == "subSample")
        {
            sample->subSamples.push_back(Sample::subSample());
            string Sample_ref;
            getAttribute(attributes, "Sample_ref", Sample_ref);
            sample->subSamples.back().samplePtr=SamplePtr(new Sample(Sample_ref));
        }
        else
            throw runtime_error(("[IO::HandlerSample] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }

    private:
    HandlerContactRole handlerContactRole_;
};


PWIZ_API_DECL void read(std::istream& is, SamplePtr sample)
{
    if (!sample.get())
        throw runtime_error("[IO::read] Null SamplePtr value.");

    read(is, *sample);
}

PWIZ_API_DECL void read(std::istream& is, Sample& sample)
{
    HandlerSample handler(&sample);
    SAXParser::parse(is, handler);
}


//
// Sample::subSample
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample::subSample& sc)
{
    XMLWriter::Attributes attributes;
    if (sc.samplePtr.get())
        attributes.push_back(make_pair("Sample_ref", sc.samplePtr->id));
    
    writer.startElement("subSample", attributes, XMLWriter::EmptyElement);
}


//
// AnalysisCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisCollection& ac)
{
    XMLWriter::Attributes attributes;

    writer.startElement("AnalysisCollection", attributes);
    for (vector<SpectrumIdentificationPtr>::const_iterator it=ac.spectrumIdentification.begin(); it!=ac.spectrumIdentification.end(); it++)
        write(writer, **it);

    if (!ac.proteinDetection.empty())
        write(writer, ac.proteinDetection);
    
    writer.endElement();
}

struct HandlerAnalysisCollection : public SAXParser::Handler
{
    AnalysisCollection* anal;
    HandlerAnalysisCollection(AnalysisCollection* _anal = 0) : anal(_anal) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!anal)
            throw runtime_error("[IO::HandlerAnalysisCollection] Null AnalysisCollection.");

        if (name == "AnalysisCollection")
        {
        }
        else if (name == "SpectrumIdentification")
        {
            anal->spectrumIdentification.push_back(SpectrumIdentificationPtr(new SpectrumIdentification()));
            handlerSpectrumIdentification_.spectrumId = anal->spectrumIdentification.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentification_);
        }
        else if (name == "ProteinDetection")
        {
            handlerProteinDetection_.pd = &anal->proteinDetection;
            return Status(Status::Delegate, &handlerProteinDetection_);
        }
        else
            throw runtime_error(("[IO::HandlerAnalysisCollection] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerSpectrumIdentification handlerSpectrumIdentification_;
    HandlerProteinDetection handlerProteinDetection_;
};

PWIZ_API_DECL void read(std::istream& is, AnalysisCollection& anal)
{
    HandlerAnalysisCollection handler(&anal);
    SAXParser::parse(is, handler);
}


//
// Enzyme
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const EnzymePtr& ez)
{
    if (!ez.get())
        throw runtime_error("write: Null EnzymePtr value.");
    
    write(writer, *ez);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzyme& ez)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", ez.id));
    if (!ez.cTermGain.empty())
        attributes.push_back(make_pair("CTermGain", ez.cTermGain));
    if (!ez.nTermGain.empty())
        attributes.push_back(make_pair("NTermGain", ez.nTermGain));
    if (ez.semiSpecific != indeterminate)
        attributes.push_back(make_pair("semiSpecific", ez.semiSpecific ? "true" : "false"));
    if (ez.missedCleavages != 0)
        attributes.push_back(
            make_pair("missedCleavages",
                      lexical_cast<string>(ez.missedCleavages)));
    if (ez.minDistance != 0)
        attributes.push_back(
            make_pair("minDistance",
                      lexical_cast<string>(ez.minDistance)));
    
    writer.startElement("Enzyme", attributes);

    if (!ez.siteRegexp.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("SiteRegexp");
        writer.characters(ez.siteRegexp);
        writer.endElement();
        writer.popStyle();
    }

    writer.startElement("EnzymeName");
    writeParamContainer(writer, ez.enzymeName);
    writer.endElement();

    writer.endElement();
}


struct HandlerEnzyme : public SAXParser::Handler
{
    Enzyme* ez;
    bool inSiteRegexp;
    HandlerEnzyme(Enzyme* _ez = 0)
        : ez(_ez), inSiteRegexp(false),
          handlerNamedParamContainer_("EnzymeName") {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Enzyme")
        {
            getAttribute(attributes, "id", ez->id);
            ez->nTermGain.clear();
            getAttribute(attributes, "NTermGain", ez->nTermGain);
            ez->cTermGain.clear();
            getAttribute(attributes, "CTermGain", ez->cTermGain);
            ez->semiSpecific = indeterminate;
            string value;
            getAttribute(attributes, "semiSpecific", value);
            if (!value.empty())
                ez->semiSpecific = value == "true" ? true : false;
            ez->missedCleavages = 0;
            getAttribute(attributes, "missedCleavages", ez->missedCleavages);
            ez->minDistance = 0;
            getAttribute(attributes, "minDistance", ez->minDistance);
        }
        else if (name == "SiteRegexp")
            inSiteRegexp = true;
        else if (name == "EnzymeName")
        {
            handlerNamedParamContainer_.paramContainer = &ez->enzymeName;
            return Status(Status::Delegate, &handlerNamedParamContainer_);
        }
        else
            throw runtime_error(("[IO::HandlerEnzyme] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    
    virtual Status characters(const string& text, 
                              stream_offset position)
    {
        if (inSiteRegexp)
            ez->siteRegexp = text;
        else
            throw runtime_error("[IO::HandlerEnzyme] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status startElement(const string& name, 
                                stream_offset position)
    {
        if (name == "SiteRegexp")
            inSiteRegexp = false;

        return Status::Ok;
    }
    
    private:
    HandlerNamedParamContainer handlerNamedParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, EnzymePtr ez)
{
    if (!ez.get())
        throw runtime_error("read: Null EnzymePtr value");
    
    read(is, *ez);
}

PWIZ_API_DECL void read(std::istream& is, Enzyme& ez)
{
    HandlerEnzyme handler(&ez);
    SAXParser::parse(is, handler);
}


//
// Enzymes
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzymes& ez)
{
    XMLWriter::Attributes attributes;
    if (!ez.independent.empty())
        attributes.push_back(make_pair("independent", ez.independent));
    
    writer.startElement("Enzymes", attributes);

    for (vector<EnzymePtr>::const_iterator it=ez.enzymes.begin(); it!=ez.enzymes.end(); it++)
        write(writer, *it);
    
    writer.endElement();
}


struct HandlerEnzymes : public SAXParser::Handler
{
    Enzymes* ez;
    HandlerEnzymes(Enzymes* _ez = 0) : ez(_ez) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Enzymes")
        {
            getAttribute(attributes, "independent", ez->independent);
        }
        else if (name == "Enzyme")
        {
            ez->enzymes.push_back(EnzymePtr(new Enzyme()));
            handlerEnzyme_.ez = ez->enzymes.back().get();
            return Status(Status::Delegate, &handlerEnzyme_);
        }
        else
            throw runtime_error(("[IO::HandlerEnzymes] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }

    private:
    HandlerEnzyme handlerEnzyme_;
};


PWIZ_API_DECL void read(std::istream& is, Enzymes& br)
{
    HandlerEnzymes handler(&br);
    SAXParser::parse(is, handler);
}


//
// Residue
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ResiduePtr residue)
{
    if (!residue.get())
        throw runtime_error("write: Null ResiduePtr value");

    write(writer, *residue);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Residue& residue)
{
    XMLWriter::Attributes attributes;

    attributes.push_back(make_pair("Code", residue.Code));
    attributes.push_back(make_pair("Mass", lexical_cast<string>(residue.Mass)));

    writer.startElement("Residue", attributes, XMLWriter::EmptyElement);
}


struct HandlerResidue : public SAXParser::Handler
{
    Residue* residue;
    HandlerResidue(Residue* _residue = 0) : residue(_residue) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "Residue")
            throw runtime_error(("[IO::HandlerResidue] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "Code", residue->Code);
        getAttribute(attributes, "Mass", residue->Mass);
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ResiduePtr residue)
{
    if (!residue.get())
        throw runtime_error("read: Null ResiduePtr value");

    read(is, *residue);
}

PWIZ_API_DECL void read(std::istream& is, Residue& residue)
{
    HandlerResidue handler(&residue);
    SAXParser::parse(is, handler);
}


//
// AmbiguousResidue
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResiduePtr residue)
{
    if (!residue.get())
        throw runtime_error("write: Null AmbiguousResiduePtr value");

    write(writer, *residue);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResidue& residue)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("Code", residue.Code));
    
    writer.startElement("AmbiguousResidue", attributes);
    writeParamContainer(writer, residue.params);
    writer.endElement();
}


struct HandlerAmbiguousResidue : public SAXParser::Handler
{
    AmbiguousResidue* residue;
    HandlerAmbiguousResidue(AmbiguousResidue* _residue = 0)
        : residue(_residue) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "AmbiguousResidue")
        {
            getAttribute(attributes, "Code", residue->Code);
        }
        else if (name == "cvParam" || name == "userParam")
        {
            handlerParamContainer_.paramContainer = &residue->params;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerAmbiguousResidue] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, AmbiguousResiduePtr residue)
{
    if (!residue.get())
        throw runtime_error("write: Null AmbiguousResiduePtr value");

    read(is, *residue);
}

PWIZ_API_DECL void read(std::istream& is, AmbiguousResidue& residue)
{
    HandlerAmbiguousResidue handler(&residue);
    SAXParser::parse(is, handler);
}

//
// MassTable
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const MassTable& mt)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("id", mt.id));
    attributes.push_back(make_pair("msLevel", mt.msLevel));
    
    writer.startElement("MassTable", attributes);

    for(vector<ResiduePtr>::const_iterator it=mt.residues.begin(); it!=mt.residues.end(); it++)
        write(writer, *it);
    
    for(vector<AmbiguousResiduePtr>::const_iterator it=mt.ambiguousResidue.begin(); it!=mt.ambiguousResidue.end(); it++)
        write(writer, *it);
    
    writer.endElement();
}


struct HandlerMassTable : public SAXParser::Handler
{
    MassTable* mt;
    HandlerMassTable(MassTable* _mt = 0) : mt(_mt) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "MassTable")
        {
            getAttribute(attributes, "id", mt->id);
            getAttribute(attributes, "msLevel", mt->msLevel);
        }
        else if (name == "Residue")
        {
            mt->residues.push_back(ResiduePtr(new Residue()));
            handlerResidue_.residue = mt->residues.back().get();
            return handlerResidue_.startElement(name, attributes, position);
        }
        else if (name == "AmbiguousResidue")
        {
            mt->ambiguousResidue.push_back(
                AmbiguousResiduePtr(new AmbiguousResidue()));
            handlerAmbiguousResidue_.residue = mt->ambiguousResidue.back().get();
            return Status(Status::Delegate, &handlerAmbiguousResidue_);
        }
        else
            throw runtime_error(("[IO::HandlerMassTable] Unexpected element name: " + name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerResidue handlerResidue_;
    HandlerAmbiguousResidue handlerAmbiguousResidue_;
};


PWIZ_API_DECL void read(std::istream& is, MassTable& mt)
{
    HandlerMassTable handler(&mt);
    SAXParser::parse(is, handler);
}

//
// ModParam
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ModParam& mp)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("massDelta", lexical_cast<string>(mp.massDelta)));
    attributes.push_back(make_pair("residues", mp.residues));

    writer.startElement("ModParam", attributes);
    
    writeParamContainer(writer, mp.cvParams);

    writer.endElement();
}


struct HandlerModParam : public HandlerParamContainer
{
    ModParam* mp;
    HandlerModParam(ModParam* _mp = 0) : mp(_mp) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "ModParam")
        {
            getAttribute(attributes, "massDelta", mp->massDelta);
            getAttribute(attributes, "residues", mp->residues);
        }
        else if (name =="cvParam")
        {
            HandlerParamContainer::paramContainer = &mp->cvParams;
            return HandlerParamContainer::startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerModParam] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ModParam& mp)
{
    HandlerModParam handler(&mp);
    SAXParser::parse(is, handler);
}

//
// SearchModification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchModification& sm)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("fixedMod", sm.fixedMod ? "true" : "false"));
    
    writer.startElement("SearchModification", attributes);
    write(writer, sm.modParam);

    if (!sm.specificityRules.empty())
    {
        writer.startElement("SpecificityRules");
        writeParamContainer(writer, sm.specificityRules);
        writer.endElement();
    }
    
    writer.endElement();
}


struct HandlerSearchModification : public SAXParser::Handler
{
    SearchModification* sm;
    HandlerSearchModification(SearchModification* _sm = 0)
        : sm(_sm), handlerNamedParamContainer_("SpecificityRules") {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SearchModification")
        {
            string value;
            getAttribute(attributes, "fixedMod", value);
            sm->fixedMod = value == "true" ? true : false;
        }
        else if (name == "ModParam")
        {
            handlerModParam_.mp = &sm->modParam;
            return Status(Status::Delegate, &handlerModParam_);
        }
        else if (name == "SpecificityRules")
        {
            handlerNamedParamContainer_.paramContainer = &sm->specificityRules;
            return Status(Status::Delegate, &handlerNamedParamContainer_);
        }
        else
            throw runtime_error(("[IO::HandlerSearchModification] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerModParam handlerModParam_;
    HandlerNamedParamContainer handlerNamedParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, SearchModification& sm)
{
    HandlerSearchModification handler(&sm);
    SAXParser::parse(is, handler);
}

//
// Filter
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Filter& filter)
{
    XMLWriter::Attributes attributes;
    writer.startElement("Filter", attributes);

    if (!filter.filterType.empty())
    {
        writer.startElement("FilterType");
        writeParamContainer(writer, filter.filterType);
        writer.endElement();
    }
    
    if (!filter.include.empty())
    {
        writer.startElement("Include");
        writeParamContainer(writer, filter.include);
        writer.endElement();
    }
    
    if (!filter.exclude.empty())
    {
        writer.startElement("Exclude");
        writeParamContainer(writer, filter.exclude);
        writer.endElement();
    }
    
    writer.endElement();
}


struct HandlerFilter : public SAXParser::Handler
{
    Filter* filter;
    HandlerFilter(Filter* _filter = 0)
        : filter(_filter), handlerFilterType_("FilterType"),
          handlerInclude_("Include"), handlerExclude_("Exclude")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Filter")
        {
        }
        else if (name == "FilterType")
        {
            handlerFilterType_.paramContainer = &filter->filterType;
            return Status(Status::Delegate, &handlerFilterType_);
        }
        else if (name == "Include")
        {
            handlerInclude_.paramContainer = &filter->include;
            return Status(Status::Delegate, &handlerInclude_);
        }
        else if (name == "Exclude")
        {
            handlerExclude_.paramContainer = &filter->exclude;
            return Status(Status::Delegate, &handlerExclude_);
        }
        else
            throw runtime_error(("[IO::HandlerFilter] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerNamedParamContainer handlerFilterType_;
    HandlerNamedParamContainer handlerInclude_;
    HandlerNamedParamContainer handlerExclude_;
};


PWIZ_API_DECL void read(std::istream& is, Filter& filter)
{
    HandlerFilter handler(&filter);
    SAXParser::parse(is, handler);
}


//
// TranslationTable
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTablePtr tt)
{
    if (!tt.get())
        throw runtime_error("[IO::write] TranslationTablePtr has Null value.");

    write(writer, *tt);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTable& tt)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(tt, attributes);
    writer.startElement("TranslationTable", attributes);

    if (!tt.params.empty())
    {
        writeParamContainer(writer, tt.params);
    }

    writer.endElement();
}

struct HandlerTranslationTable : public HandlerIdentifiableType
{
    TranslationTable* tt;
    HandlerTranslationTable(TranslationTable* _tt = 0)
        : tt(_tt)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "TranslationTable")
        {
            HandlerIdentifiableType::id = tt;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParams_.paramContainer = &tt->params;
            return handlerParams_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerFilter] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParams_;
};


PWIZ_API_DECL void read(std::istream& is, TranslationTablePtr tt)
{
    if (!tt.get())
        throw runtime_error("[IO::read] TranslationTablePtr has Null value.");

    read(is, *tt);
}

PWIZ_API_DECL void read(std::istream& is, TranslationTable& tt)
{
    HandlerTranslationTable handler(&tt);
    SAXParser::parse(is, handler);
}



//
// DatabaseTranslation
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslationPtr dt)
{
    if (!dt.get())
        throw runtime_error("[IO::write] DatabaseTranslation has Null value.");

    write(writer, *dt);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslation& dt)
{
    XMLWriter::Attributes attributes;
    if (!dt.frames.empty())
        attributes.push_back(make_pair("frames", dt.getFrames()));

    writer.startElement("DatabaseTranslation", attributes);

    if (!dt.translationTable.empty())
        writeList(writer, dt.translationTable);
    
    writer.endElement();
}

struct HandlerDatabaseTranslation : SAXParser::Handler
{
    DatabaseTranslation* dt;
    HandlerDatabaseTranslation(DatabaseTranslation* _dt = 0)
        : dt(_dt)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "DatabaseTranslation")
        {
            string values;
            getAttribute(attributes, "frames", values);
            if (!values.empty())
                dt->setFrames(values);
        }
        else if (name == "TranslationTable")
        {
            dt->translationTable.push_back(TranslationTablePtr(new TranslationTable()));
            handlerTranslationTable_.tt = dt->translationTable.back().get();
            return Status(Status::Delegate, &handlerTranslationTable_);
        }
        else
            throw runtime_error(("[IO::HandlerDatabaseTranslation] Unknown tag"+name).c_str());

        return Status::Ok;
    }
    private:
    HandlerTranslationTable handlerTranslationTable_;
};


PWIZ_API_DECL void read(std::istream& is, DatabaseTranslationPtr dt)
{
    if (!dt.get())
        throw runtime_error("[IO::read] DatabaseTranslationPtr has Null value.");

    read(is, *dt);
}

PWIZ_API_DECL void read(std::istream& is, DatabaseTranslation& dt)
{
    HandlerDatabaseTranslation handler(&dt);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationProtocol
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocolPtr sip)
{
    if (!sip.get())
        throw runtime_error("[IO::write] SpectrumIdentificationProtocolPtr has Null value.");

    write(writer, *sip);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocol& si)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(si, attributes);
    if (si.analysisSoftwarePtr.get() && !si.analysisSoftwarePtr->empty())
        attributes.push_back(make_pair("AnalysisSoftware_ref", si.analysisSoftwarePtr->id));

    writer.startElement("SpectrumIdentificationProtocol", attributes);

    if (!si.searchType.empty())
    {
        writer.startElement("SearchType");
        writeParamContainer(writer, si.searchType);
        writer.endElement();
    }
    
    if (!si.additionalSearchParams.empty())
    {
        writer.startElement("AdditionalSearchParams");
        writeParamContainer(writer, si.additionalSearchParams);
        writer.endElement();
    }
    
    writePtrList(writer, si.modificationParams, "ModificationParams");

    if (!si.enzymes.empty())
        write(writer, si.enzymes);
    
    if (!si.massTable.empty())
        write(writer, si.massTable);

    if (!si.fragmentTolerance.empty())
    {
        writer.startElement("FragmentTolerance");
        writeParamContainer(writer, si.fragmentTolerance);
        writer.endElement();
    }

    if (!si.parentTolerance.empty())
    {
        writer.startElement("ParentTolerance");
        writeParamContainer(writer, si.parentTolerance);
        writer.endElement();
    }

    if (!si.threshold.empty())
    {
        writer.startElement("Threshold");
        writeParamContainer(writer, si.threshold);
        writer.endElement();
    }
    
    writePtrList(writer, si.databaseFilters, "DatabaseFilters");

    if (si.databaseTranslation.get() && !si.databaseTranslation->empty())
        write(writer, si.databaseTranslation);
    
    writer.endElement();
}

struct HandlerSpectrumIdentificationProtocol : public HandlerIdentifiableType
{
    SpectrumIdentificationProtocol* sip;
    HandlerSpectrumIdentificationProtocol(SpectrumIdentificationProtocol* _sip = 0)
        : sip(_sip), handlerSearchType_("SearchType"),
          handlerAdditionalSearchParams_("AdditionalSearchParams"),
          handlerFragmentTolerance_("FragmentTolerance"),
          handlerParentTolerance_("ParentTolerance"),
          handlerThreshold_("Threshold")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SpectrumIdentificationProtocol")
        {
            string value;
            getAttribute(attributes, "AnalysisSoftware_ref", value);
            if (!value.empty())
                sip->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware(value));
            
            HandlerIdentifiableType::id = sip;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "SearchType")
        {
            handlerSearchType_.paramContainer = &sip->searchType;
            return Status(Status::Delegate, &handlerSearchType_);
        }
        else if (name == "AdditionalSearchParams")
        {
            handlerAdditionalSearchParams_.paramContainer = &sip->additionalSearchParams;
            return Status(Status::Delegate, &handlerAdditionalSearchParams_);
        }
        else if (name == "ModificationParams")
        {
        }
        else if (name == "SearchModification")
        {
            sip->modificationParams.push_back(SearchModificationPtr(new SearchModification()));
            handlerModificationParams_.sm = sip->modificationParams.back().get();
            return Status(Status::Delegate, &handlerModificationParams_);
        }
        else if (name == "Enzymes")
        {
            handlerEnzymes_.ez = &sip->enzymes;
            return Status(Status::Delegate, &handlerEnzymes_);
        }
        else if (name == "MassTable")
        {
            handlerMassTable_.mt = &sip->massTable;
            return Status(Status::Delegate, &handlerMassTable_);
        }
        else if (name == "FragmentTolerance")
        {
            handlerFragmentTolerance_.paramContainer = &sip->fragmentTolerance;
            return Status(Status::Delegate, &handlerFragmentTolerance_);
        }
        else if (name == "ParentTolerance")
        {
            handlerParentTolerance_.paramContainer = &sip->parentTolerance;
            return Status(Status::Delegate, &handlerParentTolerance_);
        }
        else if (name == "Threshold")
        {
            handlerThreshold_.paramContainer = &sip->threshold;
            return Status(Status::Delegate, &handlerThreshold_);
        }
        else if (name == "DatabaseFilters")
        {
        }
        else if (name == "Filter")
        {
            sip->databaseFilters.push_back(FilterPtr(new Filter()));
            handlerFilter_.filter = sip->databaseFilters.back().get();
            return Status(Status::Delegate, &handlerFilter_);
        }
        else if (name == "DatabaseTranslation")
        {
            sip->databaseTranslation = DatabaseTranslationPtr(new DatabaseTranslation());
            handlerDatabaseTranslation_.dt = sip->databaseTranslation.get();
            return Status(Status::Delegate, &handlerDatabaseTranslation_);
        }
        else
            throw runtime_error(("[IO::HandlerSpectrumIdentificationProtocol] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerNamedParamContainer handlerSearchType_;
    HandlerNamedParamContainer handlerAdditionalSearchParams_;
    HandlerSearchModification handlerModificationParams_;
    HandlerEnzymes handlerEnzymes_;
    HandlerMassTable handlerMassTable_;
    HandlerNamedParamContainer handlerFragmentTolerance_;
    HandlerNamedParamContainer handlerParentTolerance_;
    HandlerNamedParamContainer handlerThreshold_;
    HandlerFilter handlerFilter_;
    HandlerDatabaseTranslation handlerDatabaseTranslation_;
};

PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocolPtr sip)
{
    if (!sip.get())
        throw runtime_error("[IO::read] SpectrumIdentificationProtocolPtr has Null value.");
    read(is, *sip);
}


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocol& sip)
{
    HandlerSpectrumIdentificationProtocol handler(&sip);
    SAXParser::parse(is, handler);
}


//
// ProteinDetectionProtocol
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocolPtr pdp)
{
    if (!pdp.get())
        throw runtime_error("[IO::write] ProteinDetectionProtocolPtr has Null value.");

    write(writer, *pdp);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocol& pd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pd, attributes);
    if (pd.analysisSoftwarePtr.get())
        attributes.push_back(make_pair("AnalysisSoftware_ref",
                                        pd.analysisSoftwarePtr->id));
    
    writer.startElement("ProteinDetectionProtocol", attributes);

    if (!pd.analysisParams.empty())
    {
        writer.startElement("AnalysisParams");
        writeParamContainer(writer, pd.analysisParams);
        writer.endElement();
    }

    if (!pd.threshold.empty())
    {
        writer.startElement("Threshold");
        writeParamContainer(writer, pd.threshold);
        writer.endElement();
    }
    
    writer.endElement();
}

struct HandlerProteinDetectionProtocol : public HandlerIdentifiableType
{
    ProteinDetectionProtocol* pdp;
    HandlerProteinDetectionProtocol(ProteinDetectionProtocol* _pdp = 0)
        : pdp(_pdp), handlerAnalysisParams_("AnalysisParams"),
          handlerThreshold_("Threshold")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pdp)
            throw runtime_error("[IO::HandlerProteinDetectionProtocol] Null ProteinDetectionProtocol.");
        
        if (name == "ProteinDetectionProtocol")
        {
            string value;

            getAttribute(attributes, "AnalysisSoftware_ref", value);
            pdp->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware(value));
            HandlerIdentifiableType::id = pdp;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "AnalysisParams")
        {
            handlerAnalysisParams_.paramContainer = &pdp->analysisParams;
            return Status(Status::Delegate, &handlerAnalysisParams_);
        }
        else if (name == "Threshold")
        {
            handlerThreshold_.paramContainer = &pdp->threshold;
            return Status(Status::Delegate, &handlerThreshold_);
        }
        else
            throw runtime_error(("[IO::HandlerProteinDetectionProtocol] Unknown tag "+name).c_str());
        return Status::Ok;
    }
    private:
    HandlerNamedParamContainer handlerAnalysisParams_;
    HandlerNamedParamContainer handlerThreshold_;
};

PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocolPtr pdp)
{
    if (!pdp.get())
        throw runtime_error("[IO::read] ProteinDetectionProtocolPtr has Null value.");

    read(is, *pdp);
}

PWIZ_API_DECL void read(std::istream& is, ProteinDetectionProtocol& pd)
{
    HandlerProteinDetectionProtocol handler(&pd);
    SAXParser::parse(is, handler);
}

//
// AnalysisProtocolCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisProtocolCollection& anal)
{
    writer.startElement("AnalysisProtocolCollection");

    for(vector<SpectrumIdentificationProtocolPtr>::const_iterator it=anal.spectrumIdentificationProtocol.begin();
        it!=anal.spectrumIdentificationProtocol.end(); it++)
        write(writer, *it);
    
    for(vector<ProteinDetectionProtocolPtr>::const_iterator it=anal.proteinDetectionProtocol.begin();
        it!=anal.proteinDetectionProtocol.end(); it++)
        write(writer, *it);

    writer.endElement();
}

struct HandlerAnalysisProtocolCollection : public SAXParser::Handler
{
    AnalysisProtocolCollection* anal;
    HandlerAnalysisProtocolCollection(AnalysisProtocolCollection* _anal = 0) : anal(_anal) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!anal)
            throw runtime_error("[IO::HandlerAnalysisProtocolCollection] Null AnalysisProtocolCollection.");

        if (name == "AnalysisProtocolCollection")
        {
            // Ignore
        }
        else if (name == "SpectrumIdentificationProtocol")
        {
            anal->spectrumIdentificationProtocol.push_back(SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol()));
            handlerSpectrumIdentificationProtocol_.sip = anal->spectrumIdentificationProtocol.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationProtocol_);
        }
        else if (name == "ProteinDetectionProtocol")
        {
            anal->proteinDetectionProtocol.push_back(ProteinDetectionProtocolPtr(new ProteinDetectionProtocol()));
            handlerProteinDetectionProtocol_.pdp = anal->proteinDetectionProtocol.back().get();
            return Status(Status::Delegate, &handlerProteinDetectionProtocol_);
        }
        else
            throw runtime_error(("[IO::HandlerAnalysisProtocolCollection] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerSpectrumIdentificationProtocol handlerSpectrumIdentificationProtocol_;
    HandlerProteinDetectionProtocol handlerProteinDetectionProtocol_;
};

PWIZ_API_DECL void read(std::istream& is, AnalysisProtocolCollection& anal)
{
    HandlerAnalysisProtocolCollection handler(&anal);
    SAXParser::parse(is, handler);
}

//
// AnalysisSampleCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisSampleCollection& asc)
{
    XMLWriter::Attributes attributes;

    writer.startElement("AnalysisSampleCollection", attributes);
    writeList(writer, asc.samples);
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
        if (!asc)
            throw runtime_error("[IO::HandlerAnalysisSampleCollection] Null AnalysisSampleCollection");
        
        if (name == "AnalysisSampleCollection")
        {
            // Ignore
        }
        else if (name == "Sample")
        {
            asc->samples.push_back(SamplePtr(new Sample()));
            handlerSample_.sample = asc->samples.back().get();
            return Status(Status::Delegate, &handlerSample_);
        }
        else
            throw runtime_error(("[IO::HandlerAnalysisSampleCollection] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerSample handlerSample_;
};

PWIZ_API_DECL void read(std::istream& is, AnalysisSampleCollection& asc)
{
    HandlerAnalysisSampleCollection handler(&asc);
    SAXParser::parse(is, handler);
}


//
// SpectraData
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraDataPtr sd)
{
    if (!sd.get())
        throw runtime_error("write: SpectraDataPtr has NULL value.");

    write(writer, *sd);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraData& sd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sd, attributes);
    attributes.push_back(make_pair("location", sd.location));

    writer.startElement("SpectraData", attributes);

    // write out externalFormatDocumentation
    for (vector<string>::const_iterator it=sd.externalFormatDocumentation.begin(); it != sd.externalFormatDocumentation.end(); it++)
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("externalFormatDocumentation");
        writer.characters(*it);
        writer.endElement();
        writer.popStyle();
    }

    if (!sd.fileFormat.empty())
    {
        writer.startElement("fileFormat");
        writeParamContainer(writer, sd.fileFormat);
        writer.endElement();
    }

    if (!sd.spectrumIDFormat.empty())
    {
        writer.startElement("spectrumIDFormat");
        writeParamContainer(writer, sd.spectrumIDFormat);
        writer.endElement();
    }

    writer.endElement();
}

struct HandlerSpectraData : public HandlerIdentifiableType
{
    bool inExternalFormatDocumentation;
    SpectraData* sd;
    HandlerSpectraData(SpectraData* _sd = 0)
        : inExternalFormatDocumentation(false), sd(_sd),
          handlerFileFormat_("fileFormat"),
          handlerSpectrumIDFormat_("spectrumIDFormat")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sd)
            throw runtime_error("[IO::HandlerSpectraData] Null SpectraData.");

        if (name == "SpectraData")
        {
            getAttribute(attributes, "location", sd->location);
            
            HandlerIdentifiableType::id = sd;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "fileFormat")
        {
            handlerFileFormat_.paramContainer = &sd->fileFormat;
            return Status(Status::Delegate, &handlerFileFormat_);
        }
        else if (name == "externalFormatDocumentation")
        {
            inExternalFormatDocumentation = true;
        }
        else if (name == "spectrumIDFormat")
        {
            handlerSpectrumIDFormat_.paramContainer = &sd->spectrumIDFormat;
            return Status(Status::Delegate, &handlerSpectrumIDFormat_);
        }
        else
            throw runtime_error(("[IO::HandlerSpectraData] Unknown tag"+name).c_str());
        
        return Status::Ok;
    }
    
    virtual Status characters(const string& text, 
                              stream_offset position)
    {
        if (inExternalFormatDocumentation)
        {
            sd->externalFormatDocumentation.push_back(text);
        }
        else
            throw runtime_error("[IO::HandlerSpectraData] Unexpected characters");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "externalFormatDocumentation")
            inExternalFormatDocumentation = false;

        return Status::Ok;
    }

    private:
    HandlerNamedParamContainer handlerFileFormat_;
    HandlerNamedParamContainer handlerSpectrumIDFormat_;
};

PWIZ_API_DECL void read(std::istream& is, SpectraDataPtr sd)
{
    if (!sd.get())
        throw runtime_error("read: SpectraDataPtr has NULL value.");

    read(is, *sd);
}


PWIZ_API_DECL void read(std::istream& is, SpectraData& sd)
{
    HandlerSpectraData handler(&sd);
    SAXParser::parse(is, handler);
}


//
// SearchDatabase
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabasePtr sd)
{
    if (!sd.get())
        throw runtime_error("writer: SearchDatabasePtr has NULL value.");
    write(writer, *sd);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabase& sd)
{
    XMLWriter::Attributes attributes;
    addExternalDataAttributes(sd, attributes);
    if (!sd.version.empty())
        attributes.push_back(make_pair("version", sd.version));
    if (!sd.releaseDate.empty())
        attributes.push_back(make_pair("releaseDate", sd.releaseDate));
    if (sd.numDatabaseSequences>0)
        attributes.push_back(
            make_pair("numDatabaseSequences",
                      lexical_cast<string>(sd.numDatabaseSequences)));
    if (sd.numResidues>0)
        attributes.push_back(
            make_pair("numResidues",
                      lexical_cast<string>(sd.numResidues)));

    writer.startElement("SearchDatabase", attributes);

    if (!sd.fileFormat.empty())
    {
        writer.startElement("fileFormat");
        writeParamContainer(writer, sd.fileFormat);
        writer.endElement();
    }
    
    if (!sd.DatabaseName.empty())
    {
        writer.startElement("DatabaseName");
        writeParamContainer(writer, sd.DatabaseName);
        writer.endElement();
    }

    writeParamContainer(writer, sd.params);
    
    writer.endElement();
}

struct HandlerSearchDatabase : public HandlerIdentifiableType
{
    SearchDatabase* sd;
    HandlerSearchDatabase(SearchDatabase* _sd = 0)
        : sd(_sd), handlerFileFormat_("fileFormat"),
          handlerDatabaseName_("DatabaseName")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sd)
            throw runtime_error("[IO::HandlerSearchDatabase] Null SearchDatabase.");

        if (name == "SearchDatabase")
        {
            getAttribute(attributes, "location", sd->location);
            getAttribute(attributes, "version", sd->version);
            getAttribute(attributes, "releaseDate", sd->releaseDate);
            getAttribute(attributes, "numDatabaseSequences", sd->numDatabaseSequences);
            getAttribute(attributes, "numResidues", sd->numResidues);

            HandlerIdentifiableType::id = sd;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "fileFormat")
        {
            handlerFileFormat_.paramContainer = &sd->fileFormat;
            return Status(Status::Delegate, &handlerFileFormat_);
        }
        else if (name == "DatabaseName")
        {
            handlerDatabaseName_.paramContainer = &sd->DatabaseName;
            return Status(Status::Delegate, &handlerDatabaseName_);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParams_.paramContainer = &sd->params;
            return handlerParams_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerSearchDatabase] Unknown tag"+
                                 name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerNamedParamContainer handlerFileFormat_;
    HandlerNamedParamContainer handlerDatabaseName_;
    HandlerParamContainer handlerParams_;
};

PWIZ_API_DECL void read(std::istream& is, SearchDatabasePtr sd)
{
    if (!sd.get())
        throw runtime_error("writer: SearchDatabasePtr has NULL value.");

    read(is, *sd);
}

PWIZ_API_DECL void read(std::istream& is, SearchDatabase& sd)
{
    HandlerSearchDatabase handler(&sd);
    SAXParser::parse(is, handler);
}

//
// SourceFile
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFilePtr sf)
{
    if (!sf.get())
        throw runtime_error("write: SourceFilePtr has NULL value.");

    write(writer, *sf);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sf, attributes);
    if (!sf.location.empty())
        attributes.push_back(make_pair("location", sf.location));

    writer.startElement("SourceFile", attributes);

    if (!sf.fileFormat.empty())
    {
        writer.startElement("fileFormat");
        writeParamContainer(writer, sf.fileFormat);
        writer.endElement();
    }

    // write out externalFormatDocumentation.
    for (vector<string>::const_iterator it=sf.externalFormatDocumentation.begin(); it != sf.externalFormatDocumentation.end(); it++)
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("externalFormatDocumentation");
        writer.characters(*it);
        writer.endElement();
        writer.popStyle();
    }
    
    writeParamContainer(writer, sf.paramGroup);
    
    writer.endElement();
}

struct HandlerSourceFile : public HandlerIdentifiableType
{
    bool inExternalFormatDocumentation;
    SourceFile* sf;
    HandlerSourceFile(SourceFile* _sf = 0)
        : inExternalFormatDocumentation(false), sf(_sf),
          handlerFileFormat_("fileFormat")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SourceFile")
        {
            getAttribute(attributes, "location", sf->location);
            HandlerIdentifiableType::id = sf;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "externalFormatDocumentation")
        {
            inExternalFormatDocumentation = true;
        }
        else if (name == "fileFormat")
        {
            handlerFileFormat_.paramContainer = &sf->fileFormat;
            return Status(Status::Delegate, &handlerFileFormat_);
        }
        else if (name == "cvParam" || name == "userParam")
        {
            handlerParamContainer_.paramContainer = &sf->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerSourceFile] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }

    virtual Status characters(const string& text, 
                              stream_offset position)
    {
        if (inExternalFormatDocumentation)
        {
            sf->externalFormatDocumentation.push_back(text);
        }
        else
            throw runtime_error("[IO::HandlerSourceFile] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "externalFormatDocumentation")
        {
            inExternalFormatDocumentation = false;
        }

        return Status::Ok;
    }
    
    private:
    HandlerNamedParamContainer handlerFileFormat_;
    HandlerParamContainer handlerParamContainer_;
};

PWIZ_API_DECL void read(std::istream& is, SourceFilePtr sf)
{
    if (!sf.get())
        throw runtime_error("read: SourceFilePtr has NULL value.");

    read(is, sf);
}

PWIZ_API_DECL void read(std::istream& is, SourceFile& sf)
{
    HandlerSourceFile handler(&sf);
    SAXParser::parse(is, handler);
}

//
// Inputs
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Inputs& inputs)
{
    writer.startElement("Inputs");

    if (!inputs.sourceFile.empty())
    {
        for (vector<SourceFilePtr>::const_iterator it=inputs.sourceFile.begin();
             it!=inputs.sourceFile.end(); it++)
            write(writer, *it);
    }
    
    if (!inputs.searchDatabase.empty())
    {
        for (vector<SearchDatabasePtr>::const_iterator it=inputs.searchDatabase.begin();
             it!=inputs.searchDatabase.end(); it++)
            write(writer, *it);
    }
    
    if (!inputs.spectraData.empty())
    {
        for (vector<SpectraDataPtr>::const_iterator it=inputs.spectraData.begin();
             it!=inputs.spectraData.end(); it++)
            write(writer, *it);
    }
    
    writer.endElement();
}

struct HandlerInputs : public SAXParser::Handler
{
    Inputs* inputs;
    HandlerInputs(Inputs* _inputs = 0) : inputs(_inputs) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!inputs)
            throw runtime_error("[IO::HandlerInputs] Null Inputs.");

        if (name == "Inputs")
        {
            // Ignore
        }
        else if (name == "SourceFile")
        {
            inputs->sourceFile.push_back(SourceFilePtr(new SourceFile()));
            handlerSourceFile_.sf = inputs->sourceFile.back().get();
            return Status(Status::Delegate, &handlerSourceFile_);
        }
        else if (name == "SearchDatabase")
        {
            inputs->searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase()));
            handlerSearchDatabase_.sd = inputs->searchDatabase.back().get();
            return Status(Status::Delegate, &handlerSearchDatabase_);
        }
        else if (name == "SpectraData")
        {
            inputs->spectraData.push_back(SpectraDataPtr(new SpectraData()));
            handlerSpectraData_.sd = inputs->spectraData.back().get();
            return Status(Status::Delegate, &handlerSpectraData_);
        }
        else
            throw runtime_error(("[IO::HandlerInputs] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerSourceFile handlerSourceFile_;
    HandlerSearchDatabase handlerSearchDatabase_;
    HandlerSpectraData handlerSpectraData_;
};

PWIZ_API_DECL void read(std::istream& is, Inputs& inputs)
{
    HandlerInputs handler(&inputs);
    SAXParser::parse(is, handler);
}


//
// ProteinDetectionHypothesis
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionHypothesis& pdh)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pdh, attributes);
    if (pdh.dbSequencePtr.get() && !pdh.dbSequencePtr->empty())
        attributes.push_back(make_pair("DBSequence_ref", pdh.dbSequencePtr->id));
    attributes.push_back(make_pair("passThreshold", pdh.passThreshold ? "true" : "false"));

    writer.startElement("ProteinDetectionHypothesis", attributes);
    for(vector<PeptideEvidencePtr>::const_iterator it=pdh.peptideHypothesis.begin();
        it != pdh.peptideHypothesis.end(); it++)
    {
        XMLWriter::Attributes pepAttrs;
        pepAttrs.push_back(make_pair("PeptideEvidence_Ref", (*it)->id));
        writer.startElement("PeptideHypothesis", pepAttrs, XMLWriter::EmptyElement);
    }
    writeParamContainer(writer, pdh.paramGroup);
    
    writer.endElement();
}

struct HandlerProteinDetectionHypothesis : public HandlerIdentifiableType
{
    ProteinDetectionHypothesis* pdh;
    HandlerProteinDetectionHypothesis(ProteinDetectionHypothesis* _pdh = 0)
        : pdh(_pdh)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pdh)
            throw runtime_error("[IO::HandlerProteinDetectionHypothesis] Null ProteinDetectionHypothesis value.");

        if (name == "ProteinDetectionHypothesis")
        {
            string value;
            getAttribute(attributes, "DBSequence_ref", value);
            if (!value.empty())
                pdh->dbSequencePtr = DBSequencePtr(new DBSequence(value));

            value.clear();
            getAttribute(attributes, "passThreshold", value);
            pdh->passThreshold = (value=="true" ? true : false);

            HandlerIdentifiableType::id = pdh;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "PeptideHypothesis")
        {
            string value;
            getAttribute(attributes, "PeptideEvidence_Ref", value);
            pdh->peptideHypothesis.push_back(PeptideEvidencePtr(new PeptideEvidence(value)));
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &pdh->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerProteinDetectionHypothesis] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }

    private:
    HandlerParamContainer handlerParamContainer_;
};

PWIZ_API_DECL void read(std::istream& is, ProteinDetectionHypothesis& pdh)
{
    HandlerProteinDetectionHypothesis handler(&pdh);
    SAXParser::parse(is, handler);
}


//
// ProteinAmbiguityGroup
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinAmbiguityGroup& pag)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pag, attributes);

    writer.startElement("ProteinAmbiguityGroup", attributes);
    for(vector<ProteinDetectionHypothesisPtr>::const_iterator it=pag.proteinDetectionHypothesis.begin();
        it != pag.proteinDetectionHypothesis.end(); it++)
        write(writer, **it);

    writeParamContainer(writer, pag.paramGroup);
    
    writer.endElement();
}

struct HandlerProteinAmbiguityGroup : public HandlerIdentifiableType
{
    ProteinAmbiguityGroup* pag;
    HandlerProteinAmbiguityGroup(ProteinAmbiguityGroup* _pag = 0) : pag(_pag) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pag)
            throw runtime_error("[IO::HandlerProteinAmbiguityGroup] Null ProteinAmbiguityGroup.");

        if (name == "ProteinAmbiguityGroup")
        {
            HandlerIdentifiableType::id = pag;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "ProteinDetectionHypothesis")
        {
            ProteinDetectionHypothesisPtr pdh(new ProteinDetectionHypothesis());
            pag->proteinDetectionHypothesis.push_back(pdh);
            handlerProteinDetectionHypothesis_.pdh = pag->proteinDetectionHypothesis.back().get();
            return Status(Status::Delegate, &handlerProteinDetectionHypothesis_);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &pag->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerProteinAmbiguityGroup] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerProteinDetectionHypothesis handlerProteinDetectionHypothesis_;
    HandlerParamContainer handlerParamContainer_;
};

PWIZ_API_DECL void read(std::istream& is, ProteinAmbiguityGroup& pag)
{
    HandlerProteinAmbiguityGroup handler(&pag);
    SAXParser::parse(is, handler);
}


//
// ProteinDetectionList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionList& pdl)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pdl, attributes);
    
    writer.startElement("ProteinDetectionList", attributes);
    
    writeParamContainer(writer, pdl.paramGroup);
    for (vector<ProteinAmbiguityGroupPtr>::const_iterator it=pdl.proteinAmbiguityGroup.begin();
         it!= pdl.proteinAmbiguityGroup.end(); it++)
        write(writer, **it);
    writer.endElement();
}


struct HandlerProteinDetectionList : public HandlerIdentifiableType
{
    ProteinDetectionList* pdl;
    HandlerProteinDetectionList(ProteinDetectionList* _pdl = 0) : pdl(_pdl) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "ProteinDetectionList")
        {
            HandlerIdentifiableType::id = pdl;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &pdl->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else if (name == "ProteinAmbiguityGroup")
        {
            ProteinAmbiguityGroupPtr pag(new ProteinAmbiguityGroup());
            pdl->proteinAmbiguityGroup.push_back(pag);
            handlerProteinAmbiguityGroup_.pag = pdl->proteinAmbiguityGroup.back().get();
            return Status(Status::Delegate, &handlerProteinAmbiguityGroup_);
        }
        else
            throw runtime_error(("[IO::HandlerProteinDetectionList] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
    HandlerProteinAmbiguityGroup handlerProteinAmbiguityGroup_;
};


PWIZ_API_DECL void read(std::istream& is, ProteinDetectionList& pdl)
{
    HandlerProteinDetectionList handler(&pdl);
    SAXParser::parse(is, handler);
}


//
// PeptideEvidence
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidencePtr pep)
{
    if (!pep.get())
        throw runtime_error("write: Null PeptideEvidencePtr value.");

    write(writer, *pep);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidence& pep)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pep, attributes);
    if (pep.dbSequencePtr.get() && !pep.dbSequencePtr->empty())
        attributes.push_back(make_pair("DBSequence_Ref", pep.dbSequencePtr->id));
    attributes.push_back(make_pair("start", lexical_cast<string>(pep.start)));
    attributes.push_back(make_pair("end", lexical_cast<string>(pep.end)));
    if (!pep.pre.empty())
        attributes.push_back(make_pair("pre", pep.pre));
    if (!pep.post.empty())
        attributes.push_back(make_pair("post", pep.post));
    if (pep.translationTablePtr.get() && !pep.translationTablePtr->empty())
        attributes.push_back(make_pair("TranslationTable_ref", pep.translationTablePtr->id));
    if (pep.frame != 0)
        attributes.push_back(make_pair("frame", lexical_cast<string>(pep.frame)));
    attributes.push_back(make_pair("isDecoy", pep.isDecoy  ? "true" : "false"));
    if (pep.missedCleavages != 0)
        attributes.push_back(make_pair("missedCleavages", lexical_cast<string>(pep.missedCleavages)));
    
    writer.startElement("PeptideEvidence", attributes); //, XMLWriter::EmptyElement);

    writeParamContainer(writer, pep.paramGroup);
    writer.endElement();
}


struct HandlerPeptideEvidence : public HandlerIdentifiableType
{
    PeptideEvidence* pep;
    HandlerPeptideEvidence(PeptideEvidence* _pep = 0) : pep(_pep) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pep)
            throw runtime_error("[IO::HandlerPeptideEvidence] Null PeptideEvidence.");
        
        if (name == "PeptideEvidence")
        {
            string value;
            getAttribute(attributes, "DBSequence_Ref", value);
            if (!value.empty())
                pep->dbSequencePtr = DBSequencePtr(new DBSequence(value));

            value.clear();
            getAttribute(attributes, "start", value);
            if (!value.empty())
                pep->start = lexical_cast<int>(value);

            value.clear();
            getAttribute(attributes, "end", value);
            if (!value.empty())
                pep->end = lexical_cast<int>(value);

            getAttribute(attributes, "pre", pep->pre);

            getAttribute(attributes, "post", pep->post);

            value.clear();
            getAttribute(attributes, "TranslationTable_ref", value);
            if (!value.empty())
                pep->translationTablePtr = TranslationTablePtr(new TranslationTable(value));

            value.clear();
            getAttribute(attributes, "frame", value);
            if (!value.empty())
                pep->frame = lexical_cast<int>(value);
            
            value.clear();
            getAttribute(attributes, "isDecoy", value);
            if (!value.empty())
                pep->isDecoy = (value=="true" ? true : false);

            value.clear();
            getAttribute(attributes, "missedCleavages", value);
            if (!value.empty())
                pep->missedCleavages = lexical_cast<int>(value);

            HandlerIdentifiableType::id = pep;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &pep->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerPeptideEvidence] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, PeptideEvidencePtr pep)
{
    if (!pep.get())
        throw runtime_error("read: Null PeptideEvidencePtr value.");

    read(is, *pep);
}

PWIZ_API_DECL void read(std::istream& is, PeptideEvidence& pep)
{
    HandlerPeptideEvidence handler(&pep);
    SAXParser::parse(is, handler);
}


//
// FragmentArray
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArrayPtr fa)
{
    if (!fa.get())
        throw runtime_error("write: Null FragmentArrayPtr value");

    write(writer, *fa);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArray& fa)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("values", fa.getValues()));
    if (fa.measurePtr.get() && !fa.measurePtr->empty())
        attributes.push_back(make_pair("Measure_ref", fa.measurePtr->id));
    
    writer.startElement("FragmentArray", attributes, XMLWriter::EmptyElement);
}


struct HandlerFragmentArray : public SAXParser::Handler
{
    FragmentArray* fa;
    HandlerFragmentArray(FragmentArray* _fa = 0) : fa(_fa) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!fa)
            throw runtime_error("[IO::HandlerFragmentArray] Null FragmentArray.");
        
        if (name == "FragmentArray")
        {
            string values;
            getAttribute(attributes, "values", values);
            fa->setValues(values);

            values.clear();
            getAttribute(attributes, "Measure_ref", values);
            if (!values.empty())
                fa->measurePtr = MeasurePtr(new Measure(values));
        }
        else
            throw runtime_error(("[IO::HandlerFragmentArray] Unexpected element name: " + name).c_str());
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, FragmentArrayPtr fa)
{
    if (!fa.get())
        throw runtime_error("read: Null FragmentArrayPtr value");

    read(is, *fa);
    
}

PWIZ_API_DECL void read(std::istream& is, FragmentArray& fa)
{
    HandlerFragmentArray handler(&fa);
    SAXParser::parse(is, handler);
}


//
// IonType
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IonType& itype)
{
    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("index", itype.getIndex()));
    attributes.push_back(make_pair("charge", lexical_cast<string>(itype.charge)));

    writer.startElement("IonType", attributes);

    writeParamContainer(writer, itype.paramGroup);
    
    for(vector<FragmentArrayPtr>::const_iterator it=itype.fragmentArray.begin();
        it!=itype.fragmentArray.end(); it++)
        write(writer, *it);
    
    writer.endElement();
}


struct HandlerIonType : public SAXParser::Handler
{
    IonType* it;
    HandlerIonType(IonType* _it = 0) : it(_it) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!it)
            throw runtime_error("[IO::HandlerIonType] Null IonType.");
        
        if (name == "IonType")
        {
            string values;
            getAttribute(attributes, "index", values);
            it->setIndex(values);
            getAttribute(attributes, "charge", it->charge);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &it->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else if (name == "FragmentArray")
        {
            FragmentArrayPtr fa(new FragmentArray());
            it->fragmentArray.push_back(fa);
            handlerFragmentArray_.fa = it->fragmentArray.back().get();
            return handlerFragmentArray_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerIonType] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
    HandlerFragmentArray handlerFragmentArray_;
};


PWIZ_API_DECL void read(std::istream& is, IonType& it)
{
    HandlerIonType handler(&it);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationItem
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItemPtr siip)
{
    if (!siip.get())
        throw runtime_error("write: Null SpectrumIdentificationItemPtr value.");

    write(writer, *siip);
}


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItem& siip)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(siip, attributes);
    attributes.push_back(make_pair("chargeState", lexical_cast<string>(siip.chargeState)));
    attributes.push_back(make_pair("experimentalMassToCharge", lexical_cast<string>(siip.experimentalMassToCharge)));
    attributes.push_back(make_pair("calculatedMassToCharge", lexical_cast<string>(siip.calculatedMassToCharge)));
    attributes.push_back(make_pair("calculatedPI", lexical_cast<string>(siip.calculatedPI)));
    if (siip.peptidePtr.get() && !siip.peptidePtr->empty())
        attributes.push_back(make_pair("Peptide_ref", siip.peptidePtr->id));
    attributes.push_back(make_pair("rank", lexical_cast<string>(siip.rank)));
    attributes.push_back(make_pair("passThreshold", (siip.passThreshold ? "true" : "false")));
    if (siip.massTablePtr.get() && !siip.massTablePtr->empty())
        attributes.push_back(make_pair("MassTable_ref", siip.massTablePtr->id));
    if (siip.samplePtr.get() && !siip.samplePtr->empty())
        attributes.push_back(make_pair("Sample_ref", siip.samplePtr->id));


    writer.startElement("SpectrumIdentificationItem", attributes);

    for(vector<PeptideEvidencePtr>::const_iterator it=siip.peptideEvidence.begin();
        it!=siip.peptideEvidence.end(); it++)
    {
        write(writer, *it);
    }

    writeParamContainer(writer, siip.paramGroup);

    writePtrList(writer, siip.fragmentation, "Fragmentation");
    
    writer.endElement();
}


struct HandlerSpectrumIdentificationItem : public HandlerIdentifiableType
{
    SpectrumIdentificationItem* siip;
    HandlerSpectrumIdentificationItem(SpectrumIdentificationItem* _siip = 0) : siip(_siip) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!siip)
            throw runtime_error("[IO::HandlerSpectrumIdentificationItem] Null SpectrumIdentificationItem.");
        
        if (name == "SpectrumIdentificationItem")
        {
            getAttribute(attributes, "chargeState", siip->chargeState);
            getAttribute(attributes, "experimentalMassToCharge", siip->experimentalMassToCharge);
            getAttribute(attributes, "calculatedMassToCharge", siip->calculatedMassToCharge);
            getAttribute(attributes, "calculatedPI", siip->calculatedPI);

            string value;
            getAttribute(attributes, "Peptide_ref", value);
            siip->peptidePtr = PeptidePtr(new Peptide(value));
            
            getAttribute(attributes, "rank", siip->rank);

            value.clear();
            getAttribute(attributes, "passThreshold", value);
            siip->passThreshold = (value=="true" ? true : false);

            value.clear();
            getAttribute(attributes, "MassTable_ref", value);
            if (!value.empty())
                siip->massTablePtr = MassTablePtr(new MassTable(value));

            value.clear();
            getAttribute(attributes, "Sample_ref", value);
            if (!value.empty())
                siip->samplePtr = SamplePtr(new Sample(value));

            HandlerIdentifiableType::id = siip;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "PeptideEvidence")
        {
            siip->peptideEvidence.push_back(PeptideEvidencePtr(new PeptideEvidence()));
            handlerPeptideEvidence_.pep = siip->peptideEvidence.back().get();
            return Status(Status::Delegate, &handlerPeptideEvidence_);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &siip->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else if (name =="Fragmentation")
        {
            // Ignore
        }
        else if (name == "IonType")
        {
            siip->fragmentation.push_back(IonTypePtr(new IonType()));
            handlerIonType_.it = siip->fragmentation.back().get();
            return Status(Status::Delegate, &handlerIonType_);
        }
        else
            throw runtime_error(("[IO::HandlerSpectrumIdentificationItem] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
    HandlerPeptideEvidence handlerPeptideEvidence_;
    HandlerIonType handlerIonType_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItemPtr siip)
{
    if (!siip.get())
        throw runtime_error("read: Null SpectrumIdentificationItemPtr value.");

    read(is, *siip);
}


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItem& siip)
{
    HandlerSpectrumIdentificationItem handler(&siip);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationResult
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResultPtr sirp)
{
    if (!sirp.get())
        throw runtime_error("write: Null SpectrumIdentificationResultPtr value.");

    write(writer, *sirp);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResult& sirp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sirp, attributes);
    attributes.push_back(make_pair("spectrumID", sirp.spectrumID));
    if (sirp.spectraDataPtr.get() && !sirp.spectraDataPtr->empty())
        attributes.push_back(make_pair("SpectraData_ref", sirp.spectraDataPtr->id));
    
    writer.startElement("SpectrumIdentificationResult", attributes);

    for (vector<SpectrumIdentificationItemPtr>::const_iterator it=sirp.spectrumIdentificationItem.begin(); it!=sirp.spectrumIdentificationItem.end(); it++)
        write(writer, *it);
    
    writeParamContainer(writer, sirp.paramGroup);
    writer.endElement();
}


struct HandlerSpectrumIdentificationResult : public HandlerIdentifiableType
{
    SpectrumIdentificationResult* sirp;
    HandlerSpectrumIdentificationResult(SpectrumIdentificationResult* _sirp = 0) : sirp(_sirp) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sirp)
            throw runtime_error("[IO::HandlerSpectrumIdentificationResult] Null SpectrumIdentificationResult.");
        
        if (name == "SpectrumIdentificationResult")
        {
            getAttribute(attributes, "spectrumID", sirp->spectrumID);

            string value;
            getAttribute(attributes, "SpectraData_ref", value);
            if (!value.empty())
                sirp->spectraDataPtr = SpectraDataPtr(new SpectraData(value));

            HandlerIdentifiableType::id = sirp;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "SpectrumIdentificationItem")
        {
            SpectrumIdentificationItemPtr siip(new SpectrumIdentificationItem());
            sirp->spectrumIdentificationItem.push_back(siip);
            handlerSpectrumIdentificationItem_.siip = sirp->spectrumIdentificationItem.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationItem_);
        }
        else if (name == "cvParam" ||
            name == "userParam")
        {
            handlerParamContainer_.paramContainer = &sirp->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerSpectrumIdentificationResult] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
    HandlerSpectrumIdentificationItem handlerSpectrumIdentificationItem_;
    
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResultPtr sirp)
{
    if (!sirp.get())
        throw runtime_error("read: Null SpectrumIdentificationResultPtr value.");

    read(is, *sirp);
}

PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResult& sirp)
{
    HandlerSpectrumIdentificationResult handler(&sirp);
    SAXParser::parse(is, handler);
}


//
// Measure
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Measure& measure)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(measure, attributes);

    writer.startElement("Measure", attributes);

    writeParamContainer(writer, measure.paramGroup);
    
    writer.endElement();
}


struct HandlerMeasure : public HandlerIdentifiableType
{
    Measure* measure;
    HandlerMeasure(Measure* _measure = 0) : measure(_measure) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Measure")
        {
            HandlerIdentifiableType::id = measure;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "cvParam" ||
                 name == "userParam")
        {
            handlerParamContainer_.paramContainer = &measure->paramGroup;
            return handlerParamContainer_.startElement(name, attributes, position);
        }
        else
            throw runtime_error(("[IO::HandlerMeasure] Unexpected element name: " + name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerParamContainer handlerParamContainer_;
};


PWIZ_API_DECL void read(std::istream& is, Measure& measure)
{
    HandlerMeasure handler(&measure);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationList
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationListPtr silp)
{
    if (!silp.get())
        throw runtime_error("write: Null SpectrumIdentificationListPtr value.");

    write(writer, *silp);
}

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationList& silp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(silp, attributes);
    attributes.push_back(make_pair("numSequencesSearched", lexical_cast<string>(silp.numSequencesSearched)));
    
    writer.startElement("SpectrumIdentificationList", attributes);

    writePtrList(writer, silp.fragmentationTable, "FragmentationTable");

    for (vector<SpectrumIdentificationResultPtr>::const_iterator it=silp.spectrumIdentificationResult.begin(); it!=silp.spectrumIdentificationResult.end(); it++)
        write(writer, *it);
    
    writer.endElement();
}


struct HandlerSpectrumIdentificationList : public HandlerIdentifiableType
{
    SpectrumIdentificationList* silp;
    HandlerSpectrumIdentificationList(SpectrumIdentificationList* _silp = 0) : silp(_silp) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!silp)
            throw runtime_error("[IO::HandlerSpectrumIdentificationList] Null SpectrumIdentificationList.");
        
        if (name == "SpectrumIdentificationList")
        {
            getAttribute(attributes, "numSequencesSearched", silp->numSequencesSearched);
            
            HandlerIdentifiableType::id = silp;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "FragmentationTable")
        {
            // Ignore
        }
        else if (name == "Measure")
        {
            MeasurePtr measure(new Measure());

            silp->fragmentationTable.push_back(measure);
            handlerMeasure_.measure = silp->fragmentationTable.back().get();

            return Status(Status::Delegate, &handlerMeasure_);
        }
        else if (name == "SpectrumIdentificationResult")
        {
            SpectrumIdentificationResultPtr sirp(new SpectrumIdentificationResult());
            silp->spectrumIdentificationResult.push_back(sirp);
            handlerSpectrumIdentificationResult_.sirp = silp->spectrumIdentificationResult.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationResult_);
        }
        else
            throw runtime_error(("[IO::HandlerSpectrumIdentificationList] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }
    private:
    HandlerMeasure handlerMeasure_;
    HandlerSpectrumIdentificationResult handlerSpectrumIdentificationResult_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationListPtr silp)
{
    if (!silp.get())
        throw runtime_error("read: Null SpectrumIdentificationListPtr value.");

    read(is, *silp);
}

PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationList& silp)
{
    HandlerSpectrumIdentificationList handler(&silp);
    SAXParser::parse(is, handler);
}


//
// AnalysisData
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisData& ad)
{
    writer.startElement("AnalysisData");

    for (vector<SpectrumIdentificationListPtr>::const_iterator it=ad.spectrumIdentificationList.begin(); it!=ad.spectrumIdentificationList.end(); it++)
        write(writer, *it);

    if (ad.proteinDetectionListPtr.get() &&
        !ad.proteinDetectionListPtr->empty())
        write(writer, *ad.proteinDetectionListPtr);
    
    writer.endElement();
}


struct HandlerAnalysisData : public SAXParser::Handler
{
    AnalysisData* ad;
    HandlerAnalysisData(AnalysisData* _ad = 0) : ad(_ad) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!ad)
            throw runtime_error("[HandlerAnalysisData::startElement] NULL value for AnalysisData");
        
        if (name == "AnalysisData")
        {
            // ignore
        }
        else if (name == "SpectrumIdentificationList")
        {
            SpectrumIdentificationListPtr silp(new SpectrumIdentificationList());
            ad->spectrumIdentificationList.push_back(silp);
            handlerSpectrumIdentificationList_.silp = ad->spectrumIdentificationList.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationList_);
        }
        else if (name == "ProteinDetectionList")
        {
            ad->proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList());
            handlerProteinDetectionList_.pdl = ad->proteinDetectionListPtr.get();
            return Status(Status::Delegate, &handlerProteinDetectionList_);
        }
        else
            throw runtime_error(("[IO::HandlerAnalysisData] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }
    private:
    HandlerSpectrumIdentificationList handlerSpectrumIdentificationList_;
    HandlerProteinDetectionList handlerProteinDetectionList_;
};


PWIZ_API_DECL void read(std::istream& is, AnalysisData& ad)
{
    HandlerAnalysisData handler(&ad);
    SAXParser::parse(is, handler);
}


//
// DataCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollection& dc)
{
    XMLWriter::Attributes attributes;

    writer.startElement("DataCollection", attributes);

    write(writer, dc.inputs);
    write(writer, dc.analysisData);
    
    writer.endElement();
}

struct HandlerDataCollection : public SAXParser::Handler
{
    DataCollection* dc;
    HandlerDataCollection(DataCollection* _dc = 0) : dc(_dc) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "DataCollection")
        {
        }
        else if (name == "Inputs")
        {
            handlerInputs_.inputs = &dc->inputs;
            return Status(Status::Delegate, &handlerInputs_);
        }
        else if (name == "AnalysisData")
        {
            handlerAnalysisData_.ad = &dc->analysisData;
            return Status(Status::Delegate, &handlerAnalysisData_);
        }
        else
            throw runtime_error(("[IO::HandlerDataCollection] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    private:
    HandlerInputs handlerInputs_;
    HandlerAnalysisData handlerAnalysisData_;
};

PWIZ_API_DECL void read(std::istream& is, DataCollection& dc)
{
    HandlerDataCollection handler(&dc);
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

struct HandlerProvider : public HandlerIdentifiableType
{
    Provider* p;
    HandlerProvider(Provider* _p = 0) : p(_p) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Provider")
        {
            HandlerIdentifiableType::id = p;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }
        else if (name == "ContactRole")
        {
            handlerContactRole_.cr = &p->contactRole;
            return Status(Status::Delegate, &handlerContactRole_);
        }
        else
            throw runtime_error(("[IO::HandlerProvider] Unknown tag "+name).c_str());
        
        return Status::Ok;
    }
    HandlerContactRole handlerContactRole_;
};

PWIZ_API_DECL void read(std::istream& is, Provider& provider)
{
    HandlerProvider handler(&provider);
    SAXParser::parse(is, handler);
}

//
// MzIdentML
//

/// Since the MS CV element write will allways have an "MS" id, we
/// need to change the incoming PSI-MS element to match.
void fixCVList(vector<CV>& cvs)
{
    for(vector<CV>::iterator it=cvs.begin(); it!=cvs.end(); it++)
    {
        if (it->id == "PSI-MS")
        {
            it->id = "MS";
            break;
        }
    }
}

PWIZ_API_DECL
void write(minimxml::XMLWriter& writer, const MzIdentML& mzid)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(mzid, attributes);

    attributes.push_back(make_pair("creationDate", mzid.creationDate));
    attributes.push_back(make_pair("version", mzid.version()));


    attributes.push_back(make_pair("xsi:schemaLocation",
                                   "http://psidev.info/psi/pi/mzIdentML/1.0 ../schema/mzIdentML" + mzid.version() + ".xsd"));
    attributes.push_back(make_pair("xmlns",
                                   "http://psidev.info/psi/pi/mzIdentML/1.0"));
    attributes.push_back(make_pair("xmlns:xsi",
                                   "http://www.w3.org/2001/XMLSchema-instance"));
    writer.startElement("mzIdentML", attributes);

    writeList(writer, mzid.cvs, "cvList");

    if (!mzid.analysisSoftwareList.empty())
        writePtrList(writer, mzid.analysisSoftwareList, "AnalysisSoftwareList");
    if (!mzid.provider.empty())
        write(writer, mzid.provider);
    if (!mzid.auditCollection.empty())
        writeList(writer, mzid.auditCollection, "AuditCollection");
    if (!mzid.analysisSampleCollection.empty())
        write(writer, mzid.analysisSampleCollection);
    if (!mzid.sequenceCollection.empty())
        write(writer, mzid.sequenceCollection);
    if (!mzid.analysisCollection.empty())
        write(writer, mzid.analysisCollection);
    if (!mzid.analysisProtocolCollection.empty())
        write(writer, mzid.analysisProtocolCollection);
    if (!mzid.dataCollection.empty())
        write(writer, mzid.dataCollection);
    if (!mzid.bibliographicReference.empty())
        writePtrList(writer, mzid.bibliographicReference);

    writer.endElement();
}

struct HandlerMzIdentML : public HandlerIdentifiableType
{
    MzIdentML* mzid;
    HandlerMzIdentML(MzIdentML* _mzid = 0) : mzid(_mzid)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!mzid)
            throw runtime_error("[IO::HandlerMzIdentML] Null mzIdentML.");

        
        if (name == "mzIdentML")
        {
            getAttribute(attributes, "creationDate", mzid->creationDate);

            // "http://psidev.info/psi/pi/mzIdentML/1.0 ../schema/mzIdentML<version>.xsd"
            string schemaLocation;
            getAttribute(attributes, "xsi:schemaLocation", schemaLocation);
            if (schemaLocation.empty())
                getAttribute(attributes, "version", mzid->version_); // deprecated?
            else
            {
                schemaLocation = schemaLocation.substr(schemaLocation.find(' ')+1);
                string xsdName = bfs::path(schemaLocation).filename();
                mzid->version_ = xsdName.substr(9, xsdName.length()-13); // read between "mzIdentML" and ".xsd"
            }

            if (mzid->version_.find("1.0.0") == 0)
                version = MZIDENTML_VERSION_1_0;

            HandlerIdentifiableType::id = mzid;
            return HandlerIdentifiableType::startElement(name, attributes, position);
        }

        else if (name == "cvList" ||
            name == "AnalysisSoftwareList")
        {
            // ignore these, unless we want to validate the count
            // attribute
            return Status::Ok;
            
        }
        else if (name == "cv")
        {
            mzid->cvs.push_back(CV());
            handlerCV_.cv = &mzid->cvs.back();
            return Status(Status::Delegate, &handlerCV_);
                    
        }
        else if (name == "AnalysisSoftware")
        {
            mzid->analysisSoftwareList.push_back(
                AnalysisSoftwarePtr(new AnalysisSoftware()));
            handlerAnalysisSoftware_.anal = mzid->analysisSoftwareList.back().get();
            return Status(Status::Delegate, &handlerAnalysisSoftware_);
        }
        else if (name == "Provider")
        {
            handlerProvider_.p = &mzid->provider;
            return Status(Status::Delegate, &handlerProvider_);
        }
        else if (name == "AuditCollection")
        {
            handlerContact_.c = &mzid->auditCollection;
            return Status(Status::Delegate, &handlerContact_);
        }
        else if (name == "AnalysisSampleCollection")
        {
            handlerAnalysisSampleCollection_.asc = &mzid->analysisSampleCollection;
            return Status(Status::Delegate, &handlerAnalysisSampleCollection_);
        }
        else if (name == "SequenceCollection")
        {
            handlerSequenceCollection_.sc = &mzid->sequenceCollection;
            return Status(Status::Delegate, &handlerSequenceCollection_);
        }
        else if (name == "AnalysisCollection")
        {
            handlerAnalysisCollection_.anal = &mzid->analysisCollection;
            return Status(Status::Delegate, &handlerAnalysisCollection_);
        }
        else if (name == "AnalysisProtocolCollection")
        {
            handlerAnalysisProtocolCollection_.anal = &mzid->analysisProtocolCollection;
            return Status(Status::Delegate, &handlerAnalysisProtocolCollection_);
        }
        else if (name == "DataCollection")
        {
            handlerDataCollection_.dc = &mzid->dataCollection;
            return Status(Status::Delegate, &handlerDataCollection_);
            
        }
        else if (name == "BibliographicReference")
        {
            mzid->bibliographicReference.push_back(
                BibliographicReferencePtr(new BibliographicReference));
            handlerBibliographicReference_.br = mzid->bibliographicReference.back().get();
            return Status(Status::Delegate, &handlerBibliographicReference_);
        }
        
        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "cvList")
            fixCVList(mzid->cvs);

            return Status::Ok;
    }
    
    private:
    HandlerCV handlerCV_;
    HandlerAnalysisSoftware handlerAnalysisSoftware_;
    HandlerContactVector handlerContact_;
    HandlerProvider handlerProvider_;
    HandlerAnalysisSampleCollection handlerAnalysisSampleCollection_;
    HandlerSequenceCollection handlerSequenceCollection_;
    HandlerAnalysisCollection handlerAnalysisCollection_;
    HandlerAnalysisProtocolCollection handlerAnalysisProtocolCollection_;
    HandlerDataCollection handlerDataCollection_;
    HandlerBibliographicReference handlerBibliographicReference_;
};

PWIZ_API_DECL void read(std::istream& is, MzIdentML& mzid)
{
    HandlerMzIdentML handler(&mzid);
    SAXParser::parse(is, handler);
    
    // "Fix" the name of the PSI-MS cv since the write's will assume
    // an id of MS
    fixCVList(mzid.cvs);
    
    References::resolve(mzid); 
}

} // namespace pwiz 
} // namespace mziddata 
} // namespace IO 

