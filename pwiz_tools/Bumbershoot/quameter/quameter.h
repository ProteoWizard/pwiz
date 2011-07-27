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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Ken Polzin.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#include "stdafx.h"
#include "shared_defs.h"

#include <iostream>
#include <sstream>
//#include <fstream>
#include <map>
#include <math.h>
#include <time.h>

#include "../sqlite/sqlite3pp.h"

#include "crawdad/SimpleCrawdad.h"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"

#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>

#include <boost/timer.hpp>
#include <boost/thread/thread.hpp>
#include <boost/filesystem/operations.hpp>

using boost::shared_ptr;
using namespace boost::filesystem;
using namespace boost::icl;
using namespace std;
using namespace freicore;

namespace sqlite = sqlite3pp;

struct ms_amalgam {
	string MS2;
	double MS2Retention;
	string precursor;
	double precursorMZ;
	double precursorIntensity;
	double precursorRetention;
};

struct preMZandRT {
	double MS2Retention;
	double precursorMZ;
	double precursorRetention;
};

struct chromatogram {
	vector<double> MS1Intensity;
	vector<double> MS1RT;
};

struct fourInts {
	int	first;
	int second;
	int	third;
	int	fourth;
};

struct ppmStruct {
	double median;
	double interquartileRange;
};

struct windowData {
	int peptide;
	double firstMS2RT;
	interval_set<double> preMZ;
	interval_set<double> preRT;
	vector<double> MS1Intensity;
	vector<double> MS1RT;
};

struct peakData {
	int peptide;
	double peakIntensity;
	double peakRT;
	double fwhm;
};

struct sourceFile {
	string id;
	string filename;
	string dbFilename;
};
