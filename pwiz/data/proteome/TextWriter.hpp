//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _PROTEOME_TEXTWRITER_HPP_
#define _PROTEOME_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace proteome {


class PWIZ_API_DECL TextWriter
{
    public:

    TextWriter(std::ostream& os, int depth = 0)
    :   os_(os), depth_(depth), indent_(depth*2, ' ')
    {}

    TextWriter child() {return TextWriter(os_, depth_+1);}

    TextWriter& operator()(const std::string& text)
    {
        os_ << indent_ << text << std::endl;
        return *this;
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const std::vector<object_type>& v)
    {
        (*this)(label);
        for_each(v.begin(), v.end(), child());
        return *this;
    }


    TextWriter& operator()(const ProteomeData& pd, bool metadata_only=false)
    {
        (*this)("ProteomeData:");
        child()
            ("id: " + pd.id);

        if (pd.proteinListPtr.get())
            child()(*pd.proteinListPtr, metadata_only);

        return *this;
    }

    TextWriter& operator()(const Protein& p)
    {
        (*this)("protein:");
        child()
            ("id: " + p.id)
            ("index: " + p.index)
            ("description: " + p.description)
            ("sequence: " + p.sequence().substr(0, 10));
        return *this;
    }

    TextWriter& operator()(const ProteinList& proteinList, bool metadata_only=false)
    {
        std::string text("proteinList (" + boost::lexical_cast<std::string>(proteinList.size()) + " proteins)");
        if (!metadata_only)
            text += ":";

        (*this)(text);

        if (!metadata_only)
            for (size_t index = 0; index < proteinList.size(); ++index)
                child()
                    (*proteinList.protein(index, true));
        return *this;
    }

    // if no other overload matches, assume the object is a shared_ptr of a valid overloaded type
    template<typename object_type>
    TextWriter& operator()(const boost::shared_ptr<object_type>& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }


    private:
    std::ostream& os_;
    int depth_;
    std::string indent_;
};

	
} // namespace proteome
} // namespace pwiz


#endif // _PROTEOME_TEXTWRITER_HPP_
