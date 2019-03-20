//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _SEARCHSPECTRUM_H
#define _SEARCHSPECTRUM_H

#include "shared_defs.h"
#include "shared_funcs.h"
#include "BaseSpectrum.h"
#include "Profiler.h"
#include "SearchResultSet.h"
#include "SimpleXMLWriter.h"
#include "proteinStore.h"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include <iomanip>

using std::setw;
using std::left;

namespace freicore
{
    struct ComparisonStatistics
    {

        ComparisonStatistics()
            : numTargetUnmodComparisons(0), numDecoyUnmodComparisons(0), 
              numTargetModComparisons(0), numDecoyModComparisons(0)
        {}

        ComparisonStatistics(const ComparisonStatistics& old)
            : numTargetUnmodComparisons( old.numTargetUnmodComparisons),
              numDecoyUnmodComparisons( old.numDecoyUnmodComparisons ),
              numTargetModComparisons( old.numTargetModComparisons ),
              numDecoyModComparisons( old.numDecoyModComparisons )
        {}

        ComparisonStatistics operator+ (const ComparisonStatistics& rhs)
        {
            ComparisonStatistics total;
            total.numTargetUnmodComparisons = numTargetUnmodComparisons + rhs.numTargetUnmodComparisons;
            total.numDecoyUnmodComparisons = numDecoyUnmodComparisons + rhs.numDecoyUnmodComparisons;
            total.numTargetModComparisons = numTargetModComparisons + rhs.numTargetModComparisons;
            total.numDecoyModComparisons = numDecoyModComparisons + rhs.numDecoyModComparisons;
            return total;
        }

        bool isValidEntry()
        {
            size_t totalComps;
            totalComps += numTargetUnmodComparisons;
            totalComps += numDecoyUnmodComparisons;
            totalComps += numTargetModComparisons;
            totalComps += numDecoyModComparisons;
            return (totalComps > 0);
        }
        
        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & numTargetUnmodComparisons & numDecoyUnmodComparisons;
            ar & numTargetModComparisons & numDecoyModComparisons;
        }

