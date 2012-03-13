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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#ifndef _DIRECTAG_H
#define _DIRECTAG_H

#include "stdafx.h"
#include "freicore.h"
#include "directagSpectrum.h"
#include "tagsFile.h"
#include <boost/atomic.hpp>
#include <boost/cstdint.hpp>

#define DIRECTAG_LICENSE				COMMON_LICENSE

using namespace freicore;

namespace freicore
{

namespace directag
{
	/*struct tagFinder
	{
		tagFinder( const string& tagName = "" ) { name = tagName; }
		bool operator() ( TagInfo& test )
		{
			return name == test.tag;
		}
		string name;
	};*/

	extern SpectraList                  spectra;

	int						InitProcess( argList_t& args );
	int						ProcessHandler( int argc, char* argv[] );
	void					MakeResultFiles();
	void					GenerateForegroundTables();

	gapMap_t::iterator		FindPeakNear( gapMap_t&, float, float );

	// code for ScanRanker
	extern vector<NativeID>			mergedSpectraIndices;
	extern vector<NativeID>			highQualSpectraIndices;
	extern float					bestTagScoreMean;
	extern float					bestTagTICMean;
	extern float					tagMzRangeMean;
	extern float					bestTagScoreIQR;
	extern float					bestTagTICIQR;
	extern float					tagMzRangeIQR;
	extern size_t					numTaggedSpectra;


	struct spectraSortByQualScore
	{
		bool operator() ( const Spectrum* a, const Spectrum* b )
		{
			return a->qualScore > b->qualScore;  // descending order
		}
	};
}
}

#endif
