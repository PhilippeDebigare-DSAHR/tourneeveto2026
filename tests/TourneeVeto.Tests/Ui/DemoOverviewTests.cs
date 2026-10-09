using Bunit;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Components;

namespace TourneeVeto.Tests.Ui;

public sealed class DemoOverviewTests
{
    private static readonly DateOnly AujourdHui = new(2026, 10, 9);

    [Fact]
    public void Rendu_AfficheChaqueElevageAvecSonEffectifEtLeContexteFictif()
    {
        using var contexte = new BunitContext();
        var donnees = DemoData.Generate(AujourdHui, seed: 42);

        var composant = contexte.Render<DemoOverview>(parametres => parametres
            .Add(p => p.Data, donnees));

        var section = composant.Find("section[aria-labelledby='demo-title']");
        Assert.Equal("Élevages de démonstration", section.QuerySelector("h2")?.TextContent);

        var elevagesAffiches = composant.FindAll("li");
        Assert.Equal(donnees.Locations.Count, elevagesAffiches.Count);
        for (var index = 0; index < donnees.Locations.Count; index++)
        {
            var elevage = donnees.Locations[index];
            var texte = elevagesAffiches[index].TextContent;

            Assert.Contains(elevage.Name, texte);
            Assert.Contains(elevage.City, texte);
            Assert.Contains($"{elevage.AnimalCount} vaches", texte);
        }

        Assert.Contains("Toutes les données sont fictives", section.TextContent);
        Assert.Contains("ne remplacent pas le jugement clinique", section.TextContent);
        Assert.Contains("restent à implémenter", section.TextContent);
    }

    [Fact]
    public void Rendu_EchappeLeContenuFourniCommeTexte()
    {
        using var contexte = new BunitContext();
        const string nomElevage = "<script>alert('x')</script>";
        const string ville = "<img src=x onerror=alert(1)>";
        var donnees = new DemoDataSet(
            [new Location(Guid.NewGuid(), nomElevage, ville, AnimalCount: 1)],
            []);

        var composant = contexte.Render<DemoOverview>(parametres => parametres
            .Add(p => p.Data, donnees));

        Assert.Empty(composant.FindAll("script"));
        Assert.Empty(composant.FindAll("img"));
        Assert.Contains(nomElevage, composant.Find("li").TextContent);
        Assert.Contains(ville, composant.Find("li").TextContent);
    }

    [Fact]
    public void Rendu_AfficheUneListeVideSansInventerDelevage()
    {
        using var contexte = new BunitContext();
        var donnees = new DemoDataSet([], []);

        var composant = contexte.Render<DemoOverview>(parametres => parametres
            .Add(p => p.Data, donnees));

        Assert.Empty(composant.FindAll("li"));
        Assert.Contains("Toutes les données sont fictives", composant.Find("section").TextContent);
    }
}
