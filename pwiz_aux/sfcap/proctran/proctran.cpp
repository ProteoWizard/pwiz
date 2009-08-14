//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "pwiz/analysis/frequency/PeakDetectorNaive.hpp"
#include "pwiz/analysis/frequency/PeakDetectorMatchedFilter.hpp"
#include "pwiz/analysis/frequency/FrequencyEstimatorSimple.hpp"
#include "pwiz/analysis/frequency/FrequencyEstimatorPhysicalModel.hpp"
#include "pwiz/analysis/calibration/Calibrator.hpp"
#include "pwiz/analysis/calibration/MassDatabase.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include "pwiz_aux/sfcap/transient/TransientData.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/proteome/IsotopeEnvelopeEstimator.hpp"
#include "RecalibratorKnownMassList.hpp" 
#include "RecalibratorSimple.hpp" 

#include "pwiz_aux/sfcap/peptideSieve/fasta.h" // interface to fasta file handling
using bioinfo::fasta;

#include "pwiz_aux/sfcap/peptideSieve/digest.hpp"// interface for doing trivial tryptic digests

#include "boost/filesystem/operations.hpp"
#include "boost/filesystem/fstream.hpp"
#include "boost/filesystem/exception.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/program_options.hpp"

#include <iostream>
#include <fstream>
#include <map>
#include <stdexcept>
#include <iterator>


using namespace std;
using namespace pwiz::calibration;
using namespace pwiz::data;
using namespace pwiz::frequency;
using namespace pwiz::data::peakdata;
using namespace pwiz::pdanalysis;
using namespace pwiz::proteome;
namespace bfs = boost::filesystem;
namespace po = boost::program_options;


struct Configuration
{
    // misc

    bool write_logs;
    const char* label_write_logs;

    // fft

    unsigned int fft_zero_padding;
    const char* label_fft_zero_padding;

    // PeakDetector type

    const char* peak_detector_type_naive; 
    const char* peak_detector_type_matched_filter; 

    string peak_detector_type;
    const char* label_peak_detector_type;

    // PeakDetectorNaive parameters

    double detect_naive_noise_factor;
    const char* label_detect_naive_noise_factor;

    unsigned int detect_naive_detection_radius;
    const char* label_detect_naive_detection_radius;

    // PeakDetectorMatchedFilter parameters

    int detect_mf_filterMatchRate;
    const char* label_detect_mf_filterMatchRate;
    int detect_mf_filterSampleRadius;
    const char* label_detect_mf_filterSampleRadius;
    double detect_mf_peakThresholdFactor;
    const char* label_detect_mf_peakThresholdFactor;
    double detect_mf_peakMaxCorrelationAngle;
    const char* label_detect_mf_peakMaxCorrelationAngle;
    double detect_mf_isotopeThresholdFactor;
    const char* label_detect_mf_isotopeThresholdFactor;
    int detect_mf_isotopeMaxChargeState;
    const char* label_detect_mf_isotopeMaxChargeState;
    int detect_mf_isotopeMaxNeutronCount; 
    const char* label_detect_mf_isotopeMaxNeutronCount;
    double detect_mf_collapseRadius; 
    const char* label_detect_mf_collapseRadius;

    // FrequencyEstimator type

    const char* freqest_type_parabola;
    const char* freqest_type_lorentzian;
    const char* freqest_type_physical;

    string freqest_type;
    const char* label_freqest_type;

    // FrequencyEstimatorPhysicalModel parameters

    unsigned int freqest_physical_window_radius;
    const char* label_freqest_physical_window_radius;

    unsigned int freqest_physical_iteration_count;
    const char* label_freqest_physical_iteration_count;

    // Calibrator parameters

    string calibrate_database_filename;
    const char* label_calibrate_database_filename;
    
    double calibrate_initial_error_estimate;
    const char* label_calibrate_initial_error_estimate;

    unsigned int calibrate_error_estimator_iteration_count;
    const char* label_calibrate_error_estimator_iteration_count;

    unsigned int calibrate_calibrator_iteration_count;
    const char* label_calibrate_calibrator_iteration_count;

    // least squares recalibration

    const char* recal_type_5pep;
    const char* recal_type_calmix;
    const char* recal_type_db;

    string recal_type;
    const char* label_recal_type;

    string lsq_calibrate_database_filename;
    const char* label_lsq_calibrate_database_filename;



