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
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "boost/foreach_field.hpp"
#include "boost/range/algorithm/remove_if.hpp"
#include "boost/range/adaptor/map.hpp"
#include "boost/crc.hpp"
#include "boost/variant.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"
#include "boost/core/null_deleter.hpp"

#include "SchemaUpdater.hpp"
#include "TotalCounts.hpp"
#include "Parser.hpp"
#include "Merger.hpp"
#include "Filter.hpp"
#include "Embedder.hpp"
#include "Logger.hpp"
#include <boost/log/expressions.hpp>
#include <boost/log/utility/setup/common_attributes.hpp>
#include <boost/log/sinks/sync_frontend.hpp>
#include <boost/log/sinks/text_ostream_backend.hpp>
#include "sqlite3pp.h"
#include <iomanip>
#include "CoreVersion.hpp"
#include "idpAssembleVersion.hpp"


using namespace IDPicker;
using namespace pwiz::util;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;
namespace bxp = boost::xpressive;
namespace expr = boost::log::expressions;
namespace sinks = boost::log::sinks;


BOOST_LOG_ATTRIBUTE_KEYWORD(line_id, "LineID", unsigned int)
BOOST_LOG_ATTRIBUTE_KEYWORD(severity, "Severity", MessageSeverity::domain)

struct severity_tag;
// The operator is used when putting the severity level to log
boost::log::formatting_ostream& operator<<
(
    boost::log::formatting_ostream& strm,
    const boost::log::to_log_manip< MessageSeverity::domain, severity_tag>& manip
)
{
    if (manip.get() >= MessageSeverity::Warning || manip.get() == MessageSeverity::DebugInfo)
        strm << '\n' << MessageSeverity::get_by_value(manip.get()).get().str() << ": ";
    return strm;
}

template <typename T>
T& parse(T& lhs, const typename T::domain& rhs)
{
    // boost::lexical_cast fails on BOOST_ENUM types
    stringstream ss;
    ss << T(rhs);
    ss >> lhs;
    return lhs;
}

template <typename T, typename S>
inline T& parse(T& lhs, const S& rhs) { return lhs = lexical_cast<T, S>(rhs); }

void embedIsobaricSampleMapping(const string& idpDbFilepath, const string& isobaricSampleMappingFilepath)
{
    // open the mapping file; format is "<Group Name>\t<Sample1>,<Sample2>,<Sample3>,<Sample4>"
    ifstream isobaricSampleMappingFile(isobaricSampleMappingFilepath.c_str());

    map<string, sqlite3_int64> groupIdByName;
    map<string, vector<string> > isobaricSampleMapping;

    sqlite3pp::database idpDb(idpDbFilepath);
    sqlite3pp::query sourceIdByNameQuery(idpDb, "SELECT Id, Name FROM SpectrumSourceGroup");
    for(sqlite3pp::query::rows queryRow : sourceIdByNameQuery)
    {
        groupIdByName[queryRow.get<string>(1)] = queryRow.get<sqlite3_int64>(0);
    }

    bxp::sregex groupFilemaskRegex = bxp::sregex::compile("(\\S+)\t(\\S+)");
    bxp::smatch match;

    string line;
    while (getlinePortable(isobaricSampleMappingFile, line))
    {
        if (line.empty())
            continue;

        try
        {
            bxp::regex_match(line, match, groupFilemaskRegex);
            string groupName = match[1].str();
            string sampleNamesString = match[2].str();

            if (groupIdByName.count(groupName) == 0)
                throw runtime_error("assembly does not contain a group named \"" + groupName + "\"");

            bal::split(isobaricSampleMapping[groupName], sampleNamesString, bal::is_any_of(","));

            if (isobaricSampleMapping[groupName].size() < 2)
                throw runtime_error("there must be at least 2 samples (i.e. TMT duplex)");
        }
        catch (exception& e)
        {
            throw runtime_error("error reading line \"" + line + "\" from isobaric sample mapping file: " + e.what());
        }
    }

    if (!isobaricSampleMapping.empty())
        Embedder::embedIsobaricSampleMapping(idpDbFilepath, isobaricSampleMapping);
}


