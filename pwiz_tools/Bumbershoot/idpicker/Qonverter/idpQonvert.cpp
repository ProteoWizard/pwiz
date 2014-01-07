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

#include "Parser.hpp"
#include "Embedder.hpp"
#include "idpQonvert.hpp"
#include "sqlite3pp.h"
#include <iomanip>
//#include "svnrev.hpp"

using namespace freicore;
using namespace IDPicker;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;


BEGIN_IDPICKER_NAMESPACE

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

RunTimeConfig* g_rtConfig;

int InitProcess( argList_t& args )
{
    cout << "IDPickerQonvert " << Version::str() << " (" << Version::LastModified() << ")\n" <<
            "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
            "" << endl;

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
	g_numWorkers = GetNumProcessors();

	// First set the working directory, if provided
	for( size_t i=1; i < args.size(); ++i )
	{
		if( args[i] == "-workdir" && i+1 <= args.size() )
		{
			chdir( args[i+1].c_str() );
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
        else
			continue;

		args.erase( args.begin() + i );
		--i;
	}

	g_rtConfig = new RunTimeConfig(!ignoreConfigErrors);
	g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;

    vector<string> extraFilepaths;

	for( size_t i=1; i < args.size(); ++i )
	{
		if( args[i] == "-cfg" && i+1 <= args.size() )
		{
			if( g_rtConfig->initializeFromFile( args[i+1] ) )
			{
				cerr << "Could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
				return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
			}
			args.erase( args.begin() + i );

		}
        else if( args[i] == "-b" && i+1 <= args.size() )
        {
            // read filepaths from file
            if( !bfs::exists(args[i+1]) )
            {
                cerr << "Could not find list file at \"" << args[i+1] << "\"." << endl;
                return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
            }

            ifstream listFile(args[i+1].c_str());
            string line;
            while (getline(listFile, line))
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
		cerr << "Not enough arguments.\n\n" << usage << endl;
		return QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS;
	}

	if( !g_rtConfig->initialized() )
	{
		if( g_rtConfig->initializeFromFile() )
		{
			cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
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

	g_rtConfig->setVariables( vars );

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
                cerr << "Error: unrecognized parameter \"" << args[i] << "\"" << endl;
                return 1;
            }

			cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
			args.erase( args.begin() + i );
			--i;
		}
	}

    if (args.size() == 1)
    {
        if( g_pid == 0 ) cerr << "No data sources specified.\n\n" << usage << endl;
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
            error << "\nWarning: multiple distinct analyses detected in the input files. IdpQonvert settings apply to every analysis." << endl;

            for (size_t i=0; i < distinctAnalyses.size(); ++i)
            {
                const Parser::Analysis& analysis = *distinctAnalyses[i];
                error << "Analysis " << analysis.name << endl;
                error << "\tstartTime: " << analysis.startTime << endl;
                if (analysis.filepaths.size() > 1)
                {
                    error << "\tinputFilepaths:" << endl;
                    BOOST_FOREACH(const string& filepath, analysis.filepaths)
                        error << "\t\t" << filepath << endl;
                }
                else
                    error << "\tinputFilepath: " << analysis.filepaths[0] << endl;
            }
            cerr << error.str() << endl;
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
                   cerr << "\nWarning: ProteinDatabase " << bfs::path(g_rtConfig->ProteinDatabase).filename()
                        << " does not match " << proteinDatabaseFilepath.filename();
                analysis.importSettings.proteinDatabaseFilepath = g_rtConfig->ProteinDatabase;
            }
            else if (!proteinDatabaseFilepath.is_complete())
                analysis.importSettings.proteinDatabaseFilepath = (bfs::path(analysis.filepaths[0]).parent_path() / proteinDatabaseFilepath).string();

            analysis.importSettings.analysisName = analysis.name;
            analysis.importSettings.maxQValue = g_rtConfig->MaxImportFDR;
            analysis.importSettings.maxResultRank = g_rtConfig->MaxResultRank;
            analysis.importSettings.qonverterSettings = g_rtConfig->getQonverterSettings();
            analysis.importSettings.ignoreUnmappedPeptides = g_rtConfig->IgnoreUnmappedPeptides;
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

    virtual Status update(const UpdateMessage& updateMessage)
    {
        if (!bal::contains(updateMessage.message, "*"))
        {
            // when the message changes, make a new line
            if (currentMessage.empty() || currentMessage != updateMessage.message)
            {
                if (!currentMessage.empty())
                    cout << endl;
                currentMessage = updateMessage.message;
            }

            int index = updateMessage.iterationIndex;
            int count = updateMessage.iterationCount;

            cout << "\r";
            if (index == 0 && count == 0)
                cout << updateMessage.message;
            else if (count > 0)
                cout << updateMessage.message << ": " << (index+1) << "/" << count;
            else
                cout << updateMessage.message << ": " << (index+1);
            cout << flush;

            return Status_Ok;
        }

        vector<string> parts;
        bal::split(parts, updateMessage.message, bal::is_any_of("*"));

        // parts[0] must be filepath, parts[1] must be original update message
        if (parts.size() != 2)
        {
            cerr << "Invalid update message: " << updateMessage.message << endl;
            return Status_Cancel;
        }

        const string& originalMessage = parts[1];

        boost::mutex::scoped_lock lock(mutex);

        // when the message changes, clear lastUpdateByFilepath and make a new line
        if (currentMessage.empty() || currentMessage != originalMessage)
        {
            if (!currentMessage.empty())
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

            if (index == 0 && count == 0)
            {
                updateStrings.push_back((boost::format("%1% (%2%)")
                                         % source % message).str());
            }
            else if (count > 0)
            {
                updateStrings.push_back((boost::format("%1% (%2%: %3%/%4%)")
                                         % source % message % (index+1) % count).str());
            }
            else
                updateStrings.push_back((boost::format("%1% (%2%: %3%)")
                                         % source % message % (index+1)).str());
        }

        cout << "\r" << bal::join(updateStrings, "; ") << flush;

        return Status_Ok;
    }
};

struct UserFeedbackProgressMonitor : public Qonverter::ProgressMonitor
{
    UserFeedbackProgressMonitor(const string& idpDbFilepath) : source(Parser::sourceNameFromFilename(bfs::path(idpDbFilepath).filename().string()))
    {
    }

    virtual void operator() (UpdateMessage& updateMessage) const
    {
        // create a message for each file like "source (message: index/count)"
        int index = updateMessage.qonvertedAnalyses;
        int count = updateMessage.totalAnalyses;
        const string& message = updateMessage.message;

        if (index == 0 && count == 0)
            cout << (boost::format("%1% (%2%)%|80t|")
                     % source % message).str();
        else if (count > 0)
            cout << (boost::format("%1% (%3%/%4%: %2%)%|80t|")
                     % source % message % (index+1) % count).str();
        else
            cout << (boost::format("%1% (%3%: %2%)%|80t|")
                     % source % message % (index+1)).str();

        cout << "\r" << flush;
    }

    private:
    string source;
};

END_IDPICKER_NAMESPACE


pair<int, int> summarizeQonversion(const string& filepath)
{
    pair<int, int> result(0, 0);

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
        string source, analysis;
        int spectra, peptides;
        boost::tie(source, analysis, spectra, peptides) = row.get_columns<string, string, int, int>(0, 1, 2, 3);

        string sourceTitle;
        if (bfs::path(filepath).replace_extension("").filename() == source)
            sourceTitle = source;
        else
            sourceTitle = filepath + ":" + source;

        cout << left << setw(8) << spectra
             << left << setw(9) << peptides
             << analysis << " / " << sourceTitle << endl;
        result.first += spectra;
        result.second += peptides;
    }

    return result;
}


