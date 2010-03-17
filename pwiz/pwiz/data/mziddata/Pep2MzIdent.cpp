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
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/common/cv.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/filesystem.hpp"
#include "boost/algorithm/string.hpp"

using namespace pwiz;
using namespace pwiz::cv;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;
using namespace pwiz::chemistry;

using namespace boost;
using namespace std;

// String constants

const char* PERSON_DOC_OWNER = "PERSON_DOC_OWNER";

// Utility structs
struct Pep2MzIdent::Indices
{
    Indices()
        : dbseq(0), enzyme(0), sip(0), peptide(0),
          peptideEvidence(0), sd(0), sir(0), sii(0), sil(0),
          pdp(0)
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
    size_t pdp;
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

struct seq_p
{
    const string seq;
    
    seq_p(const string& seq) : seq(seq) {}

    bool operator()(const DBSequencePtr& dbs) const
    {
        return (dbs->seq == seq);
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


struct search_score_p
{
    const string name;

    search_score_p(const string& name) : name(name) {}

    bool operator()(SearchScorePtr ss)
    {
        return iequals(name, ss->name);
    }
};

void resizeSoftware(vector<AnalysisSoftwarePtr>& v, size_t new_size)
{
    size_t start = v.size();
    for (size_t i=start; i<new_size; i++)
        v.push_back(AnalysisSoftwarePtr(new AnalysisSoftware()));
    
}

struct software_p
{
    CVID id;
    
    software_p(const CVID id) : id(id) {}

    bool operator()(const AnalysisSoftwarePtr p)
    {
        bool result = false;

        if (p.get())
            result = p->softwareName.hasCVParam(id);

        return result;
    }
};


AnalysisSoftwarePtr findSoftware(const vector<AnalysisSoftwarePtr>& software,
    CVID cvid)
{
    AnalysisSoftwarePtr as((AnalysisSoftware*)NULL);
    
    vector<AnalysisSoftwarePtr>::const_iterator i =
        find_if(software.begin(), software.end(), software_p(cvid));
    
    if (i != software.end())
        as = *i;
    
    return  as;
}

CVParam guessThreshold(const vector<AnalysisSoftwarePtr>& software)
{
    static const CVID cvids[] = {MS_Mascot, MS_Sequest,
                                 MS_Phenyx, CVID_Unknown};
    CVParam cvparam;

    for (size_t idx = 0; cvids[idx] != CVID_Unknown; idx++)
    {
        AnalysisSoftwarePtr as = findSoftware(software, cvids[idx]);
        
        if (!as.get())
        {
            cvparam = CVParam(cvids[idx], "0.5");
            break;
        }
    }

    // TODO put a reasonable default here and a reasonable way to set it.
    if (cvparam.cvid == CVID_Unknown)
    {
        cvparam.cvid = MS_Mascot;
        cvparam.value = "0.05";
    }
    
    return cvparam;
}

AnalysisSoftwarePtr guessAnalysisSoftware(
    const vector<AnalysisSoftwarePtr>& software)
{
    static const CVID cvids[] = {MS_Mascot, MS_Sequest,
                                 MS_Phenyx, CVID_Unknown};
    AnalysisSoftwarePtr asp(new AnalysisSoftware());

    for (size_t idx = 0; cvids[idx] != CVID_Unknown; idx++)
    {
        AnalysisSoftwarePtr as = findSoftware(software, cvids[idx]);
        
        if (as.get() && !as->empty())
        {
            asp = as;
            break;
        }
    }

    return asp;
}

//
// Pep2MzIdent
//

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr mzid)
    : _mspa(&mspa), mzid(mzid),
      precursorMonoisotopic(false), fragmentMonoisotopic(false),
      indices(new Indices())
{
    mzid->cvs.push_back(cv::cv("MS"));
    mzid->cvs.push_back(cv::cv("UO"));
    //translate();
}

void Pep2MzIdent::setMspa(const MSMSPipelineAnalysis& mspa)
{
    clear();
    
    _mspa = &mspa;
}

void Pep2MzIdent::clear()
{
    indices = shared_ptr<Indices>(new Indices());

    _mspa = NULL;

    mzid = MzIdentMLPtr(new MzIdentML());
    mzid->cvs.push_back(cv::cv("MS"));
    mzid->cvs.push_back(cv::cv("UO"));

    _translated = false;

    precursorMonoisotopic = false;
    fragmentMonoisotopic = false;

    seqPeptidePairs.clear();
    aminoAcidModifications = NULL;
}

void Pep2MzIdent::translateRoot()
{
    mzid->creationDate = _mspa->date;

    translateEnzyme(_mspa->msmsRunSummary.sampleEnzyme, mzid);

    earlyMetadata();

    for (vector<SearchSummaryPtr>::const_iterator ss =
             _mspa->msmsRunSummary.searchSummary.begin();
         ss != _mspa->msmsRunSummary.searchSummary.end(); ss++)
    {
        translateSearch(*ss, mzid);
    }

    for (vector<SpectrumQueryPtr>::const_iterator it=_mspa->msmsRunSummary.spectrumQueries.begin(); it!=_mspa->msmsRunSummary.spectrumQueries.end(); it++)
    {
        translateQueries(*it, mzid);
    }

    lateMetadata();
    
    addFinalElements();
}

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
    sip->threshold.cvParams.push_back(guessThreshold(mzid->analysisSoftwareList));
    EnzymePtr enzyme(new Enzyme("E_"+lexical_cast<string>(indices->enzyme++)));

