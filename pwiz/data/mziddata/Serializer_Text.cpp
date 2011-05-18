//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "pwiz/utility/misc/Std.hpp"
#include "Serializer_Text.hpp"
#include "TextWriter.hpp"
#include <boost/regex.hpp>

namespace pwiz {
namespace mziddata {

using namespace pwiz::util;
using namespace boost;

namespace {

struct TextRecord
{
    string none;
    string scan;
    string rt;
    string mz;
    string charge;
    string score;
    string scoretype;
    string peptide;
    string protein;

    TextRecord()
        : none(""), scan(""), rt(""), mz(""), charge(""),
          score(""), scoretype(""), peptide(""), protein("")
    {}
    TextRecord(const TextRecord& tr)
        : none(tr.none),scan(tr.scan),rt(tr.rt),mz(tr.mz),
          charge(tr.charge), score(tr.score),scoretype(tr.scoretype),
          peptide(tr.peptide),protein(tr.protein)
    {}

    TextRecord copy() const
    {
        TextRecord tr;
        
        tr.none = none;
        tr.scan = scan;
        tr.rt = rt;
        tr.mz = mz;
        tr.score = score;
        tr.scoretype = scoretype;
        tr.peptide = peptide;
        tr.protein = protein;

        return tr;
    }
};

ostream& operator<<(ostream& os, const TextRecord& tr)
{
    os << "[TextRecord]\n"
       << "none: " << tr.none << "\n"
       << "scan: " << tr.scan << "\n"
       << "rt: " << tr.rt << "\n"
       << "mz: " << tr.mz << "\n"
       << "score: " << tr.score << "\n"
       << "scoretype: " << tr.scoretype << "\n"
       << "peptide: " << tr.peptide << "\n"
       << "protein: " << tr.protein << "\n";

    return os;
}

} // anonymous namespace

PWIZ_API_DECL Serializer_Text::Config::Config()
    : headers(true), sort(Scan), recordDelim("\n"),fieldDelim("\t")
{
}

PWIZ_API_DECL Serializer_Text::Config::Config(const Config& config)
    : headers(config.headers), fields(config.fields), sort(config.sort),
      recordDelim(config.recordDelim),fieldDelim(config.fieldDelim)
{}

class Serializer_Text::Impl
{
public:
    Impl(const Config& config_)
        : config(config_)
    {
        for (int i=0; i<Last; i++)
        {
            idNames.push_back(IdFieldNames[i]);
        }
    }

    void write(ostream& os, const MzIdentML& mzid,
               const IterationListenerRegistry* iterationListenerRegistry) const;

    void read(shared_ptr<istream> is, MzIdentML& msd) const;

    void write(ostream& os, const TextRecord& tr,
               const IterationListenerRegistry* iterationListenerRegistry) const;

    void writeField(ostream& os, const TextRecord& tr,
                    Serializer_Text::IdField field) const;
    
