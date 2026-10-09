using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

public sealed class DailyRoundPageTests
{
    private const int Seed = 42;
    private static readonly DateOnly Aujourdhui = new(2026, 10, 9);
    private static readonly DateOnly Hier = Aujourdhui.AddDays(-1);
    private static readonly DateOnly Demain = Aujourdhui.AddDays(1);

    private const string ErreurAbsentes = "le troupeau de démonstration est absent ou n'est pas encore initialisé sur cet appareil";
    private const string ErreurIllisibles = "les données locales sont illisibles";

    private static string MessageAttendu(VisitStorageError code) => code switch
    {
        VisitStorageError.UnsupportedVersion => "le stockage local utilise une version plus récente",
        VisitStorageError.Unavailable => "le stockage local est indisponible ou refusé",
        _ => ErreurIllisibles
    };

    [Fact]
    public async Task Tournee_ContientUniquementLesElevagesAvecVisiteAujourdhui()
    {
        await using var contexte = CreateContext(out var depot);
        var elevages = depot.Herd!.Locations;
        depot.Visits.Add(NewVisit(1, elevages[0], Aujourdhui));
        depot.Visits.Add(NewVisit(2, elevages[1], Hier));
        depot.Visits.Add(NewVisit(3, elevages[2], Aujourdhui));
        depot.Visits.Add(NewVisit(4, elevages[3], Demain));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[data-farm-id]")));
        var ids = composant.FindAll("[data-farm-id]").Select(a => Guid.Parse(a.GetAttribute("data-farm-id")!)).ToHashSet();
        Assert.Equal(new HashSet<Guid> { elevages[0].Id, elevages[2].Id }, ids);
        Assert.Contains("2 élevage(s) prévu(s)", composant.Markup);
        Assert.Empty(composant.FindAll("[role='alert']"));
        Assert.Equal("2026-10-09", composant.Find("time").GetAttribute("datetime"));
    }

