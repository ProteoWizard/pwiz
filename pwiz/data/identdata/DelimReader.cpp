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

#include "DelimReader.hpp"
#include <pwiz/utility/misc/Filesystem.hpp>
#include <pwiz/utility/misc/Std.hpp>
#include <boost/tokenizer.hpp>

#include <boost/algorithm/string/predicate.hpp>
#include <map>

namespace pwiz {
namespace identdata {

using namespace pwiz::util;
using namespace boost;

struct DBSequenceMatch
{
    const string seq;
    const string accession;
    
    DBSequenceMatch(const string _seq,
                    const string _accession)
        : seq(_seq), accession(_accession)
    {
    }
    
    bool operator()(const DBSequencePtr& dbs) const
    {
        return dbs.get() && dbs->seq == seq &&
            dbs->accession == accession;
    }
};

struct ExtType
{
    const char* extension;
    const char* type;
    char  field_sep;
    char  record_sep;
};

ExtType extType_[] =
{
    {".tab", "tab separated", '\t', '\n'},
    {".csv", "comma separated", ',', '\n'},
}; // extType_

const size_t extTypeSize_ = sizeof(extType_) / sizeof(ExtType);

CVID commonTimeUnits(const string& units)
{
    CVID unitsCVID = CVID_Unknown;

    if (units == "second")
        unitsCVID = UO_second;
    else if (units == "minute")
        unitsCVID = UO_minute;
    else if (units == "hour")
        unitsCVID = UO_hour;
    else if (units == "day")
        unitsCVID = UO_day;
    else if (units == "week")
        unitsCVID = UO_week;
    else if (units == "month")
        unitsCVID = UO_month;

    return unitsCVID;
}

//
// class DelimReader::Impl
//
class DelimReader::Impl
{
public:
    Impl() : type(0), nextDBSIndex(1)
    {
    }

    CVID mapScore(const string& value);
    
    ExtType* type;

    std::map<Param, string> params;

    int nextDBSIndex;

