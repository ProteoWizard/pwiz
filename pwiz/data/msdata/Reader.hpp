//
// Reader.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _READER_HPP_ 
#define _READER_HPP_ 

#include "utility/misc/Export.hpp"
#include "MSData.hpp"
#include <string>


namespace pwiz {
namespace msdata {


/// interface for file readers
class PWIZ_API_DECL Reader
{
    public:

    /// return true iff Reader can handle the file; 
    /// Reader may filter based on filename and/or head of the file
    virtual bool accept(const std::string& filename, 
                        const std::string& head) const = 0;

    /// fill in the MSData structure
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result) const = 0;

    virtual ~Reader(){}
};


typedef boost::shared_ptr<Reader> ReaderPtr;


///
/// Reader container (composite pattern).  
/// 
/// The template get<reader_type>() gives access to child Readers by type, to facilitate 
/// Reader-specific configuration at runtime. 
///
class PWIZ_API_DECL ReaderList : public Reader,
                                 public std::vector<ReaderPtr>
{
    public:

    /// returns true iff some child accepts
    virtual bool accept(const std::string& filename, 
                        const std::string& head) const; 

    /// delegates to first child that accepts
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result) const;

    /// returns pointer to Reader of the specified type
    template <typename reader_type>
    reader_type* get()
    {
        for (iterator it=begin(); it!=end(); ++it)
        {
            reader_type* p = dynamic_cast<reader_type*>(it->get());
            if (p) return p;
        }
        
        return 0;
    }

    /// returns const pointer to Reader of the specified type
    template <typename reader_type>
    const reader_type* get() const
    {
        return const_cast<ReaderList*>(this)->get<reader_type>();
    }
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_HPP_ 

