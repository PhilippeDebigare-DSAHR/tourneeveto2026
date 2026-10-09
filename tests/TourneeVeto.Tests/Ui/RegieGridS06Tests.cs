using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Biosecurity;
using TourneeVeto.Domain.Herd;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

/// <summary>S06 : modification et sauvegarde de la grille de régie (dépôt simulé, temps figé, seed fixe, pas d'IndexedDB).</summary>
public sealed class RegieGridS06Tests
{
    private static readonly DateOnly Aujourdhui = new(2026, 10, 9);
    private static readonly DemoDataSet Troupeau = DemoData.Generate(Aujourdhui, 42);
    private static readonly Guid FarmA = Troupeau.Locations[0].Id;
    private static readonly Cow VacheA1 = Troupeau.Cows.First(c => c.LocationId == FarmA);
    private static readonly Cow VacheA2 = Troupeau.Cows.Where(c => c.LocationId == FarmA).Skip(1).First();
    private static readonly Guid VisiteId = Guid.Parse("0b000000-0000-0000-0000-000000000001");
    private static readonly Guid VisiteBId = Guid.Parse("0b000000-0000-0000-0000-000000000002");
    private static readonly Guid PhotoId = Guid.Parse("0b000000-0000-0000-0000-0000000000f1");

    private static readonly Guid ActionVelage = Guid.Parse("0d000000-0000-0000-0000-000000000001");
    private static readonly Guid ActionTarissement = Guid.Parse("0d000000-0000-0000-0000-000000000002");
    private static readonly Guid ActionVelageBis = Guid.Parse("0d000000-0000-0000-0000-000000000003");
    private static readonly Guid ActionVisiteB = Guid.Parse("0d000000-0000-0000-0000-000000000004");

    private static readonly BiosecurityResponse[] Reponses =
    [
        new(Guid.Parse("0e000000-0000-0000-0000-000000000001"), Guid.Parse("0e000000-0000-0000-0000-0000000000a1")),
        new(Guid.Parse("0e000000-0000-0000-0000-000000000002"), Guid.Parse("0e000000-0000-0000-0000-0000000000a2")),
    ];

    private static VisitAction Velage() =>
        new(ActionVelage, VacheA1.Id, ActionType.Calving, Aujourdhui, false, "Note vêlage");

    private static VisitAction Tarissement() =>
        new(ActionTarissement, VacheA2.Id, ActionType.DryOff, Aujourdhui, true, "Note tarissement");

    private static Visit VisiteComplete(DateOnly? cloture = null) =>
        new(VisiteId, FarmA, Aujourdhui, "Cause fictive", "Notes de visite fictives", PhotoId)
        {
            Actions = [Velage(), Tarissement()],
            BiosecurityResponses = Reponses,
            ClosedOn = cloture,
        };

    private static VisitStorageException Echec(VisitStorageError code) =>
        new(code, "Échec simulé.");

    private static BunitContext Contexte(FakeRepo depot)
    {
        var contexte = new BunitContext();
        contexte.Services.AddLogging();
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(depot);
        return contexte;
    }

    private static IRenderedComponent<RegieGrid> Afficher(BunitContext contexte, Guid visitId)
    {
        var composant = contexte.Render<RegieGrid>(p => p.Add(c => c.VisitId, visitId));
        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-category']")));
        return composant;
    }

    private static AngleSharp.Dom.IElement Ligne(IRenderedComponent<RegieGrid> c, Guid cowId, string type) =>
        c.Find($"[data-testid='regie-category'][data-category='{type}'] [data-testid='regie-action'][data-cow-id='{cowId}']");

    private static AngleSharp.Dom.IElement Case(IRenderedComponent<RegieGrid> c, Guid cowId, string type) =>
        Ligne(c, cowId, type).QuerySelector("[data-testid='regie-completed']")!;

    private static AngleSharp.Dom.IElement Note(IRenderedComponent<RegieGrid> c, Guid cowId, string type) =>
        Ligne(c, cowId, type).QuerySelector("[data-testid='regie-notes']")!;

    private static string ValeurNote(IRenderedComponent<RegieGrid> c, Guid cowId, string type) =>
        Note(c, cowId, type).GetAttribute("value") ?? Note(c, cowId, type).TextContent;

    private static void Enregistrer(IRenderedComponent<RegieGrid> c) => c.Find("[data-testid='regie-save']").Click();

