using System.Collections.Generic;
using System.Collections.Immutable;

namespace Fabricate.IntegrationTests.Models;

public record Team(string Name, List<string> Members);

public record OptionalTeam(string Name, List<string>? Members);

public class TagList
{
    public string Name { get; set; } = "";
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
}

public class ScoreBoard
{
    public string Name { get; set; } = "";
    public int[] Scores { get; set; } = System.Array.Empty<int>();
}

public class OptionalTags
{
    public string Name { get; set; } = "";
    public List<string>? Tags { get; set; }
}

public class ImmutablePoints
{
    public string Name { get; set; } = "";
    public ImmutableArray<int> Points { get; set; } = ImmutableArray<int>.Empty;
}

public class Profile
{
    public string First { get; set; } = "";
    public string Last { get; set; } = "";
    public string FullName => $"{First} {Last}";
}
