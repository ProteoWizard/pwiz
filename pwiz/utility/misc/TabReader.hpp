//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _TABREADER_HPP_
#define _TABREADER_HPP_


#include "Export.hpp"
#include "boost/shared_ptr.hpp"
#include <string>
#include <vector>
#include <iostream>


namespace pwiz {
namespace util {

struct PWIZ_API_DECL TabHandler
{
    virtual ~TabHandler() {}

    virtual bool getHeaders() = 0;

    virtual char useComment() const = 0;
    
    virtual bool open() = 0;
    
    virtual bool updateLine(const std::string& line) = 0;

    virtual bool updateRecord(const std::vector<std::string>& fields) = 0;

    virtual bool close() = 0;
};

class PWIZ_API_DECL DefaultTabHandler : public TabHandler
{
    public:

    DefaultTabHandler(bool need_headers = true,
                      char comment_char='#');
    
    DefaultTabHandler(const DefaultTabHandler& c);
    
    virtual ~DefaultTabHandler() {}

    virtual bool getHeaders();
    
    virtual char useComment() const;
    
    virtual bool open();
    
    virtual bool updateLine(const std::string& line);

    virtual bool updateRecord(const std::vector<std::string>& fields);

    virtual size_t columns() const;
    
    virtual size_t getHeader(const std::string& name) const;

    virtual std::string getHeader(size_t index) const;

    virtual bool close();

    protected:

    class Impl;
    boost::shared_ptr<Impl> pimpl;
};

class PWIZ_API_DECL VectorTabHandler : public DefaultTabHandler
{
    public:

    typedef std::vector< std::vector<std::string> >::const_iterator const_iterator;
    typedef std::vector< std::vector<std::string> >::iterator iterator;    

    VectorTabHandler();
    VectorTabHandler(const DefaultTabHandler& c);
    
    virtual ~VectorTabHandler() {}

    virtual const_iterator begin() const;
    virtual const_iterator end() const;
};

class PWIZ_API_DECL TabReader 
{
    public:
    TabReader();
    virtual ~TabReader() {}

    virtual void setHandler(TabHandler* handler);
    virtual const TabHandler* getHandler();

    virtual bool process(const char* filename);

    //virtual const std::vector< std::vector<std::string> >& records();

    private:

    class Impl;
    boost::shared_ptr<Impl> pimpl;
};

} // namespace pwiz
} // namespace utility

#endif // _TABREADER_HPP_

