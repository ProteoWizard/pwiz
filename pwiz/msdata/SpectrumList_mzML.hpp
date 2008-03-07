//
// SpectrumList_mzML.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SPECTRUMLIST_MZML_HPP_
#define _SPECTRUMLIST_MZML_HPP_


#include "MSData.hpp"
#include <iosfwd>


namespace pwiz {
namespace msdata {


/// implementation of SpectrumList, backed by an mzML file
class SpectrumList_mzML
{
    public:

    static SpectrumListPtr create(boost::shared_ptr<std::istream> is,
                                  const MSData& msd,
                                  bool indexed = true);
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLIST_MZML_HPP_

