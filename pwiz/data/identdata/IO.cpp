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
#include "TextWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace identdata {
namespace IO {


using namespace minimxml;
using namespace minimxml::SAXParser;
using namespace boost::logic;
using namespace util;


// indexes the SequenceCollection so that SpectrumIdentificationItems and PeptideEvidences
// can resolve references immediately
struct SequenceIndex
{
    // maps dBSequence_ref to DBSequence
    map<string, DBSequencePtr> dbSequences;

    // maps peptide_ref to Peptide
    map<string, PeptidePtr> peptides;

    // maps peptideEvidence_ref to PeptideEvidence
    map<string, PeptideEvidencePtr> peptideEvidence;

    // maps spectrumIdentificationItem_ref to SpectrumIdentificationItem
    map<string, SpectrumIdentificationItemPtr> spectrumIdentificationItems;
};


// convenience to support attribute name changes between schema versions
namespace {

#define ATTRIBUTE_NAME(name, v1_0_name, v1_1_name) \
    struct name##_attribute \
    { \
        name##_attribute() : v1_0_(v1_0_name), v1_1_(v1_1_name) {} \
        const string& operator() (int version) const {return version == (int) SchemaVersion_1_0 ? v1_0_ : v1_1_;} \
        private: const string v1_0_, v1_1_; \
    }; \
    name##_attribute name;

ATTRIBUTE_NAME(Role_element, "role", "Role")
ATTRIBUTE_NAME(Affiliation_element, "Affiliations", "Affiliation")
ATTRIBUTE_NAME(Parent_element, "parent", "Parent")
ATTRIBUTE_NAME(Seq_element, "seq", "Seq")
ATTRIBUTE_NAME(SubSample_element, "subSample", "SubSample")
ATTRIBUTE_NAME(PeptideSequence_element, "peptideSequence", "PeptideSequence")
ATTRIBUTE_NAME(SearchDatabaseRef_element, "SearchDatabase", "SearchDatabaseRef")
ATTRIBUTE_NAME(FileFormat_element, "fileFormat", "FileFormat")
ATTRIBUTE_NAME(SpectrumIDFormat_element, "spectrumIDFormat", "SpectrumIDFormat")
ATTRIBUTE_NAME(ExternalFormatDocumentation_element, "externalFormatDocumentation", "ExternalFormatDocumentation")

ATTRIBUTE_NAME(analysisSoftware_ref, "AnalysisSoftware_ref", "analysisSoftware_ref")
ATTRIBUTE_NAME(organization_ref, "Organization_ref", "organization_ref")
ATTRIBUTE_NAME(contact_ref, "Contact_ref", "contact_ref")
ATTRIBUTE_NAME(spectraData_ref, "SpectraData_ref", "spectraData_ref")
ATTRIBUTE_NAME(searchDatabase_ref, "SearchDatabase_ref", "searchDatabase_ref")
ATTRIBUTE_NAME(dBSequence_ref, "DBSequence_Ref", "dBSequence_ref")
ATTRIBUTE_NAME(peptideEvidence_ref, "PeptideEvidence_Ref", "peptideEvidence_ref")
ATTRIBUTE_NAME(peptide_ref, "Peptide_ref", "peptide_ref")
ATTRIBUTE_NAME(sample_ref, "Sample_ref", "sample_ref")
ATTRIBUTE_NAME(measure_ref, "Measure_ref", "measure_ref")
ATTRIBUTE_NAME(massTable_ref, "MassTable_ref", "massTable_ref")
ATTRIBUTE_NAME(translationTable_ref, "TranslationTable_ref", "translationTable_ref")
ATTRIBUTE_NAME(spectrumIdentificationProtocol_ref, "SpectrumIdentificationProtocol_ref", "spectrumIdentificationProtocol_ref")
ATTRIBUTE_NAME(spectrumIdentificationList_ref, "SpectrumIdentificationList_ref", "spectrumIdentificationList_ref")
ATTRIBUTE_NAME(proteinDetectionProtocol_ref, "ProteinDetectionProtocol_ref", "proteinDetectionProtocol_ref")
ATTRIBUTE_NAME(proteinDetectionList_ref, "ProteinDetectionList_ref", "proteinDetectionList_ref")
ATTRIBUTE_NAME(uri_attribute, "URI", "uri")
ATTRIBUTE_NAME(nTermGain_attribute, "NTermGain", "nTermGain")
ATTRIBUTE_NAME(cTermGain_attribute, "CTermGain", "cTermGain")
ATTRIBUTE_NAME(code_attribute, "Code", "code")
ATTRIBUTE_NAME(mass_attribute, "Mass", "mass")

} // namespace

void write(minimxml::XMLWriter& writer, const Contact& c)
{
    if (dynamic_cast<const Organization*>(&c))
        write(writer, static_cast<const Organization&>(c));
    else if (dynamic_cast<const Person*>(&c))
        write(writer, static_cast<const Person&>(c));
}

template <typename object_type>
void write(minimxml::XMLWriter& writer, const boost::shared_ptr<object_type>& objectPtr)
{
    if (objectPtr.get())
        write(writer, *objectPtr);
}

template <typename object_type>
void writeList(minimxml::XMLWriter& writer, const vector<object_type>& objects, 
               const string& label = "")
{
    if (!objects.empty())
    {
        XMLWriter::Attributes attributes;
        //attributes.add("count", objects.size());
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
        //attributes.add("count", objectPtrs.size());
        if (!label.empty())
            writer.startElement(label, attributes);
        for (typename vector<object_type>::const_iterator it=objectPtrs.begin(); it!=objectPtrs.end(); ++it)
            write(writer, **it);
        if (!label.empty())
            writer.endElement();
    }
}

template <typename object_type>
std::string makeDelimitedListString(const vector<object_type>& objects, const char* delimiter = " ")
{
    ostringstream oss;
    oss.precision(12);
    for (size_t i=0; i < objects.size(); ++i)
    {
        if (i > 0) oss << delimiter;
        oss << objects[i];
    }
    return oss.str();
}

template <typename object_type>
void parseDelimitedListString(vector<object_type>& objects, const string& delimitedList, const string& delimiter = " ")
{
    istringstream iss(delimitedList);
    iss >> std::noskipws;
    object_type value;
    while (iss >> value)
    {
        iss.seekg(delimiter.length(), iss.cur);
        objects.push_back(value);
    }
}


//
// CV
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const CV& cv)
{
    XMLWriter::Attributes attributes;
    attributes.add("id", cv.id);
    attributes.add("fullName", cv.fullName);
    attributes.add("version", cv.version);
    attributes.add("uri", cv.URI);
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
            throw runtime_error("[IO::HandlerCV] Unexpected element name: " + name);
        getAttribute(attributes, "id", cv->id);
        getAttribute(attributes, "fullName", cv->fullName);
        getAttribute(attributes, "version", cv->version);
        getAttribute(attributes, uri_attribute(version), cv->URI);
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
            throw runtime_error("[IO::HandlerCVParam] Unexpected element name: " + name);

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
            throw runtime_error("[IO::HandlerUserParam] Unexpected element name: " + name);

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

        throw runtime_error("[IO::HandlerParamContainer] Unknown element " + name); 
    }

private:

    HandlerCVParam handlerCVParam_;
    HandlerUserParam handlerUserParam_;
};


struct HandlerNamedCVParam : public HandlerCVParam
{
    string name_;

    HandlerNamedCVParam(const string& name, CVParam* _cvParam = 0)
        :   HandlerCVParam(_cvParam), name_(name)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == name_)
            return Status::Ok;

        return HandlerCVParam::startElement(name, attributes, position);
    }
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
// Adds attributes for Identifiable child classes.
void addIdAttributes(const Identifiable& id, XMLWriter::Attributes& attributes)
{
    attributes.add("id", id.id);
    if (!id.name.empty())
        attributes.add("name", id.name);
}

void addIdAttributes(const IdentifiableParamContainer& id, XMLWriter::Attributes& attributes)
{
    attributes.add("id", id.id);
    if (!id.name.empty())
        attributes.add("name", id.name);
}

//
// HandlerString
//
struct HandlerString : public SAXParser::Handler
{
    string* str;
    
    HandlerString(string* str_ = 0) : str(str_) {parseCharacters = true;}
    virtual ~HandlerString() {}
    
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!str)
            throw runtime_error("[IO::HandlerNamedString] Null string.");

        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        *str = text.c_str();

        return Status::Ok;
    }

};

//
// Identifiable
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Identifiable& it)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(it, attributes);
    writer.startElement("FakeIdentifiable", attributes, XMLWriter::EmptyElement);
}

struct HandlerIdentifiable : public SAXParser::Handler
{
    Identifiable* id;
    HandlerIdentifiable(Identifiable* _id = 0) : id(_id) {}
    virtual ~HandlerIdentifiable() {}
    
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!id)
            throw runtime_error("[IO::HandlerIdentifiable] Null Identifiable.");

        getAttribute(attributes, "id", id->id);
        getAttribute(attributes, "name", id->name);

        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, Identifiable& it)
{
    HandlerIdentifiable handler(&it);
    SAXParser::parse(is, handler);
}

//
// IdentifiableParamContainer
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const IdentifiableParamContainer& it)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(it, attributes);
    if (it.ParamContainer::empty())
        writer.startElement("FakeIdentifiableParamContainer", attributes, XMLWriter::EmptyElement);
    else
    {
        writer.startElement("FakeIdentifiableParamContainer", attributes);
        writeParamContainer(writer, it);
        writer.endElement();
    }
}

struct HandlerIdentifiableParamContainer : public HandlerParamContainer
{
    IdentifiableParamContainer* id;
    HandlerIdentifiableParamContainer(IdentifiableParamContainer* _id = 0)
        : HandlerParamContainer(_id), id(_id) {}

    virtual ~HandlerIdentifiableParamContainer() {}
    
    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!id)
            throw runtime_error("[IO::HandlerIdentifiableParamContainer] Null IdentifiableParamContainer.");

        if (name == "cvParam" || name == "userParam")
        {
            paramContainer = id;
            return HandlerParamContainer::startElement(name, attributes, position);
        }
        else
        {
            getAttribute(attributes, "id", id->id);
            getAttribute(attributes, "name", id->name);
            return Status::Ok;
        }
    }
};

PWIZ_API_DECL void read(std::istream& is, IdentifiableParamContainer& it)
{
    HandlerIdentifiableParamContainer handler(&it);
    SAXParser::parse(is, handler);
}


//
// BibliographicReference
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const BibliographicReference& br)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(br, attributes);
    attributes.add("authors", br.authors);
    attributes.add("publication", br.publication);
    attributes.add("publisher", br.publisher);
    attributes.add("editor", br.editor);
    attributes.add("year", br.year);
    attributes.add("volume", br.volume);
    attributes.add("issue", br.issue);
    attributes.add("pages", br.pages);
    attributes.add("title", br.title);
    
    writer.startElement("BibliographicReference", attributes, XMLWriter::EmptyElement);
}


struct HandlerBibliographicReference : public HandlerIdentifiable
{
    BibliographicReference* br;
    HandlerBibliographicReference(BibliographicReference* _br = 0)
        : br(_br) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "BibliographicReference")
            throw runtime_error("[IO::HandlerBibliographicReference] Unexpected element name: " + name);

        
        getAttribute(attributes, "authors", br->authors);
        getAttribute(attributes, "publication", br->publication);
        getAttribute(attributes, "publisher", br->publisher);
        getAttribute(attributes, "editor", br->editor);
        getAttribute(attributes, "year", br->year);
        getAttribute(attributes, "volume", br->volume);
        getAttribute(attributes, "issue", br->issue);
        getAttribute(attributes, "pages", br->pages);
        getAttribute(attributes, "title", br->title);

        HandlerIdentifiable::id = br;
        return HandlerIdentifiable::startElement(name, attributes, position);
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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DBSequence& ds)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(ds, attributes);
    //attributes.add("id", ds.id);
    if (ds.length > 0)
        attributes.add("length", ds.length);
    attributes.add("accession", ds.accession);
    if (ds.searchDatabasePtr.get())
        attributes.add("searchDatabase_ref", ds.searchDatabasePtr->id);
    
    if (!ds.ParamContainer::empty() || !ds.seq.empty())
    {
        writer.startElement("DBSequence", attributes);

        if (!ds.seq.empty())
        {
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("Seq");
            writer.characters(ds.seq);
            writer.endElement();
            writer.popStyle();
        }

        writeParamContainer(writer, ds);
        writer.endElement();
    }
    else
        writer.startElement("DBSequence", attributes, XMLWriter::EmptyElement);
}


