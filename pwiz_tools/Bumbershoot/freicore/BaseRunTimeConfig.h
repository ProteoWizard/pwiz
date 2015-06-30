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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _BASERUNTIMECONFIG_H
#define _BASERUNTIMECONFIG_H

#include "stdafx.h"

#define RTCONFIG_VARIABLE_EX(varType, varName, varDefaultValue, varInit)    ((4, (varType, varName, varDefaultValue, varInit)))
#define RTCONFIG_VARIABLE(varType, varName, varDefaultValue)                RTCONFIG_VARIABLE_EX(varType, varName, varDefaultValue, 1)
#define RTCONFIG_VAR_NAME(var)                                                BOOST_PP_ARRAY_ELEM(1, var)
#define RTCONFIG_VAR_TYPE(var)                                                BOOST_PP_ARRAY_ELEM(0, var)
#define RTCONFIG_VAR_DEFAULTVALUE(var)                                        BOOST_PP_ARRAY_ELEM(2, var)
#define RTCONFIG_VAR_INIT(var)                                                BOOST_PP_ARRAY_ELEM(3, var)

#define RTCONFIG_VAR_NAME_CAT(var, str)                                        BOOST_PP_CAT( RTCONFIG_VAR_NAME(var), str )
#define RTCONFIG_VAR_NAME_STR(var)                                            BOOST_PP_STRINGIZE( RTCONFIG_VAR_NAME(var) )

#define RTCONFIG_DECLARE_VAR(r, n_a, var)                        RTCONFIG_VAR_TYPE(var) RTCONFIG_VAR_NAME(var);
#define RTCONFIG_INIT_DEFAULT_VAR_(r, n_a, var) \
        try \
        { \
            parse(RTCONFIG_VAR_NAME(var), RTCONFIG_VAR_DEFAULTVALUE(var)); \
        } catch(exception& e) \
        { \
            m_warnings << "[RunTimeConfig::ctor] error initializing " << RTCONFIG_VAR_NAME_STR(var) << " with value \"" << RTCONFIG_VAR_DEFAULTVALUE(var) << "\": " << e.what() << "\n"; \
        }

#define RTCONFIG_INIT_DEFAULT_VAR(r, n_a, var) \
    BOOST_PP_IF( RTCONFIG_VAR_INIT(var), RTCONFIG_INIT_DEFAULT_VAR_(r, n_a, var), 0; )

#define RTCONFIG_FILL_MAP(r, varMap, var) \
        string RTCONFIG_VAR_NAME_CAT( var, Val ); \
        try \
        { \
            if( !hideDefaultValues || !( lexical_cast<string>(RTCONFIG_VAR_NAME(var)) == lexical_cast<string>(RTCONFIG_VAR_DEFAULTVALUE(var)) ) ) \
            { \
                RTCONFIG_VAR_NAME_CAT( var, Val ) = lexical_cast<string>( RTCONFIG_VAR_NAME(var) ); \
                varMap[ RTCONFIG_VAR_NAME_STR(var) ] = RTCONFIG_VAR_NAME_CAT( var, Val ); \
            } \
        } catch(exception& e) \
        { \
            m_warnings << "Casting " << RTCONFIG_VAR_NAME_STR(var) << " with value \"" << RTCONFIG_VAR_NAME(var) << "\": " << e.what() << "\n"; \
        }

#define RTCONFIG_READ_MAP(r, varMap, var) \
        RunTimeVariableMap::const_iterator RTCONFIG_VAR_NAME_CAT( var, Itr ) = varMap.find( RTCONFIG_VAR_NAME_STR(var) ); \
        if( RTCONFIG_VAR_NAME_CAT( var, Itr ) != varMap.end() ) \
        { \
            string RTCONFIG_VAR_NAME_CAT( var, Str ) = UnquoteString( RTCONFIG_VAR_NAME_CAT( var, Itr )->second ); \
            try \
            { \
                parse( RTCONFIG_VAR_NAME(var), RTCONFIG_VAR_NAME_CAT( var, Str ) ); \
            } catch(bad_lexical_cast&) \
            { \
                m_warnings << "Parsing " << RTCONFIG_VAR_NAME_STR(var) << " with value \"" << RTCONFIG_VAR_NAME_CAT( var, Str ) << "\": expected " << cppTypeToNaturalLanguage<RTCONFIG_VAR_TYPE(var)>(BOOST_PP_STRINGIZE(RTCONFIG_VAR_TYPE(var))) << "\n"; \
            } catch(exception& e) \
            { \
                m_warnings << "Parsing " << RTCONFIG_VAR_NAME_STR(var) << " with value \"" << RTCONFIG_VAR_NAME_CAT( var, Str ) << "\": " << e.what() << "\n"; \
            } \
        }

