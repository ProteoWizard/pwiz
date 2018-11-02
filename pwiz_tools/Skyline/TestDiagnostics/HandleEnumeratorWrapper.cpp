#include "HandleEnumeratorWrapper.h"

array<HandleInfoWrapper^>^ HandleEnumeratorWrapper::GetHandleInfos()
{
    auto handleInfoVector = HandleEnumerator::GetHandleInfos();
    auto result = gcnew array<HandleInfoWrapper^>(static_cast<int>(handleInfoVector.size()));
    for (auto i = 0; i < handleInfoVector.size(); ++i)
        result[i] = gcnew HandleInfoWrapper(handleInfoVector[i]);
    return result;
}