        size_t numTargetUnmodComparisons;
        size_t numDecoyUnmodComparisons;
        size_t numTargetModComparisons;
        size_t numDecoyModComparisons;
    };

    template< class SearchResultT >
    struct SearchSpectrum : public virtual BaseSpectrum
    {
        typedef SearchResultT                        SearchResultType;
        typedef boost::shared_ptr<SearchResultT>    SearchResultPtr;
        typedef SearchResultSet< SearchResultT >    SearchResultSetType;

        SearchSpectrum()
            :   BaseSpectrum(), decoyState(0),
                numTargetComparisons(0), numDecoyComparisons(0)
        {}

        SearchSpectrum( const SearchSpectrum& old )
            :    BaseSpectrum( old ), resultsByCharge( old.resultsByCharge ), decoyState(0),
                numTargetComparisons( old.numTargetComparisons ),
                numDecoyComparisons( old.numDecoyComparisons ),
                detailedCompStats ( old.detailedCompStats )
        {}

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & fragmentTypes;
            ar & resultsByCharge;
            ar & numTargetComparisons & numDecoyComparisons;
            ar & detailedCompStats;
        }

        FragmentTypesBitset fragmentTypes;
        vector<SearchResultSetType> resultsByCharge;

        char decoyState;
        char numTerminiCleavages; // 0, 1, or 2 termini
        int numTargetComparisons; // number of comparisons to a target sequence
        int numDecoyComparisons;  // number of comparisons to a decoy sequence
        ComparisonStatistics detailedCompStats;
    };

    template< class SpectrumType >
    struct SearchSpectraListSortByTotalScore
    {
        bool operator() ( const SpectrumType* lhs, const SpectrumType* rhs )
        {
            double lhsScore = lhs->resultSet.getBestTotalScore();
            double rhsScore = rhs->resultSet.getBestTotalScore();

            if( lhsScore == rhsScore )
            {
                return spectraSortByID()( lhs, rhs );
            }

            return lhsScore > rhsScore;
        }
    };

    template< class SpectrumType, class SpectraListType >
    struct SearchSpectraList : public virtual BaseSpectraList< SpectrumType, SpectraListType >
    {
        bool spectraDecoyStatesSet;

        SearchSpectraList()
            :    BaseSpectraList< SpectrumType, SpectraListType >(), spectraDecoyStatesSet(false)
        {}

//        typedef SearchSpectraList< SpectrumType, ListType >            ListType;
        typedef BaseSpectraList< SpectrumType, SpectraListType >    SearchBaseList;
        typedef typename SearchBaseList::ListConstIterator            ListConstIterator;
        typedef typename SearchBaseList::ListIterator                ListIterator;

        void write(    const string& sourceFilepath,
                    pwiz::identdata::IdentDataFile::Format outputFormat,
                    const string& filenameSuffix,
                    const string& searchEngineName,
                    const string& searchEngineVersion,
                    const string& searchEngineURI,
                    const string& searchDatabase,
                    CVID cleavageAgent,
                    const string& cleavageAgentRegex,
                    const string& decoyPrefix,
                    const RunTimeVariableMap& vars ) const
        {
            using namespace pwiz::identdata;
            namespace msdata = pwiz::msdata;
            namespace proteome = pwiz::proteome;

            IdentData mzid;

            mzid.id = sourceFilepath + " " + searchDatabase + " " + searchEngineName + " " + searchEngineVersion;
            mzid.creationDate = GetDateTime();

            // add default CVs
            mzid.cvs = defaultCVList();

            // add the SpectrumIdentificationProtocol
            SpectrumIdentificationProtocolPtr sipPtr(new SpectrumIdentificationProtocol("SIP"));
            mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sipPtr);

            CVTranslator cvTranslator;
            CVID searchEngineCVID = cvTranslator.translate(searchEngineName);

            // add analysis software
            sipPtr->analysisSoftwarePtr.reset(new AnalysisSoftware("AS"));
            mzid.analysisSoftwareList.push_back(sipPtr->analysisSoftwarePtr);

            // set software name
            if (searchEngineCVID != CVID_Unknown)
                sipPtr->analysisSoftwarePtr->softwareName.set(searchEngineCVID);
            else
                sipPtr->analysisSoftwarePtr->softwareName.set(MS_custom_unreleased_software_tool, searchEngineName);

            // set version and URI
            sipPtr->analysisSoftwarePtr->version = searchEngineVersion;
            sipPtr->analysisSoftwarePtr->URI = searchEngineURI;

            // set search type
            sipPtr->searchType.cvid = MS_ms_ms_search;

            // add a mass table for all MS levels
            MassTablePtr massTable(new MassTable("MT"));
            massTable->msLevel.push_back(1);
            massTable->msLevel.push_back(2);
            massTable->msLevel.push_back(3);
            sipPtr->massTable.push_back(massTable);

            // specify amino acid masses used
            const char* residueSymbols = "ACDEFGHIKLMNPQRSTUVWY";
            for (int i=0; i < 21; ++i)
            {
                const AminoAcid::Info::Record& record = AminoAcid::Info::record(residueSymbols[i]);       
                ResiduePtr rp(new Residue);
                rp->code = record.symbol;
                rp->mass = record.residueFormula.monoisotopicMass();
                massTable->residues.push_back(rp);
            }

            // add the SpectrumIdentificationList
            SpectrumIdentificationListPtr silPtr(new SpectrumIdentificationList("SIL"));
            mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silPtr);

            if (vars.count("SearchStats: Overall"))
            {
                string searchStats = vars.find("SearchStats: Overall")->second;
                silPtr->numSequencesSearched = lexical_cast<int>(searchStats.substr(0, searchStats.find_first_of(' ')));
            }

            // add the SpectrumIdentification
            SpectrumIdentificationPtr siPtr(new SpectrumIdentification("SI"));
            siPtr->spectrumIdentificationListPtr = silPtr;
            siPtr->spectrumIdentificationProtocolPtr = sipPtr;
            siPtr->activityDate = mzid.creationDate;
            mzid.analysisCollection.spectrumIdentification.push_back(siPtr);

            // add search database
            SearchDatabasePtr sdb(new SearchDatabase("SDB"));
            sdb->fileFormat.cvid = MS_FASTA_format;
            sdb->location = searchDatabase;
            sdb->name = bfs::path(searchDatabase).filename().string();
            sdb->set(MS_database_type_amino_acid);
            sdb->databaseName.userParams.push_back(UserParam("database name", sdb->name, "xsd:string"));
            mzid.dataCollection.inputs.searchDatabase.push_back(sdb);
            mzid.analysisCollection.spectrumIdentification[0]->searchDatabase.push_back(sdb);

            // add source file
            SpectraDataPtr spectraData(new SpectraData("SD"));
            spectraData->location = sourceFilepath;
            spectraData->name = bfs::path(spectraData->location).filename().string();
            mzid.dataCollection.inputs.spectraData.push_back(spectraData);
            mzid.analysisCollection.spectrumIdentification[0]->inputSpectra.push_back(spectraData);

            // set source file format (required for a semantically valid mzIdentML file)
            msdata::FullReaderList readerList;
            auto readerPtr = readerList.identifyAsReader(sourceFilepath);
            if (readerPtr)
                spectraData->fileFormat.cvid = readerPtr->getCvType();
            else if (outputFormat == IdentDataFile::Format_MzIdentML)
                throw runtime_error("[SearchSpectraList::write] unable to determine source file format of \"" + sourceFilepath + "\"");

            {
                msdata::MSDataFile msd(sourceFilepath, &readerList);
                spectraData->spectrumIDFormat.cvid = msdata::id::getDefaultNativeIDFormat(msd);
            }

            // add the cleavage rules
            EnzymePtr enzyme(new Enzyme);
            enzyme->id = "ENZ_" + lexical_cast<string>(sipPtr->enzymes.enzymes.size()+1);
            enzyme->terminalSpecificity = (proteome::Digestion::Specificity) lexical_cast<int>(vars.find("Config: MinTerminiCleavages")->second);
            enzyme->nTermGain = "H";
            enzyme->cTermGain = "OH";
            enzyme->missedCleavages = lexical_cast<int>(vars.find("Config: MaxMissedCleavages")->second);
            enzyme->minDistance = 1;

            if (!cleavageAgentRegex.empty())
                enzyme->siteRegexp = cleavageAgentRegex;

            if (cleavageAgent == CVID_Unknown)
                cleavageAgent = proteome::Digestion::getCleavageAgentByRegex(enzyme->siteRegexp);

            if (cleavageAgent != CVID_Unknown)
                enzyme->enzymeName.set(cleavageAgent);

            sipPtr->enzymes.enzymes.push_back(enzyme);


            // use monoisotopic mass unless PrecursorMzToleranceRule forces average
            bool forceAverageMass = vars.find("Config: PrecursorMzToleranceRule")->second == "avg";

            if (forceAverageMass)
                sipPtr->additionalSearchParams.set(MS_parent_mass_type_average);
            else
                sipPtr->additionalSearchParams.set(MS_parent_mass_type_mono);

            sipPtr->additionalSearchParams.set(MS_fragment_mass_type_mono);

            MZTolerance precursorMzTolerance;
            string precursorMassType = forceAverageMass ? "Avg" : "Mono";
            parse(precursorMzTolerance, vars.find("Config: " + precursorMassType + "PrecursorMzTolerance")->second);
            sipPtr->parentTolerance.set(MS_search_tolerance_minus_value, precursorMzTolerance.value);
            sipPtr->parentTolerance.set(MS_search_tolerance_plus_value, precursorMzTolerance.value);
            sipPtr->parentTolerance.cvParams[0].units = sipPtr->parentTolerance.cvParams[1].units =
                precursorMzTolerance.units == MZTolerance::PPM ? UO_parts_per_million : UO_dalton;

            MZTolerance fragmentMzTolerance;
            parse(fragmentMzTolerance, vars.find("Config: FragmentMzTolerance")->second);
            sipPtr->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentMzTolerance.value);
            sipPtr->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentMzTolerance.value);
            sipPtr->fragmentTolerance.cvParams[0].units = sipPtr->fragmentTolerance.cvParams[1].units =
                fragmentMzTolerance.units == MZTolerance::PPM ? UO_parts_per_million : UO_dalton;

            sipPtr->threshold.set(MS_no_threshold);

            string fragmentationRule = vars.find("Config: FragmentationRule")->second;
            if (bal::icontains(fragmentationRule, "cid"))     translateIonSeriesConsidered(*sipPtr, "b,y");
            if (bal::icontains(fragmentationRule, "etd"))     translateIonSeriesConsidered(*sipPtr, "c,z+1");
            if (bal::icontains(fragmentationRule, "manual"))  translateIonSeriesConsidered(*sipPtr, fragmentationRule.substr(7)); // skip "manual:"


            DynamicModSet dynamicMods( vars.find("Config: DynamicMods")->second );
            BOOST_FOREACH(const DynamicMod& mod, dynamicMods)
            {
                SearchModificationPtr searchModification(new SearchModification);

                switch( mod.unmodChar )
                {
                    case PEPTIDE_N_TERMINUS_SYMBOL:
                        searchModification->massDelta = mod.modMass;
                        searchModification->fixedMod = false;
                        searchModification->specificityRules.cvid = MS_modification_specificity_peptide_N_term;
                        break;

                    case PEPTIDE_C_TERMINUS_SYMBOL:
                        searchModification->massDelta = mod.modMass;
                        searchModification->fixedMod = false;
                        searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;
                        break;

                    default:
                    {
                        string specificity; // either empty, n, or c, but not both

                        if (mod.NTerminalFilters.size() == 1 &&
                            mod.NTerminalFilters[0].m_filter[PEPTIDE_N_TERMINUS_SYMBOL])
                            specificity += 'n';
                        else if (mod.CTerminalFilters.size() == 1 &&
                                 mod.CTerminalFilters[0].m_filter[PEPTIDE_C_TERMINUS_SYMBOL])
                        specificity += 'c';

                        searchModification->massDelta = mod.modMass;
                        searchModification->residues.push_back(mod.unmodChar);
                        searchModification->fixedMod = false;

                        if (specificity == "n")
                            searchModification->specificityRules.cvid = MS_modification_specificity_peptide_N_term;
                        else if (specificity == "c")
                            searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;
                        break;
                    }
                }
                sipPtr->modificationParams.push_back(searchModification);
            }

            StaticModSet staticMods( vars.find("Config: StaticMods")->second );
            BOOST_FOREACH(const StaticMod& mod, staticMods)
            {
                SearchModificationPtr searchModification(new SearchModification);
                switch( mod.name )
                {
                    case PEPTIDE_N_TERMINUS_SYMBOL:
                        searchModification->massDelta = mod.mass;
                        searchModification->fixedMod = true;
                        searchModification->specificityRules.cvid = MS_modification_specificity_peptide_N_term;
                        break;

                    case PEPTIDE_C_TERMINUS_SYMBOL:
                        searchModification->massDelta = mod.mass;
                        searchModification->fixedMod = true;
                        searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;
                        break;

                    default:
                        searchModification->massDelta = mod.mass;
                        searchModification->residues.push_back(mod.name);
                        searchModification->fixedMod = true;
                        break;
                }
                sipPtr->modificationParams.push_back(searchModification);
            }

            BOOST_FOREACH(const RunTimeVariableMap::value_type& itr, vars)
                sipPtr->additionalSearchParams.userParams.push_back(UserParam(itr.first, itr.second));

            map<string, DBSequencePtr> dbSequences;
            PeptideIndex peptides;
            PeptidePtr currentPeptide;

            size_t spectrumIndex = 0;
            set<string> uniqueNativeIDs;
            SpectrumIdentificationResultPtr sirPtr;
            int sumTargetComparisons = 0, sumDecoyComparisons = 0;

            BOOST_FOREACH(SpectrumType* s, *this)
            {
                size_t totalResults = 0;
                for (int z=0; z < (int) s->resultsByCharge.size(); ++z)
                    totalResults += s->resultsByCharge[z].size();

                // empty SpectrumIdentificationResults are not allowed
                if (totalResults == 0)
                    continue;

                // HACK: until TagRecon is changed so it doesn't duplicate spectra for multiple charge states,
                // we have to consolidate the charge states when creating the mzIdentML document
                pair<set<string>::iterator, bool> insertResult = uniqueNativeIDs.insert(s->nativeID);
                if (insertResult.second)
                {
                    if (sirPtr.get())
                    {
                        sirPtr->userParams.push_back(UserParam("num_target_comparisons", lexical_cast<string>(sumTargetComparisons)));
                        sirPtr->userParams.push_back(UserParam("num_decoy_comparisons", lexical_cast<string>(sumDecoyComparisons)));
                        sumTargetComparisons = s->numTargetComparisons;
                        sumDecoyComparisons = s->numDecoyComparisons;
                    }

                    sirPtr.reset(new SpectrumIdentificationResult);
                    silPtr->spectrumIdentificationResult.push_back(sirPtr);
    
                    sirPtr->id = "SIR_" + lexical_cast<string>(++spectrumIndex);
                    sirPtr->spectrumID = s->nativeID;
                    sirPtr->spectraDataPtr = spectraData;
                    sirPtr->set(MS_scan_start_time, s->retentionTime, UO_minute);
                    if (!s->title.empty()) sirPtr->set(MS_spectrum_title, s->title);
                }
                else
                {
                    sumTargetComparisons += s->numTargetComparisons;
                    sumDecoyComparisons += s->numDecoyComparisons;
                }

                SpectrumIdentificationResult& sir = *sirPtr;
                
                /*xmlWriter.attr( "target_unmod_comps", s->detailedCompStats.numTargetUnmodComparisons );
                xmlWriter.attr( "target_mod_comps", s->detailedCompStats.numTargetModComparisons );
                xmlWriter.attr( "decoy_unmod_comps", s->detailedCompStats.numDecoyUnmodComparisons );
                xmlWriter.attr( "decoy_mod_comps", s->detailedCompStats.numDecoyModComparisons );*/

                size_t resultIndex = 0;

                for (int z=0; z < (int) s->resultsByCharge.size(); ++z)
                {
                    typedef typename SpectrumType::SearchResultType SearchResultType;
                    typedef typename SpectrumType::SearchResultSetType SearchResultSetType;
                    typedef typename SearchResultSetType::RankMap RankMap;

                    SearchResultSetType& resultSet = s->resultsByCharge[z];

                    if (resultSet.empty())
                        continue;

                    RankMap resultsByRank = resultSet.byRankAndCategory();

                    // first=rank, second=vector of tied results
                    BOOST_FOREACH(typename RankMap::value_type& rank, resultsByRank)
                    BOOST_FOREACH(const boost::shared_ptr<SearchResultType>& resultPtr, rank.second)
                    {
                        SpectrumIdentificationItemPtr siiPtr(new SpectrumIdentificationItem);
                        sir.spectrumIdentificationItem.push_back(siiPtr);

                        SpectrumIdentificationItem& sii = *siiPtr;
                        const SearchResultType& result = *resultPtr;

                        sii.id = sir.id + "_SII_" + lexical_cast<string>(++resultIndex);
                        sii.rank = rank.first;
                        sii.chargeState = result.precursorMassHypothesis.charge;
                        sii.experimentalMassToCharge = Ion::mz(result.precursorMassHypothesis.mass, sii.chargeState);
                        sii.calculatedMassToCharge = Ion::mz(result.calculatedMass(), sii.chargeState);
                        sii.massTablePtr = massTable;

                        currentPeptide.reset(new pwiz::identdata::Peptide);
                        currentPeptide->peptideSequence = result.sequence();

                        sii.set(MS_number_of_matched_peaks, result.fragmentsMatched);
                        sii.set(MS_number_of_unmatched_peaks, result.fragmentsUnmatched);

                        const ModificationMap& modMap = result.modifications();
                        ModificationMap::const_iterator modItr;
                        for (modItr = modMap.begin(); modItr != modMap.end(); ++modItr)
                        {
                            const ModificationMap::value_type& mapPair = *modItr;
                            BOOST_FOREACH(const pwiz::proteome::Modification& mod, mapPair.second)
                            {
                                ModificationPtr resultMod(new pwiz::identdata::Modification);
                                currentPeptide->modification.push_back(resultMod);

                                resultMod->avgMassDelta = mod.averageDeltaMass();
                                resultMod->monoisotopicMassDelta = mod.monoisotopicDeltaMass();

                                switch (mapPair.first)
                                {
                                    case INT_MIN:
                                        resultMod->location = 0;
                                        break;
                                    case INT_MAX:
                                        resultMod->location = result.sequence().length() + 1;
                                        break;
                                    default:
                                        resultMod->residues.push_back(result.sequence()[mapPair.first]);
                                        resultMod->location = mapPair.first + 1;
                                        break;
                                }
                            }
                        }

                        // try to insert the current peptide variant
                        pair<typename PeptideIndex::iterator, bool> insertResult = peptides.insert(make_pair(currentPeptide, vector<PeptideEvidencePtr>()));
                        currentPeptide = insertResult.first->first;

                        // if peptide is new, add its proteins as DBSequences and
                        // populate the SII with PeptideEvidence elements
                        if (insertResult.second)
                        {
                            currentPeptide->id = "PEP_" + lexical_cast<string>(peptides.size());
                            mzid.sequenceCollection.peptides.push_back(currentPeptide);

                            BOOST_FOREACH(const string& accession, result.proteins)
                            {
                                // insert or find protein accession
                                pair<map<string, DBSequencePtr>::iterator, bool> insertResult2 = dbSequences.insert(make_pair(accession, DBSequencePtr()));

                                DBSequencePtr& dbSequence = insertResult2.first->second;

                                // if it was inserted, add it to the sequenceCollection
                                if (insertResult2.second)
                                {
                                    dbSequence.reset(new DBSequence);
                                    mzid.sequenceCollection.dbSequences.push_back(dbSequence);

                                    dbSequence->searchDatabasePtr = sdb;
                                    dbSequence->accession = accession;
                                    dbSequence->id = "DBSeq_" + accession;
                                }

                                PeptideEvidencePtr pe(new PeptideEvidence);
                                pe->dbSequencePtr = dbSequence;
                                pe->peptidePtr = currentPeptide;

                                // build a unique id for each PeptideEvidence (based on distinct peptide count)
                                //BOOST_FOREACH(PeptideEvidencePtr& pe, sii.peptideEvidence)
                                    pe->id = dbSequence->id + "_" + currentPeptide->id;

                                const string& prevAA = result.NTerminusPrefix();
                                const string& nextAA = result.CTerminusSuffix();
                                pe->pre = prevAA.empty() ? '-' : *prevAA.rbegin();
                                pe->post = nextAA.empty() ? '-' : *nextAA.begin();
                                pe->isDecoy = bal::starts_with(accession, decoyPrefix);
                                pe->start = result.offset() + 1;
                                pe->end = pe->start + result.sequence().length();

                                insertResult.first->second.push_back(pe);
                                mzid.sequenceCollection.peptideEvidence.push_back(pe);
                            }
                        }

                        sii.peptideEvidencePtr = insertResult.first->second;

                        // the peptide is guaranteed to exist, so reference it
                        sii.peptidePtr = currentPeptide;
                        sii.passThreshold = true;

                        // add search scores as either CVParams or UserParams
                        SearchScoreList scores = result.getScoreList();
                        BOOST_FOREACH(const SearchScore& score, scores)
                            if (score.cvid != CVID_Unknown)
                                sii.set(score.cvid, score.value);
                            else
                                sii.userParams.push_back(UserParam(score.name, lexical_cast<string>(score.value)));

                    } // for each tied result in a rank
                } // for each charge state
            } // for each spectrum

            // HACK: insert these userParams for the last SIR
            if (sirPtr.get())
            {
                sirPtr->userParams.push_back(UserParam("num_target_comparisons", lexical_cast<string>(sumTargetComparisons)));
                sirPtr->userParams.push_back(UserParam("num_decoy_comparisons", lexical_cast<string>(sumDecoyComparisons)));
            }

            snapModificationsToUnimod(*siPtr);

            string extension = outputFormat == IdentDataFile::Format_pepXML ? ".pepXML" : ".mzid";
            string outputFilename = bfs::path(sourceFilepath).replace_extension("").filename().string() + filenameSuffix + extension;

            IdentDataFile::write(mzid, outputFilename, IdentDataFile::WriteConfig(outputFormat));
        } // write()

        struct ModLessThan
        {
            bool operator() (const pwiz::identdata::ModificationPtr& lhsPtr, const pwiz::identdata::ModificationPtr& rhsPtr) const
            {
                const pwiz::identdata::Modification& lhs = *lhsPtr;
                const pwiz::identdata::Modification& rhs = *rhsPtr;

                return lhs.location == rhs.location ?
                       lhs.avgMassDelta == rhs.avgMassDelta ?
                       lhs.monoisotopicMassDelta == rhs.monoisotopicMassDelta ? false
                       : lhs.monoisotopicMassDelta < rhs.monoisotopicMassDelta
                       : lhs.avgMassDelta < rhs.avgMassDelta
                       : lhs.location < rhs.location;
            }
        };

        struct ModNotEquals
        {
            bool operator() (const pwiz::identdata::ModificationPtr& lhsPtr, const pwiz::identdata::ModificationPtr& rhsPtr) const
            {
                const pwiz::identdata::Modification& lhs = *lhsPtr;
                const pwiz::identdata::Modification& rhs = *rhsPtr;

                return lhs.location != rhs.location ||
                       lhs.avgMassDelta != rhs.avgMassDelta ||
                       lhs.monoisotopicMassDelta != rhs.monoisotopicMassDelta;
            }
        };

        struct PeptideLessThan
        {
            bool operator() (const pwiz::identdata::PeptidePtr& lhsPtr, const pwiz::identdata::PeptidePtr& rhsPtr) const
            {
                const pwiz::identdata::Peptide& lhs = *lhsPtr;
                const pwiz::identdata::Peptide& rhs = *rhsPtr;

                if (lhs.peptideSequence.length() == rhs.peptideSequence.length())
                {
                    int compare = lhs.peptideSequence.compare(rhs.peptideSequence);
                    if (!compare)
                    {
                        if (lhs.modification.size() != rhs.modification.size())
                            return lhs.modification.size() < rhs.modification.size();

                        ModNotEquals modNotEquals;
                        ModLessThan modLessThan;
                        for (size_t i=0; i < lhs.modification.size(); ++i)
                            if (modNotEquals(lhs.modification[i], rhs.modification[i]))
                                return modLessThan(lhs.modification[i], rhs.modification[i]);
                        return false;
                    }
                    return compare < 0;
                }
                else
                    return lhs.peptideSequence.length() < rhs.peptideSequence.length();
            }
        };

        // maps a modified peptide to its PeptideEvidences
        typedef map<pwiz::identdata::PeptidePtr, vector<pwiz::identdata::PeptideEvidencePtr>, PeptideLessThan> PeptideIndex;

        void translateIonSeriesConsidered(pwiz::identdata::SpectrumIdentificationProtocol& sip,
                                          const string& ionSeriesList) const
        {
            vector<string> tokens;
            bal::split(tokens, ionSeriesList, bal::is_any_of(","));
            BOOST_FOREACH(const string& ionSeries, tokens)
            {
                if (ionSeries == "immonium")        sip.additionalSearchParams.set(MS_param__immonium_ion);
                else if (ionSeries == "a")          sip.additionalSearchParams.set(MS_param__a_ion);
                else if (ionSeries == "a-NH3")      sip.additionalSearchParams.set(MS_param__a_ion_NH3_DEPRECATED);
                else if (ionSeries == "a-H2O")      sip.additionalSearchParams.set(MS_param__a_ion_H2O_DEPRECATED);
                else if (ionSeries == "b")          sip.additionalSearchParams.set(MS_param__b_ion);
                else if (ionSeries == "b-NH3")      sip.additionalSearchParams.set(MS_param__b_ion_NH3_DEPRECATED);
                else if (ionSeries == "b-H2O")      sip.additionalSearchParams.set(MS_param__b_ion_H2O_DEPRECATED);
                else if (ionSeries == "c")          sip.additionalSearchParams.set(MS_param__c_ion);
                //else if (ionSeries == "c-NH3")      sip.additionalSearchParams.set(MS_param__c_ion_NH3);
                //else if (ionSeries == "c-H2O")      sip.additionalSearchParams.set(MS_param__c_ion_H2O);
                else if (ionSeries == "x")          sip.additionalSearchParams.set(MS_param__x_ion);
                //else if (ionSeries == "x-NH3")      sip.additionalSearchParams.set(MS_param__x_ion_NH3);
                //else if (ionSeries == "x-H2O")      sip.additionalSearchParams.set(MS_param__x_ion_H2O);
                else if (ionSeries == "y")          sip.additionalSearchParams.set(MS_param__y_ion);
                else if (ionSeries == "y-NH3")      sip.additionalSearchParams.set(MS_param__y_ion_NH3_DEPRECATED);
                else if (ionSeries == "y-H2O")      sip.additionalSearchParams.set(MS_param__y_ion_H2O_DEPRECATED);
                else if (ionSeries == "z")          sip.additionalSearchParams.set(MS_param__z_ion);
                //else if (ionSeries == "z-NH3")      sip.additionalSearchParams.set(MS_param__z_ion_NH3);
                //else if (ionSeries == "z-H2O")      sip.additionalSearchParams.set(MS_param__z_ion_H2O);
                else if (ionSeries == "z+1" ||
                         ionSeries == "z*")         sip.additionalSearchParams.set(MS_param__z_1_ion);
                else if (ionSeries == "z+2")        sip.additionalSearchParams.set(MS_param__z_2_ion);
                else if (ionSeries == "d")          sip.additionalSearchParams.set(MS_param__d_ion);
                else if (ionSeries == "v")          sip.additionalSearchParams.set(MS_param__v_ion);
                else if (ionSeries == "w")          sip.additionalSearchParams.set(MS_param__w_ion);
            }
        }
    };
}

//BOOST_CLASS_IMPLEMENTATION( freicore::SearchSpectrum, boost::serialization::object_serializable );
//BOOST_CLASS_TRACKING( freicore::SearchSpectrum, boost::serialization::track_never )

#endif
