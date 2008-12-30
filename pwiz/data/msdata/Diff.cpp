//
// Diff.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include <string>
#include <cmath>
#include <stdexcept>

namespace pwiz {
namespace msdata {
namespace diff_impl {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;


PWIZ_API_DECL
void diff(const string& a, 
          const string& b, 
          string& a_b, 
          string& b_a,
          const DiffConfig& config)
{
    a_b.clear();
    b_a.clear();
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <typename T>
void diff_numeric(const T& a, 
                  const T& b, 
                  T& a_b, 
                  T& b_a,
                  const DiffConfig& config)
{
    a_b = 0;
    b_a = 0;
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


PWIZ_API_DECL
void diff(const CV& a, 
          const CV& b, 
          CV& a_b, 
          CV& b_a,
          const DiffConfig& config)
{
    diff(a.URI, b.URI, a_b.URI, b_a.URI, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.fullName, b.fullName, a_b.fullName, b_a.fullName, config);
    diff(a.version, b.version, a_b.version, b_a.version, config);
}


PWIZ_API_DECL
void diff(CVID a,
          CVID b,
          CVID& a_b,
          CVID& b_a,
          const DiffConfig& config)
{
    a_b = b_a = CVID_Unknown;
    if (a!=b)  
    {
        a_b = a;
        b_a = b;
    }
}


PWIZ_API_DECL
void diff(const CVParam& a, 
          const CVParam& b, 
          CVParam& a_b, 
          CVParam& b_a,
          const DiffConfig& config)
{
    diff(a.cvid, b.cvid, a_b.cvid, b_a.cvid, config);
    diff(a.value, b.value, a_b.value, b_a.value, config);
    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.cvid==CVID_Unknown) a_b.cvid = a.cvid; 
    if (!b_a.empty() && b_a.cvid==CVID_Unknown) b_a.cvid = b.cvid; 
}


PWIZ_API_DECL
void diff(const UserParam& a, 
          const UserParam& b, 
          UserParam& a_b, 
          UserParam& b_a,
          const DiffConfig& config)
{
    diff(a.name, b.name, a_b.name, b_a.name, config);
    diff(a.value, b.value, a_b.value, b_a.value, config);
    diff(a.type, b.type, a_b.type, b_a.type, config);
    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.name.empty()) a_b.name = a.name; 
    if (!b_a.empty() && b_a.name.empty()) b_a.name = b.name; 
}


template <typename object_type>
void vector_diff(const vector<object_type>& a,
                 const vector<object_type>& b,
                 vector<object_type>& a_b,
                 vector<object_type>& b_a)
{
    // calculate set differences of two vectors

    a_b.clear();
    b_a.clear();

    for (typename vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find(b.begin(), b.end(), *it) == b.end())
            a_b.push_back(*it);

    for (typename vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find(a.begin(), a.end(), *it) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}
    bool operator()(const shared_ptr<object_type>& objectPtr) {return objectPtr->id == id_;}
};


template <typename object_type>
class Same
{
    public:

    Same(const object_type& object,
         const DiffConfig& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const object_type& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type>(mine_, yours, config_);
    }

