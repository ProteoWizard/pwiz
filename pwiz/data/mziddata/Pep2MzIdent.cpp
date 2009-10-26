//
// $Id$
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

#include "Pep2MzIdent.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/data/msdata/cv.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/algorithm/string.hpp"

using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;
using namespace pwiz::proteome;

using namespace boost;
using namespace std;

// String constants

const char* PERSON_DOC_OWNER = "PERSON_DOC_OWNER";

// Utility structs
struct Pep2MzIdent::Indices
{
    Indices()
        : dbseq(0), enzyme(0), sip(0), peptide(0),
          peptideEvidence(0), sd(0), sir(0), sii(0), sil(0)
    {
    }

    size_t dbseq;
    size_t enzyme;
    size_t sip;
    size_t peptide;
    size_t peptideEvidence;
    size_t sd;
    size_t sir;
    size_t sii;
    size_t sil;
};

struct sequence_p
{
    const string seq;
    
    sequence_p(const string& seq) : seq(seq) {}

    bool operator()(const PeptidePtr& p) const
    {
        return (p->peptideSequence == seq);
    }
};

template<typename T>
struct id_p
{
    const string id;

    id_p(const string id) : id(id) {}

    bool operator()(const shared_ptr<T>& t) const { return t->id == id; } 
};

// Utility functions
template<typename T>
shared_ptr<T> find_id(vector< shared_ptr<T> >& list, const string& id)
{
    typename vector< shared_ptr<T> >::iterator c =
        find_if(list.begin(), list.end(), id_p<T>(id));

    if (c == list.end())
        return shared_ptr<T>((T*)NULL);

    return *c;
}


//
// Pep2MzIdent
//

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr _mzid)
    : _mspa(mspa), mzid(_mzid),
      precursorMonoisotopic(false), fragmentMonoisotopic(false),
      indices(new Indices())
{
    mzid->cvs.push_back(cv("MS"));
    translateRoot();
}

/// Translates pepXML data needed for the mzIdentML tag.
void Pep2MzIdent::translateRoot()
{
    mzid->creationDate = _mspa.date;

    translateMetadata();

    translateEnzyme(_mspa.msmsRunSummary.sampleEnzyme, mzid);
    for (vector<SearchSummaryPtr>::const_iterator ss=_mspa.msmsRunSummary.searchSummary.begin();
         ss != _mspa.msmsRunSummary.searchSummary.end(); ss++)
    {
        translateSearch(*ss, mzid);
    }

    for (vector<SpectrumQueryPtr>::const_iterator it=_mspa.msmsRunSummary.spectrumQueries.begin(); it!=_mspa.msmsRunSummary.spectrumQueries.end(); it++)
    {
        translateQueries(*it, mzid);
    }

    addFinalElements();
}

// Copies the data in the enzyme tag into the mzIdentML tree.
void Pep2MzIdent::translateEnzyme(const SampleEnzyme& sampleEnzyme,
                                  MzIdentMLPtr result)
{
    SpectrumIdentificationProtocolPtr sip(
        new SpectrumIdentificationProtocol(
            "SIP_"+lexical_cast<string>(indices->sip++)));

    // DEBUG
    sip->analysisSoftwarePtr = AnalysisSoftwarePtr(new AnalysisSoftware("AS"));
    //sip->analysisSoftwarePtr = result->analysisSoftwareList.back();
    sip->searchType.set(MS_ms_ms_search);
    
    EnzymePtr enzyme(new Enzyme("E_"+lexical_cast<string>(indices->enzyme++)));

    // Cross fingers and pray that the name enzyme matches a cv name.
    // TODO create a more flexable conversion.
    enzyme->enzymeName.set(translator.translate(sampleEnzyme.name));
    enzyme->enzymeName.userParams.
        push_back(UserParam("description", sampleEnzyme.description));

    if (sampleEnzyme.fidelity == "Semispecific")
        enzyme->semiSpecific = true;
    else if (sampleEnzyme.fidelity == "Nonspecific")
        enzyme->semiSpecific = false;

    enzyme->minDistance = sampleEnzyme.specificity.minSpace;

    // TODO handle sense fields
    // first attempt at regex
    enzyme->siteRegexp = "[^"+sampleEnzyme.specificity.noCut+
        "]["+sampleEnzyme.specificity.cut+"]";
    
    sip->enzymes.enzymes.push_back(enzyme);

    result->analysisProtocolCollection.
        spectrumIdentificationProtocol.push_back(sip);
}

