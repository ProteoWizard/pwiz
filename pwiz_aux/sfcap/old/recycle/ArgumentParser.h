//
// ArgumentParser.h
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


#ifndef _ARGUMENTPARSER_H_
#define _ARGUMENTPARSER_H_


#include <memory>
#include <string>
#include <vector>


namespace pwiz {
namespace msaux {


class ArgumentParser
{
    public:

    static std::auto_ptr<ArgumentParser> create(int argc, char* argv[]);

    // return argc
    virtual int argc() const = 0;

    // return argv[index]
    virtual std::string argument(int index) const = 0;

    // return args starting at index
    virtual void arguments(int index, int count, std::vector<std::string>& result) const = 0;

    // if arg == "@filename", open filename and return strings; else return {arg}
    virtual void argumentComposite(int index, std::vector<std::string>& result) const = 0;

    virtual ~ArgumentParser(){}
};


} // namespace msaux
} // namespace pwiz


#endif // _ARGUMENTPARSER_H_
