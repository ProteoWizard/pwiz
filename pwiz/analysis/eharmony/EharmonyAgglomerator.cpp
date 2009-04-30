///
/// EharmonyAgglomerator.cpp
///

#include "EharmonyAgglomerator.hpp"

using namespace pwiz;
using namespace eharmony;

void AMTContainer::merge(const AMTContainer& that)
{
    _fdf.merge(that._fdf);
    _pidf.merge(that._pidf);
        
}
