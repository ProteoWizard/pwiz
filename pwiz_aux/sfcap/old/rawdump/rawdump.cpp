//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
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
#include <iomanip>
#include <vector>
#include <map>
#include <string>
#include <numeric>


using namespace std;
using namespace pwiz;
using namespace pwiz::raw;


template <typename id_type>
void printValue(RawFilePtr& rawFile, id_type id)
{
    try
    {
        cout << "  " << rawFile->name(id) << ": " << rawFile->value(id) << endl;
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
    }
}


void testValues(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "VALUES\n";
    cout << "*************************************************************\n";

    cout << ValueID_Long_Count << " long values:\n";
    for (int i=0; i<ValueID_Long_Count; i++)
        printValue(rawFile, ValueID_Long(i));

    cout << ValueID_Double_Count << " double values:\n";
    for (int i=0; i<ValueID_Double_Count; i++)
        printValue(rawFile, ValueID_Double(i));

    cout << ValueID_String_Count << " string values:\n";
    for (int i=0; i<ValueID_String_Count; i++)
        printValue(rawFile, ValueID_String(i));
}


void testFilters(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "FILTERS\n";
    cout << "*************************************************************\n";

    auto_ptr<StringArray> sa = rawFile->getFilters();
    for (int i=0; i<sa->size(); i++)
        cout << i << ": " << sa->item(i) << endl;
}


void testMassList(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "MASS LISTS\n";
    cout << "*************************************************************\n";

    auto_ptr<MassList> massList = rawFile->getMassList(2,
                                                       "",
                                                       Cutoff_None,
                                                       0,
                                                       0,
                                                       false);
    cout << "scanNumber: " << massList->scanNumber() << endl;
    cout << "size: " << massList->size() << endl;
    cout << "centroidPeakWidth: " << massList->centroidPeakWidth() << endl;
    MassIntensityPair* mip = massList->data();
    for (int i=0; i<20; i++)
        cout << mip[i].mass << " " << mip[i].intensity << endl;
    for (int i=0; i<3; i++)
        cout << ".\n";
    for (int i=massList->size()-20; i<massList->size(); i++)
        cout << mip[i].mass << " " << mip[i].intensity << endl;
}


void testRTConversions(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "RT CONVERSION TEST\n";
    cout << "*************************************************************\n";

    cout << "Start time: " << rawFile->value(StartTime) << endl;
    cout << "End time: " << rawFile->value(EndTime) << endl;
    cout << endl;

    cout << "scanNumber -> RT:\n";
    for (int i=1; i<=15; i++)
        cout << i << " -> " << rawFile->rt(i) << endl;

    cout << "\nRT -> scanNumber\n";
    double d = rawFile->rt(1);
    for (int i=0; i<=10; i++, d+=.01)
        cout << d << " -> " << rawFile->scanNumber(d) << endl;
}


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
    
    cout << "parentCount: " << scanInfo->parentCount() << endl;
    for (int i=0; i<scanInfo->parentCount(); i++)
        cout << "  <" << scanInfo->parentMass(i) << ", " << scanInfo->parentEnergy(i) << ">\n";

    if (outputLogs)
    {
        cout << "\nStatus Log (" << scanInfo->statusLogSize() << " entries, RT: " << scanInfo->statusLogRT() << "):\n";
        for (int i=0; i<scanInfo->statusLogSize(); i++)
            cout << i << ": " << scanInfo->statusLogLabel(i) << " " << scanInfo->statusLogValue(i) << endl;

        cout << "\nTrailer Extra (" << scanInfo->trailerExtraSize() << " entries):\n";
        for (int i=0; i<scanInfo->trailerExtraSize(); i++)
            cout << i << ": " << scanInfo->trailerExtraLabel(i) << " " << scanInfo->trailerExtraValue(i) << endl;
    }

    cout << endl;
}


void testScanInfo(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "SCAN INFO\n";
    cout << "*************************************************************\n";

	long scanCount = rawFile->value(raw::NumSpectra);
	cout << "Scan count: " << scanCount << endl;

	for (int i=1; i<=scanCount; i++)
		printScanInfo(rawFile, i, true);
}


void testSeqRow(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "SEQUENCE ROW\n";
    cout << "*************************************************************\n";

    auto_ptr<LabelValueArray> info = rawFile->getSequenceRowUserInfo();
    for (int i=0; i<info->size(); i++)
        cout << info->label(i) << ": " << info->value(i) << endl;
}


