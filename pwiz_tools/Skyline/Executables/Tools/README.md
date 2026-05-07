# Tools

Container directory for external Skyline integration tools, analysis extensions, and
example code. These tools integrate with Skyline through its External Tools interface
or provide standalone analysis capabilities.

## Categories

### Analysis and Reporting
- **MSstats** / **MSstatsShiny** - statistical analysis of quantitative proteomics data
- **QuaSAR** - quantitative and systems analytical resources
- **TFExport** - transition/fragment export utilities
- **Turnover** - protein turnover analysis
- **XLTCalc** - cross-link calculation tools

### Instrument and Vendor Integration
- **MPPExport** - Agilent Mass Profiler Professional export
- **SProCoP** - statistical process control for proteomics

### Development Examples
- **ExampleArgCollector** / **ExampleInteractiveTool** - templates for building Skyline tools
- **TestArgCollector** / **TestInteractiveTool** / **TestCommandLineInteractiveTool** - test harnesses
- **ToolServiceTestHarness** - testing framework for tool service integration

### AI Integration
- **SkylineMcp** - MCP (Model Context Protocol) server for AI-assisted Skyline operations

### Utilities
- **AdvancedEditingCommands** - extended editing operations for Skyline
- **Skyline Gadget** - Windows desktop gadget for Skyline status

## Adding New Tools

New external tools should follow the patterns in the `Example*` directories. Tools
communicate with Skyline through command-line arguments, stdin/stdout, or the Skyline
Tool Service API.
