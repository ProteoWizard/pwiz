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


namespace b = pwiz::msdata;
typedef b::Diff<b::MSData, b::DiffConfig> NativeMSDataDiff;

namespace {

b::DiffConfig NativeDiffConfig(pwiz::CLI::msdata::DiffConfig^ config)
{
    b::DiffConfig nativeConfig;
    nativeConfig.precision = config->precision;
    nativeConfig.ignoreMetadata = config->ignoreMetadata;
    nativeConfig.ignoreChromatograms = config->ignoreChromatograms;
    nativeConfig.ignoreDataProcessing = config->ignoreDataProcessing;
    return nativeConfig;
}

} // namespace


namespace pwiz {
namespace CLI {
namespace msdata {


Diff::Diff()
:   base_(new NativeMSDataDiff())
{}


Diff::Diff(DiffConfig^ config)
:   base_(new NativeMSDataDiff(NativeDiffConfig(config)))
{}


Diff::Diff(MSData% a, MSData% b)
:   base_(new NativeMSDataDiff(**a.base_, **b.base_))
{}


Diff::Diff(MSData^ a, MSData^ b)
:   base_(new NativeMSDataDiff(**a->base_, **b->base_))
{}


Diff::Diff(MSData% a, MSData% b, DiffConfig^ config)
:   base_(new NativeMSDataDiff(**a.base_, **b.base_, NativeDiffConfig(config)))
{}


Diff::Diff(MSData^ a, MSData^ b, DiffConfig^ config)
:   base_(new NativeMSDataDiff(**a->base_, **b->base_, NativeDiffConfig(config)))
{}


MSData^ Diff::a_b::get() {return gcnew MSData(new boost::shared_ptr<b::MSData>(&base_->a_b, nullDelete));}
MSData^ Diff::b_a::get() {return gcnew MSData(new boost::shared_ptr<b::MSData>(&base_->b_a, nullDelete));}


Diff^ Diff::apply(MSData^ a, MSData^ b)
{
    (*base_)(**a->base_, **b->base_);
    return this;
}


Diff^ Diff::apply(MSData% a, MSData% b)
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


} // namespace msdata
} // namespace CLI
} // namespace pwiz
