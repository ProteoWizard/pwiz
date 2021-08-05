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


#ifndef _DIFF_HPP_
#define _DIFF_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"


namespace pwiz { namespace msdata { struct DiffConfig; } }


namespace pwiz {
namespace data {
namespace diff_impl {


using namespace msdata;


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
void diff(const ScanSettings& a,
          const ScanSettings& b,
          ScanSettings& a_b,
          ScanSettings& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Precursor& a,
          const Precursor& b,
          Precursor& a_b,
          Precursor& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Product& a,
          const Product& b,
          Product& a_b,
          Product& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const Scan& a,
          const Scan& b,
          Scan& a_b,
          Scan& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ScanList& a,
          const ScanList& b,
          ScanList& a_b,
          ScanList& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const BinaryDataArray& a,
          const BinaryDataArray& b,
          BinaryDataArray& a_b,
          BinaryDataArray& b_a,
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const IntegerDataArray& a,
          const IntegerDataArray& b,
          IntegerDataArray& a_b,
          IntegerDataArray& b_a,
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
          const DiffConfig& config);

PWIZ_API_DECL
void diff(const ChromatogramList& a,
          const ChromatogramList& b,
          ChromatogramListSimple& a_b,
          ChromatogramListSimple& b_a,
          const DiffConfig& config);

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
} // namespace data
} // namespace pwiz


// this include must come after the above declarations or GCC won't see them
#include "pwiz/data/common/diff_std.hpp"


namespace pwiz {
namespace msdata {


/// configuration struct for diffing MSData types
struct PWIZ_API_DECL DiffConfig : public pwiz::data::BaseDiffConfig
{
    /// ignore members of SpectrumIdentity and ChromatogramIdentity
    bool ignoreIdentity;

    /// ignore all file level metadata, and most scan level metadata,
    /// i.e. verify scan binary data, plus important scan metadata:
    ///  - msLevel
    ///  - precursor.ionSelection
    bool ignoreMetadata;

    /// ignore arrays like mobility, charge state, noise, SNR, etc.
    bool ignoreExtraBinaryDataArrays;

    bool ignoreSpectra;
    bool ignoreChromatograms;

    bool ignoreDataProcessing;

    DiffConfig()
    :   pwiz::data::BaseDiffConfig(),
        ignoreIdentity(false),
        ignoreMetadata(false),
        ignoreExtraBinaryDataArrays(false),
        ignoreSpectra(false),
        ignoreChromatograms(false),
        ignoreDataProcessing(false)
    {}
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const pwiz::data::Diff<MSData, DiffConfig>& diff);


} // namespace msdata
} // namespace pwiz


#endif // _DIFF_HPP_
