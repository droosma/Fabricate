using System;

namespace Fabricate;

/// <summary>
/// Assembly-level attribute to configure the factory class name.
/// Default is "A" if not specified.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public sealed class FabricateFactoryAttribute : Attribute
{
    public string Name { get; }

    public FabricateFactoryAttribute(string name = "A")
    {
        Name = name;
    }
}
