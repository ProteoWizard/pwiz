//
// Reader_RAW.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _READER_RAW_HPP_ 
#define _READER_RAW_HPP_ 


#include "MSDataFile.hpp"


namespace pwiz {
namespace msdata {


class Reader_RAW : public MSDataFile::Reader
{
    public:
    virtual bool accept(const std::string& filename, const std::string& head) const; 
    virtual void read(const std::string& filename, MSData& result) const;
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_RAW_HPP_ 

