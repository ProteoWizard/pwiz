//
// $Id$
//

#ifndef _PEPITOME_VERSION_HPP_
#define _PEPITOME_VERSION_HPP_

#include <string>

namespace freicore {
namespace pepitome {

struct Version
{
    static int Major();
    static int Minor();
    static int Revision();
    static std::string Branch();
    static std::string str();
    static std::string LastModified();
};

} // namespace pepitome
} // namespace freicore

#endif // _PEPITOME_VERSION_HPP_
