//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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


#define PWIZ_SOURCE

#pragma unmanaged
#include "ShimadzuReader.hpp"
#include "pwiz/utility/misc/Std.hpp"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;


using System::String;
using System::Math;
using System::Collections::Generic::IList;
namespace ShimadzuAPI = Shimadzu::LabSolutions::DataReader;
using ShimadzuAPI::ReaderResult;


namespace pwiz {
namespace vendor_api {
namespace Shimadzu {


class ChromatogramImpl : public Chromatogram
{
    public:
    ChromatogramImpl(ShimadzuAPI::MRMChromatogram^ chromatogram, const SRMTransition& transition)
        : transition_(transition),
          chromatogram_(chromatogram)
    {}

    virtual const SRMTransition& getTransition() const { return transition_; }
    virtual int getTotalDataPoints() const { try { return chromatogram_->NumDataPoints; } CATCH_AND_FORWARD }
    virtual void getXArray(std::vector<double>& x) const { try { ToStdVector(chromatogram_->Times, x); } CATCH_AND_FORWARD }
    virtual void getYArray(std::vector<double>& y) const { try { ToStdVector(chromatogram_->Intensities, y); } CATCH_AND_FORWARD }

    private:
    SRMTransition transition_;
    gcroot<ShimadzuAPI::MRMChromatogram^> chromatogram_;
};


class ShimadzuReaderImpl : public ShimadzuReader
{
    public:
    ShimadzuReaderImpl(const string& filepath)
    {
        try
        {
            reader_ = gcnew ShimadzuAPI::MassDataReader();
            String^ systemFilepath = ToSystemString(filepath);
            ReaderResult result = reader_->OpenDataFile(systemFilepath);
            if (result != ReaderResult::OK)
                throw runtime_error("[ShimadzuReader::ctor] " + ToStdString(System::Enum::GetName(result.GetType(), (System::Object^) result)));
        }
        CATCH_AND_FORWARD
    }

    virtual ~ShimadzuReaderImpl() { reader_->CloseDataFile(); }

    //virtual std::string getVersion() const = 0;
    //virtual DeviceType getDeviceType() const = 0;
    //virtual std::string getDeviceName(DeviceType deviceType) const = 0;
    //virtual boost::local_time::local_date_time getAcquisitionTime() const = 0;

    virtual const set<SRMTransition>& getTransitions() const
    {
        if (!transitionSet_.empty())
            return transitionSet_;

        try
        {
            for each (ShimadzuAPI::MrmTransition^ transition in reader_->GetMrmTransition())
            {
                SRMTransition t;
                t.id = transition->Number;
                t.channel = transition->Channel;
                t.event = transition->Event;
                t.segment = transition->Segment;
                t.collisionEnergy = transition->CE;
                t.Q1 = (transition->ParentMz[0] + transition->ParentMz[1]) / 2;
                t.Q3 = (transition->ChildMz[0] + transition->ChildMz[1]) / 2;
                transitionSet_.insert(transitionSet_.end(), t);
                transitions_[t.id] = transition;
            }
            return transitionSet_;
        }
        CATCH_AND_FORWARD
    }

    virtual ChromatogramPtr getChromatogram(const SRMTransition& transition) const
    {
        try { return ChromatogramPtr(new ChromatogramImpl(reader_->GetChromatogram(transitions_[transition.id]), transition)); } CATCH_AND_FORWARD
    }

    private:
    gcroot<ShimadzuAPI::MassDataReader^> reader_;
    mutable map<short, gcroot<ShimadzuAPI::MrmTransition^> > transitions_;
    mutable set<SRMTransition> transitionSet_;
};


PWIZ_API_DECL
ShimadzuReaderPtr ShimadzuReader::create(const string& filepath)
{
    try { return ShimadzuReaderPtr(new ShimadzuReaderImpl(filepath)); } CATCH_AND_FORWARD
}


#pragma unmanaged
PWIZ_API_DECL
bool SRMTransition::operator< (const SRMTransition& rhs) const
{
    return id < rhs.id;
}


} // Shimadzu
} // vendor_api
} // pwiz

