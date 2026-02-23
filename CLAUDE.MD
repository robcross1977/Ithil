# Make Straight The Way - Coding Standards

## Collaboration Principles

**Neither the developer nor the AI is always right.** This is a collaborative process:

- **Challenge assumptions**: If the AI thinks the developer's approach violates standards or will cause problems, it should say so clearly and explain why.
- **Explain reasoning**: Both parties should explain their thinking, especially when disagreeing.
- **Prioritize correctly**: Architectural issues (paradigm violations, design patterns) take precedence over cosmetic issues (formatting, naming).
- **Follow the rules**: These standards exist for a reason. If something seems to contradict them, discuss it rather than silently ignoring the rules.
- **Learn from mistakes**: When either party misses something obvious (like ignoring clearly-stated functional programming requirements), acknowledge it and improve.

## Core Principles

### Functional Programming

- Prefer functional programming principles throughout
- Use pure functions without side effects
- Return values instead of mutating parameters
- Separate pure logic from side effects
- Compose functions declaratively
- Avoid void methods that mutate external state

### Monadic Types (LanguageExt)

- Use `Option<T>` for nullable values instead of null references
- Use `Try<T>` for operations that might fail with exceptions
- Use `Either<L, R>` for operations with two distinct outcomes
- Use `Seq<T>` instead of `List<T>` or `IEnumerable<T>` for immutable sequences
- Use `Map<K, V>` instead of `Dictionary<K, V>` for immutable dictionaries

### LINQ Query Comprehension Syntax

**IMPORTANT**: Prefer `from...in` query syntax over `.Bind()` method chaining when it doesn't cause difficulty.

```csharp
// ✅ PREFERRED: from...in syntax
var result = from inputs in ValidateInputs(caster, readyAbilities)
             from phase in DetermineCurrentPhase(inputs.caster)
             from ability in SelectAbilityForPhase(phase, readyAbilities)
             select ability;

// ❌ AVOID: .Bind() method chaining (unless from...in doesn't work well)
var result = ValidateInputs(caster, readyAbilities)
  .Bind(inputs => DetermineCurrentPhase(inputs.caster))
  .Bind(phase => SelectAbilityForPhase(phase, readyAbilities));
```

Use method chaining (`.Bind()`, `.Map()`, `.Match()`) only when:

- Complex conditionals are involved
- `.Match()` is needed for branching logic
- Query syntax becomes awkward or unreadable

## Documentation

### XML Doc Comments

- Add XML doc comments (`///`) over ALL publicly accessible classes and functions
- Include `<summary>`, `<param>`, `<returns>`, and `<remarks>` where appropriate
- Focus on WHAT and WHY, not the programming paradigm used
- **DON'T** mention "functional", "OOP", "monadic", etc. in summaries
- **DO** describe actual changes and their practical effects

```csharp
// ✅ GOOD
/// <summary>
/// Validates that all required inputs are present.
/// Returns the unit if all checks pass.
/// </summary>

// ❌ BAD
/// <summary>
/// Uses functional programming with Option monads to validate inputs.
/// </summary>
```

### Inline Comments

- Add concise comments ONLY when intent isn't obvious
- Avoid redundant comments that just repeat the code
- Explain WHY, not WHAT (the code shows what)

```csharp
// ✅ GOOD
// Block targets lowest HP% instead of lowest block for smarter defensive play

// ❌ BAD
// This loops through the enemies
```

## Code Organization

### File Structure

- Each file/function/module has a single, clear purpose
- Split functionality into new files if exceeding size/purpose limits
- Check for appropriate existing locations before creating new files/folders
  - Example: If an "abilities" folder exists, don't create another one
- Group related files (>2 files for one thing) into subfolders

### File Size Limits

- Limit code files to ~75 lines (excluding comments/whitespace/brackets)
- Limit functions to ~15 lines (excluding comments/whitespace/brackets)
- Split into reusable modules when exceeding limits

## Naming Conventions

### Clarity Over Brevity

- Use clear, descriptive names
- Limit abbreviations to common ones (e.g., `idx`, `HP`, `AI`)
- Boolean variables/properties should be questions: `IsValid`, `HasTarget`, `CanExecute`

```csharp
// ✅ GOOD
FindLowestHealthPercentageAlly()
IsAbilityContextuallyUsable()

// ❌ BAD
FindLowestHealthAlly() // Ambiguous - is it count or percentage?
IsUsable() // Too vague
```

## Control Flow

### Declarative Over Imperative

- Prefer `map`, `reduce`, `filter`, etc. over `for` loops when possible
- Use LINQ query syntax or method chaining instead of imperative loops

```csharp
// ✅ GOOD
var validUnits = units
  .Where(u => u.IsAlive && u.gameObject.activeInHierarchy)
  .OrderBy(u => u.CurrentHP / u.MaxHP)
  .ToSeq();

// ❌ AVOID (when declarative is clearer)
var validUnits = new List<Unit>();
foreach (var unit in units)
{
  if (unit.IsAlive && unit.gameObject.activeInHierarchy)
  {
    validUnits.Add(unit);
  }
}
validUnits.Sort((a, b) => (a.CurrentHP / a.MaxHP).CompareTo(b.CurrentHP / b.MaxHP));
```

### Immutability

- Use immutability unless reassignment is more efficient
- Prefer `Seq<T>` over `List<T>` for function parameters and returns
- Avoid mutating collections passed as parameters

