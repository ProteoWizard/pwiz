/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GAPP_H__
#define __GAPP_H__

#include <stddef.h>
#include "GError.h"
#ifdef WINDOWS
#	include <windows.h>
#	ifndef ssize_t
		typedef SSIZE_T ssize_t;
#	endif
#else
#	include <termios.h>
#endif

#ifdef WINDOWS
#	define INVALID_HANDLE (HANDLE)-1
#else
#	ifndef HANDLE
#		define HANDLE int
#		define INVALID_HANDLE -1
#	endif
#endif

namespace GClasses {

typedef void (*DaemonMainFunc)(void* pArg);


/// This class wraps the handle of a pipe. It closes the pipe when it is destroyed.
/// This class is useful in conjunction with GApp::systemExecute for reading from,
/// or writing to, the standard i/o streams of a child process.
class GPipe
{
protected:
	HANDLE m_handle;

public:
	/// Construct an empty pipe holder
	GPipe();
	~GPipe();

	/// Set the handle of the pipe
	void set(HANDLE h);

	/// Returns the current handle
	HANDLE get() { return m_handle; }

	/// Read from the pipe until there is nothing else to read,
	/// or the buffer is full.
	ssize_t read(char* buf, size_t bufSize);

	/// Write to the pipe.
	void write(const char* buf, size_t bufSize);

	/// Reads from the pipe and writes to the specified file, until
	/// there is nothing left to read.
	void toFile(const char* szFilename);
};


/// Contains some generally useful functions for launching applications
class GApp
{
public:
	/// Launches a daemon process that calls pDaemonMain, and then exits.
	/// Throws an exception if any error occurs while launching the daemon.
	/// if stdoutFilename and/or stderrFilename is non-NULL, then the corresponding
	/// stream will be redirected to append to the specified file.
	/// (On Windows, this method just calls pDaemonMain normally, and does
	/// not fork off a separate process.)
	static void launchDaemon(DaemonMainFunc pDaemonMain, void* pArg, const char* stdoutFilename = NULL, const char* stderrFilename = NULL);

	/// Returns the full name (including path) of the executing application.
	/// Returns the length of the returned string, or -1 if it failed.
	/// If clipFilename is true, it will omit the filename and just return
	/// the folder in which the application resides.
	static int appPath(char* pBuf, size_t len, bool clipFilename);

	/// Executes the specified system command. Output is directed to
	/// the console. (If you want to hide the output or direct it to a
	/// file, use systemExec.) The child app runs inside a security
	/// sandbox (at least on Windows, I'm not totally sure about Linux),
	/// so you can't necessarily access everything the parent process
	/// could access. Throws if there is an
	/// error executing the command. Otherwise, returns the exit code
	/// of the child process. "show" does nothing on Linux.
	static int systemCall(const char* szCommand, bool wait, bool show);

	/// Executes the specified system command. (szCommand should contain
	/// the app name as well as args.)
	/// If wait is true, then the exit code of the command will be returned.
	/// If wait is false, then 0 will be returned immediately, and the
	/// command will continue to execute.
	/// If pStdOut is NULL, it will use the same stdout as the calling
	/// process. If pStdOut is non-NULL, then the value it points to will
	/// be set to the handle of the read end of a pipe, which will receive
	/// the stdout output of the called process. Likewise with pStdErr.
	/// If pStdIn is non-NULL, then it should point to the read end of
	/// a pipe, and it will be used for stdin with the called process.
	static int systemExecute(const char* szCommand, bool wait, GPipe* pStdOut = NULL, GPipe* pStdErr = NULL, GPipe* pStdIn = NULL);

	/// If you're having trouble with bogus floating point values
	/// (like NAN or INF), this method will cause an exception to
	/// be thrown when overflow, divide-by-zero, or a NAN condition
	/// occurs.
	static void enableFloatingPointExceptions();

	/// Opens the specified URL in the default web browser. Returns true
	/// if successful, and false if not.
	static bool openUrlInBrowser(const char* szUrl);

};

typedef void (*sighandler_t)(int);

/// Temporarily handles certain signals. (When this object is destroyed, it puts all the signal
/// handlers back the way they were.) Periodically call "check" to see if a signal has occurred.
class GSignalHandler
{
public:
#ifndef WINDOWS
	sighandler_t m_prevSigInt;
	sighandler_t m_prevSigTerm;
	sighandler_t m_prevSigPipe;
	sighandler_t m_prevSigSegV;
#endif
	int m_gotSignal;

	GSignalHandler();
	~GSignalHandler();

	/// Call this periodically. Returns 0 if no signal has occurred. Otherwise, returns the number of the signal.
	int check();

	/// You can call this to simulate a signal.
	void onSignal(int sig);
};


/// This class provides a non-blocking method for reading characters from
/// stdin. (If there are no characters ready in stdin, it immediately
/// returns '\0'.) The constructor sets flags on the console so that it
/// passes characters to the stream immediately (instead of when Enter is
/// pressed), and so that it doesn't echo the keys (if desired), and it makes stdin
/// non-blocking. The destructor puts all those things back the way they were.
class GPassiveConsole
{
#	ifdef WINDOWS
protected:
	DWORD m_oldMode;
	HANDLE m_hStdin;
	INPUT_RECORD m_inputRecord;
public:
	GPassiveConsole(bool echo);
	~GPassiveConsole();
	char getChar();
#	else
protected:
	struct termios m_old;
	int m_stdin;
	int m_oldStreamFlags;
public:
	/// If echo is true, then keys pressed will be echoed to the screen.
	GPassiveConsole(bool echo);
	~GPassiveConsole();

	/// Returns the char of the next key that was pressed. (This method
	/// does not block.) If no keys have been pressed, it returns the zero char.
	char getChar();
#	endif
};




/// Parses command-line args and provides methods
/// to conveniently process them.
class GArgReader
{
	int m_argc;
	char** m_argv;
	int m_argPos;

public:
	/// Pass the args that are passed in to main
	GArgReader(int argc, char* argv[]);

	/// Returns the current position--that is, the argument number.
	int get_pos();

	/// Sets the current position
	void set_pos(int n);

	/// Returns the current arg (without advancing)
	const char* peek();

	/// Returns the current arg as a string, and advances.
	/// Throws an exception if the end of the args was already
	/// reached before this call.
	const char* pop_string();

	/// Returns the current arg as a uint, and advances.
	/// Throws an exception if the end of the args was already
	/// reached before this call.
	unsigned int pop_uint();

	/// Returns the current arg as a double, and advances.
	/// Throws an exception if the end of the args was already
	/// reached before this call.
	double pop_double();

	/// If the current arg matches flagName, advances and returns true.
	/// Otherwise, returns false (without advancing).
	bool if_pop(const char* flagName);

	/// Returns the number of remaining args
	int size();

	/// Returns true if there is another arg, and it begins with '-'
	bool next_is_flag();

	/// Returns true if there is another arg, and it would parse
	/// accurately as an unsigned integer
	bool next_is_uint();
};

} // namespace GClasses

#endif // __GAPP_H__
