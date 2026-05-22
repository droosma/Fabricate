using System;

namespace Fabricate;

/// <summary>
/// Marks a partial class as a builder for the specified type.
/// The generator will produce With methods, Build(), and an implicit operator.
/// The user must implement: static partial T ValidInstance();
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FabricateAttribute<T> : Attribute;
