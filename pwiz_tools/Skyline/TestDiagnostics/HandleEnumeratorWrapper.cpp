#include "HandleEnumeratorWrapper.h"

HandleEnumeratorWrapper::HandleEnumeratorWrapper()
{
    _handleEnumerator = new HandleEnumerator();
}

HandleEnumeratorWrapper::~HandleEnumeratorWrapper()
{
    delete _handleEnumerator;
}

bool HandleEnumeratorWrapper::MoveNext()
{
    return _handleEnumerator->MoveNext();
}

void HandleEnumeratorWrapper::Reset()
{
    _handleEnumerator->Reset();
}

HandleInfoWrapper^ HandleEnumeratorWrapper::Current::get()
{
    const auto current = _handleEnumerator->Current();
    if (current == nullptr)
        return nullptr;
    return gcnew HandleInfoWrapper(*current);
}

System::Object^ HandleEnumeratorWrapper::CurrentObject::get()
{
    return Current;
}
