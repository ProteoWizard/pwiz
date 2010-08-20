//
// $Id$
//
// Robert Burke <robert.burke@cshs.org>
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


#include <iostream>
#include "MuckedMSRun.hpp"

namespace pwiz {
namespace msmucker {

using namespace std;
using namespace pwiz::msrun;

////////////////////////////////////////////////////////////////////////////
// class MuckedMSRun methods

const std::string& MuckedMSRun::filename() const
{
    return msr->filename();
}

double MuckedMSRun::startTime() const
{
    return msr->startTime();
}

double MuckedMSRun::endTime() const
{
    return msr->endTime();
}

long MuckedMSRun::instrumentCount() const
{
    return msr->instrumentCount();
}

const Instrument* MuckedMSRun::instrument(long index) const
{
    return msr->instrument(index);
}

const DataProcessing* MuckedMSRun::dataProcessing() const
{
    return msr->dataProcessing();
}

long MuckedMSRun::scanCount() const
{
    std::cout << msr->scanCount() << " scanCount\n";
    return msr->scanCount();
}

long MuckedMSRun::msLevel(long scanNumber) const
{
    return msr->msLevel(scanNumber);
}

std::auto_ptr<Scan> MuckedMSRun::scan(long scanNumber) const
{
    if (scanNumber != muckedScanNumber)
        return msr->scan(scanNumber);

    auto_ptr<Scan> muckedScan(new MuckedScan(msr->scan(scanNumber), amount));
    
    return muckedScan;
}

////////////////////////////////////////////////////////////////////////////
// class MuckedScan methods

MuckedScan::MuckedScan(std::auto_ptr< pwiz::msrun::Scan > scan,
                       double muckAmount)
    : scan(scan), muckAmount(muckAmount)
{
    if (this->scan.get() != NULL && this->scan->peaks() != NULL)
        mps = auto_ptr<MuckedPeaks>(new MuckedPeaks(this->scan->peaks(),
                                                    muckAmount));
}

long MuckedScan::scanNumber() const
{
    return scan->scanNumber();
}

long MuckedScan::scanEvent() const
{
    return scan->scanEvent();
}

long MuckedScan::msLevel() const
{
    return scan->msLevel();
}

long MuckedScan::peakCount() const
{
    return scan->peakCount();
}

std::string MuckedScan::polarity() const
{
    return scan->polarity();
}

std::string MuckedScan::scanType() const
{
    return scan->scanType();
}

double MuckedScan::retentionTime() const
{
    return scan->retentionTime();
}

double MuckedScan::mzLow() const
{
    return scan->mzLow();
}

double MuckedScan::mzHigh() const
{
    return scan->mzHigh();
}

double MuckedScan::basePeakMZ() const
{
    return scan->basePeakMZ();
}

double MuckedScan::basePeakIntensity() const
{
    return scan->basePeakIntensity();
}

double MuckedScan::totalIonCurrent() const
{
    return scan->totalIonCurrent();
}

long MuckedScan::instrumentID() const
{
    return scan->instrumentID();
}

Instrument::Type MuckedScan::instrumentType() const
{
    return scan->instrumentType();
}

const Peaks* MuckedScan::peaks() const
{
    return mps.get();
}

long MuckedScan::precursorCount() const
{
    return scan->precursorCount();
}

const Precursor* MuckedScan::precursor(long index) const
{
    return scan->precursor(index);
}

MuckedPeaks::MuckedPeaks(const Peaks* peaks, double muckAmount)
        : localPeaks(peaks), muckAmount(muckAmount)
{
    cout << "\nIn MuckedPeaks constructor" << endl;
    int nPairs = peaks->count();
    localPairs = new MZIntensityPair[nPairs];
    const MZIntensityPair* pristinePairs = peaks->data();

    cout << "\tMucking up scan by " << muckAmount << endl;
    for (int i=0; i<nPairs; i++)
    {
        localPairs[i].mz = pristinePairs[i].mz;
        localPairs[i].intensity = pristinePairs[i].intensity + muckAmount;

	cout << "m/z: " << pristinePairs[i].mz << " -> " << localPairs[i].mz 
		<< ", intensity: " << pristinePairs[i].intensity
		<< " -> " << localPairs[i].intensity
		<< endl;
    }
}

long MuckedPeaks::count() const
{
    return localPeaks->count();
}

const MZIntensityPair* MuckedPeaks::data() const
{
    cout << "\tReturning locally mucked data." << endl;
    return localPairs;
}

} // namespace msmucker
} // namespace pwiz