    private:
    const object_type& mine_;
    const DiffConfig& config_;
};


template <typename object_type>
void vector_diff_diff(const vector<object_type>& a,
                      const vector<object_type>& b,
                      vector<object_type>& a_b,
                      vector<object_type>& b_a,
                      const DiffConfig& config)
{
    // calculate set differences of two vectors, using diff on each object

    a_b.clear();
    b_a.clear();

    for (typename vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find_if(b.begin(), b.end(), Same<object_type>(*it, config)) == b.end())
            a_b.push_back(*it);

    for (typename vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find_if(a.begin(), a.end(), Same<object_type>(*it, config)) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
class SameDeep
{
    public:

    SameDeep(const object_type& object,
             const DiffConfig& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const shared_ptr<object_type>& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type>(mine_, *yours, config_);
    }

    private:
    const object_type& mine_;
    const DiffConfig& config_;
};


template <typename object_type>
void vector_diff_deep(const vector< shared_ptr<object_type> >& a,
                      const vector< shared_ptr<object_type> >& b,
                      vector< shared_ptr<object_type> >& a_b,
                      vector< shared_ptr<object_type> >& b_a,
                      const DiffConfig& config)
{
    // calculate set differences of two vectors of ObjectPtrs (deep compare using diff)

    a_b.clear();
    b_a.clear();

    for (typename vector< shared_ptr<object_type> >::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find_if(b.begin(), b.end(), SameDeep<object_type>(**it, config)) == b.end())
            a_b.push_back(*it);

    for (typename vector< shared_ptr<object_type> >::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find_if(a.begin(), a.end(), SameDeep<object_type>(**it, config)) == a.end())
            b_a.push_back(*it);
}


PWIZ_API_DECL
void diff(const ParamContainer& a, 
          const ParamContainer& b, 
          ParamContainer& a_b, 
          ParamContainer& b_a,
          const DiffConfig& config)
{
    vector_diff_deep(a.paramGroupPtrs, b.paramGroupPtrs, a_b.paramGroupPtrs, b_a.paramGroupPtrs, config);
    vector_diff(a.cvParams, b.cvParams, a_b.cvParams, b_a.cvParams);
    vector_diff(a.userParams, b.userParams, a_b.userParams, b_a.userParams);
}


PWIZ_API_DECL
void diff(const ParamGroup& a, 
          const ParamGroup& b, 
          ParamGroup& a_b, 
          ParamGroup& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


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
    diff_numeric(a.order, b.order, a_b.order, b_a.order, config);
    diff_numeric((int)a.type, (int)b.type, a_bType, b_aType, config);
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
    diff(a.version, b.version, a_b.version, b_a.version, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


template <typename object_type>
void ptr_diff(const shared_ptr<object_type>& a,
              const shared_ptr<object_type>& b,
              shared_ptr<object_type>& a_b,
              shared_ptr<object_type>& b_a,
              const DiffConfig& config)
{
    if (!a.get() && !b.get()) return;

    shared_ptr<object_type> a_temp = a.get() ? a : shared_ptr<object_type>(new object_type);
    shared_ptr<object_type> b_temp = b.get() ? b : shared_ptr<object_type>(new object_type);

    if (!a_b.get()) a_b = shared_ptr<object_type>(new object_type);
    if (!b_a.get()) b_a = shared_ptr<object_type>(new object_type);
    diff(*a_temp, *b_temp, *a_b, *b_a, config);

    if (a_b->empty()) a_b = shared_ptr<object_type>();
    if (b_a->empty()) b_a = shared_ptr<object_type>();
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
    diff_numeric(a.order, b.order, a_b.order, b_a.order, config);
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


// measure maximum relative difference between elements in the vectors
double maxdiff(const vector<double>& a, const vector<double>& b)
{
    if (a.size() != b.size()) 
        throw runtime_error("[Diff::maxdiff()] Sizes differ.");

    vector<double>::const_iterator i = a.begin(); 
    vector<double>::const_iterator j = b.begin(); 

    double max = 0;

    for (; i!=a.end(); ++i, ++j)
    {
        double denominator = min(*i, *j);
        if (denominator == 0) denominator = 1;
        double current = fabs(*i - *j)/denominator;
        if (max < current) max = current;

    }

    return max;
}


const char* userParamName_BinaryDataArrayDifference_ = "Binary data array difference";


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
        double max = maxdiff(a.data, b.data);
       
        if (max > config.precision + numeric_limits<double>::epsilon())
        {
            a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(max),
                                               "xsd:float"));
            b_a.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(max),
					       "xsd:float"));
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
          const DiffConfig& config, double& maxPrecisionDiff)
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
                if (max>maxPrecisionDiff) maxPrecisionDiff=max;
            }
        }
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

    // important scan metadata
    diff_numeric(a.index, b.index, a_b.index, b_a.index, config);
    diff(a.nativeID, b.nativeID, a_b.nativeID, b_a.nativeID, config);
    diff_numeric(a.defaultArrayLength, b.defaultArrayLength, a_b.defaultArrayLength, b_a.defaultArrayLength, config);
    vector_diff_diff(a.precursors, b.precursors, a_b.precursors, b_a.precursors, config);

    if (!config.ignoreMetadata)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
        ptr_diff(a.dataProcessingPtr, b.dataProcessingPtr, a_b.dataProcessingPtr, b_a.dataProcessingPtr, config);
        ptr_diff(a.sourceFilePtr, b.sourceFilePtr, a_b.sourceFilePtr, b_a.sourceFilePtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
        diff(a.scanList, b.scanList, a_b.scanList, b_a.scanList, config);
    }

    // special handling for binary data arrays

