# SkylineProcessRunner

Process launcher that bridges Skyline and external command-line tools via named pipes.
When Skyline runs an external tool, it spawns this process to execute the command and
stream output back through named pipe channels.

## Author

Trevor Killeen, MacCoss Lab

## Usage

Called internally by Skyline during external tool execution. Not intended to be run
directly by users.

```
SkylineProcessRunner <pipe_guid_suffix> <command> [arguments]
```

Creates named pipe connections for stdout and stderr, executes the specified command,
and streams output back to the calling Skyline process.
