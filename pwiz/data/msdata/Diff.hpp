//
// Diff.hpp
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


#ifndef _DIFF_HPP_
#define _DIFF_HPP_


#include "utility/misc/Export.hpp"
#include "MSData.hpp"
#include "TextWriter.hpp"


namespace pwiz {
namespace msdata {


/// configuration struct for diffs
struct PWIZ_API_DECL DiffConfig 
{
    /// precision with which two doubles are compared
    double precision;

    /// ignore all file level metadata, and most scan level metadata,
    /// i.e. verify scan binary data, plus important scan metadata:
    ///  - msLevel 
    ///  - scanNumber 
    ///  - precursor.ionSelection
    bool ignoreMetadata;

    bool ignoreChromatograms;

    DiffConfig()
    :   precision(1e-6), 
        ignoreMetadata(false),
        ignoreChromatograms(false)
    {}
};


//
// diff implementation declarations
//


namespace diff_impl {


PWIZ_API_DECL
void diff(const std::string& a,
          const std::string& b,
          std::string& a_b,
          std::string& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const CV& a,
          const CV& b,
          CV& a_b,
          CV& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const UserParam& a,
          const UserParam& b,
          UserParam& a_b,
          UserParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const CVParam& a,
          const CVParam& b,
          CVParam& a_b,
          CVParam& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ParamContainer& a,
          const ParamContainer& b,
          ParamContainer& a_b,
          ParamContainer& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ParamGroup& a,
          const ParamGroup& b,
          ParamGroup& a_b,
          ParamGroup& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SourceFile& a,
          const SourceFile& b,
          SourceFile& a_b,
          SourceFile& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const FileDescription& a,
          const FileDescription& b,
          FileDescription& a_b,
          FileDescription& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Sample& a,
          const Sample& b,
          Sample& a_b,
          Sample& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Component& a,
          const Component& b,
          Component& a_b,
          Component& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ComponentList& a,
          const ComponentList& b,
          ComponentList& a_b,
          ComponentList& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Software& a,
          const Software& b,
          Software& a_b,
          Software& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const InstrumentConfiguration& a,
          const InstrumentConfiguration& b,
          InstrumentConfiguration& a_b,
          InstrumentConfiguration& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ProcessingMethod& a,
          const ProcessingMethod& b,
          ProcessingMethod& a_b,
          ProcessingMethod& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const DataProcessing& a,
          const DataProcessing& b,
          DataProcessing& a_b,
          DataProcessing& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AcquisitionSettings& a,
          const AcquisitionSettings& b,
          AcquisitionSettings& a_b,
          AcquisitionSettings& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Acquisition& a,
          const Acquisition& b,
          Acquisition& a_b,
          Acquisition& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const AcquisitionList& a,
          const AcquisitionList& b,
          AcquisitionList& a_b,
          AcquisitionList& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Precursor& a,
          const Precursor& b,
          Precursor& a_b,
          Precursor& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Scan& a,
          const Scan& b,
          Scan& a_b,
          Scan& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumDescription& a,
          const SpectrumDescription& b,
          SpectrumDescription& a_b,
          SpectrumDescription& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const BinaryDataArray& a,
          const BinaryDataArray& b,
          BinaryDataArray& a_b,
          BinaryDataArray& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Spectrum& a,
          const Spectrum& b,
          Spectrum& a_b,
          Spectrum& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Chromatogram& a,
          const Chromatogram& b,
          Chromatogram& a_b,
          Chromatogram& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const SpectrumList& a,
          const SpectrumList& b,
          SpectrumListSimple& a_b,
          SpectrumListSimple& b_a,
          const DiffConfig& config, double& maxPrecisionDiff);

PWIZ_API_DECL
void diff(const ChromatogramList& a,
          const ChromatogramList& b,
          ChromatogramListSimple& a_b,
          ChromatogramListSimple& b_a,
          const DiffConfig& config, double& maxPrecisionDiff);

PWIZ_API_DECL
void diff(const Run& a,
          const Run& b,
          Run& a_b,
          Run& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const MSData& a,
          const MSData& b,
          MSData& a_b,
          MSData& b_a,
          const DiffConfig& config);

} // namespace diff_impl 


///     
/// Calculate diffs of objects in the MSData structure hierarchy.
///
/// A diff between two objects a and b calculates the set differences
/// a\b and b\a.
///
/// The Diff struct acts as a functor, but also stores the 
/// results of the diff calculation.  
///
/// The bool conversion operator is provided to indicate whether 
/// the two objects are different (either a\b or b\a is non-empty).
///
/// object_type requirements:
///   object_type a;
///   a.empty();
///   diff(const object_type& a, const object_type& b, object_type& a_b, object_type& b_a);
///
template <typename object_type>
struct Diff
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const object_type& a,
               const object_type& b,
               const DiffConfig& config = DiffConfig())
    :   config_(config)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
    }

    object_type a_b;
    object_type b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const object_type& a,
                           const object_type& b)
    {
        
        diff_impl::diff(a, b, a_b, b_a, config_);
        return *this;
    }

    private:
    DiffConfig config_;
};


template <>
struct Diff<SpectrumList>
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const SpectrumList& a,
               const SpectrumList& b,
               const DiffConfig& config = DiffConfig())
      :   config_(config)
    {
        double maxPrecisionDiff=0;
        diff_impl::diff(a, b, a_b, b_a, config_, maxPrecisionDiff);
    }

    SpectrumListSimple a_b;
    SpectrumListSimple b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const SpectrumList& a,
		   const SpectrumList& b)
 
   {
        double maxPrecisionDiff=0;
        diff_impl::diff(a, b, a_b, b_a, config_,maxPrecisionDiff);
        return *this;
   }


    private:
    DiffConfig config_;
};

template <>
struct Diff<ChromatogramList>
{
    Diff(const DiffConfig& config = DiffConfig())
    :   config_(config)
    {}

    Diff(const ChromatogramList& a,
               const ChromatogramList& b,
               const DiffConfig& config = DiffConfig())

      :   config_(config)
    {
        double maxPrecisionDiff=0;
        diff_impl::diff(a, b, a_b, b_a, config_,maxPrecisionDiff);
    }

    ChromatogramListSimple a_b;
    ChromatogramListSimple b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const ChromatogramList& a,
                     const ChromatogramList& b)
    {   
        double maxPrecisionDiff=0;
        diff_impl::diff(a, b, a_b, b_a, config_,maxPrecisionDiff);
        return *this;
    }

    private:
    DiffConfig config_;
};

///
/// stream insertion of Diff results
///

template <typename object_type>
std::ostream& operator<<(std::ostream& os, const Diff<object_type>& diff)
{
    TextWriter write(os, 1);

    if (!diff.a_b.empty())
    {            
        os << "+\n";
        write(diff.a_b);
    }

    if (!diff.b_a.empty())
    {            
        os << "-\n";
        write(diff.b_a);
    }

    return os;
}

std::ostream& operator<<(std::ostream& os, const Diff<MSData>& diff);

} // namespace msdata
} // namespace pwiz


#endif // _DIFF_HPP_

