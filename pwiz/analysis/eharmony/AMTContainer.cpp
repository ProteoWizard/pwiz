///
/// AMTContainer.cpp
///

#include "AMTContainer.hpp"
#include "Peptide2FeatureMatcher.hpp"

using namespace pwiz;
using namespace eharmony;

void AMTContainer::merge(const AMTContainer& that)
{
    _fdf.merge(that._fdf);
    _pidf.merge(that._pidf);
    _mdf.merge(that._mdf);

}
