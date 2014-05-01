//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//


#ifndef _MERGER_HPP_
#define _MERGER_HPP_

#include <vector>
#include <map>
#include <utility>
#include <string>
#include <boost/scoped_ptr.hpp>
#include "sqlite3.h"


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE


using std::vector;
using std::map;
using std::pair;
using std::string;


/// an object responsible for merging two or more idpDBs into a single output idpDB
struct Merger
{
    Merger();
    ~Merger();

    struct ProgressMonitor
    {
        struct UpdateMessage
        {
            int mergedFiles;
            int totalFiles;
            string message;
            bool cancel;
        };

        virtual void operator() (UpdateMessage& updateMessage) const {};
    };

    void merge(const string& mergeTargetFilepath, const std::vector<string>& mergeSourceFilepaths, int maxThreads = 8, const ProgressMonitor& progressMonitor = ProgressMonitor());
    void merge(const string& mergeTargetFilepath, sqlite3* mergeSourceConnection, const ProgressMonitor& progressMonitor = ProgressMonitor());

    struct Impl;

    private:
    boost::scoped_ptr<Impl> _impl;
};

END_IDPICKER_NAMESPACE


#endif // _MERGER_HPP_
