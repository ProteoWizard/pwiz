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
#include <boost/xpressive/xpressive_dynamic.hpp>

namespace pwiz {
namespace identdata {

using namespace pwiz::util;
using namespace boost;
namespace bxp = boost::xpressive;

namespace {

///
/// @brief TextRecord holds the data of a single "line" of data.
/// 
/// A vector of TextRecord objects is used to hold a text file. By
/// default, strings are initialized empty and numerical members are
/// initialized to -1.
///
struct TextRecord
{
    string none;
    string scan;
    string rt;
    double mz;
    int charge;
    string score;
    string scoretype;
    string peptide;
    string protein;
    string proteinDescription;

    TextRecord()
        : none(""), scan(""), rt(""), mz(-1), charge(-1),
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

    void assign(const TextRecord& tr)
    {
        none = tr.none;
        scan = tr.scan;
        rt = tr.rt;
        mz = tr.mz;
        score = tr.score;
        scoretype = tr.scoretype;
        peptide = tr.peptide;
        protein = tr.protein;
    }
};


/// Used for debugging, the TextRecord insertion operator for ostream
/// object writes an easily readable version of the object with fields
/// labeled.
///
/// @param os output stream.
/// @param tr object to be written to os.
///
/// @return output stream object
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
       << "protein: " << tr.protein << "\n"
       << "protein_description:" << tr.proteinDescription << "\n";

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
        for (int i=0; i<=Last; i++)
            idNames.push_back(IdFieldNames[i]);
    }

    void write(ostream& os, const IdentData& mzid,
               const IterationListenerRegistry* iterationListenerRegistry) const;

    void read(boost::shared_ptr<istream> is, IdentData& msd) const;

    void write(ostream& os, const TextRecord& tr,
               const IterationListenerRegistry* iterationListenerRegistry) const;

    void writeField(ostream& os, const TextRecord& tr,
                    Serializer_Text::IdField field) const;
    
    void setHeaders(const vector<string>& fields) const;
    string getHeaders() const;

    static const std::string IdFieldNames[];

private:
    Config config;
    vector<string> idNames;
};

template <Serializer_Text::IdField F>
struct tr_eq
{
    Serializer_Text::IdField field;
    
    tr_eq(Serializer_Text::IdField field)
        : field(field)
    {
    }
    
    bool operator()(const TextRecord& left, const TextRecord& right)
    {
        bool result = false;
        
        switch(field)
        {
        case Serializer_Text::None:
            result = left.none == right.none;
            break;
        
        case Serializer_Text::Scan:
            result = left.scan == right.scan;
            break;
        
        case Serializer_Text::Rt:
            result = left.rt == right.rt;
            break;
        
        case Serializer_Text::Mz:
            result = left.mz == right.mz;
            break;

        case Serializer_Text::Charge:
            result = left.charge == right.charge;
            break;
        
        case Serializer_Text::Score:
            result = left.score == right.score;
            break;
        
        case Serializer_Text::ScoreType:
            result = left.scoretype == right.scoretype;
            break;
        
        case Serializer_Text::Peptide:
            result = left.peptide == right.peptide;
            break;
        
        case Serializer_Text::Protein:
            result = left.protein == right.protein;
            break;

        case Serializer_Text::ProteinDescription:
            result = left.proteinDescription == right.proteinDescription;
            break;

        default:
            break;
        }

        return result;
    }
};

struct tr_less
{
    Serializer_Text::IdField field;
    
    tr_less(Serializer_Text::IdField field) : field(field) {}

