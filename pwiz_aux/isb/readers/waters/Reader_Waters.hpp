#ifndef _READER_WATERS_HPP_ 
#define _READER_WATERS_HPP_ 


#include "utility/misc/Export.hpp"
#include "data/msdata/Reader.hpp"


// DAC usage is msvc only - mingw doesn't provide com support
#if (!defined(_MSC_VER) && !defined(PWIZ_NO_READER_WATERS))
#define PWIZ_NO_READER_WATERS
#endif


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_Waters : public Reader
{
    public:

    virtual bool accept(const std::string& filename, 
                        const std::string& head) const; 

    virtual void read(const std::string& filename, 
                      const std::string& head, 
                      MSData& result) const;
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_WATERS_HPP_
