#pragma once
#include "NT.h"
#include <utility>
#include <xstring>
#include <memory>
#include <vector>

namespace HandleEnumerator
{
    #pragma pack(push, 1)
    struct HandleInfo
    {
        HandleInfo(HANDLE handle, std::wstring type, UCHAR objectTypeIndex) :
            Handle(handle),
            Type(std::move(type)),
            ObjectTypeIndex(objectTypeIndex)
        {
        }

        HandleInfo() :
            HandleInfo(nullptr, L"", 0x00)
        {
        }

        HANDLE Handle;
        std::wstring Type;
        UCHAR ObjectTypeIndex;
    };
    #pragma pack(pop)

    std::vector<HandleInfo> GetHandleInfos();
}