    // Cross fingers and pray that the name enzyme matches a cv name.
    // TODO create a more flexable conversion.
    enzyme->enzymeName.set(getCVID(sampleEnzyme.name));
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

CVParam Pep2MzIdent::translateSearchScore(const string& name, const vector<SearchScorePtr>& searchScore)
{
    typedef vector<SearchScorePtr>::const_iterator CIt;
    
    CIt cit = find_if(searchScore.begin(), searchScore.end(),
                      search_score_p(name));

    CVParam cvp;
    if (cit != searchScore.end())
    {
        cvp = getParamForSearchScore(*cit);
    }

    return cvp;
}

CVParam Pep2MzIdent::getParamForSearchScore(const SearchScorePtr searchScore)
{
    CVID id = cvidFromSearchScore(searchScore->name);

    CVParam cvParam(id, searchScore->value);

    return cvParam;
}

CVID Pep2MzIdent::cvidFromSearchScore(const string& name)
{
    CVID id = translator.translate(name);

    if (id == CVID_Unknown)
    {
        if (iequals(name, "ionscore"))
        {
            id = MS_mascot_score;
        }
        else if (iequals(name, "identityscore"))
        {
            id = MS_mascot_identity_threshold;
        }
        else if (iequals(name, "expect"))
        {
            id = MS_mascot_expectation_value;
        }
    }

    return id;
}


void Pep2MzIdent::translateSearch(const SearchSummaryPtr summary,
                                  MzIdentMLPtr result)
{
    namespace fs = boost::filesystem;

    // push SourceFilePtr onto sourceFile
    // in Inputs in DataCollection
    SourceFilePtr sourceFile(new SourceFile());
    sourceFile->id = "SF_1";
    sourceFile->location = summary->baseName;
    sourceFile->fileFormat.set(MS_ISB_mzXML_file);

    result->dataCollection.inputs.sourceFile.push_back(sourceFile);

    AnalysisSoftwarePtr as(new AnalysisSoftware());
    as->id = "AS";
    as->softwareName.set(getCVID(summary->searchEngine));
    result->analysisSoftwareList.push_back(as);

    // handle precursorMassType/fragmentMassType
    precursorMonoisotopic = summary->precursorMassType == "monoisotopic";
    fragmentMonoisotopic = summary->fragmentMassType == "monoisotopic";
    
    SearchDatabasePtr searchDatabase(new SearchDatabase());
    searchDatabase->id = "SD_1";
    searchDatabase->name = summary->searchDatabase.databaseName;
    searchDatabase->location = summary->searchDatabase.localPath;
    searchDatabase->version = summary->searchDatabase.databaseReleaseIdentifier;
    searchDatabase->numDatabaseSequences = summary->searchDatabase.sizeInDbEntries;
    searchDatabase->numResidues = summary->searchDatabase.sizeOfResidues;

    // Select which type of database is indeicated.
    if (summary->searchDatabase.type == "AA")
        searchDatabase->params.set(MS_database_type_amino_acid);
    else if (summary->searchDatabase.type == "NA")
        searchDatabase->params.set(MS_database_type_nucleotide);

    
    // TODO figure out if this is correct
    CVID dbName = getCVID(summary->searchDatabase.databaseName);
    if (dbName != CVID_Unknown)
        searchDatabase->DatabaseName.set(dbName);
    else
    {
        fs::path localPath(summary->searchDatabase.localPath);
        searchDatabase->DatabaseName.userParams.push_back(
            UserParam(localPath.filename()));
    }

    // TODO make this more elegant
    if (iends_with(summary->baseName, ".dat") &&
        iequals(summary->searchEngine, "mascot"))
        searchDatabase->fileFormat.set(MS_Mascot_DAT_file);
    else if (iequals(summary->searchEngine, "sequest"))
        searchDatabase->fileFormat.set(MS_Sequest_out_file);
    else if (iequals(summary->searchEngine, "xtandem"))
        searchDatabase->fileFormat.set(MS_xtandem_xml_file);

    mzid->dataCollection.inputs.searchDatabase.push_back(searchDatabase);
    
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
    for(vector<SearchResultPtr>::iterator srit=query->searchResult.begin();
        srit != query->searchResult.end(); srit++)
    {
        for (vector<SearchHitPtr>::iterator shit=(*srit)->searchHit.begin();
             shit != (*srit)->searchHit.end(); shit++)
        {
            const string pid = addPeptide(*shit, result);

            if (find_if(mzid->sequenceCollection.dbSequences.begin(),
                        mzid->sequenceCollection.dbSequences.end(),
                        seq_p((*shit)->peptide)) !=
                mzid->sequenceCollection.dbSequences.end())
                continue;
            
            DBSequencePtr dbs(new DBSequence("DBS_"+lexical_cast<string>(
                                                 indices->dbseq++)));
            dbs->length = (*shit)->peptide.length();
            dbs->seq = (*shit)->peptide;
            dbs->accession = (*shit)->protein;
            dbs->paramGroup.set(MS_protein_description, (*shit)->proteinDescr);
            if (mzid->dataCollection.inputs.searchDatabase.size()>0)
                dbs->searchDatabasePtr = mzid->dataCollection.inputs.
                    searchDatabase.at(0);
            else
                dbs->searchDatabasePtr = SearchDatabasePtr(
                    new SearchDatabase("SD_1"));

            mzid->sequenceCollection.dbSequences.push_back(dbs);
            
            PeptideEvidencePtr pepEv(new PeptideEvidence(
                                         "PE_"+lexical_cast<string>(
                                             indices->peptideEvidence++)));
            
            // TODO make sure handle the spectrum field
            pepEv->paramGroup.userParams.push_back(UserParam("spectrum",
                                                             query->spectrum));

            pepEv->start = query->startScan;
            pepEv->end = query->endScan;
            pepEv->dbSequencePtr = dbs;
    
            SpectrumIdentificationItemPtr sii(
                new SpectrumIdentificationItem(
                    "SII_"+lexical_cast<string>(indices->sii++)));

            // TODO find out if this is right.
            sii->chargeState = query->assumedCharge;

            // TODO get search_score
            CVParam cvp = translateSearchScore("ionscore",
                                               (*shit)->searchScore);

            if (cvp.cvid != CVID_Unknown)
                sii->paramGroup.set(cvp.cvid, cvp.value);
            cvp = translateSearchScore("expect", (*shit)->searchScore);
            if (cvp.cvid != CVID_Unknown)
                sii->paramGroup.set(cvp.cvid, cvp.value);
            
            sii->peptideEvidence.push_back(pepEv);

            // TODO handle precursorNeutralMass
            // TODO handle index/retentionTimeSec fields
    
            SpectrumIdentificationResultPtr sirp(
                new SpectrumIdentificationResult());
            sirp->id = "SIR_"+lexical_cast<string>(indices->sir++);

            cvp = translateSearchScore("identityscore", (*shit)->searchScore);
            sirp->paramGroup.set(cvp.cvid, cvp.value);

            sirp->spectrumID = query->spectrum;
            sirp->spectrumIdentificationItem.push_back(sii);
            if (mzid->dataCollection.inputs.spectraData.size()>0)
                sirp->spectraDataPtr = mzid->dataCollection.inputs.
                    spectraData.at(0);
            else
                throw runtime_error("[Pep2MzIdent::translateQueries] no "
                                    "SpectraData");
    
            SpectrumIdentificationListPtr sil;
            if (mzid->dataCollection.analysisData.
                spectrumIdentificationList.size() > 0)
            {
                sil = mzid->dataCollection.analysisData.
                    spectrumIdentificationList.back();
            }
            else
            {
                sil = SpectrumIdentificationListPtr(
                    new SpectrumIdentificationList("SIL_"+lexical_cast<string>(
                                                       indices->sil++)));
                mzid->dataCollection.analysisData.spectrumIdentificationList.
                    push_back(sil);
            }

            if (!sil->empty())
                sil->spectrumIdentificationResult.push_back(sirp);
            
            /*
            if (sil->spectrumIdentificationResult.empty())
            {
                sil = SpectrumIdentificationListPtr(
                    new SpectrumIdentificationList("SIL_"+lexical_cast<string>(
                                                       indices->sil++)));
                mzid->dataCollection.analysisData.spectrumIdentificationList.
                    push_back(sil);
            }
            else
            {
                sil = mzid->dataCollection.analysisData.
                    spectrumIdentificationList.back();
            }

            if (!sil->empty())
                sil->spectrumIdentificationResult.push_back(sip);
            */

        }
    }
}


MzIdentMLPtr Pep2MzIdent::translate()
{
    mzid = MzIdentMLPtr(new MzIdentML());
    mzid->cvs.push_back(cv::cv("MS"));
    mzid->cvs.push_back(cv::cv("UO"));
   
    translateRoot();

    return mzid;
}

void Pep2MzIdent::earlyParameters(ParameterPtr parameter,
                                        MzIdentMLPtr mzid)
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

