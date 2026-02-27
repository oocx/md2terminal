# C# 13, C# 14, and .NET 10: LLM Reference Document

**Purpose:** This document is intended for LLMs trained before November 2024. It describes language and runtime features introduced in C# 13 (.NET 9, released November 2024), C# 14 (.NET 10, released November 2025), and key .NET 10 platform changes. When generating C# code, prefer these newer patterns where appropriate.

**Versioning quick-reference:**
- C# 13 ships with .NET 9 (released November 2024, STS — support ends November 2026)
- C# 14 ships with .NET 10 (released November 2025, LTS — support ends November 2028)
- To enable: set `<LangVersion>14</LangVersion>` in `.csproj`. Extension members require `<LangVersion>preview</LangVersion>` until fully stabilized.
- Visual Studio 2026 is required for full .NET 10 / C# 14 tooling support.

---

## C# 13 Features (.NET 9)

### 1. `params` Collections (Generalized)
Previously `params` required an array type. C# 13 allows any supported collection type: `Span<T>`, `ReadOnlySpan<T>`, `IEnumerable<T>`, `IList<T>`, `List<T>`, or any type with an `Add` method.

```csharp
// Old: only arrays
void Print(params int[] values) { }

// New: spans or lists
void Print(params ReadOnlySpan<int> values) { }
void Print(params List<int> values) { }
```

**Impact:** The .NET 9 BCL uses this to add a new `Console.WriteLine` overload taking `ReadOnlySpan<object?>`, avoiding heap allocation for the arguments array. Code calling such methods gets faster automatically after recompile.

---

### 2. New `System.Threading.Lock` Type
A dedicated `Lock` type replaces the pattern of locking on a plain `object`. The C# `lock` statement automatically uses the new API when the target is `System.Threading.Lock`.

```csharp
// Old pattern
private readonly object _sync = new();
lock (_sync) { /* ... */ }

// New pattern
private readonly Lock _sync = new();
lock (_sync) { /* ... */ } // compiler uses Lock.EnterScope() internally
```

**Impact:** Prefer `Lock` over `object` for new synchronization code. The compiler warns if a `Lock` instance is accidentally used in an old-style object lock context.

---

### 3. Partial Properties and Indexers
C# 13 extends `partial` to properties and indexers (complementing `partial` methods from C# 9). Declaration and implementation can be split across files — primarily useful for source generators.

```csharp
// Declaration file (e.g., generated)
partial class MyModel
{
    public partial string Name { get; set; }
}

// Implementation file (user-authored)
partial class MyModel
{
    public partial string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }
    private string _name = string.Empty;
}
```

---

### 4. `ref` and `unsafe` in Iterators and Async Methods
Previously, `ref` locals and `unsafe` blocks could not appear in `async` methods or iterator methods (`yield return`). C# 13 lifts this restriction as long as usage does not span an `await` or `yield` boundary.

```csharp
async Task ProcessAsync(Stream stream)
{
    byte[] buffer = new byte[1024];
    int bytesRead = await stream.ReadAsync(buffer);
    ReadOnlySpan<byte> data = buffer.AsSpan(0, bytesRead); // now allowed
    // process data synchronously here
}
```

---

### 5. `^` (Index-from-end) Operator in Object Initializers
The `^` operator can now be used inside object initializers to index from the end of a collection.

```csharp
var timer = new TimerRemaining
{
    buffer = { [^1] = 0, [^2] = 1, [^3] = 2 } // previously illegal
};
```

---

### 6. New Escape Sequence `\e`
`\e` is a character literal for the ESCAPE character (`U+001B`), common in ANSI terminal escape codes.

```csharp
Console.Write("\e[31mRed text\e[0m"); // cleaner than \u001B or \x1B
```

---

### 7. `OverloadResolutionPriorityAttribute`
Library authors can annotate an overload with `[OverloadResolutionPriority(n)]` to guide the compiler to prefer a newer, better overload without breaking existing call sites.

```csharp
public void Log(string msg) { }

[OverloadResolutionPriority(1)]
public void Log(ReadOnlySpan<char> msg) { } // preferred when both apply
```

---

### 8. Method Group Natural Type Improvements
Overload resolution for method groups is more precise: the compiler prunes non-applicable candidates scope-by-scope, reducing false ambiguity errors in delegate-assignment scenarios.

