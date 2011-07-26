//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//


#ifndef _PARSER_HPP_
#define _PARSER_HPP_


#include <string>
#include <vector>
#include <map>
#include "Qonverter.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include <boost/date_time.hpp>


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE


using std::string;
using std::vector;
using std::map;
using std::pair;


/// TODO
struct Parser
{
    /// top-level summary of an imported result set 
    struct Analysis
    {
        Analysis();

        string name;
        string softwareName;
        string softwareVersion;
        boost::local_time::local_date_time startTime;
        map<string, string> parameters;
        vector<string> filepaths;

        struct ImportSettings
        {
            string proteinDatabaseFilepath;
            Qonverter::Settings qonverterSettings;
        };

        mutable ImportSettings importSettings;
    };

    typedef boost::shared_ptr<Analysis> AnalysisPtr;
    typedef boost::shared_ptr<const Analysis> ConstAnalysisPtr;


    /// after determining distinct analyses in the input file(s), the client is
    /// asked to provide database filepath(s) and qonversion settings
    struct ImportSettingsCallback
    {
        /// an analysis is distinct if its name is unique and it has at least one distinct parameter
        virtual void operator() (const vector<ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const;
    };

    typedef boost::shared_ptr<ImportSettingsCallback> ImportSettingsCallbackPtr;


    ImportSettingsCallbackPtr importSettingsCallback;


    void parse(const vector<string>& inputFilepaths, int maxThreads = 8, pwiz::util::IterationListenerRegistry* ilr = 0) const;

    void parse(const string& inputFilepath, int maxThreads = 8, pwiz::util::IterationListenerRegistry* ilr = 0) const;
	
	static string parseSource(const string& inputFilepath);
};


END_IDPICKER_NAMESPACE


#endif // _PARSER_HPP_
