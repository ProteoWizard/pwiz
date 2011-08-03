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

#include <iostream>
#include <fstream>
#include <sstream>
#include <map>
#include <math.h>
#include <time.h>

#include "sqlite/sqlite3pp.h"

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
using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace SimpleCrawdad;
using namespace crawpeaks;

namespace sqlite = sqlite3pp;

struct runtimeOptions {
	bool tabbedOutput;
	
	// set default values
//	runtimeOptions() : tabbedOutput = false {}
};

static const runtimeOptions runtimeDefaults = { false };

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
	int first;
	int second;
	int third;
	int fourth;
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

struct intensityPair {
	double precursorIntensity;
	double peakIntensity;
};

struct sourceFile {
	string id;
	string filename;
	string dbFilename;
};

void MetricMaster(const string&, string, const string&, runtimeOptions);

double Q1(vector<double>); // First quartile function
double Q2(vector<double>); // Second quartile (aka median) function
double Q3(vector<double>); // Third quartile function

void toLowerCase(std::string &str)
{
	std::transform(str.begin(), str.end(), str.begin(), std::tolower);
}

bool compareByPeak(const intensityPair& pair1, const intensityPair& pair2) { return pair1.peakIntensity < pair2.peakIntensity; }

vector<string> GetNativeId(const string&, const string&);

vector<windowData> MZRTWindows(const string&, const string&, map<string, int>, vector<ms_amalgam>);

multimap<int, string> GetDuplicateID(const string&, const string&);

int PeptidesIdentifiedOnce(const string&, const string&);
int PeptidesIdentifiedTwice(const string&, const string&);
int PeptidesIdentifiedThrice(const string&, const string&);

double MedianPrecursorMZ(const string&, const string&);

double MedianRealPrecursorError(const string&, const string&);
double GetMeanAbsolutePrecursorErrors(const string&, const string&);
ppmStruct GetRealPrecursorErrorPPM(const string&, const string&);

double GetMedianIDScore(const string&, const string&);

int GetNumTrypticMS2Spectra(const string&, const string&);
int GetNumTrypticPeptides(const string&, const string&);
int GetNumUniqueTrypticPeptides(const string&, const string&);
int GetNumUniqueSemiTrypticPeptides(const string&, const string&);

vector<sourceFile> GetSpectraSources(const string& dbFilename);