    Configuration()
    :   write_logs(false), label_write_logs("write_logs"), 
        fft_zero_padding(1), label_fft_zero_padding("fft_zero_padding"),
        peak_detector_type_naive("naive"),
        peak_detector_type_matched_filter("matched_filter"),
        peak_detector_type(peak_detector_type_matched_filter), label_peak_detector_type("peak_detector_type"),
        detect_naive_noise_factor(5), label_detect_naive_noise_factor("detect_naive_noise_factor"),
        detect_naive_detection_radius(2), label_detect_naive_detection_radius("detect_naive_detection_radius"),
        detect_mf_filterMatchRate(4), label_detect_mf_filterMatchRate("detect_mf_filter_match_rate"),
        detect_mf_filterSampleRadius(2), label_detect_mf_filterSampleRadius("detect_mf_filter_sample_radius"),
        detect_mf_peakThresholdFactor(10), label_detect_mf_peakThresholdFactor("detect_mf_peak_threshold_factor"),
        detect_mf_peakMaxCorrelationAngle(30), label_detect_mf_peakMaxCorrelationAngle("detect_mf_peak_max_correlation_angle"),
        detect_mf_isotopeThresholdFactor(10), label_detect_mf_isotopeThresholdFactor("detect_mf_isotope_threshold_factor"),
        detect_mf_isotopeMaxChargeState(6), label_detect_mf_isotopeMaxChargeState("detect_mf_isotope_max_charge_state"),
        detect_mf_isotopeMaxNeutronCount(4), label_detect_mf_isotopeMaxNeutronCount("detect_mf_isotope_max_neutron_count"),
        detect_mf_collapseRadius(15), label_detect_mf_collapseRadius("detect_mf_collapse_radius"),
        freqest_type_parabola("parabola"),
        freqest_type_lorentzian("lorentzian"),
        freqest_type_physical("physical"),
        freqest_type(freqest_type_physical), label_freqest_type("freqest_type"),
        freqest_physical_window_radius(10), label_freqest_physical_window_radius("freqest_physical_window_radius"),
        freqest_physical_iteration_count(20), label_freqest_physical_iteration_count("freqest_physical_iteration_count"),
        calibrate_database_filename("trypsin0_pro_uniq_mass.pdb"), label_calibrate_database_filename("calibrate_database_filename"),
        calibrate_initial_error_estimate(5e-6), label_calibrate_initial_error_estimate("calibrate_initial_error_estimate"),
        calibrate_error_estimator_iteration_count(20), label_calibrate_error_estimator_iteration_count("calibrate_error_estimator_iteration_count"),
        calibrate_calibrator_iteration_count(20), label_calibrate_calibrator_iteration_count("calibrate_calibrator_iteration_count"),
        recal_type_5pep("5pep"),
        recal_type_calmix("calmix"),
	recal_type_db("db"),
        recal_type(recal_type_5pep), label_recal_type("recal_type"), 
        lsq_calibrate_database_filename("lsq_db.tfa"), label_lsq_calibrate_database_filename("lsq_calibrate_database_filename")
     {}
};


ostream& operator<<(ostream& os, const Configuration& config)
{
    os.precision(12);
    os << 
        config.label_write_logs << " = " << config.write_logs << endl <<
        config.label_fft_zero_padding << " = " << config.fft_zero_padding << endl <<
        config.label_peak_detector_type << " = " << config.peak_detector_type << endl <<
        config.label_detect_naive_noise_factor << " = " << config.detect_naive_noise_factor << endl <<
        config.label_detect_naive_detection_radius << " = " << config.detect_naive_detection_radius << endl <<
        config.label_detect_mf_filterMatchRate << " = " << config.detect_mf_filterMatchRate << endl <<
        config.label_detect_mf_filterSampleRadius << " = " << config.detect_mf_filterSampleRadius << endl <<
        config.label_detect_mf_peakThresholdFactor << " = " << config.detect_mf_peakThresholdFactor << endl <<
        config.label_detect_mf_peakMaxCorrelationAngle << " = " << config.detect_mf_peakMaxCorrelationAngle<< endl <<
        config.label_detect_mf_isotopeThresholdFactor << " = " << config.detect_mf_isotopeThresholdFactor << endl <<
        config.label_detect_mf_isotopeMaxChargeState << " = " << config.detect_mf_isotopeMaxChargeState << endl <<
        config.label_detect_mf_isotopeMaxNeutronCount << " = " << config.detect_mf_isotopeMaxNeutronCount << endl <<
        config.label_detect_mf_collapseRadius << " = " << config.detect_mf_collapseRadius<< endl <<
        config.label_freqest_type << " = " << config.freqest_type << endl <<
        config.label_freqest_physical_window_radius << " = " << config.freqest_physical_window_radius << endl <<
        config.label_freqest_physical_iteration_count << " = " << config.freqest_physical_iteration_count << endl << 
        config.label_calibrate_database_filename << " = " << config.calibrate_database_filename << endl <<
        config.label_calibrate_initial_error_estimate << " = " << config.calibrate_initial_error_estimate << endl <<
        config.label_calibrate_error_estimator_iteration_count << " = " << config.calibrate_error_estimator_iteration_count << endl <<
        config.label_calibrate_calibrator_iteration_count << " = " << config.calibrate_calibrator_iteration_count << endl <<
      config.label_recal_type << " = " << config.recal_type << endl <<
      config.label_lsq_calibrate_database_filename << " = " << config.lsq_calibrate_database_filename << endl;
    return os;
}


