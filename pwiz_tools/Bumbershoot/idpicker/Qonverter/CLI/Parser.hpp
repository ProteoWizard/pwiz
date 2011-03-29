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


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "Qonverter.hpp"
#include "pwiz/utility/bindings/CLI/common/IterationListener.hpp"
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
    event pwiz::CLI::util::IterationEventHandler^ ParsingProgress;


    void Parse(IEnumerable<String^>^ inputFilepaths);

    void Parse(String^ inputFilepath);


    internal:
    void marshal(Object^ sender, ImportSettingsEventArgs^ e);
    void marshal(Object^ sender, pwiz::CLI::util::IterationEventArgs^ e);
};


} // namespace IDPicker
