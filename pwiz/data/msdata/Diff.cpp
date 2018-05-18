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

#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


using namespace pwiz::msdata;


namespace pwiz {
namespace data {
namespace diff_impl {


PWIZ_API_DECL
void diff(const SourceFile& a, 
          const SourceFile& b, 
          SourceFile& a_b, 
          SourceFile& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.name, b.name, a_b.name, b_a.name, config);
    diff(a.location, b.location, a_b.location, b_a.location, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const FileDescription& a, 
          const FileDescription& b, 
          FileDescription& a_b, 
          FileDescription& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a.fileContent), b.fileContent, a_b.fileContent, b_a.fileContent, config);
    vector_diff_deep(a.sourceFilePtrs, b.sourceFilePtrs, a_b.sourceFilePtrs, b_a.sourceFilePtrs, config);
    vector_diff_diff<Contact>(a.contacts, b.contacts, a_b.contacts, b_a.contacts, config);
}


PWIZ_API_DECL
void diff(const Sample& a, 
          const Sample& b, 
          Sample& a_b, 
          Sample& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.name, b.name, a_b.name, b_a.name, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Component& a, 
          const Component& b, 
          Component& a_b, 
          Component& b_a,
          const DiffConfig& config)
{
    int a_bType, b_aType; // TODO: how to take the difference of enum types?
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff_integral(a.order, b.order, a_b.order, b_a.order, config);
    diff_integral((int)a.type, (int)b.type, a_bType, b_aType, config);
}


PWIZ_API_DECL
void diff(const ComponentList& a, 
          const ComponentList& b, 
          ComponentList& a_b, 
          ComponentList& b_a,
          const DiffConfig& config)
{
    //size_t a_bSize, b_aSize; // TODO: what to do with this?
    //diff_numeric(a.size(), b.size(), a_bSize, b_aSize, config);
    //for (size_t i=0; i < a.size(); ++i)
    //    diff(a[i], b[i], a_b[i], b_a[i], config);
    vector_diff_diff(static_cast<const vector<Component>&>(a),
                     static_cast<const vector<Component>&>(b),
                     static_cast<vector<Component>&>(a_b),
                     static_cast<vector<Component>&>(b_a),
                     config);
}


PWIZ_API_DECL
void diff(const Software& a, 
          const Software& b, 
          Software& a_b, 
          Software& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
	if (!config.ignoreVersions)
    diff(a.version, b.version, a_b.version, b_a.version, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const InstrumentConfiguration& a,
          const InstrumentConfiguration& b,
          InstrumentConfiguration& a_b,
          InstrumentConfiguration& b_a,
          const DiffConfig& config)
{
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.componentList, b.componentList, a_b.componentList, b_a.componentList, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const ProcessingMethod& a,
          const ProcessingMethod& b,
          ProcessingMethod& a_b,
          ProcessingMethod& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff_integral(a.order, b.order, a_b.order, b_a.order, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
}


PWIZ_API_DECL
void diff(const DataProcessing& a,
          const DataProcessing& b,
          DataProcessing& a_b,
          DataProcessing& b_a,
          const DiffConfig& config)
{
    diff(a.id, b.id, a_b.id, b_a.id, config);
    vector_diff_diff(a.processingMethods, b.processingMethods, a_b.processingMethods, b_a.processingMethods, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const ScanSettings& a,
          const ScanSettings& b,
          ScanSettings& a_b,
          ScanSettings& b_a,
          const DiffConfig& config)
{
    diff(a.id, b.id, a_b.id, b_a.id, config);
    vector_diff_deep(a.sourceFilePtrs, b.sourceFilePtrs, a_b.sourceFilePtrs, b_a.sourceFilePtrs, config);
    vector_diff_diff(a.targets, b.targets, a_b.targets, b_a.targets, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Precursor& a,
          const Precursor& b,
          Precursor& a_b,
          Precursor& b_a,
          const DiffConfig& config)
{
    a_b = Precursor();
    b_a = Precursor();

    // important scan metadata
    vector_diff_diff<SelectedIon>(a.selectedIons, b.selectedIons, a_b.selectedIons, b_a.selectedIons, config);

    if (!config.ignoreMetadata)
    {
        if (!config.ignoreIdentity)
            diff(a.spectrumID, b.spectrumID, a_b.spectrumID, b_a.spectrumID, config);
        diff(static_cast<const ParamContainer&>(a.isolationWindow), b.isolationWindow, a_b.isolationWindow, b_a.isolationWindow, config);
        diff(static_cast<const ParamContainer&>(a.activation), b.activation, a_b.activation, b_a.activation, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    }

    // provide spectrumID for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.spectrumID = a.spectrumID; 
        b_a.spectrumID = b.spectrumID; 
    }
}


PWIZ_API_DECL
void diff(const Product& a,
          const Product& b,
          Product& a_b,
          Product& b_a,
          const DiffConfig& config)
{
    a_b = Product();
    b_a = Product();

    if (!config.ignoreMetadata)
    {
        diff(static_cast<const ParamContainer&>(a.isolationWindow), b.isolationWindow, a_b.isolationWindow, b_a.isolationWindow, config);
    }
}


PWIZ_API_DECL
void diff(const Scan& a,
          const Scan& b,
          Scan& a_b,
          Scan& b_a,
          const DiffConfig& config)
{
    ptr_diff(a.instrumentConfigurationPtr, b.instrumentConfigurationPtr, a_b.instrumentConfigurationPtr, b_a.instrumentConfigurationPtr, config);
    vector_diff_diff(a.scanWindows, b.scanWindows, a_b.scanWindows, b_a.scanWindows, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);

    // provide instrumentConfigurationPtr for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.instrumentConfigurationPtr = a.instrumentConfigurationPtr; 
        b_a.instrumentConfigurationPtr = b.instrumentConfigurationPtr; 
    }
}


PWIZ_API_DECL
void diff(const ScanList& a,
          const ScanList& b,
          ScanList& a_b,
          ScanList& b_a,
          const DiffConfig& config)
{
    vector_diff_diff(a.scans, b.scans, a_b.scans, b_a.scans, config);
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


// measure maximum relative difference between elements in the vectors;
// returns the index and magnitude of the largest difference
pair<size_t, double> maxdiff(const vector<double>& a, const vector<double>& b)
{
    if (a.size() != b.size()) 
        throw runtime_error("[Diff::maxdiff()] Sizes differ.");

    vector<double>::const_iterator i = a.begin(); 
    vector<double>::const_iterator j = b.begin(); 

    pair<size_t, double> max;

    for (; i!=a.end(); ++i, ++j)
    {
        double denominator = min(*i, *j);
        if (denominator == 0) denominator = 1;
        double current = fabs(*i - *j)/denominator;
        if (max.second < current) max = make_pair(i-a.begin(), current);

    }

    return max;
}


const char* userParamName_BinaryDataArrayDifference_ = "Binary data array difference";
const char* userParamName_BinaryDataArrayDifferenceAtIndex_ = "Binary data array difference at index";

PWIZ_API_DECL
void diff(const BinaryDataArray& a,
          const BinaryDataArray& b,
          BinaryDataArray& a_b,
          BinaryDataArray& b_a,
          const DiffConfig& config)
{
    if (!config.ignoreMetadata)
    {
        ptr_diff(a.dataProcessingPtr, b.dataProcessingPtr, a_b.dataProcessingPtr, b_a.dataProcessingPtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    }

    if (a.data.size() != b.data.size())
    {
        a_b.userParams.push_back(UserParam("Binary data array size: " + 
                                           lexical_cast<string>(a.data.size())));
        b_a.userParams.push_back(UserParam("Binary data array size: " + 
                                           lexical_cast<string>(b.data.size())));
    }
    else
    {
        pair<size_t, double> max = maxdiff(a.data, b.data);
       
        if (max.second > config.precision + numeric_limits<double>::epsilon())
        {
            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(max.second),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());

            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifferenceAtIndex_,
                                               lexical_cast<string>(max.first),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());
        }
    }    
    
    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.cvParams = a.cvParams; 
        b_a.cvParams = b.cvParams; 
    }
}


PWIZ_API_DECL
void diff(const vector<BinaryDataArrayPtr>& a,
          const vector<BinaryDataArrayPtr>& b,
          vector<BinaryDataArrayPtr>& a_b,
          vector<BinaryDataArrayPtr>& b_a,
          const DiffConfig& config, pair<size_t, double>& maxPrecisionDiff)
{
    if (a.size() != b.size())
        throw runtime_error("[Diff::diff(vector<BinaryDataArrayPtr>)] Sizes differ.");

    a_b.clear();
    b_a.clear();

    for (vector<BinaryDataArrayPtr>::const_iterator i=a.begin(), j=b.begin();
         i!=a.end(); ++i, ++j)
    {
        BinaryDataArrayPtr temp_a_b(new BinaryDataArray);
        BinaryDataArrayPtr temp_b_a(new BinaryDataArray);
        diff(**i, **j, *temp_a_b, *temp_b_a, config); 

        if (!temp_a_b->empty() || !temp_b_a->empty())
        {
            a_b.push_back(temp_a_b);
            b_a.push_back(temp_b_a);

            //if UserParam with binary data diff exists, cast it to a double and compare with maxPrecisionDiff
            if(!temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).empty())
            {
                double max = lexical_cast<double>(temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).value);
                if (max>maxPrecisionDiff.second)
                {
                    size_t maxIndex = lexical_cast<size_t>(temp_a_b->userParam(userParamName_BinaryDataArrayDifferenceAtIndex_).value);
                    maxPrecisionDiff.first = maxIndex;
                    maxPrecisionDiff.second = max;
                }
            }
        }
    }
}

static void diff_index(const size_t& a, 
                   const size_t& b, 
                   size_t& a_b, 
                   size_t& b_a)
{
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
    else
    {
        a_b = IDENTITY_INDEX_NONE;
        b_a = IDENTITY_INDEX_NONE;
    }
}



PWIZ_API_DECL
void diff(const Spectrum& a,
          const Spectrum& b,
          Spectrum& a_b,
          Spectrum& b_a,
          const DiffConfig& config)
{
    a_b = Spectrum();
    b_a = Spectrum();

    if (!config.ignoreIdentity)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
        diff_index(a.index, b.index, a_b.index, b_a.index);
    }

