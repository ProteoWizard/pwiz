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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2013 Vanderbilt University
//
// Contributor(s):
//


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/foreach_field.hpp"
#include "boost/range/algorithm/remove_if.hpp"
#include "boost/range/adaptor/map.hpp"
#include "boost/variant.hpp"

#include "SchemaUpdater.hpp"
#include "Parser.hpp"
#include "Embedder.hpp"
#include "sqlite3pp.h"
#include <iomanip>
#include "CoreVersion.hpp"
#include "idpQueryVersion.hpp"


using namespace IDPicker;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;


BEGIN_IDPICKER_NAMESPACE


BOOST_ENUM(GroupBy,
    (Invalid)
    (Protein)
    (ProteinGroup)
    (Cluster)
    (Gene)
    (GeneGroup)
    /*(DistinctMatch)
    (Peptide)
    (PeptideGroup)
    (PeptideSpectrumMatch)
    (Spectrum)
    (SpectrumSourceAndGroup)
    (Modification)
    (DeltaMass)
    (ModifiedSite)*/
);

typedef pair<string, int> SqlColumn;

BOOST_ENUM_VALUES(ProteinColumn, SqlColumn,
    (Invalid)(make_pair("", 0))
    (Accession)(make_pair("GROUP_CONCAT(DISTINCT pro.Accession)", SQLITE_TEXT))
    (GeneId)(make_pair("GROUP_CONCAT(DISTINCT pro.GeneId)", SQLITE_TEXT))
    (GeneGroup)(make_pair("pro.GeneGroup", SQLITE_INTEGER))
    (DistinctPeptides)(make_pair("COUNT(DISTINCT pi.Peptide)", SQLITE_INTEGER))
    (DistinctMatches)(make_pair("COUNT(DISTINCT dm.DistinctMatchId)", SQLITE_INTEGER))
    (FilteredSpectra)(make_pair("COUNT(DISTINCT psm.Spectrum)", SQLITE_INTEGER))
    (IsDecoy)(make_pair("pro.IsDecoy", SQLITE_INTEGER))
    (Cluster)(make_pair("pro.Cluster", SQLITE_INTEGER))
    (ProteinGroup)(make_pair("pro.ProteinGroup", SQLITE_INTEGER))
    (Length)(make_pair("pro.Length", SQLITE_INTEGER))
    (PercentCoverage)(make_pair("ROUND(pc.Coverage, 2)", SQLITE_FLOAT))
    (Sequence)(make_pair("pd.Sequence", SQLITE_TEXT))
    (Description)(make_pair("pmd.Description", SQLITE_TEXT))
    (TaxonomyId)(make_pair("pmd.TaxonomyId", SQLITE_INTEGER))
    (GeneName)(make_pair("pmd.GeneName", SQLITE_TEXT))
    (GeneFamily)(make_pair("pmd.GeneFamily", SQLITE_TEXT))
    (Chromosome)(make_pair("pmd.Chromosome", SQLITE_TEXT))
    (GeneDescription)(make_pair("pmd.GeneDescription", SQLITE_TEXT))
    (PrecursorIntensity)(make_pair("IFNULL(SUM(DISTINCT xic.PeakIntensity), 0)", SQLITE_FLOAT))
    (PrecursorArea)(make_pair("IFNULL(SUM(DISTINCT xic.PeakArea), 0)", SQLITE_FLOAT))
    (PrecursorBestSNR)(make_pair("IFNULL(MAX(xic.PeakSNR), 0)", SQLITE_FLOAT))
    (PrecursorMeanSNR)(make_pair("IFNULL(AVG(DISTINCT xic.PeakSNR), 0)", SQLITE_FLOAT))
    (iTRAQ4plex)(make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities), 0)", SQLITE_BLOB))
    (iTRAQ8plex)(make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities), 0)", SQLITE_BLOB))
    (TMT2plex)(make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB))
    (TMT6plex)(make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB))
    (TMT10plex)(make_pair("IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)", SQLITE_BLOB))
    (PivotMatchesByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotMatchesBySource)(make_pair("0", SQLITE_INTEGER))
    (PivotPeptidesByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotPeptidesBySource)(make_pair("0", SQLITE_INTEGER))
    (PivotSpectraByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotSpectraBySource)(make_pair("0", SQLITE_INTEGER))
    (PivotPrecursorIntensityByGroup)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorIntensityBySource)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorAreaByGroup)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorAreaBySource)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorBestSNRByGroup)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorBestSNRBySource)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorMeanSNRByGroup)(make_pair("0", SQLITE_FLOAT))
    (PivotPrecursorMeanSNRBySource)(make_pair("0", SQLITE_FLOAT))
    (PivotITRAQByGroup)(make_pair("0", SQLITE_BLOB))
    (PivotITRAQBySource)(make_pair("0", SQLITE_BLOB))
    (PivotTMTByGroup)(make_pair("0", SQLITE_BLOB))
    (PivotTMTBySource)(make_pair("0", SQLITE_BLOB))
    (PeptideGroups)(make_pair("GROUP_CONCAT(DISTINCT pep.PeptideGroup)", SQLITE_TEXT))
    (PeptideSequences)(make_pair("GROUP_CONCAT(DISTINCT (SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), pep.DecoySequence) FROM PeptideInstance pi LEFT JOIN ProteinData pd ON pi.Protein=pd.Id WHERE pep.Id=pi.Peptide))", SQLITE_TEXT))
);

