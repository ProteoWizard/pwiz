//
// XMLWriter.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "boost/shared_ptr.hpp"
#include "boost/iostreams/positioning.hpp"
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
class XMLWriter
{
    public:

    /// flags to control the XML writing style
    enum StyleFlag
    {
        StyleFlag_InlineInner = 0x01, // no whitespace within an element 
        StyleFlag_InlineOuter = 0x02, // no whitespace around an element
        StyleFlag_Inline = StyleFlag_InlineInner | StyleFlag_InlineOuter,
        StyleFlag_AttributesOnMultipleLines = 0x04
    };

    /// interface to allow outside observation of data sent to output stream 
    class OutputObserver
    {
        public:
        virtual void update(const std::string& output) = 0;
        virtual ~OutputObserver(){}
    };

    /// initial configuration of the XMLWriter
    struct Config
    {
        unsigned int initialStyle;
        unsigned int indentationStep;
        OutputObserver* outputObserver;

        Config()
        :   initialStyle(0), indentationStep(2), outputObserver(0)
        {}
    };

    /// constructor
    XMLWriter(std::ostream& os, const Config& config = Config());

    /// pushes style flags onto the internal style stack
    void pushStyle(unsigned int flags);

    /// pops the style stack
    void popStyle();

    /// writes a processing instruction
    void processingInstruction(const std::string& name, const std::string& data);

    typedef std::vector< std::pair<std::string,std::string> > Attributes;

    /// tag for indicating an empty element
    enum EmptyElementTag {NotEmptyElement, EmptyElement};

    /// writes element start tag
    void startElement(const std::string& name, 
                      const Attributes& attributes = Attributes(),
                      EmptyElementTag emptyElementTag = NotEmptyElement);

    /// writes element end tag
    void endElement();

    /// writes character data
    void characters(const std::string& text);

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
    

} // namespace minimxml
} // namespace pwiz


#endif // _XMLWRITER_HPP_ 

