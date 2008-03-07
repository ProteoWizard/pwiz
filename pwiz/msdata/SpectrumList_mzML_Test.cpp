//
// SpectrumList_mzML_Test.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SpectrumList_mzML.hpp"
#include "Serializer_mzML.hpp" // depends on Serializer_mzML::write() only
#include "examples.hpp"
#include "minimxml/XMLWriter.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using boost::shared_ptr;


ostream* os_ = 0;


void test(bool indexed)
{
    if (os_) *os_ << "test(): indexed=\"" << boolalpha << indexed << "\"\n";

    MSData tiny;
    examples::initializeTiny(tiny);

    Serializer_mzML::Config config;
    config.indexed = indexed;
    Serializer_mzML serializer(config);  

    ostringstream oss;
    serializer.write(oss, tiny);

    if (os_) *os_ << "oss:\n" << oss.str() << endl;

    shared_ptr<istream> is(new istringstream(oss.str()));

    // dummy would normally be read in from file
  
    MSData dummy;

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->cvParams.push_back(MS_positive_scan);
    pg1->cvParams.push_back(MS_full_scan);
    dummy.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->cvParams.push_back(MS_positive_scan);
    pg2->cvParams.push_back(MS_full_scan);
    dummy.paramGroupPtrs.push_back(pg2);

    // so we don't have any dangling references
    dummy.instrumentPtrs.push_back(InstrumentPtr(new Instrument("LCQ Deca")));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("Xcalibur Processing")));

    SpectrumListPtr sl = SpectrumList_mzML::create(is, dummy, indexed);

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 2);
    unit_assert(sl->find ("S19") == 0);
    unit_assert(sl->findNative("19") == 0);
    unit_assert(sl->find("S20") == 1);
    unit_assert(sl->findNative("20") == 1);

    // check scan 19

    SpectrumPtr s = sl->spectrum(0); // read without binary data
    unit_assert(s.get());
    unit_assert(s->id == "S19");
    unit_assert(s->nativeID == "19");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 1);
    unit_assert(s->binaryDataArrayPtrs.empty());

    unit_assert(sl->spectrumIdentity(0).index == 0);
    unit_assert(sl->spectrumIdentity(0).id == "S19");
    unit_assert(sl->spectrumIdentity(0).nativeID == "19");
 
    SpectrumPtr s_cache = sl->spectrum(0); // cache read
    unit_assert(s_cache.get() == s.get());

    s = sl->spectrum(0, true); // read with binary data
    unit_assert(s_cache.get() != s.get());

    vector<MZIntensityPair> pairs;
    s->getMZIntensityPairs(pairs);
    unit_assert(pairs.size() == 15);
    for (int i=0; i<15; i++)
        unit_assert(pairs[i].mz==i && pairs[i].intensity==15-i);

    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.size() == 1);
    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.back()->id == "CommonMS1SpectrumParams");
    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.back()->cvParams.size() == 2);

    // check scan 20

    s = sl->spectrum(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "S20");
    unit_assert(s->nativeID == "20");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);

    unit_assert(sl->spectrumIdentity(1).index == 1);
    unit_assert(sl->spectrumIdentity(1).id == "S20");
    unit_assert(sl->spectrumIdentity(1).nativeID == "20");

    pairs.clear();
    s->getMZIntensityPairs(pairs);
    unit_assert(pairs.size() == 10);
    for (int i=0; i<10; i++)
        unit_assert(pairs[i].mz==2*i && pairs[i].intensity==(10-i)*2);

    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.size() == 1);
    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.back()->id == "CommonMS2SpectrumParams");
    unit_assert(s->spectrumDescription.scan.paramGroupPtrs.back()->cvParams.size() == 2);
}


void test()
{
    bool indexed = true;
    test(indexed);

    indexed = false;
    test(indexed);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
}


