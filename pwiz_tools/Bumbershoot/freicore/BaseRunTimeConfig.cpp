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

#include "stdafx.h"
#include "BaseRunTimeConfig.h"

namespace freicore
{
    BaseRunTimeConfig::BaseRunTimeConfig(bool treatWarningsAsErrors) : m_treatWarningsAsErrors(treatWarningsAsErrors)
    {
    }

    BaseRunTimeConfig::BaseRunTimeConfig(const BaseRunTimeConfig& rhs)
    {
        m_treatWarningsAsErrors = rhs.m_treatWarningsAsErrors;
        m_warnings.str(rhs.m_warnings.str());
        m_variables = rhs.getVariables();
    }

    BaseRunTimeConfig& BaseRunTimeConfig::operator=(const BaseRunTimeConfig& rhs)
    {
        m_treatWarningsAsErrors = rhs.m_treatWarningsAsErrors;
        m_warnings.str(rhs.m_warnings.str());
        m_variables = rhs.getVariables();
        return *this;
    }

    void BaseRunTimeConfig::initializeFromBuffer( const string& cfgStr )
    {
        if (&cfgStr != &this->cfgStr)
            this->cfgStr = cfgStr;

        m_warnings.str("");
        RunTimeVariableMap newVars;
        int lineNum = 0;

        try
        {
            getVariables();

            istringstream cfgStream(cfgStr);

            string line;
            while (getlinePortable(cfgStream, line))
            {
                ++lineNum;
                bal::trim(line); // trim whitespace

                // skip blank or comment lines
                if (line.empty() || line.find_first_of("#[") == 0)
                    continue;

                // otherwise, the line must be in the form "Key=Value" and Key must be in the variables map
                if (!bal::contains(line, "="))
                {
                    m_warnings << "Line " << lineNum << ": line does not define a parameter in the \"Parameter = Value\" format.\n";
                    continue;
                }

                size_t predIdx = line.find_first_of('=') + 1;

                string key = line.substr(0, predIdx-1);
                bal::trim(key);

                if (m_variables.count(key) == 0)
                {
                    m_warnings << "Line " << lineNum << ": \"" << key << "\" is not a supported parameter.\n";
                    continue;
                }

                RunTimeVariableMap::iterator itr = newVars.find(key);
                if (itr != newVars.end())
                {
                    m_warnings << "Line " << lineNum << ": \"" << key << "\" has already been defined.\n";
                    continue;
                }

                size_t valBegin = line.find_first_not_of("\t ", predIdx);
                size_t valEnd = valBegin;
                bool inQuote = false;
                for (valEnd = valBegin; valEnd < line.size(); ++valEnd)
                {
                    if (line[valEnd] == '"' && line[valEnd-1] != '\\')
                        inQuote = !inQuote;
                    else if ((line[valEnd] == '#') && !inQuote)
                        break; // stop at unquoted comment token
                }
                
                if (valEnd == valBegin || valBegin == string::npos)
                {
                    m_warnings << "Line " << lineNum << ": no value set for \"" << key << "\"; did you mean to use an empty string (\"\")?\n";
                    continue;
                }

                string& value = newVars[key];
                value = TrimWhitespace(line.substr(valBegin, valEnd-valBegin));
                if (value.empty())
                {
                    m_warnings << "Line " << lineNum << ": no value set for \"" << key << "\"; did you mean to use an empty string (\"\")?\n";
                    continue;
                }

                value = UnquoteString(value);
                bal::replace_all(value, "\\\"", "\"");
                bal::replace_all(value, "true", "1");
                bal::replace_all(value, "false", "0");
            }
        }
        catch (exception& e)
        {
            m_warnings << "Line " << lineNum << ": " << e.what() << "\n";
        }

        // apply the new variable values
        setVariables(newVars);
    }

    RunTimeVariableMap BaseRunTimeConfig::getVariables( bool hideDefaultValues ) const
    {
        return m_variables;
    }

    void BaseRunTimeConfig::setVariables( RunTimeVariableMap& vars )
    {
        for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
        {
            string value = UnquoteString( itr->second );
            if( value == "true" )
                itr->second = "\"1\"";
            else if( value == "false" )
                itr->second = "\"0\"";
            //cout << itr->first << " " << itr->second << "\n";
        }
    }

    void BaseRunTimeConfig::dump()
    {
        getVariables();
        string::size_type longestName = 0;
        for( RunTimeVariableMap::iterator itr = m_variables.begin(); itr != m_variables.end(); ++itr )
            if( itr->first.length() > longestName )
                longestName = itr->first.length();

        for( RunTimeVariableMap::iterator itr = m_variables.begin(); itr != m_variables.end(); ++itr )
        {
            cout.width( (streamsize) longestName + 2 );
            stringstream s;
            s << right << itr->first << ": ";
            cout << s.str() << boolalpha << "\"" << itr->second << "\"" << endl;
        }
    }

    void BaseRunTimeConfig::finalize()
    {
        if (m_warnings.tellp() > 0)
        {
            if (m_treatWarningsAsErrors)
                throw runtime_error(string("Error! There are problems with the configuration file: \n") + m_warnings.str());
            else
                cerr << "Warning! There are problems with the configuration file: \n" << m_warnings.str() << endl;
        }
    }

    int BaseRunTimeConfig::initializeFromFile( const string& rtConfigFilename )
    {
        // Abort
        if( rtConfigFilename.empty() )
        {
            finalize();
            return 1;
        }

        // Read settings from file; abort if file does not exist
        else
        {
            ifstream rtConfigFile( rtConfigFilename.c_str(), ios::binary );
            if( rtConfigFile.is_open() )
            {
                //cout << GetHostname() << " is reading its configuration file \"" << rtConfigFilename << "\"" << endl;
                int cfgSize = (int) GetFileSize( rtConfigFilename );
                cfgStr.resize( cfgSize );
                rtConfigFile.read( &cfgStr[0], cfgSize );
                initializeFromBuffer( cfgStr );
                rtConfigFile.close();
            } else
            {
                finalize();
                return 1;
            }
        }

        return 0;
    }
}