    void setHeaders(const vector<string>& fields) const;
    string getHeaders() const;

private:
    Config config;
    vector<string> idNames;
};


void parseHeaders(shared_ptr<istream> is, vector<string> fields)
{
    // TODO add haeder parsing & field identification.
}

vector<TextRecord> fetchPeptideEvidence(const vector<PeptideEvidencePtr>& peps,
                                        const TextRecord& record)
{
    vector<TextRecord> records;

    BOOST_FOREACH(PeptideEvidencePtr pep, peps)
    {
        TextRecord tr(record);
        ostringstream oss;

        /*
        if (pep->start != pep->end)
            oss << pep->start << "-" << pep->end;
        else
            oss << pep->start;
        tr.scan = oss.str();
        */
        
        // Fetch the peptide sequence by way of the DBSequence
        // reference.
        if (pep->dbSequencePtr.get())
            tr.peptide = pep->dbSequencePtr->seq;
        
        // Fetch the protein name by way of the "protein description"
        // CVParam
        CVParam cvp = pep->dbSequencePtr->cvParam(MS_protein_description);

        if (cvp.cvid != CVID_Unknown)
            tr.protein = cvp.value;

        records.push_back(tr);
    }


    return records;
}

vector<TextRecord> fetchSpectrumIdItem(
    const vector<SpectrumIdentificationItemPtr>& siiv,
    const TextRecord& record)
{
    vector<TextRecord> records;

    BOOST_FOREACH(SpectrumIdentificationItemPtr sii, siiv)
    {
        TextRecord tr(record);

        ostringstream oss;
        oss << sii->experimentalMassToCharge;
        tr.mz = oss.str();

        oss.str("");
        oss << sii->chargeState;
        tr.charge = oss.str();

        CVParam score = sii->cvParamChild(MS_search_engine_specific_score);
        if (score.cvid != CVID_Unknown)
        {
            tr.score = score.value;
            CVTermInfo info = cvTermInfo(score.cvid);
            tr.scoretype = info.name;
        }

        tr.peptide = sii->peptidePtr->peptideSequence;
        
        vector<TextRecord> pepsRecords =
            fetchPeptideEvidence(sii->peptideEvidencePtr, tr);
        BOOST_FOREACH(TextRecord t, pepsRecords)
        {
            records.push_back(t);
        }
    }
    
    return records;
}

vector<TextRecord> fetchSpectrumIdResults(
    const vector<SpectrumIdentificationResultPtr>& sirl)
{
    const string spectrumIDPattern = "[^=]*=([0-9\\.]+)";
    regex spectrumIDExp(spectrumIDPattern);

    vector<TextRecord> records;

    if (!sirl.size())
        return records;

    
    BOOST_FOREACH(SpectrumIdentificationResultPtr sir, sirl)
    {
        TextRecord tr;
        
        // Fetch the retention time.
        //
        // Retention time should be kept in the cvParams under accession
        // "MS:1001114"

        CVParam rtParam = sir->cvParam(MS_retention_time);
        if (rtParam.cvid != CVID_Unknown)
        {
            ostringstream oss;
            
            oss << rtParam.value;
            if (rtParam.units != CVID_Unknown)
                oss << " " << cvTermInfo(rtParam.units).name;
            
            tr.rt=oss.str();
        }

        // Check for a keyword/numerical value match int he spectrumID
        cmatch what;
        if (regex_match(sir->spectrumID.c_str(), what, spectrumIDExp))
        {
            // Use the numerical value as the scan
            tr.scan.assign(what[1].first, what[1].second);
        }
        
        vector<TextRecord> siiResults =fetchSpectrumIdItem(
            sir->spectrumIdentificationItem, tr);
        BOOST_FOREACH(TextRecord t, siiResults)
        {
            records.push_back(t);
        }
    }


    return records;
}

vector<TextRecord> fetchRecords(const MzIdentML& mzid)
{
    vector<TextRecord> records;

    const vector<SpectrumIdentificationListPtr>& sipl =
        mzid.dataCollection.analysisData.spectrumIdentificationList;
    if (sipl.size())
    {
        BOOST_FOREACH(SpectrumIdentificationListPtr sip, sipl)
        {
            vector<TextRecord> sipResults =
                fetchSpectrumIdResults(sip->spectrumIdentificationResult);
            BOOST_FOREACH(TextRecord t, sipResults)
            {
                records.push_back(t);
            }
        }
    }

    return records;
}

void Serializer_Text::Impl::writeField(ostream& os, const TextRecord& tr,
                                       Serializer_Text::IdField field) const
{
    switch(field)
    {
    case None:
        break;
        
    case Scan:
        os << tr.scan;
        break;
        
    case Rt:
        os << tr.rt;
        break;
        
    case Mz:
        os << tr.mz;
        break;

    case Charge:
        os << tr.charge;
        break;
        
    case Score:
        os << tr.score;
        break;
        
    case ScoreType:
        os << tr.scoretype;
        break;
        
    case Peptide:
        os << tr.peptide;
        break;
        
    case Protein:
        os << tr.protein;
        break;

    default:
        break;
    }
}

void Serializer_Text::Impl::write(ostream& os, const TextRecord& tr,
    const IterationListenerRegistry* iterationListenerRegistry) const
{
    vector<IdField> fields;

    if (config.fields.size())
    {
        fields.resize(config.fields.size());
        copy(config.fields.begin(), config.fields.end(), fields.begin());
    }
    else
    {
        for (size_t i=1;i<=Last; i++)
            fields.push_back((IdField)i);
    }
    
    for (size_t i=0; i<fields.size(); i++)
    {
        writeField(os, tr, fields.at(i));
        if (i<fields.size()-1)
            os << config.fieldDelim;
    }
    
    os << config.recordDelim;
}

void Serializer_Text::Impl::write(ostream& os, const MzIdentML& mzid,
    const IterationListenerRegistry* iterationListenerRegistry) const
{

    os << getHeaders() << config.recordDelim;

    vector<TextRecord> records = fetchRecords(mzid);

    // TODO setup sorting.
    BOOST_FOREACH(TextRecord tr, records)
    {
        write(os, tr, iterationListenerRegistry);
    }
}

void Serializer_Text::Impl::read(shared_ptr<istream> is, MzIdentML& mzid) const
{
    namespace algo=boost::algorithm;
    
    vector<string> headers;
    string line;

    // Get the header line from the file & split it into fields
    getline(*is, line);
    split(headers, line, is_any_of("\t"));

    // Sort out which fields we can manage.
    setHeaders(headers);
}

string Serializer_Text::Impl::getHeaders() const
{
    vector<string> headerList;
    if (config.fields.size()>0)
    {
        BOOST_FOREACH(IdField id, config.fields)
            headerList.push_back(idNames[id]);
    }
    else
    {

        headerList.resize(idNames.size()-1);
        copy(idNames.begin()+1, idNames.end(), headerList.begin());
        
        BOOST_FOREACH(string name, headerList)
            headerList.push_back(name);

    }
    
    return join(headerList, config.fieldDelim);
}

void Serializer_Text::Impl::setHeaders(const vector<string>& headers) const
{
    vector<Serializer_Text::IdField> fields;
    
    BOOST_FOREACH(string f, headers)
    {
        int idx=0;
        to_lower(f);
        vector<string>::const_iterator i;
        i=std::find_if(idNames.begin(), idNames.end(),
                       std::bind2nd(std::equal_to<string>(),f));


        if (i!=idNames.end())
            idx=i-idNames.begin();

        fields.push_back((Serializer_Text::IdField)idx);
    }
}


PWIZ_API_DECL const string Serializer_Text::IdFieldNames[] =
{
    "none",
    "scan",
    "rt",
    "mz",
    "charge",
    "score",
    "scoretype",
    "peptide",
    "protein",
    ""
};

PWIZ_API_DECL Serializer_Text::Serializer_Text(const Config& config)
    : impl_(new Impl(config))
{}

PWIZ_API_DECL void Serializer_Text::write(ostream& os, const MzIdentML& mzid,
           const IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(os, mzid, iterationListenerRegistry);
}

PWIZ_API_DECL void Serializer_Text::read(shared_ptr<istream> is, MzIdentML& mzid) const
{
    return impl_->read(is, mzid);
}


} // namespace msdata 
} // namespace pwiz 


