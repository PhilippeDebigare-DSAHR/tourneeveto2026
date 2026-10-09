using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Herd;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

/// <summary>S04 : branchement de FarmPreviousVisitsSummary dans FarmSheet (dépôt simulé, temps figé, seed fixe).</summary>
public sealed class FarmSheetS04Tests
{
    private static readonly DateOnly Aujourdhui = new(2026, 10, 9);
    private static readonly DemoDataSet Troupeau = DemoData.Generate(Aujourdhui, 42);
    private static readonly Guid FarmA = Troupeau.Locations[0].Id;
    private static readonly Guid FarmB = Troupeau.Locations[1].Id;
    private static readonly Guid FarmInconnue = Guid.Parse("0f000000-0000-0000-0000-0000000000ff");

    private static Visit Visite(int n, Guid farm, DateOnly date, params VisitAction[] actions) =>
        new(Guid.Parse($"0a000000-0000-0000-0000-{n:D12}"), farm, date, "Cause fictive", "Notes fictives", null)
        { Actions = actions };

    private static VisitAction Action(int n, bool faite, DateOnly date, ActionType type = ActionType.Insemination) =>
        new(Guid.Parse($"0c000000-0000-0000-0000-{n:D12}"), Troupeau.Cows[0].Id, type, date, faite, "Note fictive");

