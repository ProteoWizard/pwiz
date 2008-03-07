//
// XMLWriter.cpp
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


#include "XMLWriter.hpp"
#include "boost/iostreams/positioning.hpp"
#include <iostream>
#include <stack>
#include <stdexcept>
#include <sstream>


namespace pwiz {
namespace minimxml {

	
using namespace std;
using boost::iostreams::stream_offset;


class XMLWriter::Impl
{
    public:

    Impl(ostream& os, const Config& config);
    void pushStyle(unsigned int flags);
    void popStyle(); 
    void processingInstruction(const string& name, const string& data);
    void startElement(const string& name, 
                      const Attributes& attributes,
                      EmptyElementTag emptyElementTag);
    void endElement();
    void characters(const string& text);
    stream_offset position() const;
    stream_offset positionNext() const;

    private:
    ostream& os_;
    Config config_;
    stack<string> elementStack_;
    stack<unsigned int> styleStack_;

    string indentation() const {return string(elementStack_.size()*config_.indentationStep, ' ');}
    bool style(StyleFlag styleFlag) const {return styleStack_.top() & styleFlag ? true : false;}
};


XMLWriter::Impl::Impl(ostream& os, const Config& config)
:   os_(os), config_(config)
{
    styleStack_.push(config.initialStyle);
}


void XMLWriter::Impl::pushStyle(unsigned int flags)
{
    styleStack_.push(flags);
}


void XMLWriter::Impl::popStyle() 
{
    styleStack_.pop();
    if (styleStack_.empty())
        throw runtime_error("[XMLWriter] Style stack underflow.");
}


void XMLWriter::Impl::processingInstruction(const string& name, const string& data) 
{
    ostringstream oss;
    oss << indentation() << "<?" << name << " " << data << "?>\n"; 

    if (config_.outputObserver) config_.outputObserver->update(oss.str());
    os_ << oss.str();
}


void XMLWriter::Impl::startElement(const string& name, 
                  const Attributes& attributes,
                  EmptyElementTag emptyElementTag)
{
    ostringstream oss;

    if (!style(StyleFlag_InlineOuter))
        oss << indentation();

    oss << "<" << name;

    string attributeIndentation(name.size()+1, ' ');

    for (Attributes::const_iterator it=attributes.begin(); it!=attributes.end(); ++it)
    {
        oss << " " << it->first << "=\"" << it->second << "\"";
        if (style(StyleFlag_AttributesOnMultipleLines) && (it+1)!=attributes.end())
            oss << endl << indentation() << attributeIndentation;
    }

    oss << (emptyElementTag==EmptyElement ? "/" : "") << ">";

    if (!style(StyleFlag_InlineInner) || 
        !style(StyleFlag_InlineOuter) && emptyElementTag==EmptyElement)
        oss << endl;

    if (emptyElementTag == NotEmptyElement)
        elementStack_.push(name);

    if (config_.outputObserver) config_.outputObserver->update(oss.str());
    os_ << oss.str();
}


void XMLWriter::Impl::endElement()
{
    ostringstream oss;

    if (elementStack_.empty())
        throw runtime_error("[XMLWriter] Element stack underflow.");

    string name = elementStack_.top();
    elementStack_.pop();

    if (!style(StyleFlag_InlineInner))
        oss << indentation();

    oss << "</" << name << ">";

    if (!style(StyleFlag_InlineOuter))
        oss << endl;
        
    if (config_.outputObserver) config_.outputObserver->update(oss.str());
    os_ << oss.str();
}


void XMLWriter::Impl::characters(const string& text)
{
    ostringstream oss;

    if (!style(StyleFlag_InlineInner))
        oss << indentation();

    oss << text;

    if (!style(StyleFlag_InlineInner))
        oss << endl;

    if (config_.outputObserver) config_.outputObserver->update(oss.str());
    os_ << oss.str();
}


stream_offset XMLWriter::Impl::position() const
{
    os_ << flush;
    return boost::iostreams::position_to_offset(os_.tellp()); 
}


stream_offset XMLWriter::Impl::positionNext() const
{
    boost::iostreams::stream_offset offset = position(); 
    if (!style(StyleFlag_InlineOuter))
        offset += indentation().size();
    return offset;
}


//
// XMLWriter forwarding functions 
//


XMLWriter::XMLWriter(ostream& os, const Config& config)
:   impl_(new Impl(os, config))
{}

void XMLWriter::pushStyle(unsigned int flags) {impl_->pushStyle(flags);}

void XMLWriter::popStyle() {impl_->popStyle();}

void XMLWriter::processingInstruction(const string& name, const string& data) 
{
    impl_->processingInstruction(name, data);
}

void XMLWriter::startElement(const string& name, 
                             const Attributes& attributes,
                             EmptyElementTag emptyElementTag)
{
    impl_->startElement(name, attributes, emptyElementTag);
}

void XMLWriter::endElement() {impl_->endElement();}

void XMLWriter::characters(const string& text) {impl_->characters(text);}

stream_offset XMLWriter::position() const {return impl_->position();}

stream_offset XMLWriter::positionNext() const {return impl_->positionNext();}


} // namespace minimxml
} // namespace pwiz


