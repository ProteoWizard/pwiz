//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
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

#include "MascotReader.hpp"
#include "pwiz/data/identdata/MzidPredicates.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/data/identdata/TextWriter.hpp"

#include "mascotutil.hpp"
#include "msparser.hpp"

#include <boost/algorithm/string.hpp>
#include <boost/tokenizer.hpp>
#include <boost/foreach.hpp>
#include <boost/regex.hpp>
#include <iostream>
#include <algorithm>

namespace {

static const char* modMappings[] = {""};

struct terminfo_name_p
{
    string mine;
    terminfo_name_p(const string mine) : mine(mine){}
    bool operator()(const pwiz::cv::CVTermInfo* yours)
    {
        return yours && mine == yours->name;
    }
};

struct Indices
{
    Indices()
        : enzyme(0), sequence(0), spectrum_id_item(0),
          proteindetectionlist(0),
          proteinambiguitygroup(0),
          proteindetectionhypothesis(0),
          peptide(0), peptideevidence(0)
    {
    }
    
    string makeIndex(const string& prefix, size_t& index)
    {
        ostringstream oss;
        oss << prefix << index++;

        return oss.str();
    }

    // Will there ever be more than one enzyme?
    size_t enzyme;
    
    size_t sequence;
    size_t spectrum_id_item;

    size_t proteindetectionlist;
    size_t proteinambiguitygroup;
    size_t proteindetectionhypothesis;
    size_t peptide;
    size_t dbsequence;
    size_t peptideevidence;
};


template<class T>
struct id_p
{
    const string a;
    
    id_p(const string& _a) : a(_a) {}

    bool operator()(const T& b) {return a == b.id;}
    bool operator()(const shared_ptr<T>& b) {return a == b->id;}
};

} // anonymous namespace

namespace pwiz {
namespace identdata {

using pwiz::cv::CVTermInfo;

// All the classes are part of the matrix_science namespace
using namespace matrix_science;
using namespace boost;
using std::vector;

//
// MascotReader::Impl
//
class MascotReader::Impl
{
public:
    Impl()
        :varmodPattern("(.*) \\((.+)\\)"),
         varmodListOfChars("([A-Z]+)")
    {}
    
    void read(const string& filename, const string head,
              IdentData& result)
    {
        ms_mascotresfile file(filename.c_str());

        if (file.isValid())
        {
            // We get this for free just by being in this function.
            addMzid(file, result);
            addMascotSoftware(file, result);
            addSearchDatabases(file, result);
            
            searchInformation(file, result);
            inputData(file, result);
            searchParameters(file, result);
            proteinSummary(file, result);
        }
    }

    void addMzid(ms_mascotresfile& file, IdentData& result)
    {
        result.id = "MZID";
        result.name = file.params().getCOM();
    }
    
    /**
     * Sets the measure cvParams used later in fillFragmentation().
     */
    void fillFragmentationTable(vector<MeasurePtr>& fragmentationTable)
    {
        MeasurePtr measure(new Measure(mz_id));
        measure->set(MS_product_ion_m_z);
        fragmentationTable.push_back(measure);

        measure = MeasurePtr(new Measure(intensity_id));
        measure->set(MS_product_ion_intensity);
        fragmentationTable.push_back(measure);

        measure = MeasurePtr(new Measure(error_id));
        measure->set(MS_product_ion_m_z_error);
        fragmentationTable.push_back(measure);
    }
    
    bool fillFragmentation(vector< pair<double, double> > peaks,
                           vector<MeasurePtr>& measures,
                           IonTypePtr ionType)
    {
        if (peaks.empty())
            return false;
        
        FragmentArrayPtr mzFa(new FragmentArray());
        FragmentArrayPtr intFa(new FragmentArray());
        vector<double> mzArray, intensityArray;

        // Add the Measure_ref that indicates what kind of a list each
        // FragmentArray is.
        typedef vector<MeasurePtr>::const_iterator measure_iterator;
        measure_iterator mit = find_if(measures.begin(),
                                       measures.end(), id_p<Measure>(mz_id));
        if (mit != measures.end())
            mzFa->measurePtr = *mit;
        
        mit = find_if(measures.begin(), measures.end(),
                      id_p<Measure>(intensity_id));

        if (mit != measures.end())
            intFa->measurePtr = *mit;

        // For each pair, split them into m/z and intensity.
        typedef pair<double, double> peak_pair;
        BOOST_FOREACH(peak_pair peak, peaks)
        {
            mzArray.push_back(peak.first);
            intensityArray.push_back(peak.second);
        }

        // Once filled, each FragmentArray is put into the ionType's
        // fragmentArray vector.
        copy(mzArray.begin(), mzArray.end(), mzFa->values.begin());
        ionType->fragmentArray.push_back(mzFa);

        copy(intensityArray.begin(), intensityArray.end(), intFa->values.begin());
        ionType->fragmentArray.push_back(intFa);

        return true;
    }
    