    // important scan metadata
    diff_integral(a.defaultArrayLength, b.defaultArrayLength, a_b.defaultArrayLength, b_a.defaultArrayLength, config);
    vector_diff_diff(a.precursors, b.precursors, a_b.precursors, b_a.precursors, config);
    vector_diff_diff(a.products, b.products, a_b.products, b_a.products, config);

    if (!config.ignoreMetadata)
    {
        ptr_diff(a.dataProcessingPtr, b.dataProcessingPtr, a_b.dataProcessingPtr, b_a.dataProcessingPtr, config);
        ptr_diff(a.sourceFilePtr, b.sourceFilePtr, a_b.sourceFilePtr, b_a.sourceFilePtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
        diff(a.scanList, b.scanList, a_b.scanList, b_a.scanList, config);
    }

    // special handling for binary data arrays

    if ((!config.ignoreExtraBinaryDataArrays && a.binaryDataArrayPtrs.size() != b.binaryDataArrayPtrs.size()) ||
        (config.ignoreExtraBinaryDataArrays && (a.binaryDataArrayPtrs.size() < 2 || b.binaryDataArrayPtrs.size() < 2)))
    {
        a_b.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(a.binaryDataArrayPtrs.size())));
        b_a.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(b.binaryDataArrayPtrs.size())));
    }
    else
    {
        pair<size_t, double> maxPrecisionDiff(0, 0);
        if (config.ignoreExtraBinaryDataArrays)
        {
            // only check 2 primary arrays
            vector<BinaryDataArrayPtr> aBDA(a.binaryDataArrayPtrs.begin(), a.binaryDataArrayPtrs.begin() + 2);
            vector<BinaryDataArrayPtr> bBDA(b.binaryDataArrayPtrs.begin(), b.binaryDataArrayPtrs.begin() + 2);
            diff(aBDA, bBDA,
                 a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs,
                 config, maxPrecisionDiff);
        }
        else
        {
            diff(a.binaryDataArrayPtrs, b.binaryDataArrayPtrs, 
                 a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs,
                 config, maxPrecisionDiff);
        }
      
        if (maxPrecisionDiff.second>(config.precision+numeric_limits<double>::epsilon()))   
        {
            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(maxPrecisionDiff.second),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());

            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifferenceAtIndex_,
                                               lexical_cast<string>(maxPrecisionDiff.first),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());
        }
    }

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id;
        b_a.id = b.id;
        a_b.index = a.index;
        b_a.index = b.index;
    }
}


