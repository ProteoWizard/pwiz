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


namespace b = pwiz::tradata;
typedef b::Diff<b::TraData, b::DiffConfig> NativeTraDataDiff;

namespace {

b::DiffConfig NativeDiffConfig(pwiz::CLI::tradata::DiffConfig^ config)
{
    b::DiffConfig nativeConfig;
    nativeConfig.precision = config->precision;

    return nativeConfig;
}

} // namespace


namespace pwiz {
namespace CLI {
namespace tradata {


Diff::Diff()
:   base_(new NativeTraDataDiff())
{}


Diff::Diff(DiffConfig^ config)
:   base_(new NativeTraDataDiff(NativeDiffConfig(config)))
{}


Diff::Diff(TraData% a, TraData% b)
:   base_(new NativeTraDataDiff(**a.base_, **b.base_))
{}


Diff::Diff(TraData^ a, TraData^ b)
:   base_(new NativeTraDataDiff(**a->base_, **b->base_))
{}


Diff::Diff(TraData% a, TraData% b, DiffConfig^ config)
:   base_(new NativeTraDataDiff(**a.base_, **b.base_, NativeDiffConfig(config)))
{}


Diff::Diff(TraData^ a, TraData^ b, DiffConfig^ config)
:   base_(new NativeTraDataDiff(**a->base_, **b->base_, NativeDiffConfig(config)))
{}


// for shared ptrs to non-heap objects, this deallocator does nothing
namespace { void nullDeallocator(b::TraData* p) {} }


TraData^ Diff::a_b::get() {return gcnew TraData(new boost::shared_ptr<b::TraData>(&base_->a_b, nullDeallocator), this);}
TraData^ Diff::b_a::get() {return gcnew TraData(new boost::shared_ptr<b::TraData>(&base_->b_a, nullDeallocator), this);}


Diff^ Diff::apply(TraData^ a, TraData^ b)
{
    (*base_)(**a->base_, **b->base_);
    return this;
}


Diff^ Diff::apply(TraData% a, TraData% b)
{
    (*base_)(**a.base_, **b.base_);
    return this;
}


Diff::operator System::String^ (Diff^ diff)
{
    std::ostringstream oss;
    oss << *diff->base_;
    return ToSystemString(oss.str());
}


Diff::operator System::String^ (Diff% diff)
{
    std::ostringstream oss;
    oss << *diff.base_;
    return ToSystemString(oss.str());
}


} // namespace tradata
} // namespace CLI
} // namespace pwiz
