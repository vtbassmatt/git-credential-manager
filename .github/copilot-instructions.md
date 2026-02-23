# Copilot Instructions for Git Credential Manager

## Build, Test, and Lint

This is a .NET solution (SDK 8.0+). Use platform-specific build configurations:

```shell
# macOS
dotnet build -c MacDebug
dotnet build -c MacRelease

# Windows (PowerShell)
dotnet build -c WindowsDebug

# Linux
dotnet build -c LinuxDebug
```

Do **not** use plain `Debug`/`Release` configurations for platform-specific projects (installers, packaging). The shared projects (Core, host providers, tests) build under all configurations.

```shell
# Run all tests
dotnet test

# Run a single test project
dotnet test src/shared/Core.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"

# Test with coverage
dotnet test --collect:"XPlat Code Coverage" --settings=./.code-coverage/coverlet.settings.xml
```

Markdown linting uses [markdownlint-cli2](https://github.com/DavidAnson/markdownlint-cli2) with config in `.markdownlint.jsonc`. Link checking uses [lychee](https://lychee.cli.rs/) with `.lycheeignore`.

## Architecture

GCM is a [Git credential helper](https://git-scm.com/docs/gitcredentials) that reads input from stdin and writes credentials to stdout. Git invokes GCM with one of three commands: `get`, `store`, or `erase`.

### Key layers

- **`src/shared/Core/`** — Platform-agnostic core: `IHostProvider`, `ICommandContext` (service locator), `ICredentialStore`, command infrastructure (`System.CommandLine`), and platform subsystem abstractions.
- **`src/shared/GitHub/`, `GitLab/`, `Microsoft.AzureRepos/`, `Atlassian.Bitbucket/`** — Host provider implementations, one per supported Git hosting service.
- **`src/shared/Git-Credential-Manager/`** — Entry point. `Program.cs` registers all host providers with the `Application` and runs it. Contains very little logic.
- **`src/shared/TestInfrastructure/`** — Hand-written test doubles (no mocking framework). Provides `TestCommandContext` with all test fakes pre-wired.
- **`src/{osx,windows,linux}/`** — Platform-specific installer/packaging projects.

### Command execution flow

1. Git calls GCM with `get`/`store`/`erase` and passes request data on stdin.
2. The command consults the **Host Provider Registry** to find a matching provider (by priority, then registration order).
3. The matched provider's `GetCredentialAsync`/`StoreCredentialAsync`/`EraseCredentialAsync` is called.
4. For `get`, the credential is serialized back to Git on stdout.

### Host provider pattern

Providers extend the `HostProvider` abstract base class and implement:
- **`IsSupported(InputArguments)`** — Return `true` if this provider handles the request (e.g., matching hostname).
- **`GenerateCredentialAsync(InputArguments)`** — Create a new credential when none is cached.
- **`GetServiceName(InputArguments)`** (optional override) — Return a stable key for credential storage lookup. Default is `<protocol>://<host>[/<path>]`.

Providers are registered in `Program.cs` with `HostProviderPriority` (High, Normal, Low). `GenericHostProvider` must always be registered last at Low priority as the catch-all.

Providers may also implement `ICommandProvider` (custom subcommands), `IConfigurableComponent`, or `IDiagnosticProvider`.

### ICommandContext

`ICommandContext` is the service locator passed to providers and commands. Key services: `Settings`, `Streams` (stdin/stdout/stderr), `Terminal`, `CredentialStore`, `HttpClientFactory`, `FileSystem`, `Git`, `Environment`, `Trace`, `Trace2`.

## Key Conventions

### Testing

- **Framework**: xUnit (`[Fact]`, `[Theory]`).
- **No mocking library** — use the hand-written test doubles in `TestInfrastructure/`. `TestCommandContext` provides a fully-wired test environment with `TestSettings`, `TestCredentialStore`, `TestTerminal`, `TestHttpClientFactory`, etc.
- Test doubles use delegate properties (e.g., `IsSupportedFunc`, `GenerateCredentialFunc`) for configurable behavior.
- Test projects follow the naming convention `<ProjectName>.Tests`.

### Error handling

GCM uses a 'fail fast' approach — throw an `Exception` that propagates to the entry point. Error messages must be human-readable and include remediation steps or doc links when possible. Use `InteropException` for native/interop errors.

Warnings go to stderr via `ICommandContext.Streams.Error`. Diagnostic info goes to `ITrace`/`ITrace2`.

### Versioning

The product version lives in the `VERSION` file at the repo root (e.g., `2.7.2.0`). It is read at build time by a custom MSBuild task in `build/`.

### Async

The codebase uses `async`/`await` pervasively since most operations eventually hit the network.

### Debugging with Git

To debug GCM as invoked by Git, set `GCM_DEBUG=1` — GCM will pause on launch waiting for a debugger. For tracing, set `GCM_TRACE=1` (stderr) or `GCM_TRACE=/path/to/file`.