typedef int (*Subcommand)(const vector<string>& args, const Configuration& config);


int defaults(const vector<string>& args, const Configuration& config)
{
    cout << Configuration();
    return 0;
}


int fft(const TransientData& td, FrequencyData& fd, const Configuration& config, const string& filenameOut)
{
    cerr << "Computing FFT..." << flush;
    td.computeFFT(config.fft_zero_padding, fd);
    //fd.write(filenameOut);
    cerr << "done.\n";
    return 0;
}


int fft(const vector<string>& args, const Configuration& config)
{
    if (args.size() < 3) throw runtime_error("Wrong number of arguments.");
    const string& filenameIn = args[1];
    const string& filenameOut = args[2];

    TransientData td(filenameIn);
    FrequencyData fd;
    return fft(td, fd, config, filenameOut); 
}


auto_ptr<IsotopeEnvelopeEstimator> createIsotopeEnvelopeEstimator()
{
    const double abundanceCutoff = .01;
    const double massPrecision = .1; 
    IsotopeCalculator isotopeCalculator(abundanceCutoff, massPrecision);

    IsotopeEnvelopeEstimator::Config config;
    config.isotopeCalculator = &isotopeCalculator;

    return auto_ptr<IsotopeEnvelopeEstimator>(new IsotopeEnvelopeEstimator(config));
}


auto_ptr<IsotopeEnvelopeEstimator> isotopeEnvelopeEstimator_;


auto_ptr<PeakDetector> createPeakDetector(const Configuration& config, ostream* log)
{
    auto_ptr<PeakDetector> pd;

    if (config.peak_detector_type == config.peak_detector_type_naive)
    { 
        pd = PeakDetectorNaive::create(config.detect_naive_noise_factor, 
                                       config.detect_naive_detection_radius);
    }
    else if (config.peak_detector_type == config.peak_detector_type_matched_filter)
    {
        isotopeEnvelopeEstimator_ = createIsotopeEnvelopeEstimator();

        PeakDetectorMatchedFilter::Config pdconfig;
        pdconfig.isotopeEnvelopeEstimator = isotopeEnvelopeEstimator_.get();
        pdconfig.filterMatchRate = config.detect_mf_filterMatchRate; 
        pdconfig.filterSampleRadius = config.detect_mf_filterSampleRadius;  
        pdconfig.peakThresholdFactor = config.detect_mf_peakThresholdFactor;
        pdconfig.peakMaxCorrelationAngle = config.detect_mf_peakMaxCorrelationAngle;
        pdconfig.isotopeThresholdFactor = config.detect_mf_isotopeThresholdFactor;
        pdconfig.isotopeMaxChargeState = config.detect_mf_isotopeMaxChargeState;
        pdconfig.isotopeMaxNeutronCount = config.detect_mf_isotopeMaxNeutronCount;
        pdconfig.collapseRadius = config.detect_mf_collapseRadius;
        pdconfig.log = log;
        pd = PeakDetectorMatchedFilter::create(pdconfig);
    }
    else
    {
        throw runtime_error("Peak detector type '" + config.peak_detector_type + "' not supported.");
    }

    return pd;
}


int detect(const FrequencyData& fd, peakdata::Scan& scan, const Configuration& config, 
           ostream& report, ostream* log = 0)
{
    cerr << "Running peak detector..." << flush;
    auto_ptr<PeakDetector> pd = createPeakDetector(config, log);
    pd->findPeaks(fd, scan);
    cerr << "done.\n";
    cerr << "Peaks found: " << scan.peakFamilies.size() << endl;

    report.precision(12);
    report << scan << endl;

    return 0;
}


int detect(const vector<string>& args, const Configuration& config)
{
    if (args.size() != 2) throw runtime_error("Wrong number of arguments.");
    const string& filename = args[1];
    const FrequencyData fd(filename);
    peakdata::Scan scan;
    return detect(fd, scan, config, cout, &cerr);
}


class EnvelopeEstimator
{
    public:

    EnvelopeEstimator(const FrequencyEstimator& estimator,
                      const FrequencyData& fd) 
    :   estimator_(estimator),
        fd_(fd)
    {}
        
    peakdata::PeakFamily operator()(const peakdata::PeakFamily& in) const
    {
        peakdata::PeakFamily out;
        out.mzMonoisotopic = in.mzMonoisotopic;
        out.charge = in.charge;

        try 
        {
            // estimate each peak in the envelope
            for (vector<peakdata::Peak>::const_iterator it=in.peaks.begin(); it!=in.peaks.end(); ++it)
                out.peaks.push_back(estimator_.estimate(fd_, *it));
        }
        catch (exception&)
        {
            cerr << "[proctran::EnvelopeEstimator] Caught exception from FrequencyEstimator.\n"
                 << "envelope:\n" << in << endl;
        }

        return out;
    }

