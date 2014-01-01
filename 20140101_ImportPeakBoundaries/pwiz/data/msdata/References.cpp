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

#define PWIZ_SOURCE

#include "References.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace msdata {
namespace References {




template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}

    bool operator()(const shared_ptr<object_type>& objectPtr)
    {
        return objectPtr.get() && objectPtr->id == id_;
    }
};


template <typename object_type>
void resolve(shared_ptr<object_type>& reference, 
             const vector< shared_ptr<object_type> >& referentList)
{
    if (!reference.get() || reference->id.empty())
        return; 

    typename vector< shared_ptr<object_type> >::const_iterator it = 
        find_if(referentList.begin(), referentList.end(), HasID<object_type>(reference->id));

    if (it == referentList.end())
    {
        ostringstream oss;
        oss << "[References::resolve()] Failed to resolve reference.\n"
            << "  object type: " << typeid(object_type).name() << endl
            << "  reference id: " << reference->id << endl
            << "  referent list: " << referentList.size() << endl;
        for (typename vector< shared_ptr<object_type> >::const_iterator it=referentList.begin();
             it!=referentList.end(); ++it)
            oss << "    " << (*it)->id << endl;
        throw runtime_error(oss.str().c_str());
    }

    reference = *it;
}


template <typename object_type>
void resolve(vector < shared_ptr<object_type> >& references,
             const vector< shared_ptr<object_type> >& referentList)
{
    for (typename vector< shared_ptr<object_type> >::iterator it=references.begin();
         it!=references.end(); ++it)
        resolve(*it, referentList);
}


PWIZ_API_DECL void resolve(ParamContainer& paramContainer, const MSData& msd)
{
    resolve(paramContainer.paramGroupPtrs, msd.paramGroupPtrs); 
}


template <typename object_type>
void resolve(vector<object_type>& objects, const MSData& msd)
{
    for (typename vector<object_type>::iterator it=objects.begin(); it!=objects.end(); ++it)
        resolve(*it, msd);
}


template <typename object_type>
void resolve(vector< shared_ptr<object_type> >& objectPtrs, const MSData& msd)
{
    for (typename vector< shared_ptr<object_type> >::iterator it=objectPtrs.begin(); 
         it!=objectPtrs.end(); ++it)
        resolve(**it, msd);
}


PWIZ_API_DECL void resolve(FileDescription& fileDescription, const MSData& msd)
{
    resolve(fileDescription.fileContent, msd);
    resolve(fileDescription.sourceFilePtrs, msd);
    resolve(fileDescription.contacts, msd);
}


PWIZ_API_DECL void resolve(ComponentList& componentList, const MSData& msd)
{
    for (size_t i=0; i < componentList.size(); ++i)
        resolve(componentList[i], msd); 
}


PWIZ_API_DECL void resolve(InstrumentConfiguration& instrumentConfiguration, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(instrumentConfiguration), msd);
    resolve(instrumentConfiguration.componentList, msd);
    resolve(instrumentConfiguration.softwarePtr, msd.softwarePtrs); 
}


PWIZ_API_DECL void resolve(ProcessingMethod& processingMethod, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(processingMethod), msd);
    resolve(processingMethod.softwarePtr, msd.softwarePtrs); 
}


PWIZ_API_DECL void resolve(DataProcessing& dataProcessing, const MSData& msd)
{
    resolve(dataProcessing.processingMethods, msd);
}


PWIZ_API_DECL void resolve(ScanSettings& scanSettings, const MSData& msd)
{
    resolve(scanSettings.sourceFilePtrs, msd.fileDescription.sourceFilePtrs);
    resolve(scanSettings.targets, msd);
}


PWIZ_API_DECL void resolve(Precursor& precursor, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(precursor), msd);
    resolve(precursor.sourceFilePtr, msd.fileDescription.sourceFilePtrs);
    resolve(precursor.isolationWindow, msd);
    resolve(precursor.selectedIons, msd);
    resolve(precursor.activation, msd);
}


PWIZ_API_DECL void resolve(Product& product, const MSData& msd)
{
    resolve(product.isolationWindow, msd);
}


PWIZ_API_DECL void resolve(Scan& scan, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(scan), msd);
    if (!scan.instrumentConfigurationPtr.get())
        scan.instrumentConfigurationPtr = msd.run.defaultInstrumentConfigurationPtr;
    resolve(scan.instrumentConfigurationPtr, msd.instrumentConfigurationPtrs);
    resolve(scan.scanWindows, msd);
}


PWIZ_API_DECL void resolve(ScanList& scanList, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(scanList), msd);
    resolve(scanList.scans, msd);
}


PWIZ_API_DECL void resolve(BinaryDataArray& binaryDataArray, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(binaryDataArray), msd);
    resolve(binaryDataArray.dataProcessingPtr, msd.dataProcessingPtrs);
}


PWIZ_API_DECL void resolve(Spectrum& spectrum, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(spectrum), msd);
    resolve(spectrum.dataProcessingPtr, msd.dataProcessingPtrs);
    resolve(spectrum.sourceFilePtr, msd.fileDescription.sourceFilePtrs);
    resolve(spectrum.scanList, msd);
    resolve(spectrum.precursors, msd);
    resolve(spectrum.products, msd);
    resolve(spectrum.binaryDataArrayPtrs, msd);
}


PWIZ_API_DECL void resolve(Chromatogram& chromatogram, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(chromatogram), msd);
    resolve(chromatogram.dataProcessingPtr, msd.dataProcessingPtrs);
    resolve(chromatogram.binaryDataArrayPtrs, msd);
}


PWIZ_API_DECL void resolve(Run& run, const MSData& msd)
{
    resolve(static_cast<ParamContainer&>(run), msd);
    resolve(run.defaultInstrumentConfigurationPtr, msd.instrumentConfigurationPtrs);
    resolve(run.samplePtr, msd.samplePtrs);
    resolve(run.defaultSourceFilePtr, msd.fileDescription.sourceFilePtrs);
}


PWIZ_API_DECL void resolve(MSData& msd)
{
    resolve(msd.paramGroupPtrs, msd);
    resolve(msd.samplePtrs, msd);
    resolve(msd.instrumentConfigurationPtrs, msd);
    resolve(msd.dataProcessingPtrs, msd);
    resolve(msd.scanSettingsPtrs, msd);
    resolve(msd.run, msd);

    // if we're using SpectrumListSimple, resolve the references in each Spectrum
    SpectrumListSimple* simple = dynamic_cast<SpectrumListSimple*>(msd.run.spectrumListPtr.get());
    if (simple)
        resolve(simple->spectra, msd);

    // if we're using ChromatogramListSimple, resolve the references in each Chromatogram
    ChromatogramListSimple* chromatogramListSimple = dynamic_cast<ChromatogramListSimple*>(msd.run.chromatogramListPtr.get());
    if (chromatogramListSimple)
        resolve(chromatogramListSimple->chromatograms, msd);
}


} // namespace References
} // namespace msdata
} // namespace pwiz

