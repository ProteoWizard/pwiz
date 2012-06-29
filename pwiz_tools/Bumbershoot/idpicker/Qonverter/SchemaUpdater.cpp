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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "SchemaUpdater.hpp"
#include "../Lib/SQLite/sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"


using namespace pwiz::util;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE

const int CURRENT_SCHEMA_REVISION = 1;

namespace SchemaUpdater {


namespace {

void update_0_to_1(sqlite::database& db, IterationListenerRegistry* ilr)
{
    db.execute("CREATE TABLE About (Id INTEGER PRIMARY KEY, SoftwareName TEXT, SoftwareVersion TEXT, StartTime DATETIME, SchemaRevision INT);"
               "INSERT INTO About VALUES (1, 'IDPicker', '3.0', datetime('now'), 1);");

    try
    {
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredProtein LIMIT 1");
            q.begin()->get<int>(0);
        }

        // if UnfilteredProtein exists but UnfilteredSpectrum does not, create the filtered Spectrum table
        db.execute("ALTER TABLE Spectrum RENAME TO UnfilteredSpectrum;"
                   "CREATE TABLE Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC);"
                   "INSERT INTO Spectrum SELECT * FROM UnfilteredSpectrum WHERE Id IN (SELECT Spectrum FROM PeptideSpectrumMatch);");
        
        // if UnfilteredProtein exists, replace the UnfilteredPeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
        try
        {
            sqlite::query q(db, "SELECT ObservedNeutralMass FROM UnfilteredPeptideSpectrumMatch LIMIT 1");
            q.begin()->get<double>(0);
        }
        catch (sqlite::database_error& e)
        {
            if (!bal::contains(e.what(), "no such")) // column or table
                throw runtime_error(e.what());

            db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                       "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM UnfilteredPeptideSpectrumMatch;"
                       "DROP TABLE UnfilteredPeptideSpectrumMatch;"
                       "ALTER TABLE NewPeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch;");
        }
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }
    
    // replace PeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
    try
    {
        sqlite::query q(db, "SELECT ObservedNeutralMass FROM PeptideSpectrumMatch LIMIT 1");
        q.begin()->get<double>(0);
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());

        db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                   "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM PeptideSpectrumMatch;"
                   "DROP TABLE PeptideSpectrumMatch;"
                   "ALTER TABLE NewPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;");
    }    
}

} // namespace


bool update(const string& idpDbFilepath, IterationListenerRegistry* ilr)
{
    int schemaRevision;
    sqlite::database db(idpDbFilepath);

    try
    {
        sqlite::query q(db, "SELECT SchemaRevision FROM About");
        schemaRevision = q.begin()->get<int>(0);
    }
    catch (sqlite::database_error&)
    {
        schemaRevision = 0;
    }

    if (schemaRevision == 0)
        update_0_to_1(db, ilr);
    else if (schemaRevision > CURRENT_SCHEMA_REVISION)
        throw runtime_error("[SchemaUpdater::update] unable to update schema revision " +
                            lexical_cast<string>(schemaRevision) +
                            "; the latest compatible revision is " +
                            lexical_cast<string>(CURRENT_SCHEMA_REVISION));
    else
        return false; // no update needed

    return true; // an update was done
}


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE
