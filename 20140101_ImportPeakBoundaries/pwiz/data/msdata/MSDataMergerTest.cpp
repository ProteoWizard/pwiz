//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/unit.hpp"
#include "MSDataMerger.hpp"
#include "examples.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::msdata;
using namespace pwiz::util;


ostream* os_ = 0;


void test()
{
    MSData tinyReference;
    examples::initializeTiny(tinyReference);

    const size_t tinyCopyCount = 3;

    vector<MSDataPtr> tinyExamples;
    for (size_t i=0; i < tinyCopyCount; ++i)
    {
        tinyExamples.push_back(MSDataPtr(new MSData));
        MSData& msd = *tinyExamples.back();
        examples::initializeTiny(msd);
        msd.id = msd.run.id = "tiny" + lexical_cast<string>(i);
    }

    MSDataMerger tinyMerged(tinyExamples);

    if (os_)
    {
        TextWriter writer(*os_);
        writer(tinyMerged);
    }

    unit_assert(tinyMerged.id == "tiny"); // longest common prefix of tiny[012]
    unit_assert(tinyMerged.run.id == "tiny"); // longest common prefix of tiny[012]

    unit_assert(tinyMerged.fileDescription.fileContent.hasCVParam(MS_MSn_spectrum));
    unit_assert(tinyMerged.fileDescription.fileContent.hasCVParam(MS_centroid_spectrum));

    unit_assert(tinyMerged.fileDescription.sourceFilePtrs.size() == tinyReference.fileDescription.sourceFilePtrs.size() * tinyCopyCount);
    for (size_t i=0; i < tinyCopyCount; ++i)
        for (size_t j=0; j < tinyReference.fileDescription.sourceFilePtrs.size(); ++j)
        {
            string expectedPrefix = "tiny" + lexical_cast<string>(i) + "_";
            size_t expectedIndex = j + (i * tinyReference.fileDescription.sourceFilePtrs.size());
            unit_assert(tinyMerged.fileDescription.sourceFilePtrs[expectedIndex]->id == expectedPrefix + tinyReference.fileDescription.sourceFilePtrs[j]->id);
        }


    //unit_assert(tinyMerged.fileDescription.contacts.size() == tinyReference.fileDescription.contacts.size());
    SpectrumList& sl = *tinyMerged.run.spectrumListPtr;
    unit_assert(sl.size() == 3 * tinyReference.run.spectrumListPtr->size());
    for (size_t index=0; index < sl.size(); ++index)
    {
        size_t referenceIndex = index % tinyReference.run.spectrumListPtr->size();

        const SpectrumIdentity& identity = sl.spectrumIdentity(index);
        const SpectrumIdentity& referenceIdentity = tinyReference.run.spectrumListPtr->spectrumIdentity(referenceIndex);

        unit_assert(identity.index == index);
        unit_assert(identity.id == referenceIdentity.id);

        SpectrumPtr spectrum = sl.spectrum(index);
        SpectrumPtr referenceSpectrum = tinyReference.run.spectrumListPtr->spectrum(referenceIndex);

        unit_assert(spectrum->index == index);
        unit_assert(spectrum->id == referenceSpectrum->id);

        vector<SourceFilePtr>::const_iterator foundSourceFile = find(tinyMerged.fileDescription.sourceFilePtrs.begin(),
                                                                     tinyMerged.fileDescription.sourceFilePtrs.end(),
                                                                     spectrum->sourceFilePtr);

        unit_assert(foundSourceFile != tinyMerged.fileDescription.sourceFilePtrs.end());
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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
