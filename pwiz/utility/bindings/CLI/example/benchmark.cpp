;//
 // $Id$
 //
 //
 // Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 //
 // Copyright 2017 Matt Chambers
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
#include "../common/SharedCLI.hpp"
#include <stdexcept>
#include <vector>
#include <boost/chrono.hpp>
#include <boost/format.hpp>
#include <string>

#using <System.dll>
using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::util;
using namespace System::Collections::Generic;
using namespace System::Diagnostics;
using System::Exception;
using System::String;
using System::Console;

std::string keyValueProcessTimes(const boost::chrono::process_cpu_clock_times& times)
{
    return (boost::format("(real: %.3f; user: %.3f; sys: %.3f) seconds")
        % (times.real / 1e9)
        % (times.user / 1e9)
        % (times.system / 1e9)).str();
}

void benchmark(String^ filename, System::Collections::Generic::List<String^>^ filters, ReaderConfig^ readerConfig)
{
    ReaderList^ readers = ReaderList::FullReaderList;
    MSDataList^ results = gcnew MSDataList();
    readers->read(filename, results, readerConfig);
    MSData^ msd = results[0];
    pwiz::CLI::analysis::SpectrumListFactory::wrap(msd, filters);
    auto sl = msd->run->spectrumList;
    int numSpectra = sl->size();
    int64_t dataPoints = 0;
    double totalIntensity = 0;

    auto start = boost::chrono::process_cpu_clock::now();

    for (int i = 0; i < numSpectra; ++i)
    {
        auto s = sl->spectrum(i, true);

        auto mzArray = s->getMZArray();
        auto intensityArray = s->getIntensityArray();

        if (mzArray == nullptr || intensityArray == nullptr)
            continue;

        auto mzArrayData = mzArray->data;
        auto intensityArrayData = intensityArray->data;

        if (mzArrayData == nullptr || intensityArrayData == nullptr)
            continue;

        dataPoints += mzArrayData->Count;
        for each (double x in intensityArrayData)
            totalIntensity += x;

        if (i == 0 || (i % 1000) == 0)
            Console::Write("{0} spectra, {1} data points\r", i, dataPoints);
    }
    auto stop = boost::chrono::process_cpu_clock::now();

    Console::Write("{0} spectra, {1} data points, {3} total intensity, enumerated in {2}", numSpectra, dataPoints, ToSystemString(keyValueProcessTimes((stop - start).count())), totalIntensity);
}

[System::LoaderOptimizationAttribute(System::LoaderOptimization::NotSpecified)]
int main(cli::array<String^>^ args)
{
    try
    {
        if (args->Length < 2 || args[0] == "--help")
        {
            Console::WriteLine("Usage: benchmark <full-data|full-metadata|fast-metadata|instant-metadata> <filename> [--filter <filter name> <options>] [another filter] [optional flags]\n\n"
                 "Iterates over a file's spectra to test CLI bindings' reader speed.\n\n"
                 "See msconvert documentation for supported --filter's.\n\n"
                 "Optional flags are:\n"
                 "  --acceptZeroLengthSpectra (skip expensive checking for empty spectra when opening a file)\n"
                 "  --ignoreZeroIntensityPoints (read profile data exactly as the vendor provides, even if there are no flanking zero points)\n"
                 //"  --reverse (iterate backwards)\n\n"
                 "https://github.com/ProteoWizard\n"
                 "support@proteowizard.org\n");

            if (args[0] == "--help")
                pwiz::CLI::analysis::SpectrumListFactory::usage();

            return 1;
        }

        String^ dlArg = args[0];
        DetailLevel detailLevel;
        if (dlArg == "binary" || dlArg == "full-data")
            detailLevel = DetailLevel::FullData;
        else if (dlArg == "no-binary" || dlArg == "full-metadata")
            detailLevel = DetailLevel::FullMetadata;
        else if (dlArg == "fast-metadata")
            detailLevel = DetailLevel::FastMetadata;
        else if (dlArg == "instant-metadata")
            detailLevel = DetailLevel::InstantMetadata;
        else
            throw gcnew Exception("[benchmark] First argument must be one of [full-data, full-metadata, fast-metadata, instant-metadata]");

        String^ filename = args[1];

        ReaderConfig^ readerConfig = gcnew ReaderConfig();
        bool loop = false;
        //bool reverseIteration = false;

        auto filters = gcnew System::Collections::Generic::List<String^>();
        for (int i = 2; i < args->Length; i += 2)
            if (args[i] == "--filter")
            {
                if (i + 1 == args->Length)
                    throw gcnew Exception("[benchmark] no options passed to --filter parameter");
                filters->Add(args[i + 1]);
            }
            else if (args[i] == "--ignoreZeroIntensityPoints")
            {
                readerConfig->ignoreZeroIntensityPoints = true;
                --i;
            }
            else if (args[i] == "--acceptZeroLengthSpectra")
            {
                readerConfig->acceptZeroLengthSpectra = true;
                --i;
            }
            else if (args[i] == "--loop")
            {
                loop = true;
                --i;
            }
            /*else if (args[i] == "--reverse")
            {
                reverseIteration = true;
                --i;
            }*/
            else
                throw gcnew Exception("[benchmark] unknown option \"" + args[i] + "\"");


        do
        {
            benchmark(filename, filters, readerConfig);

            System::Runtime::GCSettings::LargeObjectHeapCompactionMode = System::Runtime::GCLargeObjectHeapCompactionMode::CompactOnce;
            System::GC::Collect();
            System::GC::WaitForPendingFinalizers();
            System::GC::Collect();
            auto memoryUsage = Process::GetCurrentProcess()->PrivateMemorySize64;

            Console::WriteLine(", memory {0}KiB",  memoryUsage/(1 << 10));

        } while (loop);
    }
    catch (std::exception& e)
    {
        Console::WriteLine("std::exception: {0}", gcnew String(e.what()));
        return 1;
    }
    catch (System::Exception^ e)
    {
        Console::WriteLine("System.Exception: " + e->Message);
        return 1;
    }
    catch (...)
    {
        Console::WriteLine("Caught unknown exception.");
        return 1;
    }
    return 1;
}