        if (iends_with(sd->location, ".mzML"))
        {
            sd->fileFormat.set(MS_mzML_file);
            sd->spectrumIDFormat.set(MS_mzML_unique_identifier);
        }
        else if (iends_with(sd->location, ".mzXML"))
        {
            sd->fileFormat.set(MS_ISB_mzXML_file);
            sd->spectrumIDFormat.set(MS_scan_number_only_nativeID_format);
        }
        else
            throw runtime_error(("[Pep2MzIdent::processParameter] Unknown "
                                 "file type for "+sd->location).c_str());
        
        mzid->dataCollection.inputs.spectraData.push_back(sd);
    }
    else if (parameter->name == "TOL")
    {
        if (mzid->analysisProtocolCollection.
            spectrumIdentificationProtocol.size()>0)
        {
            SpectrumIdentificationProtocolPtr sip =
                mzid->analysisProtocolCollection.
                spectrumIdentificationProtocol.at(0);

            CVParam cvp = sip->fragmentTolerance.
                cvParam(MS_search_tolerance_plus_value);

            sip->fragmentTolerance.set(MS_search_tolerance_plus_value,
                                       parameter->value,
                                       cvp.units);
            
            cvp = sip->fragmentTolerance.
                cvParam(MS_search_tolerance_minus_value);
            
            sip->fragmentTolerance.set(MS_search_tolerance_minus_value,
                                       parameter->value,
                                       cvp.units);
        }
    }
    else if (parameter->name == "TOLU")
    {
        if (mzid->analysisProtocolCollection.
            spectrumIdentificationProtocol.size()>0)
        {
            SpectrumIdentificationProtocolPtr sip =
                mzid->analysisProtocolCollection.
                spectrumIdentificationProtocol.at(0);

            CVParam cvp = sip->fragmentTolerance.
                cvParam(MS_search_tolerance_plus_value);
            
            sip->fragmentTolerance.set(MS_search_tolerance_plus_value,
                                       cvp.value,
                                       getCVID(parameter->value));

            cvp = sip->fragmentTolerance.
                cvParam(MS_search_tolerance_minus_value);
            
            sip->fragmentTolerance.set(MS_search_tolerance_minus_value,
                                       cvp.value,
                                       getCVID(parameter->value));
        }
    }
    else if (parameter->name == "ITOL")
    {
        if (mzid->analysisProtocolCollection.
            spectrumIdentificationProtocol.size()>0)
        {
            SpectrumIdentificationProtocolPtr sip =
                mzid->analysisProtocolCollection.
                spectrumIdentificationProtocol.at(0);

            CVParam cvp = sip->parentTolerance.
                cvParam(MS_search_tolerance_plus_value);

            sip->parentTolerance.set(MS_search_tolerance_plus_value,
                                       parameter->value,
                                       cvp.units);
            
            cvp = sip->parentTolerance.
                cvParam(MS_search_tolerance_minus_value);
            
            sip->parentTolerance.set(MS_search_tolerance_minus_value,
                                       parameter->value,
                                       cvp.units);
        }
    }
    else if (parameter->name == "ITOLU")
    {
        if (mzid->analysisProtocolCollection.
            spectrumIdentificationProtocol.size()>0)
        {
            SpectrumIdentificationProtocolPtr sip =
                mzid->analysisProtocolCollection.
                spectrumIdentificationProtocol.at(0);

            CVParam cvp = sip->parentTolerance.
                cvParam(MS_search_tolerance_plus_value);
            
            sip->parentTolerance.set(MS_search_tolerance_plus_value,
                                       cvp.value,
                                       getCVID(parameter->value));

            cvp = sip->parentTolerance.
                cvParam(MS_search_tolerance_minus_value);
            
            sip->parentTolerance.set(MS_search_tolerance_minus_value,
                                       cvp.value,
                                     getCVID(parameter->value));
        }
    }
    else if (parameter->name == "_mzML.fileDescription.sourceFileList."
             "sourceFile.cvParam.01.accession")
    {
        istringstream oss(parameter->value.substr(
                              parameter->value.find_first_of(":")));
        size_t cvid;
        oss >> cvid;
    }
    else if (parameter->name == "_mzML.fileDescription.sourceFileList."
             "sourceFile.cvParam.02.accession")
    {
        istringstream oss(parameter->value.substr(
                              parameter->value.find_first_of(":")));
        size_t cvid;
        oss >> cvid;
    }
    else if (parameter->name == "_mzML.fileDescription.sourceFileList."
             "sourceFile.cvParam.03.accession")
    { 
        istringstream oss(parameter->value.substr(
                              parameter->value.find_first_of(":")));
        size_t cvid;
        
        oss >> cvid;
    }
    else if (parameter->name == "_mzML.fileDescription.sourceFileList."
             "sourceFile.cvParam.03.value")
    {
    }
    else if (parameter->name == "_mzML.referenceableParamGroupList."
             "referenceableParamGroup.cvParam.01.accession")
    {
        size_t idx = parameter->value.find_first_of(":");

        if (idx == string::npos)
            return;
        
        istringstream oss(parameter->value.substr(idx));
        size_t cvid;
        oss >> cvid;
    }
    else if (parameter->name == "_mzML.referenceableParamGroupList."
             "referenceableParamGroup.cvParam.02.accession")
    {
        size_t idx = parameter->value.find_first_of(":");

        if (idx == string::npos)
            return;
        
        istringstream oss(parameter->value.substr(idx));
        size_t cvid;
        oss >> cvid;
    }
    else if (parameter->name == "_mzML.referenceableParamGroupList."
             "referenceableParamGroup.cvParam.02.value")
    {
    }
    else if (starts_with(parameter->name, "_mzML.softwareList.") &&
             starts_with(parameter->name, ".count"))
    {
        size_t idx = parameter->value.find_first_of(":");

        if (idx == string::npos)
            return;
        
        istringstream oss(parameter->value.substr(idx));
        size_t id;
        oss >> id;

        size_t start = mzid->analysisSoftwareList.size();
        for (size_t i=start; i<id; i++)
            mzid->analysisSoftwareList.push_back(
                AnalysisSoftwarePtr(new AnalysisSoftware()));
    }
    else if (starts_with(parameter->name, "_mzML.softwareList.software."))
    {
        if (parameter->name.size() < 31)
            return;
        string number = parameter->name.substr(28, 2);

        istringstream oss(number);
        size_t idx;
        oss >> idx;

        if (idx < 1)
            return;
        else if (mzid->analysisSoftwareList.size() < idx)
            resizeSoftware(mzid->analysisSoftwareList, idx);
        
        if (ends_with(parameter->name, "id"))
        {
            mzid->analysisSoftwareList[idx-1]->id = parameter->value;
            CVID cvid = getCVID(parameter->value);
            if (cvid != CVID_Unknown)
                mzid->analysisSoftwareList[idx-1]->softwareName.set(cvid);
        }
        else if (ends_with(parameter->name, "version"))
        {
            mzid->analysisSoftwareList[idx-1]->version = parameter->value;
        }
    }
}

