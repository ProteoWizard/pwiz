//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#ifndef _MSDATAANALYZER_HPP_
#define _MSDATAANALYZER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <iosfwd>


namespace pwiz {
namespace analysis {


using namespace msdata;


///
/// Interface for MSData analyzers.
///
/// MSDataAnalyzer encapsulates a passive update strategy.  The MSDataAnalyzer expects to  
/// handle events generated from an outside driver.  This allows the driver to 
/// control access to the MSData object -- in particular, the driver can ensure
/// that scans are read from file only once.
///
/// Event sequence: 
///   - open
///   - loop: 
///     - updateReqested
///     - update
///   - close
///
/// UpdateRequest_Ok handles the following use case: a spectrum cache wants to cache 
/// only those spectra that are requested by other MSDataAnalyzers; it won't request 
/// any updates, but it needs to see any update requested by someone else.
///
class PWIZ_API_DECL MSDataAnalyzer
{
    public:

    /// information about the data to be analyzed
    struct PWIZ_API_DECL DataInfo
    {
        const MSData& msd;
        std::string sourceFilename;
        std::string outputDirectory;
        std::ostream* log;

        DataInfo(const MSData& _msd) : msd(_msd), log(0) {}
    };

    enum PWIZ_API_DECL UpdateRequest
    {
        UpdateRequest_None,      // do not update
        UpdateRequest_Ok,        // will accept an update
        UpdateRequest_NoBinary,  // update requested, no binary data needed 
        UpdateRequest_Full       // update requested, with binary data 
    };

    /// \name Event Handling 
    //@{

    /// start analysis of the data
    virtual void open(const DataInfo& dataInfo) {}

    /// ask analyzer if it wants an update
    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const 
    {   
        return UpdateRequest_None;
    }

    /// analyze a single spectrum
    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum) {}

    /// end analysis of the data
    virtual void close(const DataInfo& dataInfo) {} 
    //@}

    virtual ~MSDataAnalyzer() {}
};


typedef boost::shared_ptr<MSDataAnalyzer> MSDataAnalyzerPtr;


/// This auxilliary class should be specialized for MSDataAnalyzers
/// whose instantiation is controlled by user-supplied strings 
/// (via command line, config file, etc.).
template <typename analyzer_type>
struct analyzer_strings
{
    /// string identifier for the analyzer
    static const char* id() {return "analyzer_traits not specialized";}

    /// description of the analyzer
    static const char* description() {return typeid(analyzer_type).name();} 

    /// format of args string
    static const char* argsFormat() {return "";}

    /// description of args string options
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


/// 
/// container of MSDataAnalyzer (composite pattern)
///
class PWIZ_API_DECL MSDataAnalyzerContainer : public MSDataAnalyzer,
                                              public std::vector<MSDataAnalyzerPtr>
{
    public:

    /// \name MSDataAnalyzer interface 
    //@{
    virtual void open(const DataInfo& dataInfo);

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);

    virtual void close(const DataInfo& dataInfo);
    //@}
};


///
/// event generator for MSDataAnalyzer
///
class PWIZ_API_DECL MSDataAnalyzerDriver
{
    public:

    /// instantiate with an MSDataAnalyzer
    MSDataAnalyzerDriver(MSDataAnalyzer& analyzer);

    enum PWIZ_API_DECL Status {Status_Ok, Status_Cancel};

    /// progress callback interface
    class PWIZ_API_DECL ProgressCallback
    {
        public:
        virtual size_t iterationsPerCallback() const {return 100;}
        virtual Status progress(size_t index, size_t size) {return Status_Ok;}
        virtual ~ProgressCallback(){}
    };

    ///
    /// analyze a single MSData object, calling back to client if requested
    /// 
    /// If progressCallback->progress() returns Status_Cancel, analysis
    /// is canceled and Status_Cancel is returned.
    ///
    Status analyze(const MSDataAnalyzer::DataInfo& dataInfo,
                   ProgressCallback* progressCallback = 0) const;

    private:
    MSDataAnalyzer& analyzer_;
};

// helper function for argument parsing
// return true iff text contains desiredArg followed by '=' and
// a range of form [a,b] or [a] or a,b or a or [a-b] or a-b
// iff true then populates result 
template <typename value_type>
bool parseRange(const std::string &desiredArg, const std::string& text, std::pair<value_type,value_type>& result, const std::string& callerName)
{
    if (!text.compare(0,desiredArg.size()+1,desiredArg+"="))
    {
        std::string val = text.substr(desiredArg.size()+1);
        std::string::size_type indexPairSeperator = val.find(',');
        if (std::string::npos == indexPairSeperator)
        {   // no comma, perhaps a dash instead?
            indexPairSeperator = val.find('-');
            if (0==indexPairSeperator) // assume that's just a negative value
            {
                indexPairSeperator = string::npos;
            }
        }
        int bracket =  (val[0] == '[')?1:0;
        if (val.empty() ||
           ((bracket!=0) && val[val.size()-1] != ']'))
        {
            std::cerr << "[" << callerName << "] Unable to parse range: " << text << endl;
            return false;
        }
    
        try
        {
            if (std::string::npos == indexPairSeperator) 
            {   // form "<start>", read as "<start>-<start>"
                std::string first = val.substr(bracket,val.size()-(2*bracket));
                result.first = result.second = lexical_cast<value_type>(first);
            }
            else
            {   // form "<start>-<end>" or "<start>-" 
                std::string first = val.substr(bracket, indexPairSeperator-bracket);
                std::string second = val.substr(indexPairSeperator+1, val.size()-indexPairSeperator-(1+bracket));
                result.first = lexical_cast<value_type>(first);
                if (second.size()) // form "<start>-<end>"
                    result.second = lexical_cast<value_type>(second);
                else  // form "<start>-", assume that's "<start>-maxval"
                    result.second = numeric_limits<value_type>::max();
            }
            return true;
        }
        catch (boost::bad_lexical_cast&)
        {
            std::cerr << "[" << callerName << "] Unable to parse range: " << text << endl;
        }
    }

    return false;
}
template <typename value_type>
bool parseValue(const std::string &desiredArg, const std::string& text, value_type& result, const std::string& callerName)
{
    if (!text.compare(0,desiredArg.size()+1,desiredArg+"="))
    {
        std::string val = text.substr(desiredArg.size()+1);
        if (val.empty())
        {
            std::cerr << "[" << callerName << "] Unable to parse value: " << text << endl;
            return false;
        }
    
        try
        {
            result = lexical_cast<value_type>(val);
            return true;
        }
        catch (boost::bad_lexical_cast&)
        {
            std::cerr << "[" << callerName << "] Unable to parse value: " << text << endl;
        }
    }

    return false;
}

} // namespace analysis 
} // namespace pwiz


#endif // _MSDATAANALYZER_HPP_

