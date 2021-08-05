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
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/common/diff_std.hpp"
#include "../freicore/freicore.h"
#include "boost/foreach_field.hpp"
#include "boost/thread/mutex.hpp"
#include "boost/range/algorithm/remove_if.hpp"
#include "boost/make_shared.hpp"
#include "boost/core/null_deleter.hpp"
#include "boost/range/algorithm_ext/insert.hpp"
#include "boost/assign.hpp"

#include "SchemaUpdater.hpp"
#include "Parser.hpp"
#include "Embedder.hpp"
#include "idpQonvert.hpp"
#include "Logger.hpp"
#include <boost/log/expressions.hpp>
#include <boost/log/utility/setup/common_attributes.hpp>
#include <boost/log/sinks/sync_frontend.hpp>
#include <boost/log/sinks/text_ostream_backend.hpp>
#include "sqlite3pp.h"
#include <iomanip>
#include "CoreVersion.hpp"
#include "idpQonvertVersion.hpp"


using namespace freicore;
using namespace IDPicker;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;
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
    if (manip.get() >= MessageSeverity::Warning)
        strm << '\n' << MessageSeverity::get_by_value(manip.get()).get().str() << ": ";
    return strm;
}

BEGIN_IDPICKER_NAMESPACE

RunTimeConfig* g_rtConfig;

int InitProcess( argList_t& args )
{
    cout << "IDPickerQonvert " << idpQonvert::Version::str() << " (" << idpQonvert::Version::LastModified() << ")\n" << endl;

    string usage = "Usage: " + lexical_cast<string>(bfs::path(args[0]).filename()) + " [optional arguments] <analyzed data filemask> [<another filemask> ...]\n"
                    "Optional arguments:\n"
                    "-b <batch filepath>           : specify a file which lists the filepaths to process line-by-line\n"
                    "-cfg <config filepath>        : specify a configuration file other than the default\n"
                    "-workdir <working directory>  : change working directory\n"
                    "-cpus <value>                 : force use of <value> worker threads\n"
                    "-ignoreConfigErrors           : ignore errors in configuration file or the command-line\n"
                    "-AnyParameterName <value>     : override the value of the given parameter to <value>\n"
                    "-dump                         : show runtime configuration settings before starting the run\n";

    bool ignoreConfigErrors = false;
    string logFilepath;
    g_numWorkers = GetNumProcessors();

    // First set the working directory, if provided
    for( size_t i=1; i < args.size(); ++i )
    {
        if( args[i] == "-workdir" && i+1 <= args.size() )
        {
            bfs::current_path(args[i + 1]);
            args.erase( args.begin() + i );
        }
        else if( args[i] == "-cpus" && i+1 <= args.size() )
        {
            g_numWorkers = atoi( args[i+1].c_str() );
            args.erase( args.begin() + i );
        }
        else if( args[i] == "-ignoreConfigErrors" )
        {
            ignoreConfigErrors = true;
        }
        else if (args[i] == "-LogFilepath")
        {
            logFilepath = args[i+1];
            continue;
        }
        else
            continue;

        args.erase( args.begin() + i );
        --i;
    }

    g_rtConfig = new RunTimeConfig(!ignoreConfigErrors);
    g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
    g_rtConfig->LogFilepath = logFilepath;

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

    // Add attributes
    boost::log::add_common_attributes();

    vector<string> extraFilepaths;

    for( size_t i=1; i < args.size(); ++i )
    {
        if( args[i] == "-cfg" && i+1 <= args.size() )
        {
            if( g_rtConfig->initializeFromFile( args[i+1] ) )
            {
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
                return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
            }
            args.erase( args.begin() + i );

        }
        else if( args[i] == "-b" && i+1 <= args.size() )
        {
            // read filepaths from file
            if( !bfs::exists(args[i+1]) )
            {
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "could not find list file at \"" << args[i + 1] << "\"." << endl;
                return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
            }

            ifstream listFile(args[i+1].c_str());
            string line;
            while (getlinePortable(listFile, line))
                extraFilepaths.push_back(line);

            args.erase( args.begin() + i );
        }
        else
            continue;

        args.erase( args.begin() + i );
        --i;
    }

    if( g_rtConfig->inputFilepaths.empty() && args.size() < 2 )
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "not enough arguments.\n\n" << usage << endl;
        return QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS;
    }

    if( !g_rtConfig->initialized() )
    {
        if( g_rtConfig->initializeFromFile() )
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "could not find the default configuration file (hard-coded defaults in use)." << endl;
        }
    }

    // Command line overrides happen after config file has been distributed but before PTM parsing
    RunTimeVariableMap vars = g_rtConfig->getVariables();
    for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
    {
        string varName;
        varName += "-" + itr->first;

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == varName && i+1 < args.size() )
            {
                //cout << varName << " " << itr->second << " " << args[i+1] << endl;
                itr->second = args[i+1];
                args.erase( args.begin() + i );
                args.erase( args.begin() + i );
                --i;
            }
        }
    }

    try
    {
        g_rtConfig->setVariables(vars);
    }
    catch (runtime_error& e)
    {
        string error = bal::replace_all_copy(string(e.what()), "Error! ", "");
        throw runtime_error(error);
    }

    sink = boost::make_shared<text_sink>();
    if (g_rtConfig->LogFilepath.empty())
    {
        sink->locked_backend()->add_stream(boost::shared_ptr<std::ostream>(&cout, boost::null_deleter())); // if no logfile is set, send messages to console
        sink->set_filter(severity < MessageSeverity::Error && severity >= g_rtConfig->LogLevel.index()); // but don't send errors to the console twice
    }
    else
    {
        sink->locked_backend()->add_stream(boost::make_shared<std::ofstream>(g_rtConfig->LogFilepath.c_str()));
        sink->set_filter(severity >= g_rtConfig->LogLevel.index()); // NOTE: use index() to avoid delayed evaluation of expression
    }
    fmt = expr::stream << expr::attr<MessageSeverity::domain, severity_tag>("Severity") << expr::smessage;
    sink->set_formatter(fmt);
    boost::log::core::get()->add_sink(sink);

    for( size_t i=1; i < args.size(); ++i )
    {
        if( args[i] == "-dump" )
        {
            g_rtConfig->dump();
            args.erase( args.begin() + i );
            --i;
        }
    }

    for( size_t i=1; i < args.size(); ++i )
    {
        if( args[i][0] == '-' )
        {
            if (!ignoreConfigErrors)
            {
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "unrecognized parameter \"" << args[i] << "\"" << endl;
                return 1;
            }

            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
            args.erase( args.begin() + i );
            --i;
        }
    }

    if (args.size() == 1)
    {
        if (g_pid == 0) BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "no data sources specified.\n\n" << usage << endl;
        return 1;
    }


    args.insert(args.end(), extraFilepaths.begin(), extraFilepaths.end());

    return 0;
}

