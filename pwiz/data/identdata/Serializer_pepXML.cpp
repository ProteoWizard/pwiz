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
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/misc/Singleton.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"
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
namespace bxp = boost::xpressive;


namespace {


struct ci_less
{
    bool operator() (const string& lhs, const string& rhs) const
    {
        if (lhs.length() != rhs.length())
            return lhs.length() < rhs.length();
        for (size_t i=0; i < lhs.length(); ++i)
            if (tolower(lhs[i]) != tolower(rhs[i]))
                return tolower(lhs[i]) < tolower(rhs[i]);
        return false;
    }
};


struct AnalysisSoftwareTranslation
{
    CVID softwareCVID;
    const char* softwareNames; // first name is the preferred one
};

const AnalysisSoftwareTranslation analysisSoftwareTranslationTable[] =
{
    {MS_ProteoWizard_software, "ProteoWizard software;ProteoWizard"},
    {MS_SEQUEST, "Sequest"},
    {MS_Mascot, "Mascot"},
    {MS_OMSSA, "OMSSA"},
    {MS_Phenyx, "Phenyx"},
    {MS_greylag, "greylag"},
    {MS_ProteinPilot_Software, "ProteinPilot;Protein Pilot"},
    {MS_ProteinLynx_Global_Server, "ProteinLynx;Protein Lynx;PLGS"},
    {MS_MyriMatch, "MyriMatch"},
    {MS_TagRecon, "TagRecon"},
    {MS_Pepitome, "Pepitome"},
    {MS_X_Tandem, "X! Tandem;X!Tandem;xtandem;X! Tandem (k-score)"},
    {MS_Spectrum_Mill_for_MassHunter_Workstation, "Spectrum Mill;SpectrumMill"},
    {MS_Proteios, "Proteios"},
    {MS_MS_GF_, "MS-GF+"},
    {MS_Comet, "Comet"}
    // TODO: PROBID, InsPecT, Crux, Tide need CV terms
};

const size_t analysisSoftwareTranslationTableSize = sizeof(analysisSoftwareTranslationTable)/sizeof(AnalysisSoftwareTranslation);

struct AnalysisSoftwareTranslator : public boost::singleton<AnalysisSoftwareTranslator>
{
    AnalysisSoftwareTranslator(boost::restricted)
    {
        for (size_t i=0; i < analysisSoftwareTranslationTableSize; ++i)
        {
            const AnalysisSoftwareTranslation& ast = analysisSoftwareTranslationTable[i];
            vector<string> names;
            bal::split(names, ast.softwareNames, bal::is_any_of(";"));
            if (names.empty())
                throw runtime_error("[AnalysisSoftwareTranslator::ctor] Invalid software name list.");

            preferredSoftwareNameByCVID[ast.softwareCVID] = names[0];
            for (size_t j=0; j < names.size(); ++j)
                cvidBySoftwareName[names[j]] = ast.softwareCVID;
        }
    }

    CVID translate(const string& softwareName) const
    {
        map<string, CVID, ci_less>::const_iterator itr = cvidBySoftwareName.find(softwareName);
        if (itr == cvidBySoftwareName.end())
            return CVID_Unknown;
        return itr->second;
    }

    const string& translate(CVID softwareCVID) const
    {
        map<CVID, string>::const_iterator itr = preferredSoftwareNameByCVID.find(softwareCVID);
        if (itr == preferredSoftwareNameByCVID.end())
            return empty;
        return itr->second;
    }

    private:
    map<CVID, string> preferredSoftwareNameByCVID;
    map<string, CVID, ci_less> cvidBySoftwareName;
    const string empty;
};


struct ScoreTranslation
{
    CVID softwareCVID;
    CVID scoreCVID;
    const char* scoreNames; // first name is the preferred one
};

const ScoreTranslation scoreTranslationTable[] =
{
    {MS_SEQUEST, MS_SEQUEST_xcorr, "xcorr"},
    {MS_SEQUEST, MS_SEQUEST_deltacn, "deltacn;deltcn"},
    {MS_Mascot, MS_Mascot_score, "ionscore;score"},
    {MS_Mascot, MS_Mascot_identity_threshold, "identityscore"},
    {MS_Mascot, MS_Mascot_homology_threshold, "homologyscore"},
    {MS_Mascot, MS_Mascot_expectation_value, "expect"}, // ??
    {MS_OMSSA, MS_OMSSA_pvalue, "pvalue"},
    {MS_OMSSA, MS_OMSSA_evalue, "expect"},
    {MS_Phenyx, MS_Phenyx_Pepzscore, "zscore"},
    {MS_Phenyx, MS_Phenyx_PepPvalue, "zvalue"},
    //{MS_greylag, MS_greylag_??, "??"},
    //{MS_Phenyx, MS_Phenyx_Score, "??"},
    //{MS_ProteinPilot_Software, MS_Paragon_score, "??"},
    //{MS_ProteinLynx_Global_Server, MS_ProteinLynx_Ladder_Score, "??"},
    //{MS_ProteinLynx_Global_Server, MS_ProteinLynx_Log_Likelihood, "??"},
    {MS_MyriMatch, MS_MyriMatch_MVH, "mvh"},
    {MS_TagRecon, MS_MyriMatch_MVH, "mvh"},
    {MS_Pepitome, MS_MyriMatch_MVH, "mvh"},
    {MS_MyriMatch, MS_MyriMatch_mzFidelity, "mzFidelity"},
    {MS_TagRecon, MS_MyriMatch_mzFidelity, "mzFidelity"},
    {MS_Pepitome, MS_MyriMatch_mzFidelity, "mzFidelity"},
    {MS_X_Tandem, MS_X_Tandem_hyperscore, "hyperscore"},
    {MS_X_Tandem, MS_X_Tandem_expect, "expect"},
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_Score, "??"},
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_Discriminant_Score, "??"},
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_SPI, "??"},
    //{MS_Proteios, MS_Proteios_??, "??"},
    {MS_MS_GF, MS_MS_GF_RawScore, "raw"},
    {MS_MS_GF, MS_MS_GF_DeNovoScore, "denovo"},
    {MS_MS_GF, MS_MS_GF_Energy, "energy"},
    {MS_MS_GF, MS_MS_GF_EValue, "EValue"},
    {MS_MS_GF, MS_MS_GF_QValue, "QValue"},
    {MS_MS_GF, MS_MS_GF_SpecEValue, "SpecEValue"},
    {MS_MS_GF, MS_MS_GF_PepQValue, "PepQValue"},
    {MS_MS_GF, MS_MS_GF_PEP, "PEP"},
    {MS_MS_GF_, MS_MS_GF_RawScore, "raw"},
    {MS_MS_GF_, MS_MS_GF_DeNovoScore, "denovo"},
    {MS_MS_GF_, MS_MS_GF_Energy, "energy"},
    {MS_MS_GF_, MS_MS_GF_EValue, "EValue"},
    {MS_MS_GF_, MS_MS_GF_QValue, "QValue"},
    {MS_MS_GF_, MS_MS_GF_SpecEValue, "SpecEValue"},
    {MS_MS_GF_, MS_MS_GF_PepQValue, "PepQValue"},
    {MS_MS_GF_, MS_MS_GF_PEP, "PEP"},
    {MS_Comet, MS_Comet_xcorr, "xcorr"},
    {MS_Comet, MS_Comet_deltacn, "deltacn"},
    {MS_Comet, MS_Comet_deltacnstar, "deltacnstar"},
    {MS_Comet, MS_Comet_sprank, "sprank"},
    {MS_Comet, MS_Comet_spscore, "spscore"},
    {MS_Comet, MS_Comet_expectation_value, "expect"}
};

const size_t scoreTranslationTableSize = sizeof(scoreTranslationTable)/sizeof(ScoreTranslation);

struct ScoreTranslator : public boost::singleton<ScoreTranslator>
{
    ScoreTranslator(boost::restricted)
    {
        preferredScoreNameBySoftwareAndScoreCVID[CVID_Unknown][CVID_Unknown] = "";

        for (size_t i=0; i < scoreTranslationTableSize; ++i)
        {
            const ScoreTranslation& st = scoreTranslationTable[i];
            vector<string> names;
            bal::split(names, st.scoreNames, bal::is_any_of(";"));
            if (names.empty())
                throw runtime_error("[AnalysisSoftwareTranslator::ctor] Invalid software name list.");

            preferredScoreNameBySoftwareAndScoreCVID[st.softwareCVID][st.scoreCVID] = names[0];
            for (size_t j=0; j < names.size(); ++j)
                scoreCVIDBySoftwareAndScoreName[st.softwareCVID][names[j]] = st.scoreCVID;
        }
    }

    CVID translate(CVID softwareCVID, const string& scoreName) const
    {
        map<CVID, map<string, CVID, ci_less> >::const_iterator itr = scoreCVIDBySoftwareAndScoreName.find(softwareCVID);
        if (itr == scoreCVIDBySoftwareAndScoreName.end())
            return CVID_Unknown;
        map<string, CVID, ci_less>::const_iterator itr2 = itr->second.find(scoreName);
        if (itr2 == itr->second.end())
            return CVID_Unknown;
        return itr2->second;
    }

    const string& translate(CVID softwareCVID, CVID scoreCVID) const
    {
        map<CVID, map<CVID, string> >::const_iterator itr = preferredScoreNameBySoftwareAndScoreCVID.find(softwareCVID);
        if (itr == preferredScoreNameBySoftwareAndScoreCVID.end())
            return empty;
        map<CVID, string>::const_iterator itr2 = itr->second.find(scoreCVID);
        if (itr2 == itr->second.end())
            return empty;
        return itr2->second;
    }

    private:
    // TODO: use boost::multi_index?
    map<CVID, map<CVID, string> > preferredScoreNameBySoftwareAndScoreCVID;
    map<CVID, map<string, CVID, ci_less> > scoreCVIDBySoftwareAndScoreName;
    const string empty;
};


struct NativeIdTranslator : public boost::singleton<NativeIdTranslator>
{
    NativeIdTranslator(boost::restricted)
    {
        using namespace boost::xpressive;

        BOOST_FOREACH(CVID cvid, pwiz::cv::cvids())
        {
            if (!cvIsA(cvid, MS_native_spectrum_identifier_format))
                continue;

            string format = cvTermInfo(cvid).def;
            if (!bal::icontains(format, "xsd"))
                continue;

            sregex nativeIdFormatRegex = sregex::compile(".*?(\\S+=\\S+( \\S+=\\S+)*)\\.?");
            smatch what;
            if (!regex_match(format, what, nativeIdFormatRegex))
                continue;

            format = what[1].str();
            bal::trim_right_if(format, bal::is_any_of("."));
            bal::ireplace_all(format, "xsd:nonNegativeInteger", "\\d+");
            bal::ireplace_all(format, "xsd:positiveInteger", "\\d+");
            bal::ireplace_all(format, "xsd:Long", "\\d+");
            bal::ireplace_all(format, "xsd:string", "\\S+");
            bal::ireplace_all(format, "xsd:IDREF", "\\S+");
            nativeIdRegexAndFormats.push_back(make_pair(sregex::compile(format), cvid));
        }
    }

