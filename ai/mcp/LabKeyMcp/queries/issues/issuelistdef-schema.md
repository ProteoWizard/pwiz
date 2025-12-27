# issues.IssueListDef Table Schema

**Container:** `/home/issues`
**Schema:** `issues`
**Table:** `IssueListDef`

Issue list definitions - metadata about the issue tracker configuration.

## Columns

| Column | Type | Lookup | Description |
|--------|------|--------|-------------|
| RowId | Integer | | Primary key (AI, PK, Req, RO) |
| Name | Text | | Internal name (Req) |
| Label | Text | | Display label (Req) |
| Container | Text | core.Containers.EntityId | Container reference (Req, RO) |
| Kind | Text | | Definition type (Req) |
| DomainContainer | Integer | | Domain container ID (AI, Req, RO) |
| Created | DateTime | | Creation timestamp |
| Modified | DateTime | | Last modification (RO) |

## Usage Notes

- Typically only 1 row per container
- Defines the issue list configuration
- Kind value: "IssueDefinition"
