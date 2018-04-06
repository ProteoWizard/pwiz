//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#ifndef _XMLWRITER_HPP_
#define _XMLWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/iostreams/positioning.hpp"
#include "boost/iostreams/filter/counter.hpp"
#include <iosfwd>
#include <string>
#include <vector>


namespace pwiz {
namespace minimxml {


///
/// The XMLWriter class provides simple, tag-level XML syntax writing.
/// Internally, XMLWriter keeps a style stack (for client customization
/// of the XML style) and an element stack (for element nesting/indentation). 
///
class PWIZ_API_DECL XMLWriter
{
    public:

    /// flags to control the XML writing style
    enum PWIZ_API_DECL StyleFlag
    {
        StyleFlag_InlineInner = 0x01, // no whitespace within an element 
        StyleFlag_InlineOuter = 0x02, // no whitespace around an element
        StyleFlag_Inline = StyleFlag_InlineInner | StyleFlag_InlineOuter,
        StyleFlag_AttributesOnMultipleLines = 0x04
    };

    /// interface to allow outside observation of data sent to output stream 
    class PWIZ_API_DECL OutputObserver
    {
        public:
        virtual void update(const std::string& output) = 0;
        virtual ~OutputObserver(){}
    };

    /// initial configuration of the XMLWriter
    struct PWIZ_API_DECL Config
    {
        unsigned int initialStyle;
        unsigned int indentationStep;
        OutputObserver* outputObserver;

        Config()
        :   initialStyle(0), indentationStep(2), outputObserver(0)
        {}
    };

    /// vector of name/value pairs to be written as XML attributes
    class PWIZ_API_DECL Attributes : public std::vector< std::pair<std::string,std::string> >
    {
        public:
        void add(const std::string& name, const double& value);
        void add(const std::string& name, const int& value);

        template <typename T>
        inline void add(const std::string& name, const T& value)
        {
            push_back(make_pair(name, boost::lexical_cast<std::string>(value)));
        }
    };

    /// constructor
    XMLWriter(std::ostream& os, const Config& config = Config());
    virtual ~XMLWriter() {}

    /// pushes style flags onto the internal style stack
    void pushStyle(unsigned int flags);

    /// pops the style stack
    void popStyle();

    /// writes a processing instruction
    void processingInstruction(const std::string& name, const std::string& data);

    /// tag for indicating an empty element
    enum EmptyElementTag {NotEmptyElement, EmptyElement};

    /// writes element start tag
    void startElement(const std::string& name, 
                      const Attributes& attributes = Attributes(),
                      EmptyElementTag emptyElementTag = NotEmptyElement);

    /// writes element end tag
    void endElement();

    /// writes character data;
    /// autoEscape writes reserved XML characters in the input text in their escaped form
    /// '&', '<', and '>' are '&amp;', '&lt;', '&gt;' respectively
    void characters(const std::string& text, bool autoEscape = true);

    typedef boost::iostreams::stream_offset stream_offset;

    /// returns current stream position
    stream_offset position() const;

    /// returns stream position of next element start tag 
    stream_offset positionNext() const;


    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    XMLWriter(const XMLWriter&);
    XMLWriter& operator=(const XMLWriter&);
};


/// Encodes any characters not suitable in an xml:ID or xml:IDREF
/// with their hexadecimal value, e.g. " " encodes as "_x0020_"
/// This override modifies the input string in place and returns its reference.
PWIZ_API_DECL std::string& encode_xml_id(std::string& str);


/// Encodes any characters not suitable in an xml:ID or xml:IDREF
/// with their hexadecimal value, e.g. " " encodes as "_x0020_"
/// This override modifies and returns a copy of the input string.
PWIZ_API_DECL std::string encode_xml_id_copy(const std::string& str);


//
// Template name: basic_charcounter.
// Template paramters:
//      Ch - The character type.
// Description: Filter which counts characters.  
// Based on boost's basic_counter, but
// without the line counting, and couting using 
// stream_offset instead of int
//
template<typename Ch>
class basic_charcounter  {
public:
    typedef Ch char_type;
    struct category
		: boost::iostreams::dual_use,
          boost::iostreams::filter_tag,
          boost::iostreams::multichar_tag,
          boost::iostreams::optimally_buffered_tag
        { };
    explicit basic_charcounter(int first_char = 0)
        : chars_(first_char)
        { }
    boost::iostreams::stream_offset characters() const { return chars_; }
    std::streamsize optimal_buffer_size() const { return 0; }

    template<typename Source>
    std::streamsize read(Source& src, char_type* s, std::streamsize n)
    {
		std::streamsize result = boost::iostreams::read(src, s, n);
        if (result == -1)
            return -1;
        chars_ += result;
        return result;
    }

    template<typename Sink>
    std::streamsize write(Sink& snk, const char_type* s, std::streamsize n)
    {
		std::streamsize result = boost::iostreams::write(snk, s, n);
        chars_ += result;
        return result;
    }
private:
    boost::iostreams::stream_offset chars_;
};
BOOST_IOSTREAMS_PIPABLE(basic_charcounter, 1)


typedef basic_charcounter<char>     charcounter;
typedef basic_charcounter<wchar_t>  wcharcounter;

} // namespace minimxml
} // namespace pwiz


#endif // _XMLWRITER_HPP_ 

