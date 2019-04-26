//
// $Id: PSM.h 9228 2015-12-22 00:28:23Z kaipot $
//
//
// Original author: Brian Pratt <bspratt@u.washington.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

#pragma once

#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include <boost/algorithm/string/replace.hpp>

/**
 * \struct SmallMolMetadata
 * \brief Information peculiar to small molecules
 */
struct SmallMolMetadata {
    std::string inchiKey; ///< this is our baseline identifier
    std::string precursorAdduct; ///< should agree with charge
    std::string chemicalFormula; ///< formula without adduct
    std::string moleculeName; ///< friendly name
    std::string otherKeys; //< of form <idType:value>\t<idType:value> etc as in "CAS:58-08-2\tinchi:1S/C8H10N4O2/c1-10-4-9-6-5(10)7(13)12(3)8(14)11(6)2/h4H,1-3H3"

    SmallMolMetadata(){};

    SmallMolMetadata(const SmallMolMetadata &rhs)
    {
        *this = rhs;
    }

    virtual ~SmallMolMetadata(){ };

    // Declare the various small molecule column names and their descriptions
    // This order matters - it's used for sorting 
    #define DEFINE_SQL_COLS_AND_COMMENTS const char *sql_cols[][2] = { \
        { "moleculeName", "precursor molecule's name (not needed for peptides)" }, \
        { "chemicalFormula", "precursor molecule's neutral formula (not needed for peptides)" }, \
        { "precursorAdduct", "ionizing adduct e.g. [M+Na], [2M-H2O+2H] etc (not needed for peptides)" }, \
        { "inchiKey", "molecular identifier for structure retrieval (not needed for peptides)" }, \
        { "otherKeys", "alternative molecular identifiers for structure retrieval, tab separated name:value pairs e.g. cas:58-08-2\\thmdb:01847 (not needed for peptides)" }, \
        { NULL, NULL } \
    }

    // Get a comma seperated list of column names with type decls and comments
    static string sql_col_decls() { 
        DEFINE_SQL_COLS_AND_COMMENTS;
        string result = "";
        vector<string> types = sql_col_types();
        for (int i = 0; sql_cols[i][0] != NULL; i++)
            result += string(sql_cols[i][0]) + " " + types[i] + ", -- " + string(sql_cols[i][1]) + "\n";
        return result;
    }

    static vector<string> sql_col_names()
    {
        DEFINE_SQL_COLS_AND_COMMENTS;
        vector<string> result;
        for (int i = 0; sql_cols[i][0] != NULL; i++)
            result.push_back(sql_cols[i][0]);
        return result;
    }

    static string sql_col_names_csv()
    {
        DEFINE_SQL_COLS_AND_COMMENTS;
        string result = "";
        for (int i = 0; sql_cols[i][0] != NULL; i++)
            result+= string(", ") + sql_cols[i][0];
        return result;
    }

    static vector<string> sql_col_types()
    {
        DEFINE_SQL_COLS_AND_COMMENTS;
        vector<string> result;
        for (int i = 0; sql_cols[i][0] != NULL; i++)
            result.push_back("VARCHAR(128)");
        return result;
    }

  SmallMolMetadata& operator= (const SmallMolMetadata& rhs)
  {
    inchiKey = rhs.inchiKey;
    precursorAdduct = rhs.precursorAdduct;
    chemicalFormula = rhs.chemicalFormula;
    moleculeName = rhs.moleculeName;
    otherKeys = rhs.otherKeys;
    return *this;
  }

  bool IsCompleteEnough() const
  {
	  // Need at least name, adduct and formula to be a useful description
	  return (!moleculeName.empty() && !precursorAdduct.empty() && !chemicalFormula.empty());
  }

  void clear(){
    inchiKey.clear();
    precursorAdduct.clear();
    chemicalFormula.clear();
    moleculeName.clear();
    otherKeys.clear();
  };

};