    CVID translate(const string& id)
    {
        using namespace boost::xpressive;

        smatch what;
        BOOST_FOREACH(const RegexFormatPair& pair, nativeIdRegexAndFormats)
            if (regex_match(id, what, pair.first))
                return pair.second;
        return CVID_Unknown;
    }

    private:
    typedef pair<boost::xpressive::sregex, CVID> RegexFormatPair;
    vector<RegexFormatPair> nativeIdRegexAndFormats;
};


// Formula nTerm("H1"), cTerm("O1H1");  danger!
// global initialization order isn't predictable, if this inits  
// before boost::singleton does you can get a double free on exit
// (observed under MSVC8) - bpratt
const char *Formula_nTerm="H1";
const char *Formula_cTerm="O1H1"; 

string base_name(const IdentData& mzid, const string& filepath)
{
    bfs::path location = filepath;
    if (!mzid.dataCollection.inputs.spectraData.empty())
        location = mzid.dataCollection.inputs.spectraData[0]->location;
    return BFS_STRING(location.replace_extension("").filename());
}

void start_msms_pipeline_analysis(XMLWriter& xmlWriter, const IdentData& mzid, const string& filepath)
{
    XMLWriter::Attributes attributes;

    if (!mzid.creationDate.empty())
        attributes.add("date", mzid.creationDate);

    attributes.add("summary_xml", filepath);
    attributes.add("xmlns", "http://regis-web.systemsbiology.net/pepXML");
    attributes.add("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
    attributes.add("xsi:schemaLocation", "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd");

    xmlWriter.startElement("msms_pipeline_analysis", attributes);
}

void write_analysis_summary(XMLWriter& xmlWriter, const IdentData& mzid, const AnalysisSoftware& as)
{
    XMLWriter::Attributes attributes;

    CVParam searchEngine = as.softwareName.cvParamChild(MS_analysis_software);
    CVParam customTool = as.softwareName.cvParam(MS_custom_unreleased_software_tool);
    if (!searchEngine.empty())
        attributes.add("analysis", searchEngine.name());
    else if (!customTool.value.empty())
        attributes.add("analysis", customTool.value);
    else if (!as.softwareName.userParams.empty())
        attributes.add("analysis", as.softwareName.userParams[0].name);
    else
        throw runtime_error("[write_analysis_summary] empty AnalysisSoftware::SoftwareName");

    if (!as.version.empty())
        attributes.add("version", as.version);

    if (!mzid.analysisCollection.spectrumIdentification[0]->activityDate.empty())
        attributes.add("time", mzid.analysisCollection.spectrumIdentification[0]->activityDate);
    else
        attributes.add("time", encode_xml_datetime(bpt::second_clock::universal_time()));

    xmlWriter.startElement("analysis_summary", attributes, XMLWriter::EmptyElement);
}

void start_msms_run_summary(XMLWriter& xmlWriter, const IdentData& mzid, const string& filepath)
{
    XMLWriter::Attributes attributes;

    attributes.add("base_name", base_name(mzid, filepath));
    attributes.add("raw_data_type", "");
    attributes.add("raw_data", "");

    xmlWriter.startElement("msms_run_summary", attributes);
}

struct EnzymePtr_name
{
    typedef string result_type;
    result_type operator()(const EnzymePtr& x) const
    {
        CVParam enzymeName = x->enzymeName.cvParamChild(MS_cleavage_agent_name);
        if (!enzymeName.empty() && enzymeName.cvid != MS_NoEnzyme_OBSOLETE)
            return enzymeName.name();
        if (!x->enzymeName.userParams.empty())
            return x->enzymeName.userParams[0].name;
        if (!x->name.empty())
            return x->name;
        if (!x->siteRegexp.empty())
            return x->siteRegexp;
        throw runtime_error("[EnzymePtr_name] No enzyme name or regular expression.");
    }
};

struct EnzymePtr_specificity
{
    typedef int result_type;
    int operator()(const EnzymePtr& x) const {return x->terminalSpecificity;}
};

struct EnzymePtr_missedCleavages
{
    typedef int result_type;
    int operator()(const EnzymePtr& x) const {return x->missedCleavages;}
};

struct EnzymePtr_minDistance
{
    typedef int result_type;
    int operator()(const EnzymePtr& x) const {return x->minDistance;}
};

void write_sample_enzyme(XMLWriter& xmlWriter, const IdentData& mzid)
{
    const SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];
    bool independent = sip.enzymes.independent;

    // create a cumulative enzyme name for multiple enzymes like "Trypsin + AspN + Chymotrypsin"
    string enzymeName = bal::join(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_name()), " + ");

    // find the minimum specificity
    int minSpecificity = *boost::range::min_element(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_specificity()));

    XMLWriter::Attributes attributes;
    attributes.add("name", enzymeName);
    attributes.add("independent", independent ? "true" : "false");

    switch (minSpecificity)
    {
        case 2: attributes.add("fidelity", "specific"); break;
        case 1: attributes.add("fidelity", "semispecific"); break;
        case 0: attributes.add("fidelity", "nonspecific"); break;
    }

    xmlWriter.startElement("sample_enzyme", attributes);
    {
        BOOST_FOREACH(const EnzymePtr& ez, sip.enzymes.enzymes)
        {
            // parse CV enzymeName or siteRegexp from each enzyme into cut/no_cut/sense attributes
            PepXMLSpecificity result = pepXMLSpecificity(*ez);
            attributes.clear();
            attributes.add("sense", result.sense);
            attributes.add("cut", result.cut);
            attributes.add("no_cut", result.no_cut);
            attributes.add("min_spacing", ez->minDistance);
            xmlWriter.startElement("specificity", attributes, XMLWriter::EmptyElement);
        }
    }
    xmlWriter.endElement(); // sample_enzyme
}

struct CVParam_name
{
    typedef string result_type;
    result_type operator()(const CVParam& x) const {return x.name();}
};

void write_search_summary(XMLWriter& xmlWriter, const IdentData& mzid, const string& filepath)
{
    XMLWriter::Attributes attributes;

    attributes.add("base_name", base_name(mzid, filepath));

    const SpectrumIdentificationProtocol& sip =
        *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];

    if (!sip.analysisSoftwarePtr.get())
        throw runtime_error("[write_search_summary] PepXML requires the analysis software to be known.");

    CVParam searchEngine = sip.analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software);
    if (!searchEngine.empty())
        attributes.add("search_engine", searchEngine.name());
    else if (!sip.analysisSoftwarePtr->softwareName.userParams.empty())
        attributes.add("search_engine", sip.analysisSoftwarePtr->softwareName.userParams[0].name);
    else
        throw runtime_error("[write_search_summary] PepXML requires the analysis software to be known.");

    attributes.add("precursor_mass_type",
                   sip.additionalSearchParams.hasCVParam(MS_parent_mass_type_average) ?
                   "average" : "monoisotopic");
    attributes.add("fragment_mass_type",
                   sip.additionalSearchParams.hasCVParam(MS_fragment_mass_type_average) ?
                   "average" : "monoisotopic");
    attributes.add("out_data_type", "");
    attributes.add("out_data", "");

    xmlWriter.startElement("search_summary", attributes);
    {
        if (mzid.dataCollection.inputs.searchDatabase.empty())
            throw runtime_error("[write_search_summary] PepXML requires the searched database to be known.");

        const SearchDatabase& sd = *mzid.dataCollection.inputs.searchDatabase[0];
        attributes.clear();
        attributes.add("local_path", sd.location);
        attributes.add("database_name", sd.id);
        //attributes.add("database_release_identifier", "");
        if (sd.numDatabaseSequences > 0)
            attributes.add("size_in_db_entries", sd.numDatabaseSequences);
        if (sd.numResidues > 0)
            attributes.add("size_of_residues", sd.numResidues);
        attributes.add("type", sd.hasCVParam(MS_database_type_amino_acid) ? "AA" : "NA");
        xmlWriter.startElement("search_database", attributes, XMLWriter::EmptyElement);

        // create a cumulative enzyme name for multiple enzymes like "Trypsin + AspN + Chymotrypsin"
        string enzymeName = bal::join(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_name()), " + ");

        // find the maximum missed cleavages
        int maxMissedCleavages = *boost::range::max_element(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_missedCleavages()));

        // find the minimum specificity
        int minSpecificity = *boost::range::min_element(sip.enzymes.enzymes | boost::adaptors::transformed(EnzymePtr_specificity()));

        Formula nTerm(Formula_nTerm), cTerm(Formula_cTerm); 


        attributes.clear();
        attributes.add("enzyme", enzymeName);
        attributes.add("max_num_internal_cleavages", maxMissedCleavages);
        attributes.add("min_number_termini", minSpecificity);
        xmlWriter.startElement("enzymatic_search_constraint", attributes, XMLWriter::EmptyElement);

        BOOST_FOREACH(const SearchModificationPtr& sm, sip.modificationParams)
        {
            vector<char> residues = sm->residues;
            if (residues.empty() || (residues.size() == 1 && residues[0] == '.'))
            {
                residues.clear();

                if (sm->specificityRules.empty())
                    throw runtime_error("[write_search_summary] Empty or non-specific SearchModification.");

                switch (sm->specificityRules.cvid)
                {
                    case MS_modification_specificity_peptide_N_term: residues.push_back('n'); break;
                    case MS_modification_specificity_protein_N_term: residues.push_back('o'); break;
                    case MS_modification_specificity_peptide_C_term: residues.push_back('c'); break;
                    case MS_modification_specificity_protein_C_term: residues.push_back('d'); break;
                    default: throw runtime_error("[write_search_summary] Cannot handle CV term in specificityRules: " + sm->specificityRules.name());
                }
            }

            BOOST_FOREACH(char aa, residues)
            {
                attributes.clear();
                if (aa > 'Z') // terminal_modification
                {
                    switch (aa)
                    {
                        case 'o':
                            attributes.add("protein_terminus", "n");
                        case 'n':
                            attributes.add("terminus", "n");
                            attributes.add("massdiff", sm->massDelta);
                            attributes.add("mass", nTerm.monoisotopicMass() + sm->massDelta);
                            break;

                        case 'd':
                            attributes.add("protein_terminus", "c");
                        case 'c':
                            attributes.add("terminus", "c");
                            attributes.add("massdiff", sm->massDelta);
                            attributes.add("mass", cTerm.monoisotopicMass() + sm->massDelta);
                            break;

                        default: throw runtime_error("[write_search_summary] Unsupported AA residue: " + aa);
                    }
                }
                else // aminoacid_modificiation
                {
                    double aaMass = AminoAcid::Info::record(aa).residueFormula.monoisotopicMass();
                    attributes.add("aminoacid", string(1, aa));
                    attributes.add("massdiff", sm->massDelta);
                    attributes.add("mass", sm->massDelta + aaMass);
                    
                    if (!sm->specificityRules.empty())
                        switch (sm->specificityRules.cvid)
                        {
                            case MS_modification_specificity_peptide_N_term: attributes.add("peptide_terminus", "n"); break;
                            case MS_modification_specificity_protein_N_term: attributes.add("peptide_terminus", "n"); break;
                            case MS_modification_specificity_peptide_C_term: attributes.add("peptide_terminus", "c"); break;
                            case MS_modification_specificity_protein_C_term: attributes.add("peptide_terminus", "c"); break;
                            default: throw runtime_error("[write_search_summary] Cannot handle CV term in specificityRules: " + sm->specificityRules.name());
                        }
                }
                attributes.add("variable", sm->fixedMod ? "N" : "Y");

                if (sm->hasCVParamChild(UNIMOD_unimod_root_node))
                {
                    vector<CVParam> possibleMods = sm->cvParamChildren(UNIMOD_unimod_root_node);
                    string description = bal::join(possibleMods | boost::adaptors::transformed(CVParam_name()), ", ");
                    attributes.add("description", description);
                }

                xmlWriter.startElement(aa > 'Z' ? "terminal_modification" : "aminoacid_modification", attributes, XMLWriter::EmptyElement);
            }
        } // *_modification

        BOOST_FOREACH(const UserParam& userParam, sip.additionalSearchParams.userParams)
        {
            attributes.clear();
            attributes.add("name", userParam.name);
            attributes.add("value", userParam.value);
            xmlWriter.startElement("parameter", attributes, XMLWriter::EmptyElement);
        }

        CVParam decoyPrefix = sd.cvParam(MS_decoy_DB_accession_regexp);
        if (!decoyPrefix.empty())
        {
            attributes.clear();
            attributes.add("name", "DecoyPrefix");
            attributes.add("value", bal::trim_left_copy_if(decoyPrefix.value, bal::is_any_of("^")));
            xmlWriter.startElement("parameter", attributes, XMLWriter::EmptyElement);
        }
    }
    xmlWriter.endElement(); // search_summary
}

