//
// $Id$
//
//
// Original author: Brian Pratt <brian.pratt .@. insilicos.com>
//  after Serializer_pepXML by Matt Chambers <matt.chambers .@. vanderbilt.edu>
//  and with respect to OpenMS for ideas on pep/prot XML to mzIdentML mappings
//
// Copyright 2012 Spielberg Family Center for Applied Proteomics
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

#include "Serializer_protXML.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/misc/Singleton.hpp"
#include "boost/xpressive/xpressive.hpp"
#include "boost/range/adaptor/transformed.hpp"
#include "boost/range/algorithm/min_element.hpp"
#include "boost/range/algorithm/max_element.hpp"
#include "boost/range/algorithm/set_algorithm.hpp"
#include <cstring>

namespace pwiz {
namespace identdata {


using minimxml::XMLWriter;
using boost::iostreams::stream_offset;
using namespace pwiz::minimxml;
using namespace pwiz::chemistry;
using namespace pwiz::proteome;
using namespace pwiz::util;
using namespace pwiz::cv;



PWIZ_API_DECL void Serializer_protXML::write(ostream& os, const IdentData& mzid, const string& filepath,
                                            const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    throw runtime_error("[ASerializer_protXML::write] protXML output not implemented.");
}


namespace {


struct Handler_protXML : public SAXParser::Handler
{
    IdentData& mzid;

    PeptidePtr peptide; // peptide currently being read
    Attributes peptideAttributes;  // peptide currently being read
    SearchDatabasePtr searchDatabase; // our search database
    DBSequencePtr dbSequence; // current sequence
    ModificationPtr mod; // current modifications
    SpectrumIdentificationListPtr sil; // current spectrum IDs
    ProteinAmbiguityGroupPtr pagPtr; // current protein ambiguity group
    ProteinDetectionHypothesisPtr pdh; // current protein detection hypothesis

    int nPeptides;
    
    std::vector<std::string> *SourceFilesPtr; // if nonnull, this call is just to find out about source pepXML files

    pwiz::data::CVTranslator CVtranslator;