    if (a.binaryDataArrayPtrs.size() != b.binaryDataArrayPtrs.size())
    {
        a_b.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(a.binaryDataArrayPtrs.size())));
        b_a.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(b.binaryDataArrayPtrs.size())));
    }
    else
    { 

      double maxPrecisionDiff=0;
      diff(a.binaryDataArrayPtrs, b.binaryDataArrayPtrs, 
             a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs, config, maxPrecisionDiff);
      
      if (maxPrecisionDiff>(config.precision+numeric_limits<double>::epsilon()))   
  
      {
	a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(maxPrecisionDiff),
                                               "xsd:float"));      
        b_a.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
					 lexical_cast<string>(maxPrecisionDiff),
					 "xsd:float"));
      }

    }

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        a_b.nativeID = a.nativeID; 
        b_a.id = b.id; 
        b_a.nativeID = b.nativeID; 
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

    // important scan metadata
    diff_numeric(a.index, b.index, a_b.index, b_a.index, config);
    diff(a.nativeID, b.nativeID, a_b.nativeID, b_a.nativeID, config);
    diff_numeric(a.defaultArrayLength, b.defaultArrayLength, a_b.defaultArrayLength, b_a.defaultArrayLength, config);

    if (!config.ignoreMetadata)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
        ptr_diff(a.dataProcessingPtr, b.dataProcessingPtr, a_b.dataProcessingPtr, b_a.dataProcessingPtr, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    }

    // special handling for binary data arrays

    if (a.binaryDataArrayPtrs.size() != b.binaryDataArrayPtrs.size())
    {
        a_b.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(a.binaryDataArrayPtrs.size())));
        b_a.userParams.push_back(UserParam("Binary data array count: " + 
                                 lexical_cast<string>(b.binaryDataArrayPtrs.size())));
    }

    else
    {
      double maxPrecisionDiff=0;
      diff(a.binaryDataArrayPtrs, b.binaryDataArrayPtrs, 
             a_b.binaryDataArrayPtrs, b_a.binaryDataArrayPtrs, config, maxPrecisionDiff);
     
      if (maxPrecisionDiff>(config.precision+numeric_limits<double>::epsilon()))   
  
      {
	a_b.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
                                               lexical_cast<string>(maxPrecisionDiff),
                                               "xsd:float"));      
        b_a.userParams.push_back(UserParam(userParamName_BinaryDataArrayDifference_,
					 lexical_cast<string>(maxPrecisionDiff),
					 "xsd:float"));
      }

    }

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        a_b.nativeID = a.nativeID; 
        b_a.id = b.id; 
        b_a.nativeID = b.nativeID; 
    }
}


PWIZ_API_DECL
void diff(const SpectrumList& a,
          const SpectrumList& b,
          SpectrumListSimple& a_b,
          SpectrumListSimple& b_a,
          const DiffConfig& config, double& maxPrecisionDiff)
{
    a_b.spectra.clear();
    b_a.spectra.clear();
    
    if (a.size() != b.size())

    {
        SpectrumPtr dummy(new Spectrum);
        dummy->userParams.push_back(UserParam("SpectrumList sizes differ"));
        a_b.spectra.push_back(dummy);
        return;
    }

    for (unsigned int i=0; i<a.size(); i++)
    { 
        SpectrumPtr temp_a_b(new Spectrum);        
        SpectrumPtr temp_b_a(new Spectrum);
        diff(*a.spectrum(i, true), *b.spectrum(i, true), *temp_a_b, *temp_b_a, config);

        if (!temp_a_b->empty() || !temp_b_a->empty())
        {
            a_b.spectra.push_back(temp_a_b);
            b_a.spectra.push_back(temp_b_a);
	    
            if(!temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).empty())
            {
                double max=lexical_cast<double>(temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).value);
                if (max>maxPrecisionDiff) maxPrecisionDiff=max;
            }
        }
    }
}


