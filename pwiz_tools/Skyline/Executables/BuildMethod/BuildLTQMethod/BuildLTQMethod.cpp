/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// BuildLTQMethod.cpp
//   Builds a Thermo LTQ SRM instrument method from one or many
//   Skyline generated transition lists.

#define _WIN32_WINNT 0x0501     // Windows XP

#define DEFAULT_TOP_N_PRECURSORS 5

#define DEFAULT_MS1_MIN_MZ 400
#define DEFAULT_MS1_MAX_MZ 1400

#include <stdio.h>
#include <iostream>
#include <math.h>
#include <string.h>
#include "StringUtil.h"
#include "Verbosity.h"
#include "MethodBuilder.h"

// To create new versions of ltmethod.tlh|.tli, uncomment this line
// after installing the LTMethod DLL software.  Otherwise, you should
// be able to build without the DLL using the .tlh|.tli versions committed
// to this project.
// #define _IMPORT_PROCESSING_

#ifdef _IMPORT_PROCESSING_
#import "C:\\XCalibur\\system\\LTQ\\programs\\LTMethod.dll"
#else
#include "ltmethod.tlh"
//#include "ltmethod.tli" - change absolute path in the .tlh file
#endif
using namespace LTMETHODLib;

enum Fields
{
    precursor_mz,
    product_mz,
    collision_energy,
    start_time,
    stop_time,
    polarity,
    sequence,
    // CONSIDER: Use extended format like AB?
    protein,
    fragment,
    lib_rank,
    standard_type   // Optional
};

struct InclListEntry
{
	double precursor_mz;
	double start_time;
	double stop_time;
};

// Equation for low mass limit provided by Thermo
double FirstMass(double precursorMass, double activationQ)
{
    return ((int) (precursorMass * (activationQ/0.908))/5.0) * 5.0;
}

class BuildLTQMethod : public MethodBuilder
{
public:
    BuildLTQMethod();

    virtual void usage()
    {
        const char* usage =
            "Usage: BuildLTQMethod [options] <template file> [list file]*\n"
            "   Takes template LTQ SRM method file and a Skyline generated Thermo\n"
            "   scheduled transition list as inputs, to generate a new LTQ SRM method\n"
            "   file as output.\n"
			"   -f               Collect full scan MS/MS, ignoring product m/z values\n"
			"   -1               Collect full scan MS1 on each cycle\n"
			"                    (NB: it's a \"one\", not an \"L\")\n"
			//no way to set 'w' within Skyline yet
			"   -w [min]-[max]   Use min and max as the m/z window for full scan MS1\n"
			"                    [default range 400-1400]\n"
			"   -i               Create an inclusion list method instead of a transition\n"
			"                    list method\n"
			//no way to set 't' within Skyline yet
			"   -t [n]           When using an inclusion list, cycle through the top n most\n"
			"                    intense precursors after the MS1 scan. Default is 5.\n"
			"   -a [analyzer]    For MS1, use given mass analyzer (\"iontrap\" or \"ftms\")\n";
		    "   -b [analyzer]    For MS/MS, use given mass analyzer (\"iontrap\" or \"ftms\")"; 

        cerr << usage;

        MethodBuilder::usage();
    }

    virtual void parseCommandArgs(int argc, char* argv[]);

	virtual void createMethod(string templateMethod, string outputMethod,
        const vector<vector<string>>& tableTranList);

private:
    void replaceTransitionList(const vector<vector<string>>& tableTranList);
	void buildInclusionListMethod(const vector<vector<string>>& tableTranList);
    void printMethod();

private:
	bool _fullScans;
	bool _ms1Scans;
	double _ms1MinMz;
	double _ms1MaxMz;
	bool _inclusionList;
	int _topNPrecursors;
	int _msAnalyzer;
	int _msmsAnalyzer;
    ILCQMethodPtr _methodPtr;
};

int main(int argc, char* argv[])
{
    BuildLTQMethod builder;
    builder.parseCommandArgs(argc, argv);
    builder.build();
	
    return 0;
}

BuildLTQMethod::BuildLTQMethod()
{
	_fullScans = false;
	_ms1Scans = false;
	_ms1MinMz = DEFAULT_MS1_MIN_MZ;
	_ms1MaxMz = DEFAULT_MS1_MAX_MZ;
    _topNPrecursors = DEFAULT_TOP_N_PRECURSORS;
	_inclusionList = false;
	_msAnalyzer = 1;
	_msmsAnalyzer = 0;
    if (FAILED(CoInitialize(NULL)))
        Verbosity::error("Failure during initialization.");

    try
    {
        ILCQMethodPtr methodPtr("LTMethod.LTMethod.1");
        _methodPtr = methodPtr;
    }
    catch (_com_error&)
    {
        Verbosity::error("Failure during initialization, LTQ method support may not be installed. Method export for a LTQ should be performed on the LTQ instrument control computer.");
    }
}

