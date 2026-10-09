using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Biosecurity;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

public sealed class VisitReportTests
{
    private static readonly Guid VisitIdentifiant = Guid.Parse("0a000000-0000-0000-0000-000000000001");
    private static readonly Guid FarmId = Guid.Parse("0b000000-0000-0000-0000-000000000001");

    private const string CloseButton = "[data-testid='close-visit']";
    private const string PrintButton = "[data-testid='print-report']";
    private const string PrintModule = "./_content/TourneeVeto.Ui/js/printReport.js";
    private static readonly Guid PhotoIdentifiant = Guid.Parse("0d000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Impression_ChargeLeModuleIsoleAppelleImprimerEtLeLibere()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        var module = contexte.JSInterop.SetupModule(PrintModule);
        module.SetupVoid("printReport").SetVoidResult();
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));
        Assert.Empty(contexte.JSInterop.Invocations);

        composant.Find(PrintButton).Click();

        composant.WaitForAssertion(() => Assert.Single(module.Invocations, i => i.Identifier == "printReport"));
        Assert.Empty(composant.FindAll("[role='alert']"));
        composant.WaitForAssertion(() => Assert.False(composant.Find(PrintButton).HasAttribute("disabled")));

        composant.Find(PrintButton).Click();
        composant.WaitForAssertion(() => Assert.Equal(2, module.Invocations.Count(i => i.Identifier == "printReport")));
        Assert.Single(contexte.JSInterop.Invocations, i => i.Identifier == "import");

        await composant.Instance.DisposeAsync();
        await composant.Instance.DisposeAsync();
    }

    [Fact]
    public async Task Impression_EnErreur_AfficheUneAlerteSansFauxSucces()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        var module = contexte.JSInterop.SetupModule(PrintModule);
        module.SetupVoid("printReport").SetException(new JSException("impression refusée"));
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(PrintButton).Click();

        composant.WaitForAssertion(() =>
            Assert.Contains("L'impression n'a pas pu être lancée", composant.Find("[role='alert']").TextContent));
        Assert.Contains("Aucun document n'a été produit", composant.Find("[role='alert']").TextContent);
        Assert.False(composant.Find(PrintButton).HasAttribute("disabled"));
        Assert.Empty(composant.FindAll(".report__success"));
    }

    [Fact]
    public async Task Impression_ModuleIndisponible_AfficheUneAlerte()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        contexte.Services.AddSingleton<IJSRuntime>(new FailingJsRuntime());
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(PrintButton).Click();

        composant.WaitForAssertion(() =>
            Assert.Contains("L'impression n'a pas pu être lancée", composant.Find("[role='alert']").TextContent));
    }

    [Fact]
    public async Task Photo_LocaleEstAfficheeEnDataUrlSansRequeteReseau()
    {
        await using var contexte = CreateContext(out var depot);
        byte[] octets = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        var visite = CreateVisit() with { PhotoId = PhotoIdentifiant };
        depot.Visits[visite.Id] = visite;
        depot.Photos[PhotoIdentifiant] = new VisitPhoto(PhotoIdentifiant, "image/png", octets);

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        var image = composant.Find("img[data-report-photo]");
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(octets)}", image.GetAttribute("src"));
        Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
        Assert.Equal("Photo de la visite", composant.FindAll("h2")[^1].TextContent);
        Assert.Equal(1, depot.PhotoCalls);
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.Empty(contexte.JSInterop.Invocations);
    }

    [Fact]
    public async Task SansPhotoId_NeLitPasDePhotoNiNAffichePhoto()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Empty(composant.FindAll("img"));
        Assert.Equal(0, depot.PhotoCalls);
    }

    [Fact]
    public async Task Photo_ErreurDeLecture_EstExpliciteEtLeRapportResteAffiche()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with { PhotoId = PhotoIdentifiant };
        depot.Visits[visite.Id] = visite;
        depot.PhotoFailure = new VisitStorageException(
            VisitStorageError.Unavailable, "Le stockage local est indisponible.", "échec simulé");

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("La photo n'a pas pu être lue", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("img"));
        Assert.Equal(4, composant.FindAll("section").Count);
        Assert.Contains("Contrôle de routine fictif", composant.Markup);
    }

    [Fact]
    public async Task Photo_Absente_EstSignaleeSansImage()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with { PhotoId = PhotoIdentifiant };
        depot.Visits[visite.Id] = visite;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("introuvable", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("img"));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("image/png\"onerror=\"x")]
    public async Task Photo_TypeNonPrisEnCharge_EstRefuseeSansImage(string type)
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with { PhotoId = PhotoIdentifiant };
        depot.Visits[visite.Id] = visite;
        depot.Photos[PhotoIdentifiant] = new VisitPhoto(PhotoIdentifiant, type, [1, 2, 3]);

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("format non pris en charge", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("img"));
    }

    [Fact]
    public void Route_EstAtteignableParIdentifiantDeVisite()
    {
        var routes = typeof(VisitReport).GetCustomAttributes<RouteAttribute>().Select(r => r.Template);

        Assert.Contains("/rapport/{VisitId:guid?}", routes);
    }

    [Fact]
    public async Task Chargement_PresenteUnEtatOccupeSansDonnees()
    {
        await using var contexte = CreateContext(out var depot);
        var attente = new TaskCompletionSource<Visit?>();
        depot.GetHandler = _ => attente.Task;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, VisitIdentifiant));

        Assert.Equal("true", composant.Find("article").GetAttribute("aria-busy"));
        Assert.Contains("Chargement", composant.Find("[role='status']").TextContent);
        Assert.Empty(composant.FindAll("section"));
        Assert.Empty(composant.FindAll("button"));
        attente.SetResult(null);
    }

    [Fact]
    public async Task Rendu_AfficheSectionsEtSyntheseDeLaVisiteEnregistree()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        var synthese = VisitReportSummary.Create(visite, BiosecurityChecklistReference.Questions);

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Equal("Rapport de visite", composant.Find("h1").TextContent);
        Assert.Equal("false", composant.Find("article").GetAttribute("aria-busy"));
        var titres = composant.FindAll("h2").Select(h => h.TextContent).ToArray();
        Assert.Equal(["Visite", "Actions", "Bilan de biosécurité"], titres);
        Assert.Contains("Contrôle de routine fictif", composant.Markup);
        Assert.Contains("1 octobre 2026", composant.Markup);
        Assert.Contains("Visite ouverte", composant.Markup);
        Assert.Equal(1, composant.Find("[data-testid='completed-actions']").QuerySelectorAll("li").Length);
        Assert.Equal(1, composant.Find("[data-testid='pending-actions']").QuerySelectorAll("li").Length);
        Assert.Contains("Diagnostic de gestation", composant.Find("[data-testid='completed-actions']").TextContent);
        Assert.Contains("Tarissement", composant.Find("[data-testid='pending-actions']").TextContent);

        Assert.Equal(synthese.Sections.Count, composant.Find("[data-testid='sections']").QuerySelectorAll("li").Length);
        Assert.Contains($"{synthese.AnsweredCount} question(s) répondue(s) sur {synthese.QuestionCount}", composant.Markup);
        Assert.Contains("Bilan incomplet", composant.Markup);
        var priorites = composant.Find("[data-testid='priorities']").QuerySelectorAll("li");
        Assert.Equal(synthese.PriorityPractices.Count, priorites.Length);
        Assert.NotEmpty(priorites);
        Assert.Contains(synthese.PriorityPractices[0].Prompt, priorites[0].TextContent);
        Assert.Contains("Démonstration — données fictives", composant.Markup);
        Assert.Equal("Clôturer la visite et l'enregistrer", composant.Find(CloseButton).TextContent.Trim());
        Assert.Equal("Imprimer / PDF", composant.Find(PrintButton).TextContent.Trim());
    }

    [Fact]
    public async Task VisiteDejaCloturee_AfficheLaDateSansProposerLaCloture()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with { ClosedOn = new DateOnly(2026, 10, 2) };
        depot.Visits[visite.Id] = visite;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("clôturée le 2 octobre 2026", composant.Markup);
        Assert.Empty(composant.FindAll(CloseButton));
        Assert.Single(composant.FindAll(PrintButton));
    }

    [Fact]
    public async Task VisiteSansReponseNiAction_AfficheLesEtatsVidesSansScoreInvente()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = new Visit(VisitIdentifiant, FarmId, new DateOnly(2026, 10, 1), "", "", null);
        depot.Visits[visite.Id] = visite;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("Non calculé", composant.Markup);
        Assert.Contains("Aucune action réalisée", composant.Markup);
        Assert.Contains("Aucune action à suivre", composant.Markup);
        Assert.Contains("Aucune pratique défavorable enregistrée", composant.Markup);
        Assert.Contains("Non renseigné", composant.Markup);
    }

    [Fact]
    public async Task VisiteAbsente_AfficheUneErreurSansDonnees()
    {
        await using var contexte = CreateContext(out _);

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, VisitIdentifiant));

        Assert.Contains("Aucune visite enregistrée", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("section"));
        Assert.Empty(composant.FindAll("button"));
    }

    [Fact]
    public async Task SansIdentifiant_AfficheUneErreurSansInterrogerLeStockage()
    {
        await using var contexte = CreateContext(out var depot);

        var composant = contexte.Render<VisitReport>();

        Assert.Contains("Aucune visite enregistrée", composant.Find("[role='alert']").TextContent);
        Assert.Equal(0, depot.GetCalls);
    }

    [Fact]
    public async Task ErreurDeLecture_EstExpliciteEtPermetDeReessayer()
    {
        await using var contexte = CreateContext(out var depot);
        depot.GetHandler = _ => throw new VisitStorageException(
            VisitStorageError.Unavailable, "Le stockage local est indisponible.", "refus simulé");

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, VisitIdentifiant));

        Assert.Contains("n'a pas pu être lue", composant.Find("[role='alert']").TextContent);
        Assert.Contains("indisponible", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("section"));
        Assert.DoesNotContain("clôturée", composant.Markup);

        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        depot.GetHandler = null;
        composant.Find("button").Click();

        Assert.Equal(3, composant.FindAll("section").Count);
        Assert.Equal(2, depot.GetCalls);
    }

    [Fact]
    public async Task ReponsesIncoherentes_NeProduisentPasDeSynthese()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with
        {
            BiosecurityResponses = [new BiosecurityResponse(Guid.NewGuid(), Guid.NewGuid())]
        };
        depot.Visits[visite.Id] = visite;

        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        Assert.Contains("incohérentes", composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("section"));
    }

    [Fact]
    public async Task Cloture_EnregistreLaVisiteAvecLaDateDuJourPuisConfirmeApresRelecture()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(CloseButton).Click();

        composant.WaitForAssertion(() => Assert.Contains("clôturée et enregistrée localement", composant.Find(".report__success").TextContent));
        var enregistree = Assert.Single(depot.Saved);
        Assert.Equal(visite.Id, enregistree.Id);
        Assert.Equal(new DateOnly(2026, 10, 9), enregistree.ClosedOn);
        Assert.True(enregistree.IsClosed);
        Assert.Equal(visite.BiosecurityResponses, enregistree.BiosecurityResponses);
        Assert.Equal(visite.Actions, enregistree.Actions);
        Assert.Empty(composant.FindAll(CloseButton));
        Assert.Contains("9 octobre 2026", composant.Markup);
    }

    [Theory]
    [InlineData(VisitStorageError.QuotaExceeded, "Le quota de stockage local est dépassé.")]
    [InlineData(VisitStorageError.Unavailable, "Le stockage local est indisponible.")]
    public async Task ErreurDEcriture_NAffichePasDeReussiteEtConserveLaVisiteOuverte(VisitStorageError code, string message)
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        depot.SaveFailure = new VisitStorageException(code, message, "échec simulé");
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(CloseButton).Click();

        composant.WaitForAssertion(() => Assert.Contains("n'a pas été enregistrée", composant.Find("[role='alert']").TextContent));
        Assert.Contains(message, composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll(".report__success"));
        Assert.DoesNotContain("clôturée et enregistrée", composant.Markup);
        Assert.Contains("Visite ouverte", composant.Markup);
        Assert.Single(composant.FindAll(CloseButton));
        Assert.False(composant.Find(CloseButton).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Cloture_NonConfirmeeParLaRelecture_NEstPasPresenteeCommeReussie()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit();
        depot.Visits[visite.Id] = visite;
        depot.IgnoreSaves = true;
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(CloseButton).Click();

        composant.WaitForAssertion(() => Assert.Contains("n'a pas pu être confirmée", composant.Find("[role='alert']").TextContent));
        Assert.Empty(composant.FindAll(".report__success"));
    }

    [Fact]
    public async Task Cloture_AvantLaDateDeVisite_EstRefuseSansEcriture()
    {
        await using var contexte = CreateContext(out var depot);
        var visite = CreateVisit() with { Date = new DateOnly(2026, 10, 20) };
        depot.Visits[visite.Id] = visite;
        var composant = contexte.Render<VisitReport>(p => p.Add(c => c.VisitId, visite.Id));

        composant.Find(CloseButton).Click();

        composant.WaitForAssertion(() => Assert.Contains("avant sa date", composant.Find("[role='alert']").TextContent));
        Assert.Empty(depot.Saved);
        Assert.Empty(composant.FindAll(".report__success"));
    }

    private static Visit CreateVisit()
    {
        var questions = BiosecurityChecklistReference.Questions;
        var reponses = new[]
        {
            Respond(questions[0], Answer.No),
            Respond(questions[1], Answer.Yes)
        };
        return new Visit(VisitIdentifiant, FarmId, new DateOnly(2026, 10, 1), "Contrôle de routine fictif",
            "Notes fictives de visite.", null)
        {
            Actions =
            [
                new VisitAction(Guid.Parse("0c000000-0000-0000-0000-000000000001"), Guid.NewGuid(),
                    ActionType.PregnancyDiagnosis, new DateOnly(2026, 10, 1), true, "Réalisé"),
                new VisitAction(Guid.Parse("0c000000-0000-0000-0000-000000000002"), Guid.NewGuid(),
                    ActionType.DryOff, new DateOnly(2026, 10, 15), false, "")
            ],
            BiosecurityResponses = reponses
        };
    }

    private static BiosecurityResponse Respond(BiosecurityQuestion question, Answer answer) =>
        new(question.Id, question.Options.First(option => option.Value == answer).Id);

    private static BunitContext CreateContext(out FakeVisitRepository depot)
    {
        depot = new FakeVisitRepository();
        var contexte = new BunitContext();
        contexte.Services.AddLogging();
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(depot);
        return contexte;
    }

    private sealed class FailingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("module introuvable");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new JSException("module introuvable");
    }

    private sealed class FakeVisitRepository : IVisitRepository
    {
        public Dictionary<Guid, Visit> Visits { get; } = [];
        public List<Visit> Saved { get; } = [];
        public Func<Guid, Task<Visit?>>? GetHandler { get; set; }
        public Exception? SaveFailure { get; set; }
        public bool IgnoreSaves { get; set; }
        public int GetCalls { get; private set; }

        public Task<Visit?> GetAsync(Guid id)
        {
            GetCalls++;
            return GetHandler is not null
                ? GetHandler(id)
                : Task.FromResult(Visits.GetValueOrDefault(id));
        }

        public Task SaveAsync(Visit visit, VisitPhoto? photo = null)
        {
            if (SaveFailure is not null)
            {
                throw SaveFailure;
            }

            Saved.Add(visit);
            if (!IgnoreSaves)
            {
                Visits[visit.Id] = visit;
            }

            return Task.CompletedTask;
        }

        public Task InitializeDemoAsync(DemoSeed seed) => throw new NotSupportedException();
        public Task<IReadOnlyList<Visit>> GetAllAsync() => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task<DemoDataSet?> GetHerdAsync() => throw new NotSupportedException();
        public Task SaveHerdAsync(DemoDataSet herd) => throw new NotSupportedException();
        public Task<VisitPhoto?> GetPhotoAsync(Guid photoId)
        {
            PhotoCalls++;
            if (PhotoFailure is not null)
            {
                throw PhotoFailure;
            }

            return Task.FromResult(Photos.GetValueOrDefault(photoId));
        }

        public Dictionary<Guid, VisitPhoto> Photos { get; } = [];
        public Exception? PhotoFailure { get; set; }
        public int PhotoCalls { get; private set; }
        public Task SavePhotoAsync(Guid visitId, VisitPhoto photo) => throw new NotSupportedException();
        public Task DeletePhotoAsync(Guid visitId) => throw new NotSupportedException();
    }
}
