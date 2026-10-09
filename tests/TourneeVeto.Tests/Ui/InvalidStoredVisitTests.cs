using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Ui.Data;
using TourneeVeto.Ui.Pages;

namespace TourneeVeto.Tests.Ui;

/// <summary>S03 : un document visite local qui rompt un invariant devient VisitStorageException(InvalidData).</summary>
public sealed class InvalidStoredVisitTests
{
    private const string Prefixe = """
        "id":"11111111-1111-1111-1111-111111111111","farmId":"22222222-2222-2222-2222-222222222222",
        "date":"2026-10-09","cause":"SECRET-CAUSE-5521","notes":"Notes fictives","photoId":null
        """;

    public static TheoryData<string> DocumentsInvalides => new()
    {
        $$"""{ {{Prefixe}},"isClosed":true,"closedOn":"2026-10-08" }""",
        $$"""{ {{Prefixe}},"biosecurityResponses":[null] }""",
    };

    private static string Enveloppe(string visite, bool liste) =>
        liste ? $$"""{"value":[{{visite}}],"error":null}""" : $$"""{"value":{{visite}},"error":null}""";

    [Theory]
    [MemberData(nameof(DocumentsInvalides))]
    public async Task Lecture_InvarianteInvalide_LeveVisitStorageExceptionInvalidData(string visite)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        module.Setup<string>("getAll").SetResult(Enveloppe(visite, liste: true));
        module.Setup<string>("get", id).SetResult(Enveloppe(visite, liste: false));
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var tout = await Assert.ThrowsAsync<VisitStorageException>(() => depot.GetAllAsync());
        var un = await Assert.ThrowsAsync<VisitStorageException>(() => depot.GetAsync(id));

        foreach (var erreur in new[] { tout, un })
        {
            Assert.Equal(VisitStorageError.InvalidData, erreur.Code);
            Assert.Null(erreur.InnerException);
            Assert.DoesNotContain("SECRET-CAUSE-5521", erreur.Message + erreur.Detail);
            Assert.DoesNotContain("ClosedOn", erreur.Message + erreur.Detail);
        }
    }

    [Theory]
    [MemberData(nameof(DocumentsInvalides))]
    public async Task Tournee_DocumentInvalide_AfficheAlerteFixeSansDivulguerLesDetails(string visite)
    {
        var journal = new List<string>();
        await using var contexte = new BunitContext();
        var herd = JsonSerializer.Serialize(DemoData.Generate(new DateOnly(2026, 10, 9), 42), JsonSerializerOptions.Web);
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getHerd").SetResult($$"""{"value":{{herd}},"error":null}""");
        module.Setup<string>("getAll").SetResult(Enveloppe(visite, liste: true));
        contexte.Services.AddLogging(b => b.AddProvider(new Provider(journal)));
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddSingleton<IVisitRepository>(
            new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime));

        var composant = contexte.Render<DailyRoundPage>();

        composant.WaitForAssertion(() => Assert.Contains("les données locales sont illisibles",
            composant.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("SECRET-CAUSE-5521", composant.Markup);
        Assert.DoesNotContain("ArgumentOutOfRange", composant.Markup);
        Assert.DoesNotContain("ClosedOn", composant.Markup);
        Assert.DoesNotContain("incohérentes", composant.Find("[role='alert']").TextContent);
        Assert.Contains("Réessayer", composant.Find("button").TextContent);
        Assert.Empty(composant.FindAll("[data-farm-id]"));
        Assert.All(journal, ligne => Assert.DoesNotContain("SECRET-CAUSE-5521", ligne));
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