    string id(const string& prefix, int& index)
    {
        ostringstream oss;
        oss << prefix;
        oss << index++;

        return oss.str();
    }
};

//
// class DelimReader
//
DelimReader::DelimReader()
    : pimpl(new Impl())
{
}

DelimReader::~DelimReader()
{
}

DelimReader& DelimReader::set(Param param, const string& value)
{
    pimpl->params[param].assign(value.begin(), value.end());

    return *this;
}

const string DelimReader::get(Param param) const
{
    if (pimpl->params.find(param)!=pimpl->params.end())
        return pimpl->params[param];

    return "";
}

string DelimReader::identify(const string& filename, const string& head) const
{
    const char* result = "";
    
    for (ExtType* et=extType_; et!=extType_+extTypeSize_; ++et)
    {
        if (iends_with(filename, et->extension))
        {
            pimpl->type = et;
            result = et->type;
            break;
        }
    }

    return result;
}

void DelimReader::read(const string& filename,
                       const string& header,
                       IdentData& result) const
{
    if (!pimpl->type)
        throw runtime_error(("[DelimReader::read] Unidentified file type for"+
                             filename).c_str());
    
    ifstream is(filename.c_str());

    if (is.good())
    {
        // Each line is parsed into its fields
        int no=0;
        string line;
        while(getlinePortable(is, line, pimpl->type->record_sep))
        {
            vector<string> fields;
            
            no++;
            if (!line.size())
            {
                ostringstream oss;
                oss << "[DelimReader::read] at line "
                    << no;
                throw runtime_error(oss.str().c_str());
            }
            
            if (line.at(0) == '#')
                continue;

            string separator(&pimpl->type->field_sep, 1);

            char_separator<char> sep(separator.c_str());
            tokenizer< char_separator<char> > tokens(line, sep);

            fields.clear();
            for(tokenizer< char_separator<char> >::const_iterator it=tokens.begin();
                it!=tokens.end(); it++)
            {
                string field = *it;
                
                fields.push_back(field);
            }

            // Put the fields into the mzIdentML
            //
            // retention time, m/z, score, peptide, protein
            if (fields.size()<5)
            {
                ostringstream oss;
                oss << "[DelimReader::read] missing fields"
                    << no;
                throw runtime_error(oss.str().c_str());
            }

            // 1st field: rt
            int rtIdx = fields.at(0).find(' ');
            string value;
            value.assign(fields.at(0).begin(),
                         fields.at(0).begin() + rtIdx);
            string units;
            units.assign(fields.at(0).begin() + rtIdx,
                         fields.at(0).end());

            CVID unitsCVID = commonTimeUnits(units);

            SpectrumIdentificationResultPtr sir(
                new SpectrumIdentificationResult());
            sir->set(MS_retention_time, value, unitsCVID);

            // 2nd field: scan
            sir->set(MS_peak_list_scans, fields.at(1));
            
            SpectrumIdentificationListPtr sil(new SpectrumIdentificationList());
            sil->spectrumIdentificationResult.push_back(sir);
            result.dataCollection.analysisData.
                spectrumIdentificationList.push_back(sil);

            // 3rd field: m/z
            SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());

            istringstream mzStream(fields.at(2));
            if (get(mzType) == "calculatedMassToCharge")
                mzStream >> sii->calculatedMassToCharge ;
            else
                mzStream >> sii->experimentalMassToCharge ;

            sir->spectrumIdentificationItem.push_back(sii);
            
            // 4th field: score
            CVID scoreType = pimpl->mapScore(get(ScoreType));
            sii->set(scoreType, fields.at(3));

            // 5th field: peptide
            PeptideEvidencePtr pe(new PeptideEvidence());

            // If the DBSequence doesn't exist yet, add it.
            DBSequencePtr dbSeq;

            DBSequenceMatch match(fields.at(4), fields.at(5));
            vector<DBSequencePtr>::const_iterator dbs_it;

            dbs_it = find_if(result.sequenceCollection.dbSequences.begin(),
                             result.sequenceCollection.dbSequences.end(),
                             match);

            if (dbs_it == result.sequenceCollection.dbSequences.end())
            {
                dbSeq = DBSequencePtr(
                    new DBSequence(pimpl->id("DBS_", pimpl->nextDBSIndex)));

                dbSeq->seq = fields.at(4);

                // 6th field: protein
                dbSeq->accession = fields.at(5);

                result.sequenceCollection.dbSequences.push_back(dbSeq);
            }

            pe->dbSequencePtr = dbSeq;
                        
            sii->peptideEvidencePtr.push_back(pe);
        }
    }    
}

void DelimReader::read(const std::string& filename,
                       const std::string& head,
                       IdentDataPtr& result) const
{
    if (!result.get())
        result = IdentDataPtr(new IdentData());
    
    read(filename, head, *result);
}

void DelimReader::read(const std::string& filename,
                       const std::string& head,
                       vector<IdentDataPtr>& results) const
{
    IdentDataPtr mzid(new IdentData());
    read(filename, head, mzid);
    results.push_back(mzid);
}


const char *DelimReader::getType() const
{
    return "mzIdentML";
}

CVID DelimReader::Impl::mapScore(const string& value)
{
    if (value.empty() || value == "")
        return MS_PSM_level_search_engine_specific_statistic;

    // {MS_SEQUEST_probability, MS_PSM_level_search_engine_specific_statistic},
    else if (value ==  "MS:1001154" || value == "Sequest:probability")
        return MS_SEQUEST_probability;
    //{MS_SEQUEST_xcorr,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001155" || value == "Sequest:xcorr")
        return MS_SEQUEST_xcorr;

    //{MS_SEQUEST_deltacn,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001156" || value == "Sequest:deltacn")
        return MS_SEQUEST_deltacn;
    
    //{MS_SEQUEST_sf, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001160" || value == "Sequest:sf")
        return MS_SEQUEST_sf;
    
    //{MS_SEQUEST_matched_ions,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001161" || value == "Sequest:matched ions")
        return MS_SEQUEST_matched_ions;
    
    //{MS_SEQUEST_total_ions,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001162" || value == "Sequest:total ions")
        return MS_SEQUEST_total_ions;
    
    //{MS_Mascot_score, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001171" || value == "Mascot:score")
        return MS_Mascot_score;
    
    //{MS_Mascot_expectation_value,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001172" || value == "Mascot:expectation value")
        return MS_Mascot_expectation_value;
    
    //{MS_Mascot_matched_ions,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001173" || value == "Mascot:matched ions")
        return MS_Mascot_matched_ions;
    
    //{MS_Mascot_total_ions,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001174" || value == "Mascot:total ions")
        return MS_Mascot_total_ions;
    
    //{MS_SEQUEST_PeptideSp,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001215" || value == "Sequest:PeptideSp")
        return MS_SEQUEST_PeptideSp;
    
    //{MS_SEQUEST_PeptideRankSp,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001217" || value == "Sequest:PeptideRankSp")
        return MS_SEQUEST_PeptideRankSp;
    
    //{MS_SEQUEST_PeptideNumber,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001217" || value == "Sequest:PeptideRankSp")
        return MS_SEQUEST_PeptideRankSp;
    
    //{MS_SEQUEST_PeptideIdnumber,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001219" || value == "Sequest:PeptideIdnumber")
        return MS_SEQUEST_PeptideIdnumber;
    
    //{MS_OMSSA_evalue, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001328" || value == "OMSSA:evalue")
        return MS_OMSSA_evalue;
    
    //{MS_OMSSA_pvalue, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001329" || value == "OMSSA:pvalue")
        return MS_OMSSA_pvalue;
    
    //{MS_X_Tandem_expect,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001330" || value == "X!Tandem:expect")
        return MS_X_Tandem_expect;
    
    //{MS_X_Tandem_hyperscore,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001331" || value == "X!Tandem:hyperscore")
        return MS_X_Tandem_hyperscore;
    
    //{MS_Mascot_homology_threshold,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001370" || value == "Mascot:homology threshold")
        return MS_Mascot_homology_threshold;
    
    //{MS_Mascot_identity_threshold,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001371" || value == "Mascot:identity threshold")
        return MS_Mascot_identity_threshold;
    
    //{MS_Phenyx_Auto, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001393" || value == "Phenyx:Auto")
        return MS_Phenyx_Auto;
    
    //{MS_Phenyx_User, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001394" || value == "Phenyx:User")
        return MS_Phenyx_User;
    
    //{MS_Phenyx_Pepzscore,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001395" || value == "Phenyx:Pepzscore")
        return MS_Phenyx_Pepzscore;
    
    //{MS_Phenyx_PepPvalue,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001396" || value == "Phenyx:PepPvalue")
        return MS_Phenyx_PepPvalue;
    
    //{MS_Phenyx_NumberOfMC,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001397" || value == "Phenyx:NumberOfMC")
        return MS_Phenyx_NumberOfMC;
    
    //{MS_Phenyx_Modif, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001398" || value == "Phenyx:Modif")
        return MS_Phenyx_Modif;
    
    //{MS_SpectraST_dot,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001417" || value == "SpectraST:dot")
        return MS_SpectraST_dot;
    
    //{MS_SpectraST_dot_bias,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001418" || value == "SpectraST:dot_bias")
        return MS_SpectraST_dot_bias;
    
    //{MS_SpectraST_discriminant_score_F,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "SpectraST:dot_bias" || value == "SpectraST:discriminant score F")
        return MS_SpectraST_discriminant_score_F;
    
    //{MS_SpectraST_delta,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001420" || value == "SpectraST:delta")
        return MS_SpectraST_delta;
    
    //{MS_percolator_Q_value,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001491" || value == "percolator:Q value")
        return MS_percolator_Q_value;
    
    //{MS_percolator_score_,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001492" || value == "percolator:score ")
        return MS_percolator_score;
    
    //{MS_percolaror_PEP_,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001493" || value == "percolator:PEP ")
        return MS_percolator_PEP;
    
    //{MS_ProteinScape_SearchResultId,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001495" || value == "ProteinScape:SearchResultId")
        return MS_ProteinScape_SearchResultId;
    
    //{MS_ProteinScape_SearchEventId,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001496" || value == "ProteinScape:SearchEventId")
        return MS_ProteinScape_SearchEventId;
    
    //{MS_Profound_z_value,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001498" || value == "Profound:z value")
        return MS_Profound_z_value;
    
    //{MS_Profound_Cluster,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001499" || value == "Profound:Cluster")
        return MS_Profound_Cluster;
    
    //{MS_Profound_ClusterRank,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "" || value == "")
        return MS_Profound_ClusterRank;
    
    //{MS_MSFit_Mowse_score,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "Profound:Cluster" || value == "Profound:ClusterRank")
        return MS_MSFit_Mowse_score;
    
    //{MS_Sonar_Score, MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001502" || value == "Sonar:Score")
        return MS_Sonar_Score;
    
    //{MS_ProteinScape_PFFSolverExp,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001503" || value == "ProteinScape:PFFSolverExp")
        return MS_ProteinScape_PFFSolverExp;
    
    //{MS_ProteinScape_PFFSolverScore,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001504" || value == "ProteinScape:PFFSolverScore")
        return MS_ProteinScape_PFFSolverScore;
    
    //{MS_ProteinScape_IntensityCoverage,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001505" || value == "ProteinScape:IntensityCoverage")
        return MS_ProteinScape_IntensityCoverage;
    
    //{MS_ProteinScape_SequestMetaScore,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001506" || value == "ProteinScape:SequestMetaScore")
        return MS_ProteinScape_SequestMetaScore;
    
    //{MS_Scaffold_Peptide_Probability,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "ProteinScape:SequestMetaScore" || value == "Scaffold: Peptide Probability")
        return MS_Scaffold_Peptide_Probability;
    
    //{MS_IdentityE_Score,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001569" || value == "IdentityE Score")
        return MS_IdentityE_Score;
    
    //{MS_ProteinLynx__Log_Likelihood,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001570" || value == "ProteinLynx: Log Likelihood")
        return MS_ProteinLynx_Log_Likelihood;
    
    //{MS_ProteinLynx_Ladder_Score,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001571" || value == "ProteinLynx: Ladder Score")
        return MS_ProteinLynx_Ladder_Score;
    
    //{MS_SpectrumMill_Score,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001572" || value == "SpectrumMill: Score")
        return MS_SpectrumMill_Score;
    
    //{MS_SpectrumMill_SPI,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001573" || value == "SpectrumMill: SPI")
        return MS_SpectrumMill_SPI;
    
    //{MS_ProteinScape_ProfoundProbability,
    //MS_PSM_level_search_engine_specific_statistic},
    else if (value == "MS:1001597" || value == "ProteinScape:ProfoundProbability")
        return MS_ProteinScape_ProfoundProbability;
    
    return MS_PSM_level_search_engine_specific_statistic;
}

} // namespace identdata
} // namespace pwiz