/*DistinctMatch, Peptide, PeptideGroup
Sequence
DistinctPeptides
DistinctMatches
FilteredSpectra
MonoisotopicMass
MolecularWeight
PeptideGroup
ProteinGroups
ProteinAccessions
Proteins
iTRAQ
TMT
*/

/*PeptideSpectrumMatch, Spectrum, SpectrumSourceAndGroup
Key
DistinctPeptides
DistinctMatches
FilteredSpectra
DistinctAnalyses
DistinctCharges
ProteinGroups
PrecursorMz
ScanTime
ObservedMass
ExactMass
MassError
Analysis
Charge
QValue
Sequence
iTRAQ
TMT
*/

/*Modification, MonoDeltaMass, ModifiedSite
ModifiedSite
MonoDeltaMass
AvgDeltaMass
DistinctPeptides
DistinctMatches
FilteredSpectra
Description
*/


struct ReporterIon
{
    const char* name;
    size_t index;
};

ReporterIon iTRAQ_ions[8] =
{
    { "113", 0 },
    { "114", 1 },
    { "115", 2 },
    { "116", 3 },
    { "117", 4 },
    { "118", 5 },
    { "119", 6 },
    { "121", 7 }
};
ReporterIon itraq4plexIons[4] = { iTRAQ_ions[1], iTRAQ_ions[2], iTRAQ_ions[3], iTRAQ_ions[4] };
ReporterIon itraq8plexIons[8] = { iTRAQ_ions[0], iTRAQ_ions[1], iTRAQ_ions[2], iTRAQ_ions[3], iTRAQ_ions[4], iTRAQ_ions[5], iTRAQ_ions[6], iTRAQ_ions[7] };

ReporterIon TMT_ions[10] =
{
    { "126", 0 },
    { "127N", 1 },
    { "127C", 2 },
    { "128N", 3 },
    { "128C", 4 },
    { "129N", 5 },
    { "129C", 6 },
    { "130N", 7 },
    { "130C", 8 },
    { "131", 9 }
};
ReporterIon tmt2plexIons[2] = { TMT_ions[0], TMT_ions[2] };
ReporterIon tmt6plexIons[6] = { TMT_ions[0], TMT_ions[2], TMT_ions[4], TMT_ions[6], TMT_ions[8], TMT_ions[9] };
ReporterIon tmt10plexIons[10] = { TMT_ions[0], TMT_ions[1], TMT_ions[2], TMT_ions[3], TMT_ions[4], TMT_ions[5], TMT_ions[6], TMT_ions[7], TMT_ions[8], TMT_ions[9] };


template <typename ArrayType>
void writeBlobArray(const void* blob, ostream& os, const vector<ReporterIon>& arrayInfo)
{
    if (arrayInfo.empty())
        return;

    if (blob == NULL)
    {
        os << "0";
        for (size_t i=1; i < arrayInfo.size(); ++i)
            os << "\t0";
        return;
    }

    const ArrayType* blobArray = reinterpret_cast<const ArrayType*>(blob);
    streamsize oldPrecision = os.precision(0);
    ios::fmtflags oldFlags = os.flags(ios::fixed);
    for (size_t i = 0; i < arrayInfo.size(); ++i)
        os << blobArray[arrayInfo[i].index] << '\t';
    os.seekp(-1, ios::cur);
    os.precision(oldPrecision);
    os.flags(oldFlags);
}