#define RTCONFIG_PRINT_VAR(r, n_a, var) \
    stringstream RTCONFIG_VAR_NAME_CAT( var, Stream ); \
    RTCONFIG_VAR_NAME_CAT( var, Stream ) << right << RTCONFIG_VAR_NAME_STR(var) << ": "; \
    cout << RTCONFIG_VAR_NAME_CAT( var, Stream ).str() << RTCONFIG_VAR_NAME(var) << endl;

#define RTCONFIG_DEFINE_MEMBERS_EX(configName, baseConfigName, configVariables, configDefaultFilename) \
    BOOST_PP_SEQ_FOR_EACH( RTCONFIG_DECLARE_VAR, ~, configVariables ) \
    configName(bool treatWarningsAsErrors = true) : baseConfigName(treatWarningsAsErrors) \
    { \
        BOOST_PP_SEQ_FOR_EACH( RTCONFIG_INIT_DEFAULT_VAR, ~, configVariables ) \
        if (m_warnings.tellp() > 0) throw runtime_error(m_warnings.str()); /* initialization errors are bugs */ \
    } \
    RunTimeVariableMap getVariables( bool hideDefaultValues = false ) \
    { \
        baseConfigName::getVariables( hideDefaultValues ); \
        BOOST_PP_SEQ_FOR_EACH( RTCONFIG_FILL_MAP, m_variables, configVariables ) \
        return m_variables; \
    } \
    void setVariables( RunTimeVariableMap& vars ) \
    { \
        baseConfigName::setVariables( vars ); \
        BOOST_PP_SEQ_FOR_EACH( RTCONFIG_READ_MAP, vars, configVariables ) \
        finalize(); \
    } \
    int initializeFromFile( const string& rtConfigFilename = configDefaultFilename ) \
    { \
        return BaseRunTimeConfig::initializeFromFile( rtConfigFilename ); \
    }

#define RTCONFIG_DEFINE_MEMBERS(configName, configVariables, configDefaultFilename) \
    RTCONFIG_DEFINE_MEMBERS_EX(configName, BaseRunTimeConfig, configVariables, configDefaultFilename)

namespace freicore
{
    template <typename T>
    inline T& parse(T& lhs, const typename T::domain& rhs)
    {
        // boost::lexical_cast fails on BOOST_ENUM types
        stringstream ss;
        ss << T(rhs);
        ss >> lhs;
        return lhs;
    }

    template <typename T, typename S>
    inline T& parse(T& lhs, const S& rhs) {return lhs = lexical_cast<T,S>(rhs);}

    inline MZTolerance& parse(MZTolerance& lhs, const string& rhs)
    {
        if (!bal::icontains(rhs, "ppm") && !bal::icontains(rhs, "m/z") && !bal::icontains(rhs, "mz") && !bal::icontains(rhs, "da"))
            throw invalid_argument("missing units for m/z tolerance (\"m/z\" or \"PPM\")");

        stringstream ss(rhs);
        ss >> lhs;
        return lhs;
    }

    inline IntegerSet& parse(IntegerSet& lhs, const string& rhs)
    {
        if (rhs.find_first_not_of("0123456789[]-+, ") != string::npos)
            throw invalid_argument("invalid format; expected an integer set in the form of a series of intervals (single integers, a-b ranges, or [a,b] ranges)");

        lhs = IntegerSet();
        lhs.parse(rhs);
        return lhs;
    }

    template <typename T>
    inline string cppTypeToNaturalLanguage(const string& cppType)
    {
        if (numeric_limits<T>::is_specialized)
        {
            if (numeric_limits<T>::is_integer) return "an integer";
            else return "a real number";
        }
        else
            return "a " + cppType;
    }

    struct RunTimeVariableMap : public map< string, string >
    {
        RunTimeVariableMap(    const string& initialVarList = "" )
        {
            static const boost::char_separator<char> delim(" ");
            stokenizer parser( initialVarList.begin(), initialVarList.begin() + initialVarList.length(), delim );

            for( stokenizer::iterator itr = parser.begin(); itr != parser.end(); ++itr )
            {
                operator[]( *itr ) = "";
            }
        }
    };

    struct BaseRunTimeConfig
    {
    protected:
        RunTimeVariableMap m_variables;
        bool m_treatWarningsAsErrors;
        ostringstream m_warnings;

    public:
        string cfgStr;

                                    BaseRunTimeConfig(bool treatWarningsAsErrors = true);
        virtual                     ~BaseRunTimeConfig() {}

        virtual void                initializeFromBuffer( const string& cfgStr );
        virtual    RunTimeVariableMap  getVariables( bool hideDefaultValues = false );
        virtual void                setVariables( RunTimeVariableMap& vars );
        virtual void                dump();
        virtual void                finalize();

        bool                        initialized() { return !cfgStr.empty(); }
        int                         initializeFromFile( const string& rtConfigFilename );
    };
}

#endif
