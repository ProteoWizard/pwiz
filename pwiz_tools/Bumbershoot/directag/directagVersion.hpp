//
// $Id: tagreconVersion.hpp 35 2011-07-21 17:32:40Z chambm $
//

#ifndef _DIRECTAG_VERSION_HPP_
#define _DIRECTAG_VERSION_HPP_

#include <string>

namespace freicore {
namespace directag {

struct Version
{
    static int Major();
    static int Minor();
    static int Revision();
    static std::string str();
    static std::string LastModified();
};

} // namespace directag
} // namespace freicore

#endif // _DIRECTAG_VERSION_HPP_