typedef boost::variant<int, double, const void*> PivotDataType;
typedef map<boost::int64_t, map<boost::int64_t, PivotDataType> > PivotDataMap;
typedef map<size_t, PivotDataMap> PivotDataByColumnMap;
void pivotData(sqlite::database& idpDB, GroupBy groupBy, const string& pivotMode, PivotDataMap& pivotDataMap)
{
    string groupByString = "pro.Id";
    if (groupBy == GroupBy::ProteinGroup) groupByString = "pro.ProteinGroup";
    else if (groupBy == GroupBy::Cluster) groupByString = "pro.Cluster";
    else if (groupBy == GroupBy::Gene) groupByString = "pro.GeneId";
    else if (groupBy == GroupBy::GeneGroup) groupByString = "pro.GeneGroup";

    string pivotColumn;
    if (bal::ends_with(pivotMode, "Source"))
        pivotColumn = "ss.Id";
    else
        pivotColumn = "ssgl.Group_";
    
    string countColumn;

    if (bal::contains(pivotMode, "Matches"))
        countColumn = "COUNT(DISTINCT dm.DistinctMatchId)";
    else if (bal::contains(pivotMode, "Peptides"))
        countColumn = "COUNT(DISTINCT psm.Peptide)";
    else if (bal::contains(pivotMode, "Spectra"))
        countColumn = "COUNT(DISTINCT s.Id)";
    else
    {
        if (bal::contains(pivotMode, "ITRAQ"))
            countColumn = "IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities), 0)";
        else if (bal::contains(pivotMode, "TMT"))
            countColumn = "IFNULL(DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities), 0)";
        else if (bal::contains(pivotMode, "PrecursorIntensity"))
            countColumn = "IFNULL(SUM(DISTINCT xic.PeakIntensity), 0)";
        else if (bal::contains(pivotMode, "PrecursorArea"))
            countColumn = "IFNULL(SUM(DISTINCT xic.PeakArea), 0)";
        else if (bal::contains(pivotMode, "BestSNR"))
            countColumn = "IFNULL(MAX(xic.PeakSNR), 0)";
        else if (bal::contains(pivotMode, "MeanSNR"))
            countColumn = "IFNULL(AVG(DISTINCT xic.PeakSNR), 0)";
        else
            throw runtime_error("unsupported pivot mode");

        // build SQL query with quantitation
        string sql = "SELECT " + groupByString + ", " + pivotColumn + ", " + countColumn + " "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "JOIN SpectrumSource ss ON s.Source=ss.Id "
                     "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                     "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                     "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                     "JOIN Protein pro ON pi.Protein=pro.Id "
                     "LEFT JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id "
                     "LEFT JOIN XICMetrics xic ON psm.Id=xic.PsmId "
                     "GROUP BY " + groupByString + ", " + pivotColumn;
        cout << sql << endl;
        sqlite::query q(idpDB, sql.c_str());

        if (bal::contains(countColumn, "ARRAY_SUM"))
            BOOST_FOREACH(sqlite::query::rows row, q)
            {
                int blobBytes = row.column_bytes(2);
                if (blobBytes == 0)
                    continue;
                char* blobCopy = new char[blobBytes];
                memcpy(blobCopy, (const char*) row.get<const void*>(2), blobBytes);
                pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = blobCopy;
            }
        else
            BOOST_FOREACH(sqlite::query::rows row, q)
                pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = row.get<double>(2);
        return;
    }

    // build SQL query without quantitation
    string sql = "SELECT " + groupByString + ", " + pivotColumn + ", " + countColumn + " "
                 "FROM PeptideSpectrumMatch psm "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId "
                 "JOIN Spectrum s ON psm.Spectrum=s.Id "
                 "JOIN SpectrumSource ss ON s.Source=ss.Id "
                 "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                 "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                 "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                 "JOIN Protein pro ON pi.Protein=pro.Id "
                 "GROUP BY " + groupByString + ", " + pivotColumn;
    cout << sql << endl;
    sqlite::query q(idpDB, sql.c_str());
    BOOST_FOREACH(sqlite::query::rows row, q)
        pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = row.get<int>(2);
}


