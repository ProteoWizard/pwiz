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

#include "SchemaUpdater.hpp"
#include "TotalCounts.hpp"
#include "Parser.hpp"
#include "Merger.hpp"
#include "Filter.hpp"
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


void assignSourceGroupHierarchy(const string& idpDbFilepath, const string& assemblyFilepath)
{
    sqlite3pp::database idpDb(idpDbFilepath);

    map<string, sqlite3_int64> sourceIdByName;
    map<string, set<sqlite3_int64> > sourcesByGroup;
    set<sqlite3_int64> ungroupedSources;
    vector<string> sourceGroups;

    sqlite3pp::query sourceIdByNameQuery(idpDb, "SELECT Id, Name FROM SpectrumSource");
    BOOST_FOREACH(sqlite3pp::query::rows queryRow, sourceIdByNameQuery)
    {
        sourceIdByName[queryRow.get<string>(1)] = queryRow.get<sqlite3_int64>(0);
        ungroupedSources.insert(queryRow.get<sqlite3_int64>(0));
    }

    // open the assembly.txt file
    ifstream assembleTxtFile(assemblyFilepath.c_str());

    boost::regex groupFilemaskRegex("((\"(.+)\")|(\\S+))\\s+((\"(.+)\")|(\\S+))");
    boost::smatch match;

    string line;
    while (getline(assembleTxtFile, line))
    {
        if (line.empty())
            continue;

        try
        {
            boost::regex_match(line, match, groupFilemaskRegex);
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
                BOOST_FOREACH(bfs::path& filepath, matchingPaths)
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
    BOOST_FOREACH(sqlite3_int64 sourceId, ungroupedSources)
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

        BOOST_FOREACH(sqlite3_int64 sourceId, sources)
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

        BOOST_FOREACH(sqlite3_int64 sourceId, sources)
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

    virtual Status update(const UpdateMessage& updateMessage)
    {
        // when the message changes, make a new line
        if (currentMessage.empty() || currentMessage != updateMessage.message)
        {
            if (!currentMessage.empty())
                cout << endl;
            currentMessage = updateMessage.message;
        }

        bool isError = bal::contains(currentMessage, "error:");

        if (isError)
        {
            bal::replace_all(currentMessage, "error: ", "");
            cout << endl;
        }

        int index = updateMessage.iterationIndex;
        int count = updateMessage.iterationCount;

        string message = currentMessage.empty() ? "" : string(1, std::toupper(currentMessage[0])) + currentMessage.substr(1);

        cout << "\r";
        if (index == 0 && count == 0)
            cout << message;
        else if (count > 0)
            cout << message << ": " << (index + 1) << "/" << count;
        else
            cout << message << ": " << (index + 1);

        if (isError)
            cout << endl;
        else
            cout << flush;

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

    cout << "\n\nSpectra    Matches  Peptides  Proteins  Protein Groups  Analysis/Source\n"
            "-----------------------------------------------------------------------\n";
    if (summarizeSources)
        BOOST_FOREACH(sqlite::query::rows row, summaryQuery)
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

            cout << std::left << setw(11) << rowCounts.filteredSpectra
                 << std::left << setw(9) << rowCounts.distinctMatches
                 << std::left << setw(10) << rowCounts.distinctPeptides
                 << std::left << setw(10) << rowCounts.proteins
                 << std::left << setw(16) << rowCounts.proteinGroups
                 << analysis << " / " << sourceTitle << endl;
        }

    TotalCounts totalCounts(idpDb.connected());
    if (summarizeSources)
        cout << "-----------------------------------------------------------------------\n";
    cout << std::left << setw(11) << totalCounts.filteredSpectra()
         << std::left << setw(9) << totalCounts.distinctMatches()
         << std::left << setw(10) << totalCounts.distinctPeptides()
         << std::left << setw(10) << totalCounts.proteins()
         << std::left << setw(16) << totalCounts.proteinGroups()
         << "Total" << endl;
}

int main(int argc, const char* argv[])
{
    cout << "IDPickerAssemble " << idpAssemble::Version::str() << " (" << idpAssemble::Version::LastModified() << ")\n" <<
            "IDPickerCore " << IDPicker::Version::str() << " (" << IDPicker::Version::LastModified() << ")\n"  << endl;

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
                   "                   [-FilterAtGeneLevel <boolean>]\n"
                   "                   [-MergedOutputFilepath <string>]\n"
                   "                   [-AssignSourceHierarchy <assemble.tsv>]\n"
                   "                   [-SummarizeSources <boolean>]\n"
                   "                   [-cpus <max thread count>]\n"
                   "                   [-b <file containing a long list of newline-separated idpDB filemasks>]\n"
                   "\n"
                   "Example: idpAssemble fraction1.idpDB fraction2.idpDB fraction3.idpDB -MergedOutputFilepath mudpit.idpDB\n"
                   "\n"
                   "The assemble.tsv file is a tab-delimited file with two columns. The first column is the source group path,\n"
                   "the second column is a source group name to assign to that group.";

    string mergeTargetFilepath;
    string assembleTextFilepath;
    vector<string> mergeSourceFilepaths;
    Filter::Config filterConfig;
    int maxThreads = 8;
    string batchFile;
    bool summarizeSources = false;

    vector<string> args(argv + 1, argv + argc);

    for (size_t i = 0; i < args.size(); ++i)
    {
        if (args[i][0] == '-' && !bfs::exists(args[i]) && i + 1 == args.size())
        {
            cerr << args[i] << " must be followed by a value." << endl;
            return 1;
        }

        if (args[i] == "-MergedOutputFilepath")
            mergeTargetFilepath = args[++i];
        else if (args[i] == "-AssignSourceHierarchy")
            assembleTextFilepath = args[++i];
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
        else if (args[i] == "-FilterAtGeneLevel")
            filterConfig.geneLevelFiltering = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-SummarizeSources")
            summarizeSources = lexical_cast<bool>(args[++i]);
        else if (args[i] == "-cpus")
            maxThreads = lexical_cast<int>(args[++i]);
        else if (args[i] == "-b")
            batchFile = args[++i];
        else
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(args[i], matchingFiles);
            if (matchingFiles.empty())
                cerr << "Warning: no matching files for filemask: " << args[i] << endl;
            else
                BOOST_FOREACH(const bfs::path& p, matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    if (!batchFile.empty())
    {
        ifstream batchStream(batchFile.c_str());
        string line;
        while (batchStream >> line)
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(line, matchingFiles);
            if (matchingFiles.empty())
                cerr << "Warning: no matching files for filemask: " << line << endl;
            else
                BOOST_FOREACH(const bfs::path& p, matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    if (mergeSourceFilepaths.empty())
    {
        cerr << "Error: not enough arguments. No idpDB files given as input.\n" << endl;
        cerr << usage << endl;
        return 1;
    }

    if (mergeSourceFilepaths.size() > 1 && mergeTargetFilepath.empty())
    {
        cerr << "Error: more than one idpDB file was given as input but no merge target filepath was given.\n" << endl;
        cerr << usage << endl;
        return 1;
    }

    if (!assembleTextFilepath.empty() && !bfs::exists(assembleTextFilepath))
    {
        cerr << "Error: the assembly file specified by AssignSourceHierarchy does not exist." << endl;
        return 1;
    }

    if (mergeSourceFilepaths.size() == 1)
        mergeTargetFilepath = mergeSourceFilepaths[0];

    IterationListenerRegistry ilr;
    ilr.addListener(IterationListenerPtr(new UserFeedbackIterationListener), 1);

    try
    {
        if (mergeSourceFilepaths.size() > 1)
        {
            cout << "Merging " << mergeSourceFilepaths.size() << " files to: " << mergeTargetFilepath << endl;
            bpt::ptime start = bpt::microsec_clock::local_time();
            Merger merger;
            merger.merge(mergeTargetFilepath, mergeSourceFilepaths, maxThreads, &ilr);
            //std::random_shuffle(mergeSourceFilepaths.begin(), mergeSourceFilepaths.end());
            //merger.merge(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), mergeSourceFilepaths, 1, &ilr);
            cout << "\nMerging finished in " << bpt::to_simple_string(bpt::microsec_clock::local_time() - start);
        }

        bpt::ptime start = bpt::microsec_clock::local_time();
        Filter filter;
        filter.config = filterConfig;
        filter.filter(mergeTargetFilepath, &ilr);
        //filter.filter(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), &ilr);
        cout << "\nFiltering finished in " << bpt::to_simple_string(bpt::microsec_clock::local_time() - start);

        if (!assembleTextFilepath.empty())
            assignSourceGroupHierarchy(mergeTargetFilepath, assembleTextFilepath);

        summarizeAssembly(mergeTargetFilepath, summarizeSources);
        //summarizeAssembly(bfs::path(mergeTargetFilepath).replace_extension(".reverse.idpDB").string(), summarizeSources);

        return 0;
    }
    catch (exception& e)
    {
        cerr << "\nUnhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "\nUnknown exception." << endl;
    }
    return 1;
}
