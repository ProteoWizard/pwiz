//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "SpectrumIterator.hpp"
#include "MSData.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;


ostream* os_ = 0;


void initializeSpectrumList(SpectrumListSimple& spectrumList)
{
    // initialize with scans:
    // scan  0: IT
    // scan  5: FT (1,100)
    // scan 10: IT (1,100), (2,200)
    // scan 15: FT (1,100), (2,200), (3,300)
    // scan 20: IT (1,100), (2,200), (3,300), (4,400)
    // ...

    for (int i=0; i<=10; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->id = lexical_cast<string>(i*5);
        
        spectrum->cvParams.push_back(i%2 ? 
            MS_FT_ICR :
            MS_ion_trap);

        BinaryDataArrayPtr bdMZ(new BinaryDataArray);
        bdMZ->cvParams.push_back(MS_m_z_array);
        spectrum->binaryDataArrayPtrs.push_back(bdMZ);

        BinaryDataArrayPtr bdIntensity(new BinaryDataArray);
        bdIntensity->cvParams.push_back(MS_intensity_array);
        spectrum->binaryDataArrayPtrs.push_back(bdIntensity);

        for (int j=1; j<=i; j++)
        {
            bdMZ->data.push_back(j);
            bdIntensity->data.push_back(100*j);
        }

        spectrum->defaultArrayLength = i;
        spectrumList.spectra.push_back(spectrum);
    }
}


const char* anal(const CVParam& cvParam)
{
    if (cvParam == MS_FT_ICR)
        return "FT";
    else if (cvParam == MS_ion_trap)
        return "IT";
    else 
        return "Unknown";
}


void printSpectrumList(ostream& os, const SpectrumList& sl)
{
    if (os_) *os_ << "printSpectrumList()\n";

    for (unsigned int i=0; i<sl.size(); i++)
    {
        SpectrumPtr spectrum = sl.spectrum(i);
        os << spectrum->id << " "
           << anal(spectrum->cvParamChild(MS_mass_analyzer)) << endl;

        vector<MZIntensityPair> mziPairs;
        spectrum->getMZIntensityPairs(mziPairs);
        copy(mziPairs.begin(), mziPairs.end(), ostream_iterator<MZIntensityPair>(os,""));
        os << endl;
    }
}


void testBasic(const SpectrumList& sl)
{
    if (os_) *os_ << "testBasic()\n";

    SpectrumIterator it(sl);

    unit_assert(it->id == "0");
    unit_assert((*it).cvParamChild(MS_mass_analyzer_type) == MS_ion_trap);
    unit_assert(it->binaryDataArrayPtrs.size() == 2);

    ++it; ++it; ++it; ++it; ++it; // advance to scan 5

    unit_assert(it->id == "25");
    unit_assert(it->cvParamChild(MS_mass_analyzer_type) == MS_FT_ICR);
    unit_assert(it->binaryDataArrayPtrs.size() == 2 &&
                it->binaryDataArrayPtrs[0]->data.size() == 5);
}


void doSomething(const Spectrum& spectrum)
{
    if (os_) *os_ << "spectrum: " << spectrum.id << " "
                  << anal(spectrum.cvParamChild(MS_mass_analyzer)) << endl; 
    
    vector<MZIntensityPair> pairs;
    spectrum.getMZIntensityPairs(pairs);

    if (os_)
    {
        copy(pairs.begin(), pairs.end(), ostream_iterator<MZIntensityPair>(*os_,"")); 
        *os_ << endl;
    }
  
    unit_assert((int)pairs.size()*5 == lexical_cast<int>(spectrum.id));
}


void testForEach(const SpectrumList& spectrumList)
{
    if (os_) *os_ << "testForEach(): \n";
    for_each(SpectrumIterator(spectrumList), SpectrumIterator(), doSomething);
}


void testIntegerSet(const SpectrumList& spectrumList)
{
    // iterate through even scan numbers 

    if (os_) *os_ << "testIntegerSet():\n";

    IntegerSet scanNumbers;
    for (int i=2; i<=50; i+=2) // note that some scan numbers don't exist in spectrumList 
        scanNumbers.insert(i);
    
    // loop written for illustration
    // note automatic conversion from IntegerSet to SpectrumIterator::Config
    for (SpectrumIterator it(spectrumList, scanNumbers); it!=SpectrumIterator(); ++it)
        doSomething(*it); 

    // using for_each: 
    for_each(SpectrumIterator(spectrumList, scanNumbers), SpectrumIterator(), doSomething);
}


inline int getScanNumber(const Spectrum& spectrum) 
{
    return lexical_cast<int>(spectrum.id);
}


class FTSieve : public SpectrumIterator::Sieve
{
    public:
    virtual bool accept(const Spectrum& spectrum) const 
    {
        return (spectrum.cvParamChild(MS_mass_analyzer_type) == MS_FT_ICR);
    }
};


void testSieve(const SpectrumList& spectrumList)
{
    vector<int> ftScanNumbers;

    FTSieve sieve;
    SpectrumIterator::Config config(sieve, false);

    transform(SpectrumIterator(spectrumList, config),
              SpectrumIterator(), 
              back_inserter(ftScanNumbers), 
              getScanNumber);

    if (os_)
    {
        *os_ << "testSieve():\n"; 
        copy(ftScanNumbers.begin(), ftScanNumbers.end(), ostream_iterator<int>(*os_, " "));
        *os_ << endl;
    }

    unit_assert(ftScanNumbers.size() == 5);
    unit_assert(ftScanNumbers[0] == 5);
    unit_assert(ftScanNumbers[1] == 15);
    unit_assert(ftScanNumbers[2] == 25);
    unit_assert(ftScanNumbers[3] == 35);
    unit_assert(ftScanNumbers[4] == 45);
}


void testIteratorEquality(const SpectrumList& spectrumList)
{
    if (os_) *os_ << "testIteratorEquality()\n";

    SpectrumIterator it(spectrumList);
    ++it; ++it; ++it;

    SpectrumIterator jt(spectrumList);
    unit_assert(it!=jt);
    ++jt;
    unit_assert(it!=jt);
    ++jt;
    unit_assert(it!=jt);
    ++jt;
    unit_assert(it==jt);
}


void testMSDataConstruction()
{
    if (os_) *os_ << "testMSDataConstruction()\n";

    SpectrumListSimplePtr sl(new SpectrumListSimple());
    initializeSpectrumList(*sl);

    MSData msd;
    msd.run.spectrumListPtr = sl; 

    int i = 0;
    FTSieve sieve;
    for (SpectrumIterator it(msd, sieve); it!=SpectrumIterator(); ++it, ++i)
    {
        if (os_) *os_ << it->id << " "
                      << anal(it->cvParamChild(MS_mass_analyzer)) << endl;

        unit_assert(it->id == lexical_cast<string>(5+i*10));
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;

        SpectrumListSimple spectrumList;
        initializeSpectrumList(spectrumList);
        if (os_) printSpectrumList(*os_, spectrumList);

        testBasic(spectrumList);
        testForEach(spectrumList);
        testIntegerSet(spectrumList);
        testSieve(spectrumList);
        testIteratorEquality(spectrumList);
        testMSDataConstruction();

    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}


