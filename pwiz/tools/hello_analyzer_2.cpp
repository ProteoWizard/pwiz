//
// hello_analyzer_2.cpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "pwiz/analysis/MSDataAnalyzerApplication.hpp"
#include "pwiz/analysis/MSDataCache.hpp"
#include <iostream>


using namespace pwiz::analysis;
using boost::shared_ptr;
using namespace std;


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
            cout << "Usage: hello_analyzer_2 [options] [filenames]\n";
            cout << "Options:\n" << app.usageOptions << endl;
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