void write_modification_info(XMLWriter& xmlWriter, const SpectrumIdentificationItem& sii)
{
    XMLWriter::Attributes attributes;

    const Peptide& peptide = *sii.peptidePtr;
    vector<ModificationPtr> aaMods;
    aaMods.reserve(peptide.modification.size());
    Formula nTerm(Formula_nTerm), cTerm(Formula_cTerm); 

    double nTermModMass = 0, cTermModMass = 0;
    BOOST_FOREACH(const ModificationPtr& modPtr, peptide.modification)
    {
        const Modification& mod = *modPtr;
        double modMass = mod.monoisotopicMassDelta != 0 ? mod.monoisotopicMassDelta : mod.avgMassDelta;
        if (mod.location == 0)
            nTermModMass += modMass;
        else if (mod.location == (int) peptide.peptideSequence.length() + 1)
            cTermModMass += modMass;
        else
            aaMods.push_back(modPtr);
    }

    if (nTermModMass != 0)
        attributes.add("mod_nterm_mass", nTerm.monoisotopicMass() + nTermModMass);
    if (cTermModMass != 0)
        attributes.add("mod_cterm_mass", cTerm.monoisotopicMass() + cTermModMass);

    if (aaMods.empty())
        xmlWriter.startElement("modification_info", attributes, XMLWriter::EmptyElement);
    else
    {
        xmlWriter.startElement("modification_info", attributes);
        {
            BOOST_FOREACH(const ModificationPtr& modPtr, aaMods)
            {
                const Modification& mod = *modPtr;
                char modifiedResidue = mod.residues.size() == 1 ? mod.residues[0] : peptide.peptideSequence[mod.location-1];
                double aaMass = AminoAcid::Info::record(modifiedResidue).residueFormula.monoisotopicMass();
                double modMass = mod.monoisotopicMassDelta != 0 ? mod.monoisotopicMassDelta : mod.avgMassDelta;

                attributes.clear();
                attributes.add("position", mod.location);
                attributes.add("mass", aaMass + modMass);
                xmlWriter.startElement("mod_aminoacid_mass", attributes, XMLWriter::EmptyElement);
            }
        }
        xmlWriter.endElement();
    }
}

void write_alternative_proteins(XMLWriter& xmlWriter, const SpectrumIdentificationItem& sii)
{
    XMLWriter::Attributes attributes;

    for (size_t i=1; i < sii.peptideEvidencePtr.size(); ++i)
    {
        attributes.clear();
        attributes.add("protein", sii.peptideEvidencePtr[i]->dbSequencePtr->accession);
        if (sii.peptideEvidencePtr[i]->dbSequencePtr->hasCVParam(MS_protein_description))
            attributes.add("protein_descr",
                           sii.peptideEvidencePtr[i]->dbSequencePtr->cvParam(MS_protein_description).value);

        xmlWriter.startElement("alternative_protein", attributes, XMLWriter::EmptyElement);
    }
}

// we only write search_scores for numeric CVParams and UserParams;
// examples of valid numbers: 1 1.234 1.234e5 1.234E-5 (also 123.456e5, not a big deal)
boost::xpressive::sregex numericRegex = boost::xpressive::sregex::compile("[+-]?\\d+(?:\\.\\d*)?(?:[eE][+-]?\\d+)?");

void write_search_hit(XMLWriter& xmlWriter,
                      CVID analysisSoftwareCVID,
                      const IdentData& mzid,
                      const SpectrumIdentificationResult& sir,
                      const SpectrumIdentificationItem& sii)
{
    if (!sii.peptidePtr.get())
        throw runtime_error("[write_search_hit] PepXML requires SpectrumIdentificationItem elements to refer to Peptides.");
    if (sii.peptideEvidencePtr.empty())
        throw runtime_error("[write_search_hit] PepXML requires PeptideEvidence elements.");
    if (!sii.peptideEvidencePtr[0]->dbSequencePtr.get())
        throw runtime_error("[write_search_hit] PepXML requires PeptideEvidence elements to refer to DBSequences.");

    XMLWriter::Attributes attributes;

    DBSequencePtr dbseq =  sii.peptideEvidencePtr[0]->dbSequencePtr;
    attributes.add("hit_rank", sii.rank);
    attributes.add("peptide", sii.peptidePtr->peptideSequence);
    attributes.add("peptide_prev_aa", sii.peptideEvidencePtr[0]->pre);
    attributes.add("peptide_next_aa", sii.peptideEvidencePtr[0]->post);
    attributes.add("protein", dbseq->accession);
    attributes.add("num_tot_proteins", sii.peptideEvidencePtr.size());
    attributes.add("calc_neutral_pep_mass", Ion::neutralMass(sii.calculatedMassToCharge, sii.chargeState));
    attributes.add("massdiff", Ion::neutralMass(sii.calculatedMassToCharge, sii.chargeState) - Ion::neutralMass(sii.experimentalMassToCharge, sii.chargeState));

    // Add the protein description, if present
    if (dbseq->hasCVParam(MS_protein_description))
        attributes.add("protein_descr",
                       dbseq->cvParam(MS_protein_description).value);
    
    // calculate num_tol_term and num_missed_cleavages
    const SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];
    DigestedPeptide digestedPeptide = identdata::digestedPeptide(sip, *sii.peptideEvidencePtr[0]);
    attributes.add("num_tol_term", digestedPeptide.specificTermini());
    attributes.add("num_missed_cleavages", digestedPeptide.missedCleavages());


    if (sii.hasCVParam(MS_number_of_matched_peaks))
    {
        int matchedPeaks = sii.cvParam(MS_number_of_matched_peaks).valueAs<int>();
        attributes.add("num_matched_ions", matchedPeaks);

        if (sii.hasCVParam(MS_number_of_unmatched_peaks))
            attributes.add("tot_num_ions", matchedPeaks + sii.cvParam(MS_number_of_unmatched_peaks).valueAs<int>());
    }

    xmlWriter.startElement("search_hit", attributes);
    {
        if (sii.peptideEvidencePtr.size() > 1)
            write_alternative_proteins(xmlWriter, sii);

        if (!sii.peptidePtr->modification.empty())
            write_modification_info(xmlWriter, sii);

        using namespace boost::xpressive;
        smatch what;

        BOOST_FOREACH(const CVParam& cvParam, sii.cvParams)
        {
            if (cvParam.cvid != MS_number_of_matched_peaks &&
                cvParam.cvid != MS_number_of_unmatched_peaks &&
                (cvIsA(cvParam.cvid, MS_PSM_level_search_engine_specific_statistic) ||
                 regex_match(cvParam.value, what, numericRegex)))
            {
                const string& preferredScoreName = ScoreTranslator::instance->translate(analysisSoftwareCVID, cvParam.cvid);

                attributes.clear();
                attributes.add("name", preferredScoreName.empty() ? cvParam.name() : preferredScoreName);
                attributes.add("value", cvParam.value);
                xmlWriter.startElement("search_score", attributes, XMLWriter::EmptyElement);
            }
        }

        BOOST_FOREACH(const UserParam& userParam, sii.userParams)
            if (regex_match(userParam.value, what, numericRegex))
            {
                attributes.clear();
                attributes.add("name", userParam.name);
                attributes.add("value", userParam.value);
                xmlWriter.startElement("search_score", attributes, XMLWriter::EmptyElement);
            }
    }
    xmlWriter.endElement();
}

