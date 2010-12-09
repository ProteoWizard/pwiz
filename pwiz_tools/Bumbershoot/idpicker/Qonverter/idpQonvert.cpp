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


#include "../freicore/pwiz_src/pwiz/utility/misc/Std.hpp"
#include "../freicore/freicore.h"

#include "idpQonvert.hpp"
#include <iomanip>
//#include "svnrev.hpp"

using namespace freicore;
using namespace IDPicker;
using std::setw;
using std::setfill;
using boost::format;


BEGIN_IDPICKER_NAMESPACE

int Version::Major()                {return 2;}
int Version::Minor()                {return 6;}
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
	g_residueMap = new ResidueMap;
	g_endianType = GetHostEndianType();
	g_numWorkers = GetNumProcessors();
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

    BOOST_FOREACH(const string& filepath, g_rtConfig->inputFilepaths)
    {
        Qonverter qonverter;
        qonverter.logQonversionDetails = g_rtConfig->WriteQonversionDetails;

        Qonverter::Settings& settings = qonverter.settingsByAnalysis[0];
        settings.qonverterMethod = Qonverter::QonverterMethod_StaticWeighted;
        settings.decoyPrefix = g_rtConfig->DecoyPrefix;
        settings.scoreInfoByName = g_rtConfig->scoreInfoByName;

        qonverter.Qonvert(filepath);
    }

	return 0;
}