    private static VisitAction Trouver(Visit v, Guid id) => v.Actions.Single(a => a.Id == id);

    [Fact]
    public async Task Sauvegarde_ModifieIsCompletedEtNotesDeLActionCibleEtPreserveLesAutresChampsDeLaVisite()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete(Aujourdhui.AddDays(1))] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        Case(composant, VacheA1.Id, "Calving").Change(true);
        Note(composant, VacheA1.Id, "Calving").Change("Vêlage terminé sans complication");
        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        var sauvegarde = Assert.Single(depot.Saved);
        var cible = Trouver(sauvegarde, ActionVelage);
        Assert.True(cible.IsCompleted);
        Assert.Equal("Vêlage terminé sans complication", cible.Notes);
        Assert.Equal(VacheA1.Id, cible.CowId);
        Assert.Equal(ActionType.Calving, cible.Type);
        Assert.Equal(Aujourdhui, cible.Date);
        // Autre action inchangée.
        Assert.Equal(Tarissement(), Trouver(sauvegarde, ActionTarissement));
        Assert.Equal(2, sauvegarde.Actions.Count);
        // Visite complète préservée.
        Assert.Equal(VisiteId, sauvegarde.Id);
        Assert.Equal(FarmA, sauvegarde.FarmId);
        Assert.Equal(Aujourdhui, sauvegarde.Date);
        Assert.Equal("Cause fictive", sauvegarde.Cause);
        Assert.Equal("Notes de visite fictives", sauvegarde.Notes);
        Assert.Equal(PhotoId, sauvegarde.PhotoId);
        Assert.Equal(Reponses, sauvegarde.BiosecurityResponses);
        Assert.Equal(Aujourdhui.AddDays(1), sauvegarde.ClosedOn);
        Assert.Contains("Modifications enregistrées.", composant.Markup);
        Assert.Empty(composant.FindAll("[data-testid='regie-save-error']"));
    }

    [Fact]
    public async Task Rechargement_RestitueLesModificationsSauvegardees()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Case(composant, VacheA1.Id, "Calving").Change(true);
        Note(composant, VacheA1.Id, "Calving").Change("Note modifiée");
        Case(composant, VacheA2.Id, "DryOff").Change(false);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));

        await using var contexte2 = Contexte(depot);
        var rechargee = Afficher(contexte2, VisiteId);

        Assert.True(Case(rechargee, VacheA1.Id, "Calving").HasAttribute("checked"));
        Assert.Equal("Note modifiée", ValeurNote(rechargee, VacheA1.Id, "Calving"));
        Assert.False(Case(rechargee, VacheA2.Id, "DryOff").HasAttribute("checked"));
        Assert.Equal("Note tarissement", ValeurNote(rechargee, VacheA2.Id, "DryOff"));
        Assert.Empty(rechargee.FindAll("[data-testid='regie-save-success']"));
    }

    [Fact]
    public async Task Sauvegarde_SansModificationReenregistreLaVisiteIdentique()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        var sauvegarde = Assert.Single(depot.Saved);
        Assert.Equal(VisiteComplete().Actions, sauvegarde.Actions);
        Assert.Equal(Reponses, sauvegarde.BiosecurityResponses);
    }

    [Fact]
    public async Task Sauvegarde_IdentifieLActionParIdMemeSiMemeVacheEtMemeType()
    {
        var jumelle = new VisitAction(ActionVelageBis, VacheA1.Id, ActionType.Calving, Aujourdhui, false, "Jumelle");
        var visite = VisiteComplete() with { Actions = [Velage(), jumelle, Tarissement()] };
        var depot = new FakeRepo { Herd = Troupeau, Visits = [visite] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        var lignes = composant.FindAll("[data-testid='regie-category'][data-category='Calving'] [data-testid='regie-action']");
        Assert.Equal(2, lignes.Count);
        // Une action d'une même vache est cochée : seule celle-ci doit changer (ordre d'affichage non supposé).
        var ligneJumelle = lignes.Single(l => l.QuerySelector("textarea")!.TextContent.Contains("Jumelle")
            || l.QuerySelector("textarea")!.GetAttribute("value") == "Jumelle");
        ligneJumelle.QuerySelector("[data-testid='regie-completed']")!.Change(true);
        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        var sauvegarde = Assert.Single(depot.Saved);
        Assert.True(Trouver(sauvegarde, ActionVelageBis).IsCompleted);
        Assert.False(Trouver(sauvegarde, ActionVelage).IsCompleted);
        Assert.Equal("Note vêlage", Trouver(sauvegarde, ActionVelage).Notes);
        Assert.Equal(Tarissement(), Trouver(sauvegarde, ActionTarissement));
    }

    [Fact]
    public async Task Sauvegarde_PermetDeVider_LesNotesEtDeDecocher()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        Case(composant, VacheA2.Id, "DryOff").Change(false);
        Note(composant, VacheA2.Id, "DryOff").Change("");
        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        var action = Trouver(Assert.Single(depot.Saved), ActionTarissement);
        Assert.False(action.IsCompleted);
        Assert.Equal(string.Empty, action.Notes);
    }

    [Fact]
    public async Task EchecDeSauvegarde_AfficheUneAlerteFrancaise_ConserveLesEditsEtLeNouvelEssaiReussit()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()] };
        depot.SaveHandler = _ => throw Echec(VisitStorageError.Unavailable);
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Case(composant, VacheA1.Id, "Calving").Change(true);
        Note(composant, VacheA1.Id, "Calving").Change("Note à conserver");

        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-error']")));
        var alerte = composant.Find("[data-testid='regie-save-error']");
        Assert.Equal("alert", alerte.GetAttribute("role"));
        Assert.Contains("Enregistrement impossible", alerte.TextContent);
        Assert.Contains("Vos modifications sont conservées", alerte.TextContent);
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
        Assert.DoesNotContain("Modifications enregistrées", composant.Markup);
        Assert.True(Case(composant, VacheA1.Id, "Calving").HasAttribute("checked"));
        Assert.Equal("Note à conserver", ValeurNote(composant, VacheA1.Id, "Calving"));
        Assert.False(composant.Find("[data-testid='regie-save']").HasAttribute("disabled"));
        Assert.False(Case(composant, VacheA1.Id, "Calving").HasAttribute("disabled"));
        Assert.Empty(depot.Saved);
        Assert.False(Trouver(depot.Visits[0], ActionVelage).IsCompleted);

        depot.SaveHandler = null;
        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        Assert.Empty(composant.FindAll("[data-testid='regie-save-error']"));
        var sauvegarde = Assert.Single(depot.Saved);
        Assert.True(Trouver(sauvegarde, ActionVelage).IsCompleted);
        Assert.Equal("Note à conserver", Trouver(sauvegarde, ActionVelage).Notes);
        Assert.Equal(2, depot.SaveAttempts);
    }

    [Theory]
    [InlineData(VisitStorageError.QuotaExceeded, "quota")]
    [InlineData(VisitStorageError.UpgradeBlocked, "bloquée")]
    [InlineData(VisitStorageError.InvalidData, "Veuillez réessayer")]
    public async Task EchecDeSauvegarde_MessageFrancaisSelonLeCodeDErreur(VisitStorageError code, string extrait)
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()], SaveHandler = _ => throw Echec(code) };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-error']")));
        Assert.Contains(extrait, composant.Find("[data-testid='regie-save-error']").TextContent);
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
    }

    [Fact]
    public async Task EchecDeSauvegarde_ExceptionInattendue_AfficheLeMessageGeneriqueSansFuiteTechnique()
    {
        var depot = new FakeRepo
        {
            Herd = Troupeau, Visits = [VisiteComplete()],
            SaveHandler = _ => throw new InvalidOperationException("détail-technique-secret"),
        };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-error']")));
        Assert.Contains("Enregistrement impossible", composant.Markup);
        Assert.DoesNotContain("détail-technique-secret", composant.Markup);
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
    }

    [Fact]
    public async Task ModificationApresUnSucces_MasqueLaConfirmation_EtApresUnEchec_MasqueLAlerte()
    {
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));

        Note(composant, VacheA1.Id, "Calving").Change("Nouvelle note");

        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));

        depot.SaveHandler = _ => throw Echec(VisitStorageError.Unavailable);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-error']")));
        Case(composant, VacheA1.Id, "Calving").Change(true);
        Assert.Empty(composant.FindAll("[data-testid='regie-save-error']"));
    }

    [Fact]
    public async Task PendantLEnregistrement_BoutonCasesEtNotesSontDesactivesPuisReactives()
    {
        var porte = new TaskCompletionSource();
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete()], SaveHandler = _ => porte.Task };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Case(composant, VacheA1.Id, "Calving").Change(true);

        Enregistrer(composant);

        composant.WaitForAssertion(() => Assert.True(composant.Find("[data-testid='regie-save']").HasAttribute("disabled")));
        Assert.Contains("Enregistrement en cours", composant.Find("[data-testid='regie-save']").TextContent);
        Assert.All(composant.FindAll("[data-testid='regie-completed']"), e => Assert.True(e.HasAttribute("disabled")));
        Assert.All(composant.FindAll("[data-testid='regie-notes']"), e => Assert.True(e.HasAttribute("disabled")));
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
        Assert.Equal(1, depot.SaveAttempts);

        porte.SetResult();

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-testid='regie-save-success']")));
        Assert.False(composant.Find("[data-testid='regie-save']").HasAttribute("disabled"));
        Assert.All(composant.FindAll("[data-testid='regie-completed']"), e => Assert.False(e.HasAttribute("disabled")));
        Assert.All(composant.FindAll("[data-testid='regie-notes']"), e => Assert.False(e.HasAttribute("disabled")));
        Assert.Equal(1, depot.SaveAttempts);
    }

    [Fact]
    public async Task ChangementDeVisitIdPendantLEnregistrement_NAfficheAucunSuccesEtNeSauvegardePasLeMauvaisId()
    {
        var porte = new TaskCompletionSource();
        var visiteB = new Visit(VisiteBId, FarmA, Aujourdhui, "Cause B", "Notes B", null)
        {
            Actions = [new VisitAction(ActionVisiteB, VacheA2.Id, ActionType.Insemination, Aujourdhui, false, "Note B")]
        };
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete(), visiteB], SaveHandler = _ => porte.Task };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Case(composant, VacheA1.Id, "Calving").Change(true);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.True(composant.Find("[data-testid='regie-save']").HasAttribute("disabled")));

        composant.Render(p => p.Add(c => c.VisitId, VisiteBId));
        composant.WaitForAssertion(() => Assert.Contains("Note B", composant.Markup));
        depot.SaveHandler = null;
        porte.SetResult();
        await Task.Delay(100);
        composant.Render(p => p.Add(c => c.VisitId, VisiteBId));

        // Aucun succès de la visite A ne doit apparaître sur la visite B.
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
        Assert.Empty(composant.FindAll("[data-testid='regie-save-error']"));
        Assert.False(composant.Find("[data-testid='regie-save']").HasAttribute("disabled"));
        // Les appels de sauvegarde ne concernent que la visite A (celle qui était affichée au clic).
        Assert.DoesNotContain(depot.SaveRequests, v => v.Id == VisiteBId);
        Assert.Single(depot.SaveRequests);
        // Visite B reste inchangée en stockage.
        Assert.False(Trouver(depot.Visits.Single(v => v.Id == VisiteBId), ActionVisiteB).IsCompleted);
        Assert.DoesNotContain("Cause fictive", composant.Markup);
    }

    [Fact]
    public async Task EchecTardifApresChangementDeVisitId_NAfficheAucuneAlerteSurLaNouvelleVisite()
    {
        var porte = new TaskCompletionSource();
        var visiteB = new Visit(VisiteBId, FarmA, Aujourdhui, "Cause B", "Notes B", null)
        {
            Actions = [new VisitAction(ActionVisiteB, VacheA2.Id, ActionType.Insemination, Aujourdhui, false, "Note B")]
        };
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete(), visiteB], SaveHandler = _ => porte.Task };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.True(composant.Find("[data-testid='regie-save']").HasAttribute("disabled")));

        composant.Render(p => p.Add(c => c.VisitId, VisiteBId));
        composant.WaitForAssertion(() => Assert.Contains("Note B", composant.Markup));
        porte.SetException(Echec(VisitStorageError.Unavailable));
        await Task.Delay(100);
        composant.Render(p => p.Add(c => c.VisitId, VisiteBId));

        Assert.Empty(composant.FindAll("[data-testid='regie-save-error']"));
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
        Assert.False(composant.Find("[data-testid='regie-save']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task AllerRetourVisitIdPendantLEnregistrement_GenerationObsolete_NAffichePasDeSuccesErrone()
    {
        var porte = new TaskCompletionSource();
        var visiteB = new Visit(VisiteBId, FarmA, Aujourdhui, "Cause B", "Notes B", null)
        {
            Actions = [new VisitAction(ActionVisiteB, VacheA2.Id, ActionType.Insemination, Aujourdhui, false, "Note B")]
        };
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete(), visiteB], SaveHandler = _ => porte.Task };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);
        Case(composant, VacheA1.Id, "Calving").Change(true);
        Enregistrer(composant);
        composant.WaitForAssertion(() => Assert.True(composant.Find("[data-testid='regie-save']").HasAttribute("disabled")));

        composant.Render(p => p.Add(c => c.VisitId, VisiteBId));
        composant.WaitForAssertion(() => Assert.Contains("Note B", composant.Markup));
        composant.Render(p => p.Add(c => c.VisitId, VisiteId));
        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-testid='regie-action']")));
        depot.SaveHandler = null;
        porte.SetResult();
        await Task.Delay(100);
        composant.Render(p => p.Add(c => c.VisitId, VisiteId));

        // Le rechargement a créé une nouvelle génération : l'ancien enregistrement ne confirme rien.
        Assert.Empty(composant.FindAll("[data-testid='regie-save-success']"));
        Assert.False(composant.Find("[data-testid='regie-save']").HasAttribute("disabled"));
        Assert.All(composant.FindAll("[data-testid='regie-completed']"), e => Assert.False(e.HasAttribute("disabled")));
        Assert.DoesNotContain(depot.SaveRequests, v => v.Id == VisiteBId);
    }

    [Fact]
    public async Task VisiteCloturee_Comportement_Courant_LaGrilleResteModifiableEtConserveClosedOn()
    {
        // S06 ne définit aucun blocage pour une visite clôturée : ce test documente le comportement courant
        // (modification et sauvegarde possibles, ClosedOn préservée) sans l'imposer comme exigence.
        var depot = new FakeRepo { Herd = Troupeau, Visits = [VisiteComplete(Aujourdhui)] };
        await using var contexte = Contexte(depot);
        var composant = Afficher(contexte, VisiteId);

        var desactivee = composant.Find("[data-testid='regie-save']").HasAttribute("disabled");
        if (!desactivee)
        {
            Case(composant, VacheA1.Id, "Calving").Change(true);
            Enregistrer(composant);
            composant.WaitForAssertion(() => Assert.NotEmpty(depot.Saved));
            Assert.Equal(Aujourdhui, depot.Saved[0].ClosedOn);
            Assert.True(depot.Saved[0].IsClosed);
        }

        Assert.Equal(!desactivee, depot.Saved.Count == 1);
    }

    private sealed class FakeRepo : IVisitRepository
    {
        private readonly object _verrou = new();

        public DemoDataSet? Herd { get; set; }
        public List<Visit> Visits { get; set; } = [];
        public Func<Visit, Task>? SaveHandler { get; set; }
        public List<Visit> Saved { get; } = [];
        public List<Visit> SaveRequests { get; } = [];
        public int SaveAttempts { get; private set; }

        public Task<DemoDataSet?> GetHerdAsync() => Task.FromResult(Herd);

        public Task<IReadOnlyList<Visit>> GetAllAsync() => Task.FromResult<IReadOnlyList<Visit>>(Visits.ToArray());

        public Task<Visit?> GetAsync(Guid id)
        {
            lock (_verrou)
            {
                return Task.FromResult(Visits.FirstOrDefault(v => v.Id == id));
            }
        }

        public async Task SaveAsync(Visit visit, VisitPhoto? photo = null)
        {
            Func<Visit, Task>? handler;
            lock (_verrou)
            {
                SaveAttempts++;
                SaveRequests.Add(visit);
                handler = SaveHandler;
            }

            if (handler is not null)
            {
                await handler(visit);
            }

            lock (_verrou)
            {
                var index = Visits.FindIndex(v => v.Id == visit.Id);
                if (index >= 0)
                {
                    Visits[index] = visit;
                }
                else
                {
                    Visits.Add(visit);
                }

                Saved.Add(visit);
            }
        }

        public Task InitializeDemoAsync(DemoSeed seed) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task SaveHerdAsync(DemoDataSet herd) => throw new NotSupportedException();
        public Task<VisitPhoto?> GetPhotoAsync(Guid photoId) => throw new NotSupportedException();
        public Task SavePhotoAsync(Guid visitId, VisitPhoto photo) => throw new NotSupportedException();
        public Task DeletePhotoAsync(Guid visitId) => throw new NotSupportedException();
    }
}