void Pep2MzIdent::lateParameters(ParameterPtr parameter,
                                       MzIdentMLPtr mzid)
{
    if (parameter->name == "USERNAME" ||
        parameter->name == "USEREMAIL" || 
        parameter->name == "FILE" ||
        parameter->name == "TOL" ||
        parameter->name == "TOLU" ||
        parameter->name == "ITOL" || 
        parameter->name == "ITOLU" ||
        starts_with(parameter->name, "_mzML.fileDescription.sourceFileList."
                    "sourceFile.cvParam") ||
        starts_with(parameter->name, "_mzML.referenceableParamGroupList."
                    "referenceableParamGroup.cvParam") || 
        starts_with(parameter->name, "_mzML.softwareList.") ||
        starts_with(parameter->name, "_mzML.softwareList.software."))
    {
        // These parameters have already been dealt with in earlyParameters.
        return;
    }
    else if (parameter->name == "PEAK")
    {
        if (mzid->analysisProtocolCollection.proteinDetectionProtocol.size()==0)
            mzid->analysisProtocolCollection.proteinDetectionProtocol.
                push_back(ProteinDetectionProtocolPtr(
                              new ProteinDetectionProtocol(
                                  "PDP_"+lexical_cast<string>(
                                      indices->pdp++))));
        
        ProteinDetectionProtocolPtr pdp = mzid->analysisProtocolCollection.
            proteinDetectionProtocol.back();
        // TODO eventually use guessAnalysisSoftware
        pdp->analysisSoftwarePtr  =AnalysisSoftwarePtr(new AnalysisSoftware("AS"));

        CVParam cvparam = guessThreshold(mzid->analysisSoftwareList);
        pdp->threshold.cvParams.push_back(cvparam);
        pdp->analysisSoftwarePtr = findSoftware(mzid->analysisSoftwareList,
                                                cvparam.cvid);
        pdp->analysisParams.set(MS_mascot_MaxProteinHits, parameter->value);
    }
    
    // TODO stick the rest in UserParam objects somewhere.
}

