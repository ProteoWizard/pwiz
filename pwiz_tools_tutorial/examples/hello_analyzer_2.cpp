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
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::analysis;


class HelloAnalyzer2 : public MSDataAnalyzer
{
    public:

    HelloAnalyzer2(const MSDataCache& cache)
    :   cache_(cache)
    {}

    virtual void open(const DataInfo& dataInfo) 
    {
        cout << "sourceFilename: " << dataInfo.sourceFilename << endl;
        cout << "outputDirectory: " << dataInfo.outputDirectory << endl;
    }

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo, 
                                          const SpectrumIdentity& spectrumIdentity) const 
    {
        return UpdateRequest_Full; // get binary data
    }

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum) 
    {
        const SpectrumInfo& info = cache_[spectrum.index];

        // get some stuff from the cache

        cout << "spectrum " << info.index << ":\n" 
             << "  scan number: " << info.scanNumber << endl
             << "  " << "ms" << info.msLevel << endl
             << "  retention time: " << info.retentionTime << " sec\n";

        if (!info.filterString.empty())
            cout << "  " << info.filterString << endl;

        // binary data was cached too

        cout << "  ";
        for (size_t i=0; i<min(info.data.size(), size_t(3)); i++)
            cout << "(" << info.data[i].mz << "," << info.data[i].intensity << ") ";
        cout << "...\n";
    }

    virtual void close(const DataInfo& dataInfo)
    {}

    private:
    const MSDataCache& cache_;
};


int main(int argc, const char* argv[])
{
    try
    {
        MSDataAnalyzerApplication app(argc, argv);

        if (app.filenames.empty())
        {
            cout << "Usage: hello_analyzer_2 [options] [filenames]\n"
                 << "Options:\n" << app.usageOptions << endl
                 << "http://proteowizard.sourceforge.net\n"
                 << "support@proteowizard.org\n";
            return 1;
        }

        //
        // This example illustrates the use of a data cache to minimize
        // retrieval expenses, which include not only disk I/O, but also
        // parameter searching, validation, and conversion. 
        //
        // HelloAnalyzer2 keeps a reference to MSDataCache, and uses
        // data from the cache only.
        //
        // On update(), the cache is updated first, so that the downstream
        // analyzers can use the cached data.
        //
        // Note that this idea can be extended to a set of analyzers, each 
        // of which can be dependent on multiple upstream analyzers, as long
        // as the dependency graph is directed acyclic, so that they can be
        // placed in the container in an order preserving manner.
        //

        shared_ptr<MSDataCache> cache(new MSDataCache);
        MSDataAnalyzerPtr hello2(new HelloAnalyzer2(*cache));

        MSDataAnalyzerContainer analyzers;
        analyzers.push_back(cache); // push cache first!
        analyzers.push_back(hello2);
        app.run(analyzers, &cerr);

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


