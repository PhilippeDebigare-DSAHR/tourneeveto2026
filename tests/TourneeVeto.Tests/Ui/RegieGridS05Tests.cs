using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Herd;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

/// <summary>S05 : grille de régie en lecture seule (dépôt simulé, temps figé, seed fixe, pas d'IndexedDB).</summary>
public sealed class RegieGridS05Tests
{
    private static readonly DateOnly Aujourdhui = new(2026, 10, 9);
    private static readonly DemoDataSet Troupeau = DemoData.Generate(Aujourdhui, 42);
    private static readonly Guid FarmA = Troupeau.Locations[0].Id;
    private static readonly Guid FarmB = Troupeau.Locations[1].Id;
    private static readonly Cow VacheA1 = Troupeau.Cows.First(c => c.LocationId == FarmA);
    private static readonly Cow VacheA2 = Troupeau.Cows.Where(c => c.LocationId == FarmA).Skip(1).First();
    private static readonly Cow VacheB = Troupeau.Cows.First(c => c.LocationId == FarmB);
    private static readonly Guid VisiteId = Guid.Parse("0a000000-0000-0000-0000-000000000001");
    private static readonly Guid AutreVisiteId = Guid.Parse("0a000000-0000-0000-0000-000000000002");

    private static readonly string[] Categories =
        ["Calving", "DryOff", "Insemination", "PregnancyDiagnosis", "HighSomaticCellCount"];

    private static int _compteur;

    private static VisitAction Action(Cow vache, ActionType type, bool faite = false, string notes = "Note fictive") =>
        new(Guid.Parse($"0c000000-0000-0000-0000-{Interlocked.Increment(ref _compteur):D12}"),
            vache.Id, type, Aujourdhui, faite, notes);

    private static Visit Visite(Guid id, Guid farm, params VisitAction[] actions) =>
        new(id, farm, Aujourdhui, "Cause fictive", "Notes fictives", null) { Actions = actions };

    private static BunitContext Contexte(FakeRepo depot)
    {
        var contexte = new BunitContext();
        contexte.Services.AddLogging();
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(depot);
        return contexte;
    }

    private static IRenderedComponent<RegieGrid> Afficher(BunitContext contexte, Guid visitId) =>
        contexte.Render<RegieGrid>(p => p.Add(c => c.VisitId, visitId));

    private static AngleSharp.Dom.IElement Categorie(IRenderedComponent<RegieGrid> c, string type) =>
        c.Find($"[data-testid='regie-category'][data-category='{type}']");

