//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#include "Serializer_pepXML.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/data/common/CVTranslator.hpp"


namespace pwiz {
namespace mziddata {


using minimxml::XMLWriter;
using boost::iostreams::stream_offset;
using namespace pwiz::minimxml;
using namespace pwiz::chemistry;
using namespace pwiz::proteome;
using namespace pwiz::util;


void Serializer_pepXML::write(ostream& os, const MzIdentML& mzid,
                              const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    // instantiate XMLWriter

    /*XMLWriter::Config xmlConfig;
    XMLWriter xmlWriter(os, xmlConfig);

    string xmlData = "version=\"1.0\" encoding=\"ISO-8859-1\"";
    xmlWriter.processingInstruction("xml", xmlData);

    IO::write(xmlWriter, mzid);*/
}




namespace {


struct HandlerSampleEnzyme : public SAXParser::Handler
{
    SpectrumIdentificationProtocol* _sip;

    HandlerSampleEnzyme(const CVTranslator& cvTranslator) : _cvTranslator(cvTranslator) {}

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "sample_enzyme")
        {
            getAttribute(attributes, "name", _name);
            getAttribute(attributes, "description", _description);
            getAttribute(attributes, "fidelity", _fidelity);
            getAttribute(attributes, "independent", _sip->enzymes.independent);
            return Handler::Status::Ok;
        }
        else if (name == "specificity")
        {
            EnzymePtr enzyme = EnzymePtr(new Enzyme);
            enzyme->id = "ENZ_" + lexical_cast<string>(_sip->enzymes.enzymes.size());
            enzyme->semiSpecific = _fidelity == "semispecific" ? true : false;
            enzyme->nTermGain = "H";
            enzyme->cTermGain = "OH";

            string cut, noCut, sense;

            getAttribute(attributes, "cut", cut);
            getAttribute(attributes, "no_cut", noCut);
            bal::to_lower(getAttribute(attributes, "sense", sense));

            if (cut.empty())
                throw runtime_error("[HandlerSampleEnzyme] Empty cut attribute");

            if (sense == "n")
                std::swap(cut, noCut);
            else if (sense != "c")
                throw runtime_error("[HandlerSampleEnzyme] Invalid specificity sense: " + sense);

            enzyme->siteRegexp = string("(?<=") + (cut.length() > 1 ? "[" : "") + cut + (cut.length() > 1 ? "])" : ")") +
                                (noCut.empty() ? "" : "(?!") + (noCut.length() > 1 ? "[" : "") + noCut + (noCut.length() > 1 ? "])" : ")");

            string value;
            getAttribute(attributes, "min_spacing", value);
            if (!value.empty())
                enzyme->minDistance = lexical_cast<size_t>(value);

            // TODO: populate EnzymeName

            _sip->enzymes.enzymes.push_back(enzyme);
            return Handler::Status::Ok;
        }
        else
            throw runtime_error("[HandlerSampleEnzyme] Unexpected element name: " + name);
    }

    private:
    string _name, _description, _fidelity;
    const CVTranslator& _cvTranslator;
};

struct HandlerSearchSummary : public SAXParser::Handler
{
    MzIdentML* _mzid;
    SpectrumIdentificationProtocol* _sip;

