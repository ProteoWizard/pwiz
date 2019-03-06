//
// $Id$
//
//
// Original author: Jarrett Egertson <jegertso .a.t uw.edu>
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

#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include <boost/program_options/options_description.hpp>
#include <boost/program_options/parsers.hpp>
#include <boost/program_options/variables_map.hpp>
#include <pwiz/analysis/demux/EnumConstantNotPresentException.hpp>
#include <pwiz/analysis/spectrum_processing/SpectrumList_Demux.hpp>
#include <pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp>
#include <pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp>
#include <boost/chrono/chrono.hpp>
#include <src/Core/products/Parallelizer.h>

#define PRISM_VERSION "0.3"

using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::analysis;

namespace po = boost::program_options;

struct Optimization
{
    Optimization(const string& s) : value(s) {}
    string value;
};

// This overload is needed for using default_value when building a variable_map. The default_value function relies on boost::lexical_cast, which relies on this operator.
ostream& operator <<(ostream& stream, const Optimization& opt) { return stream << opt.value; }

/// Overload the 'validate' function for the user-defined Optimization option class.
void validate(boost::any& v,
    const vector<string>& values,
    Optimization*,
    int)
{
    // Make sure no previous assignment to 'a' was made.
    po::validators::check_first_occurrence(v);
    // Extract the first string from 'values'. If there is more than
    // one string, it's an error, and exception will be thrown.
    const string& s = po::validators::get_single_string(values);

    // Try conversion to desired enum type
    try
    {
        SpectrumList_Demux::Params::stringToOptimization(s);
        v = boost::any(Optimization(s));
    }
    catch (EnumConstantNotPresentException&)
    {
        throw po::invalid_option_value(s);
    }
}

class IterationListenerStream : public IterationListener
{
public:
    explicit IterationListenerStream(ostream &os = std::cerr) : os_(os), entfmt_("%5.1f"){}

    Status update(const UpdateMessage& updateMessage) override
    {
        auto percentProgress = 100.0 * float(updateMessage.iterationIndex) / float(updateMessage.iterationCount);
        os_ << "\rDemultiplexing: " << entfmt_ % percentProgress << "% (Spectrum "
            << updateMessage.iterationIndex << " of " << updateMessage.iterationCount << ")       ";
        return Status_Ok;
    }
private:
    ostream &os_;
    boost::format entfmt_;
};

po::variables_map parseCommandLine(int argc, char **argv);

void run(int argc, char ** argv)
{
    // The Eigen library must be initialized to support multithreaded operations before creating the threads
    Eigen::initParallel();
    auto start = boost::chrono::system_clock::now();
    std::cerr << "Welcome to Prism v" << PRISM_VERSION << endl;
    auto vm = parseCommandLine(argc, argv);
    
    SpectrumList_Demux::Params demuxParams;
    demuxParams.massError = vm["massError"].as<pwiz::chemistry::MZTolerance>();
    demuxParams.nnlsMaxIter = vm["nnlsMaxIter"].as<unsigned int>();
    demuxParams.nnlsEps = vm["nnlsEps"].as<double>();
    demuxParams.applyWeighting = !vm["noWeighting"].as<bool>();
    demuxParams.demuxBlockExtra = vm["demuxBlockExtra"].as<double>();
    demuxParams.variableFill = vm["variableFill"].as<bool>();
    demuxParams.regularizeSums = !vm["noSumNormalize"].as<bool>();
    demuxParams.optimization = SpectrumList_Demux::Params::stringToOptimization(vm["optimization"].as<Optimization>().value);
    demuxParams.padScanTimes = vm["padScanTimes"].as<bool>();
    demuxParams.interpolateRetentionTime = vm["interpolateRT"].as<bool>();
    bool skipCentroiding = vm["skipCentroiding"].as<bool>();

    FullReaderList readers;
    MSDataFile msd(vm["inputfile"].as<string>(), &readers);
    IntegerSet levelsToCentroid(1,2);

    // Bypass centroiding
    SpectrumListPtr toDemuxPtr = msd.run.spectrumListPtr;
    if (!skipCentroiding)
    {
        SpectrumListPtr tempPtr = toDemuxPtr;
        toDemuxPtr.reset(new SpectrumList_PeakPicker(tempPtr,
                PeakDetectorPtr(boost::make_shared<LocalMaximumPeakDetector>(3)),
                true,
                levelsToCentroid));
        msd.filterApplied();
    }

    SpectrumListPtr demux_list(new SpectrumList_Demux(toDemuxPtr, demuxParams));
    msd.filterApplied();
    msd.run.spectrumListPtr = demux_list;
    // set up progress indicator
    IterationListenerPtr listenerPtr(new IterationListenerStream(std::cerr));
    IterationListenerRegistry listenerRegistry;
    listenerRegistry.addListener(listenerPtr, 10);

    MSDataFile::WriteConfig writeConfig;
    MSDataFile::write(msd, vm["outputfile"].as<string>(), writeConfig, &listenerRegistry);
    std::cerr << endl;
    boost::chrono::duration<double> sec = boost::chrono::system_clock::now() - start;
    std::cout << "Demultiplexing took " << sec.count() / 60.0 << " minutes." << std::endl;
}