    [Fact]
    public async Task Grille_AfficheToujoursLesCinqCategoriesDansLOrdre_MemeSansAction()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, FarmA)] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.Equal(5, composant.FindAll("[data-testid='regie-category']").Count));
        Assert.Equal(Categories, composant.FindAll("[data-testid='regie-category']").Select(e => e.GetAttribute("data-category")));
        Assert.Empty(composant.FindAll("[data-testid='regie-action']"));
        Assert.Equal(5, composant.FindAll(".grid__empty").Count);
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.Contains("Vêlage (0)", composant.Markup);
        Assert.Contains("Tarissement (0)", composant.Markup);
        Assert.Contains("Insémination (0)", composant.Markup);
        Assert.Contains("Diagnostic de gestation (0)", composant.Markup);
        Assert.Contains("CCS élevée (0)", composant.Markup);
    }

    [Fact]
    public async Task Grille_ActionsAfficheesDansLaBonneCategorieAvecLaBonneVacheEtSonIdentifiant()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(VisiteId, FarmA,
                    Action(VacheA1, ActionType.Calving),
                    Action(VacheA2, ActionType.DryOff),
                    Action(VacheA1, ActionType.Insemination, faite: true, notes: "Fait fictif"),
                    Action(VacheA2, ActionType.PregnancyDiagnosis),
                    Action(VacheA1, ActionType.HighSomaticCellCount))
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.Equal(5, composant.FindAll("[data-testid='regie-action']").Count));
        var attendu = new Dictionary<string, Cow>
        {
            ["Calving"] = VacheA1, ["DryOff"] = VacheA2, ["Insemination"] = VacheA1,
            ["PregnancyDiagnosis"] = VacheA2, ["HighSomaticCellCount"] = VacheA1,
        };
        foreach (var (type, vache) in attendu)
        {
            var ligne = Assert.Single(Categorie(composant, type).QuerySelectorAll("[data-testid='regie-action']"));
            Assert.Equal(vache.Id.ToString(), ligne.GetAttribute("data-cow-id"));
            Assert.Contains(vache.Name, ligne.TextContent);
            Assert.Contains($"Identifiant : {vache.Id}", ligne.TextContent);
            Assert.Contains("(1)", Categorie(composant, type).QuerySelector("h2")!.TextContent);
        }

        var insemination = Categorie(composant, "Insemination");
        Assert.True(insemination.QuerySelector("[data-testid='regie-completed']")!.HasAttribute("checked"));
        Assert.Contains("Statut : action réalisée", insemination.TextContent);
        var noteInsemination = insemination.QuerySelector("textarea[data-testid='regie-notes']")!;
        Assert.Equal("Fait fictif", noteInsemination.GetAttribute("value") ?? noteInsemination.TextContent);

        var velage = Categorie(composant, "Calving");
        Assert.False(velage.QuerySelector("[data-testid='regie-completed']")!.HasAttribute("checked"));
        Assert.Contains("Statut : action à faire", velage.TextContent);
        var noteVelage = velage.QuerySelector("textarea[data-testid='regie-notes']")!;
        Assert.Equal("Note fictive", noteVelage.GetAttribute("value") ?? noteVelage.TextContent);
    }

    [Fact]
    public async Task Grille_CategoriesSansActionRestentVidesQuandUneSeuleEstRenseignee()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, FarmA, Action(VacheA1, ActionType.DryOff))] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        Assert.Equal(5, composant.FindAll("[data-testid='regie-category']").Count);
        Assert.Single(Categorie(composant, "DryOff").QuerySelectorAll("li"));
        foreach (var type in Categories.Where(t => t != "DryOff"))
        {
            Assert.Empty(Categorie(composant, type).QuerySelectorAll("li"));
            Assert.Contains("Aucune vache à traiter", Categorie(composant, type).TextContent);
        }
    }

    [Fact]
    public async Task Grille_ExclutLesActionsDUneAutreVisiteEtLesVachesDUneAutreFerme()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(VisiteId, FarmA, Action(VacheA1, ActionType.Calving), Action(VacheB, ActionType.Calving)),
                Visite(AutreVisiteId, FarmA, Action(VacheA2, ActionType.Calving)),
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-action']")));
        var ligne = Assert.Single(composant.FindAll("[data-testid='regie-action']"));
        Assert.Equal(VacheA1.Id.ToString(), ligne.GetAttribute("data-cow-id"));
        Assert.DoesNotContain(VacheA2.Id.ToString(), composant.Markup);
        Assert.DoesNotContain(VacheB.Id.ToString(), composant.Markup);
    }

    [Fact]
    public async Task Grille_ActionOrphelineEstAvertieEtExclue()
    {
        var inconnue = VacheA1 with { Id = Guid.Parse("0f000000-0000-0000-0000-0000000000aa"), Name = "Fantome fictive" };
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(VisiteId, FarmA,
                    Action(VacheA1, ActionType.Calving),
                    Action(inconnue, ActionType.Calving),
                    Action(VacheB, ActionType.DryOff))
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-ignored']")));
        Assert.Contains("2 action(s)", composant.Find("[data-testid='regie-ignored']").TextContent);
        Assert.Equal("alert", composant.Find("[data-testid='regie-ignored']").GetAttribute("role"));
        var ligne = Assert.Single(composant.FindAll("[data-testid='regie-action']"));
        Assert.Equal(VacheA1.Id.ToString(), ligne.GetAttribute("data-cow-id"));
        Assert.DoesNotContain("Fantome fictive", composant.Markup);
        Assert.Empty(composant.FindAll("[data-testid='regie-error']"));
    }

    [Fact]
    public async Task Grille_SansActionOrpheline_NAffichePasDAvertissement()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, FarmA, Action(VacheA1, ActionType.Calving))] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-action']")));
        Assert.Empty(composant.FindAll("[data-testid='regie-ignored']"));
    }

    [Fact]
    public async Task Grille_AfficheElevageEtDateDeLaVisite()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, FarmA)] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.Contains(Troupeau.Locations[0].Name, composant.Markup));
        Assert.Equal("2026-10-09", composant.Find("time").GetAttribute("datetime"));
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    [Fact]
    public void Route_EstRegieParVisitId()
    {
        var route = typeof(RegieGrid).GetCustomAttributes<RouteAttribute>().Single();

        Assert.Equal("/regie/{VisitId:guid}", route.Template);
    }

    [Fact]
    public async Task Tournee_LienVersLaGrilleParVisitId()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits = [Visite(VisiteId, FarmA), Visite(AutreVisiteId, FarmB)]
        };
        await using var contexte = Contexte(depot);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Equal(2, composant.FindAll("a[data-visit-id]").Count));
        foreach (var id in new[] { VisiteId, AutreVisiteId })
        {
            var lien = composant.Find($"a[data-visit-id='{id}']");
            Assert.Equal($"regie/{id}", lien.GetAttribute("href"));
            Assert.Contains("Ouvrir la grille de régie", lien.TextContent);
        }
    }

    [Fact]
    public async Task Grille_VisiteIntrouvable_AfficheErreurSansListeVideDeFauxSucces()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, FarmA, Action(VacheA1, ActionType.Calving))] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, Guid.Parse("0a000000-0000-0000-0000-0000000000ff"));

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-not-found']")));
        AssertAucuneListe(composant);
    }

    [Fact]
    public async Task Grille_VisitIdVide_AfficheVisiteIntrouvable()
    {
        var depot = new FakeRepo { Herd = Troupeau };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, Guid.Empty);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-not-found']")));
        AssertAucuneListe(composant);
    }

    [Fact]
    public async Task Grille_TroupeauAbsent_AfficheErreurSansListeVideDeFauxSucces()
    {
        var depot = new FakeRepo { Herd = null, Visits = [Visite(VisiteId, FarmA)] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-error']")));
        Assert.Contains("troupeau de démonstration est absent", composant.Find("[data-testid='regie-error']").TextContent);
        AssertAucuneListe(composant);
    }

    [Fact]
    public async Task Grille_ElevageIntrouvable_AfficheErreurSansListeVideDeFauxSucces()
    {
        var elevageInconnu = Guid.Parse("0f000000-0000-0000-0000-0000000000ff");
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(VisiteId, elevageInconnu)] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-error']")));
        Assert.Contains("élevage de cette visite est introuvable", composant.Find("[data-testid='regie-error']").TextContent);
        AssertAucuneListe(composant);
    }

    [Theory]
    [InlineData(VisitStorageError.Unavailable, "le stockage local est indisponible ou refusé")]
    [InlineData(VisitStorageError.QuotaExceeded, "le quota de stockage local est dépassé")]
    [InlineData(VisitStorageError.InvalidData, "les données locales sont illisibles")]
    public async Task Grille_ErreurDeDepotSurLaVisite_AfficheMessageFixeSansFauxSucces(VisitStorageError code, string attendu)
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            GetFailure = new VisitStorageException(code, "msg", "SECRET-DETAIL-1234")
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-error']")));
        Assert.Contains(attendu, composant.Find("[data-testid='regie-error']").TextContent);
        Assert.DoesNotContain("SECRET-DETAIL", composant.Markup);
        AssertAucuneListe(composant);
    }

    [Fact]
    public async Task Grille_ErreurDeDepotSurLeTroupeau_AfficheErreurSansFauxSucces()
    {
        var depot = new FakeRepo
        {
            Visits = [Visite(VisiteId, FarmA)],
            HerdFailure = new VisitStorageException(VisitStorageError.Unavailable, "msg", "SECRET-DETAIL-1234")
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, VisiteId);

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-error']")));
        Assert.DoesNotContain("SECRET-DETAIL", composant.Markup);
        AssertAucuneListe(composant);
    }

    [Fact]
    public async Task Grille_ReessayerApresErreur_AfficheLaGrilleQuandLeDepotRepond()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits = [Visite(VisiteId, FarmA, Action(VacheA1, ActionType.Calving))],
            GetFailure = new VisitStorageException(VisitStorageError.Unavailable, "msg")
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-error']")));

        depot.GetFailure = null;
        composant.Find("button.grid__action").Click();

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        Assert.Empty(composant.FindAll("[data-testid='regie-error']"));
    }

    private static Visit VisiteAvecVache(Guid id, Cow vache) =>
        Visite(id, vache.LocationId, Action(vache, ActionType.Calving));

    [Fact]
    public async Task Grille_ChangementDeVisitIdPendantLeChargement_AncienSuccesNEcrasePasLaNouvelleVisite()
    {
        var ancienne = new TaskCompletionSource<Visit?>();
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            GetHandler = id => id == VisiteId
                ? ancienne.Task
                : Task.FromResult<Visit?>(VisiteAvecVache(AutreVisiteId, VacheB))
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Assert.Equal("true", composant.Find("section.grid").GetAttribute("aria-busy"));

        composant.Render(p => p.Add(c => c.VisitId, AutreVisiteId));
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        ancienne.SetResult(VisiteAvecVache(VisiteId, VacheA1));
        await Task.Delay(50);

        var ligne = Assert.Single(composant.FindAll("[data-testid='regie-action']"));
        Assert.Equal(VacheB.Id.ToString(), ligne.GetAttribute("data-cow-id"));
        Assert.Contains(Troupeau.Locations[1].Name, composant.Markup);
        Assert.DoesNotContain(VacheA1.Id.ToString(), composant.Markup);
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    [Fact]
    public async Task Grille_ChangementDeVisitIdPendantLeChargement_AncienneErreurNEcrasePasLaNouvelleVisite()
    {
        var ancienne = new TaskCompletionSource<Visit?>();
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            GetHandler = id => id == VisiteId
                ? ancienne.Task
                : Task.FromResult<Visit?>(VisiteAvecVache(AutreVisiteId, VacheB))
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        composant.Render(p => p.Add(c => c.VisitId, AutreVisiteId));
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        ancienne.SetException(new VisitStorageException(VisitStorageError.Unavailable, "msg", "SECRET-DETAIL-1234"));
        await Task.Delay(50);

        Assert.Single(composant.FindAll("[data-testid='regie-action']"));
        Assert.Empty(composant.FindAll("[data-testid='regie-error']"));
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.DoesNotContain("SECRET-DETAIL", composant.Markup);
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    [Fact]
    public async Task Grille_AncienResultatArriveAvantLeNouveau_LeChargementResteActifPuisLaNouvelleVisiteSAffiche()
    {
        var ancienne = new TaskCompletionSource<Visit?>();
        var nouvelle = new TaskCompletionSource<Visit?>();
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            GetHandler = id => id == VisiteId ? ancienne.Task : nouvelle.Task
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        composant.Render(p => p.Add(c => c.VisitId, AutreVisiteId));

        ancienne.SetResult(VisiteAvecVache(VisiteId, VacheA1));
        await Task.Delay(50);

        Assert.Equal("true", composant.Find("section.grid").GetAttribute("aria-busy"));
        Assert.Contains("Chargement", composant.Markup);
        Assert.Empty(composant.FindAll("[data-testid='regie-action']"));
        Assert.Empty(composant.FindAll("[role='alert']"));

        nouvelle.SetResult(VisiteAvecVache(AutreVisiteId, VacheB));

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        Assert.Equal(VacheB.Id.ToString(), composant.Find("[data-testid='regie-action']").GetAttribute("data-cow-id"));
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    [Fact]
    public async Task Grille_AncienneErreurArriveAvantLeNouveau_LeChargementResteActifSansAlerte()
    {
        var ancienne = new TaskCompletionSource<Visit?>();
        var nouvelle = new TaskCompletionSource<Visit?>();
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            GetHandler = id => id == VisiteId ? ancienne.Task : nouvelle.Task
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        composant.Render(p => p.Add(c => c.VisitId, AutreVisiteId));

        ancienne.SetException(new VisitStorageException(VisitStorageError.QuotaExceeded, "msg"));
        await Task.Delay(50);

        Assert.Equal("true", composant.Find("section.grid").GetAttribute("aria-busy"));
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.Empty(composant.FindAll("[data-testid='regie-error']"));

        nouvelle.SetResult(VisiteAvecVache(AutreVisiteId, VacheB));

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        Assert.Empty(composant.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task Grille_ChangementDeVisitIdPendantLaLectureDuTroupeau_AncienResultatEtAncienneErreurSontIgnores()
    {
        var ancienTroupeau = new TaskCompletionSource<DemoDataSet?>();
        var premierAppel = true;
        var depot = new FakeRepo
        {
            Visits = [Visite(VisiteId, FarmA, Action(VacheA1, ActionType.Calving)), VisiteAvecVache(AutreVisiteId, VacheB)],
            HerdHandler = () => premierAppel ? ancienTroupeau.Task : Task.FromResult<DemoDataSet?>(Troupeau)
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        premierAppel = false;
        composant.Render(p => p.Add(c => c.VisitId, AutreVisiteId));
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-action']")));
        ancienTroupeau.SetException(new VisitStorageException(VisitStorageError.Unavailable, "msg"));
        await Task.Delay(50);

        Assert.Equal(VacheB.Id.ToString(), composant.Find("[data-testid='regie-action']").GetAttribute("data-cow-id"));
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    private static void AssertAucuneListe(IRenderedComponent<RegieGrid> composant)
    {
        Assert.Empty(composant.FindAll("[data-testid='regie-category']"));
        Assert.Empty(composant.FindAll("[data-testid='regie-action']"));
        Assert.Empty(composant.FindAll(".grid__empty"));
        Assert.NotEmpty(composant.FindAll("[role='alert']"));
        Assert.Equal("false", composant.Find("section.grid").GetAttribute("aria-busy"));
    }

    private sealed class FakeRepo : IVisitRepository
    {
        public DemoDataSet? Herd { get; set; }
        public List<Visit> Visits { get; set; } = [];
        public Exception? GetFailure { get; set; }
        public Exception? HerdFailure { get; set; }

        public Func<Guid, Task<Visit?>>? GetHandler { get; set; }
        public Func<Task<DemoDataSet?>>? HerdHandler { get; set; }

        public Task<DemoDataSet?> GetHerdAsync() =>
            HerdHandler is not null ? HerdHandler()
            : HerdFailure is not null ? Task.FromException<DemoDataSet?>(HerdFailure) : Task.FromResult(Herd);

        public Task<IReadOnlyList<Visit>> GetAllAsync() => Task.FromResult<IReadOnlyList<Visit>>(Visits);

        public Task<Visit?> GetAsync(Guid id) =>
            GetHandler is not null ? GetHandler(id) :
            GetFailure is not null
                ? Task.FromException<Visit?>(GetFailure)
                : Task.FromResult(Visits.FirstOrDefault(v => v.Id == id));

        public Task InitializeDemoAsync(DemoSeed seed) => throw new NotSupportedException();
        public Task SaveAsync(Visit visit, VisitPhoto? photo = null) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task SaveHerdAsync(DemoDataSet herd) => throw new NotSupportedException();
        public Task<VisitPhoto?> GetPhotoAsync(Guid photoId) => throw new NotSupportedException();
        public Task SavePhotoAsync(Guid visitId, VisitPhoto photo) => throw new NotSupportedException();
        public Task DeletePhotoAsync(Guid visitId) => throw new NotSupportedException();
    }
}