struct ImportSettingsHandler : public Parser::ImportSettingsCallback
{
    virtual void operator() (const vector<Parser::ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const
    {
        typedef pair<string, string> AnalysisParameter;

        if (distinctAnalyses.size() > 1)
        {
            ostringstream error;
            error << "multiple distinct analyses detected in the input files. IdpQonvert settings apply to every analysis." << endl;

            for (size_t i=0; i < distinctAnalyses.size(); ++i)
            {
                const Parser::Analysis& analysis = *distinctAnalyses[i];
                error << "Analysis " << analysis.name << endl;
                error << "\tStartTime: " << analysis.startTime << endl;
                if (analysis.filepaths.size() > 1)
                {
                    error << "\tInputFilepaths:" << endl;
                    BOOST_FOREACH(const string& filepath, analysis.filepaths)
                        error << "\t\t" << filepath << endl;
                }
                else
                    error << "\tInputFilepath: " << analysis.filepaths[0] << endl;
            }
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << error.str() << endl;
        }

        // verify the protein database from the imported files matches ProteinDatabase
        BOOST_FOREACH(const Parser::ConstAnalysisPtr& analysisPtr, distinctAnalyses)
        {
            const Parser::Analysis& analysis = *analysisPtr;

            // replace backslashes with forward slashes (will work with POSIX or Windows parsers)
            bal::replace_all(analysis.importSettings.proteinDatabaseFilepath, "\\", "/");

            bfs::path proteinDatabaseFilepath = analysis.importSettings.proteinDatabaseFilepath;
            if (!g_rtConfig->ProteinDatabase.empty())
            {
                if (!bal::iequals(proteinDatabaseFilepath.replace_extension("").filename().string(),
                                  bfs::path(g_rtConfig->ProteinDatabase).replace_extension("").filename().string()))
                   BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "ProteinDatabase " << bfs::path(g_rtConfig->ProteinDatabase).filename()
                                                                             << " does not match " << proteinDatabaseFilepath.filename();
                analysis.importSettings.proteinDatabaseFilepath = g_rtConfig->ProteinDatabase;
            }
            else
            {
                analysis.importSettings.proteinDatabaseFilepath = (bfs::path(analysis.filepaths[0]).parent_path() / bfs::path(proteinDatabaseFilepath).filename()).string();
                if (!bfs::exists(analysis.importSettings.proteinDatabaseFilepath) && !proteinDatabaseFilepath.is_complete())
                    analysis.importSettings.proteinDatabaseFilepath = (bfs::path(analysis.filepaths[0]).parent_path() / proteinDatabaseFilepath).string();
            }

            analysis.importSettings.analysisName = analysis.name;
            analysis.importSettings.maxQValue = g_rtConfig->MaxImportFDR;
            analysis.importSettings.maxResultRank = g_rtConfig->MaxResultRank;
            analysis.importSettings.ignoreUnmappedPeptides = g_rtConfig->IgnoreUnmappedPeptides;
            analysis.importSettings.logQonversionDetails = g_rtConfig->WriteQonversionDetails;
            analysis.importSettings.qonverterSettings = g_rtConfig->getQonverterSettings();

            if (analysis.importSettings.qonverterSettings.decoyPrefix.empty())
            {
                map<string, string>::const_iterator findItr = analysis.parameters.find("DecoyPrefix");
                if (findItr != analysis.parameters.end())
                    analysis.importSettings.qonverterSettings.decoyPrefix = findItr->second;
                else
                    throw runtime_error("[ImportSettingsHandler] unable to automatically determine decoy prefix in analysis: " + analysis.name);
            }
        }
    }
};

struct UserFeedbackIterationListener : public IterationListener
{
    struct PersistentUpdateMessage
    {
        size_t iterationIndex;
        size_t iterationCount; // 0 == unknown
        string message;