    HandlerSearchSummary(const CVTranslator& cvTranslator) : _cvTranslator(cvTranslator) {}

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "search_summary")
        {
            string basename, searchEngine, precursorMassType, fragmentMassType;
            getAttribute(attributes, "base_name", basename);
            bal::to_lower(getAttribute(attributes, "search_engine", searchEngine));
            getAttribute(attributes, "precursor_mass_type", precursorMassType);
            getAttribute(attributes, "fragment_mass_type", fragmentMassType);

            // TODO: translate search engine name

            if (bal::istarts_with(precursorMassType, "mono"))
                _sip->additionalSearchParams.set(MS_parent_mass_type_mono);
            else if (bal::istarts_with(precursorMassType, "av"))
                _sip->additionalSearchParams.set(MS_parent_mass_type_average);
            else
                throw runtime_error("[HandlerSearchSummary] Invalid precursor_mass_type: " + precursorMassType);

            if (bal::istarts_with(fragmentMassType, "mono"))
                _sip->additionalSearchParams.set(MS_fragment_mass_type_mono);
            else if (bal::istarts_with(fragmentMassType, "av"))
                _sip->additionalSearchParams.set(MS_fragment_mass_type_average);
            else
                throw runtime_error("[HandlerSearchSummary] Invalid fragment_mass_type: " + fragmentMassType);
        }
        else if (name == "search_database")
        {
            SearchDatabasePtr searchDatabase = SearchDatabasePtr(new SearchDatabase);
            searchDatabase->fileFormat.set(MS_FASTA_format);

            string databaseReleaseIdentifier, type;
            getAttribute(attributes, "local_path", searchDatabase->location);
            getAttribute(attributes, "database_name", searchDatabase->id);
            getAttribute(attributes, "database_release_identifier", searchDatabase->version);
            getAttribute(attributes, "size_in_db_entries", searchDatabase->numDatabaseSequences);
            getAttribute(attributes, "size_of_residues", searchDatabase->numResidues);
            bal::to_lower(getAttribute(attributes, "type", type));

            if (searchDatabase->id.empty())
                searchDatabase->id = searchDatabase->location.empty() ? "DB_1" : bfs::path(searchDatabase->location).filename();

            if (type == "aa")
                searchDatabase->params.set(MS_database_type_amino_acid);
            else if (type == "na")
                searchDatabase->params.set(MS_database_type_nucleotide);
            else
                throw runtime_error("[HandlerSearchSummary] Invalid database type: " + type);

            _mzid->dataCollection.inputs.searchDatabase.push_back(searchDatabase);
            _mzid->analysisCollection.spectrumIdentification[0]->searchDatabase.push_back(searchDatabase);
        }
        else if (name == "enzymatic_search_constraint")
        {
            string enzyme, minTermini;
            getAttribute(attributes, "enzyme", enzyme);
            getAttribute(attributes, "max_num_internal_cleavages", _sip->enzymes.enzymes[0]->missedCleavages);
            getAttribute(attributes, "min_number_termini", minTermini);
            _sip->enzymes.enzymes[0]->semiSpecific = (minTermini == "1" ? true : false);
        }
        else if (name == "aminoacid_modification")
        {
            string aminoacid, variable, peptideTerminus;
            getAttribute(attributes, "aminoacid", aminoacid);
            bal::to_lower(getAttribute(attributes, "variable", variable));
            getAttribute(attributes, "peptide_terminus", peptideTerminus);

            // TODO: snap masses to unimod

            SearchModificationPtr searchModification = SearchModificationPtr(new SearchModification);
            getAttribute(attributes, "massdiff", searchModification->modParam.massDelta);
            searchModification->modParam.residues = aminoacid;
            searchModification->fixedMod = !(variable == "y" || lexical_cast<bool>(variable));

            if (bal::icontains(peptideTerminus, "n"))
                searchModification->specificityRules.set(MS_modification_specificity_N_term);
            if (bal::icontains(peptideTerminus, "c"))
                searchModification->specificityRules.set(MS_modification_specificity_C_term);

            _sip->modificationParams.push_back(searchModification);
        }
        else if (name == "terminal_modification")
        {
            string terminus, variable;
            getAttribute(attributes, "terminus", terminus);
            bal::to_lower(getAttribute(attributes, "variable", variable));
            
            SearchModificationPtr searchModification = SearchModificationPtr(new SearchModification);
            getAttribute(attributes, "massdiff", searchModification->modParam.massDelta);
            searchModification->fixedMod = !(variable == "y" || lexical_cast<bool>(variable));

            if (bal::icontains(terminus, "n"))
                searchModification->specificityRules.set(MS_modification_specificity_N_term);
            if (bal::icontains(terminus, "c"))
                searchModification->specificityRules.set(MS_modification_specificity_C_term);

            _sip->modificationParams.push_back(searchModification);
        }
        else if (name == "parameter")
        {
            // TODO: use cvTranslator? to map search engine specific parameters to mzIdentML elements and attributes
            // e.g. Mascot: TOL -> precursor m/z tolerance, ITOL -> fragment m/z tolerance

            string name, value;
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "value", value);
            _sip->additionalSearchParams.userParams.push_back(UserParam(name, value));
        }
        else
            throw runtime_error("[HandlerSearchSummary] Unexpected element name: " + name);

        return Handler::Status::Ok;
    }

    private:
    const CVTranslator& _cvTranslator;
};


