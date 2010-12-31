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


#include "Serializer_pepXML.hpp"
#include "Diff.hpp"
#include "References.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "TextWriter.hpp"
#include <cstring>


using namespace pwiz::mziddata;
using namespace pwiz::mziddata::examples;
using namespace pwiz::util;

ostream* os_ = 0;

void stripUnmappedMetadata(MzIdentML& mzid)
{
    mzid.bibliographicReference.clear();
    mzid.analysisSampleCollection.samples.clear();
    mzid.auditCollection.clear();
    mzid.provider = Provider();
    mzid.dataCollection.inputs.sourceFile.clear();

    // TODO: add serialization of the analysis_summary element
    mzid.analysisSoftwareList[0]->version.clear();
    mzid.analysisCollection.spectrumIdentification[0]->activityDate.clear();

    // HACK: fix the enzyme mapping!
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->enzymes.enzymes[0]->enzymeName.clear();

    // pepXML doesn't map these attributes
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->version.clear();
    mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->releaseDate.clear();

    // pepXML doesn't reliably store location or file format
    string& location = mzid.analysisCollection.spectrumIdentification[0]->inputSpectra[0]->location;
    location = bfs::path(location).replace_extension("").filename();
    mzid.analysisCollection.spectrumIdentification[0]->inputSpectra[0]->fileFormat.clear();

    string& location2 = mzid.analysisCollection.spectrumIdentification[0]->searchDatabase[0]->location;
    location2 = bfs::path(location2).replace_extension("").filename();

    // pepXML doesn't support protein sequences
    BOOST_FOREACH(DBSequencePtr& dbSequence, mzid.sequenceCollection.dbSequences)
    {
        dbSequence->seq.clear();
        dbSequence->length = 0;
        dbSequence->id = "DBSeq_" + dbSequence->accession;
    }

    // pepXML can only support one mass type (we pick the max mass in case one of them is 0)
    BOOST_FOREACH(PeptidePtr& peptide, mzid.sequenceCollection.peptides)
    BOOST_FOREACH(ModificationPtr& mod, peptide->modification)
        mod->monoisotopicMassDelta = mod->avgMassDelta = max(mod->monoisotopicMassDelta, mod->avgMassDelta);

    BOOST_FOREACH(SpectrumIdentificationResultPtr& sir, mzid.dataCollection.analysisData.spectrumIdentificationList[0]->spectrumIdentificationResult)
    BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
    for (size_t i=0; i < sii->peptideEvidence.size(); ++i)
    {
        PeptideEvidence& pe = *sii->peptideEvidence[i];

        // pepXML does not store peptide start and end offsets
        pe.start = pe.end = 0;

        // pepXML's alternative_proteins do not store prev/next AA or missed cleavages
        if (i > 0)
        {
            pe.pre.clear();
            pe.post.clear();
            pe.missedCleavages = 0;
        }
    }
}

void testSerialize()
{
    cerr << "begin testSerialize\n";
    MzIdentML mzid;
    initializeBasicSpectrumIdentification(mzid);
    stripUnmappedMetadata(mzid);

    Serializer_pepXML serializer;
    ostringstream oss;
    serializer.write(oss, mzid, "tiny.pepXML");

    if (os_) *os_ << "oss:\n" << oss.str() << endl; 

    shared_ptr<istringstream> iss(new istringstream(oss.str()));
    MzIdentML mzid2;
    serializer.read(iss, mzid2);

    References::resolve(mzid2);

    Diff<MzIdentML, DiffConfig> diff(mzid, mzid2);
    if (os_ && diff) *os_ << diff << endl; 
    unit_assert(!diff);
}

void test()
{
    testSerialize();
}

int main(int argc, char** argv)
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
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}