    private:
    const FrequencyEstimator& estimator_;
    const FrequencyData& fd_;
};


int freqest(const FrequencyData& fd, const peakdata::Scan& scanIn, peakdata::Scan& scanOut,
            const Configuration& config, const string& outputDirectory, ostream& report)
{
    // instantiate FrequencyEstimator

    auto_ptr<FrequencyEstimator> fe;

    if (config.freqest_type == config.freqest_type_physical)
    {
        FrequencyEstimatorPhysicalModel::Config fepmConfig;
        fepmConfig.windowRadius = config.freqest_physical_window_radius;
        fepmConfig.iterationCount = config.freqest_physical_iteration_count;
        fepmConfig.outputDirectory = outputDirectory;
        fe = FrequencyEstimatorPhysicalModel::create(fepmConfig);
    }
    else if (config.freqest_type == config.freqest_type_parabola)
    {
        fe = FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Parabola); 
    }
    else if (config.freqest_type == config.freqest_type_lorentzian)
    {
        fe = FrequencyEstimatorSimple::create(FrequencyEstimatorSimple::Lorentzian); 
    }
    else
    {
        throw runtime_error("Unknown frequency estimator type.");
    }

    if (!fe.get())
        throw runtime_error("Error instantiating frequency estimator.");

    // fill in metadata

    scanOut.scanNumber = scanIn.scanNumber; 
    scanOut.retentionTime = scanIn.retentionTime;
    scanOut.observationDuration = scanIn.observationDuration;
    scanOut.calibrationParameters = scanIn.calibrationParameters;
    scanOut.peakFamilies.clear();

    // run the estimator on each envelope in the scan

    cerr << "Running " << config.freqest_type << " frequency estimator..." << flush;
    transform(scanIn.peakFamilies.begin(), scanIn.peakFamilies.end(),
              back_inserter(scanOut.peakFamilies), EnvelopeEstimator(*fe, fd));
    cerr << "done.\n";

    report.precision(12);
    report << scanOut << endl;
    
    return 0;
}


// hack to preserve current functionality -- TODO: make this go away
peakdata::Scan readPeakInfoFile(const string& filename)
{
    ifstream is(filename.c_str());
    if (!is) throw runtime_error(("[freqest] Unable to open file " + filename).c_str());

    peakdata::Scan result;

    while (is)
    {  
        double frequency;
        complex<double> value;
        int charge;
        is >> frequency >> value >> charge;
        if (!is) break;

        peakdata::Peak peak;
        peak.frequency = frequency;
        peak.intensity = abs(value);
        peak.phase = std::arg(value);

        peakdata::PeakFamily envelope;
        envelope.charge = charge;
        envelope.peaks.push_back(peak);

        result.peakFamilies.push_back(envelope);
    }

    return result;
}


int freqest(const vector<string>& args, const Configuration& config)
{
    if (args.size() < 3) throw runtime_error("Wrong number of arguments.");
    const string& filenameCfd = args[1];
    const string& filenamePeaks = args[2];
    string outputDirectory = args.size()>3 ? args[3] : "";

    const FrequencyData fd(filenameCfd);

    peakdata::Scan detected = readPeakInfoFile(filenamePeaks);
    peakdata::Scan estimated; 

    return freqest(fd, detected, estimated, config, outputDirectory, cout);
}


vector<Calibrator::Measurement> createMeasurements(const peakdata::Scan& scan)
{
    vector<Calibrator::Measurement> result;

    for (vector<peakdata::PeakFamily>::const_iterator it=scan.peakFamilies.begin(); 
         it!=scan.peakFamilies.end(); ++it)
        result.push_back(Calibrator::Measurement(it->peaks.empty() ? 0 : it->peaks[0].frequency, 
                                                 it->charge));

    return result;
}


int calibrate(const FrequencyData& fd, const peakdata::Scan& scan, 
              const Configuration& config, const string& outputDirectory)
{
    try 
    {
        const string& pdbFilename = config.calibrate_database_filename; 
        cout << "Using database " << pdbFilename << endl;
        auto_ptr<MassDatabase> mdb = MassDatabase::createFromPeptideDatabase(pdbFilename); 

        vector<Calibrator::Measurement> measurements = createMeasurements(scan);

        if (measurements.empty())
            throw runtime_error("No measurements read.");

        CalibrationParameters cp =  fd.calibrationParameters();

        bfs::create_directories(outputDirectory);

        cerr << "Running calibrator..." << flush;

        auto_ptr<Calibrator> calibrator = Calibrator::create(*mdb,
                                                             measurements,
                                                             cp,
                                                             config.calibrate_initial_error_estimate,
                                                             config.calibrate_error_estimator_iteration_count,
                                                             outputDirectory);
        
        for (unsigned int i=0; i<config.calibrate_calibrator_iteration_count; i++) 
            calibrator->iterate();

        cerr << "done.\n";

        cout << "A: " << calibrator->parameters().A << " B: " << calibrator->parameters().B << endl;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }

    return 0;
}