struct HandlerDBSequence : public HandlerIdentifiableParamContainer
{
    DBSequence* ds;
    bool inSeq;

    HandlerDBSequence(DBSequence* _ds = 0)
        : ds(_ds), inSeq(false)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

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
            getAttribute(attributes, searchDatabase_ref(version), value);
            ds->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase(value));
            HandlerIdentifiableParamContainer::id = ds;
        }
        else if (name == Seq_element(version))
        {
            inSeq = true;
            return Status::Ok;
        }
        
        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }

    virtual Status characters(const SAXParser::saxstring& text, 
                              stream_offset position)
    {
        if (inSeq)
            ds->seq = text.c_str();
        else
            throw runtime_error("[IO::HandlerDBSequence] Unexpected characters found");

        return Status::Ok;
    }

    
    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == Seq_element(version))
            inSeq = false;

        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, DBSequence& ds)
{
    HandlerDBSequence handler(&ds);
    SAXParser::parse(is, handler);
}


//
// Modification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Modification& mod)
{
    XMLWriter::Attributes attributes;
    attributes.add("location", mod.location);
    if (!mod.residues.empty())
        attributes.add("residues", makeDelimitedListString(mod.residues));
    if (mod.avgMassDelta != 0)
        attributes.add("avgMassDelta", mod.avgMassDelta);
    //if (mod.monoisotopicMassDelta > 0)
    attributes.add("monoisotopicMassDelta", mod.monoisotopicMassDelta);


    XMLWriter::EmptyElementTag elementTag = mod.ParamContainer::empty() ?
        XMLWriter::EmptyElement : XMLWriter::NotEmptyElement;
    writer.startElement("Modification", attributes, elementTag);
    if (!mod.ParamContainer::empty())
    {
        writeParamContainer(writer, mod);
        writer.endElement();
    }
}


struct HandlerModification : public HandlerParamContainer
{
    Modification* mod;
    HandlerModification(Modification* _mod = 0) : mod(_mod) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Modification")
        {
            string residues;
            getAttribute(attributes, "residues", residues);
            parseDelimitedListString(mod->residues, residues);

            getAttribute(attributes, "location", mod->location);
            getAttribute(attributes, "avgMassDelta", mod->avgMassDelta);
            getAttribute(attributes, "monoisotopicMassDelta", mod->monoisotopicMassDelta);
            HandlerParamContainer::paramContainer = mod;
            return Status::Ok;
        }

        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Modification& mod)
{
    HandlerModification handler(&mod);
    SAXParser::parse(is, handler);
}

//
// SubstitutionModification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SubstitutionModification& sm)
{
    XMLWriter::Attributes attributes;
    if (sm.originalResidue != 0)
        attributes.add("originalResidue", sm.originalResidue);
    if (sm.replacementResidue != 0)
        attributes.add("replacementResidue", sm.replacementResidue);
    if (sm.location != 0)
        attributes.add("location", boost::lexical_cast<string>(sm.location));
    if (sm.avgMassDelta != 0)
        attributes.add("avgMassDelta", boost::lexical_cast<string>(sm.avgMassDelta));
    if (sm.monoisotopicMassDelta != 0)
        attributes.add("monoisotopicMassDelta", boost::lexical_cast<string>(sm.monoisotopicMassDelta));
    
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
            throw runtime_error("[IO::HandlerSubstitutionModification] Unexpected element name: " + name);

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
    HandlerSubstitutionModification handler(&sm);
    SAXParser::parse(is, handler);
}


//
// Peptide
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Peptide& peptide)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(peptide, attributes);

    // if peptide has mods or params, it takes up more than a single line
    bool hasModsOrParams = !peptide.modification.empty() ||
                           !peptide.substitutionModification.empty() ||
                           !peptide.ParamContainer::empty();

    if (!hasModsOrParams)
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("Peptide", attributes);

    if (!hasModsOrParams)
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
    else
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("PeptideSequence");
    writer.characters(peptide.peptideSequence);
    writer.endElement();
    writer.popStyle();

    if (!peptide.modification.empty())
        writeList(writer, peptide.modification);
    if (!peptide.substitutionModification.empty())
        writeList(writer, peptide.substitutionModification);

    writeParamContainer(writer, peptide);
    writer.endElement();

    if (!hasModsOrParams)
        writer.popStyle();
}


struct HandlerPeptide : public HandlerIdentifiableParamContainer
{
    bool inPeptideSequence;
    Peptide* peptide;

    HandlerPeptide(Peptide* _peptide = 0)
        : inPeptideSequence(false), peptide(_peptide)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!peptide)
            throw runtime_error("[IO::HandlerPeptide] Null Peptide.");
        
        if (name == "Peptide")
        {
            HandlerIdentifiableParamContainer::id = peptide;
        }
        else if (name == PeptideSequence_element(version))
        {
            inPeptideSequence = true;
            return Status::Ok;
        }
        else if (name == "Modification")
        {
            peptide->modification.push_back(ModificationPtr(new Modification));
            handlerModification_.version = version;
            handlerModification_.mod = peptide->modification.back().get();
            return Status(Status::Delegate, &handlerModification_);
        }
        else if (name == "SubstitutionModification")
        {
            peptide->substitutionModification.push_back(SubstitutionModificationPtr(new SubstitutionModification));
            handlerSubstitutionModification_.version = version;
            handlerSubstitutionModification_.subMod = peptide->substitutionModification.back().get();
            return Status(Status::Delegate, &handlerSubstitutionModification_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }

    virtual Status characters(const SAXParser::saxstring& text, 
                              stream_offset position)
    {
        if (inPeptideSequence)
            peptide->peptideSequence = text.c_str();
        else
            throw runtime_error("[IO::HandlerPeptide] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == PeptideSequence_element(version))
            inPeptideSequence = false;

        return Status::Ok;
    }
    private:
    HandlerModification handlerModification_;
    HandlerSubstitutionModification handlerSubstitutionModification_;
};


PWIZ_API_DECL void read(std::istream& is, Peptide& peptide)
{
    HandlerPeptide handler(&peptide);
    SAXParser::parse(is, handler);
}


//
// PeptideEvidence
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideEvidence& pep)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pep, attributes);

    if (pep.peptidePtr.get() && !pep.peptidePtr->empty())
        attributes.add("peptide_ref", pep.peptidePtr->id);
    if (pep.dbSequencePtr.get() && !pep.dbSequencePtr->empty())
        attributes.add("dBSequence_ref", pep.dbSequencePtr->id);

    // don't output these optional attributes if they haven't been set
    if (pep.start > 0 || pep.end > pep.start)
    {
        attributes.add("start", pep.start);
        attributes.add("end", pep.end);
    }

    if (pep.pre != 0)
        attributes.add("pre", pep.pre);
    if (pep.post != 0)
        attributes.add("post", pep.post);
    if (pep.translationTablePtr.get() && !pep.translationTablePtr->empty())
        attributes.add("translationTable_ref", pep.translationTablePtr->id);
    if (pep.frame != 0)
        attributes.add("frame", pep.frame);
    attributes.add("isDecoy", pep.isDecoy  ? "true" : "false");

    if (!pep.ParamContainer::empty())
    {
        writer.startElement("PeptideEvidence", attributes);
        writeParamContainer(writer, pep);
        writer.endElement();
    }
    else
        writer.startElement("PeptideEvidence", attributes, XMLWriter::EmptyElement);
}


struct HandlerPeptideEvidence : public HandlerIdentifiableParamContainer
{
    PeptideEvidence* pep;
    SequenceIndex& sequenceIndex;

    HandlerPeptideEvidence(SequenceIndex& sequenceIndex, PeptideEvidence* _pep = 0)
    : pep(_pep), sequenceIndex(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pep)
            throw runtime_error("[IO::HandlerPeptideEvidence] Null PeptideEvidence.");

        if (name == "PeptideEvidence")
        {
            string value;

            if (version != SchemaVersion_1_0)
            {
                getAttribute(attributes, "peptide_ref", value);
                map<string, PeptidePtr>::const_iterator findItr = sequenceIndex.peptides.find(value);
                if (findItr == sequenceIndex.peptides.end())
                    pep->peptidePtr = PeptidePtr(new Peptide(value));
                else
                    pep->peptidePtr = findItr->second;
            }

            getAttribute(attributes, dBSequence_ref(version), value);
            map<string, DBSequencePtr>::const_iterator findItr = sequenceIndex.dbSequences.find(value);
            if (findItr == sequenceIndex.dbSequences.end())
                pep->dbSequencePtr = DBSequencePtr(new DBSequence(value));
            else
                pep->dbSequencePtr = findItr->second;

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
            getAttribute(attributes, translationTable_ref(version), value);
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

            HandlerIdentifiableParamContainer::id = pep;
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, PeptideEvidence& pep)
{
    SequenceIndex dummy;
    HandlerPeptideEvidence handler(dummy, &pep);
    SAXParser::parse(is, handler);
}


//
// SequenceCollection
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SequenceCollection& sc,
                         const IterationListenerRegistry* ilr)
{
    XMLWriter::Attributes attributes;

    writer.startElement("SequenceCollection", attributes);

    int iterationIndex = 0;
    int iterationCount = sc.dbSequences.size();
    BOOST_FOREACH(const DBSequencePtr& dbSequence, sc.dbSequences)
    {
        if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(iterationIndex++, iterationCount, "writing protein sequences")) == IterationListener::Status_Cancel)
            return;

        write(writer, *dbSequence);
    }

    iterationIndex = 0;
    iterationCount = sc.peptides.size();
    BOOST_FOREACH(const PeptidePtr& peptide, sc.peptides)
    {
        if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(iterationIndex++, iterationCount, "writing peptide sequences")) == IterationListener::Status_Cancel)
            return;

        write(writer, *peptide);
    }

    iterationIndex = 0;
    iterationCount = sc.peptideEvidence.size();
    BOOST_FOREACH(const PeptideEvidencePtr& peptideEvidence, sc.peptideEvidence)
    {
        if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(iterationIndex++, iterationCount, "writing peptide evidence")) == IterationListener::Status_Cancel)
            return;

        write(writer, *peptideEvidence);
    }

    writer.endElement();
}


struct HandlerSequenceCollection : public SAXParser::Handler
{
    SequenceCollection* sc;
    SequenceIndex& sequenceIndex;
    SequenceCollectionFlag sequenceCollectionFlag;