struct ModLessThan
{
    bool operator() (const ModificationPtr& lhsPtr, const ModificationPtr& rhsPtr) const
    {
        const Modification& lhs = *lhsPtr;
        const Modification& rhs = *rhsPtr;

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
    bool operator() (const ModificationPtr& lhsPtr, const ModificationPtr& rhsPtr) const
    {
        const Modification& lhs = *lhsPtr;
        const Modification& rhs = *rhsPtr;

        return lhs.location != rhs.location ||
               lhs.avgMassDelta != rhs.avgMassDelta ||
               lhs.monoisotopicMassDelta != rhs.monoisotopicMassDelta;
    }
};

struct PeptideLessThan
{
    bool operator() (const PeptidePtr& lhsPtr, const PeptidePtr& rhsPtr) const
    {
        const Peptide& lhs = *lhsPtr;
        const Peptide& rhs = *rhsPtr;

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

struct HandlerSearchResults : public SAXParser::Handler
{
    MzIdentML* _mzid;
    SpectrumIdentificationProtocol* _sip;
    SpectrumIdentificationList* _sil;

    HandlerSearchResults(const CVTranslator& cvTranslator,
                         const IterationListenerRegistry* iterationListenerRegistry)
    :   _nTerm("H1"),
        _cTerm("O1H1"),
        _cvTranslator(cvTranslator),
        ilr(iterationListenerRegistry),
        siiCount(0) {}

    DBSequencePtr getDBSequence(const string& accession)
    {
        pair<map<string, DBSequencePtr>::iterator, bool> insertResult = _dbSequences.insert(make_pair(accession, DBSequencePtr()));

        DBSequencePtr dbSequence = insertResult.first->second;

        if (insertResult.second)
        {
            dbSequence.reset(new DBSequence);
            _mzid->sequenceCollection.dbSequences.push_back(dbSequence);

            // MzIdentML::dataCollection is populated in HandlerSearchSummary
            dbSequence->searchDatabasePtr = _mzid->dataCollection.inputs.searchDatabase[0];
            dbSequence->accession = dbSequence->id = accession;
        }
        return dbSequence;
    }

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "search_score")
        {
            SpectrumIdentificationItem& sii = *_sir->spectrumIdentificationItem.back();

            // search_score comes after <alternative_protein> and <modification_info>,
            // so once we get here we can check if _currentPeptide is in sequenceCollection
            if (_currentPeptide->id.empty())
            {
                sort(_currentPeptide->modification.begin(), _currentPeptide->modification.end(), ModLessThan());
                pair<set<PeptidePtr, PeptideLessThan>::iterator, bool> insertResult = _peptides.insert(_currentPeptide);
                _currentPeptide = *insertResult.first;

                // if peptide does not exist, add it
                if (insertResult.second)
                {
                    _currentPeptide->id = "PEP_" + lexical_cast<string>(_peptides.size());
                    _mzid->sequenceCollection.peptides.push_back(_currentPeptide);

                    // now that _currentPeptide has an id, use it to build a unique id for each PeptideEvidence
                    BOOST_FOREACH(PeptideEvidencePtr& pe, sii.peptideEvidence)
                        pe->id += "_" + _currentPeptide->id;
                }

                // the peptide is guaranteed to exist, so reference it
                sii.peptidePtr = _currentPeptide;
            }

            sii.paramGroup.userParams.push_back(UserParam());
            UserParam& score = sii.paramGroup.userParams.back();
            getAttribute(attributes, "name", score.name);
            getAttribute(attributes, "value", score.value);
        }
        else if (name == "modification_info")
        {
            double modNTermMass, modCTermMass;
            getAttribute(attributes, "mod_nterm_mass", modNTermMass);
            getAttribute(attributes, "mod_cterm_mass", modCTermMass);

            if (modNTermMass > 0)
            {
                _currentPeptide->modification.push_back(ModificationPtr(new Modification));
                Modification& mod = *_currentPeptide->modification.back();
                mod.monoisotopicMassDelta = mod.avgMassDelta = modNTermMass - _nTerm.monoisotopicMass();
                mod.location = 0;
            }

            if (modCTermMass > 0)
            {
                _currentPeptide->modification.push_back(ModificationPtr(new Modification));
                Modification& mod = *_currentPeptide->modification.back();
                mod.monoisotopicMassDelta = mod.avgMassDelta = modCTermMass - _cTerm.monoisotopicMass();
                mod.location = _currentPeptide->peptideSequence.length() + 1;
            }
        }
        else if (name == "mod_aminoacid_mass")
        {
            _currentPeptide->modification.push_back(ModificationPtr(new Modification));
            Modification& mod = *_currentPeptide->modification.back();
            getAttribute(attributes, "position", mod.location);

            char modifiedResidue = _currentPeptide->peptideSequence[mod.location-1];
            double modMassPlusAminoAcid;
            getAttribute(attributes, "mass", modMassPlusAminoAcid);
            mod.avgMassDelta = mod.monoisotopicMassDelta = modMassPlusAminoAcid - AminoAcid::Info::record(modifiedResidue).residueFormula.monoisotopicMass();
        }
        else if (name == "alternative_protein")
        {
            SpectrumIdentificationItem& sii = *_sir->spectrumIdentificationItem.back();

            PeptideEvidencePtr pe = PeptideEvidencePtr(new PeptideEvidence);
            sii.peptideEvidence.push_back(pe);

            getAttribute(attributes, "protein", pe->id);
            pe->dbSequencePtr = getDBSequence(pe->id);
        }
        else if (name == "search_hit")
        {
            _sir->spectrumIdentificationItem.push_back(SpectrumIdentificationItemPtr(new SpectrumIdentificationItem(_sii)));
            SpectrumIdentificationItem& sii = *_sir->spectrumIdentificationItem.back();
            sii.id = "SII_" + lexical_cast<string>(++siiCount);
            getAttribute(attributes, "hit_rank", sii.rank);
            getAttribute(attributes, "calc_neutral_pep_mass", sii.calculatedMassToCharge);
            sii.calculatedMassToCharge = Ion::mz(sii.calculatedMassToCharge, sii.chargeState);

            int matchedIons, totalIons;
            getAttribute(attributes, "num_matched_ions", matchedIons);
            getAttribute(attributes, "tot_num_ions", totalIons);
            sii.paramGroup.set(MS_number_of_matched_peaks, matchedIons);
            sii.paramGroup.set(MS_number_of_unmatched_peaks, totalIons - matchedIons);

            _currentPeptide = PeptidePtr(new Peptide);
            getAttribute(attributes, "peptide", _currentPeptide->peptideSequence);

            PeptideEvidencePtr pe = PeptideEvidencePtr(new PeptideEvidence);
            sii.peptideEvidence.push_back(pe);

            getAttribute(attributes, "protein", pe->id);
            pe->dbSequencePtr = getDBSequence(pe->id);
            getAttribute(attributes, "num_missed_cleavages", pe->missedCleavages);
            getAttribute(attributes, "peptide_prev_aa", pe->pre);
            getAttribute(attributes, "peptide_next_aa", pe->post);
        }
        else if (name == "spectrum_query")
        {
            if (ilr) ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(1, siiCount, "Reading spectrum queries"));

            string spectrumNativeID;
            getAttribute(attributes, "spectrumNativeID", spectrumNativeID);
            //if (sirPtr->spectrumID.empty())

            // we assume that spectrum_query elements from the same spectrum are adjacent;
            // so we add a new SpectrumIdentificationResult when the spectrumNativeID changes
            if (!_sir.get() || _sir->spectrumID != spectrumNativeID)
            {
                _sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
                _sil->spectrumIdentificationResult.push_back(_sir);
                _sir->id = "SIR_" + lexical_cast<string>(_sil->spectrumIdentificationResult.size());
                _sir->spectrumID = spectrumNativeID;
                getAttribute(attributes, "spectrum", _sir->name);
            }

            double precursorNeutralMass;
            getAttribute(attributes, "precursor_neutral_mass", precursorNeutralMass);
            getAttribute(attributes, "assumed_charge", _sii.chargeState);
            _sii.experimentalMassToCharge = Ion::mz(precursorNeutralMass, _sii.chargeState);
            _sii.passThreshold = true;
        }
        else if (name == "search_result")
        {
            // some engines write custom attributes here; we transcode them as UserParams
            BOOST_FOREACH(const Attributes::value_type& attribute, attributes)
                _sir->paramGroup.userParams.push_back(UserParam(attribute.first, attribute.second));
        }
        else
            throw runtime_error("[HandlerSearchResults] Unexpected element name: " + name);

