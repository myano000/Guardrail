# Guardrail

**A Roslyn analyzer collection that enforces coding policies at compile time — so AI agents fix their own mistakes.**

---

## Why Guardrail?

You write coding policies in CLAUDE.md. The AI agent ignores them by the next message.  
You end up pointing out the same issues over and over: "stop using bool parameters", "don't downcast", "every test needs an assertion".

Guardrail turns those policies into **compiler warnings with built-in fix instructions**.  
The agent reads the error message, self-corrects, and moves on — no human review required.

```
Warning GRD005: Parameter 'isUrgent' is of type bool.
  Express intent using an enum, method overloads, or an options type instead.
```

Set up Guardrail once, then go get that coffee.

---

## Rules

| ID     | Rule                              | What it catches                                                        |
|--------|-----------------------------------|------------------------------------------------------------------------|
| GRD001 | Constructor assignments only      | Method calls or control flow inside a constructor body                 |
| GRD002 | Single instance constructor       | More than one constructor — use factory methods instead                |
| GRD003 | No ref / out parameters           | `ref`/`out` params — return a tuple instead                            |
| GRD004 | Test methods must assert          | Test methods with no assertion — they always pass and prove nothing    |
| GRD005 | No bool parameters                | Flag arguments like `Process(true)` — intent is invisible at call site |
| GRD006 | No downcasts                      | `(Dog)animal` or `animal as Cat` — use polymorphism                   |
| GRD007 | No meaningless null checks        | `new Foo() == null` — agents write this defensively, it's always false |

---

## Quick Start

### 1. Add the analyzer to your project

```xml
<!-- .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/Guardrail.Analyzers.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

NuGet package (coming soon):
```
dotnet add package Guardrail.Analyzers
```

### 2. Configuration (optional)

Place a `guardrail.json` file at the project root and register it as an additional file:

```xml
<!-- .csproj -->
<ItemGroup>
  <AdditionalFiles Include="guardrail.json" />
</ItemGroup>
```

```json
{
  "boolParameter": {
    "allowedMethods": ["TryParse", "IsEnabled"]
  },
  "noDowncast": {
    "excludedFilePatterns": ["/Ui/", "/Views/"],
    "excludedNamespacePatterns": ["MyApp.Ui"]
  },
  "constructorOnlyAssignments": {
    "allowedInvocations": ["InitializeComponent"]
  },
  "noRefOutParameter": {
    "allowedMethods": ["TryGetValue", "TryParse"]
  }
}
```

### 3. Build

```bash
dotnet build
```

Violations appear as warnings. Promote them to errors to block the build:

```xml
<PropertyGroup>
  <WarningsAsErrors>GRD001;GRD002;GRD003;GRD004;GRD005;GRD006;GRD007</WarningsAsErrors>
</PropertyGroup>
```

---

## Rule Reference

### GRD001 — Constructor assignments only

Constructors should only assign fields and properties. Side effects and logic belong in factory methods.

```csharp
// Violation
public OrderService(ILogger logger)
{
    _logger = logger;
    _logger.Log("initialized");  // method call → GRD001
}

// Compliant
public OrderService(ILogger logger)
{
    _logger = logger;
}

public static OrderService Create(ILogger logger)
{
    var svc = new OrderService(logger);
    svc.Initialize();
    return svc;
}
```

### GRD002 — Single instance constructor

Multiple constructors force callers to know which one to use and obscure dependencies.

```csharp
// Violation
public class Report
{
    public Report() { }
    public Report(string title) { _title = title; }  // second constructor → GRD002
}

// Compliant
public class Report
{
    private Report(string title) { _title = title; }

    public static Report Untitled()           => new Report("");
    public static Report WithTitle(string t)  => new Report(t);
}
```

### GRD003 — No ref / out parameters

`ref`/`out` make side effects invisible. Return a tuple instead.

```csharp
// Violation
bool TryParse(string s, out int result) { ... }  // GRD003

// Compliant
(bool success, int result) TryParse(string s) { ... }
```

### GRD004 — Test methods must assert

A test with no assertion always passes. It is not a test; it is noise.

```csharp
// Violation
[Test]
public void CalculateTotal_ReturnsValue()
{
    var result = _calc.Total();  // no assertion → GRD004
}

// Compliant
[Test]
public void CalculateTotal_ReturnsExpectedSum()
{
    var result = _calc.Total();
    Assert.That(result, Is.EqualTo(100));
}
```

### GRD005 — No bool parameters (flag arguments)

`Send(message, true)` — what does `true` mean? The caller cannot tell without reading the implementation.

```csharp
// Violation
void Send(string message, bool isUrgent) { ... }  // GRD005

// Compliant — express intent with an enum
void Send(string message, Priority priority) { ... }

// Compliant — split into separate methods
void Send(string message) { ... }
void SendUrgent(string message) { ... }
```

### GRD006 — No downcasts

Downcasts break the type system's guarantees and are a common source of `InvalidCastException`.

```csharp
// Violation
Animal a = GetAnimal();
var dog = (Dog)a;    // GRD006
var cat = a as Cat;  // GRD006

// Compliant
animal.Speak();           // polymorphism
animal.Accept(visitor);   // Visitor pattern
```

UI frameworks sometimes require downcasts. Use `excludedFilePatterns` in `guardrail.json` to opt out specific paths.

### GRD007 — No meaningless null checks

`new` never returns null. AI agents write defensive null checks after object creation — Guardrail catches this pattern.

```csharp
// Violation
var order = new Order();
if (order == null) { ... }    // GRD007: order can never be null

// Violation
if (new Foo() != null) { ... } // GRD007: always true

// Compliant
Order? order = GetOrderOrNull();
if (order == null) { ... }     // received from outside — check is valid
```

---

## Roadmap

Guardrail starts with C# and Roslyn, but the concept applies everywhere AI agents write code.

- **Unused properties and variables** — catch dead code agents leave behind
- **JavaScript / TypeScript** — ESLint custom rules with the same philosophy
- **Python** — flake8 / pylint plugins

The goal is a language-agnostic guardrail layer for vibe coding.

---

## Development

```bash
# Build
dotnet build Guardrail.sln

# Test
dotnet test

# Sample — compliant code (no warnings)
dotnet build samples/Guardrail.Sample/Guardrail.Sample.csproj

# Sample — violations (warnings expected)
dotnet build samples/Guardrail.Sample.Violations/Guardrail.Sample.Violations.csproj
```

## License

MIT
