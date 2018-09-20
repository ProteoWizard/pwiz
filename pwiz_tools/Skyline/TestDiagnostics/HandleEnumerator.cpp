#include "HandleEnumerator.h"
#include <vector>
#include <iterator>
#include <memory>
#include <ntstatus.h>

template <typename T>
CLREnumerator<T>::CLREnumerator() = default;

template <typename T>
CLREnumerator<T>::~CLREnumerator() = default;

template <typename T>
CLREnumerator<T>::CLREnumerator(const CLREnumerator& other) = default;

template <typename T>
CLREnumerator<T>::CLREnumerator(CLREnumerator&& other) noexcept = default;

template <typename T>
CLREnumerator<T>& CLREnumerator<T>::operator=(const CLREnumerator& other) = default;

template <typename T>
CLREnumerator<T>& CLREnumerator<T>::operator=(CLREnumerator&& other) noexcept = default;

HandleEnumerator::HandleEnumerator() :
    _processId(GetCurrentProcessId()),
    _handleIndex(-1)
{
    ULONG bufferSize = sizeof(SYSTEM_HANDLE_INFORMATION);
    auto buffer = new BYTE[bufferSize];
    while (NtQuerySystemInformation(static_cast<SYSTEM_INFORMATION_CLASS>(16), buffer, bufferSize, nullptr) ==
        STATUS_INFO_LENGTH_MISMATCH) {
        delete[] buffer;
        bufferSize += bufferSize - sizeof(ULONG);
        buffer = new BYTE[bufferSize];
    }

    _handleInfos = std::shared_ptr<SYSTEM_HANDLE_INFORMATION>(reinterpret_cast<SYSTEM_HANDLE_INFORMATION*>(buffer),
        [](SYSTEM_HANDLE_INFORMATION* ptr) -> void
    {
        delete[] ptr;
    });

    std::vector<HandleInfo> handleEntries;
}

HandleEnumerator::~HandleEnumerator() = default;
HandleEnumerator::HandleEnumerator(const HandleEnumerator& other) = default;
HandleEnumerator::HandleEnumerator(HandleEnumerator&& other) noexcept = default;
HandleEnumerator& HandleEnumerator::operator=(const HandleEnumerator& other) = default;
HandleEnumerator& HandleEnumerator::operator=(HandleEnumerator&& other) noexcept = default;

const HandleInfo* HandleEnumerator::Current()
{
    if (_handleIndex < 0 || _handleIndex >= int(_handleInfos->NumberOfHandles))
        return nullptr;

    return &_current;
}

bool HandleEnumerator::MoveNext()
{
    if (_handleIndex < 0)
        _handleIndex = 0;

    for (; _handleIndex < int(_handleInfos->NumberOfHandles); ++_handleIndex) {
        if (_handleInfos->Handles[_handleIndex].UniqueProcessId == _processId) {
            auto typeInfoBytes = new BYTE[sizeof(PUBLIC_OBJECT_TYPE_INFORMATION)];
            const auto handle = reinterpret_cast<HANDLE>(_handleInfos->Handles[_handleIndex].HandleValue);
            ULONG size;
            auto queryResult = NtQueryObject(handle, ObjectTypeInformation, typeInfoBytes,
                sizeof(PUBLIC_OBJECT_TYPE_INFORMATION), &size);
            if (queryResult == STATUS_INFO_LENGTH_MISMATCH) {
                delete[] typeInfoBytes;
                typeInfoBytes = new BYTE[size];
                queryResult = NtQueryObject(handle, ObjectTypeInformation, typeInfoBytes, size, nullptr);
            }

            if (NT_SUCCESS(queryResult)) {
                const auto typeInfo = reinterpret_cast<PUBLIC_OBJECT_TYPE_INFORMATION*>(typeInfoBytes);
                const auto type = std::wstring(typeInfo->TypeName.Buffer, typeInfo->TypeName.Length / sizeof(WCHAR));
                _current = HandleInfo(handle, type, _handleInfos->Handles[_handleIndex].ObjectTypeIndex);
            }

            delete[] typeInfoBytes;
            ++_handleIndex;
            return true;
        }
    }

    return false;
}

void HandleEnumerator::Reset()
{
    _handleIndex = -1;
}