    // Add Mascot to the analysis software
    void addMascotSoftware(ms_mascotresfile & file, IdentData& mzid)
    {
        AnalysisSoftwarePtr as(new AnalysisSoftware("AS_0"));
        as->version = file.getMascotVer();
        as->softwareName.set(MS_Mascot);
        mzid.analysisSoftwareList.push_back(as);
    }

    SpectrumIdentificationProtocolPtr getSpectrumIdentificationProtocol(
        IdentData& mzid)
    {
        SpectrumIdentificationProtocolPtr sip;
        
        if (mzid.analysisProtocolCollection.
            spectrumIdentificationProtocol.size())
        {
            sip = mzid.analysisProtocolCollection.
                spectrumIdentificationProtocol.at(0);
        }
        else
        {
            sip = SpectrumIdentificationProtocolPtr(
                new SpectrumIdentificationProtocol("SIP"));

            // Assume we've already called the addMascot method
            sip->analysisSoftwarePtr =
                mzid.analysisSoftwareList.at(0);
            
            mzid.analysisProtocolCollection.
                spectrumIdentificationProtocol.
                push_back(sip);
        }

        return sip;
    }
    
    // Add the FASTA file search database
    void addSearchDatabases(ms_mascotresfile & file, IdentData& mzid)
    {
        for (int i=1; i<=file.params().getNumberOfDatabases();i++)
        {
            SearchDatabasePtr sd(new SearchDatabase(file.params().getDB(i)));
            sd->location = file.getFastaPath(i);
            sd->version = file.getFastaVer(i);
            sd->releaseDate = file.getFastaVer(i);
            sd->numDatabaseSequences = file.getNumSeqs(i);
            sd->numResidues = file.getNumResidues(i);
            // TODO add a CVParam/UserParam w/ the name of the database.
            mzid.dataCollection.inputs.searchDatabase.push_back(sd);
        }
    }

    CVID getToleranceUnits(const string& units)
    {
        CVID cvid = CVID_Unknown;
        
        if (iequals(units, "da") ||
            iequals(units, "u") ||
            iequals(units, "unified atomic mass unit"))
        {
            cvid = UO_dalton;
        }
        else if (iequals(units, "kda"))
        {
            cvid = UO_kilodalton;
        }

        return cvid;
    }

    EnzymePtr getEnzyme(ms_searchparams& msp)
    {
        EnzymePtr ez(new Enzyme(indices.makeIndex("EZ_", indices.enzyme)));

        ez->missedCleavages = msp.getPFA();
        
        // TODO add other enzymes
        if (msp.getCLE() == "Trypsin")
        {
            ez->enzymeName.set(MS_Trypsin);
        }
        else
            cerr << "[MascotReader::Impl::getEnzyme()] Unhandled enzyme "
                 << msp.getCLE() << endl;
        
        return ez;
    }
    
    void addUser(ms_searchparams& msp, IdentData& mzid)
    {
        PersonPtr user(new Person(owner_person_id));
        user->lastName = msp.getUSERNAME();
        user->set(MS_contact_email,msp.getUSEREMAIL());

        mzid.auditCollection.push_back(user);

        mzid.provider.id = provider_id;
        mzid.provider.contactRolePtr = ContactRolePtr(new ContactRole());
        mzid.provider.contactRolePtr->contactPtr = user;

        // TODO Is this right?
        mzid.provider.contactRolePtr->cvid = MS_researcher;
    }

    void addMassTable(ms_searchparams& p, IdentData& mzid)
    {
        SpectrumIdentificationProtocolPtr sip =
            getSpectrumIdentificationProtocol(mzid);

        MassTablePtr massTable(new MassTable("MT"));

        for (char ch='A'; ch <= 'Z'; ch++)
        {
            ResiduePtr residue(new Residue());
            residue->code = ch;
            residue->mass = p.getResidueMass(ch);
            massTable->residues.push_back(residue);
        }
        sip->massTable.push_back(massTable);
        
    }

