//
// $Id$
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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Matt Chambers.
//


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "BaseRunTimeConfig.h"


using namespace pwiz::util;
using namespace freicore;


#define TEST_RUNTIME_CONFIG \
    RTCONFIG_VARIABLE( string,          TestString,           "foo"            ) \
    RTCONFIG_VARIABLE( bool,            TestBoolean,          true             ) \
    RTCONFIG_VARIABLE( int,             TestInteger,          42               ) \
    RTCONFIG_VARIABLE( long,            TestLong,             42               ) \
    RTCONFIG_VARIABLE( boost::int64_t,  TestInt64,            42               ) \
    RTCONFIG_VARIABLE( boost::uint64_t, TestUInt64,           42               ) \
    RTCONFIG_VARIABLE( float,           TestFloat,            0.42f            ) \
    RTCONFIG_VARIABLE( double,          TestDouble,           0.42             ) \
    RTCONFIG_VARIABLE( IntegerSet,      TestIntegerSet,       string("[-4,2]") ) \
    RTCONFIG_VARIABLE( MZTolerance,     TestMZTolerance,      string("42 ppm") )

#define DERIVED_TEST_RUNTIME_CONFIG \
    RTCONFIG_VARIABLE( string,          TestString2,           "foo2"          ) \
    RTCONFIG_VARIABLE( bool,            TestBoolean2,          true            ) \
    RTCONFIG_VARIABLE( int,             TestInteger2,          422             ) \

struct TestRunTimeConfig : public BaseRunTimeConfig
{
    RTCONFIG_DEFINE_MEMBERS( TestRunTimeConfig, TEST_RUNTIME_CONFIG, "test.cfg" )

    void finalize()
    {
        BaseRunTimeConfig::finalize();
    }

    ostringstream& warnings() {return m_warnings;}
};

struct DerivedTestRunTimeConfig : public TestRunTimeConfig
{
    RTCONFIG_DEFINE_MEMBERS_EX( DerivedTestRunTimeConfig, TestRunTimeConfig, DERIVED_TEST_RUNTIME_CONFIG, "test.cfg" )

    void finalize()
    {
        TestRunTimeConfig::finalize();
    }
};


void testDerived()
{
    // test validation warnings
    {
        DerivedTestRunTimeConfig rtConfig(false);

        rtConfig.initializeFromBuffer("TestInteger2 = 31\r\nTestInteger2 = 42\r\nTestInteger2 = 42");
        unit_assert_operator_equal("Line 2: \"TestInteger2\" has already been defined.\n"
                                    "Line 3: \"TestInteger2\" has already been defined.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger2 =");
        unit_assert_operator_equal("Line 1: no value set for \"TestInteger2\"; did you mean to use an empty string (\"\")?\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger2 = ");
        unit_assert_operator_equal("Line 1: no value set for \"TestInteger2\"; did you mean to use an empty string (\"\")?\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestNotSupported = 123");
        unit_assert_operator_equal("Line 1: \"TestNotSupported\" is not a supported parameter.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("[TestSection]\r\nI'm not a comment or a parameter!");
        unit_assert_operator_equal("Line 2: line does not define a parameter in the \"Parameter = Value\" format.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger2 = abc");
        unit_assert_operator_equal("Parsing TestInteger2 with value \"abc\": expected an integer\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestFloat = abc");
        unit_assert_operator_equal("Parsing TestFloat with value \"abc\": expected a real number\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestMZTolerance = 123");
        unit_assert_operator_equal("Parsing TestMZTolerance with value \"123\": missing units for m/z tolerance (\"m/z\" or \"PPM\")\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestIntegerSet = abc");
        unit_assert_operator_equal("Parsing TestIntegerSet with value \"abc\": invalid format; expected an integer set in the form of a series of intervals (single integers, a-b ranges, or [a,b] ranges)\n",
                                    rtConfig.warnings().str());
    }

    // test reading a config from buffer and file with Unix newlines
    string testConfig = "TestString = The quick brown fox jumps over the lazy dog.\n"
                        "[TestBoolean is true in this section]\n"
                        "TestBoolean = 0 # this is a comment\n"
                        "#TestInteger is just a number (this is another comment)\n"
                        "TestInteger2 = 4242\n"
                        "TestFloat = 1234.567";
        
    {
        DerivedTestRunTimeConfig rtConfig;
        rtConfig.initializeFromBuffer(testConfig);
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger2);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
    }

    {
        {ofstream cfgFile("test.cfg"); cfgFile << testConfig;}
        DerivedTestRunTimeConfig rtConfig;
        rtConfig.initializeFromFile();
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger2);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
        bfs::remove("test.cfg");
    }

    // test with DOS newlines
    bal::replace_all(testConfig, "\n", "\r\n");

    {
        DerivedTestRunTimeConfig rtConfig;
        rtConfig.initializeFromBuffer(testConfig);
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger2);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
    }

    {
        {ofstream cfgFile("test.cfg"); cfgFile << testConfig;}
        DerivedTestRunTimeConfig rtConfig;
        rtConfig.initializeFromFile();
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger2);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
        bfs::remove("test.cfg");
    }
}

