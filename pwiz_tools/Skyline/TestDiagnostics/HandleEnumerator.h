#pragma once
#include "NT.h"
#include <xstring>
#include <memory>

#pragma pack(push, 1)
struct HandleInfo
{
    HandleInfo(HANDLE handle, const std::wstring& type, UCHAR objectTypeIndex) :
        Handle(handle),
        Type(type),
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

struct HandleInfoList
{
    int Count;
    HandleInfo* Items;
};


template<typename T>
class CLREnumerator
{
public:
    CLREnumerator();
    virtual ~CLREnumerator();
    CLREnumerator(const CLREnumerator& other);
    CLREnumerator(CLREnumerator&& other) noexcept;
    CLREnumerator& operator=(const CLREnumerator& other);
    CLREnumerator& operator=(CLREnumerator&& other) noexcept;

    virtual const T* Current() = 0;
    virtual bool MoveNext() = 0;
    virtual void Reset() = 0;
};

class HandleEnumerator : public CLREnumerator<HandleInfo>
{
public:
    HandleEnumerator();

    virtual ~HandleEnumerator();
    HandleEnumerator(const HandleEnumerator& other);
    HandleEnumerator(HandleEnumerator&& other) noexcept;
    HandleEnumerator& operator=(const HandleEnumerator& other);
    HandleEnumerator& operator=(HandleEnumerator&& other) noexcept;

    const HandleInfo* Current() override;
    bool MoveNext() override;
    void Reset() override;

private:
    DWORD _processId{};
    int _handleIndex{};
    HandleInfo _current;

    std::shared_ptr<SYSTEM_HANDLE_INFORMATION>  _handleInfos;
};