const char* controllerTypeText(ControllerType type)
{
    switch(type)
    {
        case Controller_None:
            return "Controller_None";
        case Controller_MS:
            return "Controller_MS";
        case Controller_Analog:
            return "Controller_Analog";
        case Controller_ADCard:
            return "Controller_ADCard";
        case Controller_PDA:
            return "Controller_PDA";
        case Controller_UV:
            return "Controller_UV";
        default:
            return "booger";
    }
}


void testControllers(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "CONTROLLERS\n";
    cout << "*************************************************************\n";

    cout << "Controllers:\n";
    long count = rawFile->value(raw::NumberOfControllers);
    for (int i=0; i<count; i++)
        cout << i << ": " << controllerTypeText(rawFile->getControllerType(i)) << endl;

    ControllerInfo info = rawFile->getCurrentController();
    cout << "Current Controller: ";
        cout << controllerTypeText(info.type) << " " << info.controllerNumber << endl;
}


void testErrorLog(RawFilePtr& rawFile)
{
    // TODO: test with a RAW file with Error Log Items!!

    cout << "*************************************************************\n";
    cout << "ERROR LOG\n";
    cout << "*************************************************************\n";

    long count = rawFile->value(raw::NumErrorLog);
    cout << "Error Log (" << count << " items):\n";
    for (int i=0; i<count; i++)
    {
        ErrorLogItem item = rawFile->getErrorLogItem(i);
        cout << item.rt << ": " << item.errorMessage << endl;
    }
}


void testTuneData(RawFilePtr& rawFile)
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


void testInstrumentMethods(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "INSTRUMENT METHODS\n";
    cout << "*************************************************************\n";

    auto_ptr<LabelValueArray> a(rawFile->getInstrumentMethods());
    for (int i=0; i<a->size(); i++)
    {
        cout << "METHOD " << i << ": " << a->label(i) << endl;
        cout << a->value(i) << endl;
    }

    auto_ptr<StringArray> b(rawFile->getInstrumentChannelLabels());
    cout << "Instrument Channel Labels (" << b->size() << " entries):\n";
    for (int i=0; i<b->size(); i++)
        cout << b->item(i) << endl;
}


void testChromatogramData(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "CHROMATOGRAM DATA\n";
    cout << "*************************************************************\n";

    auto_ptr<ChromatogramData> a(rawFile->getChromatogramData(1, Operator_None, 0,
                                                              "", "", "",
                                                              0, 0, 0,
                                                              Smoothing_None, 0));
    cout << "Chromatogram Data:\n";
    cout << "size: " << a->size() << endl;

    TimeIntensityPair* p = a->data();
    for (int i=0; i<20; i++, p++)
        cout << p->time << "\t" << p->intensity << endl;
}


void testAverageMassList(RawFilePtr& rawFile)
{
    cout << "*************************************************************\n";
    cout << "AVERAGE MASS LISTS\n";
    cout << "*************************************************************\n";

    cout << "Average Mass List:\n";
    auto_ptr<MassList> massList(rawFile->getAverageMassList(1, 10,
                                                     0, 0,
                                                     0, 0,
                                                     "",
                                                     Cutoff_None,
                                                     0,
                                                     0,
                                                     false));
    cout << "scanNumber: " << massList->scanNumber() << endl;
    cout << "size: " << massList->size() << endl;
    cout << "centroidPeakWidth: " << massList->centroidPeakWidth() << endl;
    MassIntensityPair* mip = massList->data();
    for (int i=0; i<20; i++)
        cout << mip[i].mass << " " << mip[i].intensity << endl;
    for (int i=0; i<3; i++)
        cout << ".\n";
    for (int i=massList->size()-20; i<massList->size(); i++)
        cout << mip[i].mass << " " << mip[i].intensity << endl;
}


void runTest(void (*f)(RawFilePtr&), RawFilePtr& rawFile)
{
    try
    {
        f(rawFile);
    }
    catch (RawEgg& egg)
    {
        cout << "Test aborted: " << egg.error() << endl;
    }
}


int test(RawFilePtr& rawfile, const vector<string>& args)
{
    try
    {
        cout << "File: " << rawfile->value(raw::FileName) << endl;
        cout << "Creation Date: " << rawfile->getCreationDate() << endl;

        runTest(testValues, rawfile);
        runTest(testFilters, rawfile);
        runTest(testMassList, rawfile);
        runTest(testRTConversions, rawfile);
        runTest(testScanInfo, rawfile);
        runTest(testSeqRow, rawfile);
        runTest(testControllers, rawfile);
        runTest(testErrorLog, rawfile);
        runTest(testTuneData, rawfile);
        runTest(testInstrumentMethods, rawfile);
        runTest(testChromatogramData, rawfile);
        runTest(testAverageMassList, rawfile);
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
    }

    cout << endl;
    return 0;
}