PWIZ_API_DECL
void diff(const ChromatogramList& a,
          const ChromatogramList& b,
          ChromatogramListSimple& a_b,
          ChromatogramListSimple& b_a,
          const DiffConfig& config, double& maxPrecisionDiff)
{
    a_b.chromatograms.clear();
    b_a.chromatograms.clear();

    if (config.ignoreChromatograms) return;
    
    if (a.size() != b.size())
    {
        ChromatogramPtr dummy(new Chromatogram);
        dummy->userParams.push_back(UserParam("ChromatogramList sizes differ"));
        a_b.chromatograms.push_back(dummy);
        return;
    }

    for (unsigned int i=0; i<a.size(); i++)
    { 
        ChromatogramPtr temp_a_b(new Chromatogram);        
        ChromatogramPtr temp_b_a(new Chromatogram);        
        diff(*a.chromatogram(i, true), *b.chromatogram(i, true), *temp_a_b, *temp_b_a, config);
        if (!temp_a_b->empty() || !temp_b_a->empty())
        {
            a_b.chromatograms.push_back(temp_a_b);
            b_a.chromatograms.push_back(temp_b_a);
        }

	if(!temp_a_b->userParams.empty() || !temp_b_a->userParams.empty())
	  {
	    double max=lexical_cast<double>(temp_a_b->userParam(userParamName_BinaryDataArrayDifference_).value);
	    if(max>maxPrecisionDiff) 
	      {
		maxPrecisionDiff=max;
	
	      }
	  }	

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
        vector_diff_deep(a.sourceFilePtrs, b.sourceFilePtrs, a_b.sourceFilePtrs, b_a.sourceFilePtrs, config);
        diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    }
  
    // special handling for SpectrumList diff
    shared_ptr<SpectrumListSimple> temp_a_b(new SpectrumListSimple); 
    shared_ptr<SpectrumListSimple> temp_b_a(new SpectrumListSimple);
    a_b.spectrumListPtr = temp_a_b;
    b_a.spectrumListPtr = temp_b_a; 
    SpectrumListPtr temp_a = a.spectrumListPtr.get() ? a.spectrumListPtr : SpectrumListPtr(new SpectrumListSimple);
    SpectrumListPtr temp_b = b.spectrumListPtr.get() ? b.spectrumListPtr : SpectrumListPtr(new SpectrumListSimple);
    double maxPrecisionDiffSpec=0;
    diff(*temp_a, *temp_b, *temp_a_b, *temp_b_a, config,maxPrecisionDiffSpec);
    
    if (maxPrecisionDiffSpec>(config.precision+numeric_limits<double>::epsilon()))
      {
          a_b.userParams.push_back(UserParam("Spectrum binary data array difference",
					     lexical_cast<string>(maxPrecisionDiffSpec),
					     "xsd:float"));
          b_a.userParams.push_back(UserParam("Spectrum binary data array difference",
					     lexical_cast<string>(maxPrecisionDiffSpec),
					     "xsd:float"));
      }
    
    // special handling for ChromatogramList diff
    shared_ptr<ChromatogramListSimple> cl_temp_a_b(new ChromatogramListSimple); 
    shared_ptr<ChromatogramListSimple> cl_temp_b_a(new ChromatogramListSimple);
    a_b.chromatogramListPtr = cl_temp_a_b;
    b_a.chromatogramListPtr = cl_temp_b_a; 
    ChromatogramListPtr cl_temp_a = a.chromatogramListPtr.get() ? a.chromatogramListPtr : ChromatogramListPtr(new ChromatogramListSimple);
    ChromatogramListPtr cl_temp_b = b.chromatogramListPtr.get() ? b.chromatogramListPtr : ChromatogramListPtr(new ChromatogramListSimple);

    double maxPrecisionDiffChr=0;
    diff(*cl_temp_a, *cl_temp_b, *cl_temp_a_b, *cl_temp_b_a, config,maxPrecisionDiffChr);
   
    if (maxPrecisionDiffChr>(config.precision+numeric_limits<double>::epsilon()))
      {
        a_b.userParams.push_back(UserParam("Chromatogram binary data array difference",
                                             lexical_cast<string>(maxPrecisionDiffChr),
                                             "xsd:float"));
	b_a.userParams.push_back(UserParam("Chromatogram binary data array difference",
					   lexical_cast<string>(maxPrecisionDiffChr),
					   "xsd:float"));
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
    if (!config.ignoreMetadata)
    {
        diff(a.accession, b.accession, a_b.accession, b_a.accession, config);
        diff(a.id, b.id, a_b.id, b_a.id, config);
        diff(a.version, b.version, a_b.version, b_a.version, config);
        vector_diff_diff(a.cvs, b.cvs, a_b.cvs, b_a.cvs, config);
        diff(a.fileDescription, b.fileDescription, a_b.fileDescription, b_a.fileDescription, config);
        vector_diff_deep(a.paramGroupPtrs, b.paramGroupPtrs, a_b.paramGroupPtrs, b_a.paramGroupPtrs, config);
        vector_diff_deep(a.samplePtrs, b.samplePtrs, a_b.samplePtrs, b_a.samplePtrs, config);
        vector_diff_deep(a.softwarePtrs, b.softwarePtrs, a_b.softwarePtrs, b_a.softwarePtrs, config);
        vector_diff_deep(a.scanSettingsPtrs, b.scanSettingsPtrs, a_b.scanSettingsPtrs, b_a.scanSettingsPtrs, config);
        vector_diff_deep(a.instrumentConfigurationPtrs, b.instrumentConfigurationPtrs, a_b.instrumentConfigurationPtrs, b_a.instrumentConfigurationPtrs, config);
        vector_diff_deep(a.dataProcessingPtrs, b.dataProcessingPtrs, a_b.dataProcessingPtrs, b_a.dataProcessingPtrs, config);
    }

    diff(a.run, b.run, a_b.run, b_a.run, config);
    
    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


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

} // namespace diff_impl


PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const Diff<MSData>& diff)
{
  using namespace diff_impl;

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