    HandlerSequenceCollection(SequenceIndex& sequenceIndex,
                              SequenceCollection* _sc = 0,
                              const IterationListenerRegistry* iterationListenerRegistry = 0,
                              SequenceCollectionFlag sequenceCollectionFlag = ReadSequenceCollection)
    : sc(_sc),
      sequenceIndex(sequenceIndex),
      sequenceCollectionFlag(sequenceCollectionFlag),
      ilr_(iterationListenerRegistry),
      handlerPeptideEvidence_(sequenceIndex, 0)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sc)
            throw runtime_error("[IO::HandlerSequenceCollection] Null HandlerSequenceCollection");

        if (sequenceCollectionFlag == IgnoreSequenceCollection)
            return Status::Ok;

        if (name == "SequenceCollection")
        {
            // Ignore 
        }
        else if (name == "DBSequence")
        {
            if (ilr_ && ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(sc->dbSequences.size(), 0, "reading protein sequences")) == IterationListener::Status_Cancel)
                return Status::Done;

            string id; getAttribute(attributes, "id", id);
            sc->dbSequences.push_back(DBSequencePtr(new DBSequence(id)));
            sequenceIndex.dbSequences[id] = sc->dbSequences.back();
            handlerDBSequence_.version = version;
            handlerDBSequence_.ds = sc->dbSequences.back().get();
            return Status(Status::Delegate, &handlerDBSequence_); 
        }
        else if (name == "Peptide")
        {
            if (ilr_ && ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(sc->peptides.size(), 0, "reading peptide sequences")) == IterationListener::Status_Cancel)
                return Status::Done;

            string id; getAttribute(attributes, "id", id);
            sc->peptides.push_back(PeptidePtr(new Peptide(id)));
            sequenceIndex.peptides[id] = sc->peptides.back();
            handlerPeptide_.version = version;
            handlerPeptide_.peptide = sc->peptides.back().get();
            return Status(Status::Delegate, &handlerPeptide_);
        }
        else if (name == "PeptideEvidence")
        {
            if (ilr_ && ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(sc->peptideEvidence.size(), 0, "reading peptide evidence")) == IterationListener::Status_Cancel)
                return Status::Done;

            string id; getAttribute(attributes, "id", id);
            sc->peptideEvidence.push_back(PeptideEvidencePtr(new PeptideEvidence(id)));
            sequenceIndex.peptideEvidence[id] = sc->peptideEvidence.back();
            handlerPeptideEvidence_.version = version;
            handlerPeptideEvidence_.pep = sc->peptideEvidence.back().get();
            return Status(Status::Delegate, &handlerPeptideEvidence_);
        }
        else
            throw runtime_error("[IO::HandlerSequenceCollection] Unexpected element name: " + name);
        return Status::Ok;
    }

    private:
    const IterationListenerRegistry* ilr_;
    HandlerDBSequence handlerDBSequence_;
    HandlerPeptide handlerPeptide_;
    HandlerPeptideEvidence handlerPeptideEvidence_;
};


PWIZ_API_DECL void read(std::istream& is, SequenceCollection& sc,
                        const IterationListenerRegistry* iterationListenerRegistry)
{
    SequenceIndex dummy;
    HandlerSequenceCollection handler(dummy, &sc, iterationListenerRegistry);
    SAXParser::parse(is, handler);
}


//
// Contact
//


struct HandlerContact : public HandlerIdentifiableParamContainer
{
    Contact* c;
    HandlerContact(Contact* _c = 0) : c(_c) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!c)
            throw runtime_error("[IO::HandlerContact] Null Contact.");

        if (version == SchemaVersion_1_0)
        {
            string value;
            getAttribute(attributes, "address", value);
            if (!value.empty()) c->set(MS_contact_address, value);

            getAttribute(attributes, "phone", value);
            if (!value.empty()) c->set(MS_contact_phone_number, value);

            getAttribute(attributes, "email", value);
            if (!value.empty()) c->set(MS_contact_email, value);

            getAttribute(attributes, "fax", value);
            if (!value.empty()) c->set(MS_contact_fax_number, value);

            getAttribute(attributes, "tollFreePhone", value);
            if (!value.empty()) c->set(MS_contact_toll_free_phone_number, value);
        }

        HandlerIdentifiableParamContainer::id = c;
        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
};


//
// Person
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Person& pp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pp, attributes);

    attributes.add("lastName", pp.lastName);
    attributes.add("firstName", pp.firstName);

    if (!pp.midInitials.empty())
        attributes.add("midInitials", pp.midInitials);

    writer.startElement("Person", attributes);
    writeParamContainer(writer, pp);

    BOOST_FOREACH(const OrganizationPtr& org, pp.affiliations)
    {
        attributes.clear();
        attributes.add("organization_ref", org->id);
        writer.startElement("Affiliation", attributes, XMLWriter::EmptyElement);
    }

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
        }
        else if (name == Affiliation_element(version))
        {
            per->affiliations.push_back(OrganizationPtr(new Organization));
            getAttribute(attributes, organization_ref(version), per->affiliations.back()->id);
            return Status::Ok;
        }

        return HandlerContact::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, Person& pp)
{
    HandlerPerson handler(&pp);
    SAXParser::parse(is, handler);
}


//
// Organization
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Organization& op)
{
    XMLWriter::Attributes attributes;
    
    addIdAttributes(op, attributes);

    if (op.parent.get() || !op.ParamContainer::empty())
    {
        writer.startElement("Organization", attributes);
        writeParamContainer(writer, op);

        if (op.parent.get())
        {
            attributes.clear();
            attributes.add("organization_ref", op.parent->id);
            writer.startElement("Parent", attributes, XMLWriter::EmptyElement);
        }

        writer.endElement();
    }
    else
        writer.startElement("Organization", attributes, XMLWriter::EmptyElement);
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
        }
        else if (name == Parent_element(version))
        {
            org->parent.reset(new Organization);
            getAttribute(attributes, organization_ref(version), org->parent->id);
            return Status::Ok;
        }

        return HandlerContact::startElement(name, attributes, position);
    }
};


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
            handlerPerson_.version = version;
            handlerPerson_.per = (Person*)c->back().get();
            return Status(Status::Delegate, &handlerPerson_);
        }
        else if (name == "Organization")
        {
            c->push_back(OrganizationPtr(new Organization()));
            handlerOrganization_.version = version;
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
    attributes.add("contact_ref", cr.contactPtr->id);

    writer.startElement("ContactRole", attributes);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("Role");
    writer.pushStyle(XMLWriter::StyleFlag_Inline);
    write(writer, (const CVParam&)cr);
    writer.popStyle();
    writer.endElement();
    writer.popStyle();

    writer.endElement();
}


struct HandlerContactRole : public HandlerNamedCVParam
{
    ContactRole* cr;
    HandlerContactRole(ContactRole* _cr = 0)
    : HandlerNamedCVParam("Role", _cr),
      cr(_cr)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!cr)
            throw runtime_error("NULL ContactRole");

        if (name == "ContactRole")
        {
            cr->contactPtr.reset(new Contact);
            getAttribute(attributes, contact_ref(version), cr->contactPtr->id);

            // "role" or "Role"
            name_ = Role_element(version);
        }
        else
            return HandlerNamedCVParam::startElement(name, attributes, position);
        
        
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
    if (!anal.version.empty())
        attributes.add("version", anal.version);
    if (!anal.URI.empty())
        attributes.add("uri", anal.URI);

    writer.startElement("AnalysisSoftware", attributes);

    if (anal.contactRolePtr.get() && !anal.contactRolePtr->empty())
        write(writer, *anal.contactRolePtr);

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("SoftwareName");
    writer.pushStyle(XMLWriter::StyleFlag_Inline);
    writeParamContainer(writer, anal.softwareName);
    writer.popStyle();
    writer.endElement();
    writer.popStyle();

    if (!anal.customizations.empty())
    {
        writer.startElement("Customizations");
        writer.characters(anal.customizations);
        writer.endElement();
    }
    
    writer.endElement();
}

struct HandlerAnalysisSoftware : public HandlerIdentifiable
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
        //    throw runtime_error("[IO::HandlerAnalysisSoftware] Unexpected element name: " + name);

        if (!anal)
            throw runtime_error("[IO::HandlerAnalysisSoftware] Null AnalysisSoftware.");

        if (name == "AnalysisSoftware")
        {
            getAttribute(attributes, "version", anal->version);
            getAttribute(attributes, uri_attribute(version), anal->URI);
            getAttribute(attributes, "customizations", anal->customizations);

            HandlerIdentifiable::id = anal;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "ContactRole")
        {
            anal->contactRolePtr = ContactRolePtr(new ContactRole());
            handlerContactRole_.version = version;
            handlerContactRole_.cvParam = handlerContactRole_.cr = anal->contactRolePtr.get();
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
        attributes.add("spectrumIdentificationProtocol_ref",
                       sip.spectrumIdentificationProtocolPtr->id);
    if (sip.spectrumIdentificationListPtr.get())
        attributes.add("spectrumIdentificationList_ref",
                       sip.spectrumIdentificationListPtr->id);
    attributes.add("activityDate", sip.activityDate);

    writer.startElement("SpectrumIdentification", attributes);

    for (vector<SpectraDataPtr>::const_iterator it=sip.inputSpectra.begin();
         it != sip.inputSpectra.end(); it++)
    {
        if (!(*it).get()) continue;

        attributes.clear();
        attributes.add("spectraData_ref", (*it)->id);
        writer.startElement("InputSpectra", attributes, XMLWriter::EmptyElement);
    }

    for (vector<SearchDatabasePtr>::const_iterator it=sip.searchDatabase.begin();
         it != sip.searchDatabase.end(); it++)
    {
        if (!(*it).get()) continue;

        attributes.clear();
        attributes.add("searchDatabase_ref", (*it)->id);
        writer.startElement("SearchDatabaseRef", attributes, XMLWriter::EmptyElement);
    }

    writer.endElement();
}

struct HandlerSpectrumIdentification : public HandlerIdentifiable
{
    SpectrumIdentification* spectrumId;
    HandlerSpectrumIdentification(SpectrumIdentification* _spectrumId = 0)
        : HandlerIdentifiable(_spectrumId), spectrumId(_spectrumId) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumId)
            throw runtime_error("[IO::HandlerSpectrumIdentification] Null spectrumId.");
        
        if (name == "SpectrumIdentification")
        {
            string  value;
            getAttribute(attributes, spectrumIdentificationProtocol_ref(version), value);
            spectrumId->spectrumIdentificationProtocolPtr = SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol(value));

            value.clear();
            getAttribute(attributes, spectrumIdentificationList_ref(version), value);
            spectrumId->spectrumIdentificationListPtr = SpectrumIdentificationListPtr(new SpectrumIdentificationList(value));
            getAttribute(attributes, "activityDate", spectrumId->activityDate);

            HandlerIdentifiable::id = spectrumId;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "InputSpectra")
        {
            string value;
            getAttribute(attributes, spectraData_ref(version), value);
            spectrumId->inputSpectra.push_back(
                SpectraDataPtr(new SpectraData(value)));
        }
        else if (name == SearchDatabaseRef_element(version))
        {
            spectrumId->searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase));
            getAttribute(attributes, searchDatabase_ref(version), spectrumId->searchDatabase.back()->id);
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
        attributes.add("proteinDetectionProtocol_ref", pd.proteinDetectionProtocolPtr->id);
    if (pd.proteinDetectionListPtr.get())
        attributes.add("proteinDetectionList_ref", pd.proteinDetectionListPtr->id);
    attributes.add("activityDate", pd.activityDate);

    writer.startElement("ProteinDetection", attributes);

    for (vector<SpectrumIdentificationListPtr>::const_iterator it=pd.inputSpectrumIdentifications.begin();
         it!=pd.inputSpectrumIdentifications.end(); it++)
    {
        if (!it->get())
            continue;
        
        attributes.clear();
        attributes.add("spectrumIdentificationList_ref", (*it)->id);
        writer.startElement("InputSpectrumIdentifications", attributes, XMLWriter::EmptyElement);
    }

    writer.endElement();
}


