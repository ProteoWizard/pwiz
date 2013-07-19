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
#include "boost/crc.hpp"
#include "boost/variant.hpp"

#include "SchemaUpdater.hpp"
#include "Parser.hpp"
#include "Embedder.hpp"
#include "sqlite3pp.h"
#include <iomanip>
//#include "svnrev.hpp"

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
    (iTRAQ4plex)(make_pair("proq.iTRAQ_ReporterIonIntensities", SQLITE_BLOB))
    (iTRAQ8plex)(make_pair("proq.iTRAQ_ReporterIonIntensities", SQLITE_BLOB))
    (TMT2plex)(make_pair("proq.TMT_ReporterIonIntensities", SQLITE_BLOB))
    (TMT6plex)(make_pair("proq.TMT_ReporterIonIntensities", SQLITE_BLOB))
    (PivotMatchesByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotMatchesBySource)(make_pair("0", SQLITE_INTEGER))
    (PivotPeptidesByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotPeptidesBySource)(make_pair("0", SQLITE_INTEGER))
    (PivotSpectraByGroup)(make_pair("0", SQLITE_INTEGER))
    (PivotSpectraBySource)(make_pair("0", SQLITE_INTEGER))
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

struct Version
{
    static int Major();
    static int Minor();
    static int Revision();
    static std::string str();
    static std::string LastModified();
};

int Version::Major()                {return 3;}
int Version::Minor()                {return 0;}
int Version::Revision()             {return 0;}//SVN_REV;}
string Version::LastModified()      {return "";}//SVN_REVDATE;}
string Version::str()
{
    std::ostringstream v;
    v << Major() << "." << Minor() << "." << Revision();
    return v.str();
}


struct DistinctDoubleArraySum
{
    typedef DistinctDoubleArraySum MyType;
    set<int> arrayIds;
    vector<double> result;
    boost::crc_32_type crc32;

    DistinctDoubleArraySum(int arrayLength) : result((size_t) arrayLength, 0.0) {}

    static void Step(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        MyType** ppThis = static_cast<MyType**>(sqlite3_aggregate_context(context, sizeof(MyType*)));
        MyType* pThis = *ppThis;

        if (numValues > 1 || values[0] == NULL)
            return;

        int arrayByteCount = sqlite3_value_bytes(values[0]);
        int arrayLength = arrayByteCount / 8;
        const char* arrayBytes = static_cast<const char*>(sqlite3_value_blob(values[0]));
        if (arrayBytes == NULL || arrayByteCount % 8 > 0)
            throw runtime_error("distinct_double_array_sum only works with BLOBs of double precision floats");

        if (pThis == NULL)
            pThis = new DistinctDoubleArraySum(arrayLength);
        else
            pThis->crc32.reset();

        // if the arrayId was already in the set, ignore its values
        pThis->crc32.process_bytes(arrayBytes, arrayByteCount);
        int arrayId = pThis->crc32.checksum();
        if (!pThis->arrayIds.insert(arrayId).second)
            return;

        const double* arrayValues = reinterpret_cast<const double*>(arrayBytes);

        for (int i = 0; i < arrayLength; ++i)
            pThis->result[i] += arrayValues[i];
    }

    static void Final(sqlite3_context* context)
    {
        MyType** ppThis = static_cast<MyType**>(sqlite3_aggregate_context(context, sizeof(MyType*)));
        MyType* pThis = *ppThis;
        
        if (pThis == NULL)
            pThis = new DistinctDoubleArraySum(0);

        sqlite3_result_blob(context, &pThis->result[0], pThis->result.size() * sizeof(double), SQLITE_TRANSIENT);
        delete pThis;
    }
};


void createUserSQLiteFunctions(sqlite::database& idpDB)
{
    int result = sqlite3_create_function(idpDB.connected(), "distinct_double_array_sum", -1, SQLITE_ANY,
                                         0, NULL, &DistinctDoubleArraySum::Step, &DistinctDoubleArraySum::Final);
    if (result != 0)
        throw runtime_error("unable to create user function: SQLite error " + lexical_cast<string>(result));
}