    Handler_protXML(IdentData& mzid,
                   const IterationListenerRegistry* iterationListenerRegistry,
                   bool strict,
                   std::vector<std::string> *returnValueSourceFiles)
    :   mzid(mzid),
        ilr(iterationListenerRegistry),
        strict(strict),
        nPeptides(0),
        SourceFilesPtr(returnValueSourceFiles)
    {
        if (!returnValueSourceFiles) // are we just looking for source pepXML file info?
        {
            // add default CVs
            mzid.cvs = defaultCVList();

            // add the ProteinDetectionProtocol
            ProteinDetectionProtocolPtr pdpPtr(new ProteinDetectionProtocol("PDP"));
            mzid.analysisProtocolCollection.proteinDetectionProtocol.push_back(pdpPtr);
        }
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
#define GET_TYPED_ATTR(T,name) T name; getAttribute(attributes, #name, name);
        if (name == "protein_summary")
        {
            if (SourceFilesPtr)
            {   
                // this call is just to see if we can get associated pepXML file info
                GET_TYPED_ATTR(string, summary_xml);
                SourceFilesPtr->push_back(summary_xml);  // may be useful in locating associated pepXML file(s)
            }
        }
        else if (name == "protein_summary_header")
        {
            if (SourceFilesPtr) 
            {
                // this call is just to find out about source pepXML files
                GET_TYPED_ATTR(string, source_files); // input pepXML files
                // GET_TYPED_ATTR(string, source_files_alt); // 
                SourceFilesPtr->push_back(source_files);
                return Status::Done;
            }
            GET_TYPED_ATTR(string, reference_database);
            // check for match with anything previously loaded, like pepXML
            string dbname(boost::replace_all_copy(reference_database,"\\","/"));
            BOOST_FOREACH(SearchDatabasePtr sdb, mzid.dataCollection.inputs.searchDatabase)
            {
                string sdbname(boost::replace_all_copy(sdb->location,"\\","/"));
                if (dbname==sdbname)
                {
                    searchDatabase = sdb;
                    break;
                }
            }
            if (!searchDatabase)
            {
                // no match with anything previously loaded
                SearchDatabasePtr sdb(new SearchDatabase());
                sdb->id = "DB_" + lexical_cast<string>(mzid.dataCollection.inputs.searchDatabase.size()+1);
                sdb->name = reference_database;
                sdb->version = "unknown";
                sdb->releaseDate = "unknown";
                sdb->location = "file:///"+reference_database;
                sdb->fileFormat.cvid = MS_FASTA_format;
                // sdb->numDatabaseSequences = ??;
                // sdb->numResidues = ??;
                sdb->set(MS_database_type_amino_acid);
                sdb->databaseName.userParams.push_back(UserParam(reference_database)); // not sure why others do this but I will too
                mzid.dataCollection.inputs.searchDatabase.push_back(sdb);
                searchDatabase = sdb;
            }

            // note: lots of TODO here where mapping to mzIdent isn't apparent to me (or to the OpenMS guys)

            // TODO GET_TYPED_ATTR(string, residue_substitution_list); // residues considered equivalent when comparing peptides
            // TODO GET_TYPED_ATTR(string, organism); // sample organism (used for annotation purposes)


            // TODO GET_TYPED_ATTR(string, source_file_xtn);  // file type (if not pepXML)
            // TODO GET_TYPED_ATTR(string, min_peptide_probability); // minimum adjusted peptide probability contributing to protein probability
            // TODO GET_TYPED_ATTR(string, min_peptide_weight); // minimum peptide weight contributing to protein probability
            // TODO GET_TYPED_ATTR(string, num_predicted_correct_prots); // total number of predicted correct protein ids (sum of probabilities)
            // TODO GET_TYPED_ATTR(string, num_input_1_spectra); // number of spectra from 1+ precursor ions
            // TODO GET_TYPED_ATTR(string, num_input_2_spectra); // number of spectra from 2+ precursor ions
            // TODO GET_TYPED_ATTR(string, num_input_3_spectra); // number of spectra from 3+ precursor ions
            // TODO GET_TYPED_ATTR(string, num_input_4_spectra); // number of spectra from 4+ precursor ions
            // TODO GET_TYPED_ATTR(string, num_input_5_spectra); // number of spectra from 5+ precursor ions
            // TODO GET_TYPED_ATTR(string, initial_min_peptide_prob); // minimum initial peptide probability to contribute to analysis
            // TODO GET_TYPED_ATTR(string, total_no_spectrum_ids); // (not required) total estimated number of correct peptide assignments in dataset
            GET_TYPED_ATTR(string, sample_enzyme); // enzyme applied to sample prior to MS/MS
            CVID enzyme = CVtranslator.translate(sample_enzyme);
            if (CVID_Unknown != enzyme)
            {
                ProteinDetectionProtocolPtr pdp(mzid.analysisProtocolCollection.proteinDetectionProtocol.back());
                pdp->analysisParams.cvParams.push_back(enzyme);
            }
        }
        else if (name == "program_details")
        {
            GET_TYPED_ATTR(string, analysis);
            AnalysisSoftwarePtr software(new AnalysisSoftware);
            string version;
            getAttribute(attributes, "version", software->version);
            software->id = "AS_" + analysis + "_" + software->version;
            software->name = analysis;
            // TODO  CV entry for proteinprophet?
            software->softwareName.set(MS_analysis_software, analysis);
            mzid.analysisSoftwareList.push_back(software);

            GET_TYPED_ATTR(string, time);
            ProteinDetection &pdp(mzid.analysisCollection.proteinDetection);
            if (pdp.empty())
            {
                pdp.id="PDP_1";
                pdp.name="PDP";
                if (!time.empty())
                {
                    mzid.creationDate = time;
                    if (pdp.activityDate.empty())
                        pdp.activityDate = time;
                }
                pdp.proteinDetectionListPtr = ProteinDetectionListPtr(new ProteinDetectionList("PDL"));
                mzid.dataCollection.analysisData.proteinDetectionListPtr = pdp.proteinDetectionListPtr;
            }
        }
        else if (name == "protein_group")
        {
            // start a new protein ambiguity group
            GET_TYPED_ATTR(string, group_number);
            // TODO GET_TYPED_ATTR(double, probability); //  how does this map at this level?
            pagPtr = ProteinAmbiguityGroupPtr(new ProteinAmbiguityGroup(group_number,group_number));
            mzid.analysisCollection.proteinDetection.proteinDetectionListPtr->proteinAmbiguityGroup.push_back(pagPtr);
        }
        else if (name == "protein") // nests in protein_group
        {
            GET_TYPED_ATTR(string, protein_name);
            pdh = ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis(protein_name));
            GET_TYPED_ATTR(string, probability);
            GET_TYPED_ATTR(string, percent_coverage);
            pdh->set(pwiz::cv::MS_PSM_level_search_engine_specific_statistic, probability); // as in OpenMS
            pdh->set(pwiz::cv::MS_sequence_coverage, percent_coverage);

            pagPtr->proteinDetectionHypothesis.push_back(pdh);
            bool foundDBS=false;
            BOOST_FOREACH( DBSequencePtr dbs, mzid.sequenceCollection.dbSequences)
            {
                if (dbs->accession == protein_name)
                {
                    foundDBS=true;
                    dbSequence = dbs;
                    break;
                }
            }
            if (!foundDBS)
            {
                DBSequencePtr dbs(new DBSequence);
                dbSequence = dbs;
                dbSequence->accession = protein_name;
                dbSequence->searchDatabasePtr = searchDatabase;
                mzid.sequenceCollection.dbSequences.push_back(dbSequence);
            }
        }
        else if (name == "peptide") // nests in protein
        {
            peptideAttributes = attributes;
	        GET_TYPED_ATTR(string, peptide_sequence);
            peptide = PeptidePtr(new Peptide);
            std::ostringstream os0;
            os0 << "PEP_" << ++nPeptides;
            peptide->id = os0.str();
            peptide->peptideSequence = peptide_sequence;
            // most of the work we do at tag close, when we have modifications etc - see below

        }
        else if (name == "modification_info") // nests in peptide
        {
		    // GET_TYPED_ATTR(string, modified_peptide);
        }
        else if (name == "mod_aminoacid_mass") // nests in modification_info
        {
			GET_TYPED_ATTR(int, position);
            GET_TYPED_ATTR(double, mass);
            mod = ModificationPtr (new Modification);
            peptide->modification.push_back(mod);

            mod->location = position;
            mod->residues.push_back(peptide->peptideSequence[mod->location-1]);
            mod->avgMassDelta = mod->monoisotopicMassDelta = mass - AminoAcid::Info::record(mod->residues.back()).residueFormula.monoisotopicMass();
            // TODO mod->set(UNIMOD_???);
        }
        else if (strict)
            throw runtime_error("[Handler_protXML] Unexpected element name: " +
                                name);

