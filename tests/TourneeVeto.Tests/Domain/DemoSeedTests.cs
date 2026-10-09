using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Tests.Domain;

public sealed class DemoSeedTests
{
    private static readonly DateOnly AujourdHui = new(2026, 10, 9);

    [Fact]
    public void Generation_FournitHistoriqueTourneeEtActionsPourChaqueElevage()
    {
        var jeu = DemoData.GenerateSeed(AujourdHui, 42);

        Assert.Equal(5, jeu.Herd.Locations.Count);
        Assert.Equal(500, jeu.Herd.Cows.Count);
        Assert.Equal(15, jeu.Visits.Count);
        Assert.All(jeu.Herd.Locations, elevage =>
        {
            var visites = jeu.Visits.Where(v => v.FarmId == elevage.Id).ToArray();
            Assert.Equal(3, visites.Length);
            Assert.Single(visites, v => v.Date == AujourdHui.AddDays(-30));
            Assert.Single(visites, v => v.Date == AujourdHui.AddDays(1));
            var visite = Assert.Single(visites, v => v.Date == AujourdHui);
            Assert.Equal(Enum.GetValues<ActionType>(), visite.Actions.Select(a => a.Type));
            Assert.All(visite.Actions, action =>
            {
                Assert.False(action.IsCompleted);
                Assert.Equal(AujourdHui, action.Date);
                Assert.Contains(jeu.Herd.Cows, v => v.Id == action.CowId && v.LocationId == elevage.Id);
            });
            Assert.True(Assert.Single(visites.Single(v => v.Date < AujourdHui).Actions).IsCompleted);
        });

        var ids = jeu.Herd.Locations.Select(e => e.Id)
            .Concat(jeu.Herd.Cows.Select(v => v.Id))
            .Concat(jeu.Visits.Select(v => v.Id))
            .Concat(jeu.Visits.SelectMany(v => v.Actions).Select(a => a.Id)).ToArray();
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Generation_EstReproductibleSansChangerLeTroupeauExistant()
    {
        var premier = DemoData.GenerateSeed(AujourdHui, 42);
        var second = DemoData.GenerateSeed(AujourdHui, 42);
        var troupeau = DemoData.Generate(AujourdHui, 42);

        Assert.Equal(troupeau.Locations, premier.Herd.Locations);
        Assert.Equal(troupeau.Cows, premier.Herd.Cows);
        Assert.Equal(premier.Visits.Select(v => v.Id), second.Visits.Select(v => v.Id));
        for (var i = 0; i < premier.Visits.Count; i++)
        {
            Assert.Equal(premier.Visits[i].Actions, second.Visits[i].Actions);
        }
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(10, 1, 1)]
    [InlineData(9999, 12, 30)]
    public void Generation_AccepteLesDatesLimitesDocumentees(int annee, int mois, int jour)
    {
        var date = new DateOnly(annee, mois, jour);
        var jeu = DemoData.GenerateSeed(date, 42);
        Assert.Contains(jeu.Visits, v => v.Date == date.AddDays(1));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9, 12, 31)]
    [InlineData(9999, 12, 31)]
    public void Generation_RejetteUneDateNePermettantPasLeJeuComplet(int annee, int mois, int jour)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DemoData.GenerateSeed(new DateOnly(annee, mois, jour), 42));
    }
}
