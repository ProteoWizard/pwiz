#ifndef _DIRECTAG_H
#define _DIRECTAG_H

#include "stdafx.h"
#include "freicore.h"
#include "base64.h"
#include "directagSpectrum.h"
#include "simplethreads.h"
#include "tagsFile.h"

#define DIRECTAG_MAJOR				1.
#define DIRECTAG_MINOR				2.
#define DIRECTAG_BUILD				3

#define DIRECTAG_VERSION				BOOST_PP_CAT( DIRECTAG_MAJOR, BOOST_PP_CAT( DIRECTAG_MINOR, DIRECTAG_BUILD ) )
#define DIRECTAG_VERSION_STRING			BOOST_PP_STRINGIZE( DIRECTAG_VERSION )
#define DIRECTAG_BUILD_DATE				"6/17/2008"

#define DIRECTAG_LICENSE				COMMON_LICENSE

using namespace freicore;

namespace freicore
{
namespace directag
{
	#ifdef USE_MPI
	void TransmitConfigsToChildProcesses();
	void ReceiveConfigsFromRootProcess();
	#endif

	extern double lnCombin( int a, int b );
	extern float GetMassOfResidues( const string& a, bool b = false );

	struct tagFinder
	{
		tagFinder( const string& tagName = "" ) { name = tagName; }
		bool operator() ( TagInfo& test )
		{
			return name == test.tag;
		}
		string name;
	};

	struct taggingStats
	{
		taggingStats() :
			numSpectraTagged(0), numResidueMassGaps(0), 
			numTagsGenerated(0), numTagsRetained(0) {}
		size_t numSpectraTagged;
		size_t numResidueMassGaps;
		size_t numTagsGenerated;
		size_t numTagsRetained;

		taggingStats operator+ ( const taggingStats& rhs )
		{
			taggingStats tmp;
			tmp.numSpectraTagged = numSpectraTagged + rhs.numSpectraTagged;
			tmp.numResidueMassGaps = numResidueMassGaps + rhs.numResidueMassGaps;
			tmp.numTagsGenerated = numTagsGenerated + rhs.numTagsGenerated;
			tmp.numTagsRetained = numTagsRetained + rhs.numTagsRetained;
			return tmp;
		}
	};

	struct WorkerInfo : public BaseWorkerInfo
	{
		WorkerInfo( int num, int start, int end ) : BaseWorkerInfo( num, start, end ) {}
		taggingStats stats;
	};

	SpectraList					spectra;
	map< char, float >			compositionInfo;

	RunTimeConfig*				g_rtConfig;

	simplethread_mutex_t		resourceMutex;

	int						InitProcess( argList_t& args );
	int						ProcessHandler( int argc, char* argv[] );
	void					MakeResultFiles();
	void					GenerateForegroundTables();

	gapMap_t::iterator		FindPeakNear( gapMap_t&, float, float );
}
}

#endif
