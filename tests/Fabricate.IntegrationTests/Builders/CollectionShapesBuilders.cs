using System.Collections.Generic;
using System.Collections.Immutable;
using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<Team>]
public partial class TeamBuilder
{
    private static partial Team ValidInstance() => new("Reds", new List<string> { "Alice", "Bob" });
}

[Fabricate<OptionalTeam>]
public partial class OptionalTeamBuilder
{
    private static partial OptionalTeam ValidInstance() => new("Blues", new List<string> { "Carol" });
}

[Fabricate<TagList>]
public partial class TagListBuilder
{
    private static partial TagList ValidInstance() => new() { Name = "n", Tags = new List<string> { "a", "b" } };
}

[Fabricate<ScoreBoard>]
public partial class ScoreBoardBuilder
{
    private static partial ScoreBoard ValidInstance() => new() { Name = "n", Scores = new[] { 1, 2, 3 } };
}

[Fabricate<OptionalTags>]
public partial class OptionalTagsBuilder
{
    private static partial OptionalTags ValidInstance() => new() { Name = "n", Tags = new List<string> { "x" } };
}

[Fabricate<ImmutablePoints>]
public partial class ImmutablePointsBuilder
{
    private static partial ImmutablePoints ValidInstance() => new() { Name = "n", Points = ImmutableArray.Create(1, 2) };
}

[Fabricate<Profile>]
public partial class ProfileBuilder
{
    private static partial Profile ValidInstance() => new() { First = "Ada", Last = "Lovelace" };
}