struct HandlerProteinDetection : public HandlerIdentifiable
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
            pd->proteinDetectionProtocolPtr = ProteinDetectionProtocolPtr(new ProteinDetectionProtocol);
            getAttribute(attributes, proteinDetectionProtocol_ref(version), pd->proteinDetectionProtocolPtr->id);

            pd->proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList);
            getAttribute(attributes, proteinDetectionList_ref(version), pd->proteinDetectionListPtr->id);

            getAttribute(attributes, "activityDate", pd->activityDate);

            HandlerIdentifiable::id = pd;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "InputSpectrumIdentifications")
        {
            SpectrumIdentificationListPtr ref(new SpectrumIdentificationList);
            getAttribute(attributes, spectrumIdentificationList_ref(version), ref->id);
            pd->inputSpectrumIdentifications.push_back(ref);
        }
        else
            throw runtime_error("[IO::HandlerProteinDetection] Unexpected element name: " + name);

        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, ProteinDetection& pd)
{
    HandlerProteinDetection handler(&pd);
    SAXParser::parse(is, handler);
}


//
// Sample
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Sample& sample)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sample, attributes);

    if (sample.subSamples.empty() || !sample.ParamContainer::empty() || !sample.contactRole.empty())
    {
        writer.startElement("Sample", attributes);

        BOOST_FOREACH(const ContactRolePtr& cr, sample.contactRole)
            write(writer, *cr);

        BOOST_FOREACH(const SamplePtr& subSample, sample.subSamples)
        {
            attributes.clear();
            attributes.add("sample_ref", subSample->id);
            writer.startElement("SubSample", attributes, XMLWriter::EmptyElement);
        }
        writeParamContainer(writer, sample);
        writer.endElement();
    }
    else
        writer.startElement("Sample", attributes, XMLWriter::EmptyElement);
}


struct HandlerSample : public HandlerIdentifiableParamContainer
{
    Sample* sample;
    HandlerSample(Sample* _sample = 0) : sample(_sample) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sample)
            throw runtime_error("[IO::HandlerSample] Null Sample");

        if (name == "ContactRole")
        {
            sample->contactRole.push_back(ContactRolePtr(new ContactRole()));
            handlerContactRole_.version = version;
            handlerContactRole_.cvParam = handlerContactRole_.cr = sample->contactRole.back().get();
            return Status(Status::Delegate, &handlerContactRole_);
        }
        else if (name == SubSample_element(version))
        {
            sample->subSamples.push_back(SamplePtr(new Sample));
            getAttribute(attributes, sample_ref(version), sample->subSamples.back()->id);
        }
        else
        {
            HandlerIdentifiableParamContainer::id = sample;
            return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
        }

        return Status::Ok;
    }

    private:
    HandlerContactRole handlerContactRole_;
};


PWIZ_API_DECL void read(std::istream& is, Sample& sample)
{
    HandlerSample handler(&sample);
    SAXParser::parse(is, handler);
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
            handlerSpectrumIdentification_.version = version;
            handlerSpectrumIdentification_.spectrumId = anal->spectrumIdentification.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentification_);
        }
        else if (name == "ProteinDetection")
        {
            handlerProteinDetection_.version = version;
            handlerProteinDetection_.pd = &anal->proteinDetection;
            return Status(Status::Delegate, &handlerProteinDetection_);
        }
        else
            throw runtime_error("[IO::HandlerAnalysisCollection] Unknown tag "+name);
        
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
// IdentData has a perverse encoding for a non-specific search:
// the enzymeName must be written as MS_NoEnzyme;
// pwiz preserves the real enzyme in the data model with the siteRegexp

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Enzyme& ez)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(ez, attributes);
    if (!ez.cTermGain.empty())
        attributes.add("cTermGain", ez.cTermGain);
    if (!ez.nTermGain.empty())
        attributes.add("nTermGain", ez.nTermGain);
    if (ez.missedCleavages != 0)
        attributes.add("missedCleavages", ez.missedCleavages);
    if (ez.minDistance != 0)
        attributes.add("minDistance", ez.minDistance);

    attributes.add("semiSpecific", ez.terminalSpecificity == proteome::Digestion::FullySpecific ? "false" : "true");

    writer.startElement("Enzyme", attributes);

    if (!ez.siteRegexp.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("SiteRegexp");
        writer.characters(ez.siteRegexp);
        writer.endElement();
        writer.popStyle();
    }

    if (ez.terminalSpecificity == proteome::Digestion::NonSpecific)
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("EnzymeName");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        write(writer, CVParam(MS_unspecific_cleavage));
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
    }
    else if (!ez.enzymeName.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("EnzymeName");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        writeParamContainer(writer, ez.enzymeName);
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
    }

    writer.endElement();
}


struct HandlerEnzyme : public HandlerIdentifiable
{
    Enzyme* ez;
    bool inSiteRegexp;
    HandlerEnzyme(Enzyme* _ez = 0)
        : ez(_ez), inSiteRegexp(false),
          handlerNamedParamContainer_("EnzymeName")
    {
        parseCharacters = true;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Enzyme")
        {
            getAttribute(attributes, "id", ez->id);
            getAttribute(attributes, nTermGain_attribute(version), ez->nTermGain);
            getAttribute(attributes, cTermGain_attribute(version), ez->cTermGain);
            getAttribute(attributes, "missedCleavages", ez->missedCleavages, 0);
            getAttribute(attributes, "minDistance", ez->minDistance, 0);
            getAttribute(attributes, "semiSpecific", _semiSpecific);

            HandlerIdentifiable::id = ez;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "SiteRegexp")
            inSiteRegexp = true;
        else if (name == "EnzymeName")
        {
            handlerNamedParamContainer_.paramContainer = &ez->enzymeName;
            return Status(Status::Delegate, &handlerNamedParamContainer_);
        }
        else
            throw runtime_error("[IO::HandlerEnzyme] Unexpected element name: " + name);
        return Status::Ok;
    }
    
    virtual Status characters(const SAXParser::saxstring& text, 
                              stream_offset position)
    {
        if (inSiteRegexp)
            ez->siteRegexp = text.c_str();
        else
            throw runtime_error("[IO::HandlerEnzyme] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "SiteRegexp")
            inSiteRegexp = false;
        else if (name == "Enzyme")
        {
            if (ez->enzymeName.hasCVParam(MS_unspecific_cleavage))
            {
                ez->enzymeName.clear();
                CVID cleavageAgent = proteome::Digestion::getCleavageAgentByRegex(ez->siteRegexp);
                if (cleavageAgent != CVID_Unknown)
                    ez->enzymeName.set(cleavageAgent);
                ez->terminalSpecificity = proteome::Digestion::NonSpecific;
            }
            else if (_semiSpecific == "true")
                ez->terminalSpecificity = proteome::Digestion::SemiSpecific;
        }

        return Status::Ok;
    }
    
    private:
    HandlerNamedParamContainer handlerNamedParamContainer_;
    string _semiSpecific;
};


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
    if (!indeterminate(ez.independent))
        attributes.add("independent", ez.independent ? "true" : "false");
    
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
            handlerEnzyme_.version = version;
            handlerEnzyme_.ez = ez->enzymes.back().get();
            return Status(Status::Delegate, &handlerEnzyme_);
        }
        else
            throw runtime_error("[IO::HandlerEnzymes] Unexpected element name: " + name);

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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Residue& residue)
{
    XMLWriter::Attributes attributes;

    if (residue.code != 0)
        attributes.add("code", residue.code);
    attributes.add("mass", residue.mass);

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
            throw runtime_error("[IO::HandlerResidue] Unexpected element name: " + name);

        getAttribute(attributes, code_attribute(version), residue->code);
        getAttribute(attributes, mass_attribute(version), residue->mass);
        
        return Status::Ok;
    }
};


PWIZ_API_DECL void read(std::istream& is, Residue& residue)
{
    HandlerResidue handler(&residue);
    SAXParser::parse(is, handler);
}


//
// AmbiguousResidue
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AmbiguousResidue& residue)
{
    XMLWriter::Attributes attributes;
    if (residue.code != 0)
        attributes.add("code", residue.code);
    
    writer.startElement("AmbiguousResidue", attributes);
    writeParamContainer(writer, residue);
    writer.endElement();
}


struct HandlerAmbiguousResidue : public HandlerParamContainer
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
            getAttribute(attributes, code_attribute(version), residue->code);
            HandlerParamContainer::paramContainer = residue;
            return Status::Ok;
        }

        return HandlerParamContainer::startElement(name, attributes, position);
    }
};


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
    attributes.add("id", mt.id);
    attributes.add("msLevel", makeDelimitedListString(mt.msLevel));
    
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

            string values;
            getAttribute(attributes, "msLevel", values);
            parseDelimitedListString(mt->msLevel, values);
        }
        else if (name == "Residue")
        {
            mt->residues.push_back(ResiduePtr(new Residue()));
            handlerResidue_.version = version;
            handlerResidue_.residue = mt->residues.back().get();
            return handlerResidue_.startElement(name, attributes, position);
        }
        else if (name == "AmbiguousResidue")
        {
            mt->ambiguousResidue.push_back(AmbiguousResiduePtr(new AmbiguousResidue()));
            handlerAmbiguousResidue_.version = version;
            handlerAmbiguousResidue_.residue = mt->ambiguousResidue.back().get();
            return Status(Status::Delegate, &handlerAmbiguousResidue_);
        }
        else
            throw runtime_error("[IO::HandlerMassTable] Unexpected element name: " + name);
        
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
// SearchModification
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchModification& sm)
{
    XMLWriter::Attributes attributes;
    attributes.add("fixedMod", sm.fixedMod ? "true" : "false");
    attributes.add("massDelta", sm.massDelta);
    attributes.add("residues", makeDelimitedListString(sm.residues));

    bool emptyElement = sm.specificityRules.empty() && static_cast<const ParamContainer&>(sm).empty();

    if (!emptyElement)
    {
        writer.startElement("SearchModification", attributes);

        if (!sm.specificityRules.empty())
        {
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("SpecificityRules");
            writer.pushStyle(XMLWriter::StyleFlag_Inline);
            write(writer, sm.specificityRules);
            writer.popStyle();
            writer.endElement();
            writer.popStyle();
        }

        writeParamContainer(writer, sm);
        
        writer.endElement();
    }
    else
        writer.startElement("SearchModification", attributes, XMLWriter::EmptyElement);
}


struct HandlerSearchModification : public HandlerParamContainer
{
    SearchModification* sm;
    HandlerSearchModification(SearchModification* _sm = 0)
        : sm(_sm),
          handlerSpecificityRules_("SpecificityRules")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SearchModification")
        {
            string value;
            getAttribute(attributes, "fixedMod", value);
            sm->fixedMod = value == "true" ? true : false;

            if (version != SchemaVersion_1_0)
            {
                getAttribute(attributes, "massDelta", sm->massDelta);
                getAttribute(attributes, "residues", value);
                parseDelimitedListString(sm->residues, value);
            }

            HandlerParamContainer::paramContainer = sm;
            return Status::Ok;
        }
        else if (version == SchemaVersion_1_0 && name == "ModParam")
        {
            getAttribute(attributes, "massDelta", sm->massDelta);

            string residues;
            getAttribute(attributes, "residues", residues);
            parseDelimitedListString(sm->residues, residues);

            return Status::Ok;
        }
        else if (name == "SpecificityRules")
        {
            handlerSpecificityRules_.cvParam = &sm->specificityRules;
            return Status(Status::Delegate, &handlerSpecificityRules_);
        }

        return HandlerParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerNamedCVParam handlerSpecificityRules_;
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
            throw runtime_error("[IO::HandlerFilter] Unexpected element name: " + name);

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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const TranslationTable& tt)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(tt, attributes);
    writer.startElement("TranslationTable", attributes);

    if (!tt.ParamContainer::empty())
    {
        writeParamContainer(writer, tt);
    }

    writer.endElement();
}

struct HandlerTranslationTable : public HandlerIdentifiableParamContainer
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
            HandlerIdentifiableParamContainer::id = tt;
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
};


PWIZ_API_DECL void read(std::istream& is, TranslationTable& tt)
{
    HandlerTranslationTable handler(&tt);
    SAXParser::parse(is, handler);
}