PWIZ_API_DECL
void diff(const Chromatogram& a,
          const Chromatogram& b,
          Chromatogram& a_b,
          Chromatogram& b_a,
          const DiffConfig& config)
{
    a_b = Chromatogram();
    b_a = Chromatogram();

    if (!config.ignoreIdentity)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
        diff_index(a.index, b.index, a_b.index, b_a.index);
    }

    // important scan metadata
    diff_integral(a.defaultArrayLength, b.defaultArrayLength, a_b.defaultArrayLength, b_a.defaultArrayLength, config);

    if (!config.ignoreMetadata)
    {
        ptr_diff(a.dataProcessingPtr, b.dataProcessingPtr, a_b.dataProcessingPtr, b_a.dataProcessingPtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
        diff(a.precursor, b.precursor, a_b.precursor, b_a.precursor, config);
        diff(a.product, b.product, a_b.product, b_a.product, config);
    }

    // special handling for binary data arrays

    if ((!config.ignoreExtraBinaryDataArrays && a.binaryDataArrayPtrs.size() != b.binaryDataArrayPtrs.size()) ||
        (config.ignoreExtraBinaryDataArrays && (a.binaryDataArrayPtrs.size() < 2 || b.binaryDataArrayPtrs.size() < 2)))
    {
        a_b.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(a.binaryDataArrayPtrs.size())));
        b_a.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(b.binaryDataArrayPtrs.size())));
    }
    else
    {
        pair<size_t, double> maxPrecisionDiff(0, 0);
        if (config.ignoreExtraBinaryDataArrays)
        {
            // only check 2 primary arrays
            vector<BinaryDataArrayPtr> aBDA(a.binaryDataArrayPtrs.begin(), a.binaryDataArrayPtrs.begin() + 2);
            vector<BinaryDataArrayPtr> bBDA(b.binaryDataArrayPtrs.begin(), b.binaryDataArrayPtrs.begin() + 2);
            diff(aBDA, bBDA,
                 a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs,
                 config, maxPrecisionDiff);
        }
        else
        {
            diff(a.binaryDataArrayPtrs, b.binaryDataArrayPtrs, 
                 a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs,
                 config, maxPrecisionDiff);
        }

        if (maxPrecisionDiff.second>(config.precision+numeric_limits<double>::epsilon()))   
        {
            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(maxPrecisionDiff.second),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());

            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifferenceAtIndex_,
                                               lexical_cast<string>(maxPrecisionDiff.first),
                                               "xsd:float"));
            b_a.userParams.push_back(a_b.userParams.back());
        }
    }

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id;
        b_a.id = b.id;
        a_b.index = a.index;
        b_a.index = b.index;
    }
}