    void parseTaxonomy(const string& mascot_tax,
                       string& scientific, string& common)
    {
        const char* pattern = "[ \\.]*([\\w ]+)[ ]*\\((\\w+)\\).*";

        regex namesPattern(pattern);

        cmatch what;
        if (regex_match(mascot_tax.c_str(), what, namesPattern))
        {
            scientific.assign(what[1].first, what[1].second);
            common.assign(what[2].first, what[2].second);
        }
    }
    
    void addAnalysisProtocol(ms_mascotresfile & file, IdentData& mzid)
    {
        ms_searchparams& p = file.params();
        
        SpectrumIdentificationProtocolPtr sip =
            getSpectrumIdentificationProtocol(mzid);
        
        sip->parentTolerance.set(MS_search_tolerance_plus_value, p.getTOL(),
                                 getToleranceUnits(p.getTOLU()));
        sip->fragmentTolerance.set(MS_search_tolerance_plus_value,p.getITOL(),
                                        getToleranceUnits(p.getITOLU()));

        EnzymePtr ez = getEnzyme(p);
        
        sip->enzymes.enzymes.push_back(ez);

        CVID fragTolU = getToleranceUnits(p.getITOLU());
        sip->fragmentTolerance.set(MS_search_tolerance_plus_value,
                                   p.getITOL(), fragTolU);

        if (file.anyMSMS())
            sip->searchType = MS_ms_ms_search;

        if (file.anyPMF())
            sip->searchType = MS_pmf_search;

        // TODO Is SQ == MIS as documented?
        if (file.anySQ())
            sip->searchType = MS_ms_ms_search;

        // TODO add taxonomy search mod
        if (p.getTAXONOMY().size()>0)
        {
            string scientific, common;
            parseTaxonomy(p.getTAXONOMY(), scientific, common);

            FilterPtr taxFilter(new Filter());
            taxFilter->filterType.set(MS_DB_filter_taxonomy);
            if (!scientific.empty())
                taxFilter->include.set(MS_taxonomy__scientific_name,
                                       scientific);
            if (!common.empty())
                taxFilter->include.set(MS_taxonomy__common_name,
                                       common);

            sip->databaseFilters.push_back(taxFilter);
        }
    }

    void addSourceFile(ms_searchparams& p, IdentData& mzid)
    {
        SourceFilePtr sourceFile(new SourceFile());
        sourceFile->location = p.getFILENAME();
        if (p.getFORMAT() == "Mascot generic")
        {
            sourceFile->fileFormat = MS_Mascot_MGF_file;
        }
        mzid.dataCollection.inputs.sourceFile.push_back(sourceFile); 
    }

    void decryptMod(const string& mod, double mdelta,
                    ms_searchparams& p, IdentData& mzid)
    {
        SpectrumIdentificationProtocolPtr sip =
            getSpectrumIdentificationProtocol(mzid);

        cmatch what, where;
        if (regex_match(mod.c_str(), what, varmodPattern))
        {
            string first, second;
            first.assign(what[1].first, what[1].second);
            second.assign(what[2].first, what[2].second);
            
            const CVTermInfo* cvt = getTermInfoByName(first);
            
            SearchModificationPtr sm(new SearchModification());
            sm->fixedMod = false;
            if (mdelta)
                sm->massDelta = mdelta;
            if (cvt)
            {
                sm->cvParams.push_back(CVParam(cvt->cvid));;
                if (regex_match(second.c_str(), where, varmodListOfChars))
                    sm->residues.assign(where[1].first,
                                                 where[1].second);
                
            }
            else
            {
                // Just drop it into the bit bucket - UserParam has
                // beed removed.
                
                // Not legal, but necessary
                //sm->cvParams.userParams.
                //    push_back(UserParam("unknown_mod", what[0].first));
                if (regex_match(second.c_str(), where, varmodListOfChars))
                    sm->residues.assign(where[1].first,
                                                 where[1].second);
            }
            sip->modificationParams.push_back(sm);
        }
    }
    
    void addModifications(ms_searchparams& p, IdentData& mzid)
    {
        vector<string> mods;

        SpectrumIdentificationProtocolPtr sip =
            getSpectrumIdentificationProtocol(mzid);

        // Adding variable modifications
        int i=1;
        while (p.getVarModsName(i).length())
        {
            // Variable mod name
            string mod = p.getVarModsName(i);
            // Variable mod delta
            double mdelta = p.getVarModsDelta(i);
            // Variable mod neutral
            //double neutral = p.getVarModsNeutralLoss(i);
            i++;

            decryptMod(mod, mdelta, p, mzid);
        }

        // Add fixed modifications
        typedef boost::tokenizer< boost::char_separator<char> > tokenizer;
        boost::char_separator<char> sep(",");
        tokenizer tokens(p.getMODS(), sep);
        for (tokenizer::iterator tok_iter = tokens.begin();
             tok_iter != tokens.end(); ++tok_iter)
            decryptMod(*tok_iter, 0, p, mzid);
        
    }

