#include "HandleEnumerator.h"
#include <memory>
#include <ntstatus.h>

std::vector<HandleEnumerator::HandleInfo> HandleEnumerator::GetHandleInfos()
{
    const auto processId = GetCurrentProcessId();
    std::vector<HandleInfo> result;

    ULONG bufferSize = sizeof(SYSTEM_HANDLE_INFORMATION);
    auto buffer = new BYTE[bufferSize];
    while (NtQuerySystemInformation(static_cast<SYSTEM_INFORMATION_CLASS>(16), buffer, bufferSize, nullptr) ==
        STATUS_INFO_LENGTH_MISMATCH) {
        delete[] buffer;
        bufferSize += bufferSize - sizeof(ULONG);
        buffer = new BYTE[bufferSize];
    }

    const auto handleInfos = std::shared_ptr<SYSTEM_HANDLE_INFORMATION>(reinterpret_cast<SYSTEM_HANDLE_INFORMATION*>(buffer),
        [](SYSTEM_HANDLE_INFORMATION* ptr) -> void
    {
        delete[] ptr;
    });

    for (auto i = 0; i < static_cast<int>(handleInfos->NumberOfHandles); ++i) {
        if (handleInfos->Handles[i].UniqueProcessId == processId) {
            auto typeInfoBytes = new BYTE[sizeof(PUBLIC_OBJECT_TYPE_INFORMATION)];
            const auto handle = reinterpret_cast<HANDLE>(handleInfos->Handles[i].HandleValue);
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
                result.emplace_back(handle, type, handleInfos->Handles[i].ObjectTypeIndex);
            }

            delete[] typeInfoBytes;
        }
    }

    return result;
}
