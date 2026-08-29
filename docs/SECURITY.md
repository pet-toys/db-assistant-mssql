# Security Policy

## Supported versions

The package major version tracks the latest supported .NET major version. Only
the latest major line receives security fixes.

| Version | Supported          |
| ------- | :----------------: |
| 10.x    | :white_check_mark: |
| 8.x     | :x:                |
| < 8.0   | :x:                |

## Reporting a vulnerability

Please do not report security vulnerabilities through public issues, pull
requests, or discussions.

Instead, use GitHub's private vulnerability reporting: open the repository's
**Security** tab and click **Report a vulnerability**. This keeps the report
confidential until a fix is available.

When reporting, please include as much of the following as you can:

- A description of the vulnerability and its impact.
- The affected package version(s) and target framework.
- Steps to reproduce, ideally with a minimal code sample.
- Any known workarounds or mitigations.

## What to expect

- We aim to acknowledge a report within a few days.
- We will keep you informed as we investigate and work on a fix.
- Once a fix ships, we will publish a security advisory and credit the
  reporter, unless you prefer to remain anonymous.

## Scope

This library maps entity properties to columns and streams the values into SQL
Server through `SqlBulkCopy`. It does not create connections, manage credentials,
or build ad-hoc SQL text on your behalf: the caller supplies an already
configured `SqlConnection` and the destination table name. It does open that
connection if it is closed, and closes it again afterwards, but it neither
constructs it nor sees what it was constructed from. Reports about how the
mapping or the generated bulk-copy column bindings could mishandle data, or how a
caller-supplied table or column name could be used unsafely, are in scope. Issues
caused solely by how a consuming application builds its connection strings,
protects its credentials, or sources the table and column names it passes in are
not.
