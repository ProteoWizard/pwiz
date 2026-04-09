# SkylineRunner

Command-line interface for running Skyline operations non-interactively. Locates the
installed Skyline-Daily application, launches it in CMD mode, and pipes commands to it
via named pipes.

## Author

John Chilton, MacCoss Lab

## Usage

```
SkylineRunner [skyline_command_arguments]
```

All arguments are passed through to Skyline's command-line interface. See Skyline's
`CommandArgs` documentation for available commands.

SkylineRunner finds the Skyline-Daily installation via registry lookup, establishes
named pipe connections for input/output, and streams results to the console. Check
for "ERROR:" in output to detect failures.

## Note

This tool predates `SkylineCmd.exe`, which is the modern built-in command-line runner.
SkylineRunner is still distributed and used by scripts that depend on it, but new
integrations should prefer SkylineCmd.