void write_spectrum_queries(XMLWriter& xmlWriter, const IdentData& mzid, const string& filepath,
                            const pwiz::util::IterationListenerRegistry* ilr)
{
    XMLWriter::Attributes attributes;

    int lastChargeState, lastSpectrumIndex;
    int spectrumIndex = 0;
    int queryIndex = 0;
    bool inSpectrumQuery = false;

    string basename = base_name(mzid, filepath);

    CVParam analysisSoftware = mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software);
    CVID nativeIdFormat = mzid.dataCollection.inputs.spectraData[0]->spectrumIDFormat.cvid;

    const SpectrumIdentificationList& sil = *mzid.dataCollection.analysisData.spectrumIdentificationList[0];
    int iterationCount = sil.spectrumIdentificationResult.size();
    BOOST_FOREACH(const SpectrumIdentificationResultPtr& sirPtr, sil.spectrumIdentificationResult)
    {
        const SpectrumIdentificationResult& sir = *sirPtr;

        if (ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(spectrumIndex, iterationCount, "writing spectrum queries")) == IterationListener::Status_Cancel)
            return;

        ++spectrumIndex;
        lastChargeState = 0;

        BOOST_FOREACH(const SpectrumIdentificationItemPtr& siiPtr, sir.spectrumIdentificationItem)
        {
            const SpectrumIdentificationItem& sii = *siiPtr;

            if (sii.chargeState != lastChargeState || spectrumIndex != lastSpectrumIndex)
            {
                // close the current spectrum_query
                if (queryIndex > 0)
                {
                    xmlWriter.endElement(); // search_result
                    xmlWriter.endElement(); // spectrum_query
                    inSpectrumQuery = false;
                }

                ++queryIndex;
                lastChargeState = sii.chargeState;
                lastSpectrumIndex = spectrumIndex;

                string scanNumber = msdata::id::translateNativeIDToScanNumber(nativeIdFormat, sir.spectrumID);
                if (scanNumber.empty())
                {
                    if (sir.hasCVParam(MS_peak_list_scans))
                        scanNumber = sir.cvParam(MS_peak_list_scans).value;
                    else if (sir.hasCVParam(MS_scan_number_s__OBSOLETE))
                        scanNumber = sir.cvParam(MS_scan_number_s__OBSOLETE).value;
                    else
                        scanNumber = lexical_cast<string>(spectrumIndex);
                }

                // basename.scanNumber.scanNumber.charge
                ostringstream conventionalSpectrumId;
                conventionalSpectrumId << basename << "." << scanNumber << "." << scanNumber << "." << sii.chargeState;

                attributes.clear();
                attributes.add("spectrum", conventionalSpectrumId.str());
                if (nativeIdFormat != MS_scan_number_only_nativeID_format)
                    attributes.add("spectrumNativeID", sir.spectrumID);
                attributes.add("start_scan", scanNumber);
                attributes.add("end_scan", scanNumber);
                attributes.add("precursor_neutral_mass",
                               Ion::neutralMass(sii.experimentalMassToCharge,
                                                sii.chargeState));
                attributes.add("assumed_charge", sii.chargeState);
                attributes.add("index", queryIndex);

                if (sir.hasCVParam(MS_scan_start_time))
                    attributes.add("retention_time_sec", sir.cvParam(MS_scan_start_time).timeInSeconds());
                else if (sir.hasCVParam(MS_retention_time))
                    attributes.add("retention_time_sec", sir.cvParam(MS_retention_time).timeInSeconds());
                else if (sir.hasCVParam(MS_retention_time_s__OBSOLETE))
                    attributes.add("retention_time_sec", sir.cvParam(MS_retention_time_s__OBSOLETE).timeInSeconds());

                xmlWriter.startElement("spectrum_query", attributes);

                attributes.clear();
                BOOST_FOREACH(const UserParam& userParam, sir.userParams)
                    attributes.add(userParam.name, userParam.value);

                xmlWriter.startElement("search_result", attributes);

                inSpectrumQuery = true;
            }

            write_search_hit(xmlWriter, analysisSoftware.cvid, mzid, sir, sii);
        }
    }

    // close the last spectrum_query
    if (inSpectrumQuery)
    {
        xmlWriter.endElement(); // search_result
        xmlWriter.endElement(); // spectrum_query
    }
}

} // namespace

PWIZ_API_DECL void Serializer_pepXML::write(ostream& os, const IdentData& mzid, const string& filepath,
                                            const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    // check for the minimum information to write a pepXML
    if (mzid.analysisCollection.spectrumIdentification.empty())
        throw runtime_error("[Serializer_pepXML::write] PepXML requires at least one spectrum identification.");

    if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol.empty())
        throw runtime_error("[Serializer_pepXML::write] PepXML requires at least one spectrum identification protocol.");

    if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->searchType != MS_ms_ms_search)
        throw runtime_error("[Serializer_pepXML::write] PepXML can only represent an MS/MS analysis.");

    if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.empty())
        throw runtime_error("[Serializer_pepXML::write] PepXML requires at least one enzyme in the spectrum identification protocol.");

    if (mzid.dataCollection.inputs.searchDatabase.size() > 1)
        throw runtime_error("[Serializer_pepXML::write] PepXML only supports a single search database.");

    if (mzid.dataCollection.inputs.spectraData.empty())
        throw runtime_error("[Serializer_pepXML::write] PepXML requires at least one SpectraData in the input list.");

    // instantiate XMLWriter

    XMLWriter::Config xmlConfig;
    XMLWriter xmlWriter(os, xmlConfig);

    string xmlData = "version=\"1.0\" encoding=\"ISO-8859-1\"";
    xmlWriter.processingInstruction("xml", xmlData);

    start_msms_pipeline_analysis(xmlWriter, mzid, filepath);

    BOOST_FOREACH(const AnalysisSoftwarePtr& as, mzid.analysisSoftwareList)
        write_analysis_summary(xmlWriter, mzid, *as);

    start_msms_run_summary(xmlWriter, mzid, filepath);

    write_sample_enzyme(xmlWriter, mzid);
    write_search_summary(xmlWriter, mzid, filepath);

    if (!mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
        write_spectrum_queries(xmlWriter, mzid, filepath, iterationListenerRegistry);

    xmlWriter.endElement(); // msms_run_summary
    xmlWriter.endElement(); // msms_pipeline_analysis
}




namespace {


struct HandlerSampleEnzyme : public SAXParser::Handler
{
    SpectrumIdentificationProtocol* _sip;

    HandlerSampleEnzyme(const CVTranslator& cvTranslator,
                        bool strict)
        : _cvTranslator(cvTranslator), strict(strict)
    {}

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
            enzyme->id = "ENZ_" + lexical_cast<string>(_sip->enzymes.enzymes.size()+1);
            enzyme->nTermGain = "H";
            enzyme->cTermGain = "OH";

            if (_fidelity == "semispecific")
                enzyme->terminalSpecificity = proteome::Digestion::SemiSpecific;
            else if (_fidelity == "nonspecific")
                enzyme->terminalSpecificity = proteome::Digestion::NonSpecific;

            string cut, noCut, sense;

            getAttribute(attributes, "cut", cut);
            getAttribute(attributes, "no_cut", noCut);
            bal::to_lower(getAttribute(attributes, "sense", sense));

            if (cut.empty())
                throw runtime_error("[HandlerSampleEnzyme] Empty cut attribute");

            if (sense == "n")
                enzyme->siteRegexp = (noCut.empty() ? "" : string("(?<!") + (noCut.length() > 1 ? "[" : "") + noCut + (noCut.length() > 1 ? "]" : "") + (noCut.empty() ? "" : ")")) +
                                     (cut.empty() ? "" : string("(?=") + (cut.length() > 1 ? "[" : "") + cut + (cut.length() > 1 ? "])" : ")"));
            else if (sense == "c")
                enzyme->siteRegexp = (cut.empty() ? "" : string("(?<=") + (cut.length() > 1 ? "[" : "") + cut + (cut.length() > 1 ? "])" : ")")) +
                                     (noCut.empty() ? "" : "(?!") + (noCut.length() > 1 ? "[" : "") + noCut + (noCut.length() > 1 ? "]" : "") + (noCut.empty() ? "" : ")");
            else
                throw runtime_error("[HandlerSampleEnzyme] Invalid specificity sense: " + sense);

            getAttribute(attributes, "min_spacing", enzyme->minDistance, 1);

            CVID cleavageAgent = Digestion::getCleavageAgentByRegex(enzyme->siteRegexp);
            if (cleavageAgent == CVID_Unknown)
                enzyme->enzymeName.userParams.push_back(UserParam(_name));
            else
                enzyme->enzymeName.set(cleavageAgent);

            _sip->enzymes.enzymes.push_back(enzyme);
            return Handler::Status::Ok;
        }
        else if (strict)
            throw runtime_error("[HandlerSampleEnzyme] Unexpected element name: " + name);

        return Status::Ok;
    }

    private:
    string _name, _description, _fidelity;
    const CVTranslator& _cvTranslator;
    bool strict;
};


struct HandlerSearchSummary : public SAXParser::Handler
{
    IdentData* _mzid;
    SpectrumIdentificationProtocol* _sip;

    HandlerSearchSummary(const CVTranslator& cvTranslator,
                         bool strict)
        : _cvTranslator(cvTranslator), strict(strict)
    {}

    CVID translateSearchEngine(const string& name)
    {
        // check to see if the software was added from an analysis_summary element
        AnalysisSoftwarePtr software;
        BOOST_FOREACH(AnalysisSoftwarePtr& as, _mzid->analysisSoftwareList)
            if (as->name == name)
            {
                software = as;
                break;
            }

        if (!software.get())
        {
            // TODO: replace this with CVTranslator (after making it filter for a parent term "analysis software")
            CVID result = AnalysisSoftwareTranslator::instance->translate(name);
            const string* preferredName;
            if (result == CVID_Unknown)
            {
                result = MS_custom_unreleased_software_tool;
                preferredName = &name;
            }
            else
                preferredName = &AnalysisSoftwareTranslator::instance->translate(result);
            software.reset(new AnalysisSoftware("AS_" + *preferredName, *preferredName));
            
            // TODO if MS_analysis_software log warning that search engine could not be translated
            if (result == MS_custom_unreleased_software_tool)
                software->softwareName.set(MS_custom_unreleased_software_tool, *preferredName);
            else
                software->softwareName.set(result);

            _mzid->analysisSoftwareList.push_back(software);
        }

        _sip->analysisSoftwarePtr = software;

        return software->softwareName.cvParams[0].cvid;
    }

    CVID translateToleranceUnits(const string& value)
    {
        if (bal::istarts_with(value, "da"))  return UO_dalton;
        if (bal::iequals(value, "ppm"))      return UO_parts_per_million;
        if (bal::iequals(value, "mmu"))      return UO_dalton; // special case
        if (bal::iequals(value, "%") ||
            bal::iequals(value, "percent"))  return UO_percent;
        return CVID_Unknown;
    }

