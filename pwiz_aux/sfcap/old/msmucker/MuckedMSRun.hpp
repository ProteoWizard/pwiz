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


#ifndef MUCKEDMSRUN_HPP
#define MUCKEDMSRUN_HPP

#include <vector>
#include <string>

#include "msrun/MSRun.hpp"

namespace pwiz {
namespace msmucker {

class MuckedPeaks : virtual public pwiz::msrun::Peaks
{
public:
    MuckedPeaks(const pwiz::msrun::Peaks* peaks, double muckAmount);

    virtual long count() const;
    virtual const pwiz::msrun::MZIntensityPair* data() const;

private:
    const pwiz::msrun::Peaks* localPeaks;
    pwiz::msrun::MZIntensityPair* localPairs;
    double muckAmount;
};

class MuckedScan : virtual public pwiz::msrun::Scan
{
public:
    MuckedScan(std::auto_ptr< pwiz::msrun::Scan > scan, double muckAmount);
    
    virtual long scanNumber() const;
    virtual long scanEvent() const;
    virtual long msLevel() const;
    virtual long peakCount() const;
    virtual std::string polarity() const;
    virtual std::string scanType() const;
    virtual double retentionTime() const;
    virtual double mzLow() const;
    virtual double mzHigh() const;
    virtual double basePeakMZ() const;
    virtual double basePeakIntensity() const;
    virtual double totalIonCurrent() const;
    virtual long instrumentID() const;
    virtual pwiz::msrun::Instrument::Type instrumentType() const;
    virtual const pwiz::msrun::Peaks* peaks() const;
    virtual long precursorCount() const;
    virtual const pwiz::msrun::Precursor* precursor(long index) const;

private:
    std::auto_ptr<pwiz::msrun::Scan> scan;
    double muckAmount;
    std::auto_ptr<MuckedPeaks> mps;
};

class MuckedMSRun : virtual public pwiz::msrun::MSRun
{
public:
    MuckedMSRun(std::auto_ptr<pwiz::msrun::MSRun> msr,
                int muckedScanNumber, double muckAmount)
        : msr(msr), muckedScanNumber(muckedScanNumber), amount(muckAmount)
    {
    }
    
    virtual const std::string& filename() const;
    virtual double startTime() const ;
    virtual double endTime() const;

    virtual long instrumentCount() const;
    virtual const pwiz::msrun::Instrument* instrument(long index) const;

    virtual const pwiz::msrun::DataProcessing* dataProcessing() const;

    virtual long scanCount() const;
    virtual std::auto_ptr<pwiz::msrun::Scan> scan(long scanNumber) const;
    virtual long msLevel(long scanNumber) const;

private:
    std::auto_ptr<pwiz::msrun::MSRun> msr;
    int muckedScanNumber;
    double amount;
};

} // namespace msmucker
} // namespace pwiz

#endif // MUCKEDMSRUN_HPP
