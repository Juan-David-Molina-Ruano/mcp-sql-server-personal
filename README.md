# SQL Server Read-Only MCP

Local-first MCP server for Microsoft SQL Server.

It exposes read-only database tools over stdio for MCP clients such as OpenCode and Codex, with query guardrails and sensitive-field obfuscation.

## Quick path

1. Build the project with .NET 8.
2. Run the tests: `dotnet test`
3. Create a SQL Server login/user with read-only access.
4. Configure your MCP client to launch this server over stdio.
5. Verify `list_tables`, `describe_table`, and `execute_read_query`.

## Features

- `list_tables`
- `describe_table`
- `execute_read_query`
- read-only query validation
- row and response-size limits
- sensitive-field obfuscation for common secret/password-like columns
- support for SQL Server default instances, named instances, and explicit ports

## Requirements

- .NET 8 SDK (for building and running from source)
- SQL Server reachable from the machine running the MCP client
- A SQL login/user with read-only permissions

## Build and test

```powershell
dotnet build
dotnet test
```

## Publish for distribution

To produce a single-file executable:

```powershell
dotnet publish -c Release --self-contained false -r win-x64
```

The output will be in `bin\Release\net8.0\win-x64\publish\`. You can copy the executable and run it directly without the .NET SDK installed (the .NET 8 runtime is still required unless you publish self-contained).

For a fully self-contained build:

```powershell
dotnet publish -c Release --self-contained true -r win-x64
```

## Release automation

Releases are automated with GitHub Actions and triggered by pushing a SemVer tag.

1. Update the `<Version>` in `mcp-sql-server-personal.csproj` if needed.
2. Commit and push the version bump.
3. Create and push a tag:

   ```powershell
   git tag v1.1.0
   git push origin v1.1.0
   ```

4. The `Release` workflow will:
   - Build and test the project on Windows.
   - Publish a self-contained `win-x64` executable.
   - Create a ZIP archive and a SHA-256 checksum file.
   - Generate a GitHub artifact attestation for the release assets.
   - Create a GitHub release with the archive and checksum attached.

You can verify the artifact attestation locally using the GitHub CLI:

```powershell
gh attestation verify mcp-sql-server-personal-win-x64.zip -R <owner>/<repo>
```

> Only SemVer-style tags matching `v[0-9]+.[0-9]+.[0-9]+*` trigger a release (for example: `v1.1.0`, `v1.1.1`, `v2.0.0`).

## Environment variables

### Required

- `SQL_SERVER_HOST`
- `SQL_SERVER_DATABASE`
- `SQL_SERVER_USER`
- `SQL_SERVER_PASSWORD`

### Optional

- `SQL_SERVER_PORT`
- `SQL_SERVER_ENCRYPT` (default: `true`)
- `SQL_SERVER_TRUST_CERTIFICATE` (default: `false`)
- `SQL_MCP_MAX_ROWS` (default: `100`)
- `SQL_MCP_COMMAND_TIMEOUT_SECONDS` (default: `15`)
- `SQL_MCP_MAX_RESPONSE_BYTES` (default: `1048576`)

### Connection notes

| Scenario | `SQL_SERVER_HOST` | `SQL_SERVER_PORT` |
|---|---|---|
| Default instance | `localhost` | optional |
| Named instance | `YOUR-SERVER\\YOUR-INSTANCE` | omit |
| Explicit port | `db-host` | set it |

## Run locally

```powershell
$env:SQL_SERVER_HOST="localhost"
$env:SQL_SERVER_DATABASE="YourDatabase"
$env:SQL_SERVER_USER="your_sql_login"
$env:SQL_SERVER_PASSWORD="your-password"
$env:SQL_SERVER_ENCRYPT="true"
$env:SQL_SERVER_TRUST_CERTIFICATE="true"