//
// DatabaseTranslation
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DatabaseTranslation& dt)
{
    XMLWriter::Attributes attributes;
    if (!dt.frames.empty())
        attributes.add("frames", makeDelimitedListString(dt.frames));

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
            parseDelimitedListString(dt->frames, values);
        }
        else if (name == "TranslationTable")
        {
            dt->translationTable.push_back(TranslationTablePtr(new TranslationTable()));
            handlerTranslationTable_.version = version;
            handlerTranslationTable_.tt = dt->translationTable.back().get();
            return Status(Status::Delegate, &handlerTranslationTable_);
        }
        else
            throw runtime_error("[IO::HandlerDatabaseTranslation] Unknown tag"+name);

        return Status::Ok;
    }
    private:
    HandlerTranslationTable handlerTranslationTable_;
};


PWIZ_API_DECL void read(std::istream& is, DatabaseTranslation& dt)
{
    HandlerDatabaseTranslation handler(&dt);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationProtocol
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationProtocol& si)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(si, attributes);
    if (si.analysisSoftwarePtr.get() && !si.analysisSoftwarePtr->empty())
        attributes.add("analysisSoftware_ref", si.analysisSoftwarePtr->id);

    writer.startElement("SpectrumIdentificationProtocol", attributes);

    if (!si.searchType.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("SearchType");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        write(writer, si.searchType);
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
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
    
    BOOST_FOREACH(const MassTablePtr& mt, si.massTable)
        write(writer, *mt);

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

    writer.startElement("Threshold");
    if (si.threshold.empty())
    {
        CVParam noThreshold(MS_no_threshold);
        write(writer, noThreshold);
    } else 
        writeParamContainer(writer, si.threshold);
    writer.endElement();
    
    writePtrList(writer, si.databaseFilters, "DatabaseFilters");

    if (si.databaseTranslation.get() && !si.databaseTranslation->empty())
        write(writer, si.databaseTranslation);
    
    writer.endElement();
}

struct HandlerThreshold : public SAXParser::Handler
{
    HandlerThreshold(ParamContainer* paramContainer_ = 0)
        :   paramContainer(paramContainer_)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Threshold")
            return Status::Ok;

        if (!paramContainer)
            throw runtime_error("[IO::HandlerThreshold] Null paramContainer."); 

        if (name == "cvParam") {
            CVParam cvParam;
            
            string accession;
            getAttribute(attributes, "accession", accession);
            if (!accession.empty())
                cvParam.cvid = cvTermInfo(accession).cvid;

            if (cvParam.cvid == MS_no_threshold)
                return Status::Ok;
            
            getAttribute(attributes, "value",
                         cvParam.value);

            string unitAccession;
            getAttribute(attributes, "unitAccession", unitAccession);
            if (!unitAccession.empty())
                cvParam.units = cvTermInfo(unitAccession).cvid;

            paramContainer->cvParams.push_back(cvParam);
        }

        return Status::Ok;
    }

    ParamContainer* paramContainer;
};

struct HandlerSpectrumIdentificationProtocol : public HandlerIdentifiable
{
    SpectrumIdentificationProtocol* sip;
    HandlerSpectrumIdentificationProtocol(SpectrumIdentificationProtocol* _sip = 0)
        : sip(_sip), handlerSearchType_("SearchType"),
          handlerAdditionalSearchParams_("AdditionalSearchParams"),
          handlerFragmentTolerance_("FragmentTolerance"),
          handlerParentTolerance_("ParentTolerance")
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SpectrumIdentificationProtocol")
        {
            string value;
            getAttribute(attributes, analysisSoftware_ref(version), value);
            if (!value.empty())
                sip->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware(value));
            
            HandlerIdentifiable::id = sip;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "SearchType")
        {
            handlerSearchType_.cvParam = &sip->searchType;
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
            handlerModificationParams_.version = version;
            handlerModificationParams_.sm = sip->modificationParams.back().get();
            return Status(Status::Delegate, &handlerModificationParams_);
        }
        else if (name == "Enzymes")
        {
            handlerEnzymes_.version = version;
            handlerEnzymes_.ez = &sip->enzymes;
            return Status(Status::Delegate, &handlerEnzymes_);
        }
        else if (name == "MassTable")
        {
            sip->massTable.push_back(MassTablePtr(new MassTable()));
            handlerMassTable_.version = version;
            handlerMassTable_.mt = sip->massTable.back().get();
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
            handlerFilter_.version = version;
            handlerFilter_.filter = sip->databaseFilters.back().get();
            return Status(Status::Delegate, &handlerFilter_);
        }
        else if (name == "DatabaseTranslation")
        {
            sip->databaseTranslation = DatabaseTranslationPtr(new DatabaseTranslation());
            handlerDatabaseTranslation_.version = version;
            handlerDatabaseTranslation_.dt = sip->databaseTranslation.get();
            return Status(Status::Delegate, &handlerDatabaseTranslation_);
        }
        else
            throw runtime_error("[IO::HandlerSpectrumIdentificationProtocol] Unknown tag "+name);
        
        return Status::Ok;
    }
    private:
    HandlerNamedCVParam handlerSearchType_;
    HandlerNamedParamContainer handlerAdditionalSearchParams_;
    HandlerSearchModification handlerModificationParams_;
    HandlerEnzymes handlerEnzymes_;
    HandlerMassTable handlerMassTable_;
    HandlerNamedParamContainer handlerFragmentTolerance_;
    HandlerNamedParamContainer handlerParentTolerance_;
    //HandlerNamedParamContainer handlerThreshold_;
    HandlerThreshold handlerThreshold_;
    HandlerFilter handlerFilter_;
    HandlerDatabaseTranslation handlerDatabaseTranslation_;
};

PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationProtocol& sip)
{
    HandlerSpectrumIdentificationProtocol handler(&sip);
    SAXParser::parse(is, handler);
}


//
// ProteinDetectionProtocol
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const ProteinDetectionProtocol& pd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(pd, attributes);
    if (pd.analysisSoftwarePtr.get())
        attributes.add("analysisSoftware_ref", pd.analysisSoftwarePtr->id);
    
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

struct HandlerProteinDetectionProtocol : public HandlerIdentifiable
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

            getAttribute(attributes, analysisSoftware_ref(version), value);
            pdp->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware(value));
            HandlerIdentifiable::id = pdp;
            return HandlerIdentifiable::startElement(name, attributes, position);
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
            throw runtime_error("[IO::HandlerProteinDetectionProtocol] Unknown tag "+name);
        return Status::Ok;
    }
    private:
    HandlerNamedParamContainer handlerAnalysisParams_;
    HandlerNamedParamContainer handlerThreshold_;
};

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
            handlerSpectrumIdentificationProtocol_.version = version;
            handlerSpectrumIdentificationProtocol_.sip = anal->spectrumIdentificationProtocol.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationProtocol_);
        }
        else if (name == "ProteinDetectionProtocol")
        {
            anal->proteinDetectionProtocol.push_back(ProteinDetectionProtocolPtr(new ProteinDetectionProtocol()));
            handlerProteinDetectionProtocol_.version = version;
            handlerProteinDetectionProtocol_.pdp = anal->proteinDetectionProtocol.back().get();
            return Status(Status::Delegate, &handlerProteinDetectionProtocol_);
        }
        else
            throw runtime_error("[IO::HandlerAnalysisProtocolCollection] Unknown tag "+name);
        
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
            handlerSample_.version = version;
            handlerSample_.sample = asc->samples.back().get();
            return Status(Status::Delegate, &handlerSample_);
        }
        else
            throw runtime_error("[IO::HandlerAnalysisSampleCollection] Unknown tag "+name);
        
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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectraData& sd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sd, attributes);
    attributes.add("location", sd.location);

    writer.startElement("SpectraData", attributes);

    // write out ExternalFormatDocumentation
    for (vector<string>::const_iterator it=sd.externalFormatDocumentation.begin(); it != sd.externalFormatDocumentation.end(); it++)
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("ExternalFormatDocumentation");
        writer.characters(*it);
        writer.endElement();
        writer.popStyle();
    }

    if (!sd.fileFormat.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("FileFormat");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        write(writer, sd.fileFormat);
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
    }

    if (!sd.spectrumIDFormat.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("SpectrumIDFormat");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        write(writer, sd.spectrumIDFormat);
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
    }

    writer.endElement();
}

struct HandlerSpectraData : public HandlerIdentifiable
{
    bool inExternalFormatDocumentation;
    SpectraData* sd;
    HandlerSpectraData(SpectraData* _sd = 0)
        : inExternalFormatDocumentation(false), sd(_sd),
          handlerFileFormat_(""),
          handlerSpectrumIDFormat_("")
    {
        parseCharacters = true;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!sd)
            throw runtime_error("[IO::HandlerSpectraData] Null SpectraData.");

        if (name == "SpectraData")
        {
            getAttribute(attributes, "location", sd->location);
            
            HandlerIdentifiable::id = sd;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == FileFormat_element(version))
        {
            handlerFileFormat_.cvParam = &sd->fileFormat;
            handlerFileFormat_.name_ = FileFormat_element(version);
            return Status(Status::Delegate, &handlerFileFormat_);
        }
        else if (name == ExternalFormatDocumentation_element(version))
        {
            inExternalFormatDocumentation = true;
        }
        else if (name == SpectrumIDFormat_element(version))
        {
            handlerSpectrumIDFormat_.cvParam = &sd->spectrumIDFormat;
            handlerSpectrumIDFormat_.name_ = SpectrumIDFormat_element(version);
            return Status(Status::Delegate, &handlerSpectrumIDFormat_);
        }
        else
            throw runtime_error("[IO::HandlerSpectraData] Unknown tag"+name);
        
        return Status::Ok;
    }
    
    virtual Status characters(const SAXParser::saxstring& text, 
                              stream_offset position)
    {
        if (inExternalFormatDocumentation)
        {
            sd->externalFormatDocumentation.push_back(text.c_str());
        }
        else
            throw runtime_error("[IO::HandlerSpectraData] Unexpected characters");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == ExternalFormatDocumentation_element(version))
            inExternalFormatDocumentation = false;

        return Status::Ok;
    }

    private:
    HandlerNamedCVParam handlerFileFormat_;
    HandlerNamedCVParam handlerSpectrumIDFormat_;
};

PWIZ_API_DECL void read(std::istream& is, SpectraData& sd)
{
    HandlerSpectraData handler(&sd);
    SAXParser::parse(is, handler);
}


//
// SearchDatabase
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SearchDatabase& sd)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sd, attributes);
    if (!sd.location.empty())
        attributes.add("location", sd.location);
    if (!sd.version.empty())
        attributes.add("version", sd.version);
    if (!sd.releaseDate.empty())
        attributes.add("releaseDate", sd.releaseDate);
    if (sd.numDatabaseSequences>0)
        attributes.add("numDatabaseSequences", sd.numDatabaseSequences);
    if (sd.numResidues>0)
        attributes.add("numResidues", sd.numResidues);

    writer.startElement("SearchDatabase", attributes);

    if (!sd.fileFormat.empty())
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("FileFormat");
        writer.pushStyle(XMLWriter::StyleFlag_Inline);
        write(writer, sd.fileFormat);
        writer.popStyle();
        writer.endElement();
        writer.popStyle();
    }

    writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
    writer.startElement("DatabaseName");
    writer.pushStyle(XMLWriter::StyleFlag_Inline);
    writeParamContainer(writer, sd.databaseName);
    writer.popStyle();
    writer.endElement();
    writer.popStyle();

    writeParamContainer(writer, sd);
    
    writer.endElement();
}

struct HandlerSearchDatabase : public HandlerIdentifiableParamContainer
{
    SearchDatabase* sd;
    HandlerSearchDatabase(SearchDatabase* _sd = 0)
        : sd(_sd),
          handlerFileFormat_(""),
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