typedef int (*Command)(RawFilePtr& rawfile, const vector<string>& args);
map<string, Command> commands_;
string usage_;


int scaninfo(RawFilePtr& rawfile, const vector<string>& args)
{
    if (args.size() != 1)
        throw runtime_error(usage_);

    int scanNumber = atoi(args[0].c_str());

    try
    {
        cout << "File: " << rawfile->value(raw::FileName) << endl;
        cout << "Creation Date: " << rawfile->getCreationDate() << endl;
    
        cout << endl;

        printScanInfo(rawfile, scanNumber, true);
        return 0;
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
        return 1;
    }
}


int scandata(RawFilePtr& rawfile, const vector<string>& args)
{
    if (args.size()<1 || args.size()>2)
        throw runtime_error(usage_);

    int scanNumber = atoi(args[0].c_str());
	bool centroid = args.size()==2 && args[1]=="centroid" ? true : false;

	cout << "# " << rawfile->value(raw::FileName) << endl;
	cout << "# Scan " << scanNumber << endl;

	auto_ptr<MassList> massList = rawfile->getMassList(scanNumber,"", raw::Cutoff_None, 0, 0, centroid);

	int massCount = massList->size();
	cout << "# mass count: " << massCount << endl;
	if (centroid) cout << "# centroid\n";
	cout << "#\n";

	cout.precision(10);

	MassIntensityPair* p = massList->data();
	for (int i=0; i<massCount; i++, p++)
		cout << p->mass << " " << p->intensity << endl;

    return 0;
}


int trailer(RawFilePtr& rawfile, const vector<string>& args)
{
    if (args.size() != 2)
        throw runtime_error(usage_);
    
    int scanNumber = atoi(args[0].c_str());
    const string& name = args[1];

    auto_ptr<ScanInfo> scanInfo = rawfile->getScanInfo(scanNumber);
    string value = scanInfo->trailerExtraValue(name);

    if (!value.empty())
        cout << value << endl;
    else
        cout << "<no value>\n";

    // if we know it's a double and need the precison, use:
    //   scanInfo->trailerExtraValueDouble(name)

    return 0; 
}


double highMassNearTargetInPreviousScan(RawFilePtr& rawfile, 
                                        int scanNumber, 
                                        double massTarget,
                                        string filterPrefix)
{
    auto_ptr<MassList> massList = rawfile->getMassList(scanNumber, 
                                                       filterPrefix + " !d",
                                                       Cutoff_None, 0, 0, false,
                                                       MassList_Previous);
    double maxIntensity = 0;
    double maxMass = 0;
    MassIntensityPair* p = massList->data();

    for (int i=0; i<massList->size(); i++, p++)
    {
        if (p->mass < massTarget-.05)
            continue;

        if (p->mass > massTarget+.05)
            break;
    
        if (maxIntensity < p->intensity)
        {
            maxIntensity = p->intensity;
            maxMass = p->mass;
        }
    }

    return maxMass;
}


double calculateMean(const vector<double>& values)
{
    if (values.empty()) return 0;
    return accumulate(values.begin(), values.end(), 0.)/values.size();
}


double calculateRange(const vector<double>& values)
{
    if (values.empty()) return 0;

    double min = 100000.;
    double max = 0;

    for (vector<double>::const_iterator it=values.begin(); it!=values.end(); ++it)
    {
        if (min > *it) min = *it;
        if (max < *it) max = *it;
    }

    return max-min;
}


int precursors(RawFilePtr& rawfile, const vector<string>& args)
{
    cout << "#scan type ms  z  filter   trailer      itMax       ftMax       mean     range\n";

    double maxRange = 0;

	int scanCount = rawfile->value(raw::NumSpectra);
    for (int scanNumber=1; scanNumber<=scanCount; scanNumber++)
    {
        auto_ptr<ScanInfo> scanInfo = rawfile->getScanInfo(scanNumber);
        
        MassAnalyzerType massAnalyzerType = scanInfo->massAnalyzerType(); 
        int msLevel = scanInfo->msLevel();
        if (msLevel == 1) continue;

        double filterParentMass = scanInfo->parentCount() ? scanInfo->parentMass(0) : 0;
        double trailerParentMass = scanInfo->trailerExtraValueDouble("Monoisotopic M/Z:");
        int chargeState = atoi(scanInfo->trailerExtraValue("Charge State:").c_str());

        double itParentMass = filterParentMass ? 
            highMassNearTargetInPreviousScan(rawfile, scanNumber, filterParentMass, "ITMS") : 0;  

        double ftParentMass = filterParentMass ? 
            highMassNearTargetInPreviousScan(rawfile, scanNumber, filterParentMass, "FTMS") : 0;  

        vector<double> values;
        if (filterParentMass) values.push_back(filterParentMass);
        if (trailerParentMass) values.push_back(trailerParentMass);
        if (itParentMass) values.push_back(itParentMass);
        if (ftParentMass) values.push_back(ftParentMass);

        double mean = calculateMean(values);
        double range = calculateRange(values);
        if (maxRange < range) maxRange = range;

        cout << setw(5) << scanNumber << " "
             << toString(massAnalyzerType) << " "
             << "ms" << msLevel << " "
             << chargeState << " "
             << fixed << setprecision(2) << setw(7) << filterParentMass << " "
             << fixed << setprecision(6) << setw(11) << trailerParentMass << " "
             << fixed << setprecision(6) << setw(11) << itParentMass << " "
             << fixed << setprecision(6) << setw(11) << ftParentMass << " "
             << fixed << setprecision(6) << setw(11) << mean << " "
             << fixed << setprecision(3) << setw(5) << range << " "
             << endl;
    }

    cout << "max range: " << maxRange << endl;

    return 0;
}