template <typename ConfigType>
void test()
{
    // test default value initialization
    {
        ConfigType rtConfig;
        ostringstream testIntegerSet;

        unit_assert_operator_equal("foo", rtConfig.TestString);
        unit_assert_operator_equal(true, rtConfig.TestBoolean);
        unit_assert_operator_equal(42, rtConfig.TestInteger);
        unit_assert_operator_equal(42, rtConfig.TestLong);
        unit_assert_operator_equal(42, rtConfig.TestInt64);
        unit_assert_operator_equal(42, rtConfig.TestUInt64);
        unit_assert_operator_equal(0.42f, rtConfig.TestFloat);
        unit_assert_operator_equal(0.42, rtConfig.TestDouble);

        testIntegerSet << rtConfig.TestIntegerSet;
        unit_assert_operator_equal("[-4,2]", testIntegerSet.str());

        unit_assert_operator_equal(42, rtConfig.TestMZTolerance.value);
        unit_assert_operator_equal(MZTolerance::PPM, rtConfig.TestMZTolerance.units);
    }


    // test whitespace, quote, and comment handling
    {
        ConfigType rtConfig;
        ostringstream testIntegerSet;

        rtConfig.initializeFromBuffer("TestString = \"bar\"");
        unit_assert_operator_equal("bar", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = baz");
        unit_assert_operator_equal("baz", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = foo bar");
        unit_assert_operator_equal("foo bar", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = foo\\\"bar");
        unit_assert_operator_equal("foo\"bar", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = \"foo\\\"bar\"");
        unit_assert_operator_equal("foo\"bar", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = \"\"");
        unit_assert_operator_equal("", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = \"foo#bar\"");
        unit_assert_operator_equal("foo#bar", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestString = foo#bar");
        unit_assert_operator_equal("foo", rtConfig.TestString);

        rtConfig.initializeFromBuffer("#TestString = bar");
        unit_assert_operator_equal("foo", rtConfig.TestString);

        rtConfig.initializeFromBuffer("TestBoolean = false");
        unit_assert_operator_equal(false, rtConfig.TestBoolean);

        rtConfig.initializeFromBuffer("TestBoolean = 1");
        unit_assert_operator_equal(true, rtConfig.TestBoolean);

        rtConfig.initializeFromBuffer("TestBoolean = 0");
        unit_assert_operator_equal(false, rtConfig.TestBoolean);

        rtConfig.initializeFromBuffer("TestBoolean = true");
        unit_assert_operator_equal(true, rtConfig.TestBoolean);
            
        rtConfig.initializeFromBuffer("TestInteger = 123");
        unit_assert_operator_equal(123, rtConfig.TestInteger);

        rtConfig.initializeFromBuffer("TestLong = 1234");
        unit_assert_operator_equal(1234, rtConfig.TestLong);

        rtConfig.initializeFromBuffer("TestInt64 = 9223372036854775807");
        unit_assert_operator_equal(9223372036854775807, rtConfig.TestInt64);

        rtConfig.initializeFromBuffer("TestUInt64 = 18446744073709551615");
        unit_assert_operator_equal(18446744073709551615UL, rtConfig.TestUInt64);

        rtConfig.initializeFromBuffer("TestFloat = 1.23456");
        unit_assert_operator_equal(1.23456f, rtConfig.TestFloat);

        rtConfig.initializeFromBuffer("TestDouble = 1.234567890123");
        unit_assert_operator_equal(1.234567890123, rtConfig.TestDouble);
            
        rtConfig.initializeFromBuffer("TestIntegerSet = [-3,1]");
        testIntegerSet.str(""); testIntegerSet << rtConfig.TestIntegerSet;
        unit_assert_operator_equal("[-3,1]", testIntegerSet.str());
            
        rtConfig.initializeFromBuffer("TestIntegerSet = -3-1");
        testIntegerSet.str(""); testIntegerSet << rtConfig.TestIntegerSet;
        unit_assert_operator_equal("[-3,1]", testIntegerSet.str());
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123ppm");
        unit_assert_operator_equal(123, rtConfig.TestMZTolerance.value);
        unit_assert_operator_equal(MZTolerance::PPM, rtConfig.TestMZTolerance.units);
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123 daltons");
        unit_assert_operator_equal(MZTolerance::MZ, rtConfig.TestMZTolerance.units);
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123 ppm");
        unit_assert_operator_equal(MZTolerance::PPM, rtConfig.TestMZTolerance.units);
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123 m/z");
        unit_assert_operator_equal(MZTolerance::MZ, rtConfig.TestMZTolerance.units);
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123PPM");
        unit_assert_operator_equal(MZTolerance::PPM, rtConfig.TestMZTolerance.units);
            
        rtConfig.initializeFromBuffer("TestMZTolerance = 123Daltons");
        unit_assert_operator_equal(MZTolerance::MZ, rtConfig.TestMZTolerance.units);
    }

    // test validation errors
    {
        ConfigType rtConfig;
        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestInteger = 31\r\nTestInteger = 42\r\nTestInteger = 42"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Line 2: \"TestInteger\" has already been defined.\n"
                                "Line 3: \"TestInteger\" has already been defined.\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestInteger ="),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Line 1: no value set for \"TestInteger\"; did you mean to use an empty string (\"\")?\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestInteger = "),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Line 1: no value set for \"TestInteger\"; did you mean to use an empty string (\"\")?\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestNotSupported = 123"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Line 1: \"TestNotSupported\" is not a supported parameter.\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("[TestSection]\r\nI'm not a comment or a parameter!"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Line 2: line does not define a parameter in the \"Parameter = Value\" format.\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestInteger = abc"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Parsing TestInteger with value \"abc\": expected an integer\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestFloat = abc"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Parsing TestFloat with value \"abc\": expected a real number\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestMZTolerance = 123"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Parsing TestMZTolerance with value \"123\": missing units for m/z tolerance (\"m/z\" or \"PPM\")\n");

        unit_assert_throws_what(rtConfig.initializeFromBuffer("TestIntegerSet = abc"),
                                runtime_error,
                                "Error! There are problems with the configuration file: \n"
                                "Parsing TestIntegerSet with value \"abc\": invalid format; expected an integer set in the form of a series of intervals (single integers, a-b ranges, or [a,b] ranges)\n");
    }

    // test validation warnings
    {
        ConfigType rtConfig(false);

        rtConfig.initializeFromBuffer("TestInteger = 31\r\nTestInteger = 42\r\nTestInteger = 42");
        unit_assert_operator_equal("Line 2: \"TestInteger\" has already been defined.\n"
                                    "Line 3: \"TestInteger\" has already been defined.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger =");
        unit_assert_operator_equal("Line 1: no value set for \"TestInteger\"; did you mean to use an empty string (\"\")?\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger = ");
        unit_assert_operator_equal("Line 1: no value set for \"TestInteger\"; did you mean to use an empty string (\"\")?\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestNotSupported = 123");
        unit_assert_operator_equal("Line 1: \"TestNotSupported\" is not a supported parameter.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("[TestSection]\r\nI'm not a comment or a parameter!");
        unit_assert_operator_equal("Line 2: line does not define a parameter in the \"Parameter = Value\" format.\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestInteger = abc");
        unit_assert_operator_equal("Parsing TestInteger with value \"abc\": expected an integer\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestFloat = abc");
        unit_assert_operator_equal("Parsing TestFloat with value \"abc\": expected a real number\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestMZTolerance = 123");
        unit_assert_operator_equal("Parsing TestMZTolerance with value \"123\": missing units for m/z tolerance (\"m/z\" or \"PPM\")\n",
                                    rtConfig.warnings().str());

        rtConfig.initializeFromBuffer("TestIntegerSet = abc");
        unit_assert_operator_equal("Parsing TestIntegerSet with value \"abc\": invalid format; expected an integer set in the form of a series of intervals (single integers, a-b ranges, or [a,b] ranges)\n",
                                    rtConfig.warnings().str());
    }

    // test reading a config from buffer and file with Unix newlines
    string testConfig = "TestString = The quick brown fox jumps over the lazy dog.\n"
                        "[TestBoolean is true in this section]\n"
                        "TestBoolean = 0 # this is a comment\n"
                        "#TestInteger is just a number (this is another comment)\n"
                        "TestInteger = 4242\n"
                        "TestFloat = 1234.567";
        
    {
        ConfigType rtConfig;
        rtConfig.initializeFromBuffer(testConfig);
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
    }

    {
        {ofstream cfgFile("test.cfg"); cfgFile << testConfig;}
        ConfigType rtConfig;
        rtConfig.initializeFromFile();
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
        bfs::remove("test.cfg");
    }

    // test with DOS newlines
    bal::replace_all(testConfig, "\n", "\r\n");

    {
        ConfigType rtConfig;
        rtConfig.initializeFromBuffer(testConfig);
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
    }

    {
        {ofstream cfgFile("test.cfg"); cfgFile << testConfig;}
        ConfigType rtConfig;
        rtConfig.initializeFromFile();
        unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", rtConfig.TestString);
        unit_assert_operator_equal(false, rtConfig.TestBoolean);
        unit_assert_operator_equal(4242, rtConfig.TestInteger);
        unit_assert_operator_equal(1234.567f, rtConfig.TestFloat);
        bfs::remove("test.cfg");
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test<TestRunTimeConfig>();

        try
        {
            test<DerivedTestRunTimeConfig>();
            testDerived();
        }
        catch (exception& e)
        {
            TEST_FAILED(string("[DerivedTestRunTimeConfig] ") + e.what())
        }
        catch (...)
        {
            TEST_FAILED(string("[DerivedTestRunTimeConfig] ") + "Caught unknown exception.")
        }
    }
    catch (exception& e)
    {
        TEST_FAILED(string("[TestRunTimeConfig] ") + e.what())
    }
    catch (...)
    {
        TEST_FAILED(string("[TestRunTimeConfig] ") + "Caught unknown exception.")
    }

    TEST_EPILOG
}