            HandlerIdentifiableParamContainer::id = sd;
        }
        else if (name == FileFormat_element(version))
        {
            handlerFileFormat_.cvParam = &sd->fileFormat;
            handlerFileFormat_.name_ = FileFormat_element(version);
            return Status(Status::Delegate, &handlerFileFormat_);
        }
        else if (name == "DatabaseName")
        {
            handlerDatabaseName_.paramContainer = &sd->databaseName;
            return Status(Status::Delegate, &handlerDatabaseName_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerNamedCVParam handlerFileFormat_;
    HandlerNamedParamContainer handlerDatabaseName_;
};

PWIZ_API_DECL void read(std::istream& is, SearchDatabase& sd)
{
    HandlerSearchDatabase handler(&sd);
    SAXParser::parse(is, handler);
}

//
// SourceFile
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SourceFile& sf)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sf, attributes);
    if (!sf.location.empty())
        attributes.add("location", sf.location);

    writer.startElement("SourceFile", attributes);

    if (!sf.fileFormat.empty())
    {
        writer.startElement("FileFormat");
        write(writer, sf.fileFormat);
        writer.endElement();
    }

    // write out ExternalFormatDocumentation.
    for (vector<string>::const_iterator it=sf.externalFormatDocumentation.begin(); it != sf.externalFormatDocumentation.end(); it++)
    {
        writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
        writer.startElement("ExternalFormatDocumentation");
        writer.characters(*it);
        writer.endElement();
        writer.popStyle();
    }
    
    writeParamContainer(writer, sf);
    
    writer.endElement();
}

struct HandlerSourceFile : public HandlerIdentifiableParamContainer
{
    bool inExternalFormatDocumentation;
    SourceFile* sf;
    HandlerSourceFile(SourceFile* _sf = 0)
        : inExternalFormatDocumentation(false), sf(_sf),
          handlerFileFormat_("")
    {
        parseCharacters = true;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "SourceFile")
        {
            getAttribute(attributes, "location", sf->location);
            HandlerIdentifiableParamContainer::id = sf;
        }
        else if (name == ExternalFormatDocumentation_element(version))
        {
            inExternalFormatDocumentation = true;
        }
        else if (name == FileFormat_element(version))
        {
            handlerFileFormat_.cvParam = &sf->fileFormat;
            handlerFileFormat_.name_ = FileFormat_element(version);
            return Status(Status::Delegate, &handlerFileFormat_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }

    virtual Status characters(const SAXParser::saxstring& text, 
                              stream_offset position)
    {
        if (inExternalFormatDocumentation)
        {
            sf->externalFormatDocumentation.push_back(text.c_str());
        }
        else
            throw runtime_error("[IO::HandlerSourceFile] Unexpected characters.");

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == ExternalFormatDocumentation_element(version))
        {
            inExternalFormatDocumentation = false;
        }

        return Status::Ok;
    }
    
    private:
    HandlerNamedCVParam handlerFileFormat_;
};

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
            handlerSourceFile_.version = version;
            handlerSourceFile_.sf = inputs->sourceFile.back().get();
            return Status(Status::Delegate, &handlerSourceFile_);
        }
        else if (name == "SearchDatabase")
        {
            inputs->searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase()));
            handlerSearchDatabase_.version = version;
            handlerSearchDatabase_.sd = inputs->searchDatabase.back().get();
            return Status(Status::Delegate, &handlerSearchDatabase_);
        }
        else if (name == "SpectraData")
        {
            inputs->spectraData.push_back(SpectraDataPtr(new SpectraData()));
            handlerSpectraData_.version = version;
            handlerSpectraData_.sd = inputs->spectraData.back().get();
            return Status(Status::Delegate, &handlerSpectraData_);
        }
        else
            throw runtime_error("[IO::HandlerInputs] Unknown tag "+name);
        
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
// PeptideHypothesis
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const PeptideHypothesis& ph)
{
    XMLWriter::Attributes attributes;
    if (ph.peptideEvidencePtr.get())
        attributes.add("peptideEvidence_ref", ph.peptideEvidencePtr->id);

    writer.startElement("PeptideHypothesis", attributes);
    BOOST_FOREACH(const SpectrumIdentificationItemPtr& sii, ph.spectrumIdentificationItemPtr)
    {
        attributes.clear();
        attributes.add("spectrumIdentificationItem_ref", sii->id);
        writer.startElement("SpectrumIdentificationItemRef", attributes, XMLWriter::EmptyElement);
    }
    writer.endElement();
}

struct HandlerPeptideHypothesis : public SAXParser::Handler
{
    PeptideHypothesis* ph;
    const SequenceIndex& sequenceIndex;

    HandlerPeptideHypothesis(const SequenceIndex& sequenceIndex, PeptideHypothesis* _ph = 0)
        : ph(_ph), sequenceIndex(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!ph)
            throw runtime_error("[IO::HandlerPeptideHypothesis] Null PeptideHypothesis value.");

        if (name == "PeptideHypothesis")
        {
            string value;
            getAttribute(attributes, peptideEvidence_ref(version), value);

            map<string, PeptideEvidencePtr>::const_iterator findItr = sequenceIndex.peptideEvidence.find(value);
            if (findItr == sequenceIndex.peptideEvidence.end())
                ph->peptideEvidencePtr = PeptideEvidencePtr(new PeptideEvidence(value));
            else
                ph->peptideEvidencePtr = findItr->second;
        }
        else if (name == "SpectrumIdentificationItemRef")
        {
            string value;
            getAttribute(attributes, "spectrumIdentificationItem_ref", value);

            map<string, SpectrumIdentificationItemPtr>::const_iterator findItr = sequenceIndex.spectrumIdentificationItems.find(value);
            if (findItr == sequenceIndex.spectrumIdentificationItems.end())
                ph->spectrumIdentificationItemPtr.push_back(SpectrumIdentificationItemPtr(new SpectrumIdentificationItem(value)));
            else
                ph->spectrumIdentificationItemPtr.push_back(findItr->second);
        }
        else
            throw runtime_error("[IO::HandlerPeptideHypothesis] Unexpected element name: " + name);

        return Status::Ok;
    }
};

PWIZ_API_DECL void read(std::istream& is, PeptideHypothesis& ph)
{
    SequenceIndex dummy;
    HandlerPeptideHypothesis handler(dummy, &ph);
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
        attributes.add("dBSequence_ref", pdh.dbSequencePtr->id);
    attributes.add("passThreshold", pdh.passThreshold ? "true" : "false");

    writer.startElement("ProteinDetectionHypothesis", attributes);

    writeList(writer, pdh.peptideHypothesis);
    writeParamContainer(writer, pdh);
    
    writer.endElement();
}

struct HandlerProteinDetectionHypothesis : public HandlerIdentifiableParamContainer
{
    ProteinDetectionHypothesis* pdh;
    const SequenceIndex& sequenceIndex;

    HandlerProteinDetectionHypothesis(const SequenceIndex& sequenceIndex, ProteinDetectionHypothesis* _pdh = 0)
        : pdh(_pdh), sequenceIndex(sequenceIndex), handlerPeptideHypothesis_(sequenceIndex)
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
            getAttribute(attributes, dBSequence_ref(version), value);
            if (!value.empty())
            {
                map<string, DBSequencePtr>::const_iterator findItr = sequenceIndex.dbSequences.find(value);
                if (findItr == sequenceIndex.dbSequences.end())
                    pdh->dbSequencePtr = DBSequencePtr(new DBSequence(value));
                else
                    pdh->dbSequencePtr = findItr->second;
            }

            value.clear();
            getAttribute(attributes, "passThreshold", value);
            pdh->passThreshold = (value=="true" ? true : false);

            HandlerIdentifiableParamContainer::id = pdh;
        }
        else if (name == "PeptideHypothesis")
        {
            pdh->peptideHypothesis.push_back(PeptideHypothesis());
            handlerPeptideHypothesis_.version = version;
            handlerPeptideHypothesis_.ph = &pdh->peptideHypothesis.back();
            return Status(Status::Delegate, &handlerPeptideHypothesis_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerPeptideHypothesis handlerPeptideHypothesis_;
};

PWIZ_API_DECL void read(std::istream& is, ProteinDetectionHypothesis& pdh)
{
    SequenceIndex dummy;
    HandlerProteinDetectionHypothesis handler(dummy, &pdh);
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

    writeParamContainer(writer, pag);
    
    writer.endElement();
}

struct HandlerProteinAmbiguityGroup : public HandlerIdentifiableParamContainer
{
    ProteinAmbiguityGroup* pag;
    HandlerProteinAmbiguityGroup(const SequenceIndex& sequenceIndex, ProteinAmbiguityGroup* _pag = 0)
        : pag(_pag), handlerProteinDetectionHypothesis_(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!pag)
            throw runtime_error("[IO::HandlerProteinAmbiguityGroup] Null ProteinAmbiguityGroup.");

        if (name == "ProteinAmbiguityGroup")
        {
            HandlerIdentifiableParamContainer::id = pag;
        }
        else if (name == "ProteinDetectionHypothesis")
        {
            ProteinDetectionHypothesisPtr pdh(new ProteinDetectionHypothesis());
            pag->proteinDetectionHypothesis.push_back(pdh);
            handlerProteinDetectionHypothesis_.pdh = pag->proteinDetectionHypothesis.back().get();
            return Status(Status::Delegate, &handlerProteinDetectionHypothesis_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerProteinDetectionHypothesis handlerProteinDetectionHypothesis_;
};

PWIZ_API_DECL void read(std::istream& is, ProteinAmbiguityGroup& pag)
{
    SequenceIndex dummy;
    HandlerProteinAmbiguityGroup handler(dummy, &pag);
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
    
    for (vector<ProteinAmbiguityGroupPtr>::const_iterator it=pdl.proteinAmbiguityGroup.begin();
         it!= pdl.proteinAmbiguityGroup.end(); it++)
        write(writer, **it);
    writeParamContainer(writer, pdl);
    writer.endElement();
}


struct HandlerProteinDetectionList : public HandlerIdentifiableParamContainer
{
    ProteinDetectionList* pdl;
    HandlerProteinDetectionList(const SequenceIndex& sequenceIndex, ProteinDetectionList* _pdl = 0)
        : pdl(_pdl), handlerProteinAmbiguityGroup_(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "ProteinDetectionList")
        {
            HandlerIdentifiableParamContainer::id = pdl;
        }
        else if (name == "ProteinAmbiguityGroup")
        {
            ProteinAmbiguityGroupPtr pag(new ProteinAmbiguityGroup());
            pdl->proteinAmbiguityGroup.push_back(pag);
            handlerProteinAmbiguityGroup_.version = version;
            handlerProteinAmbiguityGroup_.pag = pdl->proteinAmbiguityGroup.back().get();
            return Status(Status::Delegate, &handlerProteinAmbiguityGroup_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerProteinAmbiguityGroup handlerProteinAmbiguityGroup_;
};


PWIZ_API_DECL void read(std::istream& is, ProteinDetectionList& pdl)
{
    SequenceIndex dummy;
    HandlerProteinDetectionList handler(dummy, &pdl);
    SAXParser::parse(is, handler);
}




//
// FragmentArray
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const FragmentArray& fa)
{
    XMLWriter::Attributes attributes;
    attributes.add("values", makeDelimitedListString(fa.values));
    if (fa.measurePtr.get() && !fa.measurePtr->empty())
        attributes.add("measure_ref", fa.measurePtr->id);
    
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
            parseDelimitedListString(fa->values, values);

            values.clear();
            getAttribute(attributes, measure_ref(version), values);
            if (!values.empty())
                fa->measurePtr = MeasurePtr(new Measure(values));
        }
        else
            throw runtime_error("[IO::HandlerFragmentArray] Unexpected element name: " + name);
        
        return Status::Ok;
    }
};


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
    attributes.add("index", makeDelimitedListString(itype.index));
    attributes.add("charge", itype.charge);

    writer.startElement("IonType", attributes);
    
    for(vector<FragmentArrayPtr>::const_iterator it=itype.fragmentArray.begin();
        it!=itype.fragmentArray.end(); it++)
        write(writer, *it);
    
    write(writer, (const CVParam&)itype);
    writer.endElement();
}


struct HandlerIonType : public HandlerNamedCVParam
{
    IonType* it;
    HandlerIonType(IonType* _it = 0) : HandlerNamedCVParam("IonType", _it), it(_it) {}

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
            parseDelimitedListString(it->index, values);
            getAttribute(attributes, "charge", it->charge);
            HandlerNamedCVParam::cvParam = it;
            return Status::Ok;
        }
        else if (name == "FragmentArray")
        {
            FragmentArrayPtr fa(new FragmentArray());
            it->fragmentArray.push_back(fa);
            handlerFragmentArray_.version = version;
            handlerFragmentArray_.fa = it->fragmentArray.back().get();
            return handlerFragmentArray_.startElement(name, attributes, position);
        }

        return HandlerNamedCVParam::startElement(name, attributes, position);
    }
    private:
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


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationItem& siip)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(siip, attributes);
    attributes.add("rank", siip.rank);
    attributes.add("chargeState", siip.chargeState);
    if (siip.peptidePtr.get() && !siip.peptidePtr->empty())
        attributes.add("peptide_ref", siip.peptidePtr->id);
    attributes.add("experimentalMassToCharge", siip.experimentalMassToCharge);
    attributes.add("calculatedMassToCharge", siip.calculatedMassToCharge);
    if (siip.calculatedPI > 0)
        attributes.add("calculatedPI", siip.calculatedPI);
    attributes.add("passThreshold", (siip.passThreshold ? "true" : "false"));
    if (siip.massTablePtr.get() && !siip.massTablePtr->empty())
        attributes.add("massTable_ref", siip.massTablePtr->id);
    if (siip.samplePtr.get() && !siip.samplePtr->empty())
        attributes.add("sample_ref", siip.samplePtr->id);


    writer.startElement("SpectrumIdentificationItem", attributes);

    BOOST_FOREACH(const PeptideEvidencePtr& pe, siip.peptideEvidencePtr)
    {
        attributes.clear();
        attributes.add("peptideEvidence_ref", pe->id);
        writer.startElement("PeptideEvidenceRef", attributes, XMLWriter::EmptyElement);
    }

    writePtrList(writer, siip.fragmentation, "Fragmentation");

    writeParamContainer(writer, siip);
    
    writer.endElement();
}