int readbacks(RawFilePtr& rawfile, const vector<string>& args)
{
    const string delim = !args.empty()&&args[0]=="usv" ? "_" : "\t";

    cout << "scanNumber" << delim
         << "scanEvent" << delim
         << "massAnalyzer" << delim
         << "msLevel" << delim
         << "retentionTime" << delim;

	int scanCount = rawfile->value(raw::NumSpectra);
    for (int scanNumber=1; scanNumber<=scanCount; scanNumber++)
    {
        auto_ptr<ScanInfo> scanInfo = rawfile->getScanInfo(scanNumber);

        if (scanNumber == 1)
        {
            // finish header line

            for (int i=0; i<scanInfo->statusLogSize(); i++)
                cout << scanInfo->statusLogLabel(i) << delim;

            for (int i=0; i<scanInfo->trailerExtraSize(); i++)
                cout << scanInfo->trailerExtraLabel(i) << delim;

            cout << endl;

            // second header line         

            for (int i=0; i<5; i++)
                cout << delim;

            for (int i=0; i<scanInfo->statusLogSize(); i++)
                cout << "status " << i << delim;

            for (int i=0; i<scanInfo->trailerExtraSize(); i++)
                cout << "trailer " << i << delim;
        
            cout << endl;
        }
        
        int scanEvent = atoi(scanInfo->trailerExtraValue("Scan Event:").c_str());  
        double rt = scanInfo->startTime();
        string anal = toString(scanInfo->massAnalyzerType());
        int msLevel = scanInfo->msLevel();

        cout << setprecision(4) << fixed 
             << scanNumber << delim
             << scanEvent << delim
             << anal << delim 
             << "ms" << msLevel << delim 
             << rt << delim; 

        for (int i=0; i<scanInfo->statusLogSize(); i++)
            cout << scanInfo->statusLogValue(i) << delim; 

        for (int i=0; i<scanInfo->trailerExtraSize(); i++)
            cout << scanInfo->trailerExtraValue(i) << delim;
    
        cout << endl;
     }

    return 0;
}


void initialize()
{
    usage_ += "Usage: rawdump filename command [args]\n";
    usage_ += "Commands:\n";

    commands_["scaninfo"] = scaninfo;
    usage_ += "    scaninfo N (output info for scan N)\n";

    commands_["scandata"] = scandata;
    usage_ += "    scandata N [centroid] (output data for scan N)\n";

    commands_["trailer"] = trailer;
    usage_ += "    trailer N name (output trailer extra value for specified name in scan N)\n";

    commands_["precursors"] = precursors; 
    usage_ += "    precursors (outputs table with precursor info)\n";

    commands_["readbacks"] = readbacks; 
    usage_ += "    readbacks (outputs readbacks table)\n";

    commands_["test"] = test;
    usage_ += "    test (dumps a bunch of info, for testing RawFile.dll)";
}


int main(int argc, char* argv[])
{
    try 
    {
        initialize();

        if (argc < 3)
            throw runtime_error(usage_);

        const string& filename = argv[1];
        const string& commandName = argv[2];    

        Command command = commands_[commandName];
        if (!command)
            throw runtime_error(usage_);

        vector<string> args;
        copy(argv+3, argv+argc, back_inserter(args));

        RawFileLibrary library;

        RawFilePtr rawfile(filename);
        rawfile->setCurrentController(Controller_MS, 1);

        return command(rawfile, args);
    }
    catch (RawEgg& egg)
    {
        cout << egg.error() << endl;
        return 1;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cout << "Unknown error.\n";
        return 1;
    }
}