void Pep2MzIdent::translateSearch(const SearchSummaryPtr summary,
                                  MzIdentMLPtr result)
{
    // push SourceFilePtr onto sourceFile
    // in Inputs in DataCollection
    SourceFilePtr sourceFile(new SourceFile());
    sourceFile->id = "SF_1";
    sourceFile->location = summary->baseName;
    sourceFile->fileFormat.set(MS_ISB_mzXML_file);

    result->dataCollection.inputs.sourceFile.push_back(sourceFile);

    AnalysisSoftwarePtr as(new AnalysisSoftware());
    result->analysisSoftwareList.push_back(as);
    result->analysisSoftwareList.back()->softwareName.set(
        translator.translate(summary->searchEngine));

    // handle precursorMassType/fragmentMassType
    precursorMonoisotopic = summary->precursorMassType == "monoisotopic";
    fragmentMonoisotopic = summary->fragmentMassType == "monoisotopic";
    
    SearchDatabasePtr searchDatabase(new SearchDatabase());
    searchDatabase->id = "SD_1";
    searchDatabase->location = summary->searchDatabase.localPath;
    searchDatabase->version = summary->searchDatabase.databaseReleaseIdentifier;
    searchDatabase->numDatabaseSequences = summary->searchDatabase.sizeInDbEntries;
    searchDatabase->numResidues = summary->searchDatabase.sizeOfResidues;

    // Another case of crossing fingers and translating
    searchDatabase->DatabaseName.set(translator.translate(summary->searchEngine));

    if (summary->searchDatabase.type == "AA")
        searchDatabase->params.set(MS_database_type_amino_acid);
    else if (summary->searchDatabase.type == "NA")
        searchDatabase->params.set(MS_database_type_nucleotide);

    
    // TODO figure out if this is correct
    searchDatabase->DatabaseName.set(translator.translate(
                                         summary->searchDatabase.databaseName));
    searchDatabase->fileFormat.set(translator.translate(
                                       summary->searchDatabase.type));

    // Save for later.
    aminoAcidModifications = &summary->aminoAcidModifications;
}


void Pep2MzIdent::addModifications(
    const vector<AminoAcidModification>& aminoAcidModifications,
    PeptidePtr peptide, MzIdentMLPtr result)
{
    typedef vector<AminoAcidModification>::const_iterator aam_iterator;
    
    for (aam_iterator it=aminoAcidModifications.begin();
         it != aminoAcidModifications.end(); it++)
    {
        ModificationPtr mod(new Modification());

        // If the peptide has modified amino acids in the proper
        // position, Add a Modification element. "nc" is tacked on to
        // both until I find out where it goes.

        // If n terminus mod, check the beginning
        if (
            ((it->peptideTerminus == "n" ||
              it->peptideTerminus == "nc") &&
             peptide->peptideSequence.at(0) == it->aminoAcid.at(0))
            ||
            // If c terminus mod, check the end
            ((it->peptideTerminus == "c" ||
              it->peptideTerminus == "nc") &&
             peptide->peptideSequence.at(peptide->peptideSequence.size()-1)
             == it->aminoAcid.at(0))
            )
        {            
            if(precursorMonoisotopic)
                mod->monoisotopicMassDelta = it->massDiff;
            else
                mod->avgMassDelta = it->massDiff;
            mod->residues = it->aminoAcid;

            // TODO save terminus somewhere
            if (it->peptideTerminus == "c")
            {
                mod->location = peptide->peptideSequence.size();
            }
            else if (it->peptideTerminus == "n")
            {
                mod->location = 0;
            }
            else if (it->peptideTerminus == "nc")
            {
                mod->location = 0;

                // TODO is this right?
                ModificationPtr mod2(new Modification());
                mod2 = mod;
                mod2->location = peptide->peptideSequence.size();
                peptide->modification.push_back(mod2);
            }
        
            peptide->modification.push_back(mod);
        }
    }
}