dotnet run --project ".\mcp-sql-server-personal.csproj"
```

## OpenCode configuration

Add this to `~/.config/opencode/opencode.json`:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "sql-server-personal": {
      "type": "local",
      "enabled": true,
      "command": [
        "dotnet",
        "run",
        "--project",
        "C:\\path\\to\\mcp-sql-server-personal.csproj"
      ],
      "cwd": "C:\\path\\to\\repo",
      "timeout": 20000,
      "environment": {
        "SQL_SERVER_HOST": "YOUR-SERVER\\\\YOUR-INSTANCE",
        "SQL_SERVER_DATABASE": "YOUR_DATABASE",
        "SQL_SERVER_USER": "YOUR_SQL_LOGIN",
        "SQL_SERVER_PASSWORD": "{env:SQL_SERVER_PASSWORD}",
        "SQL_SERVER_ENCRYPT": "true",
        "SQL_SERVER_TRUST_CERTIFICATE": "true",
        "SQL_MCP_MAX_ROWS": "100",
        "SQL_MCP_COMMAND_TIMEOUT_SECONDS": "15",
        "SQL_MCP_MAX_RESPONSE_BYTES": "1048576"
      }
    }
  }
}
```

Restart OpenCode after changing config.

## Codex setup

Codex surfaces vary, but the MCP launch details are the same:

- command: `dotnet run --project <path-to-csproj>`
- working directory: repo root
- transport: stdio
- environment: the same variables listed above

Use these values when configuring the MCP server in Codex:

```text
command: dotnet
args: run --project C:\path\to\mcp-sql-server-personal.csproj
cwd: C:\path\to\repo

SQL_SERVER_HOST=YOUR-SERVER\YOUR-INSTANCE
SQL_SERVER_DATABASE=YOUR_DATABASE
SQL_SERVER_USER=YOUR_SQL_LOGIN
SQL_SERVER_PASSWORD=<your password>
SQL_SERVER_ENCRYPT=true
SQL_SERVER_TRUST_CERTIFICATE=true
SQL_MCP_MAX_ROWS=100
SQL_MCP_COMMAND_TIMEOUT_SECONDS=15
SQL_MCP_MAX_RESPONSE_BYTES=1048576
```

## What to verify

### 1. Table listing

Expected to work:

- `list_tables` for a schema like `dbo`

### 2. Table description

Expected to work:

- `describe_table` for a table like `dbo.Users`

### 3. Query execution

Expected to work:

```sql
SELECT TOP 3 Id, FirstName, LastName
FROM dbo.Users
ORDER BY Id
```

Expected to be rejected:

```sql
DELETE FROM dbo.Users
```

## Security behavior

### Guardrails

- Only read-only queries are allowed.
- Multiple statements are rejected.
- Dangerous operations like `DELETE`, `DROP`, `ALTER`, and `EXEC` are rejected.

### Sensitive data handling

The server obfuscates common sensitive fields in result sets, for example password or token-like columns.

Current behavior is heuristic-based:

- strong sensitive patterns can still be obfuscated even when lineage is incomplete
- broader/noisier patterns require stronger metadata to avoid false positives

This is a practical balance for local/personal use, not a substitute for database-native data governance.

## Recommended for sharing with other people

- keep credentials out of config files when possible
- prefer `{env:SQL_SERVER_PASSWORD}` in OpenCode
- document which SQL login permissions are expected
- test against both a default instance and a named instance
- verify sensitive-field obfuscation on your target schema before use

## Troubleshooting

### Login failures

- confirm SQL Server Authentication is enabled
- confirm the login password is correct and not expired
- for automation, disable password expiration for the MCP login if appropriate

### Named instances

- use `SQL_SERVER_HOST=SERVER\\INSTANCE`
- omit `SQL_SERVER_PORT` unless you explicitly want host+port mode

### OpenCode MCP startup

- use `environment`, not `env`, in OpenCode config
- increase MCP timeout if `dotnet run` starts slowly
- restart OpenCode after config changes

## Status

This is a usable local read-only SQL Server MCP server with automated tests and CI.

- Query validation and sensitive-field sanitization are covered by unit tests.
- A lightweight GitHub Actions workflow runs build and test on push/PR.
- `dotnet publish` produces a single-file executable for distribution.