        PersistentUpdateMessage() {}

        PersistentUpdateMessage(const UpdateMessage& updateMessage)
        {
            iterationIndex = updateMessage.iterationIndex;
            iterationCount = updateMessage.iterationCount;
            message = updateMessage.message;
        }
    };

    boost::mutex mutex;
    map<string, PersistentUpdateMessage> lastUpdateByFilepath;
    string currentMessage; // when the message changes, make a new line
    size_t longestLine;

    UserFeedbackIterationListener() : longestLine(0)
    {
    }

    // if logging to console, use carriage returns to update the current line
    // if logging to file, at verbose level create a new line for every update, at brief level only report the finished count (i.e. iterationIndex+1 == iterationCount)
    virtual Status update(const UpdateMessage& updateMessage)
    {
        // lack of '*' means the message only has 1 part, rather than <filepath>*<message>
        if (!bal::contains(updateMessage.message, "*"))
        {
            // when the message changes, make a new line
            if (currentMessage.empty() || currentMessage != updateMessage.message)
            {
                if (!currentMessage.empty() && g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
                    cout << endl;
                currentMessage = updateMessage.message;
            }

            int index = updateMessage.iterationIndex;
            int count = updateMessage.iterationCount;

            if (g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
            {
                cout << "\r";
                if (index == 0 && count <= 1)
                    cout << updateMessage.message;
                else if (count > 1)
                    cout << updateMessage.message << ": " << (index+1) << "/" << count;
                else
                    cout << updateMessage.message << ": " << (index+1);
                cout << flush;
            }
            else if (g_rtConfig->LogLevel == MessageSeverity::VerboseInfo)
            {
                if (index == 0 && count <= 1)
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message;
                else if (count > 1)
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message << ": " << (index + 1) << "/" << count;
                else
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << updateMessage.message << ": " << (index + 1);
            }
            else if (g_rtConfig->LogLevel == MessageSeverity::BriefInfo)
            {
                if (index == 0 && count <= 1)
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << updateMessage.message;
                else if (index+1 == count)
                    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << updateMessage.message << ": " << (index + 1) << "/" << count;
            }

            return Status_Ok;
        }

        vector<string> parts;
        bal::split(parts, updateMessage.message, bal::is_any_of("*"));

        // parts[0] must be filepath, parts[1] must be original update message
        if (parts.size() != 2)
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "invalid update message: " << updateMessage.message << endl;
            return Status_Cancel;
        }

        const string& originalMessage = parts[1];

        boost::mutex::scoped_lock lock(mutex);

        // when the message changes, clear lastUpdateByFilepath and make a new line
        if (currentMessage.empty() || currentMessage != originalMessage)
        {
            if (!currentMessage.empty() && g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
                cout << endl;
            lastUpdateByFilepath.clear();
            currentMessage = originalMessage;
        }

        lastUpdateByFilepath[parts[0]] = PersistentUpdateMessage(updateMessage);

        vector<string> updateStrings;

        BOOST_FOREACH_FIELD((const string& filepath)(PersistentUpdateMessage& updateMessage), lastUpdateByFilepath)
        {
            // create a message for each file like "source (message: index/count)"
            string source = Parser::sourceNameFromFilename(bfs::path(filepath).filename().string());
            int index = updateMessage.iterationIndex;
            int count = updateMessage.iterationCount;
            const string& message = originalMessage;

            if (message.empty())
                continue;

            if (index == 0 && count <= 1)
            {
                updateStrings.push_back((boost::format("%1% (%2%)")
                                         % source % message).str());
            }
            else if ((count > 1 && g_rtConfig->LogLevel == MessageSeverity::VerboseInfo) || (index + 1 == count && g_rtConfig->LogLevel == MessageSeverity::BriefInfo))
            {
                updateStrings.push_back((boost::format("%1% (%2%: %3%/%4%)")
                                         % source % message % (index+1) % count).str());
            }
            else
                updateStrings.push_back((boost::format("%1% (%2%: %3%)")
                                         % source % message % (index+1)).str());
        }

        if (updateStrings.empty())
            return Status_Ok;

        if (g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
        {
            string fullLine = bal::join(updateStrings, "; ");
            cout << "\r" << fullLine;

            if (fullLine.length() > longestLine)
                longestLine = fullLine.length();
            else
                for(size_t i=longestLine - fullLine.length(); i > 0; --i)
                    cout << ' ';
            cout << flush;
        }
        else if (g_rtConfig->LogLevel == MessageSeverity::VerboseInfo)
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << bal::join(updateStrings, "; ");
        }
        else if (g_rtConfig->LogLevel == MessageSeverity::BriefInfo)
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << bal::join(updateStrings, "; ");
        }

        return Status_Ok;
    }
};

struct UserFeedbackProgressMonitor : public IterationListener
{
    UserFeedbackProgressMonitor(const string& idpDbFilepath) : source(Parser::sourceNameFromFilename(bfs::path(idpDbFilepath).filename().string()))
    {
        pair<int, int> consoleBounds = get_console_bounds(); // get platform-specific console bounds, or default values if an error occurs
        tabulationWidth = (boost::format("%%|%1%t|")
                           % consoleBounds.first).str();
    }