    void translateIonSeriesConsidered(const string& ionSeriesList)
    {
        vector<string> tokens;
        bal::split(tokens, ionSeriesList, bal::is_any_of(","));
        BOOST_FOREACH(const string& ionSeries, tokens)
        {
            if (ionSeries == "immonium")                    _sip->additionalSearchParams.set(MS_param__immonium_ion);
            else if (bal::starts_with(ionSeries, "a"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__a_ion);
            else if (bal::starts_with(ionSeries, "b"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__b_ion);
            else if (bal::starts_with(ionSeries, "c"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__c_ion);
            else if (bal::starts_with(ionSeries, "x"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__x_ion);
            else if (bal::starts_with(ionSeries, "y"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__y_ion);
            else if (bal::starts_with(ionSeries, "z+1") ||
                     bal::starts_with(ionSeries, "z*"))     _sip->additionalSearchParams.cvParams.push_back(MS_param__z_1_ion);
            else if (bal::starts_with(ionSeries, "z+2"))    _sip->additionalSearchParams.cvParams.push_back(MS_param__z_2_ion);
            else if (bal::starts_with(ionSeries, "z"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__z_ion);
            else if (bal::starts_with(ionSeries, "d"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__d_ion);
            else if (bal::starts_with(ionSeries, "v"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__v_ion);
            else if (bal::starts_with(ionSeries, "w"))      _sip->additionalSearchParams.cvParams.push_back(MS_param__w_ion);

            if (bal::contains(ionSeries, "NH3"))            _sip->additionalSearchParams.cvParams.push_back(MS_NH3_neutral_loss_OBSOLETE);
            if (bal::contains(ionSeries, "H2O"))            _sip->additionalSearchParams.cvParams.push_back(MS_H2O_neutral_loss_OBSOLETE);
        }
    }

    static const string& getValueOrDefault(const std::map<string, string>& keyValueMap, const string& key, const string& defaultValue)
    {
        std::map<string, string>::const_iterator findItr = keyValueMap.find(key);
        return findItr == keyValueMap.end() ? defaultValue : findItr->second;
    }

    void translateParameters()
    {
        map<string, string> kvPairs;
        BOOST_FOREACH(const UserParam& userParam, _sip->additionalSearchParams.userParams)
            kvPairs[bal::to_lower_copy(userParam.name)] = userParam.value;

        // Unless the CV starts to map the search engine specific parameters to the proper CV terms,
        // there's not really a way to avoid hand coding these mappings

        // map "decoyprefix" from any search engine; this supports the mzid->pepXML->mzid path
        const string& decoyPrefix = getValueOrDefault(kvPairs, "decoyprefix", "");
        if (!decoyPrefix.empty())
            _mzid->dataCollection.inputs.searchDatabase[0]->set(MS_decoy_DB_accession_regexp, "^" + decoyPrefix);

        CVID searchEngine = _sip->analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software).cvid;

        switch (searchEngine)
        {
            case MS_Mascot:
                {
                    string parentTolerance = getValueOrDefault(kvPairs, "tol", "");
                    const string& parentToleranceUnits = getValueOrDefault(kvPairs, "tolu", "");
                    if (!parentTolerance.empty() && !parentToleranceUnits.empty())
                    {
                        CVID parentUnits = translateToleranceUnits(parentToleranceUnits);
                        if (bal::iequals(parentToleranceUnits, "mmu"))
                            parentTolerance = lexical_cast<string>(lexical_cast<double>(parentTolerance) / 1000);
                        _sip->parentTolerance.set(MS_search_tolerance_plus_value, parentTolerance, parentUnits);
                        _sip->parentTolerance.set(MS_search_tolerance_minus_value, parentTolerance, parentUnits);
                    }

                    string fragmentTolerance = getValueOrDefault(kvPairs, "itol", "");
                    const string& fragmentToleranceUnits = getValueOrDefault(kvPairs, "itolu", "");
                    if (!fragmentTolerance.empty() && !fragmentToleranceUnits.empty())
                    {
                        CVID fragmentUnits = translateToleranceUnits(fragmentToleranceUnits);
                        if (bal::iequals(fragmentToleranceUnits, "mmu"))
                            fragmentTolerance = lexical_cast<string>(lexical_cast<double>(fragmentTolerance) / 1000);
                        _sip->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentTolerance, fragmentUnits);
                        _sip->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentTolerance, fragmentUnits);
                    }

                    // set predicted fragment series
                    const string& instrument = getValueOrDefault(kvPairs, "instrument", "");
                    if (bal::iequals(instrument, "Default"))              translateIonSeriesConsidered("a,a-NH3,b,b-NH3,y,y-NH3");
                    else if (bal::iequals(instrument, "ESI-4-SECT"))      translateIonSeriesConsidered("immonium,a,a-NH3,a-H2O,b,b-NH3,b-H2O,y,z");
                    else if (bal::istarts_with(instrument, "ESI"))        translateIonSeriesConsidered("b,b-NH3,b-H2O,y,y-NH3,y-H2O");
                    else if (bal::icontains(instrument, "ECD") ||
                             bal::icontains(instrument, "ETD"))           translateIonSeriesConsidered("c,y,z+1,z+2");
                    else if (bal::iequals(instrument, "MALDI-QUAD-TOF"))  translateIonSeriesConsidered("immonium,b,b-NH3,b-H2O,y,y-NH3,y-H2O");
                    else
                    {
                        if (bal::icontains(instrument, "TOF"))            translateIonSeriesConsidered("immonium,a,a-NH3,a-H2O,b,b-NH3,b-H2O,y");
                        if (bal::iends_with(instrument, "TOF"))           translateIonSeriesConsidered("y-NH3, y-H2O");
                        if (bal::iends_with(instrument, "TOF-TOF"))       translateIonSeriesConsidered("d,v,w");
                    }
                }
                // TODO:
                // LICENSE: add as a Contact string
                // USEREMAIL: add as Contact's email
                // USERNAME: add as Contact's name
                break;

            case MS_MyriMatch:
            case MS_TagRecon:
            case MS_Pepitome:
                {
                    // newest MyriMatch uses a single MZTolerance variable with magnitude and units (e.g. 10ppm)
                    const string& precursorMzTolerance = getValueOrDefault(kvPairs, "config: precursormztolerance", "");
                    const string& precursorMzToleranceUnits = getValueOrDefault(kvPairs, "config: precursormztoleranceunits", "");
                    const string& precursorMzToleranceRule = getValueOrDefault(kvPairs, "config: precursormztolerancerule", "");

                    if (!precursorMzTolerance.empty() && !precursorMzToleranceUnits.empty())
                    {
                        CVID parentUnits = translateToleranceUnits(precursorMzToleranceUnits);
                        _sip->parentTolerance.set(MS_search_tolerance_minus_value, precursorMzTolerance, parentUnits);
                        _sip->parentTolerance.set(MS_search_tolerance_plus_value, precursorMzTolerance, parentUnits);
                    }
                    else if (!precursorMzToleranceRule.empty())
                    {
                        const string& avgPrecursorMzTolerance = getValueOrDefault(kvPairs, "config: avgprecursormztolerance", "");
                        const string& monoPrecursorMzTolerance = getValueOrDefault(kvPairs, "config: monoprecursormztolerance", "");
                        scoped_ptr<MZTolerance> parentTolerance;
                        if (precursorMzToleranceRule == "auto" || precursorMzToleranceRule == "mono")
                            parentTolerance.reset(new MZTolerance(lexical_cast<MZTolerance>(monoPrecursorMzTolerance)));
                        else if (precursorMzToleranceRule == "avg")
                            parentTolerance.reset(new MZTolerance(lexical_cast<MZTolerance>(avgPrecursorMzTolerance)));

                        if (parentTolerance)
                        {
                            _sip->parentTolerance.set(MS_search_tolerance_minus_value, parentTolerance->value, parentTolerance->units == MZTolerance::MZ ? UO_dalton : UO_parts_per_million);
                            _sip->parentTolerance.set(MS_search_tolerance_plus_value, parentTolerance->value, parentTolerance->units == MZTolerance::MZ ? UO_dalton : UO_parts_per_million);
                        }
                    }

                    // newest MyriMatch uses a single MZTolerance variable with magnitude and units (e.g. 10ppm)
                    const string& fragmentMzTolerance = getValueOrDefault(kvPairs, "config: fragmentmztolerance", "");
                    const string& fragmentMzToleranceUnits = getValueOrDefault(kvPairs, "config: fragmentmztoleranceunits", "");

                    if (!fragmentMzTolerance.empty() && !fragmentMzToleranceUnits.empty())
                    {
                        CVID fragmentUnits = translateToleranceUnits(fragmentMzToleranceUnits);
                        _sip->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentMzTolerance, fragmentUnits);
                        _sip->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentMzTolerance, fragmentUnits);
                    }
                    else if (!fragmentMzTolerance.empty())
                    {
                        MZTolerance fragmentTolerance = lexical_cast<MZTolerance>(fragmentMzTolerance);
                        _sip->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentTolerance.value, fragmentTolerance.units == MZTolerance::MZ ? UO_dalton : UO_parts_per_million);
                        _sip->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentTolerance.value, fragmentTolerance.units == MZTolerance::MZ ? UO_dalton : UO_parts_per_million);
                    }

                    const string& fragmentationRule = getValueOrDefault(kvPairs, "config: fragmentationrule", "cid");
                    if (bal::icontains(fragmentationRule, "cid"))     translateIonSeriesConsidered("b,y");
                    if (bal::icontains(fragmentationRule, "etd"))     translateIonSeriesConsidered("c,z+1");
                    if (bal::icontains(fragmentationRule, "manual"))  translateIonSeriesConsidered(bal::replace_all_copy(fragmentationRule, "manual:", "")); // skip "manual:"
                }
                break;

            case MS_X_Tandem:
                {
                    const string& parentErrorMinus = getValueOrDefault(kvPairs, "spectrum, parent monoisotopic mass error minus", "");
                    const string& parentErrorPlus = getValueOrDefault(kvPairs, "spectrum, parent monoisotopic mass error plus", "");
                    const string& parentErrorUnits = getValueOrDefault(kvPairs, "spectrum, parent monoisotopic mass error units", "");
                    CVID parentUnits = translateToleranceUnits(parentErrorUnits);
                    if (!parentErrorMinus.empty()) _sip->parentTolerance.set(MS_search_tolerance_minus_value, parentErrorMinus, parentUnits);
                    if (!parentErrorPlus.empty()) _sip->parentTolerance.set(MS_search_tolerance_plus_value, parentErrorPlus, parentUnits);

                    const string& fragmentError = getValueOrDefault(kvPairs, "spectrum, fragment monoisotopic mass error", "");
                    const string& fragmentErrorUnits = getValueOrDefault(kvPairs, "spectrum, fragment monoisotopic mass error units", "");
                    CVID fragmentUnits = translateToleranceUnits(fragmentErrorUnits);
                    if (!fragmentError.empty())
                    {
                        _sip->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentError, fragmentUnits);
                        _sip->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentError, fragmentUnits);
                    }

                    if (getValueOrDefault(kvPairs, "scoring, a ions", "") == "yes")   translateIonSeriesConsidered("a");
                    if (getValueOrDefault(kvPairs, "scoring, b ions", "") == "yes")   translateIonSeriesConsidered("b");
                    if (getValueOrDefault(kvPairs, "scoring, c ions", "") == "yes")   translateIonSeriesConsidered("c");
                    if (getValueOrDefault(kvPairs, "scoring, x ions", "") == "yes")   translateIonSeriesConsidered("x");
                    if (getValueOrDefault(kvPairs, "scoring, y ions", "") == "yes")   translateIonSeriesConsidered("y");
                    if (getValueOrDefault(kvPairs, "scoring, z ions", "") == "yes")   translateIonSeriesConsidered("z+1");
                }
                break;

            case MS_Comet:
                {
                    if (_sip->analysisSoftwarePtr->version.empty())
                        _sip->analysisSoftwarePtr->version = getValueOrDefault(kvPairs, "# comet_version ", "");

                    string parentTolerance = getValueOrDefault(kvPairs, "peptide_mass_tolerance", "");
                    string parentToleranceUnits = getValueOrDefault(kvPairs, "peptide_mass_units", "");
                    if (!parentTolerance.empty() && !parentToleranceUnits.empty())
                    {
                        if (parentToleranceUnits == "1") // 0 = amu, 1 = mmu, 2 = ppm
                            parentTolerance = lexical_cast<string>(lexical_cast<double>(parentTolerance) / 1000);
                        _sip->parentTolerance.set(MS_search_tolerance_minus_value, parentTolerance, parentToleranceUnits == "2" ? UO_parts_per_million : UO_dalton);
                        _sip->parentTolerance.set(MS_search_tolerance_plus_value, parentTolerance, parentToleranceUnits == "2" ? UO_parts_per_million : UO_dalton);
                    }

                    // TODO: is using fragment_bin_tol the right way to get a fragmentTolerance?
                    //<parameter name = "fragment_bin_offset" value = "0.400000" / >
                    //<parameter name = "fragment_bin_tol" value = "1.000500" / >
                    string fragmentTolerance = getValueOrDefault(kvPairs, "fragment_bin_tol", "");
                    if (!fragmentTolerance.empty())
                    {
                        _sip->fragmentTolerance.set(MS_search_tolerance_minus_value, fragmentTolerance, UO_dalton);
                        _sip->fragmentTolerance.set(MS_search_tolerance_plus_value, fragmentTolerance, UO_dalton);
                    }

                    bool use_nl_ions = getValueOrDefault(kvPairs, "use_nl_ions", "") == "1";
                    if (getValueOrDefault(kvPairs, "use_a_ions", "") == "1")   translateIonSeriesConsidered("a");
                    if (getValueOrDefault(kvPairs, "use_b_ions", "") == "1")   translateIonSeriesConsidered(use_nl_ions ? "b,b-H2O,b-NH3" : "b");
                    if (getValueOrDefault(kvPairs, "use_c_ions", "") == "1")   translateIonSeriesConsidered("c");
                    if (getValueOrDefault(kvPairs, "use_x_ions", "") == "1")   translateIonSeriesConsidered("x");
                    if (getValueOrDefault(kvPairs, "use_y_ions", "") == "1")   translateIonSeriesConsidered(use_nl_ions ? "y,y-H2O,y-NH3" : "y");
                    if (getValueOrDefault(kvPairs, "use_z_ions", "") == "1")   translateIonSeriesConsidered("z+1");

                    const string& decoyPrefix = getValueOrDefault(kvPairs, "decoy_prefix", "");
                    if (!decoyPrefix.empty())
                        _mzid->dataCollection.inputs.searchDatabase[0]->set(MS_decoy_DB_accession_regexp, "^" + decoyPrefix);
                }
                break;

            // TODO: add more search engines

            default:
                // TODO: something with parameters from unknown search engines?
                break;
        }
    }

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "search_summary")
        {
            string basename, searchEngine, precursorMassType, fragmentMassType;
            getAttribute(attributes, "base_name", basename);
            getAttribute(attributes, "search_engine", searchEngine);
            getAttribute(attributes, "precursor_mass_type", precursorMassType);
            getAttribute(attributes, "fragment_mass_type", fragmentMassType);

            translateSearchEngine(searchEngine);

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

            _mzid->dataCollection.inputs.searchDatabase.push_back(SearchDatabasePtr(new SearchDatabase("DB_1")));
            _mzid->dataCollection.inputs.searchDatabase.back()->fileFormat.cvid = MS_FASTA_format;
            _mzid->analysisCollection.spectrumIdentification[0]->searchDatabase.push_back(_mzid->dataCollection.inputs.searchDatabase.back());
        }
        else if (name == "search_database")
        {
            SearchDatabasePtr searchDatabase = _mzid->dataCollection.inputs.searchDatabase.back();

            string type;
            getAttribute(attributes, "local_path", searchDatabase->location);
            getAttribute(attributes, "database_name", searchDatabase->id);
            getAttribute(attributes, "database_release_identifier", searchDatabase->version);
            getAttribute(attributes, "size_in_db_entries", searchDatabase->numDatabaseSequences);
            getAttribute(attributes, "size_of_residues", searchDatabase->numResidues);
            bal::to_lower(getAttribute(attributes, "type", type));

            if (searchDatabase->id.empty())
            {
                searchDatabase->id = "DB_1";
                searchDatabase->name = searchDatabase->location.empty() ? searchDatabase->id : BFS_STRING(bfs::path(bal::replace_all_copy(searchDatabase->location, "\\", "/")).filename());
            }

            if (type == "aa")
                searchDatabase->set(MS_database_type_amino_acid);
            else if (type == "na")
                searchDatabase->set(MS_database_type_nucleotide);
            else
                throw runtime_error("[HandlerSearchSummary] Invalid database type: " + type);
        }
        else if (name == "enzymatic_search_constraint")
        {
            string enzyme;
            int minTermini, missedCleavages;
            getAttribute(attributes, "enzyme", enzyme);
            getAttribute(attributes, "max_num_internal_cleavages", missedCleavages, 0);
            getAttribute(attributes, "min_number_termini", minTermini, 2);
            BOOST_FOREACH(const EnzymePtr& ez, _sip->enzymes.enzymes)
            {
                ez->terminalSpecificity = (proteome::Digestion::Specificity) minTermini;
                ez->missedCleavages = missedCleavages;
            }
        }
        else if (name == "aminoacid_modification")
        {
            string aminoacid, variable, peptideTerminus;
            getAttribute(attributes, "aminoacid", aminoacid);
            bal::to_lower(getAttribute(attributes, "variable", variable));
            bal::to_lower(getAttribute(attributes, "peptide_terminus", peptideTerminus));
            
            try
            {
                AminoAcid::Info::record(aminoacid[0]); // make sure the AA is supported by pwiz (ignore it if it throws)

                SearchModificationPtr searchModification(new SearchModification);
                getAttribute(attributes, "massdiff", searchModification->massDelta);
                if (!aminoacid.empty())
                    searchModification->residues.push_back(aminoacid[0]);
                if (variable == "y" || variable == "n")
                    searchModification->fixedMod = (variable == "n");
                else
                    searchModification->fixedMod = lexical_cast<bool>(variable);

                if (bal::icontains(peptideTerminus, "n"))
                    searchModification->specificityRules.cvid = MS_modification_specificity_peptide_N_term;
                else if (bal::icontains(peptideTerminus, "c"))
                    searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;

                _sip->modificationParams.push_back(searchModification);

                // in the case of either terminus, duplicate the mod with C terminal specificity
                if (peptideTerminus == "nc")
                {
                    searchModification.reset(new SearchModification(*searchModification));
                    searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;
                    _sip->modificationParams.push_back(searchModification);
                }
            }
            catch (runtime_error& e)
            {
                // ignore unsupported AA
                if (!bal::icontains(e.what(), "Invalid amino acid symbol"))
                    throw e;
            }
        }
        else if (name == "terminal_modification")
        {
            string terminus, variable, proteinTerminus;
            bal::to_lower(getAttribute(attributes, "terminus", terminus));
            bal::to_lower(getAttribute(attributes, "variable", variable));
            bal::to_lower(getAttribute(attributes, "protein_terminus", proteinTerminus));
            
            SearchModificationPtr searchModification(new SearchModification);
            getAttribute(attributes, "massdiff", searchModification->massDelta);
            searchModification->fixedMod = !(variable == "y" || lexical_cast<bool>(variable));
            searchModification->residues.push_back('.');
            
            if (bal::icontains(proteinTerminus, "n"))
                searchModification->specificityRules.cvid = MS_modification_specificity_protein_N_term;
            else if (bal::icontains(proteinTerminus, "c"))
                searchModification->specificityRules.cvid = MS_modification_specificity_protein_C_term;
            else if (terminus == "n")
                searchModification->specificityRules.cvid = MS_modification_specificity_peptide_N_term;
            else if (terminus == "c")
                searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;

            _sip->modificationParams.push_back(searchModification);

            // in the case of either terminus, duplicate the mod with C terminal specificity
            if (proteinTerminus == "nc")
            {
                searchModification.reset(new SearchModification(*searchModification));
                searchModification->specificityRules.cvid = MS_modification_specificity_peptide_C_term;
                _sip->modificationParams.push_back(searchModification);
            }
        }
        else if (name == "parameter")
        {
            string name, value;
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "value", value);
            _sip->additionalSearchParams.userParams.push_back(UserParam(name, value));
        }
        else if (strict)
            throw runtime_error("[HandlerSearchSummary] Unexpected element "
                                "name: " + name);

        return Handler::Status::Ok;
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        if (name == "search_summary")
        {
            // after all parameters are read, translate the userParams to cvParams where possible;
            // we need to wait until we have them all because interpretation of them sometimes depends on other parameters which not be read yet
            translateParameters();
        }
        return Handler::Status::Ok;
    }

    private:
    const CVTranslator& _cvTranslator;
    bool strict;
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
    IdentData* _mzid;
    SpectrumIdentificationProtocol* _sip;
    SpectrumIdentificationList* _sil;
    CVID nativeIdFormat;

    HandlerSearchResults(const CVTranslator& cvTranslator,
                         const IterationListenerRegistry* iterationListenerRegistry,
                         bool strict)
    :   _nTerm("H1"),
        _cTerm("O1H1"),
        siiCount(0), peptideCount(0), spectrumQueryCount(0),
        _cvTranslator(cvTranslator),
        ilr(iterationListenerRegistry),
        strict(strict)
    {
    }

    bool setDBSequenceParams(const string& accession,
                             const ParamContainer& params)
    {
        map<string, DBSequencePtr>::iterator result =
            _dbSequences.find(accession);

        if (result == _dbSequences.end())
            return false;
        
        copy(params.cvParams.begin(), params.cvParams.end(),
             (*result).second->cvParams.begin());

        copy(params.userParams.begin(), params.userParams.end(),
             (*result).second->userParams.begin());
    }
    
    DBSequencePtr getDBSequence(const string& accession)
    {
        pair<map<string, DBSequencePtr>::iterator, bool> insertResult = _dbSequences.insert(make_pair(accession, DBSequencePtr()));

        DBSequencePtr& dbSequence = insertResult.first->second;

        if (insertResult.second)
        {
            dbSequence.reset(new DBSequence);
            _mzid->sequenceCollection.dbSequences.push_back(dbSequence);

            // IdentData::dataCollection is populated in HandlerSearchSummary
            dbSequence->searchDatabasePtr = _mzid->dataCollection.inputs.searchDatabase[0];
            dbSequence->accession = accession;
            dbSequence->id = "DBSeq_" + accession;
        }
        return dbSequence;
    }

    void translateScore(SpectrumIdentificationItem& sii, const Attributes& attributes)
    {
        CVParam searchEngine = _sip->analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software);

        string scoreName;
        getAttribute(attributes, "name", scoreName);

        CVID controlledScoreName = ScoreTranslator::instance->translate(searchEngine.cvid, scoreName);

        if (controlledScoreName == CVID_Unknown)
        {
            sii.userParams.push_back(UserParam());
            UserParam& score = sii.userParams.back();
            swap(scoreName, score.name);
            getAttribute(attributes, "value", score.value);
            score.type = "xsd:float";
        }
        else
        {
            sii.cvParams.push_back(CVParam(controlledScoreName));
            getAttribute(attributes, "value", sii.cvParams.back().value);
        }
    }

    virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)
    {
        if (name == "search_score")
        {
            SpectrumIdentificationItem& sii = *_sir->spectrumIdentificationItem.back();

            // search_score comes after <alternative_protein> and <modification_info>,
            // so once we get here we can check if the peptide is already in sequenceCollection
            if (_currentPeptide->id.empty())
            {
                // the PeptideLessThan comparator assumes the modifications are sorted
                sort(_currentPeptide->modification.begin(), _currentPeptide->modification.end(), ModLessThan());

                // try to insert the current peptide variant
                pair<map<PeptidePtr, vector<PeptideEvidencePtr>, PeptideLessThan>::iterator, bool> insertResult =
                    _peptides.insert(make_pair(_currentPeptide, vector<PeptideEvidencePtr>()));
                _currentPeptide = insertResult.first->first;

                // if the variant was added, give it an id
                if (insertResult.second)
                {
                    _currentPeptide->id = "PEP_";
                    _currentPeptide->id += lexical_cast<string>(++peptideCount);
                    _mzid->sequenceCollection.peptides.push_back(_currentPeptide);

                    // add PeptideEvidence elements for this peptide variant
                    BOOST_FOREACH(const Attributes& proteinAttributes, _currentProteinAttributes)
                    {
                        PeptideEvidencePtr pe(new PeptideEvidence);
                        _mzid->sequenceCollection.peptideEvidence.push_back(pe);
                        insertResult.first->second.push_back(pe);

                        string accession;
                        getAttribute(proteinAttributes, "protein", accession);

                        pe->id = accession;
                        pe->id += "_";
                        pe->id += _currentPeptide->id;

                        pe->peptidePtr = _currentPeptide;
                        pe->dbSequencePtr = getDBSequence(accession);

                        string proteinDescr;
                        getAttribute(proteinAttributes, "protein_descr", proteinDescr);
                        if (!proteinDescr.empty())
                            pe->dbSequencePtr->set(MS_protein_description, proteinDescr);

                        getAttribute(proteinAttributes, "peptide_prev_aa", pe->pre, '?');
                        getAttribute(proteinAttributes, "peptide_next_aa", pe->post, '?');
                    }
                }

                sii.peptideEvidencePtr = insertResult.first->second;

                // the peptide is guaranteed to exist, so reference it
                sii.peptidePtr = _currentPeptide;
            }

            translateScore(sii, attributes);
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
            mod.residues.push_back(modifiedResidue);
        }
        else if (name == "alternative_protein")
        {
            _currentProteinAttributes.push_back(attributes);
        }
        else if (name == "search_hit")
        {
            // only add the SpectrumIdentificationResult if there is at least one SpectrumIdentificationItem
            if (_sir->spectrumIdentificationItem.empty())
                _sil->spectrumIdentificationResult.push_back(_sir);

            _sir->spectrumIdentificationItem.push_back(SpectrumIdentificationItemPtr(new SpectrumIdentificationItem(_sii)));
            SpectrumIdentificationItem& sii = *_sir->spectrumIdentificationItem.back();
            sii.id = "SII_" + lexical_cast<string>(++siiCount);
            getAttribute(attributes, "hit_rank", sii.rank);
            getAttribute(attributes, "calc_neutral_pep_mass", sii.calculatedMassToCharge);
            sii.calculatedMassToCharge = Ion::mz(sii.calculatedMassToCharge, sii.chargeState);

            string matchedIons, totalIons;
            getAttribute(attributes, "num_matched_ions", matchedIons);
            if (!matchedIons.empty())
            {
                sii.set(MS_number_of_matched_peaks, matchedIons);

                getAttribute(attributes, "tot_num_ions", totalIons);
                if (!totalIons.empty())
                    sii.set(MS_number_of_unmatched_peaks, lexical_cast<int>(totalIons) - lexical_cast<int>(matchedIons));
            }

            _currentPeptide.reset(new Peptide);
            getAttribute(attributes, "peptide", _currentPeptide->peptideSequence);

            _currentProteinAttributes.clear();
            _currentProteinAttributes.push_back(attributes);
        }
        else if (name == "spectrum_query")
        {
            // only send the 0 count for the first spectrum_query
            if ((++spectrumQueryCount == 1 || !_sil->spectrumIdentificationResult.empty()) &&
                ilr && ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(_sil->spectrumIdentificationResult.size(), 0, "reading spectrum queries")) == IterationListener::Status_Cancel)
                return Status::Done;

            string spectrum;
            getAttribute(attributes, "spectrum", spectrum);

            string spectrumWithoutCharge = stripChargeFromConventionalSpectrumId(spectrum);

            SpectrumIdentificationResultPtr& sir = _resultMap[spectrumWithoutCharge];
            if (!sir.get())
            {
                string spectrumNativeID;
                getAttribute(attributes, "spectrumNativeID", spectrumNativeID);
                if (spectrumNativeID.empty())
                {
                    if (nativeIdFormat != MS_scan_number_only_nativeID_format)
                        spectrumNativeID = spectrum;
                    else
                    {
                        string start_scan;
                        getAttribute(attributes, "start_scan", start_scan);
                        spectrumNativeID = msdata::id::translateScanNumberToNativeID(nativeIdFormat, start_scan);
                        if (spectrumNativeID.empty())
                            spectrumNativeID = "scan=" + start_scan;
                    }
                }

                sir.reset(new SpectrumIdentificationResult);
                sir->id = "SIR_" + lexical_cast<string>(_sil->spectrumIdentificationResult.size()+1);
                sir->spectrumID = spectrumNativeID;
                sir->name = spectrumWithoutCharge;
                sir->spectraDataPtr = _mzid->dataCollection.inputs.spectraData[0];

                double retentionTimeSec;
                getAttribute(attributes, "retention_time_sec", retentionTimeSec);
                if (retentionTimeSec > 0)
                    sir->set(MS_scan_start_time, retentionTimeSec, UO_second);
            }

            _sir = sir;


            double precursorNeutralMass;
            getAttribute(attributes, "precursor_neutral_mass", precursorNeutralMass);
            getAttribute(attributes, "assumed_charge", _sii.chargeState);
            _sii.experimentalMassToCharge = Ion::mz(precursorNeutralMass, _sii.chargeState);
            _sii.passThreshold = true;
        }
        else if (name == "search_result")
        {
            // some engines write custom attributes here; we transcode them as UserParams
            for (Attributes::attribute_list::const_iterator it = attributes.begin(); it != attributes.end(); ++it)
                _sir->userParams.push_back(UserParam(it->getName(), it->getValue()));
        }
        else if (strict)
            throw runtime_error("[HandlerSearchResults] Unexpected element name: " + name);

        return Status::Ok;
    }

    private:
    SpectrumIdentificationResultPtr _sir;
    SpectrumIdentificationItem _sii;
    map<string, DBSequencePtr> _dbSequences;
    map<string, SpectrumIdentificationResultPtr> _resultMap;
    PeptidePtr _currentPeptide;
    Formula _nTerm, _cTerm;
    boost::xpressive::smatch what;
    int siiCount, peptideCount, spectrumQueryCount;
    const CVTranslator& _cvTranslator;
    const IterationListenerRegistry* ilr;
    bool strict;

    // maps a modified peptide to its PeptideEvidences
    map<PeptidePtr, vector<PeptideEvidencePtr>, PeptideLessThan> _peptides;

    // Attributes from <search_hit> and <alternative_protein>s for the current SII;
    // parsing is delayed until the <modification_info> block is parsed
    vector<SAXParser::Handler::Attributes> _currentProteinAttributes;
};