bool outputFilepathExists(const string& filepath) {return bfs::exists(Parser::outputFilepath(filepath));}


int main( int argc, char* argv[] )
{
    try
    {
	    argList_t args( argv, argv+argc );

        bfs::path exePath(args[0]);
        chdir(exePath.parent_path().string().c_str());

	    int rv;
        if( ( rv = InitProcess(args) ) > 0 )
		    return rv;

	    for( size_t i = 1; i < args.size(); ++i )
		    FindFilesByMask( args[i], g_rtConfig->inputFilepaths );

	    if( g_rtConfig->inputFilepaths.empty() )
	    {
		    cout << "Error: no files found matching input filemasks." << endl;
		    return QONVERT_ERROR_NO_INPUT_FILES_FOUND;
	    }

        vector<string> idpDbFilepaths, parserFilepaths;
        BOOST_FOREACH(const string& filepath, g_rtConfig->inputFilepaths)
            if (bfs::path(filepath).extension() == ".idpDB")
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
                cerr << "Skipping existing file \"" << outputFilepath << "\"" << endl;
        }

        if (!g_rtConfig->OverwriteExistingFiles) // don't parse paths that already exist
            parserFilepaths.erase(boost::remove_if(parserFilepaths, &outputFilepathExists), parserFilepaths.end());

        Parser parser;

        // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
        const size_t iterationPeriod = 100;
        IterationListenerRegistry ilr;
        ilr.addListenerWithTimer(IterationListenerPtr(new UserFeedbackIterationListener), 1.0);

        parser.importSettingsCallback = Parser::ImportSettingsCallbackPtr(new ImportSettingsHandler);
        parser.parse(parserFilepaths, g_numWorkers, &ilr);
        
        if (!g_rtConfig->EmbedOnly)
        {
            // output summary statistics for each input file
            cout << "\nSpectra Peptides Analysis/Source\n"
                    "--------------------------------\n";

            int totalSpectra = 0, totalPeptides = 0;
            BOOST_FOREACH(const string& filepath, parserFilepaths)
            {
                pair<int, int> spectraPeptidesPair = summarizeQonversion(Parser::outputFilepath(filepath).string());
                totalSpectra += spectraPeptidesPair.first;
                totalPeptides += spectraPeptidesPair.second;
            }

            BOOST_FOREACH(const string& filepath, idpDbFilepaths)
            {
                Qonverter qonverter;
                qonverter.logQonversionDetails = g_rtConfig->WriteQonversionDetails; 
                qonverter.settingsByAnalysis[0] = g_rtConfig->getQonverterSettings();

                UserFeedbackProgressMonitor progressMonitor(filepath);
                qonverter.reset(filepath);
                qonverter.qonvert(filepath, progressMonitor);

                pair<int, int> spectraPeptidesPair = summarizeQonversion(filepath);

                totalSpectra += spectraPeptidesPair.first;
                totalPeptides += spectraPeptidesPair.second;
            }

            if (parserFilepaths.size() + idpDbFilepaths.size() > 1)
                cout << "--------------------------------\n"
                     << left << setw(8) << totalSpectra
                     << left << setw(9) << totalPeptides
                     << "Total\n" << endl;
        }
        
        BOOST_FOREACH(const string& filepath, parserFilepaths)
            idpDbFilepaths.push_back(Parser::outputFilepath(filepath).string());

        if (g_rtConfig->EmbedSpectrumSources || g_rtConfig->EmbedSpectrumScanTimes)
        {
            map<int, Embedder::QuantitationConfiguration> allSourcesQuantitationMethodMap;
            allSourcesQuantitationMethodMap[0] = Embedder::QuantitationConfiguration(g_rtConfig->QuantitationMethod, g_rtConfig->ReporterIonMzTolerance);

            for (size_t i=0 ; i < idpDbFilepaths.size(); ++i)
            {
                cout << "\rEmbedding subset spectra" << (g_rtConfig->EmbedSpectrumSources ? "" : " scan times")
                     << (g_rtConfig->QuantitationMethod == QuantitationMethod::None ? ": " : " and quantitation data: ")
                     << (i+1) << "/" << idpDbFilepaths.size()
                     << flush;

                if (g_rtConfig->EmbedSpectrumSources)
                    Embedder::embed(idpDbFilepaths[i], g_rtConfig->SourceSearchPath, allSourcesQuantitationMethodMap);
                else
                    Embedder::embedScanTime(idpDbFilepaths[i], g_rtConfig->SourceSearchPath, allSourcesQuantitationMethodMap);
            }
        }

        if (g_rtConfig->EmbedGeneMetadata)
            for (size_t i=0 ; i < idpDbFilepaths.size(); ++i)
            {
                cout << "\rEmbedding gene metadata: " << (i+1) << "/" << idpDbFilepaths.size() << flush;
                Embedder::embedGeneMetadata(idpDbFilepaths[i]);
            }

    }
    catch (exception& e)
    {
        cerr << "\nError: " << e.what() << endl;
        if (bal::contains(e.what(), "no filepath could be found"))
            cerr << "Find the file and add its parent directory to SourceSearchPath (a semicolon delimited list)." << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }
    catch (...)
    {
        cerr << "\nError: unhandled exception." << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }

	return 0;
}
