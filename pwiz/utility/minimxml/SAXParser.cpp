//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Reworked for zero-copy performance by Brian Pratt, Insilicos LLC
// those changes Copyright 2011 Insilicos LLC All Rights Reserved
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

#define PWIZ_SOURCE

#include "SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"


namespace bxp = boost::xpressive;


const string CDATA_begin("![CDATA["), CDATA_end("]]");
const string comment_begin("!--"), comment_end("--");
const string DOCTYPE_begin("!DOCTYPE");

#ifdef _DEBUG_READCOUNT 
// in case you want to verify that you aren't doing excessive reads
static boost::iostreams::stream_offset bytesin = 0;
void note_read_count(size_t n) 
{ // collect some stats
    bytesin += n;
}
#endif


namespace pwiz {
namespace minimxml {
namespace SAXParser {
    

const char* ws = " \n\r\t";

size_t count_trail_ws(const char *data,size_t len) 
{ // remove trailing whitespace if any
    size_t n=len;
    while (n && strchr(ws,data[n-1])) 
        n--;
    return len-n;
}

namespace {

inline bool unbalanced_quotes(const saxstring & buffer) 
{
    // find next single or double quote
    for (const char *c=buffer.c_str();(c=strpbrk(c,"\"'"))!=NULL;c++) 
    {
        c = strchr(c+1,*c); // find matching quote
        if (!c) 
            return true; // unmatched quote
    }
    return false;
}

// returns number of ws chars it had to eat on front end
// returns -1 if nothing read
static int eat_whitespace(istream& is) 
{
    char c;
    int lead_ws=0;
    while (is.good()) 
    {    // loop while extraction from file is possible
        c = is.get();       // get character from file
        if (is.good()) 
        {
            if (strchr(ws,c)) 
                lead_ws++; // eat the whitespace
            else 
            {
                is.unget();
                break; // no more whitespace
            }
        } 
        else 
            break;
    }
#ifdef _DEBUG_READCOUNT
    note_read_count(lead_ws); // collect some stats
#endif
    if (is.good()) 
        return lead_ws;
    else 
        return -1;
}


struct ProcessingInstruction 
{
    string name;
    string value; 

    ProcessingInstruction(const string& buffer)
    {
        istringstream iss(buffer);
        char questionMark = '\0';
        iss >> questionMark >> name;
        if (questionMark != '?') throw runtime_error("[SAXParser::ProcessingInstruction] Error.");
        eat_whitespace(iss);
        getline(iss, value, '?');
        value.resize(value.length()-count_trail_ws(value.c_str(),value.length()));
    }
};

const char * quote_ = "\"\'";

//
// zero-copy StartTag - it hacks up the 
// saxparser string into name-value pairs
// instead of making lots of little std::strings
// do that a few bazillion times and it makes
// a big difference in performance - bpratt
//
struct StartTag
{
    bool end;
    Handler::Attributes attributes;

    StartTag(saxstring &str, bool unescapeAttributes)
    :   end(str.length() && str[str.length()-1]=='/'), // evaluate str before attributes hacks it up
        attributes(str,unescapeAttributes)
    {
        if (!str.length()) 
            throw runtime_error("[SAXParser::StartTag] Empty buffer.");
    }

    const char *getName() {
        return attributes.getTagName(); // reads from hacked-up input str
    }


};



struct HandlerInfo
{
    Handler& handler;
    stack<string> names;

    HandlerInfo(Handler& _handler)
    :   handler(_handler)
    {}
};


// HandlerWrangler responsibilities:
// - maintain a Handler stack
// - validate return Status from Handler calls
// - validate element start/end tag matching
class HandlerWrangler : public SAXParser::Handler
{
    public:

    HandlerWrangler(Handler& root)
    {
        handlers_.push(root);
    }

    void verifyNoDelegate(const Status& status)
    {
        if (status.flag==Status::Delegate || status.delegate)
            throw runtime_error("[SAXParser] Illegal return of Status::Delegate.");
    }

    virtual Status processingInstruction(const string& name,
                                         const string& data,
                                         stream_offset position)
    {
        Status status = handlers_.top().handler.processingInstruction(name, data, position);
        verifyNoDelegate(status); 
        return status;
    }

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        HandlerInfo& top = handlers_.top();
         
        // element start/end validation

        top.names.push(name);

        // call handler

        Handler::Status status = top.handler.startElement(name, attributes, position);
        if (status.flag != Handler::Status::Delegate)
            return status; 

        // Status::Delegate: let status.delegate handle this message 