    void searchInformation(ms_mascotresfile & file, IdentData& mzid)
    {
        time_t t = (time_t)file.getDate();
        struct tm * t1 = localtime(&t);

        mzid.creationDate = asctime(t1);
    }

    /**
     * Handles all the input parameters.
     */
    void searchParameters(ms_mascotresfile & file, IdentData& mzid)
    {
        // TODO What was this here for?
        //ms_searchparams& p = file.params();

        addUser(file.params(), mzid);
        addMassTable(file.params(), mzid);
        addAnalysisProtocol(file, mzid);
        addSourceFile(file.params(), mzid);
        addModifications(file.params(), mzid);
    }

    void getModifications(PeptidePtr peptide, ms_searchparams& searchparam,
                          ms_peptide* pep)
    {
        string mods = pep->getVarModsStr();

        for (size_t i=0; i<mods.size(); i++)
        {
            int mod_idx=0;
            
            char m=mods.at(i);

            // Find out if there's a modification
            if (m>='A' && m<='Z')
                mod_idx = 10 + ((int)m-'A');
            else if (m>='0' && m<='9')
                mod_idx = (int)m-'0';

            if (mod_idx == 0)
                continue;

            if (searchparam.getVarModsDelta(mod_idx) == 0)
                continue;
            
            // Find the modification and add it.
            ModificationPtr modification(new Modification());
            modification->location = i;
            modification->monoisotopicMassDelta =
                searchparam.getVarModsDelta(mod_idx);

            // Find the varmod in cvTermInfo's
            string varMods = searchparam.getVarModsName(mod_idx);

            cmatch what, where;
            if (regex_match(varMods.c_str(), what, varmodPattern))
            {
                string first, second;
                first.assign(what[1].first, what[1].second);
                second.assign(what[2].first, what[2].second);

                const CVTermInfo* cvt = getTermInfoByName(first);
                
                if (cvt)
                {
                    modification->set(cvt->cvid);
                    if (regex_match(second.c_str(), where, varmodListOfChars))
                        modification->residues.assign(where[1].first,
                                                      where[1].second);
                }
                else
                {
                    // Not legal in the schema, but necessary for now
                    
                    // TODO create a laundry list of known Mascot mod
                    // names and patterns
                    modification->userParams.
                    push_back(UserParam("unknown_mod", what[0].first));
                    if (regex_match(second.c_str(), where, varmodListOfChars))
                        modification->residues.assign(where[1].first,
                                                      where[1].second);
                }
            }
            if (!modification->empty())
                peptide->modification.push_back(modification);
        }
    }
    
    SpectrumIdentificationListPtr getSpectrumIdentificationList(IdentData& mzid)
    {
        if (mzid.dataCollection.analysisData.spectrumIdentificationList.size()==0)
            mzid.dataCollection.analysisData.spectrumIdentificationList.
                push_back(SpectrumIdentificationListPtr(
                              new SpectrumIdentificationList()));
        
        return mzid.dataCollection.analysisData.
            spectrumIdentificationList.back();
    }

    SpectrumIdentificationResultPtr getSpectrumIdentificationResult(IdentData& mzid)
    {
        SpectrumIdentificationListPtr sil = getSpectrumIdentificationList(mzid);

        if (sil->spectrumIdentificationResult.size() == 0)
            sil->spectrumIdentificationResult.push_back(
                SpectrumIdentificationResultPtr(new SpectrumIdentificationResult()));

        return sil->spectrumIdentificationResult.back();
    }