// get the most inclusive set of reporter ion headers (i.e. if both TMT10 and 6 are used, write TMT10's headers)
vector<string> getReporterIonHeaders(const ReporterIon ions[], const set<QuantitationMethod>& quantitationMethods)
{
    vector<string> reporterIonColumnHeaders;

    if (quantitationMethods.empty() ||
        quantitationMethods.count(QuantitationMethod::None) + quantitationMethods.count(QuantitationMethod::LabelFree) == quantitationMethods.size())
        return reporterIonColumnHeaders;

    if (&ions[0] == &iTRAQ_ions[0])
    {
        if (quantitationMethods.count(QuantitationMethod::ITRAQ8plex) > 0)
            for (size_t i = 0, end = sizeof(itraq8plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("iTRAQ-") + itraq8plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::ITRAQ4plex) > 0)
            for (size_t i = 0, end = sizeof(itraq4plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("iTRAQ-") + itraq4plexIons[i].name);
    }
    else if (&ions[0] == &TMT_ions[0])
    {
        if (quantitationMethods.count(QuantitationMethod::TMT10plex) > 0)
            for (size_t i = 0, end = sizeof(tmt10plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt10plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT6plex) > 0)
            for (size_t i = 0, end = sizeof(tmt6plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt6plexIons[i].name);
        else if (quantitationMethods.count(QuantitationMethod::TMT2plex) > 0)
            for (size_t i = 0, end = sizeof(tmt2plexIons) / sizeof(ReporterIon); i < end; ++i)
                reporterIonColumnHeaders.push_back(lexical_cast<string>("TMT-") + tmt2plexIons[i].name);
    }
    else
        throw runtime_error("[getReporterIonHeaders] unrecognized reporter ion array");

    return reporterIonColumnHeaders;
}


int proteinQuery(GroupBy groupBy, const bfs::path& filepath,
                 const vector<ProteinColumn>& enumColumns,
                 const vector<SqlColumn>& selectedColumns,
                 const vector<string>& tokens)
{
    SchemaUpdater::update(filepath.string());
    sqlite::database idpDB(filepath.string());
    SchemaUpdater::createUserSQLiteFunctions(idpDB.connected());
    //idpDB.execute("PRAGMA mmap_size=70368744177664; -- 2^46");

    try {sqlite::query q(idpDB, "SELECT Id FROM Protein LIMIT 1"); q.begin();}
    catch (sqlite::database_error&)
    {
        cerr << "Invalid idpDB file \"" << filepath << "\" - missing or corrupt Protein table." << endl;
        return 1;
    }

    try {sqlite::query q(idpDB, "SELECT Id FROM FilterHistory LIMIT 1"); q.begin();}
    catch (sqlite::database_error&)
    {
        cerr << "Invalid idpDB file \"" << filepath << "\" - the basic filters have not been run on it yet; apply them through the GUI (optionally in --headless mode)." << endl;
        return 1;
    }

    // open output file
    string outputFilepath = bfs::change_extension(filepath.string(), ".tsv").string();
    ofstream outputStream(outputFilepath.c_str(), ios::binary);

    string groupByString = "pro.Id";
    if (groupBy == GroupBy::ProteinGroup) groupByString = "pro.ProteinGroup";
    else if (groupBy == GroupBy::Cluster) groupByString = "pro.Cluster";
    else if (groupBy == GroupBy::Gene) groupByString = "pro.GeneId";
    else if (groupBy == GroupBy::GeneGroup) groupByString = "pro.GeneGroup";

        
    PivotDataByColumnMap pivotDataByColumn;
    PivotDataByColumnMap::const_iterator findAbstractColumnItr;
    PivotDataMap::const_iterator findIdItr;
    map<boost::int64_t, PivotDataType>::const_iterator findPivotColumnItr;
    map<size_t, vector<boost::int64_t> > pivotColumnIdsByAbstractColumn;

    bool hasSpectrumQuantitation = false;
    bool hasPrecursorQuantitation = false;

    sqlite::query quantitationMethodsQuery(idpDB, "SELECT DISTINCT QuantitationMethod FROM SpectrumSource");
    set<QuantitationMethod> quantitationMethods;
    BOOST_FOREACH(sqlite::query::rows row, quantitationMethodsQuery)
        quantitationMethods.insert(QuantitationMethod::get_by_index(row.get<int>(0)).get());

    vector<ReporterIon> itraqMethodIons, tmtMethodIons;
    if (quantitationMethods.count(QuantitationMethod::ITRAQ8plex) > 0)
        itraqMethodIons.assign(itraq8plexIons, itraq8plexIons+8);
    else if (quantitationMethods.count(QuantitationMethod::ITRAQ4plex) > 0)
        itraqMethodIons.assign(itraq4plexIons, itraq4plexIons + 4);

    if (quantitationMethods.count(QuantitationMethod::TMT10plex) > 0)
        tmtMethodIons.assign(tmt10plexIons, tmt10plexIons + 10);
    else if (quantitationMethods.count(QuantitationMethod::TMT6plex) > 0)
        tmtMethodIons.assign(tmt6plexIons, tmt10plexIons + 6);
    else if (quantitationMethods.count(QuantitationMethod::TMT2plex) > 0)
        tmtMethodIons.assign(tmt2plexIons, tmt10plexIons + 2);

    bool hasITRAQ = false;
    bool hasTMT = false;

    // write column headers
    for (size_t i=0; i < tokens.size(); ++i)
    {
        if (tokens[i] == "iTRAQ4plex" && !hasITRAQ)
        {
            hasITRAQ = true;
            hasSpectrumQuantitation = true;
            if (!itraqMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(iTRAQ_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (tokens[i] == "iTRAQ8plex" && !hasITRAQ)
        {
            hasITRAQ = true;
            hasSpectrumQuantitation = true;
            if (!itraqMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(iTRAQ_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (tokens[i] == "TMT2plex" && !hasTMT)
        {
            hasTMT = true;
            hasSpectrumQuantitation = true;
            if (!tmtMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (tokens[i] == "TMT6plex" && !hasTMT)
        {
            hasTMT = true;
            hasSpectrumQuantitation = true;
            if (!tmtMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (tokens[i] == "TMT10plex" && !hasTMT)
        {
            hasTMT = true;
            hasSpectrumQuantitation = true;
            if (!tmtMethodIons.empty())
                BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                    outputStream << header << '\t';
        }
        else if (bal::starts_with(tokens[i], "Pivot"))
        {
            string sql = bal::ends_with(tokens[i], "Source") ? "SELECT Name, Id FROM SpectrumSource ORDER BY Name"
                                                             : "SELECT Name, Id FROM SpectrumSourceGroup ORDER BY Name";
            sqlite::query q(idpDB, sql.c_str());

            vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];

            if (bal::contains(tokens[i], "ITRAQ"))
            {
                if (!itraqMethodIons.empty())
                    BOOST_FOREACH(sqlite::query::rows row, q)
                    {
                        BOOST_FOREACH(const string& header, getReporterIonHeaders(iTRAQ_ions, quantitationMethods))
                            outputStream << row.get<string>(0) << " (" << header << ")\t";
                        pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                    }
            }
            else if (bal::contains(tokens[i], "TMT"))
            {
                if (!tmtMethodIons.empty())
                    BOOST_FOREACH(sqlite::query::rows row, q)
                    {
                        BOOST_FOREACH(const string& header, getReporterIonHeaders(TMT_ions, quantitationMethods))
                            outputStream << row.get<string>(0) << " (" << header << ")\t";
                        pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                    }
            }
            else
            {
                bool includeColumnName = bal::contains(tokens[i], "Precursor");
                BOOST_FOREACH(sqlite::query::rows row, q)
                {
                    outputStream << row.get<string>(0) << (includeColumnName ? " " + tokens[i] : "") << '\t';
                    pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                }
            }

            if (!pivotColumnIds.empty())
                pivotData(idpDB, groupBy, tokens[i], pivotDataByColumn[i]);
        }
        else
        {
            if (bal::contains(tokens[i], "Precursor"))
                hasPrecursorQuantitation = true;
            outputStream << tokens[i] << '\t';
        }
    }
    outputStream << endl;

    // build SQL query
    string sql = "SELECT " + groupByString + ", " + bal::join(selectedColumns | boost::adaptors::map_keys, ", ") + " "
                 "FROM Protein pro "
                 "LEFT JOIN ProteinMetadata pmd ON pro.Id=pmd.Id "
                 "LEFT JOIN ProteinData pd ON pro.Id=pd.Id "
                 "LEFT JOIN ProteinCoverage pc ON pro.Id=pc.Id "
                 "JOIN PeptideInstance pi ON pro.Id=pi.Protein "
                 "JOIN Peptide pep ON pi.Peptide=pep.Id "
                 "JOIN PeptideSpectrumMatch psm ON psm.Peptide=pi.Peptide "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId " +
                 string(hasSpectrumQuantitation ? "LEFT JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id " : "") +
                 string(hasPrecursorQuantitation ? "LEFT JOIN XICMetrics xic ON psm.Id=xic.PsmId " : "") +
                 "GROUP BY " + groupByString;
    cout << sql << endl;
    sqlite::query q(idpDB, sql.c_str());

    // write column values
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        boost::int64_t id = static_cast<boost::int64_t>(row.get<sqlite_int64>(0));

        for (size_t i=0; i < selectedColumns.size(); ++i)
        {
            findAbstractColumnItr = pivotDataByColumn.find(i);

            const SqlColumn& sqlColumn = selectedColumns[i];
            switch (sqlColumn.second)
            {
                case SQLITE_FLOAT:
                case SQLITE_INTEGER:
                    switch (enumColumns[i].index())
                    {
                        case ProteinColumn::PivotMatchesByGroup:
                        case ProteinColumn::PivotMatchesBySource:
                        case ProteinColumn::PivotPeptidesByGroup:
                        case ProteinColumn::PivotPeptidesBySource:
                        case ProteinColumn::PivotSpectraByGroup:
                        case ProteinColumn::PivotSpectraBySource:
                        case ProteinColumn::PivotPrecursorIntensityByGroup:
                        case ProteinColumn::PivotPrecursorIntensityBySource:
                        case ProteinColumn::PivotPrecursorAreaByGroup:
                        case ProteinColumn::PivotPrecursorAreaBySource:
                        case ProteinColumn::PivotPrecursorBestSNRByGroup:
                        case ProteinColumn::PivotPrecursorBestSNRBySource:
                        case ProteinColumn::PivotPrecursorMeanSNRByGroup:
                        case ProteinColumn::PivotPrecursorMeanSNRBySource:
                        {
                            if (findAbstractColumnItr == pivotDataByColumn.end())
                                throw runtime_error("unable to get pivot data for column " + lexical_cast<string>(i));

                            const vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];
                            const PivotDataMap& pivotDataMap = findAbstractColumnItr->second;
                            findIdItr = pivotDataMap.find(id);

                            // for the current protein/gene/cluster/whatever, look it up in the pivotDataMap by its id,
                            // then (even if it's not found): for every source or group, output a column for the value
                            // corresponding to that source or group (or 0 if there is no value for that source or group)
                            if (findIdItr == pivotDataMap.end())
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    outputStream << 0;
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                            else
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    findPivotColumnItr = findIdItr->second.find(pivotColumnIds[j]);
                                    if (findPivotColumnItr == findIdItr->second.end())
                                        outputStream << 0;
                                    else
                                        outputStream << findPivotColumnItr->second;
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
                        }

                        default:
                            if (sqlColumn.second == SQLITE_FLOAT)
                                outputStream << row.get<double>(i+1);
                            else
                                outputStream << row.get<int>(i+1);
                            break;
                    }
                    break;
                case SQLITE_BLOB:
                    switch (enumColumns[i].index())
                    {
                        case ProteinColumn::iTRAQ4plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, itraqMethodIons); break;
                        case ProteinColumn::iTRAQ8plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, itraqMethodIons); break;
                        case ProteinColumn::TMT2plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                        case ProteinColumn::TMT6plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                        case ProteinColumn::TMT10plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, tmtMethodIons); break;
                            
                        case ProteinColumn::PivotITRAQByGroup:
                        case ProteinColumn::PivotITRAQBySource:
                        case ProteinColumn::PivotTMTByGroup:
                        case ProteinColumn::PivotTMTBySource:
                        {
                            if (itraqMethodIons.empty() && (enumColumns[i].index() == ProteinColumn::PivotITRAQByGroup || enumColumns[i].index() == ProteinColumn::PivotITRAQBySource) ||
                                tmtMethodIons.empty() && (enumColumns[i].index() == ProteinColumn::PivotTMTByGroup || enumColumns[i].index() == ProteinColumn::PivotTMTBySource))
                                break;

                            if (findAbstractColumnItr == pivotDataByColumn.end())
                                throw runtime_error("unable to get pivot data for column " + lexical_cast<string>(i));

                            const vector<boost::int64_t>& pivotColumnIds = pivotColumnIdsByAbstractColumn[i];
                            const PivotDataMap& pivotDataMap = findAbstractColumnItr->second;
                            findIdItr = pivotDataMap.find(id);

                            // for the current protein/gene/cluster/whatever, look it up in the pivotDataMap by its id,
                            // then (even if it's not found): for every source or group, output a column for the value
                            // corresponding to that source or group (or 0 if there is no value for that source or group)
                            if (findIdItr == pivotDataMap.end())
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    if (enumColumns[i].index() == ProteinColumn::PivotITRAQByGroup || enumColumns[i].index() == ProteinColumn::PivotITRAQBySource)
                                        writeBlobArray<double>(NULL, outputStream, itraqMethodIons);
                                    else if (enumColumns[i].index() == ProteinColumn::PivotTMTByGroup || enumColumns[i].index() == ProteinColumn::PivotTMTBySource)
                                        writeBlobArray<double>(NULL, outputStream, tmtMethodIons);
                                    
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                            else
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    findPivotColumnItr = findIdItr->second.find(pivotColumnIds[j]);
                                    if (enumColumns[i].index() == ProteinColumn::PivotITRAQByGroup || enumColumns[i].index() == ProteinColumn::PivotITRAQBySource)
                                    {
                                        if (findPivotColumnItr == findIdItr->second.end())
                                            writeBlobArray<double>(NULL, outputStream, itraqMethodIons);
                                        else
                                            writeBlobArray<double>(boost::get<const void*>(findPivotColumnItr->second), outputStream, itraqMethodIons);
                                    }
                                    else if (enumColumns[i].index() == ProteinColumn::PivotTMTByGroup || enumColumns[i].index() == ProteinColumn::PivotTMTBySource)
                                    {
                                        if (findPivotColumnItr == findIdItr->second.end())
                                            writeBlobArray<double>(NULL, outputStream, tmtMethodIons);
                                        else
                                            writeBlobArray<double>(boost::get<const void*>(findPivotColumnItr->second), outputStream, tmtMethodIons);
                                    }
                                    
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
                        }

                        default: throw runtime_error("unknown enum column type");
                    }
                    break;
                case SQLITE_TEXT:
                    {
                        const char* str = row.get<const char*>(i+1);
                        if (str != NULL) outputStream << str;
                    }
                    break;
                default:
                    throw runtime_error("unknown SQL column type");
            }

            if (i < selectedColumns.size()-1)
                outputStream << '\t';
        }
        outputStream << '\n';
    }

    outputStream.close();

    return 0;
}


int query(GroupBy groupBy, const vector<string>& args)
{
    vector<string> tokens;
    bal::split(tokens, args[2], bal::is_any_of(","));

    if (groupBy >= GroupBy::Protein && groupBy <= GroupBy::GeneGroup)
    {
        vector<string> invalidTokens;

        vector<ProteinColumn> enumColumns;
        vector<SqlColumn> selectedColumns;
        for (size_t i=0 ; i < tokens.size(); ++i)
        {
            ProteinColumn newColumn = ProteinColumn::get_by_name(tokens[i].c_str()).get_value_or(ProteinColumn::Invalid);
            if (newColumn == ProteinColumn::Invalid)
                invalidTokens.push_back(tokens[i]);
            else
            {
                enumColumns.push_back(newColumn);
                selectedColumns.push_back(newColumn.value());
            }
        }

        if (!invalidTokens.empty())
        {
            cerr << "Invalid column choice" << (invalidTokens.size() > 1 ? "s" : "") << ":";
            for (size_t i=0; i < invalidTokens.size(); ++i)
                cerr << " \"" << invalidTokens[i] << "\"";
            cerr << "\nValid options are:" << endl;
            for (ProteinColumn::const_iterator itr = ProteinColumn::begin()+1; itr < ProteinColumn::end(); ++itr)
                cerr << "  " << itr->str() << "\n";
            return 1;
        }
        
        
        vector<bfs::path> filepaths;
        for (size_t i=3; i < args.size(); ++i)
        {
            size_t oldSize = filepaths.size();
            pwiz::util::expand_pathmask(args[i], filepaths);
            if (filepaths.size() == oldSize)
                cerr << "Filemask or idpDB file \"" << args[i] << "\" does not exist." << endl;
        }

        if (filepaths.empty())
        {
            cerr << "No idpDB files specified. Nothing to do." << endl;
            return 1;
        }
        
        int result = 0;
        BOOST_FOREACH(const bfs::path& filepath, filepaths)
        {
            if ((groupBy == GroupBy::Gene || groupBy == GroupBy::GeneGroup) && !Embedder::hasGeneMetadata(filepath.string()))
            {
                cerr << "Error: cannot group by gene or gene group because \"" << filepath.string() << "\" does not have embedded gene metadata; use idpQonvert to embed gene metadata." << endl;
                ++result;
                continue;
            }

            result += proteinQuery(groupBy, filepath, enumColumns, selectedColumns, tokens);
        }
        return result;
    }

    return 0;
}

END_IDPICKER_NAMESPACE


int main(int argc, const char* argv[])
{
    cout << "IDPickerQuery " << idpQuery::Version::str() << " (" << idpQuery::Version::LastModified() << ")\n" <<
            "IDPickerCore " << IDPicker::Version::str() << " (" << IDPicker::Version::LastModified() << ")\n"  << endl;

    string usage = "Usage: idpQuery <group by field> <comma-delimited export column fields> <idpDB filepath>\n"
                   "\nExample: idpQuery ProteinGroup Accession,FilteredSpectra,PercentCoverage,iTRAQ4plex data.idpDB\n";
    
    usage += string("\nValid \"group by\" fields: ") + (GroupBy::begin()+1)->str();
    for (GroupBy::const_iterator itr = GroupBy::begin()+2; itr < GroupBy::end(); ++itr)
        usage += string(", ") + itr->str();
    usage += "\nValid \"export column\" fields depend on the \"group by\" field; run idpQuery with only the group by field as an argument to see details.\n";

    GroupBy groupBy;
    vector<string> args;
    
    if (argc > 1)
    {
        args.assign(argv, argv+argc);
        groupBy = GroupBy::get_by_name(args[1].c_str()).get_value_or(GroupBy::Invalid);
        if (groupBy == GroupBy::Invalid)
        {
            cerr << "Invalid grouping choice \"" << args[1] << "\". Valid options are:" << endl;
            for (GroupBy::const_iterator itr = GroupBy::begin()+1; itr < GroupBy::end(); ++itr)
                cerr << "  " << itr->str() << "\n";
            return 1;
        }
        
        //if (groupBy >= GroupBy::Protein && groupBy <= GroupBy::Cluster)
        //usage += 
    }
    else 
    //    usage += "\n\nTo see what export columns are valid with each \"group by\" field, run idpQuery with only the group by field as an argument.";
    {
        usage += "\n\n"
                 "Protein, ProteinGroup, Cluster, Gene, GeneGroup\n"
                 "------------------------------\n";
        for (ProteinColumn::const_iterator itr = ProteinColumn::begin()+1; itr < ProteinColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";

        /*usage += "\n\n"
                 "DistinctMatch, Peptide, PeptideGroup\n"
                 "------------------------------------\n";
        for (PeptideColumn::const_iterator itr = PeptideColumn::begin()+1; itr < PeptideColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";

        usage += "\n\n"
                 "PeptideSpectrumMatch, Spectrum, SpectrumSourceAndGroup\n"
                 "------------------------------------------------------\n";
        for (SpectrumColumn::const_iterator itr = SpectrumColumn::begin()+1; itr < SpectrumColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";

        usage += "\n\n"
                 "Modification, DeltaMass, ModifiedSite\n"
                 "-------------------------------------\n";
        for (ModificationColumn::const_iterator itr = ModificationColumn::begin()+1; itr < ModificationColumn::end(); ++itr)
            usage += string("  ") + itr->str() + "\n";*/
    }

    if (argc < 4)
    {
        cerr << "Not enough arguments.\n\n" <<
                usage << endl;
        return 1;
    }

    try
    {
	    return query(groupBy, args);
    }
    catch (exception& e)
    {
        cerr << "Unhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception." << endl;
    }
    return 1;
}