        return Status::Ok;
    }

    private:
    SpectrumIdentificationResultPtr _sir;
    SpectrumIdentificationItem _sii;
    set<PeptidePtr, PeptideLessThan> _peptides;
    map<string, DBSequencePtr> _dbSequences;
    PeptidePtr _currentPeptide;
    Formula _nTerm, _cTerm;
    int siiCount;
    const CVTranslator& _cvTranslator;
    const IterationListenerRegistry* ilr;
};


struct Handler_pepXML : public SAXParser::Handler
{
    MzIdentML& mzid;

    Handler_pepXML(MzIdentML& mzid, const IterationListenerRegistry* iterationListenerRegistry)
    :   mzid(mzid),
        handlerSampleEnzyme(cvTranslator),
        handlerSearchSummary(cvTranslator),
        handlerSearchResults(cvTranslator, iterationListenerRegistry),
        ilr(iterationListenerRegistry)
    {
        // add the SpectrumIdentificationProtocol
        SpectrumIdentificationProtocolPtr sipPtr = SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol("SIP"));
        mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sipPtr);

        sipPtr->searchType.set(MS_ms_ms_search);

        handlerSearchSummary._mzid = &mzid;
        handlerSearchSummary._sip = sipPtr.get();
        handlerSampleEnzyme._sip = sipPtr.get();

        // add the SpectrumIdentificationList
        SpectrumIdentificationListPtr silPtr = SpectrumIdentificationListPtr(new SpectrumIdentificationList("SIL"));
        mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silPtr);

        handlerSearchResults._mzid = &mzid;
        handlerSearchResults._sip = sipPtr.get();
        handlerSearchResults._sil = silPtr.get();

        // add the SpectrumIdentification
        SpectrumIdentificationPtr siPtr = SpectrumIdentificationPtr(new SpectrumIdentification("SI"));
        siPtr->spectrumIdentificationListPtr = silPtr;
        siPtr->spectrumIdentificationProtocolPtr = sipPtr;
        mzid.analysisCollection.spectrumIdentification.push_back(siPtr);
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "msms_pipeline_analysis")
        {
            if (ilr) ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(1, 0, "Reading header..."));

            string date, summaryXml;
            getAttribute(attributes, "date", date);
            getAttribute(attributes, "summary_xml", summaryXml);

            if (!summaryXml.empty())
            {
                SourceFilePtr sourceFile = SourceFilePtr(new SourceFile);
                sourceFile->id = "SF_1";
                sourceFile->name = bfs::path(summaryXml).filename();
                sourceFile->location = summaryXml;
                mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);
            }
            return Status::Ok;
        }
        else if (name == "msms_run_summary")
        {
            SpectraDataPtr spectraData = SpectraDataPtr(new SpectraData("SD_1"));
            getAttribute(attributes, "base_name", spectraData->location);
            spectraData->name = bfs::path(spectraData->location).filename();

            // TODO: attempt to determine file and nativeID format?
            mzid.dataCollection.inputs.spectraData.push_back(spectraData);
            mzid.analysisCollection.spectrumIdentification[0]->inputSpectra.push_back(spectraData);

            return Status::Ok;
        }
        else if (name == "sample_enzyme")
        {
            return Status(Status::Delegate, &handlerSampleEnzyme);
        }
        else if (name == "search_summary")
        {
            return Status(Status::Delegate, &handlerSearchSummary);
        }
        else if (name == "spectrum_query")
        {
            return Status(Status::Delegate, &handlerSearchResults);
        }
        throw runtime_error("[Handler_pepXML] Unexpected element name: " + name);
    }

    virtual Status endElement(const std::string& name, stream_offset position)
    {
        if (name == "msms_run_summary")
            if (ilr) ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(1, 0, "Finished reading spectrum queries."));

        return Status::Ok;
    }

    private:
    HandlerSampleEnzyme handlerSampleEnzyme;
    HandlerSearchSummary handlerSearchSummary;
    HandlerSearchResults handlerSearchResults;

    CVTranslator cvTranslator;
    const IterationListenerRegistry* ilr;
};

} // namespace


void Serializer_pepXML::read(boost::shared_ptr<std::istream> is, MzIdentML& mzid,
                             const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_pepXML::read()] Bad istream.");

    is->seekg(0);

    Handler_pepXML handler(mzid, iterationListenerRegistry);
    SAXParser::parse(*is, handler);
}


} // namespace mziddata
} // namespace pwiz