        if (!status.delegate) throw runtime_error("[SAXParser] Null delegate.");
        top.names.pop();            
        handlers_.push(*status.delegate);
        return startElement(name, attributes, position);
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        HandlerInfo& top = handlers_.top();

        // element start/end validation
        if (top.names.empty() || top.names.top()!=name) 
            throw runtime_error("[SAXParser::ParserWrangler::elementEnd()] Illegal end tag \"" + name + "\" at offset " + lexical_cast<string>(position) + "."); 

        top.names.pop();

        // call handler

        Status status = top.handler.endElement(name, position);
        verifyNoDelegate(status); 

        // delete handler if we're done with it 

        if (top.names.empty())
        {
            handlers_.pop();
            if (handlers_.empty()) return Status::Done;
        }

        return status;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        Status status = topHandler().characters(text, position);
        verifyNoDelegate(status);
        return status;
    }

    const Handler& topHandler() const {return handlers_.top().handler;}
    Handler& topHandler() {return handlers_.top().handler;}

    private:
    stack<HandlerInfo> handlers_;
};


} // namespace

void Handler::Attributes::parseAttributes(string::size_type& index) const
{
    if (!attrs.size()) 
    {   // first access
        // avoid lots of reallocs
        int n_equals=0;
        for (const char *p=textbuff+index;(p=strchr(p,'='))!=NULL;p++) 
            n_equals++;
        attrs.resize(n_equals); // might be more than we need, but we'll correct
        int nattrs = 0;
        while (index < index_end) 
        {
            
            string::size_type indexNameBegin = index;
            string::size_type indexNameEnd = indexNameBegin;
            string::size_type indexQuoteOpen;
            string::size_type indexQuoteClose;
            const char *eq=strchr(textbuff+indexNameBegin,'=');
            if (eq)
            {
                indexNameEnd = eq-textbuff;
                const char *c=textbuff+indexNameEnd+1;
                while (*c && !strchr(quote_,*c)) c++;
                indexQuoteOpen = (c-textbuff);
                char quoteChar = *c;
                const char *q = strchr(textbuff+indexQuoteOpen+1,quoteChar);
                indexQuoteClose = q?(q-textbuff):string::npos;
            }
            else 
                indexQuoteClose = string::npos;
            if (indexQuoteClose == string::npos) 
            { // this index can only be OK if the others are too
                if ('/'==textbuff[indexNameBegin])
                { // end of tag
                    index++; 
                    break;
                }
                else if ('\0'==textbuff[indexNameBegin])
                {
                    break;
                }
                throw runtime_error("[SAXParser::parseAttribute()] Error at index "
                + lexical_cast<string>(index) + ":\n" + textbuff);
            }
            while (strchr(ws,textbuff[indexNameEnd-1])) 
                indexNameEnd--; // work back from = to end of name
            textbuff[indexNameEnd]=0; // null terminate in-place
            textbuff[indexQuoteClose]=0; // null terminate in-place
            attrs[nattrs++].set(textbuff+indexNameBegin,textbuff+indexQuoteOpen+1,autoUnescape);

            index = indexQuoteClose+1; // ready for next round
            while (textbuff[index] && strchr(ws,textbuff[index]))  // eat whitespace
                index++;
        }
        attrs.resize(nattrs);
    }
}


void unescapeXML(std::string &str) 
{
    if (std::string::npos != str.find('&')) 
    {
        SAXParser::saxstring s(str);
        s.unescapeXML();
        str = s.c_str();
    }
}


void unescapeXML(char *str)
{
    char *amp;
    size_t end=strlen(str);
    for (size_t i=0 ; (amp=strchr(str+i,'&'))!=NULL ; i++) 
    {
        i = (amp-str);

        // there must be at least three characters after '&' (&lt; or &gt;)
        if (i+3 >= end)
            throw runtime_error("[SAXParser::unescapeXML] Invalid escape sequence \"" + string(str) + "\".");

        int adjustlen=0;

        if (str[i+1] == 'l' && str[i+2] == 't' && str[i+3] == ';')
        {
            *amp = '<';
            adjustlen=3;
        }
        else if (str[i+1] == 'g' && str[i+2] == 't' && str[i+3] == ';')
        {
            *amp = '>';
            adjustlen = 3;
        }
        else if (i+4 < end && str[i+1] == 'a' && str[i+2] == 'm' && str[i+3] == 'p' && str[i+4] == ';')
        {
            *amp = '&';
            adjustlen = 4;
        }
        else if (i+5 < end && str[i+1] == 'q' && str[i+2] == 'u' && str[i+3] == 'o' && str[i+4] == 't' && str[i+5] == ';')
        {
            *amp = '"';
            adjustlen = 5;
        }
        else if (i+5 < end && str[i+1] == 'a' && str[i+2] == 'p' && str[i+3] == 'o' && str[i+4] == 's' && str[i+5] == ';')
        {
            *amp = '\'';
            adjustlen = 5;
        }
        else if (i+3 < end && str[i+1] == '#') // numeric character entities &#0; to &#127; (decimal) or &#x0; to &#x7f; (hex)
        {
            size_t j = i+3;
            while (str[j] != ';')
            {
                if (++j >= i+6 || j >= end)
                    throw runtime_error("[SAXParser::unescapeXML] Invalid escape sequence.");
            }
            size_t entitylen = j-i-2;
            char *entitybegin = str+i+2;
            int entitybase = 10;
            if (str[i+2] == 'x') // hex
            {
                --entitylen;
                ++entitybegin;
                entitybase = 16;
            }
            char *entitystr = (char*)malloc(entitylen);
            strncpy(entitystr, entitybegin, entitylen);
            entitystr[entitylen] = '\0';
            char *convertend;
            long int entity = strtol(entitystr, &convertend, entitybase);
            bool success = entitystr != convertend;
            free(entitystr);
            if (success && 0 <= entity && entity <= 127)
            {
                *amp = (char)entity;
                adjustlen = j-i;
            }
            else
                throw runtime_error("[SAXParser::unescapeXML] Invalid escape sequence.");
        }
        else
            throw runtime_error("[SAXParser::unescapeXML] Invalid escape sequence.");
        memmove(amp+1,amp+adjustlen+1,(end-(i+adjustlen)));
        end -= adjustlen;
    }
}

// (nearly)zero-copy getline
static bool getline(istream& is, saxstring &vec, char delim, bool append = false) 
{
    const size_t minbuf = 1024;
    size_t begin = append?vec.length():0;
    size_t end = begin;
    while (is.good()) 
    {
        if (vec.capacity() < minbuf + (begin+3)) 
        {
            size_t newsize = 2* ( vec.capacity() ? vec.capacity() : minbuf );
            vec.resize(newsize);
        }
        char *buffer = &vec[0];
        // always guarantee room for readahead and nullterm at end of buffer
        is.get(buffer+begin, vec.capacity()-(begin+3), delim); // keeps delim if read
        size_t nread = (size_t)is.gcount();
        if (!nread && !is.eof()) // empty line?
            is.clear(); // clear the failbit
        end += nread;
#ifdef _DEBUG_READCOUNT
        note_read_count(nread+1); // collect some stats
#endif
        // did we stop reading because we hit delimiter?
        char c=0;
        is.get(c);
        if (delim == c) 
        { // full read
            vec.resize(end); // so we don't copy more than we need
            return true;
        } 
        else if (c) 
        { // ran out of room
            buffer[end++] = c;
            buffer[end] = 0;
            begin = end;
        }
    }
    return false;
}

//
// parse() responsibilities: 
// - stream parsing
// - initiation of events
//   - events are routed through a HandlerWrangler
//   - HandlerWrangler handles any XML/Handler validation
// - return on Handler::Status::Done
//
PWIZ_API_DECL void parse(istream& is, Handler& handler)
{
    using boost::iostreams::position_to_offset;

    HandlerWrangler wrangler(handler);
    Handler::stream_offset position = position_to_offset(is.tellg());
    saxstring buffer(16384); // hopefully big enough to avoid realloc

    while (is)
    {

        // read text up to next tag (may be empty)
        buffer.clear();
        if (!getline(is, buffer, '<')) break;
        size_t lead_ws = buffer.trim_lead_ws(); 
        // remove trailing ws
        buffer.trim_trail_ws();
        // position == beginning of characters

        position += lead_ws; 

        // TODO: is it possible to detect when Handler::characters() has been overridden?
        const Handler& topHandler = wrangler.topHandler();
        if (buffer.length() && topHandler.parseCharacters)
        {
            if (topHandler.autoUnescapeCharacters)
                buffer.unescapeXML();
            Handler::Status status = wrangler.characters(buffer, position);
            if (status.flag == Handler::Status::Done) return;
        }

        // position == beginning of tag

        position = position_to_offset(is.tellg());
        if (position > 0) position--;

        // read tag

        bool inCDATA;
        buffer.clear();

        while (true)
        {
            bool firstpass = (!buffer.length());
            if (!getline(is,buffer, '>',true))  // append
                break;
            if (firstpass) 
                buffer.trim_lead_ws();
            inCDATA = buffer.starts_with(CDATA_begin.c_str());

            // If in CDATA, fetch more until the section is ended;
            // else deal with the unlikely but still legal case
            // <FifthElement leeloo='>Leeloo > mul"-ti-pass'>
            //        You're a monster, Zorg.>I know.
            //     </FifthElement>
            if (inCDATA ? !buffer.ends_with(CDATA_end.c_str()) :
                   unbalanced_quotes(buffer)) 
                buffer += ">"; // put back that char we ate, go for more
             else 
                break;
        }
        
        // remove trailing ws
        buffer.trim_trail_ws();
        if (!buffer.length())
            throw runtime_error("[SAXParser::parse()] Empty tag at offset " + lexical_cast<string>(position) + "."); 

        // switch on tag type

        switch (buffer[0])
        {
            case '?':
            {
                ProcessingInstruction pi(buffer.c_str());
                Handler::Status status = wrangler.processingInstruction(pi.name, pi.value, position);
                if (status.flag == Handler::Status::Done) return;
                break; 
            }
            case '/':
            {
                Handler::Status status = wrangler.endElement(buffer.c_str()+1, position);
                if (status.flag == Handler::Status::Done) return;
                break;
            }
            case '!':
            {
                if (inCDATA)
                {
                    std::string buf(buffer.c_str());
                    Handler::Status status = wrangler.characters(buf.substr(CDATA_begin.length(), buffer.length()-CDATA_begin.length()-CDATA_end.length()), position);
                    if (status.flag == Handler::Status::Done) return;
                }
                else if (!buffer.starts_with(DOCTYPE_begin.c_str()) && (!buffer.starts_with("!--") || !buffer.ends_with("--")))
                    throw runtime_error("[SAXParser::parse()] Illegal comment \"" + string(buffer.c_str()) + "\" at offset " + lexical_cast<string>(position) + ".");
                break;
            }
            default: 
            {
                StartTag tag(buffer, handler.autoUnescapeAttributes);

                Handler::Status status = wrangler.startElement(tag.getName(), tag.attributes, position);
                if (status.flag == Handler::Status::Done) return;
                
                if (tag.end) 
                {
                    status = wrangler.endElement(tag.getName(), position);
                    if (status.flag == Handler::Status::Done) return;
                }
            }
        }

        // position == after tag end
        position = position_to_offset(is.tellg());
    }
}


} // namespace SAXParser



string xml_root_element(const string& fileheader)
{
    // TODO: make this static again when we switch to a proper C++11 compiler (e.g. VC++ 2015)
    const /*static*/ bxp::sregex e = bxp::sregex::compile("<\\?xml.*?>.*?<([^?!]\\S+?)[\\s>]");

    // convert Unicode to ASCII
    string asciiheader;
    asciiheader.reserve(fileheader.size());
    BOOST_FOREACH(char c, fileheader)
    {
        if(c > 0)
            asciiheader.push_back(c);
    }

    bxp::smatch m;
    if (bxp::regex_search(asciiheader, m, e))
        return m[1];
    throw runtime_error("[xml_root_element] Root element not found (header is not well-formed XML)");
}

string xml_root_element(istream& is)
{
    char buf[513];
    is.read(buf, 512);
    buf[512] = 0;
    return xml_root_element(buf);
}

string xml_root_element_from_file(const string& filepath)
{
    pwiz::util::random_access_compressed_ifstream file(filepath.c_str());
    if (!file)
        throw runtime_error("[xml_root_element_from_file] Error opening file");
    return xml_root_element(file);
}


namespace { bool isalnum(char& c) {return std::isalnum(c, std::locale::classic());} }


PWIZ_API_DECL string& decode_xml_id(string& str)
{
    std::istringstream parser;
    for (size_t i=0; i < str.length(); ++i)
    {
        size_t found = str.find("_x00");
        if (found != string::npos &&
            found+6 < str.length() &&
            isalnum(str[found+4]) &&
            isalnum(str[found+5]) &&
            str[found+6] == '_')
        {
            parser.clear(); // reset state
            parser.str(str.substr(found+4, 2));
            int value;
            parser >> std::hex >> value;
            char decoded = (char) value;
            str.replace(found, 7, &decoded, 1);
        }
        else
            break;
    }

    return str;
}


PWIZ_API_DECL string decode_xml_id_copy(const string& str)
{
    string copy(str);
    return decode_xml_id(copy);
}


} // namespace minimxml
} // namespace pwiz


