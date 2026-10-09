using Bunit;
using TourneeVeto.Domain;
using TourneeVeto.Ui.Components;

namespace TourneeVeto.Tests.Ui;

public sealed class DemoOverviewTests
{
    [Fact]
    public void Render_DisplaysEveryFictionalFarmAndTheScopeWarning()
    {
        using var context = new BunitContext();
        var data = DemoData.Generate(new DateOnly(2026, 10, 9), seed: 42);

        var component = context.Render<DemoOverview>(parameters => parameters
            .Add(p => p.Data, data));

        var farms = component.FindAll("li");
        Assert.Equal(DemoData.FarmCount, farms.Count);
        for (var index = 0; index < data.Locations.Count; index++)
        {
            var farm = data.Locations[index];
            Assert.Contains(farm.Name, farms[index].TextContent);
            Assert.Contains(farm.City, farms[index].TextContent);
            Assert.Contains($"{farm.AnimalCount} vaches", farms[index].TextContent);
        }
        Assert.Contains("Toutes les données sont fictives", component.Find("section").TextContent);
        Assert.Contains("restent à implémenter", component.Find("section").TextContent);
    }
}