---

### 9. `field` Keyword (Preview in C# 13, Stable in C# 14)
Introduced as preview in C# 13; see C# 14 section for stable details.

---

## C# 14 Features (.NET 10)

### 1. Extension Members (Headline Feature)
C# 14 introduces a new `extension` block syntax that unifies extension methods, extension properties, static extension members, and extension operators into a single declaration block.

```csharp
public static class StringExtensions
{
    extension(string value)
    {
        // Extension property (instance)
        public bool IsNullOrEmpty => string.IsNullOrEmpty(value);

        // Extension method (instance)
        public string Truncate(int max) =>
            value.Length <= max ? value : value[..max];

        // Static extension on string type itself
        public static bool IsAscii(char c) => c <= 0x7F;
    }
}

// Usage
string s = "hello world";
bool empty = s.IsNullOrEmpty;        // extension property
string truncated = s.Truncate(5);    // extension method
bool ascii = string.IsAscii('A');    // static extension
```

**Key rules:**
- Extension blocks go inside a `public static class`.
- The receiver parameter name (`value` above) is optional for static-only blocks.
- Fully compatible with existing extension methods — no migration needed.
- Extension operators (e.g., defining `+` for a type you don't own) are also supported via `operator` declarations inside an `extension` block.

---

### 2. `field` Keyword (Stable)
Access the compiler-synthesized backing field of an auto-property directly inside a property accessor. Eliminates the need to manually declare a backing field when only partial custom logic is required.

```csharp
// Before C# 14: manual backing field required
private string _name = string.Empty;
public string Name
{
    get => _name;
    set => _name = value?.Trim() ?? string.Empty;
}

// C# 14: use field keyword
public string Name
{
    get;
    set => field = value?.Trim() ?? string.Empty;
}
```

**Notes:** `field` is a contextual keyword. Use `@field` if you have a variable named `field`.

---

### 3. Null-Conditional Assignment (`?.=`)
The null-conditional operator can now appear on the **left side** of an assignment. The assignment is skipped if the object is null.

```csharp
// Before
if (customer != null)
    customer.Order = GetOrder();

// C# 14
customer?.Order = GetOrder();

// Works with nested chains
order?.Customer?.Address = newAddress;
```

**Note:** Increment/decrement (`++`/`--`) cannot be used in this pattern.

---

### 4. Implicit Span Conversions (First-Class Span Types)
C# 14 adds implicit conversions among `T[]`, `Span<T>`, and `ReadOnlySpan<T>`, removing the need for explicit `.AsSpan()` calls in most contexts. Span types can also act as extension method receivers and participate in generic type inference.

```csharp
byte[] buffer = new byte[256];

// Before: explicit
ProcessSpan(buffer.AsSpan(..8));

// C# 14: implicit
ReadOnlySpan<byte> slice = buffer[..8]; // implicit array-to-span conversion
ProcessSpan(slice);
```

**Breaking change:** New implicit conversions may cause overload resolution to select a different overload than C# 13. If ambiguity errors arise, add an explicit `.AsSpan()` cast or use `OverloadResolutionPriorityAttribute`.

---

### 5. User-Defined Compound Assignment Operators (`+=`, `-=`, `*=`, etc.)
Custom types can now define compound assignment operators directly. Previously `+=` always desugared to `x = x + y`, creating copies. Now you can define `+=` to mutate in place — important for large value types like vectors/matrices.

```csharp
public struct Vector3
{
    public double X, Y, Z;

    public static Vector3 operator +(Vector3 a, Vector3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    // New in C# 14: define += directly to avoid copy overhead
    public static void operator +=(ref Vector3 a, Vector3 b)
    {
        a.X += b.X; a.Y += b.Y; a.Z += b.Z;
    }
}
```

---

### 6. `nameof` with Unbound Generic Types
`nameof` now accepts open generic types like `List<>`, returning the unqualified type name.

```csharp
string name1 = nameof(List<>);        // "List"  (new in C# 14)
string name2 = nameof(List<int>);     // "List"  (worked before)
string name3 = nameof(Dictionary<,>); // "Dictionary"
```

---

### 7. Lambda Parameter Modifiers Without Explicit Types
You can now apply `ref`, `in`, `out`, `scoped`, or `ref readonly` to lambda parameters without specifying the parameter type explicitly.

```csharp
// Before: type required when using modifier
var tryParse = (string s, out int result) => int.TryParse(s, out result);

// C# 14: type can be inferred
delegate bool TryParse<T>(string s, out T result);
TryParse<int> p = (s, out result) => int.TryParse(s, out result);
```

**Breaking change:** `scoped` is now always treated as a modifier keyword in lambda parameters. Rename any type called `scoped` to `@scoped`.

---

### 8. Partial Constructors and Partial Events
C# 14 extends `partial` (already available for methods, properties, and indexers) to instance constructors and events.

```csharp
// Useful for source generators and interop code generators
partial class MyComponent
{
    public partial MyComponent(string id); // declaration

    public partial event EventHandler? Initialized; // declaration
}

partial class MyComponent
{
    public partial MyComponent(string id) // implementation
    {
        Id = id;
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    public partial event EventHandler? Initialized
    {
        add => _initialized += value;
        remove => _initialized -= value;
    }
    private EventHandler? _initialized;
}
```

**Note:** Unlike `partial` methods, partial constructors and partial events **must** have an implementation.

---

## .NET 10 Platform Features

### Runtime & JIT Improvements
- **Array interface devirtualization:** The JIT can now inline and devirtualize array interface calls (e.g., enumerating via `IEnumerable<T>`), reducing abstraction overhead.
- **Improved block ordering:** The JIT uses a Travelling Salesman Problem heuristic to reorder code blocks, increasing hot-path density and reducing branch distances.
- **Stack allocation improvements:** More arrays and enumerators are stack-allocated rather than heap-allocated via improved conditional escape analysis.
- **Struct argument register packing:** Struct members can be packed into shared registers directly, eliminating unnecessary memory store/load pairs.
- **Write barrier optimization:** Dynamic switching between write-barrier implementations on x64 improves GC throughput.
- **NativeAOT enhancements:** The type preinitializer supports more opcodes, reducing startup time and binary size.

### Libraries & APIs
- **JSON serialization:** New options include `DisallowDuplicateProperties`, strict serialization settings, and `PipeReader` support.
- **Post-quantum cryptography:** New APIs for quantum-resistant algorithms, preparing applications for future security requirements.
- **Simplified `WebSocketStream` API:** A cleaner abstraction over WebSocket communication.
- **`SearchValues<string>`:** Extended from characters/bytes to strings, with `StringComparison` support — use for high-performance multi-string searching.
- **LINQ additions (from .NET 9 — use if on .NET 9+):** `CountBy`, `AggregateBy`, and `Index` extension methods.
- **TLS 1.3 on macOS:** Now supported.

### SDK & Tooling
- **File-based apps:** Single `.cs` files can be run directly with `dotnet run` — no `.csproj` required. Also supports `publish` and NativeAOT.
- **`dotnet tool exec`:** One-shot tool execution without installation.
- **SLNX project files:** New XML-based solution file format.
- **Native tab-completion scripts:** `dotnet` CLI generates completion scripts for popular shells.
- **Console apps as container images:** `dotnet publish` can produce container images without a Dockerfile.


## Code Generation Guidance for LLMs

When generating C# code targeting .NET 10 / C# 14:

1. **Prefer `Lock` over `object` for synchronization.**
2. **Use `field` keyword** instead of manual backing fields when only partial accessor logic is needed.
3. **Use `?.=`** (null-conditional assignment) instead of `if (x != null) x.Prop = val`.
4. **Use extension blocks** (`extension(T receiver) { ... }`) instead of static extension methods when defining multiple extensions for the same type — especially when you want extension properties.
5. **Use `params ReadOnlySpan<T>`** instead of `params T[]` for new method signatures — reduces allocations.
6. **Use `nameof(MyType<>)`** for open generic type names.
7. **Define `+=` / `-=` operators** for large value types (vectors, matrices) to avoid unnecessary copies.
8. **Do not add `.AsSpan()`** calls when assigning arrays to `Span<T>` or `ReadOnlySpan<T>` variables — implicit conversions handle this.
9. **For ref struct usage in async/iterator methods**, you can now use `ref` locals before the first `await`/`yield` — no need for a separate helper method.
10. **Use `partial` constructors and events** when generating source-generator-compatible scaffolding code.