    void createProtnPep(ms_protein * prot,
                        ms_mascotresults& r, ms_searchparams& searchparam,
                        IdentData& mzid)
    {
        SpectrumIdentificationResultPtr sir = getSpectrumIdentificationResult(mzid);

        // Creating a protein detection hypothesis
        if (!mzid.dataCollection.analysisData.proteinDetectionListPtr.get())
            mzid.dataCollection.analysisData.proteinDetectionListPtr =
                ProteinDetectionListPtr(new ProteinDetectionList());

        ProteinDetectionHypothesisPtr pdh(
            new ProteinDetectionHypothesis(
                indices.makeIndex("PDH_", indices.proteindetectionhypothesis)));

        pdh->set(MS_Mascot_score, prot->getScore());
        pdh->set(MS_sequence_coverage, prot->getCoverage());

        ProteinAmbiguityGroupPtr pag(
            new ProteinAmbiguityGroup(
                indices.makeIndex("PAG_", indices.proteinambiguitygroup)));
        pag->proteinDetectionHypothesis.push_back(pdh);

        mzid.dataCollection.analysisData.proteinDetectionListPtr->
            proteinAmbiguityGroup.push_back(pag);

        // Each protein has a number of peptides that matched
        // - list them:
        for (int i=1; i <= prot->getNumPeptides(); i++)
        {
            SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());
            sir->spectrumIdentificationItem.push_back(sii);
            
            int query = prot->getPeptideQuery(i);
            int p     = prot->getPeptideP(i);
            
            if (p == -1 ||
                query == -1 ||
                prot->getPeptideDuplicate(i) == ms_protein::DUPE_DuplicateSameQuery)
                continue;

            ms_peptide * pep;
            if (r.getPeptide(query, p, pep))
            {
                int q = pep->getQuery();

                // Setup the Peptide tag. If it doesn't exist yet, add
                // it to the SequenceCollection
                sii->chargeState = pep->getCharge();
                sii->experimentalMassToCharge = pep->getObserved();
                sii->calculatedMassToCharge = pep->getMrCalc() / pep->getCharge();
                sii->rank = pep->getRank();
                sii->passThreshold = pep->getAnyMatch();

                PeptidePtr peptide;
                sequence_p targetseq(pep->getPeptideStr());
                vector<PeptidePtr>::iterator pepFound = find_if(
                    mzid.sequenceCollection.peptides.begin(),
                    mzid.sequenceCollection.peptides.end(),
                    targetseq);
                if (pepFound == mzid.sequenceCollection.peptides.end())
                {
                    // Add the peptide to both the sequence collection and the
                    // searchIdentificationItem's Peptide_ref
                    peptide = PeptidePtr(
                        new Peptide(indices.makeIndex("PEPTIDE_",
                                                      indices.peptide)));
                    peptide->peptideSequence = pep->getPeptideStr();
                    getModifications(peptide, searchparam, pep);
                    mzid.sequenceCollection.peptides.push_back(peptide);
                }
                else
                    peptide = *pepFound;
                
                sii->peptidePtr = peptide;

                // If the DBSequence isn't there either, add it now.
                DBSequencePtr dbseq;
                dbsequence_p targetdbs(pep->getPeptideStr(), prot->getAccession());
                vector<DBSequencePtr>::iterator dbseqFound = find_if(
                    mzid.sequenceCollection.dbSequences.begin(),
                    mzid.sequenceCollection.dbSequences.end(),
                    targetdbs);
                if (dbseqFound == mzid.sequenceCollection.dbSequences.end())
                {
                    dbseq = DBSequencePtr(
                        new DBSequence(
                            indices.makeIndex("DBSEQ_", indices.dbsequence)));
                    dbseq->seq = pep->getPeptideStr();
                    dbseq->length = dbseq->seq.size();
                    dbseq->accession = prot->getAccession();
                    dbseq->set(
                        MS_protein_description,
                        r.getProteinDescription(dbseq->accession.c_str()));
                    mzid.sequenceCollection.dbSequences.push_back(dbseq);
                }
                else
                {
                    dbseq = *dbseqFound;
                }

                // Create a PeptideEvidence object.
                PeptideEvidencePtr pe(new PeptideEvidence(
                                          indices.makeIndex("PEPTIDE_",
                                                            indices.peptide)));
                pe->dbSequencePtr = dbseq;
                //pe->missedCleavages = pep->getMissedCleavages();

                if (r.getTagStart(q,i,1)>0)
                {
                    pe->start = r.getTagStart(q, i, 1);
                    pe->end = r.getTagEnd(q, i, 1);
                }

                sii->peptideEvidencePtr.push_back(pe);
                PeptideHypothesis ph;
                ph.peptideEvidencePtr = pe;
                pdh->peptideHypothesis.push_back(ph);
                
                // TODO finish.
                
        // TODO finish the unsorted peptide values

                // Assumed not recorded in mzid
                //pep->getDelta();
        pep->getNumIonsMatched();

        // Many are 0. So, what's happening? Also, is there a list of
        // ions for non-0?

        // We seem to have dealt w/ it before
        pep->getVarModsStr();
        
        r.getTagDeltaRangeStart(q, 1);
        r.getTagDeltaRangeEnd(q, 1);
            
        pep->getIonsScore() ;
        pep->getSeriesUsedStr() ;
        pep->getPeaksUsedFromIons2() ;
        pep->getPeaksUsedFromIons3() ;
        
            }
        }

    }
    