int calibrate(const vector<string>& args, const Configuration& config)
{
    throw runtime_error("calibrate broken");
    
/*
    if (args.size() != 4) throw runtime_error("Wrong number of arguments.");
    const string& filenameCfd = args[1];
    const string& filenamePeaks = args[2];
    const string& outputDirectory = args[3];

    const FrequencyData fd(filenameCfd);

    vector<PeakInfo> peaks;
    ifstream is(filenamePeaks.c_str());
    if (!is) throw runtime_error(("[calibrate] Unable to open file " + filenamePeaks).c_str());
    copy(istream_iterator<PeakInfo>(is), istream_iterator<PeakInfo>(), back_inserter(peaks));

    return calibrate(fd, peaks, config, outputDirectory);
*/
}

void computePrintCalibrationStatistics(const KnownMassList::MatchResult & before, const CalibrationParameters & cpBefore, const KnownMassList::MatchResult & after,const CalibrationParameters & cpAfter,const bfs::path& pathOutput,const string& filenameBase,const string& addendum)
{
  KnownMassList::MatchResult prunedAfter;
  
  string filename = (pathOutput / (filenameBase + ".recal." + addendum +".txt")).string();
  ofstream os(filename.c_str());
  
  if (!os)
    throw runtime_error(("Unable to open file " + filename).c_str());
  
  os << "# label mzKnown frequency mzOriginalF mzOriginal dmz frequency mzRecalF mzRecal dmzRecal ddmz dmzOrigRecal\n";
  
  //this is Parag's hack - might want to rollback later.
  
  /*
    if (before.matches.size() != after.matches.size() ||
    before.matchCount != after.matchCount)
    throw runtime_error("[proctran] Matches don't match!");
  */
  
  for (unsigned int i=0; i<before.matches.size(); ++i){
      if (!before.matches[i].entry)
	throw runtime_error("[proctran] Null KnownMassList::entry.");
      
      const KnownMassList::Entry* entry = before.matches[i].entry;
      const peakdata::PeakFamily* orig = before.matches[i].peakFamily;
      
      for (unsigned int j=0; j<after.matches.size(); ++j){ //this could be searched smarter

	const peakdata::PeakFamily* recal = after.matches[j].peakFamily;
	double beforeFrequency = orig && !orig->peaks.empty() ? orig->peaks[0].frequency : 0;
	double afterFrequency = recal && !recal->peaks.empty() ? recal->peaks[0].frequency : 0;
	if((beforeFrequency != 0) && (beforeFrequency == afterFrequency)){

	  prunedAfter.matches.push_back(after.matches[j]);
	  
	  os << setprecision(10)
	     << entry->label << "\t"
	     << entry->mz << "\t"
	     << beforeFrequency << "\t"
	     << cpBefore.mz(beforeFrequency)<<"\t"
	     << (orig ? orig->mzMonoisotopic : 0) << "\t"
	     << 1.0e6 * ((entry->mz - (orig ? orig->mzMonoisotopic : 0)) / entry->mz) << "\t"
	     << cpAfter.mz(beforeFrequency)<<"\t"
	     << (recal ? recal->mzMonoisotopic : 0) << "\t"	    
	     << 1.0e6 * ((entry->mz - (recal ? recal->mzMonoisotopic : 0))/entry->mz) << "\t"	    
	     << abs(entry->mz - (orig ? orig->mzMonoisotopic : 0)) - abs(entry->mz - (recal ? recal->mzMonoisotopic : 0))<< "\t"
	     << abs((recal ? recal->mzMonoisotopic : 0) - (orig ? orig->mzMonoisotopic : 0)) 	 	     << "\n";
	  continue;
	}
      }
  }
  // output recalibration data
  prunedAfter.reComputeStatistics();

  string filenameStats = (pathOutput / (filenameBase + ".recal." + addendum + ".stats")).string();
  ofstream osStats(filenameStats.c_str());
  if (!osStats)
    throw runtime_error(("Unable to open file " + filenameStats).c_str());
  
  osStats << "# label A B matchCount dmzMean dmz2Mean dmzRel2Mean rmsAbs rmsRel\n";
  osStats << setprecision(10)
	  << "original\t"
	  << cpBefore.A << "\t"
	  << cpBefore.B << "\t"
	  << before.matchCount << "\t"
	  << before.dmzMean << "\t"
	  << before.dmz2Mean << "\t"
	  << before.dmzRel2Mean << "\t"
	  << sqrt(before.dmz2Mean) << "\t"
	  << sqrt(before.dmzRel2Mean) << "\n";
  
  /*    osStats << "recalibrated\t"
	<< cpAfter.A << "\t"
	<< cpAfter.B << "\t"
	<< after.matchCount << "\t"
	<< after.dmzMean << "\t"
	<< after.dmz2Mean << "\t"
	<< after.dmzRel2Mean << "\t"
	<< sqrt(after.dmz2Mean) << "\t"
	<< sqrt(after.dmzRel2Mean) << "\n";
  */
  osStats << "re-recalibrated\t"
	  << cpAfter.A << "\t"
	  << cpAfter.B << "\t"
	  << prunedAfter.matchCount << "\t"
	  << prunedAfter.dmzMean << "\t"
	  << prunedAfter.dmz2Mean << "\t"
	  << prunedAfter.dmzRel2Mean << "\t"
	  << sqrt(prunedAfter.dmz2Mean) << "\t"
	  << sqrt(prunedAfter.dmzRel2Mean) << "\n";
}