    private static BunitContext Contexte(FakeRepo depot, FakeTimeProvider? temps = null)
    {
        var contexte = new BunitContext();
        contexte.Services.AddSingleton<TimeProvider>(temps ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(depot);
        return contexte;
    }

    private static IRenderedComponent<FarmSheet> Afficher(BunitContext contexte, Guid farmId) =>
        contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, farmId));

    [Fact]
    public async Task Summary_PourLeBonElevage_ContientFarmIdEtDateDuJour()
    {
        var depot = new FakeRepo { Herd = Troupeau };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        Assert.Equal(FarmA, composant.Instance.Summary!.FarmId);
        Assert.Equal(Aujourdhui, composant.Instance.Summary.ReferenceDate);
        Assert.Empty(composant.Instance.Summary.PreviousVisits);
        Assert.Empty(composant.Instance.Summary.PendingActions);
    }

    [Fact]
    public async Task Summary_DateLocale_UtiliseLeFuseauLocalEtNonLaDateUtc()
    {
        var depot = new FakeRepo { Herd = Troupeau };
        var temps = new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero));
        temps.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test-4", TimeSpan.FromHours(-4), "Test-4", "Test-4"));
        await using var contexte = Contexte(depot, temps);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        Assert.Equal(Aujourdhui, composant.Instance.Summary!.ReferenceDate);
    }

    [Fact]
    public async Task Summary_VisiteDeLaVeille_EstPrecedenteEtVisiteDuJourEstExclue()
    {
        var veille = Aujourdhui.AddDays(-1);
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(1, FarmA, veille),
                Visite(2, FarmA, Aujourdhui),
                Visite(3, FarmA, Aujourdhui.AddDays(1)),
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        var visite = Assert.Single(composant.Instance.Summary!.PreviousVisits);
        Assert.Equal(veille, visite.Date);
        Assert.Equal(Guid.Parse("0a000000-0000-0000-0000-000000000001"), visite.Id);
    }

    [Fact]
    public async Task Summary_VisiteDeLaVeilleLocale_EstPrecedenteQuandLeJourUtcAvance()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(1, FarmA, Aujourdhui), Visite(2, FarmA, Aujourdhui.AddDays(-1))] };
        var temps = new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero));
        temps.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test-4", TimeSpan.FromHours(-4), "Test-4", "Test-4"));
        await using var contexte = Contexte(depot, temps);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        var visite = Assert.Single(composant.Instance.Summary!.PreviousVisits);
        Assert.Equal(Aujourdhui.AddDays(-1), visite.Date);
    }

    [Fact]
    public async Task Summary_VisitesDUnAutreElevage_SontExclues()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(1, FarmA, Aujourdhui.AddDays(-5)),
                Visite(2, FarmB, Aujourdhui.AddDays(-3), Action(2, false, Aujourdhui)),
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        var visite = Assert.Single(composant.Instance.Summary!.PreviousVisits);
        Assert.Equal(FarmA, visite.FarmId);
        Assert.Empty(composant.Instance.Summary.PendingActions);
    }

    [Fact]
    public async Task Summary_VisitesEtActions_OrdreEtActionsNonRealiseesSeulement()
    {
        var recente = Aujourdhui.AddDays(-2);
        var ancienne = Aujourdhui.AddDays(-30);
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                Visite(1, FarmA, ancienne, Action(1, false, ancienne)),
                Visite(2, FarmA, recente, Action(2, true, recente), Action(3, false, recente, ActionType.DryOff)),
            ]
        };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));
        var synthese = composant.Instance.Summary!;
        Assert.Equal([recente, ancienne], synthese.PreviousVisits.Select(v => v.Date));
        Assert.Equal(2, synthese.PendingActions.Count);
        Assert.All(synthese.PendingActions, a => Assert.False(a.Action.IsCompleted));
        Assert.Equal(Guid.Parse("0a000000-0000-0000-0000-000000000002"), synthese.PendingActions[0].VisitId);
        Assert.Equal(Guid.Parse("0c000000-0000-0000-0000-000000000003"), synthese.PendingActions[0].Action.Id);
        Assert.Equal(Guid.Parse("0a000000-0000-0000-0000-000000000001"), synthese.PendingActions[1].VisitId);
    }

    [Fact]
    public async Task Summary_ElevageIntrouvable_RestNullAvecAlerte()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(1, FarmInconnue, Aujourdhui.AddDays(-1))] };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmInconnue);

        composant.WaitForAssertion(() => Assert.Contains("Fiche introuvable", composant.Find("[role='alert']").TextContent));
        Assert.Null(composant.Instance.Summary);
        Assert.Empty(composant.FindAll("[data-testid='farm-name']"));
    }

    [Fact]
    public async Task Summary_TroupeauAbsent_RestNullAvecAlerte()
    {
        var depot = new FakeRepo { Herd = null };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.Contains("Fiche introuvable", composant.Find("[role='alert']").TextContent));
        Assert.Null(composant.Instance.Summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_ErreurDuDepot_RestNullAvecMessageFixe(bool surGetAll)
    {
        var erreur = new VisitStorageException(VisitStorageError.Unavailable, "brut", "SECRET-DETAIL");
        var depot = new FakeRepo { Herd = Troupeau, HerdFailure = surGetAll ? null : erreur, GetAllFailure = surGetAll ? erreur : null };
        await using var contexte = Contexte(depot);

        var composant = Afficher(contexte, FarmA);

        composant.WaitForAssertion(() => Assert.Contains("indisponible", composant.Find("[role='alert']").TextContent));
        Assert.Null(composant.Instance.Summary);
        Assert.DoesNotContain("SECRET-DETAIL", composant.Markup);
        Assert.Empty(composant.FindAll("[data-testid='farm-name']"));
    }

    [Fact]
    public async Task Summary_ChangementDeFarmId_RechargeLaSyntheseDuNouvelElevage()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits = [Visite(1, FarmA, Aujourdhui.AddDays(-1)), Visite(2, FarmB, Aujourdhui.AddDays(-2)), Visite(3, FarmB, Aujourdhui.AddDays(-4))]
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, FarmA);
        composant.WaitForAssertion(() => Assert.Equal(FarmA, composant.Instance.Summary?.FarmId));

        composant.Render(p => p.Add(c => c.FarmId, FarmB));

        composant.WaitForAssertion(() => Assert.Equal(FarmB, composant.Instance.Summary?.FarmId));
        Assert.Equal(2, composant.Instance.Summary!.PreviousVisits.Count);
        Assert.All(composant.Instance.Summary.PreviousVisits, v => Assert.Equal(FarmB, v.FarmId));
    }

    [Fact]
    public async Task Summary_ChangementVersElevageIntrouvable_EffaceLaSynthesePrecedente()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(1, FarmA, Aujourdhui.AddDays(-1))] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, FarmA);
        composant.WaitForAssertion(() => Assert.NotNull(composant.Instance.Summary));

        composant.Render(p => p.Add(c => c.FarmId, FarmInconnue));

        composant.WaitForAssertion(() => Assert.Contains("Fiche introuvable", composant.Find("[role='alert']").TextContent));
        Assert.Null(composant.Instance.Summary);
    }

    [Fact]
    public async Task Summary_ChargementAncienTermineApresLeNouveau_NeRemplacePasLaSynthese()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(1, FarmA, Aujourdhui.AddDays(-1)), Visite(2, FarmB, Aujourdhui.AddDays(-2))] };
        var attenteA = new TaskCompletionSource<IReadOnlyList<Visit>>();
        var attenteB = new TaskCompletionSource<IReadOnlyList<Visit>>();
        depot.GetAllHandler = () => depot.GetAllCalls == 1 ? attenteA.Task : attenteB.Task;
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, FarmA);
        composant.WaitForAssertion(() => Assert.Equal(1, depot.GetAllCalls));

        composant.Render(p => p.Add(c => c.FarmId, FarmB));
        composant.WaitForAssertion(() => Assert.Equal(2, depot.GetAllCalls));
        attenteB.SetResult(depot.Visits);
        composant.WaitForAssertion(() => Assert.Equal(FarmB, composant.Instance.Summary?.FarmId));
        attenteA.SetResult(depot.Visits);
        await Task.Delay(100);

        Assert.Equal(FarmB, composant.Instance.Summary?.FarmId);
        Assert.Equal("farm-name", composant.Find("[data-testid='farm-name']").GetAttribute("data-testid"));
        Assert.Equal(FarmB.ToString(), composant.Find("[data-testid='farm-id']").TextContent);
    }

    [Fact]
    public async Task Summary_ChargementAncienTermineAvantLeNouveau_RestNullTantQueLeNouveauEstEnCours()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [Visite(1, FarmA, Aujourdhui.AddDays(-1)), Visite(2, FarmB, Aujourdhui.AddDays(-2))] };
        var attenteA = new TaskCompletionSource<IReadOnlyList<Visit>>();
        var attenteB = new TaskCompletionSource<IReadOnlyList<Visit>>();
        depot.GetAllHandler = () => depot.GetAllCalls == 1 ? attenteA.Task : attenteB.Task;
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, FarmA);
        composant.WaitForAssertion(() => Assert.Equal(1, depot.GetAllCalls));

        composant.Render(p => p.Add(c => c.FarmId, FarmB));
        composant.WaitForAssertion(() => Assert.Equal(2, depot.GetAllCalls));
        attenteA.SetResult(depot.Visits);
        await Task.Delay(100);

        Assert.Null(composant.Instance.Summary);
        Assert.NotEmpty(composant.FindAll("[role='status']"));

        attenteB.SetResult(depot.Visits);
        composant.WaitForAssertion(() => Assert.Equal(FarmB, composant.Instance.Summary?.FarmId));
    }

    // ---------- Rendu final (HTML) ----------

    private static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
    private static string Norm(string s) => System.Text.RegularExpressions.Regex.Replace(s.Replace('\u00A0', ' ').Replace('\u202F', ' '), @"\s+", " ").Trim();
    private static string Jour(DateOnly d) => d.ToString("d MMMM yyyy", Fr);
    private static Guid CowA => Troupeau.Cows.First(c => c.LocationId == FarmA).Id;
    private static string CowAName => Troupeau.Cows.First(c => c.LocationId == FarmA).Name;
    private static Cow CowB => Troupeau.Cows.First(c => c.LocationId == FarmB);

    private static VisitAction ActionVache(int n, Guid cowId, bool faite, DateOnly date, ActionType type, string note) =>
        new(Guid.Parse($"0c000000-0000-0000-0000-{n:D12}"), cowId, type, date, faite, note);

    private static Visit VisiteDetail(int n, Guid farm, DateOnly date, string motif, string notes, params VisitAction[] actions) =>
        new(Guid.Parse($"0a000000-0000-0000-0000-{n:D12}"), farm, date, motif, notes, null) { Actions = actions };

    private static DemoDataSet TroupeauAvecEffectif(int effectif) =>
        Troupeau with
        {
            Locations = Troupeau.Locations.Select(l => l.Id == FarmA ? l with { AnimalCount = effectif } : l).ToArray()
        };

    private static IRenderedComponent<FarmSheet> Rendu(FakeRepo depot, out BunitContext contexte, Guid? farm = null)
    {
        contexte = Contexte(depot);
        var composant = Afficher(contexte, farm ?? FarmA);
        composant.WaitForAssertion(() => Assert.Empty(composant.FindAll("[role='status']")));
        return composant;
    }

    [Theory]
    [InlineData(87, "87 animaux")]
    [InlineData(1234, "1 234 animaux")]
    [InlineData(1, "1 animal")]
    [InlineData(0, "0 animal")]
    public void Rendu_Effectif_AfficheLeNombreExactDeLaFiche(int effectif, string attendu)
    {
        var depot = new FakeRepo { Herd = TroupeauAvecEffectif(effectif) };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        // Le séparateur de milliers dépend des données de culture du runtime : seul le nombre exact est vérifié.
        Assert.Equal(attendu.Replace(" ", ""), Norm(composant.Find("[data-testid='farm-animal-count']").TextContent).Replace(" ", ""));
        Assert.Equal("Effectif", composant.Find("[data-testid='farm-herd'] h3").TextContent);
    }

    [Fact]
    public void Rendu_ToutesLesVisitesAnterieures_AfficheDateMotifEtNotes()
    {
        var d1 = Aujourdhui.AddDays(-3);
        var d2 = Aujourdhui.AddDays(-40);
        var d3 = Aujourdhui.AddDays(-400);
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                VisiteDetail(3, FarmA, d3, "Motif C", "Notes C"),
                VisiteDetail(1, FarmA, d1, "Motif A", "Notes A"),
                VisiteDetail(2, FarmA, d2, "Motif B", "Notes B"),
            ]
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        var items = composant.FindAll("[data-testid='farm-previous-visit']");
        Assert.Equal(3, items.Count);
        var attendus = new[] { (d1, "Motif A", "Notes A"), (d2, "Motif B", "Notes B"), (d3, "Motif C", "Notes C") };
        for (var i = 0; i < 3; i++)
        {
            var texte = Norm(items[i].TextContent);
            Assert.Contains(Jour(attendus[i].Item1), texte);
            Assert.Contains($"Motif : {attendus[i].Item2}", texte);
            Assert.Contains($"Notes : {attendus[i].Item3}", texte);
            Assert.Equal(attendus[i].Item1.ToString("yyyy-MM-dd"), items[i].QuerySelector("time")!.GetAttribute("datetime"));
        }
        Assert.Empty(composant.FindAll("[data-testid='farm-previous-visits-empty']"));
    }

    [Fact]
    public void Rendu_MotifEtNotesVides_AfficheRepliSansLigneNotes()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), " ", "")] };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        var texte = Norm(composant.Find("[data-testid='farm-previous-visit']").TextContent);
        Assert.Contains("Motif : Non renseigné", texte);
        Assert.DoesNotContain("Notes :", texte);
    }

    [Fact]
    public void Rendu_VisitesDuJourFuturesEtAutreElevage_NeSontPasAffichees()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                VisiteDetail(1, FarmA, Aujourdhui.AddDays(-2), "Motif passé", "Notes passées"),
                VisiteDetail(2, FarmA, Aujourdhui, "Motif du jour", "Notes du jour"),
                VisiteDetail(3, FarmA, Aujourdhui.AddDays(5), "Motif futur", "Notes futures"),
                VisiteDetail(4, FarmB, Aujourdhui.AddDays(-2), "Motif autre ferme", "Notes autre ferme",
                    ActionVache(4, CowB.Id, false, Aujourdhui.AddDays(1), ActionType.DryOff, "Note autre ferme")),
            ]
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        Assert.Single(composant.FindAll("[data-testid='farm-previous-visit']"));
        var markup = composant.Markup;
        Assert.Contains("Motif passé", markup);
        foreach (var interdit in new[] { "Motif du jour", "Notes du jour", "Motif futur", "Notes futures", "Motif autre ferme", "Note autre ferme", CowB.Name })
        {
            Assert.DoesNotContain(interdit, markup);
        }
        Assert.Empty(composant.FindAll("[data-testid='farm-pending-action']"));
        Assert.Single(composant.FindAll("[data-testid='farm-pending-actions-empty']"));
    }

    [Fact]
    public void Rendu_ActionsEnAttente_AffichentTypeNomVacheDatesEtNote_ExcluentLesRealisees()
    {
        var visite = Aujourdhui.AddDays(-10);
        var prevue = Aujourdhui.AddDays(7);
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                VisiteDetail(1, FarmA, visite, "Motif", "Notes",
                    ActionVache(1, CowA, false, prevue, ActionType.DryOff, "Note tarissement"),
                    ActionVache(2, CowA, true, prevue, ActionType.Calving, "Note réalisée")),
                // Visite du jour : ses actions en attente ne sont pas des actions « préalables ».
                VisiteDetail(2, FarmA, Aujourdhui, "Motif jour", "Notes jour",
                    ActionVache(3, CowA, false, prevue, ActionType.PregnancyDiagnosis, "Note du jour")),
                VisiteDetail(3, FarmB, visite, "Autre", "Autre",
                    ActionVache(4, CowB.Id, false, prevue, ActionType.Insemination, "Note autre ferme")),
            ]
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        var action = Assert.Single(composant.FindAll("[data-testid='farm-pending-action']"));
        var texte = Norm(action.TextContent);
        Assert.Contains("Tarissement", texte);
        Assert.Equal(CowAName, Norm(action.QuerySelector("[data-testid='farm-pending-action-cow']")!.TextContent).Replace("Vache : ", ""));
        Assert.Contains($"Visite du {Jour(visite)}", texte);
        Assert.Contains($"Action prévue le {Jour(prevue)}", texte);
        Assert.Contains("Note : Note tarissement", texte);
        var times = action.QuerySelectorAll("time").Select(t => t.GetAttribute("datetime") ?? "").ToArray();
        Assert.Equal([visite.ToString("yyyy-MM-dd"), prevue.ToString("yyyy-MM-dd")], times);
        foreach (var interdit in new[] { "Note réalisée", "Vêlage", "Note du jour", "Diagnostic de gestation", "Note autre ferme", "Insémination" })
        {
            Assert.DoesNotContain(interdit, composant.Markup);
        }
    }

    [Theory]
    [InlineData(ActionType.PregnancyDiagnosis, "Diagnostic de gestation")]
    [InlineData(ActionType.HighSomaticCellCount, "Cellules somatiques élevées")]
    [InlineData(ActionType.DryOff, "Tarissement")]
    [InlineData(ActionType.Calving, "Vêlage")]
    [InlineData(ActionType.Insemination, "Insémination")]
    public void Rendu_ActionEnAttente_LibelleDuType(ActionType type, string libelle)
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "M", "N", ActionVache(1, CowA, false, Aujourdhui, type, ""))]
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        Assert.Equal(libelle, composant.Find("[data-testid='farm-pending-action'] .sheet__item-title").TextContent);
        Assert.DoesNotContain("Note :", composant.Find("[data-testid='farm-pending-action']").TextContent);
    }

    [Fact]
    public void Rendu_VacheDUnAutreElevageOuInconnue_AfficheVacheNonIdentifiee()
    {
        var inconnue = Guid.Parse("0d000000-0000-0000-0000-0000000000aa");
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits =
            [
                VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "M", "N",
                    ActionVache(1, CowB.Id, false, Aujourdhui, ActionType.DryOff, "x"),
                    ActionVache(2, inconnue, false, Aujourdhui.AddDays(1), ActionType.Calving, "y"))
            ]
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        var vaches = composant.FindAll("[data-testid='farm-pending-action-cow']").Select(e => Norm(e.TextContent)).ToArray();
        Assert.Equal(["Vache : Vache non identifiée", "Vache : Vache non identifiée"], vaches);
        Assert.DoesNotContain(CowB.Name, composant.Markup);
    }

    [Fact]
    public void Rendu_ListesVides_ONtDesMessagesDistincts()
    {
        // Visite précédente sans action : seule la liste d'actions est vide.
        var avecVisite = new FakeRepo { Herd = Troupeau, Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "M", "N")] };
        var c1 = Rendu(avecVisite, out var ctx1);
        using var _1 = ctx1;
        Assert.Single(c1.FindAll("[data-testid='farm-previous-visit']"));
        Assert.Empty(c1.FindAll("[data-testid='farm-previous-visits-empty']"));
        Assert.Contains("Aucune action en attente", c1.Find("[data-testid='farm-pending-actions-empty']").TextContent);

        // Aucune visite : les deux listes sont vides avec deux messages différents.
        var c2 = Rendu(new FakeRepo { Herd = Troupeau }, out var ctx2);
        using var _2 = ctx2;
        var visitesVide = c2.Find("[data-testid='farm-previous-visits-empty']").TextContent;
        var actionsVide = c2.Find("[data-testid='farm-pending-actions-empty']").TextContent;
        Assert.Contains("Aucune visite précédente", visitesVide);
        Assert.NotEqual(visitesVide, actionsVide);
        Assert.Empty(c2.FindAll("[data-testid='farm-previous-visit']"));
        Assert.Empty(c2.FindAll("[data-testid='farm-pending-action']"));
    }

    [Fact]
    public void Rendu_Accessibilite_TitresEtSectionsNommees()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "M", "N")] };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        Assert.Single(composant.FindAll("h1"));
        Assert.Equal("false", composant.Find("section.sheet").GetAttribute("aria-busy"));
        var titres = composant.FindAll("h3").Select(h => h.TextContent).ToArray();
        Assert.Equal(["Effectif", "Visites précédentes", "Actions en attente"], titres);
        foreach (var section in composant.FindAll("section[data-testid]"))
        {
            var labelledby = section.GetAttribute("aria-labelledby");
            Assert.False(string.IsNullOrEmpty(labelledby));
            Assert.NotEmpty(composant.FindAll($"#{labelledby}"));
        }
    }

    [Fact]
    public void Rendu_ElevageIntrouvable_NAfficheAucunContenuDUneAutreFerme()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "Motif A secret", "Notes A secret")] };
        var composant = Rendu(depot, out var contexte, FarmInconnue);
        using var _ = contexte;

        Assert.Contains("Fiche introuvable", composant.Find("[role='alert']").TextContent);
        Assert.Equal("false", composant.Find("section.sheet").GetAttribute("aria-busy"));
        foreach (var testid in new[] { "farm-name", "farm-city", "farm-herd", "farm-previous-visits", "farm-pending-actions" })
        {
            Assert.Empty(composant.FindAll($"[data-testid='{testid}']"));
        }
        Assert.DoesNotContain("Motif A secret", composant.Markup);
        Assert.DoesNotContain(Troupeau.Locations[0].Name, composant.Markup);
        Assert.DoesNotContain(CowAName, composant.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rendu_ErreurDuDepot_NAfficheAucunContenuDeFerme(bool surGetAll)
    {
        var erreur = new VisitStorageException(VisitStorageError.QuotaExceeded, "brut", "SECRET-DETAIL");
        var depot = new FakeRepo
        {
            Herd = Troupeau,
            Visits = [VisiteDetail(1, FarmA, Aujourdhui.AddDays(-1), "Motif A secret", "Notes A secret")],
            HerdFailure = surGetAll ? null : erreur,
            GetAllFailure = surGetAll ? erreur : null
        };
        var composant = Rendu(depot, out var contexte);
        using var _ = contexte;

        Assert.Contains("quota", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("[data-testid='farm-herd']"));
        Assert.Empty(composant.FindAll("[data-testid='farm-previous-visits']"));
        Assert.Empty(composant.FindAll("[data-testid='farm-pending-actions']"));
        Assert.DoesNotContain("Motif A secret", composant.Markup);
        Assert.DoesNotContain("SECRET-DETAIL", composant.Markup);
        Assert.DoesNotContain(Troupeau.Locations[0].Name, composant.Markup);
    }

    private sealed class FakeRepo : IVisitRepository
    {
        public DemoDataSet? Herd { get; set; }
        public List<Visit> Visits { get; set; } = [];
        public Exception? HerdFailure { get; set; }
        public Exception? GetAllFailure { get; set; }
        public Func<Task<IReadOnlyList<Visit>>>? GetAllHandler { get; set; }
        public int GetAllCalls { get; private set; }

        public Task<DemoDataSet?> GetHerdAsync() =>
            HerdFailure is not null ? Task.FromException<DemoDataSet?>(HerdFailure) : Task.FromResult(Herd);

        public Task<IReadOnlyList<Visit>> GetAllAsync()
        {
            GetAllCalls++;
            if (GetAllFailure is not null)
            {
                return Task.FromException<IReadOnlyList<Visit>>(GetAllFailure);
            }

            return GetAllHandler is not null ? GetAllHandler() : Task.FromResult<IReadOnlyList<Visit>>(Visits);
        }

        public Task InitializeDemoAsync(DemoSeed seed) => throw new NotSupportedException();
        public Task<Visit?> GetAsync(Guid id) => throw new NotSupportedException();
        public Task SaveAsync(Visit visit, VisitPhoto? photo = null) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task SaveHerdAsync(DemoDataSet herd) => throw new NotSupportedException();
        public Task<VisitPhoto?> GetPhotoAsync(Guid photoId) => throw new NotSupportedException();
        public Task SavePhotoAsync(Guid visitId, VisitPhoto photo) => throw new NotSupportedException();
        public Task DeletePhotoAsync(Guid visitId) => throw new NotSupportedException();
    }
}
