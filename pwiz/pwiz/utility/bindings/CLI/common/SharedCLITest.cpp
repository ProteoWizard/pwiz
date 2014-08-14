//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#include "SharedCLI.hpp"
#include "unit.hpp"


using namespace pwiz::CLI::util;
using namespace System;


namespace native {

struct Child
{
    int simplePrimitiveVariable;
    size_t nonSimplePrimitiveVariable;
    std::string stringVariable;
};

struct SharedChild
{
    int simplePrimitiveVariable;
    size_t nonSimplePrimitiveVariable;
    std::string stringVariable;
};

typedef boost::shared_ptr<SharedChild> ChildPtr;

struct Parent
{
    Child referenceVariable;
    ChildPtr sharedReferenceVariablePtr;
};

} // namespace native


public ref class Child
{
    DEFINE_INTERNAL_BASE_CODE(Child, native::Child)

    public:
    DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, simplePrimitiveVariable)
    DEFINE_PRIMITIVE_PROPERTY(size_t, System::UInt64, nonSimplePrimitiveVariable)
    DEFINE_STRING_PROPERTY(stringVariable)
};

public ref class SharedChild
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(native, SharedChild)

    public:
    DEFINE_SIMPLE_PRIMITIVE_PROPERTY(int, simplePrimitiveVariable)
    DEFINE_PRIMITIVE_PROPERTY(size_t, System::UInt64, nonSimplePrimitiveVariable)
    DEFINE_STRING_PROPERTY(stringVariable)
};

public ref class Parent
{
    DEFINE_INTERNAL_BASE_CODE(Parent, native::Parent)

    public:
    DEFINE_REFERENCE_PROPERTY(Child, referenceVariable)
    DEFINE_SHARED_REFERENCE_PROPERTY(native::ChildPtr, SharedChild, sharedReferenceVariablePtr, sharedReferenceVariable)
};


void test()
{
    native::Parent nativeParent;
    native::Child& nativeChild = nativeParent.referenceVariable;
    nativeChild.simplePrimitiveVariable = -123;
    nativeChild.nonSimplePrimitiveVariable = 123u;
    nativeChild.stringVariable = "123";

    native::ChildPtr& nativeSharedChild = nativeParent.sharedReferenceVariablePtr;
    nativeSharedChild.reset(new native::SharedChild);
    nativeSharedChild->simplePrimitiveVariable = -789;
    nativeSharedChild->nonSimplePrimitiveVariable = 789u;
    nativeSharedChild->stringVariable = "789";

    // create an owning object for the cliParent so it doesn't try to delete nativeParent
    IntPtr nativeParentOwner(&nativeParent);

    // get CLI wrappers for the native objects
    Parent^ cliParent = gcnew Parent(&nativeParent, nativeParentOwner);
    Child^ cliChild = cliParent->referenceVariable;
    SharedChild^ cliSharedChild = cliParent->sharedReferenceVariable;

    unit_assert(cliChild->simplePrimitiveVariable == -123);
    unit_assert(cliChild->nonSimplePrimitiveVariable == 123);
    unit_assert(cliChild->stringVariable == "123");

    unit_assert(cliSharedChild->simplePrimitiveVariable == -789);
    unit_assert(cliSharedChild->nonSimplePrimitiveVariable == 789);
    unit_assert(cliSharedChild->stringVariable == "789");

    // free the native pointer
    nativeSharedChild.reset();

    // test that the CLI shared pointer is still valid even after the native pointer is gone
    unit_assert(cliSharedChild->simplePrimitiveVariable == -789);
    unit_assert(cliSharedChild->nonSimplePrimitiveVariable == 789);
    unit_assert(cliSharedChild->stringVariable == "789");

    // refresh the CLI reference
    cliSharedChild = cliParent->sharedReferenceVariable;
    unit_assert(cliSharedChild == nullptr);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
