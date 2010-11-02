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
// Contributor(s): Surendra Dasari
//


#ifndef _QONVERTER_HPP_
#define _QONVERTER_HPP_


#include <vector>
#include <map>
#include <utility>
#include <string>
#include "../Lib/SQLite/sqlite3.h"


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


struct Qonverter
{
    enum QonverterMethod
    {
        QonverterMethod_StaticWeighted,
        QonverterMethod_OptimizedMonteCarlo,
        QonverterMethod_OptimizedPercolator
    };

    struct Settings
    {
        enum NormalizationMethod
        {
            NormalizationMethod_Off,
            NormalizationMethod_Quantile,
            NormalizationMethod_Linear
        };

        enum Order
        {
            Order_Ascending,
            Order_Descending
        };

        struct ScoreInfo
        {
            double weight;
            NormalizationMethod normalizationMethod;
            Order order;
        };

        QonverterMethod qonverterMethod;
        string decoyPrefix;
        bool rerankMatches;
        map<string, ScoreInfo> scoreInfoByName;
    };

    map<int, Settings> settingsByAnalysis;
    bool logQonversionDetails;

    Qonverter();

    struct ProgressMonitor
    {
        struct UpdateMessage
        {
            int QonvertedAnalyses;
            int TotalAnalyses;
            bool Cancel;
        };

        virtual void operator() (UpdateMessage& updateMessage) const {};
    };

    void Qonvert(const string& idpDbFilepath, const ProgressMonitor& progressMonitor = ProgressMonitor());
    void Qonvert(sqlite3* idpDb, const ProgressMonitor& progressMonitor = ProgressMonitor());
};


END_IDPICKER_NAMESPACE


#endif // _QONVERTER_HPP_
