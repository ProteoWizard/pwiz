//
// SHA1Calculator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SHA1CALCULATOR_HPP_
#define _SHA1CALCULATOR_HPP_ 


#include "boost/shared_ptr.hpp"
#include <string>


namespace pwiz {
namespace util {


class SHA1Calculator
{
    public:

    SHA1Calculator();

    /// resets hash 
    void reset();

    /// update hash with buffer of bytes
    void update(const unsigned char* buffer, size_t bufferSize);

    /// update hash with buffer of bytes
    void update(const std::string& buffer);

    /// finish the hash 
    void close();

    /// returns the current hash value 
    /// note: close() must be called first to retrieve final hash value
    std::string hash() const;
    
    /// returns projected final hash value as if close() were called first;
    /// hash remains open and update() may be called afterwards 
    std::string hashProjected() const;

    /// static function to calculate hash of a buffer
    static std::string hash(const std::string& buffer);

    /// static function to calculate hash of a buffer
    static std::string hash(const unsigned char* buffer, size_t bufferSize);

    /// static function to calculate hash of a file 
    static std::string hashFile(const std::string& filename);    

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    SHA1Calculator(const SHA1Calculator&);
    SHA1Calculator& operator=(const SHA1Calculator&);
};


} // namespace util
} // namespace pwiz


#endif // _SHA1CALCULATOR_HPP_

