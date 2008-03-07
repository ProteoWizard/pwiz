//
// SAXParser.hpp
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


#ifndef _SAXPARSER_HPP_
#define _SAXPARSER_HPP_


#include "boost/lexical_cast.hpp"
#include "boost/iostreams/positioning.hpp"
#include <iosfwd>
#include <string>
#include <map>


namespace pwiz {
namespace minimxml {


///
/// An extended SAX interface for custom XML stream parsing.
///
/// Use cases:
/// - read a single element
/// - read a single element, aborting on a specified tag
/// - delegate handling of a sub-element to another handler 
///
namespace SAXParser {


/// SAX event handler interface.
class Handler
{
    public:

    /// Handler returns the Status struct as a means of changing the parser's behavior.  
    struct Status
    {
        enum Flag 
        {
            Ok,       // ok, continue parsing the stream
            Done,    // abort immediately
            Delegate  // delegate this element to the specified Handler [startElement() only] 
        };

        Flag flag;
        Handler* delegate; // valid iff (flag == Delegate)

        Status(Flag _flag = Ok, 
               Handler* _delegate = 0)
        :   flag(_flag), delegate(_delegate)
        {}
    };

    typedef std::map<std::string,std::string> Attributes;
    typedef boost::iostreams::stream_offset stream_offset; 

    virtual Status processingInstruction(const std::string& name,
                                         const std::string& data,
                                         stream_offset position) {return Status::Ok;}

    virtual Status startElement(const std::string& name,
                                const Attributes& attributes,
                                stream_offset position) {return Status::Ok;}

    virtual Status endElement(const std::string& name,
                              stream_offset position) {return Status::Ok;}

    virtual Status characters(const std::string& text,
                              stream_offset position) {return Status::Ok;}

    virtual ~Handler(){}

    protected:

    template <typename T>
    void getAttribute(const Attributes& attributes,
                      const std::string& name,
                      T& result)
    {
        Attributes::const_iterator it = attributes.find(name);
        if (it != attributes.end()) 
            result = boost::lexical_cast<T>(it->second);
    }
};


///
/// Extract a single XML element from the istream, sending SAX events to the handler.
///
/// Behavior:
///
/// - Parser returns when it completes reading of the first element it encounters.
///
/// - Parser returns immediately if the Handler returns Status::Done when handling an event.
///
/// - On startElement(), Handler may delegate handling to a sub-Handler, which will receive
///   the same startElement() event.  The sub-Handler pointer will remain on the parser's 
///   Handler stack until it handles the corresponding endElement().  Caution: The sub-Handler 
///   pointer must remain valid while it is on the Handler stack, so it cannot point to
///   a local object that goes out of scope when Handler::startElement() returns. 
///
/// Notes:
/// - Start tags with end marker '/' generate two events, e.g. <br/> will generate events
///   startElement("br", ...) and endElement("br").
///
void parse(std::istream& is, Handler& handler);


} // namespace SAXParser 
} // namespace minimxml 
} // namespace pwiz


#endif // _SAXPARSER_HPP_