void assignSourceGroupHierarchy(const string& idpDbFilepath, const string& assemblyFilepath)
{
    sqlite3pp::database idpDb(idpDbFilepath);

    map<string, sqlite3_int64> sourceIdByName;
    map<string, set<sqlite3_int64> > sourcesByGroup;
    set<sqlite3_int64> ungroupedSources;
    vector<string> sourceGroups;

    sqlite3pp::query sourceIdByNameQuery(idpDb, "SELECT Id, Name FROM SpectrumSource");
    for(sqlite3pp::query::rows queryRow : sourceIdByNameQuery)
    {
        sourceIdByName[queryRow.get<string>(1)] = queryRow.get<sqlite3_int64>(0);
        ungroupedSources.insert(queryRow.get<sqlite3_int64>(0));
    }

    // open the assembly.txt file
    ifstream assembleTxtFile(assemblyFilepath.c_str());

    bxp::sregex groupFilemaskRegex = bxp::sregex::compile("((\"(.+)\")|(\\S+))\\s+((\"(.+)\")|(\\S+))");
    bxp::smatch match;

    string line;
    while (getlinePortable(assembleTxtFile, line))
    {
        if (line.empty())
            continue;

        try
        {
            bxp::regex_match(line, match, groupFilemaskRegex);
            string group = match[3].str() + match[4].str();
            string filemask = match[7].str() + match[8].str();

            // for wildcards, use old style behavior
            if (filemask.find_first_of("*?") != string::npos)
            {
                bfs::path filemask(filemask);
                if (!filemask.has_root_directory())
                    filemask = bfs::path(assemblyFilepath).parent_path() / filemask;

                if (!bfs::exists(filemask.parent_path()))
                    continue;

                vector<bfs::path> matchingPaths;
                expand_pathmask(filemask, matchingPaths);
                for(bfs::path& filepath : matchingPaths)
                {
                    bfs::path basename = filepath.filename().replace_extension("");
                    map<string, sqlite3_int64>::const_iterator findItr = sourceIdByName.find(basename.string());
                    if (findItr == sourceIdByName.end())
                        continue;

                    sourcesByGroup[group].insert(findItr->second);
                    ungroupedSources.erase(findItr->second);
                }
            }
            else
            {
                // otherwise, match directly to source names
                string sourceName = bfs::path(filemask).filename().replace_extension("").string();
                map<string, sqlite3_int64>::const_iterator findItr = sourceIdByName.find(sourceName);
                if (findItr == sourceIdByName.end())
                    continue;

                sourcesByGroup[group].insert(findItr->second);
                ungroupedSources.erase(findItr->second);
            }
        }
        catch (exception& e)
        {
            throw runtime_error("error reading line \"" + line + "\" from assembly text at \"" + assemblyFilepath + "\": " + e.what());
        }
    }

    // assign ungrouped source to the root group
    for(sqlite3_int64 sourceId : ungroupedSources)
        sourcesByGroup["/"].insert(sourceId);

    sqlite3pp::transaction transaction(idpDb);

    // delete old groups
    idpDb.execute("UPDATE SpectrumSource SET Group_ = NULL;"
                  "DELETE FROM SpectrumSourceGroup;"
                  "DELETE FROM SpectrumSourceGroupLink;");

    sqlite3pp::command addSpectrumSourceGroup(idpDb, "INSERT INTO SpectrumSourceGroup (Id, Name) VALUES (?,?)");
    sqlite3pp::command updateSpectrumSource(idpDb, "UPDATE SpectrumSource SET Group_=? WHERE Id=?");
    sqlite3pp::command addSpectrumSourceGroupLink(idpDb, "INSERT INTO SpectrumSourceGroupLink (Id, Source, Group_) VALUES (?,?,?)");

    map<string, set<sqlite3_int64> > sourcesByParentGroup;
    int groupId = 1; // id 1 is reserved for '/'
    int linkId = 0;
    BOOST_FOREACH_FIELD((const string& group)(set<sqlite3_int64>& sources), sourcesByGroup)
    {
        // add the leaf group to SpectrumSourceGroup
        addSpectrumSourceGroup.binder() << ++groupId << group;
        addSpectrumSourceGroup.step();
        addSpectrumSourceGroup.reset();

        for(sqlite3_int64 sourceId : sources)
        {
            // update the source's group id
            updateSpectrumSource.binder() << groupId << sourceId;
            updateSpectrumSource.step();
            updateSpectrumSource.reset();

            // add the source/group pair as a link
            addSpectrumSourceGroupLink.binder() << ++linkId << sourceId << groupId;
            addSpectrumSourceGroupLink.step();
            addSpectrumSourceGroupLink.reset();
        }

        // for each leaf group, parse it into parent groups and add the leaf group's sources to the parent group's sources;
        // this creates the links to populate SpectrumSourceGroupLink
        bfs::path groupPath(group);

        // continue when the root group "/" is reached
        while (groupPath.has_parent_path())
        {
            groupPath = groupPath.parent_path();
            set<sqlite3_int64>& parentSources = sourcesByParentGroup[groupPath.string()];
            parentSources.insert(sources.begin(), sources.end());
        }
    }

    BOOST_FOREACH_FIELD((const string& group)(set<sqlite3_int64>& sources), sourcesByParentGroup)
    {
        // id 1 is reserved for '/'
        int parentGroupId = group == "/" ? 1 : ++groupId;

        // add the parent group to SpectrumSourceGroup
        addSpectrumSourceGroup.binder() << parentGroupId << group;
        addSpectrumSourceGroup.step();
        addSpectrumSourceGroup.reset();

        for(sqlite3_int64 sourceId : sources)
        {
            // add the source/group pair as a link
            addSpectrumSourceGroupLink.binder() << ++linkId << sourceId << parentGroupId;
            addSpectrumSourceGroupLink.step();
            addSpectrumSourceGroupLink.reset();
        }
    }

    transaction.commit();
}