static const char* userParamName_MaxBinaryDataArrayDifference_ = "Maximum binary data array difference";

PWIZ_API_DECL
void diff(const SpectrumList& a,
          const SpectrumList& b,
          SpectrumListSimple& a_b,
          SpectrumListSimple& b_a,
          const DiffConfig& config)
{
    a_b.spectra.clear();
    b_a.spectra.clear();
    a_b.dp = DataProcessingPtr();
    b_a.dp = DataProcessingPtr();

    DataProcessingPtr temp_a_dp(a.dataProcessingPtr().get() ? 
                                new DataProcessing(*a.dataProcessingPtr()) :
                                0); 

    DataProcessingPtr temp_b_dp(b.dataProcessingPtr().get() ? 
                                new DataProcessing(*b.dataProcessingPtr()) :
                                0); 

    if (!config.ignoreMetadata && !config.ignoreDataProcessing)
        ptr_diff(temp_a_dp, temp_b_dp, a_b.dp, b_a.dp, config);

    if (a.size() != b.size())

    {
        SpectrumPtr dummy(new Spectrum);
        dummy->userParams.push_back(UserParam("SpectrumList sizes differ"));
        a_b.spectra.push_back(dummy);
        return;
    }

    double maxPrecisionDiff = 0;
    for (unsigned int i=0; i<a.size(); i++)
    {
        SpectrumPtr a_spectrum = a.spectrum(i, true);
        SpectrumPtr b_spectrum = b.spectrum(i, true);
        SpectrumPtr temp_a_b(new Spectrum);        
        SpectrumPtr temp_b_a(new Spectrum);
        diff(*a_spectrum, *b_spectrum, *temp_a_b, *temp_b_a, config);

        // test find() if the ids are the same
        bool sameId = a_spectrum->id == b_spectrum->id;
        size_t a_find = config.ignoreIdentity || !sameId ? 0 : a.find(a_spectrum->id);
        size_t b_find = config.ignoreIdentity || !sameId ? 0 : b.find(b_spectrum->id);

        if (!temp_a_b->empty() || !temp_b_a->empty() || a_find != b_find)
        {
            a_b.spectra.push_back(temp_a_b);
            b_a.spectra.push_back(temp_b_a);

            if (a_find != b_find)
            {
                temp_a_b->index = a_spectrum->index;
                temp_a_b->id = a_spectrum->id;
                temp_b_a->index = b_spectrum->index;
                temp_b_a->id = b_spectrum->id;
                temp_a_b->userParams.push_back(UserParam("find() result", lexical_cast<string>(a_find)));
                temp_b_a->userParams.push_back(UserParam("find() result", lexical_cast<string>(b_find)));
            }

            if (!temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).empty())
            {
                double max=lexical_cast<double>(temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).value);
                if (max>maxPrecisionDiff) maxPrecisionDiff=max;
            }
        }
    }

    if (maxPrecisionDiff > 0)
    {
        if (!a_b.dp.get()) a_b.dp = DataProcessingPtr(new DataProcessing);
        if (a_b.dp->processingMethods.empty()) a_b.dp->processingMethods.push_back(ProcessingMethod());
        ProcessingMethod& listDiffMethod = a_b.dp->processingMethods.back();

        if (listDiffMethod.userParam(userParamName_MaxBinaryDataArrayDifference_).empty())
            listDiffMethod.userParams.push_back(UserParam(userParamName_MaxBinaryDataArrayDifference_, lexical_cast<string>(maxPrecisionDiff)));
        else
            listDiffMethod.userParam(userParamName_MaxBinaryDataArrayDifference_).value = lexical_cast<string>(maxPrecisionDiff);
    }
}