    // if logging to console, use carriage returns to update the current line
    // if logging to file, at verbose level create a new line for every update, at brief level only report the finished count (i.e. iterationIndex+1 == iterationCount)
    virtual Status update(const UpdateMessage& updateMessage)
    {
        // create a message for each file like "source (message: index/count)"
        int index = updateMessage.iterationIndex;
        int count = updateMessage.iterationCount;
        string message = updateMessage.message;
        bool isError = bal::contains(message, "error:");

        if (message.empty())
            return Status_Ok;

        if (isError)
        {
            bal::replace_all(message, "error: ", "");

            if (index == 0 && count <= 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << (boost::format("%1% (%2%)")
                                                                            % source % message).str();
            else if (count > 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << (boost::format("%1% (%2%: %3%/%4%)")
                                                                            % source % message % (index + 1) % count).str();
            else
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << (boost::format("%1% (%2%: %3%)")
                                                                            % source % message % (index + 1)).str();

            if (g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
                cout << endl;
        }

        if (g_rtConfig->LogFilepath.empty() && !IsStdOutRedirected())
        {
            if (index == 0 && count <= 1 && !message.empty())
                cout << (boost::format("%1% (%2%)" + tabulationWidth)
                         % source % message).str();
            else if (count > 1)
                cout << (boost::format("%1% (%2%: %3%/%4%)" + tabulationWidth)
                         % source % message % (index+1) % count).str();
            else
                cout << (boost::format("%1% (%2%: %3%)" + tabulationWidth)
                         % source % message % (index+1)).str();

            if (isError)
                cout << endl;
            else
                cout << "\r" << flush;
        }
        else if (g_rtConfig->LogLevel == MessageSeverity::VerboseInfo)
        {
            if (index == 0 && count <= 1 && !message.empty())
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << (boost::format("%1% (%2%)")
                                                                                  % source % message).str();
            else if (count > 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << (boost::format("%1% (%2%: %3%/%4%)")
                                                                                  % source % message % (index+1) % count).str();
            else
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << (boost::format("%1% (%2%: %3%)")
                                                                                  % source % message % (index+1)).str();
        }
        else if (g_rtConfig->LogLevel == MessageSeverity::BriefInfo)
        {
            if (index == 0 && count <= 1)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << (boost::format("%1% (%2%)")
                                                                                % source % message).str();
            else if (index+1 == count)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << (boost::format("%1% (%2%: %3%/%4%)")
                                                                                % source % message % (index+1) % count).str();
        }

        return Status_Ok;
    }

    private:
    string source;
    string tabulationWidth;
};

END_IDPICKER_NAMESPACE


struct QonversionSummary
{
    string source;
    string analysis;
    int spectra;
    int peptides;
};

vector<QonversionSummary> summarizeQonversion(const string& filepath)
{
    vector<QonversionSummary> results;

    if (!bfs::exists(filepath))
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << left << setw(8) << 0
                                                      << left << setw(9) << 0
                                                      << "file does not exist" << " / " << filepath << endl;
        return results;
    }

    sqlite::database idpDb(filepath);

    string sql = "SELECT ss.Name, a.Name, COUNT(DISTINCT Spectrum), COUNT(DISTINCT Peptide)"
                 "  FROM PeptideSpectrumMatch"
                 "  JOIN Spectrum s ON Spectrum=s.Id"
                 "  JOIN SpectrumSource ss ON Source=ss.Id"
                 "  JOIN Analysis a ON Analysis=a.Id"
                 " WHERE QValue < " + lexical_cast<string>(g_rtConfig->MaxFDR) +
                 " GROUP BY Source, Analysis";

    sqlite::query summaryQuery(idpDb, sql.c_str());

    BOOST_FOREACH(sqlite::query::rows row, summaryQuery)
    {
        results.push_back(QonversionSummary());
        QonversionSummary& result = results.back();

        boost::tie(result.source, result.analysis, result.spectra, result.peptides) = row.get_columns<string, string, int, int>(0, 1, 2, 3);

        if (bfs::path(filepath).replace_extension("").filename() != result.source)
            result.source = filepath + ":" + result.source;
    }

    return results;
}

// return the first existing filepath with one of the given extensions in the search path
string findNameInPath(const string& filenameWithoutExtension,
                      const vector<string>& extensions,
                      const vector<string>& searchPath)
{
    BOOST_FOREACH(const string& extension, extensions)
    BOOST_FOREACH(const string& path, searchPath)
    {
        bfs::path filepath(path);
        filepath /= filenameWithoutExtension + extension;

        // if the path exists, check whether MSData can handle it
        if (bfs::exists(filepath))
            return filepath.string();
    }
    return "";
}

bool outputFilepathExists(const string& filepath) {return bfs::exists(Parser::outputFilepath(filepath));}


int run( int argc, char* argv[] )
{
    try
    {
        argList_t args( argv, argv+argc );

        int rv;
        if( ( rv = InitProcess(args) ) > 0 )
            return rv;

        for( size_t i = 1; i < args.size(); ++i )
            FindFilesByMask( args[i], g_rtConfig->inputFilepaths );

        if( g_rtConfig->inputFilepaths.empty() )
        {
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "no files found matching input filemasks." << endl;
            return QONVERT_ERROR_NO_INPUT_FILES_FOUND;
        }

        vector<string> idpDbFilepaths, parserFilepaths;
        BOOST_FOREACH(const string& filepath, g_rtConfig->inputFilepaths)
            if (bal::iequals(bfs::path(filepath).extension().string(), ".idpDB"))
                idpDbFilepaths.push_back(filepath);
            else
                parserFilepaths.push_back(filepath);

        // skip or erase existing files according to OverwriteExistingFiles
        BOOST_FOREACH(const string& filepath, parserFilepaths)
        {
            bfs::path outputFilepath = Parser::outputFilepath(filepath);
            if (!bfs::exists(outputFilepath))
                continue;

            if (g_rtConfig->OverwriteExistingFiles)
                bfs::remove(outputFilepath);
            else
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "skipping existing file \"" << outputFilepath << "\"" << endl;
        }

        if (!g_rtConfig->OverwriteExistingFiles) // don't parse paths that already exist
            parserFilepaths.erase(boost::remove_if(parserFilepaths, &outputFilepathExists), parserFilepaths.end());

        Parser parser;

        // update at least once a second
        IterationListenerRegistry ilr;
        ilr.addListenerWithTimer(IterationListenerPtr(new UserFeedbackIterationListener), 1.0);

        parser.importSettingsCallback = Parser::ImportSettingsCallbackPtr(new ImportSettingsHandler);
        parser.skipSourceOnError = g_rtConfig->SkipSourceOnError;
        parser.parse(parserFilepaths, g_numWorkers, &ilr);
        
        if (!g_rtConfig->EmbedOnly)
        {
            vector<QonversionSummary> summaries;
            BOOST_FOREACH(const string& filepath, parserFilepaths)
            {
                boost::range::insert(summaries, summaries.end(), summarizeQonversion(Parser::outputFilepath(filepath).string()));
            }

            if (!parserFilepaths.empty())
                cout << endl;

            BOOST_FOREACH(const string& filepath, idpDbFilepaths)
            {
                Qonverter qonverter;
                qonverter.logQonversionDetails = g_rtConfig->WriteQonversionDetails; 
                qonverter.settingsByAnalysis[0] = g_rtConfig->getQonverterSettings();
                qonverter.skipSourceOnError = g_rtConfig->SkipSourceOnError;

                IterationListenerRegistry ilr;
                ilr.addListener(IterationListenerPtr(new UserFeedbackProgressMonitor(filepath)), 1);

                qonverter.reset(filepath, &ilr);
                qonverter.qonvert(filepath, &ilr);

                boost::range::insert(summaries, summaries.end(), summarizeQonversion(filepath));
            }

            // output summary statistics for each input file
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "\nSpectra Peptides Analysis/Source\n"
                                                                           "--------------------------------";

            MessageSeverity::domain perSummarySeverity = summaries.size() > 1 ? MessageSeverity::VerboseInfo : MessageSeverity::BriefInfo;

            int totalSpectra = 0, totalPeptides = 0;
            BOOST_FOREACH(const QonversionSummary& summary, summaries)
            {
                totalSpectra += summary.spectra;
                totalPeptides += summary.peptides;

                BOOST_LOG_SEV(logSource::get(), perSummarySeverity)
                    << left << setw(8) << summary.spectra
                    << left << setw(9) << summary.peptides
                    << summary.analysis << " / " << summary.source;
            }

            if (summaries.size() > 1 && g_rtConfig->LogLevel == MessageSeverity::VerboseInfo)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "--------------------------------";

            if (summaries.size() > 1 || g_rtConfig->LogLevel == MessageSeverity::VerboseInfo)
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << left << setw(8) << totalSpectra
                                                                            << left << setw(9) << totalPeptides
                                                                            << "Total" << endl;
        }
        else // make sure schema is up to date for EmbedOnly jobs (e.g. gene metadata and/or quantitation)
        {
            for (size_t i = 0, end = idpDbFilepaths.size(); i < end; ++i)
            {
                IterationListenerRegistry ilr;
                ilr.addListener(IterationListenerPtr(new UserFeedbackProgressMonitor(idpDbFilepaths[i])), 1);
                SchemaUpdater::update(idpDbFilepaths[i], &ilr);
            }
        }

        BOOST_FOREACH(const string& filepath, parserFilepaths)
            idpDbFilepaths.push_back(Parser::outputFilepath(filepath).string());

        if (g_rtConfig->EmbedSpectrumSources || g_rtConfig->EmbedSpectrumScanTimes)
        {
            map<int, Embedder::QuantitationConfiguration> allSourcesQuantitationMethodMap;
            allSourcesQuantitationMethodMap[0] = Embedder::QuantitationConfiguration(g_rtConfig->QuantitationMethod, g_rtConfig->ReporterIonMzTolerance, g_rtConfig->NormalizeReporterIons);

            map<int, XIC::XICConfiguration> allSourcesXICConfigurationMap;
            allSourcesXICConfigurationMap[0] = XIC::XICConfiguration(false, "", g_rtConfig->MaxFDR,
                                                                     g_rtConfig->LabelFreeMonoisotopeAdjustmentSet,
                                                                     g_rtConfig->LabelFreeLowerScanTimeLimit, g_rtConfig->LabelFreeUpperScanTimeLimit,
                                                                     g_rtConfig->LabelFreeLowerMzLimit, g_rtConfig->LabelFreeUpperMzLimit);

            for (size_t i=0, end=idpDbFilepaths.size(); i < end; ++i)
            {
                bfs::path idpDbFilepath = idpDbFilepaths[i];
                string sourceName = Parser::sourceNameFromFilename(idpDbFilepath.filename().string());
                PWIZ_LOG_ITER(logSource::get(), i, end) << sourceName << " (embedding subset spectra" << (g_rtConfig->EmbedSpectrumSources ? "" : " scan times")
                                                        << (g_rtConfig->QuantitationMethod == QuantitationMethod::None ? ": " : " and quantitation data: ")
                                                        << (i + 1) << "/" << end << ")";

                string sourceSearchPath = g_rtConfig->SourceSearchPath;
                if (idpDbFilepath.has_parent_path())
                    sourceSearchPath += ";" + idpDbFilepath.parent_path().string();

                IterationListenerRegistry ilr;
                ilr.addListener(IterationListenerPtr(new UserFeedbackProgressMonitor(idpDbFilepaths[i])), 1);

                if (g_rtConfig->QuantitationMethod == QuantitationMethod::LabelFree)
                    Embedder::EmbedMS1Metrics(idpDbFilepaths[i], sourceSearchPath, g_rtConfig->SourceExtensionPriorityList, allSourcesQuantitationMethodMap, allSourcesXICConfigurationMap, &ilr);

                if (g_rtConfig->EmbedSpectrumSources)
                    Embedder::embed(idpDbFilepaths[i], sourceSearchPath, g_rtConfig->SourceExtensionPriorityList, allSourcesQuantitationMethodMap, &ilr);
                else
                    Embedder::embedScanTime(idpDbFilepaths[i], sourceSearchPath, g_rtConfig->SourceExtensionPriorityList, allSourcesQuantitationMethodMap, &ilr);
            }
        }

        bfs::path currentPath = bfs::current_path();

        // switch to exe directory to allow embedGeneMetadata to find the gene mappings
        bfs::path exePath(args[0]);
        if (!exePath.has_parent_path())
        {
            using namespace boost::assign;
            vector<string> tokens;
            const char* pathSeparator = bfs::path::preferred_separator == '/' ? ":" : ";";
            string pathValue = pwiz::util::env::get("PATH");
            vector<string> exeExtensions; exeExtensions += "", ".exe", ".com", ".bat", ".cmd";
            bal::split(tokens, pathValue, bal::is_any_of(pathSeparator));
            exePath = findNameInPath(bfs::change_extension(exePath, "").string(), exeExtensions, tokens);
        }

        bfs::current_path(exePath.parent_path());

        if (g_rtConfig->EmbedGeneMetadata)
            for (size_t i=0 ; i < idpDbFilepaths.size(); ++i)
            {
                IterationListenerRegistry ilr;
                IterationListenerPtr il(new UserFeedbackProgressMonitor(idpDbFilepaths[i]));
                ilr.addListener(il, 1);

                il->update(IterationListener::UpdateMessage(i, idpDbFilepaths.size(), "embedding gene metadata"));
                Embedder::embedGeneMetadata(bfs::absolute(idpDbFilepaths[i], currentPath).string(), &ilr);
            }

    }
    catch (exception& e)
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << e.what() << endl;
        if (bal::contains(e.what(), "no filepath could be found"))
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "find the file and add its parent directory to SourceSearchPath (a semicolon delimited list)." << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }
    catch (...)
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "unhandled exception." << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }

    return 0;
}

int main(int argc, char* argv[])
{
    int result = run(argc, argv);
    boost::log::core::get()->flush();
    boost::log::core::get()->remove_all_sinks();
    return result;
}