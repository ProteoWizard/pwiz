# ImageComparer

GUI tool for visual regression testing of Skyline tutorial screenshots. Compares current
screenshots frame-by-frame against reference images, highlighting pixel-level differences.

## Author

Brendan MacLean, MacCoss Lab

## Usage

Standalone Windows Forms application. Load old and new screenshot directories, adjust
comparison parameters (highlight color, tolerance), and review differences visually.

## Related Projects

- **ImageComparer.Core** - shared library with the comparison algorithm, used by both
  this GUI tool and the MCP server
- **ImageComparer.Mcp** - MCP (Model Context Protocol) server that exposes screenshot
  comparison to AI tools like Claude Code