PWIZ_API_DECL
void diff(const ChromatogramList& a,
          const ChromatogramList& b,
          ChromatogramListSimple& a_b,
          ChromatogramListSimple& b_a,
          const DiffConfig& config)
{
    a_b.chromatograms.clear();
    b_a.chromatograms.clear();
    a_b.dp = DataProcessingPtr();
    b_a.dp = DataProcessingPtr();

    if (config.ignoreChromatograms) return;
    
    DataProcessingPtr temp_a_dp(a.dataProcessingPtr().get() ? 
                                new DataProcessing(*a.dataProcessingPtr()) :
                                0); 

    DataProcessingPtr temp_b_dp(b.dataProcessingPtr().get() ? 
                                new DataProcessing(*b.dataProcessingPtr()) :
                                0); 

    if (!config.ignoreMetadata && !config.ignoreDataProcessing)
        ptr_diff(temp_a_dp, temp_b_dp, a_b.dp, b_a.dp, config);

    if (a.size() != b.size())
    {
        ChromatogramPtr dummy(new Chromatogram);
        dummy->userParams.push_back(UserParam("ChromatogramList sizes differ"));
        a_b.chromatograms.push_back(dummy);
        return;
    }

    double maxPrecisionDiff = 0;
    for (unsigned int i=0; i<a.size(); i++)
    {
        ChromatogramPtr a_chromatogram = a.chromatogram(i, true);
        ChromatogramPtr b_chromatogram = b.chromatogram(i, true);
        ChromatogramPtr temp_a_b(new Chromatogram);        
        ChromatogramPtr temp_b_a(new Chromatogram);        
        diff(*a_chromatogram, *b_chromatogram, *temp_a_b, *temp_b_a, config);

        // test find() if the ids are the same
        bool sameId = a_chromatogram->id == b_chromatogram->id;
        size_t a_find = config.ignoreIdentity || !sameId ? 0 : a.find(a_chromatogram->id);
        size_t b_find = config.ignoreIdentity || !sameId ? 0 : b.find(b_chromatogram->id);

        if (!temp_a_b->empty() || !temp_b_a->empty() || a_find != b_find)
        {
            a_b.chromatograms.push_back(temp_a_b);
            b_a.chromatograms.push_back(temp_b_a);

            if (a_find != b_find)
            {
                temp_a_b->index = a_chromatogram->index;
                temp_a_b->id = a_chromatogram->id;
                temp_b_a->index = b_chromatogram->index;
                temp_b_a->id = b_chromatogram->id;
                temp_a_b->userParams.push_back(UserParam("find() result", lexical_cast<string>(a_find)));
                temp_b_a->userParams.push_back(UserParam("find() result", lexical_cast<string>(b_find)));
            }

            if (!temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).empty())
            {
                double max=lexical_cast<double>(temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).value);
                if(max>maxPrecisionDiff) maxPrecisionDiff=max;
            }
        }
    }

    if (maxPrecisionDiff > 0)
    {
        if (!a_b.dp.get()) a_b.dp = DataProcessingPtr(new DataProcessing);
        if (a_b.dp->processingMethods.empty()) a_b.dp->processingMethods.push_back(ProcessingMethod());
        ProcessingMethod& listDiffMethod = a_b.dp->processingMethods.back();

        if (listDiffMethod.userParam(userParamName_MaxBinaryDataArrayDifference_).empty())
            listDiffMethod.userParams.push_back(UserParam(userParamName_MaxBinaryDataArrayDifference_, lexical_cast<string>(maxPrecisionDiff)));
        else
            listDiffMethod.userParam(userParamName_MaxBinaryDataArrayDifference_).value = lexical_cast<string>(maxPrecisionDiff);
    }
}