struct Handler_pepXML : public SAXParser::Handler
{
    IdentData& mzid;

    Handler_pepXML(IdentData& mzid,
                   bool readSpectrumQueries,
                   const IterationListenerRegistry* iterationListenerRegistry,
                   bool strict)
    :   mzid(mzid),
        handlerSampleEnzyme(cvTranslator, strict),
        handlerSearchSummary(cvTranslator, strict),
        handlerSearchResults(cvTranslator, iterationListenerRegistry, strict),
        readSpectrumQueries(readSpectrumQueries),
        ilr(iterationListenerRegistry),
        strict(strict)
    {
        // add default CVs
        mzid.cvs = defaultCVList();

        // add the SpectrumIdentificationProtocol
        SpectrumIdentificationProtocolPtr sipPtr(new SpectrumIdentificationProtocol("SIP"));
        mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sipPtr);

        sipPtr->searchType.cvid = MS_ms_ms_search;

        handlerSearchSummary._mzid = &mzid;
        handlerSearchSummary._sip = sipPtr.get();
        handlerSampleEnzyme._sip = sipPtr.get();
        handlerSearchResults._mzid = &mzid;
        handlerSearchResults._sip = sipPtr.get();

        SpectrumIdentificationListPtr silPtr;

        if (readSpectrumQueries)
        {
            // add the SpectrumIdentificationList
            silPtr.reset(new SpectrumIdentificationList("SIL"));
            mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(silPtr);
            handlerSearchResults._sil = silPtr.get();
        }

