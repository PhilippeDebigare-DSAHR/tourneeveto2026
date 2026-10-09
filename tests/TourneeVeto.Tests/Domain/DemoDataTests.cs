using TourneeVeto.Domain;
using TourneeVeto.Domain.Herd;

namespace TourneeVeto.Tests.Domain;

public sealed class DemoDataTests
{
    private static readonly DateOnly AujourdHui = new(2026, 10, 9);
    private const int Graine = 42;

    [Fact]
    public void Generate_CreeCinqElevagesEtCentVachesParElevage()
    {
        var donnees = DemoData.Generate(AujourdHui, Graine);

        Assert.Equal(DemoData.FarmCount, donnees.Locations.Count);
        Assert.Equal(DemoData.FarmCount * DemoData.CowsPerFarm, donnees.Cows.Count);
        Assert.Equal(DemoData.FarmCount, donnees.Locations.Select(e => e.Id).Distinct().Count());
        Assert.Equal(DemoData.FarmCount * DemoData.CowsPerFarm, donnees.Cows.Select(v => v.Id).Distinct().Count());
        Assert.Equal(donnees.Locations.Count + donnees.Cows.Count,
            donnees.Locations.Select(e => e.Id).Concat(donnees.Cows.Select(v => v.Id)).Distinct().Count());
        Assert.Equal(DemoData.FarmCount, donnees.Locations.Select(e => e.Name).Distinct().Count());

        Assert.All(donnees.Locations, elevage =>
        {
            Assert.NotEqual(Guid.Empty, elevage.Id);
            Assert.False(string.IsNullOrWhiteSpace(elevage.Name));
            Assert.False(string.IsNullOrWhiteSpace(elevage.City));
            Assert.Equal(DemoData.CowsPerFarm, elevage.AnimalCount);
            Assert.Equal(DemoData.CowsPerFarm, donnees.Cows.Count(vache => vache.LocationId == elevage.Id));
        });

        Assert.All(donnees.Cows, vache =>
        {
            Assert.Contains(donnees.Locations, elevage => elevage.Id == vache.LocationId);
            Assert.False(string.IsNullOrWhiteSpace(vache.Name));
        });
    }

    [Fact]
    public void Generate_EstReproductiblePourUneDateEtUneGraineIdentiques()
    {
        var premier = DemoData.Generate(AujourdHui, Graine);
        var second = DemoData.Generate(AujourdHui, Graine);

        Assert.Equal(premier.Locations, second.Locations);
        Assert.Equal(premier.Cows, second.Cows);
    }

    [Fact]
    public void Generate_ProduitDesJeuxDifferentsAvecDesGrainesDifferentes()
    {
        var premier = DemoData.Generate(AujourdHui, Graine);
        var second = DemoData.Generate(AujourdHui, Graine + 1);

        Assert.NotEqual(premier.Locations, second.Locations);
        Assert.NotEqual(premier.Cows, second.Cows);
    }

    [Fact]
    public void Generate_GardeLesDatesEtLesValeursDesVachesDansDesBornesCoherentes()
    {
        var donnees = DemoData.Generate(AujourdHui, Graine);

        Assert.All(donnees.Cows, vache =>
        {
            Assert.InRange(vache.Lactation, 0, 6);
            Assert.InRange(vache.BirthDate, DateOnly.MinValue, AujourdHui);
            Assert.True(Enum.IsDefined(vache.ReproductionStatus));
            Assert.True(vache.LastCalving is null || vache.LastCalving <= AujourdHui);
            Assert.True(vache.LastInsemination is null || vache.LastInsemination <= AujourdHui);

            if (vache.Lactation == 0)
            {
                Assert.Null(vache.LastCalving);
                Assert.Null(vache.LastCcs);
            }
            else
            {
                Assert.NotNull(vache.LastCalving);
                Assert.InRange(vache.LastCcs!.Value, 20, 899);
            }
        });
    }

    [Fact]
    public void Generate_FournitDansChaqueElevageLesSituationsARevoir()
    {
        var donnees = DemoData.Generate(AujourdHui, Graine);

        Assert.All(donnees.Locations, elevage =>
        {
            var troupeau = donnees.Cows.Where(vache => vache.LocationId == elevage.Id).ToArray();

            Assert.InRange(troupeau.Count(vache =>
                vache.ReproductionStatus == ReproductiveStatus.Open
                && vache.LastCalving.HasValue
                && AgeEnJours(AujourdHui, vache.LastCalving.Value) <= 30), 4, 8);

            Assert.InRange(troupeau.Count(vache =>
                vache.ReproductionStatus == ReproductiveStatus.Dry
                && vache.LastInsemination.HasValue
                && DateDeVelageAttendue(vache) is >= 0 and <= 30), 4, 8);

            Assert.InRange(troupeau.Count(vache =>
                vache.ReproductionStatus == ReproductiveStatus.Bred
                && vache.LastInsemination.HasValue
                && AgeEnJours(AujourdHui, vache.LastInsemination.Value) is >= 28 and <= 45), 5, 10);

            Assert.InRange(troupeau.Count(vache =>
                vache.ReproductionStatus == ReproductiveStatus.Pregnant
                && DateDeVelageAttendue(vache) is >= 53 and <= 67), 3, 7);

            Assert.InRange(troupeau.Count(vache => vache.LastCcs >= DemoData.HighCcsThreshold), 4, 8);
        });
    }

    [Fact]
    public void Generate_FonctionneAutourDuJourBissextile()
    {
        var jourBissextile = new DateOnly(2024, 2, 29);

        var donnees = DemoData.Generate(jourBissextile, Graine);

        Assert.Equal(DemoData.FarmCount * DemoData.CowsPerFarm, donnees.Cows.Count);
        Assert.All(donnees.Cows, vache =>
        {
            Assert.True(vache.BirthDate <= jourBissextile);
            Assert.True(vache.LastCalving is null || vache.LastCalving <= jourBissextile);
            Assert.True(vache.LastInsemination is null || vache.LastInsemination <= jourBissextile);
        });
    }

    private static int AgeEnJours(DateOnly aujourdHui, DateOnly date)
    {
        return aujourdHui.DayNumber - date.DayNumber;
    }

    private static int DateDeVelageAttendue(Cow vache)
    {
        return vache.LastInsemination is { } insemination
            ? insemination.AddDays(283).DayNumber - AujourdHui.DayNumber
            : int.MinValue;
    }
}
