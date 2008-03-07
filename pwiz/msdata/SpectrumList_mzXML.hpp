//
// SpectrumList_mzXML.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SPECTRUMLIST_MZXML_HPP_
#define _SPECTRUMLIST_MZXML_HPP_


#include "MSData.hpp"
#include <iosfwd>
#include <stdexcept>


namespace pwiz {
namespace msdata {


/// implementation of SpectrumList, backed by an mzXML file
class SpectrumList_mzXML : public SpectrumList
{
    public:

    static SpectrumListPtr create(boost::shared_ptr<std::istream> is,
                                  const MSData& msd,
                                  bool indexed = true);

    /// exception thrown if create(*,*,true) is called and 
    /// the mzXML index cannot be found
    struct index_not_found : public std::runtime_error
    {
        index_not_found(const std::string& what) : std::runtime_error(what.c_str()) {}
    };
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLIST_MZXML_HPP_