PWIZ_API_DECL
void diff(const Run& a,
          const Run& b,
          Run& a_b,
          Run& b_a,
          const DiffConfig& config)
{
    if (!config.ignoreMetadata)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
        ptr_diff(a.defaultInstrumentConfigurationPtr, b.defaultInstrumentConfigurationPtr, a_b.defaultInstrumentConfigurationPtr, b_a.defaultInstrumentConfigurationPtr, config);
        ptr_diff(a.samplePtr, b.samplePtr, a_b.samplePtr, b_a.samplePtr, config);
        diff(a.startTimeStamp, b.startTimeStamp, a_b.startTimeStamp, b_a.startTimeStamp, config);
        ptr_diff(a.defaultSourceFilePtr, b.defaultSourceFilePtr, a_b.defaultSourceFilePtr, b_a.defaultSourceFilePtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    }

    if (!config.ignoreSpectra)
    {
        // special handling for SpectrumList diff
        shared_ptr<SpectrumListSimple> temp_a_b(new SpectrumListSimple); 
        shared_ptr<SpectrumListSimple> temp_b_a(new SpectrumListSimple);
        a_b.spectrumListPtr = temp_a_b;
        b_a.spectrumListPtr = temp_b_a; 
        SpectrumListPtr temp_a = a.spectrumListPtr.get() ? a.spectrumListPtr : SpectrumListPtr(new SpectrumListSimple);
        SpectrumListPtr temp_b = b.spectrumListPtr.get() ? b.spectrumListPtr : SpectrumListPtr(new SpectrumListSimple);
        diff(*temp_a, *temp_b, *temp_a_b, *temp_b_a, config);

        double maxPrecisionDiffSpec = 0;
        DataProcessingPtr sl_a_b_dp = temp_a_b->dp;
        if (sl_a_b_dp.get() &&
            !sl_a_b_dp->processingMethods.empty() &&
            !sl_a_b_dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).empty())
            maxPrecisionDiffSpec = lexical_cast<double>(sl_a_b_dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).value);

        if (maxPrecisionDiffSpec>(config.precision+numeric_limits<double>::epsilon()))
        {
            a_b.userParams.push_back(UserParam("Spectrum binary data array difference",
                lexical_cast<string>(maxPrecisionDiffSpec),
                "xsd:float"));
            b_a.userParams.push_back(UserParam("Spectrum binary data array difference",
                lexical_cast<string>(maxPrecisionDiffSpec),
                "xsd:float"));
        }
    }

    if (!config.ignoreChromatograms)
    {
        // special handling for ChromatogramList diff
        shared_ptr<ChromatogramListSimple> cl_temp_a_b(new ChromatogramListSimple); 
        shared_ptr<ChromatogramListSimple> cl_temp_b_a(new ChromatogramListSimple);
        a_b.chromatogramListPtr = cl_temp_a_b;
        b_a.chromatogramListPtr = cl_temp_b_a; 
        ChromatogramListPtr cl_temp_a = a.chromatogramListPtr.get() ? a.chromatogramListPtr : ChromatogramListPtr(new ChromatogramListSimple);
        ChromatogramListPtr cl_temp_b = b.chromatogramListPtr.get() ? b.chromatogramListPtr : ChromatogramListPtr(new ChromatogramListSimple);
        diff(*cl_temp_a, *cl_temp_b, *cl_temp_a_b, *cl_temp_b_a, config);

        double maxPrecisionDiffChr = 0;
        DataProcessingPtr cl_a_b_dp = cl_temp_a_b->dp;
        if (cl_a_b_dp.get() &&
            !cl_a_b_dp->processingMethods.empty() &&
            !cl_a_b_dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).empty())
            maxPrecisionDiffChr = lexical_cast<double>(cl_a_b_dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).value);

        if (maxPrecisionDiffChr>(config.precision+numeric_limits<double>::epsilon()))
        {
            a_b.userParams.push_back(UserParam("Chromatogram binary data array difference",
                lexical_cast<string>(maxPrecisionDiffChr),
                "xsd:float"));
            b_a.userParams.push_back(UserParam("Chromatogram binary data array difference",
                lexical_cast<string>(maxPrecisionDiffChr),
                "xsd:float"));
        }
    }

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const MSData& a,
          const MSData& b,
          MSData& a_b,
          MSData& b_a,
          const DiffConfig& config)
{
    string a_b_version, b_a_version;

    if (!config.ignoreMetadata)
    {
        diff(a.accession, b.accession, a_b.accession, b_a.accession, config);
        diff(a.id, b.id, a_b.id, b_a.id, config);
	    if (!config.ignoreVersions)
        diff(a.version(), b.version(), a_b_version, b_a_version, config);
        vector_diff_diff(a.cvs, b.cvs, a_b.cvs, b_a.cvs, config);
        diff(a.fileDescription, b.fileDescription, a_b.fileDescription, b_a.fileDescription, config);
        vector_diff_deep(a.paramGroupPtrs, b.paramGroupPtrs, a_b.paramGroupPtrs, b_a.paramGroupPtrs, config);
        vector_diff_deep(a.samplePtrs, b.samplePtrs, a_b.samplePtrs, b_a.samplePtrs, config);
        vector_diff_deep(a.softwarePtrs, b.softwarePtrs, a_b.softwarePtrs, b_a.softwarePtrs, config);
        vector_diff_deep(a.scanSettingsPtrs, b.scanSettingsPtrs, a_b.scanSettingsPtrs, b_a.scanSettingsPtrs, config);
        vector_diff_deep(a.instrumentConfigurationPtrs, b.instrumentConfigurationPtrs, a_b.instrumentConfigurationPtrs, b_a.instrumentConfigurationPtrs, config);

        // do diff on full DataProcessing list
        vector_diff_deep(a.allDataProcessingPtrs(), b.allDataProcessingPtrs(), a_b.dataProcessingPtrs, b_a.dataProcessingPtrs, config);
    }

    // ignore DataProcessing in SpectrumList/ChromatogramList
    DiffConfig config_ignoreDataProcessing(config);
    config_ignoreDataProcessing.ignoreDataProcessing = true;
    diff(a.run, b.run, a_b.run, b_a.run, config_ignoreDataProcessing);

    // provide context
    if (!a_b.empty() || !b_a.empty() ||
        !a_b_version.empty() || !b_a_version.empty()) 
    {
        a_b.id = a.id + (a_b_version.empty() ? "" : " (" + a_b_version + ")");
        b_a.id = b.id + (b_a_version.empty() ? "" : " (" + b_a_version + ")");
    }
}


} // namespace diff_impl
} // namespace data