static int getAnalyzer(char* analyzer)
{
	if(strcmp(analyzer, "QIT") == 0)
		return 0;
	else if(strcmp(analyzer, "Orbitrap") == 0 || strcmp(analyzer, "FT-ICR") == 0)
		return 1;
	else
		Verbosity::error((string("The LTQ instruments do not support ") + analyzer + " mass analyzers.").c_str());
}

void BuildLTQMethod::parseCommandArgs(int argc, char* argv[])
{
	// Check for full scan flag
    int i = 1;
    while (i < argc && *argv[i] == '-')
    {
		int processed = 1;
        switch (*(argv[i++]+1))
        {
        case 'f':
			_fullScans = true;
            break;
		case '1':
		    _ms1Scans = true;
			break;
		case 'w':
			{
			if(i >= argc) usage();

			vector<string> minAndMax;
			split(argv[i++], minAndMax, "-");
			processed = 2;
			if(minAndMax.size() < 2u) usage();

			_ms1MinMz = atoi(minAndMax[0].c_str());
			_ms1MaxMz = atoi(minAndMax[1].c_str());
			}
			break;
		case 'i':
			_inclusionList = true;
			break;
		case 't':
			if(i >= argc) usage();
			_topNPrecursors = atoi(argv[i++]);
			processed = 2;
			if(_topNPrecursors < 1) usage();
			break;
		case 'a':
			if(i >= argc) usage();
			_msAnalyzer = getAnalyzer(argv[i]);
			processed = 2;
			i++;
			break;
		case 'b':
			if(i >= argc) usage();
			_msmsAnalyzer = getAnalyzer(argv[i]);
			processed = 2;
			i++;
			break;
		default:
			processed = 0;
			break;
        }
		
		if(processed)
		{
			// Remove this flag so that it will not be processed again by the base class
			if (i < argc)
				memmove(&argv[i-processed], &argv[i], sizeof(char*)*(argc-i));
			argc -= processed;
			i -= processed;
		}
    }

	if(_topNPrecursors < 1)
	{
		usage();
	}

	if(_ms1MinMz <= 0 || _ms1MaxMz <= 0 || _ms1MaxMz <= _ms1MinMz)
	{
		usage();
	}

	MethodBuilder::parseCommandArgs(argc, argv);
}

void BuildLTQMethod::createMethod(string templateMethod, string outputMethod, const vector<vector<string>>& tableTranList)
{
    // Open the template
    try
    {
        // Template gets copied to output before this method is called,
        // because SaveAs() corrupts the output file.  Only Save() works
        // correctly.
        _bstr_t outputMethodW = str_to_wstr(outputMethod).c_str();

        _methodPtr->Open(outputMethodW);
    }
    catch (_com_error& e)
    {
		//cerr << e.Error() << "\n";
		//cerr << e.ErrorMessage() << "\n";
        Verbosity::error("Failure opening template method %s", templateMethod.c_str());
    }

    // Inject the new transition list into the template and save
    try
    {
		if(_inclusionList)
			buildInclusionListMethod(tableTranList);
		else
			replaceTransitionList(tableTranList);
		
        // Save into existing to avoid losing information that Thermo
        // strips with SaveAs()
		_methodPtr->Save();

        _methodPtr->Close();
    }
    catch (_com_error& e)
    {
		//cerr << e.Error() << "\n";
		//cerr << e.ErrorMessage() << "\n";
        Verbosity::error("Failure creating new method from %s", templateMethod.c_str());
    }
}

