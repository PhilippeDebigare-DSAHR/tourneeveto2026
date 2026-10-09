using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

/// <summary>S03 : troupeau invalide et messages fixes par code d'erreur de stockage, via le vrai dépôt (JS simulé).</summary>
public sealed class HerdAndStorageErrorTests
{
    private const string Secret = "SECRET-DETAIL-9917";
    private static readonly Guid FarmId = DemoData.Generate(new DateOnly(2026, 10, 9), 42).Locations[0].Id;

    public static TheoryData<string> TroupeauxInvalides => new() { "locationsNull", "elementNull", "locationsAbsent" };

    private static string Troupeau(string cas)
    {
        var noeud = JsonSerializer.SerializeToNode(DemoData.Generate(new DateOnly(2026, 10, 9), 42), JsonSerializerOptions.Web)!.AsObject();
        switch (cas)
        {
            case "locationsNull": noeud["locations"] = null; break;
            case "elementNull": noeud["locations"]!.AsArray()[0] = null; break;
            case "locationsAbsent": noeud.Remove("locations"); break;
        }

        return noeud.ToJsonString();
    }

    private static string Enveloppe(string valeur) => $$"""{"value":{{valeur}},"error":null}""";

    private static string Erreur(string code) =>
        $$$"""{"value":null,"error":{"code":"{{{code}}}","detail":"{{{Secret}}}"}}""";

    private static BunitContext Contexte(string reponseGetHerd, List<string> journal)
    {
        var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getHerd").SetResult(reponseGetHerd);
        module.Setup<string>("getAll").SetResult(Enveloppe("[]"));
        contexte.Services.AddLogging(b => b.AddProvider(new Provider(journal)));
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime));
        return contexte;
    }

    [Theory]
    [MemberData(nameof(TroupeauxInvalides))]
    public async Task GetHerd_TroupeauInvalide_LeveVisitStorageExceptionInvalidData(string cas)
    {
        using var contexte = Contexte(Enveloppe(Troupeau(cas)), []);
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var erreur = await Assert.ThrowsAsync<VisitStorageException>(() => depot.GetHerdAsync());

        Assert.Equal(VisitStorageError.InvalidData, erreur.Code);
    }

    [Theory]
    [MemberData(nameof(TroupeauxInvalides))]
    public async Task Tournee_TroupeauInvalide_AfficheAlerteFixeSansExceptionBrute(string cas)
    {
        var journal = new List<string>();
        await using var contexte = Contexte(Enveloppe(Troupeau(cas)), journal);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains("illisibles", composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("NullReference", composant.Markup);
        Assert.DoesNotContain("ArgumentNull", composant.Markup);
        Assert.All(journal, l => Assert.DoesNotContain("NullReference", l));
        Assert.All(journal, l => Assert.DoesNotContain("ArgumentNullException", l));
    }

    [Theory]
    [MemberData(nameof(TroupeauxInvalides))]
    public async Task Fiche_TroupeauInvalide_AfficheAlerteFixeSansExceptionBrute(string cas)
    {
        var journal = new List<string>();
        await using var contexte = Contexte(Enveloppe(Troupeau(cas)), journal);

        var composant = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, FarmId));

        composant.WaitForAssertion(() => Assert.Contains("illisibles", composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("NullReference", composant.Markup);
        Assert.DoesNotContain("ArgumentNull", composant.Markup);
        Assert.All(journal, l => Assert.DoesNotContain("NullReference", l));
    }

    public static TheoryData<string, string> CodesEtMessages => new()
    {
        { "blocked", "la mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto" },
        { "version", "le stockage local utilise une version plus récente. Mettez l'application à jour." },
        { "quota", "le quota de stockage local est dépassé" },
        { "unavailable", "le stockage local est indisponible ou refusé" },
        { "invalid", "les données locales sont illisibles" },
        { "inconnu", "l'accès au stockage local a échoué" },
    };

    private static void VerifierAlerte(AngleSharp.Dom.IElement alerte, string markup, string attendu, List<string> journal, string code)
    {
        Assert.Contains(attendu, alerte.TextContent);
        Assert.DoesNotContain(Secret, markup);
        Assert.All(journal, l => Assert.DoesNotContain(Secret, l));
        var texte = alerte.TextContent.ToLowerInvariant();
        foreach (var interdit in new[] { "effacer", "supprimer", "vider", "réinitialiser", "purger" })
        {
            Assert.DoesNotContain(interdit, texte);
        }

        if (code == "version")
        {
            Assert.DoesNotContain("fermez", texte);
        }
    }

    [Theory]
    [MemberData(nameof(CodesEtMessages))]
    public async Task Tournee_CodeDeStockage_AfficheMessageFrancaisFixe(string code, string attendu)
    {
        var journal = new List<string>();
        await using var contexte = Contexte(Erreur(code), journal);

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains("Impossible de charger la tournée", composant.Find("[role='alert']").TextContent));
        VerifierAlerte(composant.Find("[role='alert']"), composant.Markup, attendu, journal, code);
    }

    [Theory]
    [MemberData(nameof(CodesEtMessages))]
    public async Task Fiche_CodeDeStockage_AfficheMessageFrancaisFixe(string code, string attendu)
    {
        var journal = new List<string>();
        await using var contexte = Contexte(Erreur(code), journal);

        var composant = contexte.Render<FarmSheet>(p => p.Add(c => c.FarmId, FarmId));

        composant.WaitForAssertion(() => Assert.Contains("Impossible de charger la fiche", composant.Find("[role='alert']").TextContent));
        VerifierAlerte(composant.Find("[role='alert']"), composant.Markup, attendu, journal, code);
    }

    private sealed class Provider(List<string> lignes) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(lignes);
        public void Dispose() { }

        private sealed class Logger(List<string> lignes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (lignes)
                {
                    lignes.Add(formatter(state, exception) + exception);
                }
            }
        }
    }
}
