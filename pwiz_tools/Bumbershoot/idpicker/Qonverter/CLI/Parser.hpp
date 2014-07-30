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
// Contributor(s):
//


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "Qonverter.hpp"
#using <system.dll>
#pragma warning( pop )


namespace IDPicker {


using namespace System;
using namespace System::Collections::Generic;


/// TODO
public ref struct Parser
{
    /// top-level summary of an imported result set 
    ref struct Analysis
    {
        property String^ name;
        property String^ softwareName;
        property String^ softwareVersion;
        property DateTime startTime;
        property IDictionary<String^, String^>^ parameters;
        property IList<String^>^ filepaths;

        ref struct ImportSettings
        {
            property String^ proteinDatabaseFilepath;
            property Qonverter::Settings^ qonverterSettings;
            double maxQValue; /// filter the database on Q-value before writing to disk
            int maxResultRank; /// filter the database on PSM rank before writing to disk
            bool ignoreUnmappedPeptides;
            bool logQonversionDetails;
        };

        property ImportSettings^ importSettings;
    };


    ref struct ImportSettingsEventArgs : EventArgs
    {
        /// an analysis is distinct if its name is unique and it has at least one distinct parameter
        property IList<Analysis^>^ DistinctAnalyses;
        property bool Cancel;
    };

    delegate void ImportSettingsEventHandler(Object^ sender, ImportSettingsEventArgs^ e);

    event ImportSettingsEventHandler^ ImportSettings;


    void Parse(IEnumerable<String^>^ inputFilepaths, pwiz::CLI::util::IterationListenerRegistry^ ilr);

    void Parse(IEnumerable<String^>^ inputFilepaths, int maxThreads, pwiz::CLI::util::IterationListenerRegistry^ ilr);

    void Parse(String^ inputFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr);
	
	static String^ ParseSource(String^ inputFilepath);


    internal:
    Parser::ImportSettingsEventHandler^ handler;
    void marshal(Object^ sender, ImportSettingsEventArgs^ e);
};


} // namespace IDPicker