struct HandlerSpectrumIdentificationItem : public HandlerIdentifiableParamContainer
{
    SpectrumIdentificationItem* siip;
    SequenceIndex& sequenceIndex;

    HandlerSpectrumIdentificationItem(SequenceIndex& sequenceIndex, SpectrumIdentificationItem* _siip = 0)
    : siip(_siip),
      sequenceIndex(sequenceIndex),
      handlerPeptideEvidence_(sequenceIndex)
    {}

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
            getAttribute(attributes, peptide_ref(version), value);
            if (!value.empty())
            {
                map<string, PeptidePtr>::const_iterator findItr = sequenceIndex.peptides.find(value);
                if (findItr == sequenceIndex.peptides.end())
                    siip->peptidePtr = PeptidePtr(new Peptide(value));
                else
                    siip->peptidePtr = findItr->second;
            }

            getAttribute(attributes, "rank", siip->rank);

            value.clear();
            getAttribute(attributes, "passThreshold", value);
            siip->passThreshold = (value=="true" ? true : false);

            value.clear();
            getAttribute(attributes, massTable_ref(version), value);
            if (!value.empty())
                siip->massTablePtr = MassTablePtr(new MassTable(value));

            value.clear();
            getAttribute(attributes, sample_ref(version), value);
            if (!value.empty())
                siip->samplePtr = SamplePtr(new Sample(value));

            HandlerIdentifiableParamContainer::id = siip;
        }
        else if (version != SchemaVersion_1_0 && name == "PeptideEvidenceRef")
        {
            string value;
            getAttribute(attributes, peptideEvidence_ref(version), value);
            map<string, PeptideEvidencePtr>::const_iterator findItr = sequenceIndex.peptideEvidence.find(value);
            if (findItr == sequenceIndex.peptideEvidence.end())
                siip->peptideEvidencePtr.push_back(PeptideEvidencePtr(new PeptideEvidence(value)));
            else
                siip->peptideEvidencePtr.push_back(findItr->second);

            return Status::Ok;
        }
        else if (version == SchemaVersion_1_0 && name == "PeptideEvidence")
        {
            siip->peptideEvidencePtr.push_back(PeptideEvidencePtr(new PeptideEvidence()));
            handlerPeptideEvidence_.version = version;
            handlerPeptideEvidence_.pep = siip->peptideEvidencePtr.back().get();
            return Status(Status::Delegate, &handlerPeptideEvidence_);
        }
        else if (name =="Fragmentation")
        {
            // Ignore
            return Status::Ok;
        }
        else if (name == "IonType")
        {
            siip->fragmentation.push_back(IonTypePtr(new IonType()));
            handlerIonType_.version = version;
            handlerIonType_.it = siip->fragmentation.back().get();
            return Status(Status::Delegate, &handlerIonType_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerIonType handlerIonType_;
    HandlerPeptideEvidence handlerPeptideEvidence_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationItem& siip)
{
    SequenceIndex dummy;
    HandlerSpectrumIdentificationItem handler(dummy, &siip);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationResult
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationResult& sirp)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(sirp, attributes);
    attributes.add("spectrumID", sirp.spectrumID);
    if (sirp.spectraDataPtr.get() && !sirp.spectraDataPtr->empty())
        attributes.add("spectraData_ref", sirp.spectraDataPtr->id);

    if (sirp.ParamContainer::empty() && sirp.spectrumIdentificationItem.empty())
        writer.startElement("SpectrumIdentificationResult", attributes, XMLWriter::EmptyElement);
    else
    {
        writer.startElement("SpectrumIdentificationResult", attributes);

        for (vector<SpectrumIdentificationItemPtr>::const_iterator it=sirp.spectrumIdentificationItem.begin(); it!=sirp.spectrumIdentificationItem.end(); it++)
            write(writer, *it);
        
        writeParamContainer(writer, sirp);
        writer.endElement();
    }
}


struct HandlerSpectrumIdentificationResult : public HandlerIdentifiableParamContainer
{
    SpectrumIdentificationResult* sirp;
    SequenceIndex& sequenceIndex;

    HandlerSpectrumIdentificationResult(SequenceIndex& sequenceIndex,
                                        SpectrumIdentificationResult* _sirp = 0)
    : sirp(_sirp), sequenceIndex(sequenceIndex), handlerSpectrumIdentificationItem_(sequenceIndex)
    {}

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
            getAttribute(attributes, spectraData_ref(version), value);
            if (!value.empty())
                sirp->spectraDataPtr = SpectraDataPtr(new SpectraData(value));

            HandlerIdentifiableParamContainer::id = sirp;
        }
        else if (name == "SpectrumIdentificationItem")
        {
            SpectrumIdentificationItemPtr siip(new SpectrumIdentificationItem);
            getAttribute(attributes, "id", siip->id);
            sirp->spectrumIdentificationItem.push_back(siip);
            sequenceIndex.spectrumIdentificationItems[siip->id] = siip;
            handlerSpectrumIdentificationItem_.version = version;
            handlerSpectrumIdentificationItem_.siip = siip.get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationItem_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
    HandlerSpectrumIdentificationItem handlerSpectrumIdentificationItem_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationResult& sirp)
{
    SequenceIndex dummy;
    HandlerSpectrumIdentificationResult handler(dummy, &sirp);
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

    writeParamContainer(writer, measure);
    
    writer.endElement();
}


struct HandlerMeasure : public HandlerIdentifiableParamContainer
{
    Measure* measure;
    HandlerMeasure(Measure* _measure = 0) : measure(_measure) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Measure")
        {
            HandlerIdentifiableParamContainer::id = measure;
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }
    private:
};


PWIZ_API_DECL void read(std::istream& is, Measure& measure)
{
    HandlerMeasure handler(&measure);
    SAXParser::parse(is, handler);
}


//
// SpectrumIdentificationList
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const SpectrumIdentificationList& silp,
                         const IterationListenerRegistry* ilr)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(silp, attributes);
    attributes.add("numSequencesSearched", silp.numSequencesSearched);
    
    writer.startElement("SpectrumIdentificationList", attributes);

    writePtrList(writer, silp.fragmentationTable, "FragmentationTable");

    int iterationIndex = 0;
    int iterationCount = silp.spectrumIdentificationResult.size();
    BOOST_FOREACH(const SpectrumIdentificationResultPtr& result, silp.spectrumIdentificationResult)
    {
        if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(iterationIndex++, iterationCount, "writing spectrum identification results")) == IterationListener::Status_Cancel)
            return;

        write(writer, result);
    }
    
    writer.endElement();
}


struct HandlerSpectrumIdentificationList : public HandlerIdentifiableParamContainer
{
    SpectrumIdentificationList* silp;
    HandlerSpectrumIdentificationList(SequenceIndex& sequenceIndex,
                                      SpectrumIdentificationList* _silp = 0,
                                      const IterationListenerRegistry* iterationListenerRegistry = 0)
    : silp(_silp), ilr_(iterationListenerRegistry), handlerSpectrumIdentificationResult_(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!silp)
            throw runtime_error("[IO::HandlerSpectrumIdentificationList] Null SpectrumIdentificationList.");
        
        if (name == "SpectrumIdentificationList")
        {
            getAttribute(attributes, "numSequencesSearched", silp->numSequencesSearched);
            
            HandlerIdentifiableParamContainer::id = silp;
        }
        else if (name == "FragmentationTable")
        {
            // Ignore
            return Status::Ok;
        }
        else if (name == "Measure")
        {
            MeasurePtr measure(new Measure());

            silp->fragmentationTable.push_back(measure);
            handlerMeasure_.version = version;
            handlerMeasure_.measure = silp->fragmentationTable.back().get();

            return Status(Status::Delegate, &handlerMeasure_);
        }
        else if (name == "SpectrumIdentificationResult")
        {
            if (ilr_ && ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(silp->spectrumIdentificationResult.size(), 0, "reading spectrum identification results")) == IterationListener::Status_Cancel)
                return Status::Done;

            SpectrumIdentificationResultPtr sirp(new SpectrumIdentificationResult());
            silp->spectrumIdentificationResult.push_back(sirp);
            handlerSpectrumIdentificationResult_.version = version;
            handlerSpectrumIdentificationResult_.sirp = silp->spectrumIdentificationResult.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationResult_);
        }

        return HandlerIdentifiableParamContainer::startElement(name, attributes, position);
    }

    virtual Status endElement(const string& name,
                              stream_offset position)
    {
        // handle final iteration update once final count is known
        if (name == "SpectrumIdentificationList")
        {
            if (ilr_ && ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(silp->spectrumIdentificationResult.size()-1, silp->spectrumIdentificationResult.size(), "reading spectrum identification results")) == IterationListener::Status_Cancel)
                return Status::Done;
        }
        return Status::Ok;
    }

    private:
    const IterationListenerRegistry* ilr_;
    HandlerMeasure handlerMeasure_;
    HandlerSpectrumIdentificationResult handlerSpectrumIdentificationResult_;
};


PWIZ_API_DECL void read(std::istream& is, SpectrumIdentificationList& silp,
                        const IterationListenerRegistry* iterationListenerRegistry)
{
    SequenceIndex dummy;
    HandlerSpectrumIdentificationList handler(dummy, &silp, iterationListenerRegistry);
    SAXParser::parse(is, handler);
}