namespace msdata
{


std::ostream& os_write_spectra(std::ostream& os, const SpectrumListPtr a_b, const SpectrumListPtr b_a)
{
    TextWriter write(os, 1);

    if(a_b->size()!=b_a->size())
    {
        os<<"in SpectrumList diff: SpectrumList sizes differ"<<endl;
        return os;
    }

    for(size_t index(0);index<(*a_b).size();index++)
    {
        os<<"+\n";
        write(*(a_b->spectrum(index)));

        os<<"-\n";
        write(*(b_a->spectrum(index)));
    }

    return os;
}

std::ostream& os_write_chromatograms(std::ostream& os, const ChromatogramListPtr a_b, const ChromatogramListPtr b_a)
{
    TextWriter write(os,1);

    if((a_b==NULL) != (b_a==NULL))
    {
        os<<"in ChromatogramList diff: one of two ChromatogramList pointers is NULL"<<endl;
        return os;
    }

    if((a_b==NULL) && (b_a==NULL))
    {
        return os;
    }

    if(a_b->size()!=b_a->size())
    {
        os<<"in ChromatogramList diff: ChromatogramList sizes differ"<<endl;
        return os;
    }

    for(size_t index=0;index<a_b->size();index++)
    {
        os<<"+\n";
        write(*(a_b->chromatogram(index)));  

        os<<"-\n";
        write(*(b_a->chromatogram(index)));
    }

    return os;
}


PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const data::Diff<MSData, DiffConfig>& diff)
{
    TextWriter write(os,1);

    if(!diff.a_b.empty()|| !diff.b_a.empty())

    {
        os<<"+\n";
        write(diff.a_b,true);
        os<<"-\n";
        write(diff.b_a,true);

        os_write_spectra(os,diff.a_b.run.spectrumListPtr,diff.b_a.run.spectrumListPtr);

        os_write_chromatograms(os,diff.a_b.run.chromatogramListPtr, diff.b_a.run.chromatogramListPtr);
    }

    return os;

}


} // namespace msdata
} // namespace pwiz
