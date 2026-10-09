using TourneeVeto.Domain;

namespace TourneeVeto.Tests.Domain;

public sealed class DemoDataTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void Generate_PreservesFarmAndHerdCounts()
    {
        var data = DemoData.Generate(Today, seed: 42);

        Assert.Equal(DemoData.FarmCount, data.Locations.Count);
        Assert.Equal(DemoData.FarmCount * DemoData.CowsPerFarm, data.Cows.Count);
        Assert.Equal(data.Cows.Count, data.Cows.Select(cow => cow.Id).Distinct().Count());
        Assert.All(data.Locations, farm =>
        {
            Assert.Equal(DemoData.CowsPerFarm, farm.AnimalCount);
            Assert.Equal(farm.AnimalCount, data.Cows.Count(cow => cow.LocationId == farm.Id));
            Assert.Contains(data.Cows, cow =>
                cow.LocationId == farm.Id && cow.LastCcs >= DemoData.HighCcsThreshold);
        });
        Assert.All(data.Cows, cow => Assert.Contains(data.Locations, farm => farm.Id == cow.LocationId));
    }

    [Fact]
    public void Generate_IsReproducibleForTheSameDateAndSeed()
    {
        var first = DemoData.Generate(Today, seed: 42);
        var second = DemoData.Generate(Today, seed: 42);

        Assert.Equal(first.Locations.ToArray(), second.Locations.ToArray());
        Assert.Equal(first.Cows.ToArray(), second.Cows.ToArray());
    }
}
