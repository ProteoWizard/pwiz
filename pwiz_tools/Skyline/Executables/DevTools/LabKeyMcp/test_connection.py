"""Test script to verify LabKey connection to skyline.ms.

Run this after setting up your _netrc file to verify authentication works.

Usage:
    python test_connection.py
"""

from datetime import datetime, timedelta

from labkey.api_wrapper import APIWrapper
from labkey.query import QueryFilter


def main():
    server = "skyline.ms"
    container_path = "/home/issues/exceptions"

    print(f"Connecting to {server}{container_path}...")
    print()

    try:
        api = APIWrapper(server, container_path, "")

        # Step 1: Verify basic connection
        print("=== Testing Basic Connection ===")
        result = api.query.select_rows("core", "users", max_rows=1)
        print(f"  Connected! Found {result.get('rowCount', '?')} users in system.")
        print()

        # Step 2: Query exception data
        print("=== Querying Exception Data ===")
        since_date = (datetime.now() - timedelta(days=30)).strftime("%Y-%m-%d")
        date_filter = QueryFilter(
            "Created", since_date, QueryFilter.Types.DATE_GREATER_THAN_OR_EQUAL
        )

        result = api.query.select_rows(
            schema_name="announcement",
            query_name="Announcement",
            max_rows=10,
            sort="-Created",
            filter_array=[date_filter],
        )

        row_count = result.get("rowCount", 0)
        rows = result.get("rows", [])
        print(f"  Found {row_count} exceptions in last 30 days")
        print()

        # Step 3: Show sample exceptions
        print("=== Recent Exceptions ===")
        for row in rows[:5]:
            row_id = row.get("RowId", "?")
            title = row.get("Title", "Unknown")
            created = row.get("Created", "Unknown")
            print(f"  #{row_id} - {title}")
            print(f"    Created: {created}")
            print()

        print("Connection test successful!")
        print()
        print("Next steps:")
        print("  1. Install MCP: pip install mcp")
        print("  2. Test server: python server.py")
        print("  3. Register with Claude Code:")
        import os
        script_dir = os.path.dirname(os.path.abspath(__file__))
        server_path = os.path.join(script_dir, "server.py")
        print(f"     claude mcp add labkey -- python {server_path}")

    except Exception as e:
        print(f"ERROR: {e}")
        print()
        print("Troubleshooting:")
        print("  1. Check that _netrc exists at C:\\Users\\<YourName>\\_netrc")
        print("  2. Verify the file contains:")
        print("     machine skyline.ms")
        print("     login claude.c.skyline@gmail.com")
        print("     password <your-password>")
        print("  3. Ensure the account has been activated on skyline.ms")


if __name__ == "__main__":
    main()
