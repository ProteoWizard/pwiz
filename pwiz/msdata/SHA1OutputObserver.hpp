//
// SHA1OutputObserver.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SHA1OUTPUTOBSERVER_HPP_
#define _SHA1OUTPUTOBSERVER_HPP_


#include "minimxml/XMLWriter.hpp"
#include "util/SHA1Calculator.hpp"


namespace pwiz {
namespace msdata {


class SHA1OutputObserver : public minimxml::XMLWriter::OutputObserver
{
    public:
    virtual void update(const std::string& output) {sha1Calculator_.update(output);}
    std::string hash() {return sha1Calculator_.hashProjected();}

    private:
    util::SHA1Calculator sha1Calculator_; 
};


} // namespace msdata
} // namespace pwiz


#endif // _SHA1OUTPUTOBSERVER_HPP_ 

