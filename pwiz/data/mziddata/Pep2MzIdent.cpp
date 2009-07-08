//
// Pep2MzIdent.cpp
//

#define PWIZ_SOURCE

#include "Pep2MzIdent.hpp"

using namespace pwiz;
using namespace pwiz::mziddata;
using namespace pwiz::data::pepxml;

Pep2MzIdent::Pep2MzIdent(const MSMSPipelineAnalysis& mspa, MzIdentMLPtr result) : _mspa(mspa), _result(result) {}

MzIdentMLPtr Pep2MzIdent::translate()
{
    if (_translated) return _result;
    else
        {
            translateMetadata();
            translateSpectrumQueries();
            _translated = true;

            return _result;
        }

}

void Pep2MzIdent::translateMetadata()
{

}

void Pep2MzIdent::translateSpectrumQueries()
{

}

