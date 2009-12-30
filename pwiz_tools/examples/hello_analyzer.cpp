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


#include "pwiz_tools/common/MSDataAnalyzerApplication.hpp"
#include <iostream>


using namespace pwiz::data;
using namespace pwiz::cv;
using namespace pwiz::analysis;
using namespace std;


class HelloAnalyzer : public MSDataAnalyzer
{
    public:

    virtual void open(const DataInfo& dataInfo) 
    {
        cout << "sourceFilename: " << dataInfo.sourceFilename << endl;
        cout << "outputDirectory: " << dataInfo.outputDirectory << endl;
    }

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo, 
                                          const SpectrumIdentity& spectrumIdentity) const 
    {
        return UpdateRequest_NoBinary;
    }

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum) 
    {
        Scan dummy;
        const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];

        cout << "spectrum: " << spectrum.index << " " 
                             << spectrum.id << " "
                             << "ms" << spectrum.cvParam(MS_ms_level).value << " "
                             << scan.cvParam(MS_filter_string).value
                             << endl;
    }

    virtual void close(const DataInfo& dataInfo)
    {}
};


int main(int argc, const char* argv[])
{
    try
    {
        MSDataAnalyzerApplication app(argc, argv);

        if (app.filenames.empty())
        {
            cout << "Usage: hello_analyzer [options] [filenames]\n"
                 << "Options:\n" << app.usageOptions << endl
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        HelloAnalyzer analyzer;
        app.run(analyzer, &cerr);

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}


