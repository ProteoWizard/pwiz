"""MCP Server for LabKey/Panorama data access.

This server exposes tools for querying Skyline data from skyline.ms and other
LabKey servers, including:
- Exception reports from user crash submissions
- Nightly test results and analysis
- Wiki documentation pages
- Support board threads
- Issue tracking (bugs, TODOs)
- File attachments

Authentication is handled via netrc file.

Setup:
1. Create _netrc file (Windows) or .netrc (Unix) in your home directory:
   machine skyline.ms
   login your-email@example.com
   password your-password

2. Install dependencies:
   pip install mcp labkey

3. Register with Claude Code:
   claude mcp add labkey -- python /path/to/server.py
"""

import logging

from mcp.server.fastmcp import FastMCP

# Configure logging to stderr (required for STDIO transport)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger("labkey_mcp")

# Initialize FastMCP server
mcp = FastMCP("labkey")

# Import and register tools from all modules
from tools import register_all_tools
register_all_tools(mcp)


def main():
    """Run the MCP server."""
    logger.info("Starting LabKey MCP server")
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