template <typename ArrayType>
void writeBlobArray(const void* blob, ostream& os, size_t offset, size_t length)
{
    if (blob == NULL)
    {
        os << "0";
        for (++offset; offset < length; ++offset)
            os << "\t0";
        return;
    }

    const ArrayType* blobArray = reinterpret_cast<const ArrayType*>(blob);
    streamsize oldPrecision = os.precision(0);
    ios::fmtflags oldFlags = os.flags(ios::fixed);
    copy(blobArray+offset, blobArray+offset+length, ostream_iterator<ArrayType>(os, "\t"));
    os.seekp(-1, ios::cur);
    os.precision(oldPrecision);
    os.flags(oldFlags);
}


typedef boost::variant<int, const void*> PivotDataType;
typedef map<boost::int64_t, map<boost::int64_t, PivotDataType> > PivotDataMap;
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
            countColumn = "DISTINCT_DOUBLE_ARRAY_SUM(sq.iTRAQ_ReporterIonIntensities)";
        else if (bal::contains(pivotMode, "TMT"))
            countColumn = "DISTINCT_DOUBLE_ARRAY_SUM(sq.TMT_ReporterIonIntensities)";
        else
            throw runtime_error("unsupported pivot mode");

        // build SQL query with spectrum quantitation
        string sql = "SELECT " + groupByString + ", ssg.Id, " + countColumn + " "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "JOIN SpectrumSource ss ON s.Source=ss.Id "
                     "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                     "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                     "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                     "JOIN Protein pro ON pi.Protein=pro.Id "
                     "LEFT JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id "
                     "GROUP BY " + groupByString + ", " + pivotColumn;
    
        sqlite::query q(idpDB, sql.c_str());
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            int blobBytes = row.column_bytes(2);
            char* blobCopy = new char[blobBytes];
            memcpy(blobCopy, (const char*) row.get<const void*>(2), blobBytes);
            pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = blobCopy;
        }
        return;
    }

    // build SQL query without spectrum quantitation
    string sql = "SELECT " + groupByString + ", ssg.Id, " + countColumn + " "
                 "FROM PeptideSpectrumMatch psm "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId "
                 "JOIN Spectrum s ON psm.Spectrum=s.Id "
                 "JOIN SpectrumSource ss ON s.Source=ss.Id "
                 "JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source "
                 "JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id "
                 "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                 "JOIN Protein pro ON pi.Protein=pro.Id "
                 "GROUP BY " + groupByString + ", " + pivotColumn;
    
    sqlite::query q(idpDB, sql.c_str());
    BOOST_FOREACH(sqlite::query::rows row, q)
        pivotDataMap[row.get<sqlite_int64>(0)][row.get<sqlite_int64>(1)] = row.get<int>(2);
}