void BuildLTQMethod::buildInclusionListMethod(const vector<vector<string>>& tableTranList)
{
	//Creating an inclusion list consists of the following:
	//1 calling put_NumMassTimeWindows to allocate space for inclusion list entries
	//2 putting parent mz/start/stop entries in the list
	//2.5 creating a scan event for the MS1 scan
	//3 creating scan events for the top precursors (either from the template or CLI parameter)
	//4 making sure that the method's Acquire Time is greater than all the entry
	//  stop times.
	//5 making sure that the MS1 scan range includes all of the inclusion list mz's

	//do #4 first because to set the Acquire Time we have to set some values
	//for segments as well, and it makes sense to do this first. It might work
	//later, but this definitely works.

	//To find the max stop time (hence the Acquire Time), we have to do a "group by"
	//on inclusion list entries. At the same time, we can build a vector for the list
	//entries to be used later, and to count how many there are.

	vector<InclListEntry> iList;
	vector<vector<string>>::const_iterator trans = tableTranList.begin();
	double precursorMz = -1; //Cannot happen
	
	//Also make sure that all inclusion list entry end times are less than the
	//method acquire time
	double maxStopTime = 0;

	for(; trans != tableTranList.end(); ++trans)
	{
		double newPrecMz = atof(trans->at(precursor_mz).c_str());

		if(precursorMz != newPrecMz)
		{
			double stopTime = atof(trans->at(stop_time).c_str());
			//There is an error if any stop time is greater than the method's "Acquire Time"
			//so make sure that never happens
			if(stopTime > maxStopTime)
				maxStopTime = stopTime;

			InclListEntry listEntry;
			listEntry.precursor_mz = newPrecMz;
			listEntry.start_time = atof(trans->at(start_time).c_str());
			listEntry.stop_time = stopTime;

			if(listEntry.precursor_mz > _ms1MaxMz)
				_ms1MaxMz = listEntry.precursor_mz;

			if(listEntry.precursor_mz < _ms1MinMz)
				_ms1MinMz = listEntry.precursor_mz;
			
			iList.push_back(listEntry);

			precursorMz = newPrecMz;
		}
	}
	
	//#4
	_methodPtr->AcquireTime = maxStopTime;
	_methodPtr->NumSegments = 1;
	_methodPtr->CurrentSegment = 0;
	_methodPtr->SegmentTime = maxStopTime;
	_methodPtr->StartDelay = 0.0;
	

	//#3 Determine whether this template matches an inclusion list. If so, we will
	//write over it anyway, but remember how many scan events there were after the MS1 scan
	bool ilTemplate = false;
	if(_methodPtr->NumScanEvents > 0)
	{
		//check that the first scan is an MS1 scan
		_methodPtr->CurrentScanEvent = 0;
		if(_methodPtr->ScanType == 0)
		{
			ilTemplate = true;
			for(int i = 1; i < _methodPtr->NumScanEvents; i++)
			{
				_methodPtr->CurrentScanEvent = i;
				if(_methodPtr->DependentScan == 0)
				{
					ilTemplate = false;
					break;
				}
			}
		}
	}
	
	if(ilTemplate)
		_topNPrecursors = _methodPtr->NumScanEvents - 1;
	

	_methodPtr->NumScanEvents = 1;
	_methodPtr->CurrentScanEvent = 0;
	_methodPtr->ScanMode = 0;    // 0 = MS, ..., 9 = MS10
	_methodPtr->ScanType = 0;    // 0 = Full, 1 = SIM/SRM
	_methodPtr->DataType = 1;    // 0 = Centroid, 1 = Profile
	_methodPtr->NumReactions = 0;
	
	_methodPtr->Analyzer = _msAnalyzer;	//0 = Ion Trap, 1 = FTMS

	_methodPtr->NumMassRanges = 1;
	_methodPtr->SetMassRange(0, _ms1MinMz, _ms1MaxMz);

	_methodPtr->DataDepUseGlobalMassLists = 1;
	
	//Install the scan events 2 through _topNPrecursors+1 (which is >=2)
	_methodPtr->NumScanEvents = 1 + _topNPrecursors;
	for(int i = 1; i < _topNPrecursors + 1; i++)
	{
		_methodPtr->CurrentScanEvent = i;
		_methodPtr->NumReactions = 0;	//These do not default to zero, you must set them!!
		_methodPtr->NumMassRanges = 0;
		_methodPtr->Analyzer = _msmsAnalyzer;
		_methodPtr->ScanType = 0;		//0 = full, 1 = SRM
		_methodPtr->ScanMode = 1;		//0 = MS1 ... 9 = MS10
		_methodPtr->DataDepMasterEvent = 0;
		_methodPtr->DataDepMode = 1;	//1 = Nth most intense ion from inclusion list, 0 = Nth most intense overall
		_methodPtr->DependentScan = 1;
		_methodPtr->DataType = 0; //Centroid
		_methodPtr->DataDepNthMostIntenseIon = i; //each scan Si should look for the (i-1)th most intense ion
													//(because S0 is the MS1 scan)
		_methodPtr->DataDepFallThroughToNthMostIntense = 1; // "Most intense if no Parent Masses found" from the GUI
	}
	
	//#1 Now use the grouped data. Number of precursors,
	//(and is there a property for this? I would much rather use a property to keep the code uniform)
	_methodPtr->put_NumMassTimeWindows(0, iList.size());

	//#2 and the data for each one
	for(int i = 0; i < iList.size(); i++)
	{
		_methodPtr->SetMassTimeWindow(0, i, iList[i].start_time, iList[i].stop_time, iList[i].precursor_mz);
	}
}

