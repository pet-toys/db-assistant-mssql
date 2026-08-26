# Contributing

Thanks for taking the time to contribute! This project is a small, focused
SQL Server bulk-copy helper, so contributions of any size are welcome.

## Ways to contribute

- [Report a bug](https://github.com/pet-toys/db-assistant-mssql/issues/new?template=bug_report.yml).
- [Request a feature](https://github.com/pet-toys/db-assistant-mssql/issues/new?template=feature_request.yml).
- Improve the documentation.
- Open a pull request against the `dev` branch.

For anything beyond a small fix, please open an issue first so the approach can
be discussed before you invest time in a pull request.

## Getting started

The repository uses the .NET SDK version pinned in [`global.json`](../global.json)
and multi-targets `net8.0`, `net9.0`, and `net10.0`.

```bash
git clone https://github.com/pet-toys/db-assistant-mssql.git
cd db-assistant-mssql

dotnet restore
dotnet build -c Release
dotnet test
```

`Release` builds treat warnings as errors and enforce code-style and analyzer
rules, so build with `-c Release` before opening a pull request to catch the
same issues CI will.

The integration tests spin up a real SQL Server instance with
[Testcontainers](https://testcontainers.com/), so a running Docker engine is
required to execute them; they are skipped automatically when Docker is not
available. The remaining unit tests need no external dependencies.

## Pull requests

- Branch off `dev` and target `dev`.
- Keep each pull request focused on a single change.
- Link the related issue (for example, `Closes #123`).
- Add or update tests for any behavioral change.
- Make sure `dotnet build -c Release` and `dotnet test` both pass locally.

Commit messages and pull request descriptions should be written in English and
describe the change in plain, neutral terms.

## Code style

Most conventions are enforced automatically by the analyzers and
`.editorconfig`, so a clean `Release` build is the source of truth. The
guidelines below capture the conventions that are not fully machine-checked:

- Use `PascalCase` for type, method, property, and constant names.
- Use `camelCase` for parameters and local variables.
- Prefix private fields with an underscore (`_field`).
- Prefix interfaces with `I`.
- Use language keywords (`int`, `string`) rather than framework type names
  (`Int32`, `String`).
- Use boolean-style prefixes (`Is`, `Has`, `Can`, `Any`) for boolean members.
- Use braces around any statement that spans more than one line; a statement
  written on the same line as its `if`, `for`, or `foreach` needs none.
- Do not use Hungarian notation.

Nullable reference types are enabled project-wide, so do not add `#nullable`
directives to individual files.

### Tests

Tests use xUnit and follow the `Method_State_ExpectedResult` naming pattern
(for example, `WriteDataAsync_NullEntities_ThrowsForEntities`). Keep test data
close to the tests that use it, and prefer deterministic tests over ones that
depend on timing.
