//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Slightly modified version of gcroot.h that pins the GCHandle and allows access to the handle.
//

#pragma once
#include <gcroot.h>

#ifdef __cplusplus_cli
#define __GCHANDLE_TO_VOIDPTR(x) ((GCHandle::operator System::IntPtr(x)).ToPointer())
#define __VOIDPTR_TO_GCHANDLE(x) (GCHandle::operator GCHandle(System::IntPtr(x)))
#define __NULLPTR nullptr
#else  /* __cplusplus_cli */
#define __GCHANDLE_TO_VOIDPTR(x) ((GCHandle::op_Explicit(x)).ToPointer())
#define __VOIDPTR_TO_GCHANDLE(x) (GCHandle::op_Explicit(x))
#define __NULLPTR 0
#endif  /* __cplusplus_cli */

#ifndef __DEFINE_GCROOT_IN_GLOBAL_NAMESPACE
namespace msclr
{
#endif  /* __DEFINE_GCROOT_IN_GLOBAL_NAMESPACE */

    /// pinned_gcroot: a slightly modified gcroot that pins its GCHandle
    template <class T> struct pinned_gcroot {

        typedef System::Runtime::InteropServices::GCHandle GCHandle;
        typedef System::Runtime::InteropServices::GCHandleType GCHandleType;

        // always allocate a new handle during construction (see above)
        //
        // Initializes to a NULL handle, which is always safe
        [System::Diagnostics::DebuggerStepThroughAttribute]
        [System::Security::SecuritySafeCritical]
        pinned_gcroot() {
            _handle = __GCHANDLE_TO_VOIDPTR(GCHandle::Alloc(__NULLPTR, GCHandleType::Pinned));
        }

        // this can't be T& here because & does not yet work on managed types
        // (T should be a pointer anyway).
        //
        pinned_gcroot(T t) {
            _handle = __GCHANDLE_TO_VOIDPTR(GCHandle::Alloc(t, GCHandleType::Pinned));
        }

        pinned_gcroot(const pinned_gcroot& r) {
            // don't copy a handle, copy what it points to (see above)
            _handle = __GCHANDLE_TO_VOIDPTR(
                GCHandle::Alloc(
                    __VOIDPTR_TO_GCHANDLE(r._handle).Target, GCHandleType::Pinned));
        }

        // Since C++ objects and handles are allocated 1-to-1, we can
        // free the handle when the object is destroyed
        //
        [System::Diagnostics::DebuggerStepThroughAttribute]
        [System::Security::SecurityCritical]
        ~pinned_gcroot() {
            GCHandle g = __VOIDPTR_TO_GCHANDLE(_handle);
            g.Free();
            _handle = 0; // should fail if reconstituted
        }

        [System::Diagnostics::DebuggerStepThroughAttribute]
        [System::Security::SecurityCritical]
        pinned_gcroot& operator=(T t) {
            // no need to check for valid handle; was allocated in ctor
            __VOIDPTR_TO_GCHANDLE(_handle).Target = t;
            return *this;
        }

        pinned_gcroot& operator=(const pinned_gcroot &r) {
            // no need to check for valid handle; was allocated in ctor
            T t = (T)r;
            __VOIDPTR_TO_GCHANDLE(_handle).Target = t;
            return *this;
        }

        void swap(pinned_gcroot<T> & _right)
        {
            using std::swap;
            swap(_handle, _right._handle);
        }

        // The managed object is not a secret or protected resource, so it's okay to expose to anyone who has access to the gcroot object
        [System::Security::SecuritySafeCritical]
        operator T () const {
            // gcroot is typesafe, so use static_cast
            return static_cast<T>(__VOIDPTR_TO_GCHANDLE(_handle).Target);
        }

        // don't return T& here because & to gc pointer not yet implemented
        // (T should be a pointer anyway).
        [System::Security::SecuritySafeCritical]
        T operator->() const {
            // gcroot is typesafe, so use static_cast
            return static_cast<T>(__VOIDPTR_TO_GCHANDLE(_handle).Target);
        }

        System::IntPtr operator& () const {
            return __VOIDPTR_TO_GCHANDLE(_handle).AddrOfPinnedObject();
        }

        void* handle() const {
            return _handle;
        }

        private:
        // Don't let anyone copy the handle value directly, or make a copy
        // by taking the address of this object and pointing to it from
        // somewhere else.  The root will be freed when the dtor of this
        // object gets called, and anyone pointing to it still will
        // cause serious harm to the Garbage Collector.
        //
        void* _handle;
    };

    template<typename T>
    void swap(pinned_gcroot<T> & _left,
        pinned_gcroot<T> & _right)
    {
        _left.swap(_right);
    }

#ifndef __DEFINE_GCROOT_IN_GLOBAL_NAMESPACE
} // namespace msclr
#endif  /* __DEFINE_GCROOT_IN_GLOBAL_NAMESPACE */

#undef __GCHANDLE_TO_VOIDPTR
#undef __VOIDPTR_TO_GCHANDLE
#undef __NULLPTR


