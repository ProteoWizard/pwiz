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


#include "Diff.hpp"


namespace b = pwiz::proteome;
typedef pwiz::data::Diff<b::ProteomeData, b::DiffConfig> NativeProteomeDataDiff;

namespace {

b::DiffConfig NativeDiffConfig(pwiz::CLI::proteome::DiffConfig^ config)
{
    b::DiffConfig nativeConfig;
    nativeConfig.ignoreMetadata = config->ignoreMetadata;
    return nativeConfig;
}

} // namespace


namespace pwiz {
namespace CLI {
namespace proteome {


Diff::Diff()
:   base_(new NativeProteomeDataDiff())
{}


Diff::Diff(DiffConfig^ config)
:   base_(new NativeProteomeDataDiff(NativeDiffConfig(config)))
{}


Diff::Diff(ProteomeData% a, ProteomeData% b)
:   base_(new NativeProteomeDataDiff(a.base(), b.base()))
{}


Diff::Diff(ProteomeData^ a, ProteomeData^ b)
:   base_(new NativeProteomeDataDiff(a->base(), b->base()))
{}


Diff::Diff(ProteomeData% a, ProteomeData% b, DiffConfig^ config)
:   base_(new NativeProteomeDataDiff(a.base(), b.base(), NativeDiffConfig(config)))
{}


Diff::Diff(ProteomeData^ a, ProteomeData^ b, DiffConfig^ config)
:   base_(new NativeProteomeDataDiff(a->base(), b->base(), NativeDiffConfig(config)))
{}


// for shared ptrs to non-heap objects, this deallocator does nothing
namespace { void nullDeallocator(b::ProteomeData* p) {} }


ProteomeData^ Diff::a_b::get() {return gcnew ProteomeData(new boost::shared_ptr<b::ProteomeData>(&base_->a_b, nullDeallocator), this);}
ProteomeData^ Diff::b_a::get() {return gcnew ProteomeData(new boost::shared_ptr<b::ProteomeData>(&base_->b_a, nullDeallocator), this);}


Diff^ Diff::apply(ProteomeData^ a, ProteomeData^ b)
{
    (*base_)(a->base(), b->base());
    return this;
}


Diff^ Diff::apply(ProteomeData% a, ProteomeData% b)
{
    (*base_)(a.base(), b.base());
    return this;
}


Diff::operator System::String^ (Diff^ diff)
{
    std::ostringstream oss;
    oss << diff->base();
    return ToSystemString(oss.str());
}


Diff::operator System::String^ (Diff% diff)
{
    std::ostringstream oss;
    oss << diff.base();
    return ToSystemString(oss.str());
}


} // namespace proteome
} // namespace CLI
} // namespace pwiz
