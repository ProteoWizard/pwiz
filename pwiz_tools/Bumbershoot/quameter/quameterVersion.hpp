//
// $Id$
//

#ifndef _QUAMETER_VERSION_HPP_
#define _QUAMETER_VERSION_HPP_

#include <string>

namespace freicore {
namespace quameter {

struct Version
{
    static int Major();
    static int Minor();
    static int Revision();
    static std::string Branch();
    static std::string str();
    static std::string LastModified();
};

} // namespace quameter
} // namespace freicore

#endif // _QUAMETER_VERSION_HPP_
