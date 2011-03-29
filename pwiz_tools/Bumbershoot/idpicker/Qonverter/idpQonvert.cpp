//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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

#include "Parser.hpp"
#include "idpQonvert.hpp"
#include <iomanip>
//#include "svnrev.hpp"

using namespace freicore;
using namespace IDPicker;
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

	g_rtConfig = new RunTimeConfig;
	g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
	g_hostString = "Process #" + lexical_cast<string>( g_pid ) + " (" + GetHostname() + ")";

	// First set the working directory, if provided
	for( size_t i=1; i < args.size(); ++i )
	{
		if( args[i] == "-workdir" && i+1 <= args.size() )
		{
			chdir( args[i+1].c_str() );
			args.erase( args.begin() + i );
		} else if( args[i] == "-cpus" && i+1 <= args.size() )
		{
			g_numWorkers = atoi( args[i+1].c_str() );
			args.erase( args.begin() + i );
		} else
			continue;

		args.erase( args.begin() + i );
		--i;
	}

	for( size_t i=1; i < args.size(); ++i )
	{
		if( args[i] == "-cfg" && i+1 <= args.size() )
		{
			if( g_rtConfig->initializeFromFile( args[i+1] ) )
			{
				cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
				return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
			}
			args.erase( args.begin() + i );

		} else
			continue;

		args.erase( args.begin() + i );
		--i;
	}

	if( g_rtConfig->inputFilepaths.empty() && args.size() < 2 )
	{
		cerr << "Not enough arguments.\nUsage: idpQonvert [<idpDB filemask> [<another idpDB filemask> ...]]" << endl;
		return QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS;
	}

	if( !g_rtConfig->initialized() )
	{
		if( g_rtConfig->initializeFromFile() )
		{
			cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
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
		g_rtConfig->setVariables( vars );
	} catch( std::exception& e )
	{
		if( g_pid == 0 ) cerr << g_hostString << " had an error while overriding runtime variables: " << e.what() << endl;
		return QONVERT_ERROR_RUNTIME_CONFIG_OVERRIDE_FAILURE;
	}

	if( g_pid == 0 )
	{
		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-dump" )
			{
				g_rtConfig->dump();
				g_residueMap->dump();
				args.erase( args.begin() + i );
				--i;
			}
		}

		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i][0] == '-' )
			{
				cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
				args.erase( args.begin() + i );
				--i;
			}
		}
	}

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
            error << "Command-line idpQonvert only supports qonverting a single distinct analysis at a time." << endl;

            for (size_t i=0; i < distinctAnalyses.size(); ++i)
            {
                const Parser::Analysis& analysis = *distinctAnalyses[i];
                error << "Analysis " << analysis.name << " (" << analysis.softwareName << " " << analysis.softwareVersion << ")" << endl;
                error << "\tstartTime: " << analysis.startTime << endl;

                if (i == 0)
                {
                    vector<string> firstParameters, secondParameters;
                    BOOST_FOREACH(const AnalysisParameter& kvp, analysis.parameters)
                        firstParameters.push_back(kvp.first + ": " + kvp.second);
                    BOOST_FOREACH(const AnalysisParameter& kvp, distinctAnalyses[1]->parameters)
                        secondParameters.push_back(kvp.first + ": " + kvp.second);

                    vector<string> a_b, b_a;
                    pwiz::data::diff_impl::vector_diff(firstParameters, secondParameters, a_b, b_a);

                    error << "\tdistinct parameters in first analysis: " << bal::join(a_b, ", ") << endl;
                    error << "\tdistinct parameters in second analysis: " << bal::join(b_a, ", ") << endl;
                    //BOOST_FOREACH(const AnalysisParameter& kvp, analysis.parameters)
                    //    cerr << "\t\t" << kvp.first << ": " << kvp.second << endl;
                    //error << endl;
                }
            }
            throw runtime_error(error.str());
        }

        // verify the protein database from the imported files matches ProteinDatabase
        const Parser::Analysis& analysis = *distinctAnalyses[0];
        if (!bal::iequals(bfs::path(analysis.importSettings.proteinDatabaseFilepath).replace_extension("").filename(),
                          bfs::path(g_rtConfig->ProteinDatabase).replace_extension("").filename()))
            throw runtime_error("ProteinDatabase " + bfs::path(g_rtConfig->ProteinDatabase).filename() +
                                " does not match " + bfs::path(analysis.importSettings.proteinDatabaseFilepath).filename());

        analysis.importSettings.proteinDatabaseFilepath = g_rtConfig->ProteinDatabase;

        Qonverter::Settings& settings = analysis.importSettings.qonverterSettings;
        settings.qonverterMethod = g_rtConfig->QonverterMethod;
        settings.kernel = g_rtConfig->Kernel;
        settings.chargeStateHandling = g_rtConfig->ChargeStateHandling;
        settings.terminalSpecificityHandling = g_rtConfig->TerminalSpecificityHandling;
        settings.missedCleavagesHandling = g_rtConfig->MissedCleavagesHandling;
        settings.massErrorHandling = g_rtConfig->MassErrorHandling;
        settings.decoyPrefix = g_rtConfig->DecoyPrefix;
        settings.scoreInfoByName = g_rtConfig->scoreInfoByName;
    }
};

struct UserFeedbackIterationListener : public IterationListener
{
    virtual Status update(const UpdateMessage& updateMessage)
    {
        int index = updateMessage.iterationIndex;
        int count = updateMessage.iterationCount;

        // when the index is 0, create a new line
        if (index == 0)
            cout << endl;

        cout << updateMessage.message;
        if (index > 0)
        {
            cout << ": " << index+1;
            if (count > 0)
                cout << "/" << count;
        }

        // add tabs to erase all of the previous line
        cout << "\t\t\t\r" << flush;

        return Status_Ok;
    }
};

END_IDPICKER_NAMESPACE


int main( int argc, char* argv[] )
{
	argList_t args( argv, argv+argc );

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

    try
    {
        Parser parser;

        // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
        const size_t iterationPeriod = 100;
        UserFeedbackIterationListener feedback;
        parser.iterationListenerRegistry.addListener(feedback, iterationPeriod);

        parser.importSettingsCallback = Parser::ImportSettingsCallbackPtr(new ImportSettingsHandler);
        parser.parse(vector<string>(g_rtConfig->inputFilepaths.begin(), g_rtConfig->inputFilepaths.end()));
    }
    catch (exception& e)
    {
        cerr << "Error: " << e.what() << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }
    catch (...)
    {
        cerr << "Error: unhandled exception." << endl;
        return QONVERT_ERROR_UNHANDLED_EXCEPTION;
    }

	return 0;
}
