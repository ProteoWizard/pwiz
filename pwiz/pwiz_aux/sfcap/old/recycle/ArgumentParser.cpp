//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
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


#include "ArgumentParser.h"
#include <fstream>
#include <iterator>
#include <stdexcept>


using namespace std;


namespace pwiz {
namespace msaux {


class ArgumentParserImpl : public ArgumentParser
{
    public:

    ArgumentParserImpl(int argc, char** argv) : argc_(argc), argv_(argv) {}
    virtual int argc() const {return argc_;}
    virtual string argument(int index) const;
    virtual void arguments(int index, int count, vector<string>& result) const;
    virtual void argumentComposite(int index, vector<string>& result) const;

    private:

    int argc_;
    char** argv_;

    void check(int index) const;
};


auto_ptr<ArgumentParser> ArgumentParser::create(int argc, char** argv)
{
    return auto_ptr<ArgumentParser>(new ArgumentParserImpl(argc, argv));
}


string ArgumentParserImpl::argument(int index) const
{
    check(index);
    return string(argv_[index]);
}


void ArgumentParserImpl::arguments(int index, int count, vector<string>& result) const
{
    check(index);
    copy(argv_+index, argv_+min(index+count,argc_), back_inserter(result));
}


void ArgumentParserImpl::argumentComposite(int index, vector<string>& result) const
{
    check(index);
    string arg = argv_[index];
    if (arg.empty()) return;

    if (arg[0] == '@')
    {
        // open the file
        string filename = arg.substr(1);
        ifstream is(filename.c_str());
        if (!is)
            throw runtime_error(string("[ArgumentParser] Unable to open file ") + filename);

        // copy the strings from the file into the vector
        copy(istream_iterator<string>(is), istream_iterator<string>(), back_inserter(result));
    }
    else
    {
        result.push_back(arg);
    }
}


void ArgumentParserImpl::check(int index) const
{
    if (index < 0 || index >= argc_)
        throw out_of_range("[ArgumentParser] Argument index out of range.");
}


} // namespace msaux
} // namespace pwiz