po::variables_map parseCommandLine(int argc, char **argv)
{
    po::options_description desc("PRISM arguments");
    // Get default values
    SpectrumList_Demux::Params demuxParams;
    Optimization defaultOptimization(SpectrumList_Demux::Params::optimizationToString(demuxParams.optimization));
    bool skipCentroiding = false;
    
    desc.add_options()
        ("help,h", "Produce this help message.")
        ("massError", po::value<pwiz::chemistry::MZTolerance>()->default_value(demuxParams.massError),
        "MS/MS error between spectra. Can specify number and units of either ppm or da, e.g. 10ppm or 0.4da. Default 10ppm.")
        ("variableFill", po::bool_switch()->default_value(demuxParams.variableFill),
        "MSX data was acquired with variable fill times.")
        ("demuxBlockExtra", po::value<double>()->default_value(demuxParams.demuxBlockExtra),
        "Extra slop to include in the set of spectra used for demux. Number of spectra added is input * num spectra in one cycle")
        ("nnlsMaxIter", po::value<unsigned int>()->default_value(demuxParams.nnlsMaxIter),
        "Maximum number of iterations for demux solve.")
        ("nnlsEps", po::value<double>()->default_value(demuxParams.nnlsEps),
        "Epsilon value for testing for convergence of demux solve.")
        ("noSumNormalize", po::bool_switch()->default_value(!demuxParams.regularizeSums),
        "Do not normalize the raw demux output for a spectrum so it sums up to the multiplexed peak intensity")
        ("noWeighting", po::bool_switch()->default_value(!demuxParams.applyWeighting),
        "Use non-weighted non negative least squares")
        ("optimization", po::value<Optimization>()->default_value(defaultOptimization),
        "Use optimizations. Available optimizations are \"none\", and \"overlap_only\"" )
        ("padScanTimes", po::bool_switch()->default_value(demuxParams.padScanTimes),
        "Pad scan times with small increment to prevent simultaneous spectra (which some software cannot handle)")
        ("interpolateRT", po::bool_switch()->default_value(demuxParams.interpolateRetentionTime),
        "Interpolate retention times. This only applies when overlap demultiplexing with no MSX (overlap only)")
        ("skipCentroiding", po::bool_switch()->default_value(skipCentroiding),
        "Set to true to use profile data if available")
        ("inputfile", po::value<string>()->required(),
        "Input spectra file")
        ("outputfile", po::value<string>()->required(),
        "Output spectra file");
    
    po::positional_options_description p;
    p.add("inputfile", 1);
    p.add("outputfile", 1);
    po::variables_map vm;
    po::store(po::command_line_parser(argc, argv).options(desc).positional(p).run(), vm);
    if (vm.count("help"))
    {
        std::cout << desc << endl;
    }
    notify(vm);
    return vm;
}


int main(int argc, char **argv)
{
    try
    {
        run(argc, argv);

        return 0;
    }
    catch (std::exception& e)
    {
        std::cerr << e.what() << endl;
    }
    catch (...)
    {
        std::cerr << "Caught unknown exception.\n";
    }

    return 1;
}