void Pep2MzIdent::translateQueries(const SpectrumQueryPtr query,
                                   MzIdentMLPtr result)
{
    addPeptide(query, result);

    for(vector<SearchResultPtr>::iterator srit=query->searchResult.begin();
        srit != query->searchResult.end(); srit++)
    {
        for (vector<SearchHitPtr>::iterator shit=(*srit)->searchHit.begin();
             shit != (*srit)->searchHit.end(); shit++)
        {
            DBSequencePtr dbs(new DBSequence("DBS_"+lexical_cast<string>(indices->dbseq++)));
            dbs->length = (*shit)->peptide.length();
            //dbs->searchDatabasePtr =
            //mzid->dataCollection.inputs.searchDatabase.back();
            if (mzid->dataCollection.inputs.searchDatabase.size()>0)
                dbs->searchDatabasePtr = mzid->dataCollection.inputs.
                    searchDatabase.at(0);
            else
                dbs->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("SD_1"));

            PeptideEvidencePtr pepEv(new PeptideEvidence(
                                         "PEPEV_"+lexical_cast<string>(
                                             indices->peptideEvidence++)));
            
            // TODO make sure handle the spectrum field
            pepEv->paramGroup.userParams.push_back(UserParam("spectrum",
                                                             query->spectrum));

            pepEv->start = query->startScan;
            pepEv->end = query->endScan;
            pepEv->dbSequencePtr = dbs;
    
            SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem(
                                                  "SII_"+lexical_cast<string>(indices->sii++)));

            // TODO find out if this is right.
            sii->chargeState = query->assumedCharge;
    
            sii->peptideEvidence.push_back(pepEv);

            // TODO handle precursorNeutralMass
            // TODO handle index/retentionTimeSec fields
    
            SpectrumIdentificationResultPtr sip(new SpectrumIdentificationResult());
            sip->id = "SIR_"+lexical_cast<string>(indices->sir++);
            sip->spectrumID = query->spectrum;
            sip->spectrumIdentificationItem.push_back(sii);
            if (mzid->dataCollection.inputs.spectraData.size()>0)
                sip->spectraDataPtr = mzid->dataCollection.inputs.spectraData.at(0);
            else
                throw runtime_error("[Pep2MzIdent::translateQueries] no SpectraData");
    
            SpectrumIdentificationListPtr sil;
            if (result->dataCollection.analysisData.spectrumIdentificationList.
                size() > 0)
            {
                sil = result->dataCollection.analysisData.
                    spectrumIdentificationList.back();
            }
            else
            {
                sil = SpectrumIdentificationListPtr(
                    new SpectrumIdentificationList("SIL_"+lexical_cast<string>(
                                                       indices->sil++)));
                result->dataCollection.analysisData.spectrumIdentificationList.
                    push_back(sil);
            }

            if (sil->spectrumIdentificationResult.empty())
            {
                sil = SpectrumIdentificationListPtr(
                    new SpectrumIdentificationList("SIL_"+lexical_cast<string>(
                                                       indices->sil++)));
                result->dataCollection.analysisData.spectrumIdentificationList.
                    push_back(sil);
            }
            else
            {
                sil = result->dataCollection.analysisData.
                    spectrumIdentificationList.back();
            }

            if (!sil->empty())
                sil->spectrumIdentificationResult.push_back(sip);
        }
    }
}


MzIdentMLPtr Pep2MzIdent::translate()
{
    mzid = MzIdentMLPtr(new MzIdentML());
    mzid->cvs.push_back(cv("MS"));
   
    translateRoot();

    return mzid;
}

void Pep2MzIdent::processParameter(ParameterPtr parameter, MzIdentMLPtr mzid)
{
    if (parameter->name == "USERNAME")
    {
        ContactPtr cp = find_id(mzid->auditCollection,
                                PERSON_DOC_OWNER);

        Person* person;

        if (cp.get() && dynamic_cast<Person*>(cp.get()))
            person = (Person*)cp.get();
        else
        {
            mzid->auditCollection.push_back(
                PersonPtr(new Person(PERSON_DOC_OWNER)));
            person = (Person*)mzid->auditCollection.back().get();

        }

        person->lastName = parameter->value;
    }
    else if (parameter->name == "USEREMAIL")
    {
        ContactPtr cp = find_id(mzid->auditCollection,
                                PERSON_DOC_OWNER);
        
        Person* person;
        
        if (cp.get() && dynamic_cast<Person*>(cp.get()))
            person = (Person*)cp.get();
        else
        {
            mzid->auditCollection.push_back(
                PersonPtr(new Person(PERSON_DOC_OWNER)));
            person = (Person*)mzid->auditCollection.back().get();
        }

        person->email = parameter->value;
    }
    else if (parameter->name == "FILE")
    {
        SpectraDataPtr sd(new SpectraData(
                              "SD_"+lexical_cast<string>(indices->sd++)));
        sd->location = parameter->value;

        cerr << "Found FILE parameter of " << parameter->value << endl;
        if (iends_with(sd->location, ".mzML"))
        {
            sd->fileFormat.set(MS_mzML_file);
            sd->spectrumIDFormat.set(MS_mzML_unique_identifier);
        }
        else if (iends_with(sd->location, ".mzXML"))
        {
            sd->fileFormat.set(MS_ISB_mzXML_file);
            sd->spectrumIDFormat.set(MS_spectrum_from_database_nativeID_format);
        }
        else
            throw runtime_error(("[Pep2MzIdent::processParameter] Unknown file type for "+sd->location).c_str());
        
        mzid->dataCollection.inputs.spectraData.push_back(sd);
    }
    else if (parameter->name == "")
    {
    }
}


