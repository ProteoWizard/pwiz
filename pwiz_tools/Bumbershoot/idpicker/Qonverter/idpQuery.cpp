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
    (Accession)(make_pair("pro.Accession", SQLITE_TEXT))
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
    (iTRAQ4plex)(make_pair("proq.iTRAQ_ReporterIonIntensities", SQLITE_BLOB))
    (iTRAQ8plex)(make_pair("proq.iTRAQ_ReporterIonIntensities", SQLITE_BLOB))
    (TMT2plex)(make_pair("proq.TMT_ReporterIonIntensities", SQLITE_BLOB))
    (TMT6plex)(make_pair("proq.TMT_ReporterIonIntensities", SQLITE_BLOB))
    /*(PivotMatchesByGroup)(make_pair("", 0))
    (PivotMatchesBySource)(make_pair("", 0))
    (PivotPeptidesByGroup)(make_pair("", 0))
    (PivotPeptidesBySource)(make_pair("", 0))
    (PivotSpectraByGroup)(make_pair("", 0))
    (PivotSpectraBySource)(make_pair("", 0))*/
    (PeptideGroups)(make_pair("GROUP_CONCAT(DISTINCT pep.PeptideGroup)", SQLITE_TEXT))
    (PeptideSequences)(make_pair("GROUP_CONCAT(DISTINCT (SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), pep.DecoySequence) FROM PeptideInstance pi LEFT JOIN ProteinData pd ON pi.Protein=pd.Id WHERE pep.Id=pi.Peptide))", SQLITE_TEXT))
);


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


int query(GroupBy groupBy, const vector<string>& args)
{
    vector<string> tokens;
    bal::split(tokens, args[2], bal::is_any_of(","));

    if (groupBy >= GroupBy::Protein && groupBy <= GroupBy::Cluster)
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
        
        if (!bfs::exists(args[3]))
        {
            cerr << "IdpDB file \"" << args[3] << "\" does not exist." << endl;
            return 1;
        }

        sqlite::database idpDB(args[3]);

        try {sqlite::query q(idpDB, "SELECT Id FROM Protein LIMIT 1"); q.begin();}
        catch (sqlite::database_error&)
        {
            cerr << "Invalid idpDB file \"" << args[3] << "\" - missing or corrupt Protein table." << endl;
            return 1;
        }

        try {sqlite::query q(idpDB, "SELECT Id FROM FilterHistory LIMIT 1"); q.begin();}
        catch (sqlite::database_error&)
        {
            cerr << "Invalid idpDB file \"" << args[3] << "\" - the basic filters have not been run on it yet; apply them through the GUI (optionally in --headless mode)." << endl;
            return 1;
        }

        // open output file
        string outputFilepath = bfs::change_extension(args[3], ".tsv").string();
        ofstream outputStream(outputFilepath.c_str(), ios::binary);

        string groupByString = "pro.Id";
        if (groupBy == GroupBy::ProteinGroup) groupByString = "pro.ProteinGroup";
        else if (groupBy == GroupBy::Cluster) groupByString = "pro.Cluster";

        // build SQL query
        string sql = "SELECT " + bal::join(selectedColumns | boost::adaptors::map_keys, ", ") + " "
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

        // write column headers
        for (size_t i=0 ; i < tokens.size(); ++i)
        {
            if (tokens[i] == "iTRAQ4plex")
                for(int j=1; j < 5; ++j) outputStream << "iTRAQ-" << iTRAQ_masses[j] << '\t';
            else if (tokens[i] == "iTRAQ8plex")
                for(int j=0; j < 8; ++j) outputStream << "iTRAQ-" << iTRAQ_masses[j] << '\t';
            else if (tokens[i] == "TMT2plex")
                for(int j=0; j < 2; ++j) outputStream << "TMT-" << TMT_masses[j] << '\t';
            else if (tokens[i] == "TMT6plex")
                for(int j=0; j < 6; ++j) outputStream << "TMT-" << TMT_masses[j] << '\t';
            else
                outputStream << tokens[i] << '\t';
        }
        outputStream << endl;

        // write column values
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            for (size_t i=0; i < selectedColumns.size(); ++i)
            {
                const SqlColumn& sqlColumn = selectedColumns[i];
                switch (sqlColumn.second)
                {
                    case SQLITE_FLOAT: outputStream << row.get<double>(i); break;
                    case SQLITE_INTEGER: outputStream << row.get<int>(i); break;
                    case SQLITE_BLOB:
                        switch (enumColumns[i].index())
                        {
                            case ProteinColumn::iTRAQ4plex: writeBlobArray<double>(row.get<const void*>(i), outputStream, 1, 4); break;
                            case ProteinColumn::iTRAQ8plex: writeBlobArray<double>(row.get<const void*>(i), outputStream, 0, 8); break;
                            case ProteinColumn::TMT2plex: writeBlobArray<double>(row.get<const void*>(i), outputStream, 0, 2); break;
                            case ProteinColumn::TMT6plex: writeBlobArray<double>(row.get<const void*>(i), outputStream, 0, 6); break;
                            default: throw runtime_error("unknown enum column type");
                        }
                        break;
                    case SQLITE_TEXT:
                        {
                            const char* str = row.get<const char*>(i);
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
    }

    return 0;
}

END_IDPICKER_NAMESPACE


int main(int argc, const char* argv[])
{
    cout << "IDPickerQuery " << IDPicker::Version::str() << " (" << IDPicker::Version::LastModified() << ")\n" <<
            "" << endl;

    string usage = "Usage: idpQuery <group by field> <comma-delimited export column fields> <idpDB filepath>\n"
                   "\nExample: idpQuery ProteinGroup Accession,FilteredSpectra,PercentCoverage,iTRAQ data.idpDB\n";
    
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
                 "Protein, ProteinGroup, Cluster\n"
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
