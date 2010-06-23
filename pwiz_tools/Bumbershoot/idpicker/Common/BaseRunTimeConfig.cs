//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.ComponentModel;

namespace IDPicker
{
	public class RunTimeVariableMap : Dictionary< string, string >
	{
		public RunTimeVariableMap() {}
		public RunTimeVariableMap( string initialVarList )
		{
			string[] vars = initialVarList.Split(" ".ToCharArray());

			foreach( string varName in vars )
				Add( varName, "" );
		}
	}

	public interface IRunTimeConfig
	{
		bool initialized();
		void initializeFromBuffer( string cfgStr, string delim );
		RunTimeVariableMap getVariables();
		void setVariables( RunTimeVariableMap vars );
		List<string> initializeFromCLI( List<string> argsList );
		void dump();
		void finalize();
	}

	public class BaseRunTimeConfig
	{
		protected string cfgStr = String.Empty;
		public bool initialized() { return cfgStr.Length > 0; }

		public void initializeFromBuffer( string cfgStr, string delim )
		{
			this.cfgStr = cfgStr;

			//BaseRunTimeConfig::initializeFromBuffer( cfgStr, delim );
			FieldInfo[] fields;
			Type cfgType = this.GetType();
			fields = cfgType.GetFields( BindingFlags.Public | BindingFlags.Instance );
			foreach( FieldInfo field in fields )
			{
				Regex e = new Regex( "^\\s*(" + field.Name + ")\\s*=\\s*?(.*?)?\\s*?(#.*?)?$", RegexOptions.Multiline );
				Match m = e.Match( cfgStr );
				if( m.Success )
				{
					/*if( field.FieldType == typeof( string ) )
						Console.WriteLine( field.Name + "<-\"" + m.Groups[2].ToString().Trim() + "\"" );
					else
						Console.WriteLine( field.Name + "<-" + m.Groups[2].ToString().Trim() );*/

					object someValue = m.Groups[2].ToString().Trim();
					TypeConverter converter = TypeDescriptor.GetConverter( field.FieldType );
					someValue = converter.ConvertFrom( someValue );
					field.SetValue( this, someValue );

				}
			}
			finalize();
		}

		public RunTimeVariableMap getVariables()
		{
			RunTimeVariableMap vars = new RunTimeVariableMap();
			FieldInfo[] fields;
			Type cfgType = this.GetType();
			fields = cfgType.GetFields( BindingFlags.Public | BindingFlags.Instance );
			foreach( FieldInfo field in fields )
			{
				vars[field.Name] = field.GetValue( this ).ToString();
			}
			//BaseRunTimeConfig::getVariables( hideDefaultValues );
			return vars;
		}

		public void setVariables( RunTimeVariableMap vars )
		{
			//BaseRunTimeConfig::setVariables( vars );
			Type cfgType = this.GetType();
			foreach( string varName in vars.Keys )
			{
				FieldInfo field = cfgType.GetField( varName );
				field.SetValue( this, Convert.ChangeType( vars[varName], field.FieldType ) );
			}
			finalize();
		}

		public List<string> initializeFromCLI( List<string> argsList )
		{
			List<string> args = new List<string>( argsList );
			RunTimeVariableMap vars = getVariables();
			List<string> varList = new List<string>( vars.Keys );
			foreach( string key in varList )
			{
				string varName = key;
				string argName = "-" + varName;

				for( int i = 0; i < args.Count; ++i )
				{
					if( args[i][0] == '-' && args[i] == argName && i + 1 <= args.Count )
					{
						//Console.WriteLine( varName + " " + vars[varName] + " " + args[i + 1] );
						vars[varName] = args[i + 1];
						args.RemoveAt( i );
						args.RemoveAt( i );
						--i;
					}
				}
			}
			setVariables( vars );
			return args;
		}

		public bool initializeFromFile( string rtConfigFilename, string delim )
		{
			// Abort
			if( rtConfigFilename.Length == 0 )
			{
				finalize();
				return true;
			}

			// Read settings from file; abort if file does not exist
			else
			{
				StreamReader rtConfigFile;
				if( File.Exists(rtConfigFilename) )
				{
					rtConfigFile = new StreamReader( rtConfigFilename );
					//Console.WriteLine( "Reading configuration file \"" + rtConfigFilename + "\"" );
					initializeFromBuffer( rtConfigFile.ReadToEnd(), delim );
					rtConfigFile.Close();
					finalize();
					return false;
				} else
				{
					finalize();
					return true;
				}
			}
		}

		public void dump()
		{
			FieldInfo[] fields;
			Type cfgType = this.GetType();
			fields = cfgType.GetFields( BindingFlags.Public | BindingFlags.Instance );
			foreach( FieldInfo field in fields )
				if( field.FieldType == typeof( string ) )
					Console.WriteLine( field.Name + "=\"" + field.GetValue( this ) + "\"" );
				else
					Console.WriteLine( field.Name + "=" + field.GetValue( this ) );
		}

		protected virtual void finalize() { }
	}
}