        // add the SpectrumIdentification
        SpectrumIdentificationPtr siPtr(new SpectrumIdentification("SI"));
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
            string summaryXml;
            getAttribute(attributes, "date", mzid.creationDate);
            getAttribute(attributes, "summary_xml", summaryXml);

            /*if (!summaryXml.empty())
            {
                SourceFilePtr sourceFile = SourceFilePtr(new SourceFile);
                sourceFile->id = "SF_1";
                sourceFile->name = bfs::path(summaryXml).filename();
                sourceFile->location = summaryXml;
                mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);
            }*/
            return Status::Ok;
        }
        else if (name == "analysis_summary")
        {
            string name, version, time;
            getAttribute(attributes, "analysis", name);
            getAttribute(attributes, "time", time);

            CVID result = AnalysisSoftwareTranslator::instance->translate(name);
            const string* preferredName;
            if (result == CVID_Unknown)
            {
                result = MS_custom_unreleased_software_tool;
                preferredName = &name;
            }
            else
                preferredName = &AnalysisSoftwareTranslator::instance->translate(result);

            AnalysisSoftwarePtr software(new AnalysisSoftware);
            getAttribute(attributes, "version", software->version);
            software->id = "AS_" + *preferredName + "_" + software->version;
            software->name = *preferredName;

            // TODO if MS_analysis_software log warning that search engine could not be translated
            if (result == MS_custom_unreleased_software_tool)
                software->softwareName.set(MS_custom_unreleased_software_tool, *preferredName);
            else
                software->softwareName.set(result);

            mzid.analysisSoftwareList.push_back(software);

            if (!time.empty() && mzid.analysisCollection.spectrumIdentification[0]->activityDate.empty())
                mzid.analysisCollection.spectrumIdentification[0]->activityDate = time;

            return Status::Ok;
        }
        else if (name == "msms_run_summary")
        {
            SpectraDataPtr spectraData(new SpectraData("SD"));
            getAttribute(attributes, "base_name", spectraData->location);
            spectraData->name = BFS_STRING(bfs::path(spectraData->location).filename());

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
            // determine nativeID format from first spectrum_query
            string spectrumNativeID;
            getAttribute(attributes, "spectrumNativeID", spectrumNativeID);
            if (spectrumNativeID.empty())
                getAttribute(attributes, "spectrum", spectrumNativeID);

            CVID nativeIdFormat = NativeIdTranslator::instance->translate(spectrumNativeID);
            if (nativeIdFormat == CVID_Unknown)
                nativeIdFormat = MS_scan_number_only_nativeID_format;

            mzid.dataCollection.inputs.spectraData[0]->spectrumIDFormat.cvid = nativeIdFormat;

            if (readSpectrumQueries)
            {
                handlerSearchResults.nativeIdFormat = nativeIdFormat;
                return Status(Status::Delegate, &handlerSearchResults);
            }
            return Status::Done;
        }
        else if (strict)
            throw runtime_error("[Handler_pepXML] Unexpected element name: " +
                                name);

        return Status::Ok;
    }

    private:
    CVTranslator cvTranslator;
    HandlerSampleEnzyme handlerSampleEnzyme;
    HandlerSearchSummary handlerSearchSummary;
    HandlerSearchResults handlerSearchResults;

    bool readSpectrumQueries;
    const IterationListenerRegistry* ilr;
    bool strict;
};

} // namespace


