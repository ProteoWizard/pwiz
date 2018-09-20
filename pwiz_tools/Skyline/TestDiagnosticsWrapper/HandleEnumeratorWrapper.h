#pragma once
#include "../TestDiagnostics/HandleEnumerator.h"

public ref class HandleInfoWrapper
{
public:
    HandleInfoWrapper(HandleInfo handleInfo)
    {
        Handle = System::IntPtr(handleInfo.Handle);
        Type = gcnew System::String(const_cast<wchar_t*>(handleInfo.Type.c_str()));
        ObjectTypeIndex = handleInfo.ObjectTypeIndex;
    }

    System::IntPtr Handle;
    System::String^ Type;
    System::Byte ObjectTypeIndex;
};

public ref class HandleEnumeratorWrapper : public System::Collections::Generic::IEnumerator<HandleInfoWrapper^>
{
public:
    HandleEnumeratorWrapper();
    virtual ~HandleEnumeratorWrapper();

    virtual bool MoveNext();

    property Object^ CurrentObject
    {
        virtual Object^ get() = System::Collections::IEnumerator::Current::get;
    }

    property HandleInfoWrapper^ Current
    {
        virtual HandleInfoWrapper^ get() = System::Collections::Generic::IEnumerator<HandleInfoWrapper^>::Current::get;
    }

    virtual void Reset();
private:
    HandleEnumerator* _handleEnumerator;
};