//
// AnalysisData
//


PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const AnalysisData& ad,
                         const IterationListenerRegistry* iterationListenerRegistry)
{
    writer.startElement("AnalysisData");

    BOOST_FOREACH(const SpectrumIdentificationListPtr& sil, ad.spectrumIdentificationList)
        write(writer, *sil, iterationListenerRegistry);

    if (ad.proteinDetectionListPtr.get() &&
        !ad.proteinDetectionListPtr->empty())
        write(writer, *ad.proteinDetectionListPtr);
    
    writer.endElement();
}


struct HandlerAnalysisData : public SAXParser::Handler
{
    AnalysisData* ad;
    AnalysisDataFlag analysisDataFlag;

    HandlerAnalysisData(SequenceIndex& sequenceIndex,
                        AnalysisData* _ad = 0,
                        const IterationListenerRegistry* iterationListenerRegistry = 0,
                        AnalysisDataFlag analysisDataFlag = ReadAnalysisData)
    : ad(_ad),
      analysisDataFlag(analysisDataFlag),
      handlerSpectrumIdentificationList_(sequenceIndex, 0, iterationListenerRegistry),
      handlerProteinDetectionList_(sequenceIndex)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!ad)
            throw runtime_error("[HandlerAnalysisData::startElement] NULL value for AnalysisData");

        if (analysisDataFlag == IgnoreAnalysisData)
            return Status::Done; // there's nothing after AnalysisData

        if (name == "AnalysisData")
        {
            // ignore
        }
        else if (name == "SpectrumIdentificationList")
        {
            SpectrumIdentificationListPtr silp(new SpectrumIdentificationList());
            ad->spectrumIdentificationList.push_back(silp);
            handlerSpectrumIdentificationList_.version = version;
            handlerSpectrumIdentificationList_.silp = ad->spectrumIdentificationList.back().get();
            return Status(Status::Delegate, &handlerSpectrumIdentificationList_);
        }
        else if (name == "ProteinDetectionList")
        {
            if (analysisDataFlag != IgnoreProteinDetectionList)
            {
                ad->proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList());
                handlerProteinDetectionList_.version = version;
                handlerProteinDetectionList_.pdl = ad->proteinDetectionListPtr.get();
                return Status(Status::Delegate, &handlerProteinDetectionList_);
            }
        }
        else
            throw runtime_error("[IO::HandlerAnalysisData] Unexpected element name: " + name);
        return Status::Ok;
    }
    private:
    HandlerSpectrumIdentificationList handlerSpectrumIdentificationList_;
    HandlerProteinDetectionList handlerProteinDetectionList_;
};


PWIZ_API_DECL void read(std::istream& is, AnalysisData& ad,
                        const IterationListenerRegistry* iterationListenerRegistry)
{
    SequenceIndex dummy;
    HandlerAnalysisData handler(dummy, &ad, iterationListenerRegistry);
    SAXParser::parse(is, handler);
}


//
// DataCollection
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const DataCollection& dc,
                         const IterationListenerRegistry* iterationListenerRegistry)
{
    XMLWriter::Attributes attributes;

    writer.startElement("DataCollection", attributes);

    write(writer, dc.inputs);
    write(writer, dc.analysisData, iterationListenerRegistry);
    
    writer.endElement();
}

struct HandlerDataCollection : public SAXParser::Handler
{
    DataCollection* dc;

    HandlerDataCollection(SequenceIndex& sequenceIndex,
                          DataCollection* _dc = 0,
                          const IterationListenerRegistry* iterationListenerRegistry = 0,
                          AnalysisDataFlag analysisDataFlag = ReadAnalysisData)
    : dc(_dc), handlerAnalysisData_(sequenceIndex, 0, iterationListenerRegistry, analysisDataFlag)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "DataCollection")
        {
        }
        else if (name == "Inputs")
        {
            handlerInputs_.version = version;
            handlerInputs_.inputs = &dc->inputs;
            return Status(Status::Delegate, &handlerInputs_);
        }
        else if (name == "AnalysisData")
        {
            handlerAnalysisData_.version = version;
            handlerAnalysisData_.ad = &dc->analysisData;
            return Status(Status::Delegate, &handlerAnalysisData_);
        }
        else
            throw runtime_error("[IO::HandlerDataCollection] Unknown tag " + name);
        
        return Status::Ok;
    }
    private:
    HandlerInputs handlerInputs_;
    HandlerAnalysisData handlerAnalysisData_;
};

PWIZ_API_DECL void read(std::istream& is, DataCollection& dc,
                        const IterationListenerRegistry* iterationListenerRegistry,
                        AnalysisDataFlag analysisDataFlag)
{
    SequenceIndex sequenceIndex;
    HandlerDataCollection handler(sequenceIndex, &dc, iterationListenerRegistry, analysisDataFlag);
    SAXParser::parse(is, handler);
}


//
// Provider
//

PWIZ_API_DECL void write(minimxml::XMLWriter& writer, const Provider& provider)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(provider, attributes);

    if (provider.analysisSoftwarePtr.get() && !provider.analysisSoftwarePtr->empty())
        attributes.add("analysisSoftware_ref", provider.analysisSoftwarePtr->id);

    writer.startElement("Provider", attributes);
    if (provider.contactRolePtr.get() && !provider.contactRolePtr->empty())
        write(writer, provider.contactRolePtr);
    writer.endElement();
}

struct HandlerProvider : public HandlerIdentifiable
{
    Provider* p;
    HandlerProvider(Provider* _p = 0) : p(_p) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "Provider")
        {
            if (version == SchemaVersion_1_1)
            {
                p->analysisSoftwarePtr.reset(new AnalysisSoftware);
                getAttribute(attributes, "analysisSoftware_ref", p->analysisSoftwarePtr->id);
            }

            HandlerIdentifiable::id = p;
            return HandlerIdentifiable::startElement(name, attributes, position);
        }
        else if (name == "ContactRole")
        {
            p->contactRolePtr.reset(new ContactRole);
            handlerContactRole_.version = version;
            handlerContactRole_.cvParam = handlerContactRole_.cr = p->contactRolePtr.get();
            return Status(Status::Delegate, &handlerContactRole_);
        }
        else
            throw runtime_error("[IO::HandlerProvider] Unknown tag "+name);

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
// IdentData
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
void write(minimxml::XMLWriter& writer, const IdentData& mzid,
           const IterationListenerRegistry* iterationListenerRegistry)
{
    XMLWriter::Attributes attributes;
    addIdAttributes(mzid, attributes);

    attributes.add("creationDate", mzid.creationDate);
    attributes.add("version", mzid.version());
    attributes.add("xsi:schemaLocation", "http://psidev.info/psi/pi/mzIdentML/1.1 http://psidev.info/files/mzIdentML" + mzid.version() + ".xsd");
    attributes.add("xmlns", "http://psidev.info/psi/pi/mzIdentML/1.1");
    attributes.add("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");

    writer.startElement("MzIdentML", attributes);

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
        write(writer, mzid.sequenceCollection, iterationListenerRegistry);
    if (!mzid.analysisCollection.empty())
        write(writer, mzid.analysisCollection);
    if (!mzid.analysisProtocolCollection.empty())
        write(writer, mzid.analysisProtocolCollection);
    if (!mzid.dataCollection.empty())
        write(writer, mzid.dataCollection, iterationListenerRegistry);
    if (!mzid.bibliographicReference.empty())
        writePtrList(writer, mzid.bibliographicReference);

    writer.endElement();
}

struct HandlerIdentData : public HandlerIdentifiable
{
    IdentData* mzid;
    SequenceIndex sequenceIndex;

    HandlerIdentData(IdentData* _mzid = 0,
                     const IterationListenerRegistry* iterationListenerRegistry = 0,
                     SequenceCollectionFlag sequenceCollectionFlag = ReadSequenceCollection,
                     AnalysisDataFlag analysisDataFlag = ReadAnalysisData)
    : mzid(_mzid),
      handlerSequenceCollection_(sequenceIndex, 0, iterationListenerRegistry, sequenceCollectionFlag),
      handlerDataCollection_(sequenceIndex, 0, iterationListenerRegistry, analysisDataFlag)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!mzid)
            throw runtime_error("[IO::HandlerIdentData] Null mzIdentML.");

        
        // mzIdentML 1.0 root is "mzIdentML", 1.1 root is "MzIdentML"
        if (bal::iequals(name, "MzIdentML"))
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
                string xsdName = BFS_STRING(bfs::path(schemaLocation).filename());
                mzid->version_ = xsdName.substr(9, xsdName.length()-13); // read between "mzIdentML" and ".xsd"
            }

            if (mzid->version_.find("1.0.0") == 0)
                version = (int) SchemaVersion_1_0;

            HandlerIdentifiable::id = mzid;
            return HandlerIdentifiable::startElement(name, attributes, position);
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
            handlerCV_.version = version;
            handlerCV_.cv = &mzid->cvs.back();
            return Status(Status::Delegate, &handlerCV_);
                    
        }
        else if (name == "AnalysisSoftware")
        {
            mzid->analysisSoftwareList.push_back(AnalysisSoftwarePtr(new AnalysisSoftware()));
            handlerAnalysisSoftware_.version = version;
            handlerAnalysisSoftware_.anal = mzid->analysisSoftwareList.back().get();
            return Status(Status::Delegate, &handlerAnalysisSoftware_);
        }
        else if (name == "Provider")
        {
            handlerProvider_.version = version;
            handlerProvider_.p = &mzid->provider;
            return Status(Status::Delegate, &handlerProvider_);
        }
        else if (name == "AuditCollection")
        {
            handlerContact_.version = version;
            handlerContact_.c = &mzid->auditCollection;
            return Status(Status::Delegate, &handlerContact_);
        }
        else if (name == "AnalysisSampleCollection")
        {
            handlerAnalysisSampleCollection_.version = version;
            handlerAnalysisSampleCollection_.asc = &mzid->analysisSampleCollection;
            return Status(Status::Delegate, &handlerAnalysisSampleCollection_);
        }
        else if (name == "SequenceCollection")
        {
            handlerSequenceCollection_.version = version;
            handlerSequenceCollection_.sc = &mzid->sequenceCollection;
            return Status(Status::Delegate, &handlerSequenceCollection_);
        }
        else if (name == "AnalysisCollection")
        {
            handlerAnalysisCollection_.version = version;
            handlerAnalysisCollection_.anal = &mzid->analysisCollection;
            return Status(Status::Delegate, &handlerAnalysisCollection_);
        }
        else if (name == "AnalysisProtocolCollection")
        {
            handlerAnalysisProtocolCollection_.version = version;
            handlerAnalysisProtocolCollection_.anal = &mzid->analysisProtocolCollection;
            return Status(Status::Delegate, &handlerAnalysisProtocolCollection_);
        }
        else if (name == "DataCollection")
        {
            handlerDataCollection_.version = version;
            handlerDataCollection_.dc = &mzid->dataCollection;
            return Status(Status::Delegate, &handlerDataCollection_);
            
        }
        else if (name == "BibliographicReference")
        {
            mzid->bibliographicReference.push_back(
                BibliographicReferencePtr(new BibliographicReference));
            handlerBibliographicReference_.version = version;
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

PWIZ_API_DECL void read(std::istream& is, IdentData& mzid,
                        const IterationListenerRegistry* iterationListenerRegistry,
                        SequenceCollectionFlag sequenceCollectionFlag,
                        AnalysisDataFlag analysisDataFlag)
{
    HandlerIdentData handler(&mzid, iterationListenerRegistry, sequenceCollectionFlag, analysisDataFlag);
    SAXParser::parse(is, handler);

    // "Fix" the name of the PSI-MS cv since the write's will assume
    // an id of MS
    fixCVList(mzid.cvs);

    References::resolve(mzid); 
}

} // namespace pwiz 
} // namespace identdata 
} // namespace IO 

