# Fabricate

[![NuGet version](https://img.shields.io/nuget/v/Fabricate.svg)](https://www.nuget.org/packages/Fabricate)
[![Build status](https://img.shields.io/github/actions/workflow/status/droosma/Fabricate/ci.yml?branch=main)](https://github.com/droosma/Fabricate/actions)
[![Mutation testing](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/droosma/Fabricate/badges/.badges/mutation.json)](https://github.com/droosma/Fabricate/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/droosma/Fabricate)](LICENSE)

A .NET source generator that creates builder pattern classes for test projects. Define what a valid object looks like once, get fluent `With` methods for free.

## Why?

Test setup is verbose. You create objects, set properties, and repeat. Fabricate lets you define a **valid instance** of your domain object once, then override only what matters per test — surfacing intent and reducing noise.

## Installation

```bash
dotnet add package Fabricate
```

> **Note:** Add this only to your test project. Use `PrivateAssets="all"` to prevent it leaking to production.

## Quick Start

Given a domain model:

```csharp
public class Patient
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string? MiddleName { get; set; }
}
```

Create a builder:

```csharp
using Fabricate;

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new()
    {
        Name = "John Doe",
        Age = 30,
        MiddleName = null
    };
}
```

Use it in tests:

```csharp
// Default valid instance
Patient patient = new PatientBuilder().Build();

// Override specific values
Patient child = new PatientBuilder().WithAge(5).Build();

// Implicit conversion
Patient patient = new PatientBuilder().WithName("Jane");

// Factory shorthand (auto-generated)
Patient patient = A.Patient.WithName("Quick").Build();
```

## Features

### Supported Types
- Classes, records, structs, readonly structs, record structs
- Constructor parameters (auto-detected)
- Init-only properties
- Nullable properties (generates `Without` methods)
- Collections (`params` arrays)
- Inherited properties (flattened)

### Smart `With` Method Naming
- **BCL types** (string, int, DateTime, etc.) → `WithPropertyName(T value)` 
- **Custom types** (unique) → `With(T value)` — the type is self-describing
- **Custom types** (duplicated) → `WithPropertyName(T value)` — disambiguate

### Factory Class
A static `A` class is auto-generated with properties for each builder:

```csharp
A.Patient           // → new PatientBuilder()
A.Appointment       // → new AppointmentBuilder()
```

Customize the name with an assembly attribute:

```csharp
[assembly: FabricateFactory("Given")]
// Now use: Given.Patient.WithName("test")
```

### Nullable Support
```csharp
// Set a nullable value
builder.WithMiddleName("Marie")

// Clear a nullable value
builder.WithoutMiddleName()
```

### Collections
```csharp
// Set multiple values
builder.WithAllergies("Peanuts", "Shellfish")

// Set single value
builder.WithAllergies("Dust")
```

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| FAB001 | Error | `ValidInstance()` method not implemented |
| FAB002 | Warning | Builder class name doesn't end with "Builder" |
| FAB003 | Error | Cannot resolve a constructor for the target type |
| FAB004 | Warning | Duplicate factory property for the same target type (only the first builder is exposed) |

## Extending the Factory

Since `A` is a `partial class`, extend it in your test project:

```csharp
public static partial class A
{
    public static PatientBuilder AdultPatient => Patient.WithAge(30);
    public static PatientBuilder ChildPatient => Patient.WithAge(5);
}
```

## License

MIT