        return Status::Ok;
    }

    bool find_spectrum(const PeptidePtr pep,int charge,
            SpectrumIdentificationResultPtr &sir,
            SpectrumIdentificationItemPtr &sii)
    { 
        // can we locate pep in the spectrum identification list with proper charge?
        BOOST_FOREACH(SpectrumIdentificationResultPtr sirseek, sil->spectrumIdentificationResult)
        {
            BOOST_FOREACH(SpectrumIdentificationItemPtr siiseek,sirseek->spectrumIdentificationItem)
            {
                if ((siiseek->chargeState == charge) &&
                    (siiseek->peptidePtr == pep))
                {
                    sir = sirseek;
                    sii = siiseek;
                    return true;
                }
            }
        }
        return false;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "peptide")
        {        
            // now we've seen the modifcations, etc

            Attributes &attributes = peptideAttributes;
            GET_TYPED_ATTR(int, charge); 
            // TODO GET_TYPED_ATTR(double, weight);
            // TODO GET_TYPED_ATTR(string, is_nondegenerate_evidence); // "Y" or "N"
            // TODO GET_TYPED_ATTR(int, n_enzymatic_termini);
            // TODO GET_TYPED_ATTR(double, n_sibling_peptides);
            // TODO GET_TYPED_ATTR(int, n_sibling_peptides_bin);
            // TODO GET_TYPED_ATTR(int, n_instances);
            // TODO GET_TYPED_ATTR(string, is_contributing_evidence); // "Y" or "N"
            // TODO GET_TYPED_ATTR(double, calc_neutral_pep_mass);

            if (mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
            {
                sil = SpectrumIdentificationListPtr (new(SpectrumIdentificationList));
                std::ostringstream os1;
                os1 << "SIL_" << mzid.dataCollection.analysisData.spectrumIdentificationList.size()+1;
                sil->id = os1.str();
                mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);
            } 
            else
            {   // extend the existing list
                sil = mzid.dataCollection.analysisData.spectrumIdentificationList.back();
            }
            bool silfound=false;
            BOOST_FOREACH(SpectrumIdentificationListPtr seeksil, mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications)
            {
                if (sil==seeksil) {
                    silfound = true;
                    break;
                }
            }
            if (!silfound)
                mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back(sil);

            SpectrumIdentificationResultPtr sir;
            SpectrumIdentificationItemPtr sii;

            // do we have this peptide already loaded (perhaps from pepXML)?
            PeptidePtr match;
            BOOST_FOREACH(const PeptidePtr pep, mzid.sequenceCollection.peptides)
            {
                if (pep->peptideSequence == peptide->peptideSequence)
                {
                    if (!peptide->modification.size() && !pep->modification.size())
                    {
                        if (find_spectrum(pep,charge,sir,sii))
                            match = pep; // sequence, charge state and modifications match
                    }
                    else if (peptide->modification.size() == pep->modification.size())
                    {
                        bool failed=false;
                        BOOST_FOREACH(const ModificationPtr mod, peptide->modification)
                        {
                            bool found=false;
                            BOOST_FOREACH(const ModificationPtr mod2, pep->modification)
                            {
                                if ((mod->location==mod2->location) &&
                                    (fabs(mod->monoisotopicMassDelta-mod2->monoisotopicMassDelta)<.001))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) 
                            {
                                failed = true;
                                break;
                            }
                        }
                        if (!failed)
                        {
                            // can we locate it in the spectrum identification list with proper charge?
                            if (find_spectrum(pep,charge,sir,sii))
                            {
                                match = pep;
                                break;
                            }
                        }
                    }
                }
                if (match)
                    break;
            }
            if (match)
            {
                peptide = match;  // use previously loaded peptide
            }
            else
            {
                mzid.sequenceCollection.peptides.push_back(peptide); // new peptide

                sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult());
                std::ostringstream os1;
                os1 << sil->id << "_" << sil->spectrumIdentificationResult.size()+1;
                sir->id = os1.str();
                sil->spectrumIdentificationResult.push_back(sir);

                sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem());
                std::ostringstream os2;
                os2 << sir->id << "_" <<  sir->spectrumIdentificationItem.size()+1;
                sii->id = os2.str();
                sii->rank = 1;
                sii->chargeState = charge;
                sii->passThreshold = true;
                proteome::Peptide proteomePeptide = identdata::peptide(*peptide);
                proteome::Fragmentation fragmentation(proteomePeptide, true, true);

