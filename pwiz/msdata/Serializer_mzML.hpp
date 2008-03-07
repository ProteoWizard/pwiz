//
// Serializer_mzML.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SERIALIZER_MZML_HPP_
#define _SERIALIZER_MZML_HPP_

#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"


namespace pwiz {
namespace msdata {


/// MSData <-> mzML stream serialization
class Serializer_mzML
{
    public:

    /// Serializer_mzML configuration
    struct Config
    {
        /// configuration for binary data encoding in write()
        /// note: byteOrder is ignored (mzML always little endian) 
        BinaryDataEncoder::Config binaryDataEncoderConfig;

        /// (indexed==true): read/write with <indexedmzML> wrapper
        bool indexed;

        Config() : indexed(true) {}
    };

    /// constructor
    Serializer_mzML(const Config& config = Config());

    /// write MSData object to ostream as mzML
    void write(std::ostream& os, const MSData& msd) const;

    /// read in MSData object from an mzML istream 
    /// note: istream may be managed by MSData's SpectrumList, to allow for 
    /// lazy evaluation of Spectrum data
    void read(boost::shared_ptr<std::istream> is, MSData& msd) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Serializer_mzML(Serializer_mzML&);
    Serializer_mzML& operator=(Serializer_mzML&);
};


std::ostream& operator<<(std::ostream& os, const Serializer_mzML::Config& config);


} // namespace msdata
} // namespace pwiz


#endif // _SERIALIZER_MZML_HPP_