void Pep2MzIdent::earlyMetadata()
{
    const vector<SearchSummaryPtr>* ss = &_mspa->msmsRunSummary.searchSummary;
    for (vector<SearchSummaryPtr>::const_iterator sit = ss->begin();
         sit != ss->end(); sit++)
    {
        vector<ParameterPtr>& pp = (*sit)->parameters;
        for (vector<ParameterPtr>::const_iterator pit=pp.begin();
             pit != pp.end(); pit++)
        {
            earlyParameters(*pit, mzid);
        }
    }
}

void Pep2MzIdent::lateMetadata()
{
    const vector<SearchSummaryPtr>* ss = &_mspa->msmsRunSummary.searchSummary;
    for (vector<SearchSummaryPtr>::const_iterator sit = ss->begin();
         sit != ss->end(); sit++)
    {
        vector<ParameterPtr>& pp = (*sit)->parameters;
        for (vector<ParameterPtr>::const_iterator pit=pp.begin();
             pit != pp.end(); pit++)
        {
            lateParameters(*pit, mzid);
        }
    }
}

const string Pep2MzIdent::addPeptide(const SearchHitPtr sh, MzIdentMLPtr& mzid)
{
    // If we've already seen this sequence, continue on.
    if (find_if(mzid->sequenceCollection.peptides.begin(),
                mzid->sequenceCollection.peptides.end(),
                sequence_p(sh->peptide)) !=
        mzid->sequenceCollection.peptides.end())
        return "";
    
    PeptidePtr pp(new Peptide("PEP_"+lexical_cast<string>(indices->peptide++)));
    pp->id = sh->peptide;
    pp->peptideSequence = sh->peptide;
    
    mzid->sequenceCollection.peptides.push_back(pp);
    
    addModifications(*aminoAcidModifications, pp, mzid);

    return pp->id;
}


CVID Pep2MzIdent::getCVID(const string& name)
{
    CVID id = translator.translate(name);

    if (id == CVID_Unknown)
    {
        if (iequals(name, "mascot"))
        {
            id = MS_Mascot;
        }
        else if (iequals(name, "sequest"))
        {
            id = MS_Sequest;
        }
        else if (iequals(name, "phenyx"))
        {
            id = MS_Phenyx;
        }
        else if (iequals(name, "da"))
        {
            id = UO_dalton;
        }
    }
    
    return id;
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

    /*
    if (mzid->analysisProtocolCollection.
        spectrumIdentificationProtocol.size() > 0)
    {
        mzid->analysisProtocolCollection.spectrumIdentificationProtocol.at(0)
            ->threshold.userParams.push_back(UserParam("unknown"));
    }
    */
}


