//
// $Id$
//
//
// Jonathan Katz <Jonathan.Katz@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "rawfile/RawFile.h"
#include <iostream>


using namespace std;
using namespace pwiz;
using namespace pwiz::raw;


void printScanInfo(RawFilePtr& rawFile, int scanNumber, bool outputLogs = false)
{
    auto_ptr<ScanInfo> scanInfo = rawFile->getScanInfo(scanNumber);
    cout << boolalpha;
    cout << "scanNumber: " << scanInfo->scanNumber() << endl;
    cout << "filter: " << scanInfo->filter() << endl;
    cout << "isProfileScan: " << scanInfo->isProfileScan() << endl;
    cout << "isCentroidScan: " << scanInfo->isCentroidScan() << endl;
    cout << "packetCount: " << scanInfo->packetCount() << endl;
    cout << "startTime: " << scanInfo->startTime() << endl;
    cout << "lowMass: " << scanInfo->lowMass() << endl;
    cout << "highMass: " << scanInfo->highMass() << endl;
    cout << "totalIonCurrent: " << scanInfo->totalIonCurrent() << endl;
    cout << "basePeakMass: " << scanInfo->basePeakMass() << endl;
    cout << "basePeakIntensity: " << scanInfo->basePeakIntensity() << endl;
    cout << "channelCount: " << scanInfo->channelCount() << endl;
    cout << "isUniformTime: " << scanInfo->isUniformTime() << endl;
    cout << "frequency: " << scanInfo->frequency() << endl;
    
    if (outputLogs)
    {
        cout << "\nStatus Log (" << scanInfo->statusLogSize() << " entries, RT: " << scanInfo->statusLogRT() << "):\n";
        for (int i=0; i<scanInfo->statusLogSize(); i++)
		{
			char sqldate[256];
	        int mm,dd,yy,hh,mmm,ss;

			// KATZ - create an SQL format date from RawFile Date
			sprintf(sqldate,"%s",(rawFile->getCreationDate()).c_str());            
			sscanf(sqldate,"%d/%d/%d %d:%d:%d",&mm,&dd,&yy,&hh,&mmm,&ss);
			sprintf(sqldate,"%d-%.2d-%.2d %.2d:%.2d:%.2d",yy,mm,dd,hh,mmm,ss);

			//KATZ -- output line, format "_SCAN_<scan#>_<field#>_<fieldID>_<fieldVAL>"
			cout << "_SCAN_" << scanNumber << "_" << i << "_" << scanInfo->statusLogLabel(i) << " " << scanInfo->statusLogValue(i) << endl;
            
			//KATZ -- output line, same info, as SQL command
			cout << "INSERT INTO `TestReadBacks` (`RawFile`, `RunDate`, `Scan`, `LabelID`, `LabelName`, `LabelValue`)";
			cout << " VALUES(";
              cout << "\"" << rawFile->value(raw::FileName) << "\",";
			  cout << "\"" << sqldate << "\",";
              cout << "\"" << scanNumber << "\",";
			  cout << "\"" << i << "\",";
              cout << "\"" << scanInfo->statusLogLabel(i) << "\",";
              cout << "\"" << scanInfo->statusLogValue(i) << "\"";
			cout << ");" << endl ;

			//KATZ -- output line, same info, as CSV for insert into table cmd
            cout << "XXX ";
              cout << "\"" << rawFile->value(raw::FileName) << "\";";
			  cout << "\"" << sqldate << "\";";
              cout << "\"" << scanNumber << "\";";
			  cout << "\"" << i << "\";";
              cout << "\"" << scanInfo->statusLogLabel(i) << "\";";
              cout << "\"" << scanInfo->statusLogValue(i) << "\"";
			cout << "" << endl ;


			//KATZ 
            //cout << i << ": " << scanInfo->statusLogLabel(i) << " " << scanInfo->statusLogValue(i) << endl;
		}

        cout << "\nTrailer Extra (" << scanInfo->trailerExtraSize() << " entries):\n";
        for (int i=0; i<scanInfo->trailerExtraSize(); i++)
            cout << i << ": " << scanInfo->trailerExtraLabel(i) << " " << scanInfo->trailerExtraValue(i) << endl;
    }    

    cout << endl;
}


void doScanInfo(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "SCAN INFO\n";
    cout << "*************************************************************\n";

	long scanCount = rawFile->value(raw::NumSpectra);
	cout << "Scan count: " << scanCount << endl;

	for (int i=1; i<=scanCount; i++)
		printScanInfo(rawFile, i, true);
}


void doTuneData(RawFilePtr& rawFile)
{
    try
    {
        cout << "*************************************************************\n";
        cout << "TUNE DATA\n";
        cout << "*************************************************************\n";

        long count = rawFile->value(raw::NumTuneData);
        cout << "Tune Data (" << count << " entries):" << endl;

        // segmentNumber seems to be a 0-based index; 
        // but the docs say 1-based...
        
        auto_ptr<LabelValueArray> a(rawFile->getTuneData(0));
        for (int i=0; i<a->size(); i++)
            cout << a->label(i) << " " << a->value(i) << endl;
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
    }
}


void doFile(const char* filename)
{
    try
    {
        RawFilePtr rawFile(filename);
        cout << "File: " << rawFile->value(raw::FileName) << endl;
        cout << "Creation Date: " << rawFile->getCreationDate() << endl;
        rawFile->setCurrentController(Controller_MS, 1);

        doTuneData(rawFile);
	    doScanInfo(rawFile);
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
    }

    cout << endl;    
}


int main(int argc, char* argv[])
{
    if (argc != 2)
    {
        cout << "Usage: readbacks filename.meow\n";
        return 1;
    }

    const char* filename = argv[1];

    RawFileLibrary library;
    doFile(filename);
}