    [Fact]
    public async Task Tournee_UnElevageAvecPlusieursVisitesDuJour_ApparaitUneSeuleFois()
    {
        await using var contexte = CreateContext(out var depot);
        var elevage = depot.Herd!.Locations[0];
        depot.Visits.Add(NewVisit(1, elevage, Aujourdhui));
        depot.Visits.Add(NewVisit(2, elevage, Aujourdhui));
        depot.Visits.Add(NewVisit(3, elevage, Hier));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-farm-id]")));
        Assert.Contains("2 visite(s) prévue(s)", composant.Find(".round__meta").TextContent);
    }

    [Fact]
    public async Task Tournee_UtiliseLaDateDuTimeProvider()
    {
        await using var contexte = CreateContext(out var depot);
        var elevages = depot.Herd!.Locations;
        depot.Visits.Add(NewVisit(1, elevages[0], Aujourdhui));
        depot.Visits.Add(NewVisit(2, elevages[1], Demain));
        ((FakeTimeProvider)contexte.Services.GetRequiredService<TimeProvider>())
            .SetUtcNow(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        ((FakeTimeProvider)contexte.Services.GetRequiredService<TimeProvider>()).SetLocalTimeZone(TimeZoneInfo.Utc);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-farm-id]")));
        Assert.Equal(elevages[1].Id.ToString(), composant.Find("[data-farm-id]").GetAttribute("data-farm-id"));
    }

    [Fact]
    public async Task Tournee_SansVisiteAujourdhui_AfficheUnEtatVideExplicite()
    {
        await using var contexte = CreateContext(out var depot);
        depot.Visits.Add(NewVisit(1, depot.Herd!.Locations[0], Hier));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() =>
            Assert.Contains("Aucun élevage n'est prévu à cette date", composant.Markup));
        Assert.Empty(composant.FindAll("[data-farm-id]"));
        Assert.Empty(composant.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task Selection_OuvreLaFicheAvecLeBonIdentifiantNomEtVille()
    {
        await using var contexte = CreateContext(out var depot);
        var elevages = depot.Herd!.Locations;
        depot.Visits.Add(NewVisit(1, elevages[0], Aujourdhui));
        depot.Visits.Add(NewVisit(2, elevages[2], Aujourdhui));
        var choisi = elevages[2];
        var tournee = contexte.Render<DailyRoundPage>();
        tournee.WaitForAssertion(() => Assert.Equal(2, tournee.FindAll("[data-farm-id]").Count));

        var lien = tournee.Find($"[data-farm-id='{choisi.Id}']");
        Assert.Equal($"elevage/{choisi.Id}", lien.GetAttribute("href"));
        Assert.Contains(choisi.Name, lien.TextContent);
        Assert.Contains(choisi.City, lien.TextContent);
        var idDuLien = Guid.Parse(lien.GetAttribute("href")!["elevage/".Length..]);

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, idDuLien));

        fiche.WaitForAssertion(() => Assert.Equal(choisi.Name, fiche.Find("[data-testid='farm-name']").TextContent));
        Assert.Equal(choisi.City, fiche.Find("[data-testid='farm-city']").TextContent);
        Assert.Equal(choisi.Id.ToString(), fiche.Find("[data-testid='farm-id']").TextContent);
        Assert.DoesNotContain(elevages[0].Name, fiche.Markup);
        Assert.Empty(fiche.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task Fiche_ElevageInconnu_AfficheUneErreurSansAutreFiche()
    {
        await using var contexte = CreateContext(out var depot);

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, Guid.Parse("0f000000-0000-0000-0000-0000000000ff")));

        fiche.WaitForAssertion(() => Assert.Contains("Fiche introuvable", fiche.Find("[role='alert']").TextContent));
        Assert.Empty(fiche.FindAll("[data-testid='farm-name']"));
        foreach (var elevage in depot.Herd!.Locations)
        {
            Assert.DoesNotContain(elevage.Name, fiche.Markup);
        }
    }

    [Fact]
    public async Task Fiche_DonneesAbsentes_AfficheUneErreur()
    {
        await using var contexte = CreateContext(out var depot);
        depot.Herd = null;

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, Guid.NewGuid()));

        fiche.WaitForAssertion(() => Assert.Contains("Fiche introuvable", fiche.Find("[role='alert']").TextContent));
        Assert.Empty(fiche.FindAll("[data-testid='farm-name']"));
    }

    [Fact]
    public async Task Fiche_DonneesIllisibles_AfficheUneErreurFrancaise()
    {
        await using var contexte = CreateContext(out var depot);
        depot.HerdFailure = new VisitStorageException(VisitStorageError.InvalidData, "Données invalides.", "détail simulé");

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, Guid.NewGuid()));

        fiche.WaitForAssertion(() => Assert.Contains(ErreurIllisibles, fiche.Find("[role='alert']").TextContent));
        Assert.Empty(fiche.FindAll("[data-testid='farm-name']"));
    }

    [Fact]
    public async Task Tournee_DonneesLocalesAbsentes_AfficheErreurEtPasUneTourneeVide()
    {
        await using var contexte = CreateContext(out var depot);
        depot.Herd = null;

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(ErreurAbsentes, composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("Aucun élevage n'est prévu", composant.Markup);
        Assert.Empty(composant.FindAll("[data-farm-id]"));
        Assert.Contains("Ce chargement n'a effacé aucune donnée", composant.Find("[role='alert']").TextContent);
        Assert.Contains("Réessayer", composant.Find("button").TextContent);
        Assert.DoesNotContain(composant.FindAll("button"), b => b.TextContent.Contains("Supprimer") || b.TextContent.Contains("Réinitialiser"));
    }

    [Theory]
    [InlineData(VisitStorageError.Unavailable)]
    [InlineData(VisitStorageError.InvalidData)]
    [InlineData(VisitStorageError.UnsupportedVersion)]
    public async Task Tournee_LectureDuHeptelEnEchec_AfficheErreurIllisible(VisitStorageError code)
    {
        await using var contexte = CreateContext(out var depot);
        depot.HerdFailure = new VisitStorageException(code, "Lecture impossible.", "détail simulé");

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(MessageAttendu(code), composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("Aucun élevage n'est prévu", composant.Markup);
        Assert.Empty(composant.FindAll("[data-farm-id]"));
    }

    [Fact]
    public async Task Tournee_LectureDesVisitesEnEchec_AfficheErreurIllisible()
    {
        await using var contexte = CreateContext(out var depot);
        depot.VisitsFailure = new VisitStorageException(VisitStorageError.InvalidData, "Lecture impossible.");

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(ErreurIllisibles, composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("Aucun élevage n'est prévu", composant.Markup);
        Assert.Empty(composant.FindAll("[data-farm-id]"));
    }

    [Fact]
    public async Task Tournee_VisiteDUnElevageInconnu_AfficheErreurIncoherenteSansTourneeVide()
    {
        await using var contexte = CreateContext(out var depot);
        var inconnu = new Location(Guid.Parse("0f000000-0000-0000-0000-0000000000aa"), "Ferme Inconnue", "Nulle-Part", 1);
        depot.Visits.Add(NewVisit(1, inconnu, Aujourdhui));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains("incohérentes", composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("Aucun élevage n'est prévu", composant.Markup);
        Assert.Empty(composant.FindAll("[data-farm-id]"));
    }

    [Fact]
    public async Task Tournee_Reessayer_ApresErreurAfficheLaTournee()
    {
        await using var contexte = CreateContext(out var depot);
        var elevage = depot.Herd!.Locations[0];
        depot.Visits.Add(NewVisit(1, elevage, Aujourdhui));
        depot.HerdFailure = new VisitStorageException(VisitStorageError.Unavailable, "Lecture impossible.");
        var composant = contexte.Render<DailyRoundPage>();
        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[role='alert']")));

        depot.HerdFailure = null;
        composant.Find("button").Click();

        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-farm-id]")));
        Assert.Empty(composant.FindAll("[role='alert']"));
    }

    private const string MessageSensible = "SECRET-MESSAGE-EXCEPTION-7731";
    private const string DetailSensible = "SECRET-DETAIL-STOCKAGE-4419";

    [Theory]
    [InlineData(VisitStorageError.Unavailable)]
    [InlineData(VisitStorageError.InvalidData)]
    [InlineData(VisitStorageError.UnsupportedVersion)]
    public async Task Tournee_ErreurDeStockage_NeRevelePasLeMessageNiLeDetailDeLException(VisitStorageError code)
    {
        await using var contexte = CreateContext(out var depot);
        depot.HerdFailure = new VisitStorageException(code, MessageSensible, DetailSensible);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(MessageAttendu(code), composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain(MessageSensible, composant.Markup);
        Assert.DoesNotContain(DetailSensible, composant.Markup);
        Assert.Contains("Réessayer", composant.Find("button").TextContent);
    }

    [Fact]
    public async Task Tournee_ErreurSurLesVisites_NeRevelePasLeMessageDeLException()
    {
        await using var contexte = CreateContext(out var depot);
        depot.VisitsFailure = new VisitStorageException(VisitStorageError.InvalidData, MessageSensible, DetailSensible);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(ErreurIllisibles, composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain(MessageSensible, composant.Markup);
        Assert.DoesNotContain(DetailSensible, composant.Markup);
        Assert.Contains("Réessayer", composant.Find("button").TextContent);
    }

    [Fact]
    public async Task Tournee_ReessayerApresErreurSensible_NeRevelePasLeMessageEtRetabliLaTournee()
    {
        await using var contexte = CreateContext(out var depot);
        depot.Visits.Add(NewVisit(1, depot.Herd!.Locations[0], Aujourdhui));
        depot.HerdFailure = new VisitStorageException(VisitStorageError.Unavailable, MessageSensible, DetailSensible);
        var composant = contexte.Render<DailyRoundPage>();
        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[role='alert']")));

        composant.Find("button").Click();

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.FindAll("[role='alert']")));
        Assert.DoesNotContain(MessageSensible, composant.Markup);
        Assert.DoesNotContain(DetailSensible, composant.Markup);
        depot.HerdFailure = null;
        composant.Find("button").Click();
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("[data-farm-id]")));
    }

    [Theory]
    [InlineData(VisitStorageError.Unavailable)]
    [InlineData(VisitStorageError.InvalidData)]
    [InlineData(VisitStorageError.UnsupportedVersion)]
    public async Task Fiche_ErreurDeStockage_NeRevelePasLeMessageNiLeDetailDeLException(VisitStorageError code)
    {
        await using var contexte = CreateContext(out var depot);
        depot.HerdFailure = new VisitStorageException(code, MessageSensible, DetailSensible);

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, Guid.NewGuid()));

        fiche.WaitForAssertion(() => Assert.Contains(MessageAttendu(code), fiche.Find("[role='alert']").TextContent));
        Assert.DoesNotContain(MessageSensible, fiche.Markup);
        Assert.DoesNotContain(DetailSensible, fiche.Markup);
        Assert.Empty(fiche.FindAll("[data-testid='farm-name']"));
    }

    private const string MessageInterneSensible = "SECRET-INNER-EXCEPTION-9052";
    private const string MessageArgument = "Une visite du jour référence un élevage inconnu.";

    private static VisitStorageException ErreurSensible(VisitStorageError code) =>
        new(code, MessageSensible, DetailSensible, new InvalidOperationException(MessageInterneSensible));

    private static void AssertJournalisationSure(CapturingLoggerProvider journal)
    {
        Assert.NotEmpty(journal.Entries);
        foreach (var entree in journal.Entries)
        {
            Assert.Null(entree.Exception);
            foreach (var secret in new[] { MessageSensible, DetailSensible, MessageInterneSensible })
            {
                Assert.DoesNotContain(secret, entree.Message);
                Assert.DoesNotContain(secret, entree.StateText);
            }
        }
    }

    [Theory]
    [InlineData(VisitStorageError.Unavailable)]
    [InlineData(VisitStorageError.InvalidData)]
    [InlineData(VisitStorageError.UnsupportedVersion)]
    public async Task Tournee_ErreurDeStockage_NeJournalisePasMessageDetailNiExceptionInterne(VisitStorageError code)
    {
        var journal = new CapturingLoggerProvider();
        await using var contexte = CreateContext(out var depot, journal);
        depot.HerdFailure = ErreurSensible(code);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(MessageAttendu(code), composant.Find("[role='alert']").TextContent));
        AssertJournalisationSure(journal);
        Assert.Contains(journal.Entries, e => e.StateText.Contains(code.ToString()));
        Assert.Contains(journal.Entries, e => e.StateText.Contains(nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task Tournee_ErreurSurLesVisites_NeJournalisePasMessageDetailNiExceptionInterne()
    {
        var journal = new CapturingLoggerProvider();
        await using var contexte = CreateContext(out var depot, journal);
        depot.VisitsFailure = ErreurSensible(VisitStorageError.InvalidData);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains(ErreurIllisibles, composant.Find("[role='alert']").TextContent));
        AssertJournalisationSure(journal);
        Assert.Contains(journal.Entries, e => e.StateText.Contains(nameof(VisitStorageError.InvalidData)));
    }

    [Theory]
    [InlineData(VisitStorageError.Unavailable)]
    [InlineData(VisitStorageError.InvalidData)]
    [InlineData(VisitStorageError.UnsupportedVersion)]
    public async Task Fiche_ErreurDeStockage_NeJournalisePasMessageDetailNiExceptionInterne(VisitStorageError code)
    {
        var journal = new CapturingLoggerProvider();
        await using var contexte = CreateContext(out var depot, journal);
        depot.HerdFailure = ErreurSensible(code);

        var fiche = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, Guid.NewGuid()));

        fiche.WaitForAssertion(() => Assert.Contains(MessageAttendu(code), fiche.Find("[role='alert']").TextContent));
        AssertJournalisationSure(journal);
        Assert.Contains(journal.Entries, e => e.StateText.Contains(code.ToString()));
        Assert.Contains(journal.Entries, e => e.StateText.Contains(nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task Tournee_ArgumentException_NeJournalisePasLeMessageNiLException()
    {
        var journal = new CapturingLoggerProvider();
        await using var contexte = CreateContext(out var depot, journal);
        var inconnu = new Location(Guid.Parse("0f000000-0000-0000-0000-0000000000aa"), "Ferme Inconnue", "Nulle-Part", 1);
        depot.Visits.Add(NewVisit(1, inconnu, Aujourdhui));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains("incohérentes", composant.Find("[role='alert']").TextContent));
        Assert.NotEmpty(journal.Entries);
        foreach (var entree in journal.Entries)
        {
            Assert.Null(entree.Exception);
            Assert.DoesNotContain(MessageArgument, entree.Message);
            Assert.DoesNotContain(MessageArgument, entree.StateText);
            Assert.DoesNotContain("visits", entree.StateText);
            Assert.DoesNotContain(inconnu.Name, entree.Message);
        }
    }

    private sealed record JournalEntry(string Message, string StateText, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<JournalEntry> _entries = [];

        public IReadOnlyList<JournalEntry> Entries
        {
            get { lock (_entries) { return _entries.ToArray(); } }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var valeurs = state is IEnumerable<KeyValuePair<string, object?>> paires
                    ? string.Join(";", paires.Select(p => $"{p.Key}={p.Value}"))
                    : state?.ToString() ?? string.Empty;
                lock (owner._entries)
                {
                    owner._entries.Add(new JournalEntry(formatter(state, exception), valeurs, exception));
                }
            }
        }
    }

    private static Visit NewVisit(int numero, Location elevage, DateOnly date) =>
        new(Guid.Parse($"0a000000-0000-0000-0000-{numero:D12}"), elevage.Id, date, "Contrôle", "Notes fictives", null);

    private static BunitContext CreateContext(out FakeVisitRepository depot, ILoggerProvider? journal = null)
    {
        depot = new FakeVisitRepository
        {
            Herd = DemoData.Generate(Aujourdhui, Seed)
        };
        var contexte = new BunitContext();
        contexte.Services.AddLogging();
        if (journal is not null)
        {
            contexte.Services.AddSingleton<ILoggerProvider>(journal);
        }

        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(depot);
        return contexte;
    }

    private sealed class FakeVisitRepository : IVisitRepository
    {
        public DemoDataSet? Herd { get; set; }
        public List<Visit> Visits { get; } = [];
        public VisitStorageException? HerdFailure { get; set; }
        public VisitStorageException? VisitsFailure { get; set; }

        public Task<DemoDataSet?> GetHerdAsync() =>
            HerdFailure is null ? Task.FromResult(Herd) : Task.FromException<DemoDataSet?>(HerdFailure);

        public Task<IReadOnlyList<Visit>> GetAllAsync() =>
            VisitsFailure is null
                ? Task.FromResult<IReadOnlyList<Visit>>(Visits.ToArray())
                : Task.FromException<IReadOnlyList<Visit>>(VisitsFailure);

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