    void proteinSummary(ms_mascotresfile& file, IdentData& mzid)
    {
        ms_proteinsummary results(file);
        ms_searchparams searchparam(file);

        int hit = file.getNumHits();
        for (int j=1; j<hit; j++)
        {
            ms_protein * prot = results.getHit(j);
            
            if (!prot)
                continue;
            
            std::string accession = prot->getAccession();
            std::string description = results.
                getProteinDescription(accession.c_str());
            // TODO Where do these go?
            //double mass = results.getProteinMass(accession.c_str());

            //double score = prot->getScore();
            //int frame = prot->getFrame();
            //long coverage = prot->getCoverage();
            //int numdisplay = prot->getNumDisplayPeptides();

            createProtnPep (prot, results, searchparam,  mzid);
        }
    }

    void inputData(ms_mascotresfile & file, IdentData& mzid)
    {
        
        //display input data via inputquery get functions

        SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());
        
        ms_inputquery q(file, 1);
        
        vector<MeasurePtr> fragmentationTable;
        fillFragmentationTable(fragmentationTable);
        
        for (int j=0; j<q.getNumVals(); j++)
        {
            IonTypePtr ionType(new IonType());
            if (fillFragmentation(q.getPeakList(j), fragmentationTable,
                                  ionType))
                sii->fragmentation.push_back(ionType);
        }
    }


    const CVTermInfo* getTermInfoByName(const string& name)
    {
        if (unimods.empty())
            generateUNIMOD(unimods);

        terminfo_name_p tn_p(name);

        typedef vector<const CVTermInfo*>::const_iterator terminfo_cit;
        terminfo_cit ci= find_if(unimods.begin(), unimods.end(), tn_p);

        if (ci != unimods.end())
            return *ci;

        return NULL;
    }
    
    void generateUNIMOD(vector<const CVTermInfo*> &cvInfos)
    {
        typedef vector<CVID>::const_iterator cvid_iterator;
        
        for(cvid_iterator ci=cvids().begin(); ci!=cvids().end(); ci++)
        {
            if (cvIsA(*ci, UNIMOD_unimod_root_node))
                cvInfos.push_back(&(cvTermInfo(*ci)));
        }
    }
    
private:
    Indices indices;
    vector<const CVTermInfo*> unimods;
    
    regex varmodPattern;
    regex varmodListOfChars;
    
    static const char* owner_person_id;
    static const char* provider_id;
    static const char* mz_id;
    static const char* intensity_id;
    static const char* error_id;
};

const char* MascotReader::Impl::owner_person_id = "doc_owner_person";
const char* MascotReader::Impl::provider_id = "provider";
const char* MascotReader::Impl::mz_id = "mz_id";
const char* MascotReader::Impl::intensity_id = "intensity_id";
const char* MascotReader::Impl::error_id = "error_id";

//
// MascotReader::MascotReader
//
MascotReader::MascotReader()
    : pimpl(new MascotReader::Impl())
{
}

//
// MascotReader::identify
//
string MascotReader::identify(const string& filename,
                              const string& head) const
{
    ms_datfile file(filename.c_str());
    if (file.isValid())
        return "Mascot";

    return "";
}

//
// MascotReader::read
//
void MascotReader::read(const string& filename,
                        const string& head,
                        IdentData& result,
                        const Reader::Config& config) const
{
    pimpl->read(filename, head, result);
}

//
// MascotReader::read
//
void MascotReader::read(const string& filename,
                        const string& head,
                        IdentDataPtr& result,
                        const Reader::Config& config) const
{
    if (result.get())
        read(filename, head, *result, config);
}

//
// MascotReader::read
//
void MascotReader::read(const string& filename,
                        const string& head,
                        vector<IdentDataPtr>& results,
                        const Reader::Config& config) const
{
    results.push_back(IdentDataPtr(new IdentData));
    read(filename, head, results.back(), config);
}

//
// MascotReader::getType
//
const char *MascotReader::getType() const
{
    return "mzIdentML";
}

} // namespace pwiz 
} // namespace identdata 