                sii->experimentalMassToCharge = Ion::mz(proteomePeptide.molecularWeight(), sii->chargeState);
                sii->calculatedMassToCharge = Ion::mz(proteomePeptide.monoisotopicMass(), sii->chargeState);
                sii->peptidePtr = peptide;

                sir->spectrumIdentificationItem.push_back(sii);
            }

            PeptideEvidencePtr pe;
            BOOST_FOREACH(PeptideEvidencePtr peseek, mzid.sequenceCollection.peptideEvidence)
            {
                if ((peseek->peptidePtr == peptide) && (peseek->dbSequencePtr==dbSequence))
                {
                    pe = peseek;
                    break;
                }
            }
            if (!pe) {
                pe = PeptideEvidencePtr(new PeptideEvidence());
                pe->peptidePtr = peptide;
                pe->dbSequencePtr =  dbSequence;
                // TODO GET_TYPED_ATTR(double, initial_probability);
                GET_TYPED_ATTR(double, nsp_adjusted_probability);
                pe->set(MS_PSM_level_search_engine_specific_statistic, nsp_adjusted_probability);
                mzid.sequenceCollection.peptideEvidence.push_back(pe);
            }
            bool pefound = false;
            BOOST_FOREACH(PeptideEvidencePtr peseek, sii->peptideEvidencePtr)
            {
                if (pe == peseek)
                {
                    pefound = true;
                    break;
                }
            }
            if (!pefound)
            {
                sii->peptideEvidencePtr.push_back(pe);
            }
            PeptideHypothesis ph;
            ph.peptideEvidencePtr = pe;
            ph.spectrumIdentificationItemPtr.push_back(sii);
            pdh->peptideHypothesis.push_back(ph);

        }
        return Status::Ok;
    }

    private:

    CVTranslator cvTranslator;
    bool readSpectrumQueries;
    const IterationListenerRegistry* ilr;
    bool strict;
};

} // namespace


PWIZ_API_DECL void Serializer_protXML::read(boost::shared_ptr<std::istream> is, IdentData& mzid,
                                           std::vector<std::string> *sourceFilesPtr, // if non-null, just read the SourceFiles info and return it here
                                           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    bool strict = false;
    
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_protXML::read()] Bad istream.");

    is->seekg(0);

    Handler_protXML handler(mzid, 
                           iterationListenerRegistry, strict, sourceFilesPtr);
    SAXParser::parse(*is, handler);
    if (sourceFilesPtr)
    {
        // this was just an inital call to check for pepXML source file info
        return;
    }

    // final iteration update
    if (iterationListenerRegistry &&
        !mzid.dataCollection.analysisData.proteinDetectionListPtr->empty() &&
        iterationListenerRegistry->broadcastUpdateMessage(
        IterationListener::UpdateMessage(mzid.dataCollection.analysisData.proteinDetectionListPtr->proteinAmbiguityGroup.size()-1,
                                             mzid.dataCollection.analysisData.proteinDetectionListPtr->proteinAmbiguityGroup.size(),
                                             "reading protein groups")) == IterationListener::Status_Cancel)
        return;

}



} // namespace identdata
} // namespace pwiz

