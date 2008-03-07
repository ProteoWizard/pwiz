//
// hello_analyzer.cpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "pwiz/analysis/MSDataAnalyzerApplication.hpp"
#include <iostream>


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
        cout << "spectrum: " << spectrum.index << " " 
                             << spectrum.id << " "
                             << "ms" << spectrum.cvParam(MS_ms_level).value << " "
                             << spectrum.spectrumDescription.scan.cvParam(MS_filter_string).value
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
            cout << "Usage: hello_analyzer [options] [filenames]\n";
            cout << "Options:\n" << app.usageOptions << endl;
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