    bool operator()(const TextRecord& left, const TextRecord& right) const
    {
        switch(field)
        {
        case Serializer_Text::None:
            return true;
            break;

        case Serializer_Text::Scan:
            return left.scan < right.scan;
            break;

        case Serializer_Text::Rt:
            return left.rt < right.rt;
            break;
            
        case Serializer_Text::Mz:
            return left.mz < right.mz;
            break;
            
        case Serializer_Text::Charge:
            return left.charge < right.charge;
            break;
            
        case Serializer_Text::Score:
            return left.score < right.score;
            break;
            
        case Serializer_Text::ScoreType:
            return left.scoretype < right.scoretype;
            break;
            
        case Serializer_Text::Peptide:
            return left.peptide < right.peptide;
            break;
            
        case Serializer_Text::Protein:
            return left.protein < right.protein;
            break;

        case Serializer_Text::ProteinDescription:
            return left.proteinDescription < right.proteinDescription;
            break;

        default:
            break;
        }

        return true;
    }
};

namespace
{

void parseHeaders(boost::shared_ptr<istream> is, vector<string> fields)
{
    // TODO add header parsing & field identification.
}

vector<TextRecord> fetchPeptideEvidence(const SpectrumIdentificationItem& sii,
                                        const TextRecord& record)
{
    vector<TextRecord> records;

    BOOST_FOREACH(PeptideEvidencePtr pep, sii.peptideEvidencePtr)
    {
        TextRecord tr(record);

        // Fetch the peptide sequence by way of the DBSequence
        // reference.
        //if (pep->dbSequencePtr.get())
        //    tr.peptide = pep->dbSequencePtr->seq;
        //else if (sii.peptidePtr.get())
        //    tr.peptide = sii.peptidePtr->peptideSequence;
            
        
        // Fetch the protein name by way of the "protein description"
        // CVParam
        if (!pep->dbSequencePtr->accession.empty())
            tr.protein = pep->dbSequencePtr->accession;

        CVParam cvp = pep->dbSequencePtr->cvParam(MS_protein_description);
            
        if (cvp.cvid != CVID_Unknown)
            tr.proteinDescription = cvp.value;

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

        tr.mz = sii->experimentalMassToCharge;
        tr.charge = sii->chargeState;

        CVParam score = sii->cvParamChild(MS_search_engine_specific_score);
        if (score.cvid != CVID_Unknown)
        {
            tr.score = score.value;
            CVTermInfo info = cvTermInfo(score.cvid);
            tr.scoretype = info.name;
        }

        tr.peptide = sii->peptidePtr->peptideSequence;

        vector<TextRecord> pepsRecords =
            fetchPeptideEvidence(*sii, tr);
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
    const string spectrumIDPattern = ".*[ ]*scan=([0-9\\.]+).*";
    bxp::sregex spectrumIDExp = bxp::sregex::compile(spectrumIDPattern);

    vector<TextRecord> records;

    if (!sirl.size())
        return records;

    
    BOOST_FOREACH(SpectrumIdentificationResultPtr sir, sirl)
    {
        TextRecord tr;
        
        // Fetch the retention time.
        //
        // Retention time should be kept in the cvParams under accession
        // MS_retention_time_s__OBSOLETE "MS:1001114", formerly, or
        // MS_retention_time "MS:1000894"

        CVParam rtParam = sir->cvParam(MS_retention_time);
        if (rtParam.cvid == CVID_Unknown)
            rtParam = sir->cvParam(MS_retention_time_s__OBSOLETE);
        if (rtParam.cvid != CVID_Unknown)
        {
            ostringstream oss;
            
            oss << rtParam.value;
            if (rtParam.units != CVID_Unknown)
                oss << " " << cvTermInfo(rtParam.units).name;
            
            tr.rt=oss.str();
        }

        // Check for a keyword/numerical value match in the spectrumID
        bxp::smatch what;
        if (regex_match(sir->spectrumID, what, spectrumIDExp))
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

vector<TextRecord> fetchRecords(const IdentData& mzid)
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

void sortRecords(vector<TextRecord>& records,
                 Serializer_Text::IdField field)
{
    sort(records.begin(), records.end(), tr_less(field));
}

} // anonymous namespace

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

    case ProteinDescription:
        os << tr.proteinDescription;
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

void Serializer_Text::Impl::write(ostream& os, const IdentData& mzid,
    const IterationListenerRegistry* iterationListenerRegistry) const
{

    if (config.headers)
        os << getHeaders() << config.recordDelim;

    // Records are assembled in a depth first search of the mzIdentML
    // tree.
    vector<TextRecord> records = fetchRecords(mzid);

    // Sort the records according to the Config::sort field. If it's
    // set to None, they will be output in an arbitrary order.
    sortRecords(records, config.sort);
    
    BOOST_FOREACH(TextRecord tr, records)
    {
        write(os, tr, iterationListenerRegistry);
    }
}

void Serializer_Text::Impl::read(boost::shared_ptr<istream> is, IdentData& mzid) const
{
    namespace algo=boost::algorithm;
    
    vector<string> headers;
    string line;

    // Get the header line from the file & split it into fields
    getlinePortable(*is, line);
    split(headers, line, is_any_of("\t"));

    // Sort out which fields we can manage.
    setHeaders(headers);

    // TODO read in file and sort into mzid.
}

string Serializer_Text::Impl::getHeaders() const
{
    vector<string> headerList;

    // User selected fields are used when set.
    if (config.fields.size()>0)
    {
        BOOST_FOREACH(IdField id, config.fields)
            headerList.push_back(idNames[id]);
    }
    else
    {
        // If there is no subset of fields, we use all but the "None"
        // field.
        for (vector<string>::const_iterator i=idNames.begin()+1;
             i!=idNames.end(); ++i)
            headerList.push_back(*i);
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



const string* Serializer_Text::getIdFieldNames()
{
    return Impl::IdFieldNames;
}

const std::string Serializer_Text::Impl::IdFieldNames[] =
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
    "protein_description",
    ""
};

PWIZ_API_DECL Serializer_Text::Serializer_Text(const Config& config)
    : impl_(new Impl(config))
{
}

PWIZ_API_DECL void Serializer_Text::write(ostream& os, const IdentData& mzid,
           const IterationListenerRegistry* iterationListenerRegistry) const
{
    return impl_->write(os, mzid, iterationListenerRegistry);
}

PWIZ_API_DECL void Serializer_Text::read(boost::shared_ptr<istream> is, IdentData& mzid) const
{
    return impl_->read(is, mzid);
}


} // namespace msdata 
} // namespace pwiz 


