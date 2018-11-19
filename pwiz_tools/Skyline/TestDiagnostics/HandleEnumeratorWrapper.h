#pragma once
#include "HandleEnumerator.h"

public ref class HandleInfoWrapper
{
public:
    HandleInfoWrapper(HandleEnumerator::HandleInfo handleInfo)
    {
        Handle = System::IntPtr(handleInfo.Handle);
        Type = gcnew System::String(const_cast<wchar_t*>(handleInfo.Type.c_str()));
        ObjectTypeIndex = handleInfo.ObjectTypeIndex;
    }

    System::IntPtr Handle;
    System::String^ Type;
    System::Byte ObjectTypeIndex;
};

public ref class HandleEnumeratorWrapper
{
public:
    static array<HandleInfoWrapper^>^ GetHandleInfos();
};