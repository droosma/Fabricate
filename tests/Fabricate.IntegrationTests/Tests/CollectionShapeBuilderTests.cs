using Fabricate.IntegrationTests.Builders;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class CollectionShapeBuilderTests
{
    [Fact]
    public void CollectionConstructorParameter_BuildsAndOverrides()
    {
        var team = new TeamBuilder().Build();
        team.Members.Should().BeEquivalentTo("Alice", "Bob");

        var custom = new TeamBuilder().WithMembers("Zoe").Build();
        custom.Members.Should().BeEquivalentTo("Zoe");
    }

    [Fact]
    public void NullableCollectionConstructorParameter_Builds()
    {
        var team = new OptionalTeamBuilder().Build();
        team.Members.Should().BeEquivalentTo("Carol");
    }

    [Fact]
    public void InterfaceCollectionProperty_BuildsAndOverrides()
    {
        var tags = new TagListBuilder().Build();
        tags.Tags.Should().BeEquivalentTo("a", "b");

        var custom = new TagListBuilder().WithTags("only").Build();
        custom.Tags.Should().BeEquivalentTo("only");
    }

    [Fact]
    public void ArrayCollectionProperty_BuildsAndOverrides()
    {
        var board = new ScoreBoardBuilder().Build();
        board.Scores.Should().BeEquivalentTo(new[] { 1, 2, 3 });

        var custom = new ScoreBoardBuilder().WithScores(9).Build();
        custom.Scores.Should().BeEquivalentTo(new[] { 9 });
    }

    [Fact]
    public void NullableCollectionProperty_SupportsWithAndWithout()
    {
        var withValue = new OptionalTagsBuilder().WithTags("kept").Build();
        withValue.Tags.Should().BeEquivalentTo("kept");

        var cleared = new OptionalTagsBuilder().WithoutTags().Build();
        cleared.Tags.Should().BeNull();
    }

    [Fact]
    public void ImmutableCollectionProperty_BuildsAndOverrides()
    {
        var points = new ImmutablePointsBuilder().Build();
        points.Points.Should().BeEquivalentTo(new[] { 1, 2 });

        var custom = new ImmutablePointsBuilder().WithPoints(7, 8, 9).Build();
        custom.Points.Should().BeEquivalentTo(new[] { 7, 8, 9 });
    }

    [Fact]
    public void ReadOnlyComputedProperty_HasNoBuilderMethod()
    {
        var profile = new ProfileBuilder().WithFirst("Grace").WithLast("Hopper").Build();
        profile.FullName.Should().Be("Grace Hopper");

        typeof(ProfileBuilder).GetMethod("WithFullName").Should().BeNull();
    }
}