## Side Effects and Purity

### Pure Functions

- Keep predicates and selectors pure (no side effects)
- **NEVER** perform state mutations within LINQ methods like `Where` or `Select`
- Separate pure logic from side effects

```csharp
// ❌ BAD - mutation inside LINQ
units.Where(u => {
  u.HasBeenProcessed = true; // NO! Side effect in predicate
  return u.IsAlive;
});

// ✅ GOOD - separate side effects
var aliveUnits = units.Where(u => u.IsAlive).ToSeq();
foreach (var unit in aliveUnits)
{
  unit.HasBeenProcessed = true;
}
```

### Logging Philosophy

**CRITICAL**: Only log when something that SHOULD work is NOT working.

- ✅ **DO Log**: Problems, failures, missing data, configuration errors
- ❌ **DON'T Log**: Normal operations, successful initialization, "this happened" info messages
- 💡 **Troubleshooting**: Add temporary debug logs when actively debugging specific issues, then remove them

```csharp
// ✅ GOOD - Logs actual problems
if (card == null)
  UnityEngine.Debug.LogWarning("[CardRepository] Card slot is null at index {i}");

if (duplicates.Any())
  UnityEngine.Debug.LogWarning("[CardRepository] Duplicate cards found: {names}");

// ❌ BAD - Logs normal operation (noise)
UnityEngine.Debug.Log("[CardRepository] Static constructor called");
UnityEngine.Debug.Log("[CardRepository] Loading {count} cards...");
UnityEngine.Debug.Log("[CardRepository] Card {i}: {cardName}");
UnityEngine.Debug.Log("[CardRepository] Initialization complete");
```

**Rationale**: Excessive logging makes it impossible to find real problems. The console should be quiet during normal operation and only show actionable issues.

### Logging Levels

- **`LogError`**: Critical failures that prevent core functionality

  - No fallback exists
  - Core algorithm can't function
  - Configuration is broken
  - Example: AI strategy fails to select any ability

- **`LogWarning`**: Unexpected but recoverable situations

  - Fallback behavior will be used
  - Non-critical validation failure
  - Example: Missing optional config, using defaults

- **No Log**: Expected behavior (even if edge case)
  - Normal fallback behavior
  - Validation that's part of the algorithm

### Option/Try Handling

```csharp
// ✅ GOOD - Log on failure
result.IfNone(() =>
  UnityEngine.Debug.LogError("[ComponentName] Critical failure: expected data missing")
);

// ✅ GOOD - Handle both cases explicitly
result.Match(
  Some: value => ProcessValue(value),
  None: () => UnityEngine.Debug.LogWarning("[ComponentName] Using fallback behavior")
);
```

## Code Cleanliness

### Dead Code

- Remove dead code immediately; keep files clean
- **DON'T** comment out dead code to keep it - use version control instead
- Delete unused imports, variables, and functions

### Comments

- Remove TODO comments once tasks are complete
- Remove debugging comments before committing
- Keep comments synchronized with code changes

## Code Reviews

### Review Structure

When performing code reviews, provide:

1. **Simple Explanation**: Break down what the code does in clear, simple terms
   - Explain the purpose and role of the component
   - Describe key design decisions and their benefits
   - Use analogies or comparisons when helpful

2. **Usage Analysis**: Check if questionable code is actually needed
   - Search codebase for usage of legacy/deprecated methods
   - Verify if backwards compatibility code is still referenced
   - Check if dead code exists that can be safely removed

3. **Standards Compliance**: Verify adherence to project standards
   - Check XML doc comments follow documentation guidelines
   - Ensure no mention of paradigms (functional/OOP/monadic) in summaries
   - Verify logging only happens for actual problems, not normal operations
   - Confirm immutability and functional patterns are used appropriately

4. **Recommendations**: Provide actionable feedback
   - Suggest specific improvements with rationale
   - Indicate priority (critical/important/nice-to-have)
   - Show before/after examples for clarity


### Dependencies

- Using LanguageExt for functional types
- No custom Option/Either implementations - use LanguageExt

### Memory

- After using `LanguageExt.Seq` with `OrderBy()` or `OrderByDescending()`
- Result is LINQ's `IOrderedEnumerable<T>` which doesn't have LanguageExt methods
- Convert back using `.ToSeq()` after ordering
- Use `Count()` method instead of `Count` property for `IOrderedEnumerable`

```csharp
// ✅ GOOD
mySeq.OrderBy(x => x).ToSeq().Take(n)

// ❌ BAD
mySeq.OrderBy(x => x).Take(n) // Compile error - Take() doesn't exist
```

---

## Design
When designing we have some folders to use. 

- Put all files not related to a specific feature in the Design folder.
- When designing a new feature, create a markdown file in the Backlog folder.
- I will move files we are working on to Design/Completed and Design/InProgress, and you can reference them there. 

## Feature Design
- All features should have mermaid "flow" diagrams,  and other diagrams if pertinent.
- All features should include "Acceptance Criteria"
- All features should have a breakdown of the files/functions/etc. we will be using.
- For each thing we are making, make a separate markdown file with the name, and then put in there a description of what it is for, and a unit testing plan we will implement before writing it to let us know when we got it. 

**Remember**: These are guidelines, not strict rules. Use judgment when they conflict. Clarity and maintainability are the ultimate goals.

lease note: I want you to assist me in writing code, not write it for me. Give me the code to write and guide me and answer questions. I need to know what is in the code. 
