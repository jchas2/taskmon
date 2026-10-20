# Coding Style

This guide describes how C# code in taskmon is written and laid out. Where a rule can be expressed
in [`.editorconfig`](../.editorconfig) it lives there and your IDE applies it; this document covers
the rest and explains the intent behind both.

The reference file is
[`src/taskmon/Gui/Controls/Processes/ProcessControl.cs`](../src/taskmon/Gui/Controls/Processes/ProcessControl.cs).
When in doubt, make new code look like it.

## Tooling

- **`./style.sh`** (macOS) or **`.\style.ps1`** (Windows) at the repo root checks the whole
  solution: `dotnet format style` plus the member-ordering check below. Run it before opening a
  pull request.
- **`.editorconfig`** at the repo root covers line endings (LF), encoding (UTF-8), indentation
  (4 spaces), brace placement, naming and language preferences. Rider, Visual Studio and VS Code
  pick it up automatically.
- **Don't run `dotnet format whitespace` or an IDE "Reformat Code" on whole files.** The default
  formatter collapses the column alignment described below. `dotnet format style` is safe.
- **[`eng/tools/reorder-members.cs`](../eng/tools/reorder-members.cs)** applies
  [Member ordering](#member-ordering), skipping the exempt types listed there. It reports any file
  it can't safely reorder (split `#if` blocks) so you can order it by hand. From the repo root:

  ```bash
  dotnet run eng/tools/reorder-members.cs -- --dry-run src/taskmon   # report only
  dotnet run eng/tools/reorder-members.cs -- src/taskmon             # rewrite files
  ```

## Files and namespaces

- One top-level type per file, named after the type.
- File-scoped namespaces (`namespace Task.Monitor.System.Services.Cpu;`) that match the folder path.
- Large types are split into **partial files** named `Type.Concern.cs`, e.g.
  `ProcessControl.Columns.cs`, `DiskService.DiskMetrics.Windows.cs`.
- Prefer putting nested types that are more than a few lines in their own partial file
  (`ProcessControl.ProcessListViewItem.cs`). Small private helper types can stay in the field region
  of the main file.

## Member ordering

Members are laid out top to bottom in four regions:

| # | Region | Order within the region |
|---|---|---|
| 1 | Fields (constants, `readonly`, mutable) and nested types | Grouped by meaning, not alphabetical. |
| 2 | Events | A–Z. |
| 3 | Constructors | Declaration order. |
| 4 | Properties and methods | **One A–Z list**, ignoring case. |

Region 4 is a single alphabetical list regardless of member kind, access level, `static` or
`override`: a public property, a private method and a protected override sit side by side purely
by name.

```csharp
public sealed class ExampleControl : Control
{
    private const int Gutter = 1;

    private readonly AppConfig appConfig;
    private readonly ListView  listView;

    private bool frozen;

    public event EventHandler<int>? SelectionChanged;

    public ExampleControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;
        listView       = new ListView(terminal);
    }

    public string FooterText { get; set; } = string.Empty;   // F

    private ListView? GetTargetControl() => ...                // G

    public override bool HasFocus => ...                       // H

    protected override void OnDraw() { ... }                   // O

    protected override void OnLoad() { ... }

    private void UpdateItems() { ... }                         // U
}
```

Details:

- Overloads keep their relative order.
- Comments, attributes and `#if` lines directly above a member move with it.
- Local functions stay inside the member that uses them.
- In partial types, each file is ordered on its own. Members never move between files.
- Two platform variants of the same member (`#if __APPLE__` / `#if __WIN32__`) stay together as one
  unit at that member's position.

Exempt from alphabetical ordering:

- **Property-only data classes** (`*Metrics`, `*Specs`, `*Device`, `ProcessEntry`). Their
  properties stay in groups that mean something, such as current, average and maximum values.
  A class with any methods, such as `AppConfig` or `Theme`, is ordered like any other.
- **Enums.** Order can carry meaning (e.g. `ProcessControl.Columns` is used as an index).
- **Primary constructor parameters.**

## Formatting

### Braces

- Types, methods, properties, indexers, events and local functions: brace on a **new line**.
- Control blocks (`if`, `for`, `foreach`, `while`, `switch`, `try`, `lock`, `using`), lambdas,
  property accessors and object initializers: brace on the **same line**.
- `else`, `catch` and `finally` start a new line after the closing brace.
- Always use braces for control blocks, even single statements.

```csharp
private void Refresh()
{
    if (frozen) {
        return;
    }

    try {
        Draw();
    }
    catch (Exception ex) {
        ExceptionHelper.LogException(ex);
    }
}
```

### Column alignment

Consecutive related declarations, assignments, initializers and switch arms are aligned in columns.
Separate unrelated groups with a blank line.

```csharp
private readonly ServiceController serviceController;
private readonly AppConfig         appConfig;
private readonly ListView          sortView;

sortView = new ListView(terminal) {
    Visible  = mode == ControlMode.SortSelection,
    TabStop  = true,
    TabIndex = 1
};

sortColumn = appConfig.SortColumn switch {
    Statistics.Pid     => Columns.Pid,
    Statistics.Process => Columns.Process,
                     _ => Columns.Cpu
};
```

### Wrapping

- Break long ternaries before `?` and `:`.
- Break fluent chains before `.`, one call per line.
- Expression-bodied members that don't fit on one line break after `=>`.

```csharp
int commandLineWidth = total + ColumnCommandlineWidth < processViewWidth
    ? processViewWidth - total
    : ColumnCommandlineWidth;

processView.ColumnHeaders
    .Add(new ListViewColumnHeader(Columns.Process.GetTitle()))
    .Add(new ListViewColumnHeader(Columns.Pid.GetTitle()));

public T GetService<T>() where T : ISystemService =>
    (T)allServices.Single(srv => srv.GetType() == typeof(T));
```

### Blank lines

- One blank line between members. No blank line straight after a type's opening brace.
- Blank lines inside methods separate logical steps.

## Naming

| Element | Style | Example |
|---|---|---|
| Types, methods, properties, events | PascalCase | `ProcessControl`, `OnDraw`, `HeaderText` |
| Interfaces | `I` + PascalCase | `ISystemService` |
| Constants | PascalCase | `SortControlWidth` |
| Private `static readonly` fields | PascalCase | `PulseInterval` |
| Other private fields | camelCase, **no underscore** | `appConfig`, `numberOfProcesses` |
| Parameters, locals | camelCase | `serviceController` |
| Test methods | `Member_Expected_Behaviour` with underscores | `ShouldRefresh_Restarts_The_Interval_After_Firing` |

Interop code is the exception: Win32 and Mach constants, structs and struct fields keep their
native SDK names (`PDH_CSTATUS_NEW_DATA`), so they can be checked against the platform headers.

## Language usage

- **Explicit types** over `var`. Use `var` only when the type is anonymous or long and obvious.
- Target-typed `new()` and collection expressions (`[]`) are preferred for fields and initializers.
- Use `this.` only to tell a field apart from a parameter with the same name, typically in
  constructors.
- Seal classes that aren't designed for inheritance.
- No `#region`s.
- No XML doc comments. Use `//` comments to explain *why* something is done when the code can't say
  it. Write them as full sentences.
- Mark known follow-up work with `// TODO: <what and why>`.

## Platform-specific code

taskmon builds on macOS (Apple Silicon) and Windows. The symbols `__APPLE__` and `__WIN32__` are
defined per OS in [`eng/Preprocessor.props`](../eng/Preprocessor.props).

- Native calls live only in `Task.Monitor.Interop.Mach` (macOS) and `Task.Monitor.Interop.Win32`
  (Windows).
- Platform implementations go in partial files suffixed `.MacOS.cs` or `.Windows.cs`, with the whole
  body wrapped in `#if __APPLE__` / `#if __WIN32__`.
- Keep `#if` blocks around whole members. Inside a method body, keep them short.
- When only one platform implements a feature, declare a `partial` method in the shared file and put
  the fallback stub at the end of the class:

```csharp
private partial DriversSpecs ScanDrivers();

#if !__WIN32__
private partial DriversSpecs ScanDrivers() => new();
#endif
```

A normal build only compiles the host platform's code. On macOS, `./build-win32.ps1` compiles the
`__WIN32__` code paths as a check (the output isn't runnable). CI builds and tests both platforms, so
check the `build-windows` and `build-macos` jobs before merging.

## Dependencies

taskmon takes **no third-party runtime dependencies**. Keeping the CPU and memory footprint of the
Native AOT binary low is a core design goal; see the README's
[Dependencies](../README.md#dependencies) section. Test projects may use xUnit and Moq.

## Tests

- xUnit (`[Fact]`, `[Theory]`) and Moq.
- Every application project has a matching test project in `tests/`, with folders mirroring the
  source project.
- Test classes are `sealed` and named `<TypeUnderTest>Tests`.
- Test classes follow the same [member ordering](#member-ordering). Test names start with the member
  under test, so A–Z keeps each member's tests together. Don't group tests with section-header
  comments (`// ---- Scrolling ----`); if a group needs its own setup, give it its own test class.
- Comments in tests explain the behaviour being guarded, not the mechanics of the assertion.