void Pep2MzIdent::translateMetadata()
{
    vector<SearchSummaryPtr>& ss = _mspa.msmsRunSummary.searchSummary;
    for (vector<SearchSummaryPtr>::const_iterator sit = ss.begin();
         sit != ss.end(); sit++)
    {
        vector<ParameterPtr>& pp = (*sit)->parameters;
        for (vector<ParameterPtr>::const_iterator pit=pp.begin();
             pit != pp.end(); pit++)
        {
            processParameter(*pit, mzid);
        }
    }
}

void Pep2MzIdent::addPeptide(const SpectrumQueryPtr sq, MzIdentMLPtr& mzid)
{
    for (vector<SearchResultPtr>::const_iterator sr=sq->searchResult.begin();
         sr != sq->searchResult.end(); sr++)
    {
        for (vector<SearchHitPtr>::const_iterator sh=(*sr)->searchHit.begin();
             sh != (*sr)->searchHit.end(); sh++)
        {
            // If we've already seen this sequence, continue on.
            if (find_if(mzid->sequenceCollection.peptides.begin(),
                        mzid->sequenceCollection.peptides.end(),
                        sequence_p((*sh)->peptide)) !=
                mzid->sequenceCollection.peptides.end())
                continue;
            
            PeptidePtr pp(new Peptide("PEP_"+lexical_cast<string>(indices->peptide++)));
            pp->id = (*sh)->peptide;
            pp->peptideSequence = (*sh)->peptide;

            mzid->sequenceCollection.peptides.push_back(pp);

            addModifications(*aminoAcidModifications, pp, mzid);
        }
    }
    // TODO: Add modification info
}

void Pep2MzIdent::translateSpectrumQuery(SpectrumIdentificationListPtr result,
                                         const SpectrumQueryPtr sq)
{
    SpectrumIdentificationResultPtr sir(new SpectrumIdentificationResult());
    if (mzid->dataCollection.inputs.spectraData.size() > 0)
        sir->spectraDataPtr = mzid->dataCollection.inputs.spectraData.at(0);
    
    for (vector<SearchResultPtr>::const_iterator sr=sq->searchResult.begin();
         sr != sq->searchResult.end(); sr++)
    {
        for (vector<SearchHitPtr>::const_iterator sh=(*sr)->searchHit.begin();
             sh != (*sr)->searchHit.end(); sh++)
        {
            SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());    
            PeptideEvidencePtr pepEv(new PeptideEvidence());
            
            sii->rank = (*sh)->hitRank;
            sii->peptidePtr = PeptidePtr(new Peptide((*sh)->peptide));
            pepEv->pre = (*sh)->peptidePrevAA;
            pepEv->post = (*sh)->peptideNextAA;
            sii->chargeState = sq->assumedCharge;
            sii->experimentalMassToCharge = Ion::mz(sq->precursorNeutralMass, sq->assumedCharge);
            sii->calculatedMassToCharge = Ion::mz((*sh)->calcNeutralPepMass, sq->assumedCharge);
            
            sir->spectrumIdentificationItem.push_back(sii);
        }
    }

    result->spectrumIdentificationResult.push_back(sir);
}

void Pep2MzIdent::addFinalElements()
{
    SpectrumIdentificationPtr sip(new SpectrumIdentification("SI"));
    sip->activityDate = mzid->creationDate;

    sip->spectrumIdentificationProtocolPtr = mzid->analysisProtocolCollection.
        spectrumIdentificationProtocol.back();
    sip->spectrumIdentificationListPtr = mzid->dataCollection.
        analysisData.spectrumIdentificationList.back();

    for (vector<SpectraDataPtr>::const_iterator it=mzid->dataCollection.
             inputs.spectraData.begin();
         it != mzid->dataCollection.inputs.spectraData.end(); it++)
    {
        sip->inputSpectra.push_back(*it);
    }

    for (vector<SearchDatabasePtr>::const_iterator it=mzid->dataCollection.
             inputs.searchDatabase.begin();
         it != mzid->dataCollection.inputs.searchDatabase.end(); it++)
    {
        sip->searchDatabase.push_back(*it);
    }

    mzid->analysisCollection.spectrumIdentification.push_back(sip);
}