void recalibrateLeastSquares(peakdata::Scan& scan, 
                             const bfs::path& pathOutput,
                             const string& filenameBase,
                             const Configuration& config)
{

    KnownMassList kml_train;

    if (config.recal_type == config.recal_type_5pep)
    {
        cout << "Recalibrating using least squares with 5-Peptide masses." << endl;
        kml_train.insert_5pep();
    }
    else if (config.recal_type == config.recal_type_calmix)
    {
        cout << "Recalibrating using least squares with Calmix masses." << endl;
        kml_train.insert_calmix();
    }
    else if (config.recal_type == config.recal_type_db)
    {
        cout << "Recalibrating using least squares with masses from database." << endl;
	fasta<string> mf(config.lsq_calibrate_database_filename);
	std::vector<std::string> peptideVector;

	for(fasta<string>::const_iterator seqIter = mf.begin();seqIter != mf.end();seqIter++){
	  const fasta_seq<string>* fseq = *seqIter;
	  const string& str = fseq->get_seq();
//	  std::cout<<str<<endl<<endl;
	  Digest peptides(str);
	  for(size_t pepNdx=0;pepNdx<peptides.numPeptides();pepNdx++){
	    string peptide = peptides.currentPeptide();
//	    std::cout<<peptide<<endl;
	    peptide.erase(0,1); //drop the first character
	    peptide.erase(peptide.length() - 1,1); //drop the last character
//	    std::cout<<peptide<<endl;
	    peptideVector.push_back(peptide);
	    peptides.next();	    
	  }
	}
        kml_train.insert_db(peptideVector);
    }
    else
    {
        cout << "[proctran] Unknown recalibration type.\n";
        return;
    }



    Scan scanBefore = scan;
    CalibrationParameters cpBefore = scan.calibrationParameters;

    RecalibratorSimple rks(cpBefore); //this fixes the mz of peaks in the scan?
    rks.recalibrate(scan);
    rks.recalibrate(scanBefore);

    //determine which peaks are actually around.
    KnownMassList::MatchResult before_train = kml_train.match(scanBefore, 4.2); //PM Change
    kml_train.replace_entryVector(before_train.getMatchedEntries());

    //pull a subset of peaks for testing purposes
    KnownMassList kml_test;
    kml_test.insert_entryVector(kml_train.splitEntries(.33));

    //do the match for real on the training set.
    before_train = kml_train.match(scanBefore, 4.2); //PM Change

    //do the match for real on the testing set.
    KnownMassList::MatchResult before_test = kml_test.match(scanBefore, 4.2); //PM Change

    //recalibrate
    RecalibratorKnownMassList rkml(kml_train);
    rkml.recalibrate(scan);

    KnownMassList::MatchResult after_train = kml_train.match(scan, 100.00); //PM Change  
    KnownMassList::MatchResult after_test = kml_test.match(scan, 100.00); //PM Change  

    CalibrationParameters cpAfter = scan.calibrationParameters;

    computePrintCalibrationStatistics(before_train,cpBefore,after_train,cpAfter,pathOutput,filenameBase,"train");
    computePrintCalibrationStatistics(before_test,cpBefore,after_test,cpAfter,pathOutput,filenameBase,"test");
}


void writePeakDataFile(const peakdata::Scan& scan, 
                       const bfs::path& filename)
{
    peakdata::PeakData pd;
    pd.scans.push_back(scan);

    bfs::ofstream os(filename);
    os << pd;
}