PWIZ_API_DECL void Serializer_pepXML::read(boost::shared_ptr<std::istream> is, IdentData& mzid,
                                           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    bool strict = false;
    
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_pepXML::read()] Bad istream.");

    is->seekg(0);

    Handler_pepXML handler(mzid, config_.readSpectrumQueries,
                           iterationListenerRegistry, strict);
    SAXParser::parse(*is, handler);

    // final iteration update
    if (iterationListenerRegistry &&
        !mzid.dataCollection.analysisData.spectrumIdentificationList.empty() &&
        iterationListenerRegistry->broadcastUpdateMessage(
            IterationListener::UpdateMessage(mzid.dataCollection.analysisData.spectrumIdentificationList[0]->spectrumIdentificationResult.size()-1,
                                             mzid.dataCollection.analysisData.spectrumIdentificationList[0]->spectrumIdentificationResult.size(),
                                             "reading spectrum queries")) == IterationListener::Status_Cancel)
        return;

    // there can be only one
    snapModificationsToUnimod(*mzid.analysisCollection.spectrumIdentification[0]);
}


namespace {
  
const string allResidues = "ABCDEFGHIJKLMNOPQRSTUVWYZ";
string& invertResidueSet(string& residues)
{
    set<char> allResidueSet;
    allResidueSet.insert(allResidues.begin(), allResidues.end());

    set<char> residueSet;
    residueSet.insert(residues.begin(), residues.end());

    string result;
    boost::range::set_difference(allResidueSet, residueSet, std::back_inserter(result));

    swap(residues, result);
    return residues;
}

// match zero or one regex term like (?<=[KR]) or (?<=K) or (?<![KR]) or (?<!K)
// followed by zero or one term like (?=[KR]) or (?=K) or (?![KR]) or (?!K)
// 4 capture groups: [!=] [A-Z] for each look: 0                1                        2                3
const bxp::sregex cutNoCutRegex = bxp::sregex::compile("(?:\\(+\\?<([=!])(\\[[A-Z]+\\]|[A-Z])\\)+)?(?:\\(+\\?([=!])(\\[[A-Z]+\\]|[A-Z])\\)+)?");

} // namespace


PWIZ_API_DECL PepXMLSpecificity pepXMLSpecificity(const Enzyme& ez)
{
    PepXMLSpecificity result;
    string &cut = result.cut, &nocut = result.no_cut, &sense = result.sense;

    bxp::smatch what;
    if (ez.siteRegexp.empty() || !bxp::regex_match(ez.siteRegexp, what, cutNoCutRegex))
    {
        CVID cleavageAgent = identdata::cleavageAgent(ez);

        switch (cleavageAgent)
        {
            case MS_Trypsin:                cut="KR"; nocut="P"; sense="C"; break;
            case MS_Arg_C:                  cut="R"; nocut="P"; sense="C"; break;
            case MS_Asp_N:                  cut="BD"; nocut=""; sense="N"; break;
            case MS_Asp_N_ambic:            cut="DE"; nocut=""; sense="N"; break;
            case MS_Chymotrypsin:           cut="FYWL"; nocut="P"; sense="C"; break;
            case MS_CNBr:                   cut="M"; nocut=""; sense="C"; break;
            case MS_Formic_acid:            cut="D"; nocut=""; sense="C"; break;
            case MS_Lys_C:                  cut="K"; nocut="P"; sense="C"; break;
            case MS_Lys_C_P:                cut="K"; nocut=""; sense="C"; break;
            case MS_PepsinA:                cut="FL"; nocut=""; sense="C"; break;
            case MS_TrypChymo:              cut="KRFYWL"; nocut="P"; sense="C"; break;
            case MS_Trypsin_P:              cut="KR"; nocut=""; sense="C"; break;
            case MS_V8_DE:                  cut="BDEZ"; nocut="P"; sense="C"; break;
            case MS_V8_E:                   cut="EZ"; nocut="P"; sense="C"; break;
            case MS_proline_endopeptidase:  cut="P"; nocut="P"; sense="C"; break;
            case MS_glutamyl_endopeptidase: cut="E"; nocut=""; sense="C"; break;
            case MS_leukocyte_elastase:     cut="ALIV"; nocut="P"; sense="C"; break;
            case MS_2_iodobenzoate:         cut="W"; nocut=""; sense="C"; break;
            case MS_unspecific_cleavage:    cut="X"; nocut=""; sense="C"; break;
            case MS_no_cleavage:            cut=""; nocut=""; sense="C"; break;
            default:
                throw runtime_error("[pepXMLSpecificity] No case supporting enzyme \"" + cvTermInfo(cleavageAgent).name + "\"");
        }
    }
    else
    {
        bool hasLookbehind = what[1].matched && what[2].matched;
        bool hasLookahead = what[3].matched && what[4].matched;
        bool lookbehindIsPositive = hasLookbehind && what[1] == "=";
        bool lookaheadIsPositive = hasLookahead && what[3] == "=";
        string lookbehindResidues = hasLookbehind ? bal::trim_copy_if(what[2].str(), bal::is_any_of("[]")) : string();
        string lookaheadResidues = hasLookahead ? bal::trim_copy_if(what[4].str(), bal::is_any_of("[]")) : string();

        // if both looks are empty, throw an exception
        if (!hasLookbehind && !hasLookahead)
            throw runtime_error("[pepXMLSpecificity] No lookbehind or lookahead expressions found in \"" + ez.siteRegexp + "\"");

        // if both looks are positive, invert the lookahead residue set to be the "no_cut" set;
        // if both looks are negative, invert the lookbehind residue set to be the "cut" set (unless it's empty)
        if (lookbehindIsPositive && lookaheadIsPositive)
        {
            // convert lookahead to negative
            sense = "C";
            cut = lookbehindResidues;
            nocut = invertResidueSet(lookaheadResidues);
        }
        else if (!lookbehindIsPositive && !lookaheadIsPositive)
        {
            // if lookbehind is empty, convert lookahead to positive
            if (!hasLookbehind)
            {
                sense = "N";
                cut = invertResidueSet(lookaheadResidues);
            }
            else
            {
                // convert lookbehind to positive
                sense = "C";
                cut = invertResidueSet(lookbehindResidues);
                nocut = lookaheadResidues;
            }
        }
        else if (lookbehindIsPositive)
        {
            sense = "C";
            cut = lookbehindResidues;
            nocut = lookaheadResidues;
        }
        else if (lookaheadIsPositive)
        {
            sense = "N";
            cut = lookaheadResidues;
            nocut = lookbehindResidues;
        }
    }

    return result;
}


PWIZ_API_DECL string stripChargeFromConventionalSpectrumId(const string& id)
{
    // basename.123.123.2
    // basename.123.123
    // basename.ext.123.123.2
    // basename.ext.123.123
    // basename.2.2.2 (scan number same as charge state)
    // basename.ext.3.3.3 (scan number same as charge state)
    // Locus:w.x.y.z.charge

    size_t lastDot = id.find_last_of(".");
    if (lastDot == string::npos)
        return id;

    // there is no repeating scan number, so assume there is a charge segment
    if (bal::istarts_with(id, "Locus:"))
        return id.substr(0, lastDot);

    // with only one dot, it's not a conventional id
    size_t nextToLastDot = id.find_last_of(".", lastDot-1);
    if (nextToLastDot == string::npos)
        return id;

    // with only two dots (all cases return id unchanged):
    // * charge either must already be stripped (basename.123.123)
    // * it's a scan range (basename.123.125)
    // * scan number doesn't repeat, so it's not a conventional id (basename.123.2)
    size_t nextToNextToLastDot = id.find_last_of(".", nextToLastDot-1);
    if (nextToNextToLastDot == string::npos)
        return id;

    // with three dots, charge is probably not stripped, but it could be equal to the scan number
    // if the substring between the next to next to last and the next to last dot is the same as after the next last dot,
    // the charge is not stripped
    if (bal::equals(boost::make_iterator_range(id.begin()+nextToNextToLastDot+1, id.begin()+nextToLastDot),
                    boost::make_iterator_range(id.begin()+nextToLastDot+1, id.begin()+lastDot)))
        return id.substr(0, lastDot);

    return id;
}


PWIZ_API_DECL CVID pepXMLSoftwareNameToCVID(const std::string& softwareName)
{
    return AnalysisSoftwareTranslator::instance->translate(softwareName);
}

PWIZ_API_DECL const std::string& softwareCVIDToPepXMLSoftwareName(CVID softwareCVID)
{
    return AnalysisSoftwareTranslator::instance->translate(softwareCVID);
}

PWIZ_API_DECL CVID pepXMLScoreNameToCVID(CVID softwareCVID, const std::string& scoreName)
{
    return ScoreTranslator::instance->translate(softwareCVID, scoreName);
}

PWIZ_API_DECL const std::string& scoreCVIDToPepXMLScoreName(CVID softwareCVID, CVID scoreCVID)
{
    return ScoreTranslator::instance->translate(softwareCVID, scoreCVID);
}

PWIZ_API_DECL CVID nativeIdStringToCVID(const std::string& id)
{
    return NativeIdTranslator::instance->translate(id);
}


} // namespace identdata
} // namespace pwiz

