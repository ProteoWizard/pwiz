#include "Diff.hpp"


namespace b = pwiz::msdata;


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
:   base_(new b::Diff<b::MSData>())
{}


Diff::Diff(DiffConfig^ config)
:   base_(new b::Diff<b::MSData>(NativeDiffConfig(config)))
{}


Diff::Diff(MSData% a, MSData% b)
:   base_(new b::Diff<b::MSData>(**a.base_, **b.base_))
{}


Diff::Diff(MSData^ a, MSData^ b)
:   base_(new b::Diff<b::MSData>(**a->base_, **b->base_))
{}


Diff::Diff(MSData% a, MSData% b, DiffConfig^ config)
:   base_(new b::Diff<b::MSData>(**a.base_, **b.base_, NativeDiffConfig(config)))
{}


Diff::Diff(MSData^ a, MSData^ b, DiffConfig^ config)
:   base_(new b::Diff<b::MSData>(**a->base_, **b->base_, NativeDiffConfig(config)))
{}


// for shared ptrs to non-heap objects, this deallocator does nothing
namespace { void nullDeallocator(b::MSData* p) {} }


MSData^ Diff::a_b::get() {return gcnew MSData(new boost::shared_ptr<b::MSData>(&base_->a_b, nullDeallocator), this);}
MSData^ Diff::b_a::get() {return gcnew MSData(new boost::shared_ptr<b::MSData>(&base_->b_a, nullDeallocator), this);}


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
    return gcnew System::String(oss.str().c_str());
}


Diff::operator System::String^ (Diff% diff)
{
    std::ostringstream oss;
    oss << *diff.base_;
    return gcnew System::String(oss.str().c_str());
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