struct UserFeedbackIterationListener : public IterationListener
{
    string currentMessage; // when the message changes, make a new line
    size_t longestLine;
    string logFilepath;
    MessageSeverity logLevel;
    bool stdOutRedirected;

    UserFeedbackIterationListener(const string& logFilepath, MessageSeverity logLevel) : longestLine(0), logFilepath(logFilepath), logLevel(logLevel), stdOutRedirected(IsStdOutRedirected())
    {
    }

    virtual Status update(const UpdateMessage& updateMessage)
    {
        // when the message changes, make a new line
        if (currentMessage.empty() || currentMessage != updateMessage.message)
        {
            if (!currentMessage.empty() && logFilepath.empty() && !stdOutRedirected)
            {
                if (logLevel == MessageSeverity::DebugInfo)
                    cout << "\n";
                else if (!updateMessage.message.empty())
                    cout << "\r";
            }
            currentMessage = updateMessage.message;
            longestLine = max(longestLine, currentMessage.size());
        }

        int index = updateMessage.iterationIndex;
        int count = updateMessage.iterationCount;

        // when logging to stdout, skip the log in order to use carriage returns to show iteration updates on a single line
        if (logFilepath.empty() && !stdOutRedirected)
        {
            cout << "\r";
            if (index == 0 && count <= 1 && logLevel <= MessageSeverity::BriefInfo)
                cout << updateMessage.message;
            else if (count > 1 && logLevel <= MessageSeverity::VerboseInfo)
            {
                if (index + 1 == count)
                    cout << updateMessage.message << ": " << (index + 1) << "/" << count;
                else
                    cout << updateMessage.message << ": " << (index + 1) << "/" << count;
            }
            else if (logLevel <= MessageSeverity::BriefInfo)
            {
                if (index == 0 && count <= 1)
                    cout << updateMessage.message;
                else if (index + 1 == count)
                    cout << updateMessage.message << ": " << (index + 1) << "/" << count;
            }

            if (logLevel <= MessageSeverity::BriefInfo)
            {
                for (size_t i = longestLine - updateMessage.message.length(); i > 0; --i)
                    cout << ' ';
                if (index + 1 == count)
                    cout << endl;
                else
                    cout << flush;
            }
        }
        else if (logLevel <= MessageSeverity::VerboseInfo)
        {
            if (index == 0 && count <= 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message;
            else if (count > 1)
            {
                if (index + 1 == count)
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message << ": " << (index + 1) << "/" << count << "\n";
                else
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message << ": " << (index + 1) << "/" << count;
            }
            else
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message << ": " << (index + 1);
        }
        else if (logLevel == MessageSeverity::BriefInfo)
        {
            if (index == 0 && count <= 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << updateMessage.message;
            else if (index + 1 == count)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << updateMessage.message << ": " << (index + 1) << "/" << count << "\n";
        }

        return Status_Ok;
    }
};


struct SourceAnalysisCountRow
{
    sqlite3_int64 filteredSpectra;
    int distinctMatches;
    int distinctPeptides;
    int proteins;
    int proteinGroups;
};


void summarizeAssembly(const string& filepath, bool summarizeSources)
{
    pair<int, int> result(0, 0);

    sqlite::database idpDb(filepath);

    string sql = "SELECT ss.Name, a.Name, COUNT(DISTINCT psm.Spectrum), COUNT(DISTINCT dm.DistinctMatchId), COUNT(DISTINCT psm.Peptide), COUNT(DISTINCT pi.Protein), COUNT(DISTINCT pro.ProteinGroup)"
                 "  FROM PeptideSpectrumMatch psm"
                 "  JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide"
                 "  JOIN Protein pro on pi.Protein=pro.Id"
                 "  JOIN DistinctMatch dm on psm.Id=dm.PsmId"
                 "  JOIN Spectrum s ON psm.Spectrum=s.Id"
                 "  JOIN SpectrumSource ss ON s.Source=ss.Id"
                 "  JOIN Analysis a ON psm.Analysis=a.Id"
                 " GROUP BY s.Source, psm.Analysis";

    sqlite::query summaryQuery(idpDb, sql.c_str());

    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "\n\nSpectra    Matches  Peptides  Proteins  Protein Groups  Analysis/Source\n"
                                                                << "-----------------------------------------------------------------------\n";
    if (summarizeSources)
        for(sqlite::query::rows row : summaryQuery)
        {
            string source, analysis;
            SourceAnalysisCountRow rowCounts;
            row.getter() >> source >> analysis
                         >> rowCounts.filteredSpectra
                         >> rowCounts.distinctMatches
                         >> rowCounts.distinctPeptides
                         >> rowCounts.proteins
                         >> rowCounts.proteinGroups;

            string sourceTitle;
            if (bfs::path(filepath).replace_extension("").filename() == source)
                sourceTitle = source;
            else
                sourceTitle = filepath + ":" + source;

            BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << std::left << setw(11) << rowCounts.filteredSpectra
                 << std::left << setw(9) << rowCounts.distinctMatches
                 << std::left << setw(10) << rowCounts.distinctPeptides
                 << std::left << setw(10) << rowCounts.proteins
                 << std::left << setw(16) << rowCounts.proteinGroups
                 << analysis << " / " << sourceTitle << endl;
        }

    TotalCounts totalCounts(idpDb.connected());
    if (summarizeSources)
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "-----------------------------------------------------------------------\n";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << std::left << setw(11) << totalCounts.filteredSpectra()
         << std::left << setw(9) << totalCounts.distinctMatches()
         << std::left << setw(10) << totalCounts.distinctPeptides()
         << std::left << setw(10) << totalCounts.proteins()
         << std::left << setw(16) << totalCounts.proteinGroups()
         << "Total" << endl;
}

int main(int argc, const char* argv[])
{
    cout << "IDPickerAssemble " << idpAssemble::Version::str() << " (" << idpAssemble::Version::LastModified() << ")\n" << endl;

    string usage = "IDPAssemble is a command-line tool for merging and filtering idpDB files. You can also assign a source group hierarchy.\n"
                   "\n"
                   "Usage: idpAssemble <idpDB filepath> [another idpDB filepath ...] [-MergedOutputFilepath <filepath to merge to>]\n"
                   "                   [-MaxFDRScore <real>]\n"
                   "                   [-MinDistinctPeptides <integer>]\n"
                   "                   [-MinSpectra <integer>]\n"
                   "                   [-MinAdditionalPeptides <integer>]\n"
                   "                   [-MinSpectraPerDistinctMatch <integer>]\n"
                   "                   [-MinSpectraPerDistinctPeptide <integer>]\n"
                   "                   [-MaxProteinGroupsPerPeptide <integer>]\n"
                   "                   [-PrecursorMzTolerance <real> <mz|ppm>]\n"
                   "                   [-FilterAtGeneLevel <boolean>]\n"
                   "                   [-MergedOutputFilepath <string>]\n"
                   "                   [-AssignSourceHierarchy <assemble.tsv>]\n"
                   "                   [-IsobaricSampleMapping <mapping.tsv>]\n"
                   "                   [-SummarizeSources <boolean>]\n"
                   "                   [-SkipPeptideMismatchCheck <boolean>]\n"
                   "                   [-DropFiltersOnly <boolean>]\n"
                   "                   [-LogLevel <Error|Warning|BriefInfo|VerboseInfo|DebugInfo>]\n"
                   "                   [-LogFilepath <filepath to log to>]\n"
                   "                   [-cpus <max thread count>]\n"
                   "                   [-b <file containing a long list of newline-separated idpDB filemasks>]\n"
                   "\n"
                   "Example: idpAssemble fraction1.idpDB fraction2.idpDB fraction3.idpDB -MergedOutputFilepath mudpit.idpDB\n"
                   "\n"
                   "The assemble.tsv file is a tab-delimited file with two columns that organizes the sources into a hierarchy.\n"
                   "The first column is the name of a source group, the second column is the source path or name to assign to that group.\n"
                   "A simple example:\n"
                   "/repA\trepA1.idpDB\n"
                   "/repA\trepA2.idpDB\n"
                   "/repB\trepB1.idpDB\n"
                   "/repB\trepB2.idpDB\n"
                   "\n"
                   "A multi-level example:\n"
                   "/A/1\tA1_f1\n"
                   "/A/1\tA1_f2\n"
                   "/A/2\tA2_f1\n"
                   "/A/2\tA2_f2\n"
                   "/B/1\tB1_f1\n"
                   "/B/1\tB1_f2\n"
                   "/B/2\tB2_f1\n"
                   "/B/2\tB2_f2\n"
                   "\n"
                   "The mapping.tsv file is a tab-delimited file with two columns. The first column is the source group,\n"
                   "the second column is a comma-delimited list of sample names, in ascending order of reporter ion mass. The special\n"
                   "sample name 'Reference', if present, will be used to normalize the other channels. Samples named 'Empty' will be\n"
                   "ignored. Here is an iTRAQ-4plex example:\n"
                   "/A123_B456_C789\tA123,B456,C789,Reference\n";

    string mergeTargetFilepath;
    string assembleTextFilepath;
    string isobaricSampleMappingFilepath;
    string logFilepath;
    bool skipPeptideMismatchCheck = false;
    vector<string> mergeSourceFilepaths;
    Filter::Config filterConfig;
    int maxThreads = 8;
    string batchFile;
    bool summarizeSources = false;
    bool dropFiltersOnly = false;
    MessageSeverity logLevel = MessageSeverity::BriefInfo;

    boost::log::formatter fmt = expr::stream << expr::attr<MessageSeverity::domain, severity_tag>("Severity") << expr::smessage;

    // Initialize sinks
    typedef sinks::synchronous_sink<sinks::text_ostream_backend> text_sink;
    boost::shared_ptr<text_sink> sink = boost::make_shared<text_sink>();

    // errors always go to console
    sink->locked_backend()->add_stream(boost::shared_ptr<std::ostream>(&cerr, boost::null_deleter()));
    sink->set_formatter(fmt);
    sink->set_filter(severity >= MessageSeverity::Error);
    boost::log::core::get()->add_sink(sink);
    sink->locked_backend()->auto_flush(true);

    vector<string> args(argv + 1, argv + argc);

    for (size_t i = 0; i < args.size(); ++i)
    {
        if (args[i][0] == '-' && !bfs::exists(args[i]) && i + 1 == args.size())
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << args[i] << " must be followed by a value." << endl;
            return 1;
        }

        if (args[i] == "-MergedOutputFilepath")
            mergeTargetFilepath = args[++i];
        else if (args[i] == "-AssignSourceHierarchy")
            assembleTextFilepath = args[++i];
        else if (args[i] == "-IsobaricSampleMapping")
            isobaricSampleMappingFilepath = args[++i];
        else if (args[i] == "-MaxFDRScore")
            filterConfig.maxFDRScore = lexical_cast<double>(args[++i]);
        else if (args[i] == "-MinDistinctPeptides")
            filterConfig.minDistinctPeptides = lexical_cast<int>(args[++i]);
        else if (args[i] == "-MinSpectra")
            filterConfig.minSpectra = lexical_cast<int>(args[++i]);
        else if (args[i] == "-MinAdditionalPeptides")
            filterConfig.minAdditionalPeptides = lexical_cast<int>(args[++i]);
        else if (args[i] == "-MinSpectraPerDistinctMatch")
            filterConfig.minSpectraPerDistinctMatch = lexical_cast<int>(args[++i]);
        else if (args[i] == "-MinSpectraPerDistinctPeptide")
            filterConfig.minSpectraPerDistinctPeptide = lexical_cast<int>(args[++i]);
        else if (args[i] == "-MaxProteinGroupsPerPeptide")
            filterConfig.maxProteinGroupsPerPeptide = lexical_cast<int>(args[++i]);
        else if (args[i] == "-PrecursorMzTolerance")
        {
            if (i + 2 >= args.size() || (!bal::iequals(args[i+2], "mz") && !bal::istarts_with(args[i+2], "da") && !bal::iequals(args[i+2], "ppm")))
            {
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "-PrecursorMzTolerance must be followed by a value and a unit (mz or ppm), e.g. \"10 ppm\"" << endl;
                return 1;
            }
            istringstream mzToleranceStream(args[i + 1] + args[i + 2]);
            filterConfig.precursorMzTolerance = pwiz::chemistry::MZTolerance();
            mzToleranceStream >> filterConfig.precursorMzTolerance.get();
            i += 2;
        }
        else if (args[i] == "-FilterAtGeneLevel")
            filterConfig.geneLevelFiltering = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-SummarizeSources")
            summarizeSources = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-SkipPeptideMismatchCheck")
            skipPeptideMismatchCheck = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-DropFiltersOnly")
            dropFiltersOnly = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-LogLevel")
            parse(logLevel, args[++i]);
        else if (args[i] == "-LogFilepath")
            logFilepath = args[++i];
        else if (args[i] == "-cpus")
            maxThreads = lexical_cast<int>(args[++i]);
        else if (args[i] == "-b")
            batchFile = args[++i];
        else
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(args[i], matchingFiles);
            if (matchingFiles.empty())
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "no matching files for filemask: " << args[i] << endl;
            else
                for(const bfs::path& p : matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    sink = boost::make_shared<text_sink>();
    if (logFilepath.empty())
    {
        sink->locked_backend()->add_stream(boost::shared_ptr<std::ostream>(&cout, boost::null_deleter())); // if no logfile is set, send messages to console
        sink->set_filter(severity >= logLevel.index() && severity < MessageSeverity::Error); // but don't send errors to the console twice
        sink->locked_backend()->auto_flush(true);
    }
    else
    {
        sink->locked_backend()->add_stream(boost::make_shared<std::ofstream>(logFilepath.c_str()));
        sink->set_filter(severity >= logLevel.index()); // NOTE: use index() to avoid delayed evaluation of expression
    }
    fmt = expr::stream << expr::attr<MessageSeverity::domain, severity_tag>("Severity") << expr::smessage;
    sink->set_formatter(fmt);
    boost::log::core::get()->add_sink(sink);

    // Add attributes
    boost::log::add_common_attributes();

    if (!batchFile.empty())
    {
        ifstream batchStream(batchFile.c_str());
        string line;
        while (batchStream >> line)
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(line, matchingFiles);
            if (matchingFiles.empty())
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "no matching files for filemask: " << line << endl;
            else
                for(const bfs::path& p : matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    if (mergeSourceFilepaths.empty())
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "not enough arguments. No idpDB files given as input.\n\n" << usage << endl;
        return 1;
    }

    if (!dropFiltersOnly && mergeSourceFilepaths.size() > 1 && mergeTargetFilepath.empty())
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "more than one idpDB file was given as input but no merge target filepath was given.\n\n" << usage << endl;
        return 1;
    }

    if (!assembleTextFilepath.empty() && !bfs::exists(assembleTextFilepath))
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "the assembly file specified by AssignSourceHierarchy does not exist.\n\n" << endl;
        return 1;
    }

    if (!isobaricSampleMappingFilepath.empty() && !bfs::exists(isobaricSampleMappingFilepath))
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "the isobaric sample mapping file specified by IsobaricSampleMapping does not exist.\n\n" << endl;
        return 1;
    }

    if (mergeSourceFilepaths.size() == 1)
        mergeTargetFilepath = mergeSourceFilepaths[0];

    IterationListenerRegistry ilr;
    ilr.addListener(IterationListenerPtr(new UserFeedbackIterationListener(logFilepath, logLevel)), 1);

    try
    {
        if (dropFiltersOnly)
        {
            for (const auto& sourceFilepath : mergeSourceFilepaths)
                Qonverter::dropFilters(sourceFilepath);
            return 0;
        }

        if (mergeSourceFilepaths.size() > 1)
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "Merging " << mergeSourceFilepaths.size() << " files to: " << mergeTargetFilepath << endl;
            bpt::ptime start = bpt::microsec_clock::local_time();
            Merger merger;
            merger.merge(mergeTargetFilepath, mergeSourceFilepaths, maxThreads, &ilr, skipPeptideMismatchCheck);
            //std::random_shuffle(mergeSourceFilepaths.begin(), mergeSourceFilepaths.end());
            //merger.merge(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), mergeSourceFilepaths, 1, &ilr);
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "Merging finished in " << bpt::to_simple_string(bpt::microsec_clock::local_time() - start);
        }
        else // make sure schema is up to date
            SchemaUpdater::update(mergeTargetFilepath);

        bpt::ptime start = bpt::microsec_clock::local_time();
        Filter filter;
        filter.config = filterConfig;
        filter.filter(mergeTargetFilepath, &ilr);
        //filter.filter(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), &ilr);
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "Filtering finished in " << bpt::to_simple_string(bpt::microsec_clock::local_time() - start);

        if (!assembleTextFilepath.empty())
            assignSourceGroupHierarchy(mergeTargetFilepath, assembleTextFilepath);

        if (!isobaricSampleMappingFilepath.empty())
            embedIsobaricSampleMapping(mergeTargetFilepath, isobaricSampleMappingFilepath);

        summarizeAssembly(mergeTargetFilepath, summarizeSources);
        //summarizeAssembly(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), summarizeSources);

        return 0;
    }
    catch (exception& e)
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "\nUnhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "\nUnknown exception." << endl;
    }
    return 1;
}