int proteinQuery(GroupBy groupBy, const bfs::path& filepath,
                 const vector<ProteinColumn>& enumColumns,
                 const vector<SqlColumn>& selectedColumns,
                 const vector<string>& tokens)
{
    SchemaUpdater::update(filepath.string());
    sqlite::database idpDB(filepath.string());
    createUserSQLiteFunctions(idpDB);

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

    // build SQL query
    string sql = "SELECT " + groupByString + ", " + bal::join(selectedColumns | boost::adaptors::map_keys, ", ") + " "
                 "FROM Protein pro "
                 "LEFT JOIN ProteinMetadata pmd ON pro.Id=pmd.Id "
                 "LEFT JOIN ProteinData pd ON pro.Id=pd.Id "
                 "LEFT JOIN ProteinCoverage pc ON pro.Id=pc.Id "
                 "JOIN PeptideInstance pi ON pro.Id=pi.Protein "
                 "JOIN Peptide pep ON pi.Peptide=pep.Id "
                 "JOIN PeptideSpectrumMatch psm ON psm.Peptide=pi.Peptide "
                 "JOIN DistinctMatch dm ON psm.Id=dm.PsmId "
                 "LEFT JOIN ProteinQuantitation proq ON pro.Id=proq.Id "
                 "GROUP BY " + groupByString;
    cout << sql << endl;
    sqlite::query q(idpDB, sql.c_str());

    int iTRAQ_masses[8] = { 113, 114, 115, 116, 117, 118, 119, 121 };
    int TMT_masses[6] = { 126, 127, 128, 129, 130, 131 };
        
    PivotDataMap pivotDataMap;
    PivotDataMap::const_iterator findIdItr;
    map<boost::int64_t, PivotDataType>::const_iterator findColumnItr;
    vector<boost::int64_t> pivotColumnIds;
    PivotDataType zeroBlob = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0";

    sqlite::query itraqPlexQuery(idpDB, "SELECT DISTINCT substr(hex(iTRAQ_ReporterIonIntensities), 1, 16) = '0000000000000000' AND "
                                        "COUNT(DISTINCT substr(hex(iTRAQ_ReporterIonIntensities), 1, 16)) = 1 "
                                        "FROM SpectrumQuantitation");
    bool hasITRAQ113 = itraqPlexQuery.begin()->get<int>(0) == 0;

    sqlite::query tmtPlexQuery(idpDB, "SELECT DISTINCT substr(hex(TMT_ReporterIonIntensities), 33, 16) = '0000000000000000' AND "
                                      "COUNT(DISTINCT substr(hex(TMT_ReporterIonIntensities), 33, 16)) = 1 "
                                      "FROM SpectrumQuantitation");
    bool hasTMT128 = tmtPlexQuery.begin()->get<int>(0) == 0;

    // write column headers
    for (size_t i=0; i < tokens.size(); ++i)
    {
        if (tokens[i] == "iTRAQ4plex")
            for(int j=1; j < 5; ++j) outputStream << "iTRAQ-" << iTRAQ_masses[j] << '\t';
        else if (tokens[i] == "iTRAQ8plex")
            for(int j=0; j < 8; ++j) outputStream << "iTRAQ-" << iTRAQ_masses[j] << '\t';
        else if (tokens[i] == "TMT2plex")
            for(int j=0; j < 2; ++j) outputStream << "TMT-" << TMT_masses[j] << '\t';
        else if (tokens[i] == "TMT6plex")
            for(int j=0; j < 6; ++j) outputStream << "TMT-" << TMT_masses[j] << '\t';
        else if (bal::starts_with(tokens[i], "Pivot"))
        {            
            string sql = bal::ends_with(tokens[i], "Source") ? "SELECT Name, Id FROM SpectrumSource"
                                                             : "SELECT Name, Id FROM SpectrumSourceGroup";
            sqlite::query q(idpDB, sql.c_str());

            if (bal::contains(tokens[i], "ITRAQ"))
            {
                BOOST_FOREACH(sqlite::query::rows row, q)
                {
                    for(int j = hasITRAQ113 ? 0 : 1; j < (hasITRAQ113 ? 8 : 5); ++j)
                        outputStream << row.get<string>(0) << " (iTRAQ-" << iTRAQ_masses[j] << ")\t";
                    pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                }
            }
            else if (bal::contains(tokens[i], "TMT"))
            {
                BOOST_FOREACH(sqlite::query::rows row, q)
                {
                    for(int j = 0; j < (hasTMT128 ? 6 : 2); ++j)
                        outputStream << row.get<string>(0) << " (TMT-" << iTRAQ_masses[j] << ")\t";
                    pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                }
            }
            else
            {
                BOOST_FOREACH(sqlite::query::rows row, q)
                {
                    outputStream << row.get<string>(0) << '\t';
                    pivotColumnIds.push_back(static_cast<boost::int64_t>(row.get<sqlite3_int64>(1)));
                }
            }

            pivotData(idpDB, groupBy, tokens[i], pivotDataMap);
        }
        else
            outputStream << tokens[i] << '\t';
    }
    outputStream << endl;

    // write column values
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        boost::int64_t id = static_cast<boost::int64_t>(row.get<sqlite_int64>(0));
        findIdItr = pivotDataMap.find(id);

        for (size_t i=0; i < selectedColumns.size(); ++i)
        {
            const SqlColumn& sqlColumn = selectedColumns[i];
            switch (sqlColumn.second)
            {
                case SQLITE_FLOAT: outputStream << row.get<double>(i+1); break;
                case SQLITE_INTEGER:
                    switch (enumColumns[i].index())
                    {
                        case ProteinColumn::PivotMatchesByGroup:
                        case ProteinColumn::PivotMatchesBySource:
                        case ProteinColumn::PivotPeptidesByGroup:
                        case ProteinColumn::PivotPeptidesBySource:
                        case ProteinColumn::PivotSpectraByGroup:
                        case ProteinColumn::PivotSpectraBySource:
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
                                    findColumnItr = findIdItr->second.find(pivotColumnIds[j]);
                                    if (findColumnItr == findIdItr->second.end())
                                        outputStream << 0;
                                    else
                                        outputStream << findColumnItr->second;
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
                        default: outputStream << row.get<int>(i+1); break;
                    }
                    break;
                case SQLITE_BLOB:
                    switch (enumColumns[i].index())
                    {
                        case ProteinColumn::iTRAQ4plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, 1, 4); break;
                        case ProteinColumn::iTRAQ8plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, 0, 8); break;
                        case ProteinColumn::TMT2plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, 0, 2); break;
                        case ProteinColumn::TMT6plex: writeBlobArray<double>(row.get<const void*>(i+1), outputStream, 0, 6); break;
                            
                        case ProteinColumn::PivotITRAQByGroup:
                        case ProteinColumn::PivotITRAQBySource:
                        case ProteinColumn::PivotTMTByGroup:
                        case ProteinColumn::PivotTMTBySource:
                            // for the current protein/gene/cluster/whatever, look it up in the pivotDataMap by its id,
                            // then (even if it's not found): for every source or group, output a column for the value
                            // corresponding to that source or group (or 0 if there is no value for that source or group)
                            if (findIdItr == pivotDataMap.end())
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    if (enumColumns[i].index() == ProteinColumn::PivotITRAQByGroup || enumColumns[i].index() == ProteinColumn::PivotITRAQBySource)
                                        writeBlobArray<double>(boost::get<const void*>(zeroBlob), outputStream, 0, hasITRAQ113 ? 8 : 4);
                                    else if (enumColumns[i].index() == ProteinColumn::PivotTMTByGroup || enumColumns[i].index() == ProteinColumn::PivotTMTBySource)
                                        writeBlobArray<double>(boost::get<const void*>(zeroBlob), outputStream, 0, hasTMT128 ? 6 : 2);
                                    
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                            else
                                for (size_t j=0; j < pivotColumnIds.size(); ++j)
                                {
                                    findColumnItr = findIdItr->second.find(pivotColumnIds[j]);
                                    if (enumColumns[i].index() == ProteinColumn::PivotITRAQByGroup || enumColumns[i].index() == ProteinColumn::PivotITRAQBySource)
                                    {
                                        if (findColumnItr == findIdItr->second.end())
                                            writeBlobArray<double>(boost::get<const void*>(zeroBlob), outputStream, 0, hasITRAQ113 ? 8 : 4);
                                        else if (hasITRAQ113)
                                            writeBlobArray<double>(boost::get<const void*>(findColumnItr->second), outputStream, 0, 8);
                                        else
                                            writeBlobArray<double>(boost::get<const void*>(findColumnItr->second), outputStream, 1, 4);
                                    }
                                    else if (enumColumns[i].index() == ProteinColumn::PivotTMTByGroup || enumColumns[i].index() == ProteinColumn::PivotTMTBySource)
                                    {
                                        if (findColumnItr == findIdItr->second.end())
                                            writeBlobArray<double>(boost::get<const void*>(zeroBlob), outputStream, 0, hasTMT128 ? 6 : 2);
                                        else if (hasTMT128)
                                            writeBlobArray<double>(boost::get<const void*>(findColumnItr->second), outputStream, 0, 2);
                                        else
                                            writeBlobArray<double>(boost::get<const void*>(findColumnItr->second), outputStream, 0, 6);
                                    }
                                    
                                    if (j < pivotColumnIds.size()-1)
                                        outputStream << '\t';
                                }
                                break;
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
            result += proteinQuery(groupBy, filepath, enumColumns, selectedColumns, tokens);
        return result;
    }

    return 0;
}

END_IDPICKER_NAMESPACE


int main(int argc, const char* argv[])
{
    cout << "IDPickerQuery " << IDPicker::Version::str() << " (" << IDPicker::Version::LastModified() << ")\n" <<
            "" << endl;

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
