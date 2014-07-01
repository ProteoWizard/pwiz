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
// Copyright 2014 Vanderbilt University
//
// Contributor(s):


#ifndef _TOTALCOUNTS_HPP_
#define _TOTALCOUNTS_HPP_


#include "sqlite3.h"
#include <boost/scoped_ptr.hpp>


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE

class TotalCounts
{
    public:

    TotalCounts(sqlite3* idpDbConnection);
    ~TotalCounts();

    int clusters() const;
    int proteinGroups() const;
    int proteins() const;
    int distinctPeptides() const;
    int distinctMatches() const;
    sqlite3_int64 filteredSpectra() const;
    double proteinFDR() const;
    double peptideFDR() const;
    double spectrumFDR() const;

    private:
    class Impl;
    boost::scoped_ptr<Impl> _impl;
};

END_IDPICKER_NAMESPACE


#endif // _TOTALCOUNTS_HPP_
