/*
 * Original author: Greg Finney <gfinney .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/***
	CAutoNativePtr - A smart pointer for using native objects in managed code.

	Author	:	Nishant Sivakumar
	Email	:	voidnish@gmail.com	
	Blog	:	http://blog.voidnish.com
	Web		:	http://www.voidnish.com 	

	You may freely use this class as long as you include
	this copyright. 
	
	You may freely modify and use this class as long
	as you include this copyright in your modified version. 

	This code is provided "as is" without express or implied warranty. 
	
	Copyright © Nishant Sivakumar, 2006.
	All Rights Reserved.
***/

#pragma once

template<typename T> ref class CAutoNativePtr
{
private:
	T* _ptr;

public:
	CAutoNativePtr() : _ptr(nullptr)
	{
	}

	CAutoNativePtr(T* t) : _ptr(t)
	{
	}

	CAutoNativePtr(CAutoNativePtr<T>% an) : _ptr(an.Detach())
	{
	}

	template<typename TDERIVED> 
		CAutoNativePtr(CAutoNativePtr<TDERIVED>% an) : _ptr(an.Detach())
	{
	}

	!CAutoNativePtr()
	{	
		delete _ptr;
	}

	~CAutoNativePtr()
	{
		this->!CAutoNativePtr();
	}

	CAutoNativePtr<T>% operator=(T* t)
	{
		Attach(t);
		return *this;
	}

	CAutoNativePtr<T>% operator=(CAutoNativePtr<T>% an)
	{
		if(this != %an)
			Attach(an.Detach());
		return *this;
	}

	template<typename TDERIVED> 
		CAutoNativePtr<T>% operator=(CAutoNativePtr<TDERIVED>% an)
	{
		Attach(an.Detach());
		return *this;
	}

	static T* operator->(CAutoNativePtr<T>% an)
	{
		return an._ptr;
	}

	static operator T*(CAutoNativePtr<T>% an)
	{
		return an._ptr;
	}

	T* Detach()
	{
		T* t = _ptr;
		_ptr = nullptr;
		return t;
	}

	void Attach(T* t)
	{
		if(t)
		{	
			if(_ptr != t)
			{
				delete _ptr;
				_ptr = t;
			}
		}
		else
		{
#ifdef _DEBUG
			throw gcnew Exception(
				"Attempting to Attach(...) a nullptr!");
#endif
		}		
	}

	void Destroy()
	{
		delete _ptr;
		_ptr = nullptr;
	}
};
