#ifndef COMINITIALIZER_HPP_
#define COMINITIALIZER_HPP_


#include "ObjBase.h" // basic COM declarations


namespace pwiz {
namespace util {


// singleton used to initialize and deinitialize COM only once
class COMInitializer
{
    public:
    COMInitializer() : refCount_(0)
    {
    }

    ~COMInitializer()
    {
        refCount_ = 1;
        uninitialize();
    }

    void initialize()
    {   
        if (!refCount_)
            CoInitialize(NULL);
        ++refCount_;
    }

    void uninitialize()
    {
        if (refCount_)
            --refCount_;
        else
            CoUninitialize();
    }

    private:
    int refCount_; // TODO: track on a per thread basis
};

extern COMInitializer g_COMInitializer;

} // namespace util
} // namespace pwiz


#endif // COMINITIALIZER_HPP_
