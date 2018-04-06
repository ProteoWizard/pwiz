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


#include "pwiz/utility/misc/Std.hpp"
#include "sqlite3pp.h"
#include "SchemaUpdater.hpp"
#include "TotalCounts.hpp"
#include "boost/foreach_field.hpp"


using namespace pwiz::util;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE


class TotalCounts::Impl
{
    public:

    int clusters;
    int proteinGroups;
    int proteins;
    int geneGroups;
    int genes;
    int distinctPeptides;
    int distinctMatches;
    sqlite3_int64 filteredSpectra;
    double proteinFDR;
    double peptideFDR;
    double spectrumFDR;
};


TotalCounts::TotalCounts(sqlite3* idpDbConnection)
    : _impl(new Impl)
{
    sqlite::database idpDb(idpDbConnection, false);

    sqlite::query proteinLevelSummaryQuery(idpDb, "SELECT IFNULL(COUNT(DISTINCT pro.Cluster), 0),\n"
                                                  "       IFNULL(COUNT(DISTINCT pro.ProteinGroup), 0),\n"
                                                  "       IFNULL(COUNT(DISTINCT pro.Id), 0),\n"
                                                  "       IFNULL(COUNT(DISTINCT pro.GeneGroup), 0),\n"
                                                  "       IFNULL(COUNT(DISTINCT pro.GeneId), 0),\n"
                                                  "       IFNULL(SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END), 0)\n"
                                                  "FROM Protein pro");
    {
        sqlite::query::rows queryRow = *proteinLevelSummaryQuery.begin();
        int decoyProteins;
        queryRow.getter() >> _impl->clusters >> _impl->proteinGroups >> _impl->proteins >> _impl->geneGroups >> _impl->genes >> decoyProteins;
        _impl->proteinFDR = 2.0 * decoyProteins / _impl->proteins;
    }

    _impl->distinctPeptides = sqlite::query(idpDb, "SELECT COUNT(*) FROM Peptide").begin()->get<int>(0);
    _impl->distinctMatches = sqlite::query(idpDb, "SELECT COUNT(DISTINCT DistinctMatchId) FROM DistinctMatch").begin()->get<int>(0);
    _impl->filteredSpectra = sqlite::query(idpDb, "SELECT COUNT(*) FROM Spectrum").begin()->get<sqlite3_int64>(0);

    // get the count of peptides that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
    sqlite::query peptideLevelDecoysQuery(idpDb, "SELECT COUNT(Peptide)\n"
                                                 "FROM (SELECT pep.Id AS Peptide, \n"
                                                 "      COUNT(DISTINCT pro.Id) AS Proteins, \n"
                                                 "      SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys, \n"
                                                 "      CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy\n"
                                                 "      FROM Peptide pep\n"
                                                 "      JOIN PeptideInstance pi ON pep.Id = pi.Peptide\n"
                                                 "      JOIN Protein pro ON pi.Protein = pro.Id\n"
                                                 "      GROUP BY pep.Id\n"
                                                 "      HAVING Proteins = Decoys OR Decoys = 0\n"
                                                 "     )\n"
                                                 "GROUP BY IsDecoy\n"
                                                 "ORDER BY IsDecoy\n");
    {
        vector<int> peptideLevelDecoys;
        for(sqlite::query::rows queryRow : peptideLevelDecoysQuery)
            peptideLevelDecoys.push_back(queryRow.get<int>(0));

        // without both targets and decoys, FDR can't be calculated
        if (peptideLevelDecoys.size() != 2 || peptideLevelDecoys[0] + peptideLevelDecoys[1] < 2)
            _impl->peptideFDR = 0.0;
        else
            _impl->peptideFDR = 2.0 * peptideLevelDecoys[1] / (peptideLevelDecoys[0] + peptideLevelDecoys[1]);
    }

    // get the count of spectra that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
    sqlite::query spectrumLevelDecoysQuery(idpDb, "SELECT COUNT(Spectrum)\n"
                                                  "FROM(SELECT psm.Spectrum,\n"
                                                  "            COUNT(DISTINCT pro.Id) AS Proteins, \n"
                                                  "            SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys,\n"
                                                  "            CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy\n"
                                                  "     FROM PeptideSpectrumMatch psm\n"
                                                  "     JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide\n"
                                                  "     JOIN Protein pro ON pi.Protein = pro.Id\n"
                                                  "     GROUP BY psm.Spectrum\n"
                                                  "     HAVING Proteins = Decoys OR Decoys = 0\n"
                                                  "    )\n"
                                                  "GROUP BY IsDecoy\n"
                                                  "ORDER BY IsDecoy");
    {
        vector<int> spectrumLevelDecoys;
        for(sqlite::query::rows queryRow : spectrumLevelDecoysQuery)
            spectrumLevelDecoys.push_back(queryRow.get<int>(0));

        // without both targets and decoys, FDR can't be calculated
        if (spectrumLevelDecoys.size() != 2 || spectrumLevelDecoys[0] + spectrumLevelDecoys[1] < 2)
            _impl->spectrumFDR = 0.0;
        else
            _impl->spectrumFDR = 2.0 * spectrumLevelDecoys[1] / (spectrumLevelDecoys[0] + spectrumLevelDecoys[1]);
    }
}

TotalCounts::~TotalCounts() {}

int TotalCounts::clusters() const { return _impl->clusters; }
int TotalCounts::proteinGroups() const { return _impl->proteinGroups; }
int TotalCounts::proteins() const { return _impl->proteins; }
int TotalCounts::geneGroups() const { return _impl->geneGroups; }
int TotalCounts::genes() const { return _impl->genes; }
int TotalCounts::distinctPeptides() const { return _impl->distinctPeptides; }
int TotalCounts::distinctMatches() const { return _impl->distinctMatches; }
sqlite3_int64 TotalCounts::filteredSpectra() const { return _impl->filteredSpectra; }
double TotalCounts::proteinFDR() const { return _impl->proteinFDR; }
double TotalCounts::peptideFDR() const { return _impl->peptideFDR; }
double TotalCounts::spectrumFDR() const { return _impl->spectrumFDR; }


END_IDPICKER_NAMESPACE