void BuildLTQMethod::replaceTransitionList(const vector<vector<string>>& tableTranList)
{
    short numScans = _methodPtr->NumScanEvents;
    short scanType = _methodPtr->ScanType;
    short analyzerType = _methodPtr->Analyzer;

	short ms1DataType = 0;
	short ms2DataType = 0;

    double precursorMass = 0.0;
    short activationType = 0;
    double isolationWindow = 2.0;
    double normalizedCE = 35.0;
    float activationQ = 0.25f;
    double activationTime = 30.0;
    double productWindow = 2.0;

	// Initialize some variables from the template, if possible
    if (numScans > 0) //&& scanType == 1 && analyzerType == 0)
    {
		if(_methodPtr->ScanType == 0) //MS1 scan
		{
			_methodPtr->CurrentScanEvent = 0;
			ms1DataType = _methodPtr->DataType;
			if(numScans > 1)
			{
				_methodPtr->CurrentScanEvent = 1;
				ms2DataType = _methodPtr->DataType;
				_methodPtr->CurrentScanEvent = 0;
			}
		}
		else
		{
			ms2DataType = _methodPtr->DataType;
		}

        if (_methodPtr->NumReactions > 0)
        {
            _methodPtr->GetReaction2(0, &precursorMass, &activationType, &isolationWindow,
                &normalizedCE, &activationQ, &activationTime);
        }

        if (_methodPtr->NumMassRanges > 0)
        {
            double startMass = 0.0, endMass = 0.0;
            _methodPtr->GetMassRange(0, &startMass, &endMass);
            double deltaMass = endMass - startMass;
            if (deltaMass > 0)
                productWindow = deltaMass;
        }
    }
	
    // TODO: Handle scheduling with segments
    short scanCount = 0;
    short rangeCount = 0;

	// Always start with zero precursor mass, or the first precursor won't get written
    precursorMass = 0.0;

	
	//First, add an MS1 scan if the flag has been set
	if(_ms1Scans)
	{
		scanCount++;
		_methodPtr->NumScanEvents = scanCount;
		_methodPtr->CurrentScanEvent = scanCount - 1;
		_methodPtr->ScanMode = 0;    // 0 = MS, ..., 9 = MS10
		_methodPtr->ScanType = 0;    // 0 = Full, 1 = SIM/SRM
		_methodPtr->DataType = ms1DataType;    // 0 = Centroid, 1 = Profile
		_methodPtr->NumReactions = 0;
		_methodPtr->Analyzer = _msAnalyzer;

		_methodPtr->NumMassRanges = 1;
		_methodPtr->SetMassRange(0, _ms1MinMz, _ms1MaxMz);
	}
	
    vector<vector<string>>::const_iterator it = tableTranList.begin();
	
    for (; it != tableTranList.end(); it++)
    {
        string value = it->at(precursor_mz);
        double precursorMassList = atof(value.c_str());
        if (precursorMassList == 0.0)
            Verbosity::error("Invalid precursor m/z %s", value);

        // Start a new scan, if precursor changes, or the maximum of 10 ranges
        // per scan is reached.
        if (precursorMassList != precursorMass || rangeCount >= 10)
        {
            scanCount++;
            _methodPtr->NumScanEvents = scanCount;
            _methodPtr->CurrentScanEvent = scanCount - 1;
            _methodPtr->ScanMode = 1;    // 0 = MS, ..., 9 = MS10
			_methodPtr->ScanType = (_fullScans ? 0 : 1);    // 0 = Full, 1 = SIM/SRM
			_methodPtr->DataType = ms2DataType;    // 0 = Centroid, 1 = Profile
            _methodPtr->NumReactions = 1;
			_methodPtr->Analyzer = _msmsAnalyzer;

            precursorMass = precursorMassList;
			//_methodPtr->SetReaction(0, precursorMass, isolationWindow, normalizedCE, activationQ, activationTime);
            _methodPtr->SetReaction2(0, precursorMass, activationType, isolationWindow,
                normalizedCE, activationQ, activationTime);
			_methodPtr->GetReaction2(0, &precursorMass, &activationType, &isolationWindow, &normalizedCE, &activationQ, &activationTime);

			if (_fullScans)
			{
		        _methodPtr->NumMassRanges = 1;
	            _methodPtr->SetMassRange(0, FirstMass(precursorMass, activationQ), 2000);
			}

            rangeCount = 0;
        }

		// Ignore product ion values, if using full scans.
		if (_fullScans)
			continue;

        value = it->at(product_mz);
        double productMass = atof(value.c_str());
        if (productMass == 0.0)
            Verbosity::error("Invalid product m/z %s", value);
        double startMass = productMass - productWindow/2;
        double endMass = productMass + productWindow/2;
        double firstMass = FirstMass(precursorMass, activationQ);
        if (firstMass > startMass)
        {
            Verbosity::error("Product start m/z %.6f less than low mass limit %.0f for %s - %s: %s, %s",
                startMass,
                firstMass,
                it->at(protein).c_str(),
                it->at(sequence).c_str(),
                it->at(precursor_mz).c_str(),
                it->at(product_mz).c_str());
        }
        
        // Because ILCQMethod sometimes changes this mysteriously
        _methodPtr->CurrentScanEvent = scanCount - 1;

        _methodPtr->NumMassRanges = ++rangeCount;
        short i = rangeCount - 1;

        double startMassLast = 0;
        double endMassLast = 0;

        for (; i > 0; i--)
        {
            double startMassOld = 0, endMassOld = 0;
            _methodPtr->GetMassRange(i - 1, &startMassOld, &endMassOld);
            // Copy mass ranges until one with lower start-mass is encountered
            // (insertion sort)
            if (startMassOld < startMass)
            {
                // Check the range before the new one to make sure its end is not
                // greater than the new range to be inserted.
                if (endMassOld >= startMass)
                {
                    Verbosity::error("Overlapping mass ranges %.3f-%.3f and %.3f-%.3f found for %s - %s: %s",
                        startMassOld, endMassOld, startMass, endMass,
                        it->at(protein).c_str(),
                        it->at(sequence).c_str(),
                        it->at(precursor_mz).c_str());
                }
                break;
            }

            _methodPtr->SetMassRange(i, startMassOld, endMassOld);

            startMassLast = startMassOld;
            endMassLast = endMassOld;
        }

        // Check the last mass that was greater than the new one to make sure
        // its start is greater than the end of the new range to be inserted.
        if (startMassLast != 0 && endMass >= startMassLast)
        {
            Verbosity::error("Overlapping mass ranges %.3f-%.3f and %.3f-%.3f found for %s - %s: %s",
                startMass, endMass, startMassLast, endMassLast,
                it->at(protein).c_str(),
                it->at(sequence).c_str(),
                it->at(precursor_mz).c_str());
        }

        // Insert the new mass range in its place
        _methodPtr->SetMassRange(i, startMass, endMass);

        if (Verbosity::get_verbosity() == V_DEBUG)
        {
            // If debugging errors, check method validity after every
            // transition is added, but otherwise this is way too slow.
            long valid = 0;
            try
            {
                _methodPtr->IsMethodValid(&valid);
            }
            catch (_com_error&)
            {
                valid = 0;
            }
            if (!valid)
            {
                printMethod();
                Verbosity::error("Failure adding the transition %s - %s: %s, %s",
                    it->at(protein).c_str(),
                    it->at(sequence).c_str(),
                    it->at(precursor_mz).c_str(),
                    it->at(product_mz).c_str());
            }
        }
    }

    _methodPtr->NumScanEvents = scanCount;
}