int all(const vector<string>& args, const Configuration& config)
{
    if (args.size() != 3) throw runtime_error("Wrong number of arguments.");
    bfs::path pathTransient(args[1]);
    bfs::path pathOutput(args[2]);

    string filenameBase = basename(pathTransient);
    bfs::create_directories(pathOutput);

    cout << "filename: " << pathTransient.string() << endl;
    cout << "outputDirectory: " << pathOutput.string() << endl;

    // fft
    TransientData td(pathTransient.string());
    FrequencyData fd;
    bfs::path pathCFD = pathOutput / (filenameBase + ".cfd");
    if (exists(pathCFD)) throw runtime_error(("[proctran] " + pathCFD.string() + " already exists.").c_str());
    fft(td, fd, config, pathCFD.string()); 

    // detect 
    peakdata::Scan scanDetected;
    bfs::path pathPeaksDetected = pathOutput / (filenameBase + ".detected.txt");
    bfs::ofstream osPeaksDetected(pathPeaksDetected);
    bfs::path pathPeakDetectorLog = pathOutput / (filenameBase + ".detected.log");
    bfs::ofstream osPeakDetectorLog; //(pathPeakDetectorLog);
    detect(fd, scanDetected, config, osPeaksDetected, &osPeakDetectorLog);
    writePeakDataFile(scanDetected, pathOutput / (filenameBase + ".detected.peaks")); 

    // freqest 
    peakdata::Scan scanEstimated;
    bfs::path pathPeaksEstimated = pathOutput / (filenameBase + ".estimated.txt");
    bfs::ofstream osPeaksEstimated(pathPeaksEstimated);
    string outputDirectoryFreqest = config.write_logs ? 
        (pathOutput / (filenameBase + ".estimated.logs")).string() : "";
    freqest(fd, scanDetected, scanEstimated, config, outputDirectoryFreqest, osPeaksEstimated); 
    writePeakDataFile(scanEstimated, pathOutput / (filenameBase + ".estimated.peaks")); 

    // calibrate
    string outputDirectoryCalibration = config.write_logs ? 
        (pathOutput / (filenameBase + ".dbcalibration.logs")).string() : "";
    calibrate(fd, scanEstimated, config, outputDirectoryCalibration);

    // recalibrate least squares
    peakdata::Scan scanRecalibrated = scanEstimated; // make a copy
    recalibrateLeastSquares(scanRecalibrated, pathOutput, filenameBase, config);
    writePeakDataFile(scanRecalibrated, pathOutput / (filenameBase + ".recal.peaks")); 

    cout << endl;
    return 0;
}


void initializeSubcommands(map<string, Subcommand>& subcommands, string& usage)
{
    subcommands["defaults"] = defaults;
    usage += "    defaults\n";
    usage += "        (outputs default options)\n\n";

    subcommands["fft"] = fft;
    usage += "    fft filename.dat filename.cfd\n";
    usage += "        (converts MIDAS file to FrequencyData)\n\n";

    subcommands["detect"] = detect;
    usage += "    detect filename.cfd\n";
    usage += "        (runs peak detector on frequency data)\n\n";

    subcommands["freqest"] = freqest;
    usage += "    freqest filename.cfd peaklist.txt [outputDirectory]\n";
    usage += "        (runs frequency estimator)\n\n";

    subcommands["calibrate"] = calibrate;
    usage += "    calibrate filename.cfd peaklist.txt outputDirectory\n";
    usage += "        (runs calibrator)\n\n";

    subcommands["all"] = all;
    usage += "    all filename.dat outputDirectory\n";
    usage += "        (runs everything)\n\n";
}


