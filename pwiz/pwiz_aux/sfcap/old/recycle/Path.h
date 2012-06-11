//
// Path.h
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _PATH_H_
#define _PATH_H_


#include <string>


namespace pwiz {
namespace msaux {


class Path
{
    public:

    static std::string directory(const std::string& fullpath);
    static std::string filename(const std::string& fullpath);
    static std::string base(const std::string& fullpath);
    static std::string extension(const std::string& fullpath);

    static bool mkdir(const std::string& directoryName); // return true on success
};


} // namespace msaux
} // namespace pwiz


#endif // _PATH_H_