void BuildLTQMethod::printMethod()
{
    int numTrans = 0;
    for (short i = 0, numScans = _methodPtr->NumScanEvents; i < numScans; i++)
    {
        _methodPtr->CurrentScanEvent = i;
        for (short j = 0, numReactions = _methodPtr->NumReactions; j < numReactions; j++)
        {
            double precursorMass = 0;
            short activationType = 0;
            double isolationWindow = 2.0;
            double normalizedCE = 35.0;
            float activationQ = 0.25f;
            double activationTime = 30.0;

            _methodPtr->GetReaction2(j, &precursorMass, &activationType,  &isolationWindow,
                &normalizedCE, &activationQ, &activationTime);
            cerr << "mass = " << precursorMass
                << ", type = " << activationType
                << ", win = " << isolationWindow
                << ", ce = " << normalizedCE
                << ", q = " << activationQ
                << ", time = " << activationTime
                << endl;
        }

        for (short j = 0, numRanges = _methodPtr->NumMassRanges; j < numRanges; j++)
        {
            numTrans++;

            double mass1 = 0;
            double mass2 = 0;
            _methodPtr->GetMassRange(j, &mass1, &mass2);
            cerr << "    start-mass = " << mass1
                << ", end-mass = " << mass2
                << endl;
        }
    }
    cerr << "(" << _methodPtr->NumScanEvents << " precursors, " << numTrans << " transitions)" << endl;
    cerr << endl;
}