void parseCommandLine(int argc, char* argv[], Configuration& config, vector<string>& args, string& usage)
{
    string configFilename = "proctran.cfg";
    const char* label_config_file = "config_file";

    po::options_description od_config("Options");
    od_config.add_options()
        (label_config_file, 
            po::value<string>(&configFilename)->default_value(configFilename),
            "")
        (config.label_write_logs, 
            po::value<bool>(&config.write_logs)
                ->default_value(config.write_logs), 
            "")
        (config.label_fft_zero_padding, 
            po::value<unsigned int>(&config.fft_zero_padding)
                ->default_value(config.fft_zero_padding), 
            "")
        (config.label_peak_detector_type, 
            po::value<string>(&config.peak_detector_type)
                ->default_value(config.peak_detector_type),
            "")
        (config.label_detect_naive_noise_factor, 
            po::value<double>(&config.detect_naive_noise_factor)
                ->default_value(config.detect_naive_noise_factor), 
            "")
        (config.label_detect_naive_detection_radius, 
            po::value<unsigned int>(&config.detect_naive_detection_radius)
                ->default_value(config.detect_naive_detection_radius), 
            "")
        (config.label_detect_mf_filterMatchRate, 
            po::value<int>(&config.detect_mf_filterMatchRate)
                ->default_value(config.detect_mf_filterMatchRate), 
            "")
        (config.label_detect_mf_filterSampleRadius, 
            po::value<int>(&config.detect_mf_filterSampleRadius)
                ->default_value(config.detect_mf_filterSampleRadius), 
            "")
        (config.label_detect_mf_peakThresholdFactor, 
            po::value<double>(&config.detect_mf_peakThresholdFactor)
                ->default_value(config.detect_mf_peakThresholdFactor), 
            "")
        (config.label_detect_mf_peakMaxCorrelationAngle, 
            po::value<double>(&config.detect_mf_peakMaxCorrelationAngle)
                ->default_value(config.detect_mf_peakMaxCorrelationAngle), 
            "")
        (config.label_detect_mf_isotopeThresholdFactor, 
            po::value<double>(&config.detect_mf_isotopeThresholdFactor)
                ->default_value(config.detect_mf_isotopeThresholdFactor), 
            "")
        (config.label_detect_mf_isotopeMaxChargeState, 
            po::value<int>(&config.detect_mf_isotopeMaxChargeState)
                ->default_value(config.detect_mf_isotopeMaxChargeState), 
            "")
        (config.label_detect_mf_isotopeMaxNeutronCount, 
            po::value<int>(&config.detect_mf_isotopeMaxNeutronCount)
                ->default_value(config.detect_mf_isotopeMaxNeutronCount), 
            "")
        (config.label_detect_mf_collapseRadius, 
            po::value<double>(&config.detect_mf_collapseRadius)
                ->default_value(config.detect_mf_collapseRadius), 
            "")
        (config.label_freqest_type, 
            po::value<string>(&config.freqest_type)
                ->default_value(config.freqest_type_physical),
            "")
        (config.label_freqest_physical_window_radius, 
            po::value<unsigned int>(&config.freqest_physical_window_radius)
                ->default_value(config.freqest_physical_window_radius), 
            "")
        (config.label_freqest_physical_iteration_count, 
            po::value<unsigned int>(&config.freqest_physical_iteration_count)
                ->default_value(config.freqest_physical_iteration_count), 
            "")
        (config.label_calibrate_database_filename, 
            po::value<string>(&config.calibrate_database_filename)
                ->default_value(config.calibrate_database_filename), 
            "")
        (config.label_calibrate_initial_error_estimate, 
            po::value<double>(&config.calibrate_initial_error_estimate)
                ->default_value(config.calibrate_initial_error_estimate), 
            "")
        (config.label_calibrate_error_estimator_iteration_count, 
            po::value<unsigned int>(&config.calibrate_error_estimator_iteration_count)
                ->default_value(config.calibrate_error_estimator_iteration_count), 
            "")
        (config.label_calibrate_calibrator_iteration_count, 
            po::value<unsigned int>(&config.calibrate_calibrator_iteration_count)
                ->default_value(config.calibrate_calibrator_iteration_count), 
            "")
        (config.label_recal_type, 
            po::value<string>(&config.recal_type)
                ->default_value(config.recal_type),
            "")
       (config.label_lsq_calibrate_database_filename, 
            po::value<string>(&config.lsq_calibrate_database_filename)
                ->default_value(config.lsq_calibrate_database_filename), 
            "")
         ;

    // append options description to usage string

    ostringstream usage_options;
    usage_options << od_config << endl;
    usage += usage_options.str();
    usage += "Notes:\n";
    usage += "  Options will be read from the specified config file if it exists.\n";
    usage += "\n";
    usage += "  To recreate the default config file, use:\n"; 
    usage += "      proctran defaults > proctran.cfg\n";
    usage += "\n";
    usage += "  freqest_type = physical | parabola | lorentzian\n";
    usage += "\n";
    usage += "  peak_detector_type = naive | matched_filter\n";
    usage += "\n";
    usage += "  recal_type = 5pep | calmix | db\n";

    // handle positional arguments

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()
        (label_args, po::value< vector<string> >(), "")
        ;

    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);
    
    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    // parse and save results

    po::variables_map vm;
    po::store(po::command_line_parser(argc, argv).
          options(od_parse).positional(pod_args).run(), vm);

    ifstream is(vm[label_config_file].as<string>().c_str());
    po::store(parse_config_file(is, od_config), vm);

    po::notify(vm);

    if (vm.count(label_args))
        args = vm[label_args].as< vector<string> >();
}


int main(int argc, char* argv[])
{
    try
    {
        string usage("Usage: proctran subcommand [args] [options]\n\nSubcommands:\n\n");

        map<string, Subcommand> subcommands;
        initializeSubcommands(subcommands, usage);

        Configuration config;
        vector<string> args;
        parseCommandLine(argc, argv, config, args, usage);  

        Subcommand subcommand = args.empty() ? 0 : subcommands[args[0]];
        if (!subcommand) throw runtime_error(usage);
        
        if (subcommand != defaults)
            cerr << config << endl;

        return subcommand(args, config);
    }
    catch (exception& e)
    {
        cout << e.what() << endl << endl;
        return 1;
    }
}

