<!-- Generated from the maintainers' template; local edits are overwritten. -->

# Copilot instructions

A small, single-purpose .NET library published as a NuGet package. `src/` holds the
library, `test/` its test project. Pull requests target the default branch.

## Build and test

- The SDK version is pinned in `global.json`; do not bypass it.
- `dotnet restore`, then
  `dotnet test --no-restore --configuration Release --filter Category!=Integration`.
- Tests marked `Category=Integration` need Docker and are excluded from the default run.

## Conventions

- The library is multi-targeted; the list of target frameworks lives in
  `Directory.Build.props`. Code must compile on every target in that list - guard
  newer BCL APIs behind a conditional instead of dropping a target.
- Central Package Management. Versions live in `Directory.Packages.props` and
  `test/Directory.Packages.props`. Never put a `Version` attribute on a
  `PackageReference`; add a `PackageVersion` entry and keep the existing
  `[x.y.z,)` range notation.
- Nullable is enabled and implicit usings are disabled - write explicit `using`
  directives.
- Outside Debug, warnings are errors. `CA2007` is on: every `await` in library code
  needs `ConfigureAwait(false)`.
- The public API carries XML documentation.
- The assembly is strong-named and public-signed; leave the signing properties alone.
- `assets/RELEASE-NOTES.txt` is maintained by the release tooling - do not edit it
  by hand.

## Tests

- xunit v3 with AwesomeAssertions; Bogus for test data, Moq for fakes, Testcontainers
  where a real server is required.
- Prefer exercising behaviour through the public API.

## Commits and pull requests

- English only - code, comments, commit messages, pull request descriptions.
- Conventional commit prefixes: `feat`, `fix`, `perf`, `docs`, `test`,
  `build` (dependencies, packaging), `ci` (workflows), `chore`.
- Imperative subject, roughly 72 characters or less.
- No AI attribution and no tool names in commits or pull request descriptions.
