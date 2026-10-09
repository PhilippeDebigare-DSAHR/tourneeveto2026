using System.Text.Json;
using Bunit;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Tests.Data;

public sealed class DemoStartupServiceTests
{
    [Fact]
    public async Task Demarrage_UtiliseLaDateInjecteePuisRelitLesDonneesLocales()
    {
        using var contexte = new BunitContext();
        var date = new DateOnly(2026, 10, 9);
        var jeu = DemoData.GenerateSeed(date, 42);
        var horloge = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        horloge.SetLocalTimeZone(TimeZoneInfo.Utc);
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true).SetResult("""{"value":true,"error":null}""");
        module.Setup<string>("getHerd").SetResult(Enveloppe(jeu.Herd));
        module.Setup<string>("getAll").SetResult(Enveloppe(jeu.Visits));
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var service = new DemoStartupService(depot, horloge);

        var resultat = await service.LoadAsync();

        Assert.Equal(jeu.Herd.Locations, resultat.Herd.Locations);
        Assert.Equal(15, resultat.Visits.Count);
        var appel = Assert.Single(module.Invocations["initializeDemo"]);
        using var json = JsonDocument.Parse(Assert.IsType<string>(appel.Arguments[0]));
        Assert.Equal("2026-10-09", json.RootElement.GetProperty("referenceDate").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("seed").GetInt32());
        Assert.Equal(15, json.RootElement.GetProperty("visits").GetArrayLength());
        Assert.Single(module.Invocations["getHerd"]);
        Assert.Single(module.Invocations["getAll"]);
    }

    [Theory]
    [InlineData("unavailable", VisitStorageError.Unavailable)]
    [InlineData("quota", VisitStorageError.QuotaExceeded)]
    [InlineData("invalid", VisitStorageError.InvalidData)]
    public async Task Demarrage_PropageLEchecSansRetournerLeJeuEnMemoire(string code, VisitStorageError attendu)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true)
            .SetResult($$$"""{"value":null,"error":{"code":"{{{code}}}","detail":"échec simulé"}}""");
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var horloge = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var service = new DemoStartupService(depot, horloge);

        var erreur = await Assert.ThrowsAsync<VisitStorageException>(service.LoadAsync);

        Assert.Equal(attendu, erreur.Code);
        Assert.Empty(module.Invocations["getHerd"]);
    }

    [Fact]
    public async Task Demarrage_RejetteUnTroupeauAbsentApresInitialisation()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true).SetResult("""{"value":true,"error":null}""");
        module.Setup<string>("getHerd").SetResult("""{"value":null,"error":null}""");
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var horloge = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));

        var erreur = await Assert.ThrowsAsync<VisitStorageException>(() =>
            new DemoStartupService(depot, horloge).LoadAsync());

        Assert.Equal(VisitStorageError.InvalidData, erreur.Code);
    }

    private static string Enveloppe<T>(T valeur) =>
        JsonSerializer.Serialize(new { value = valeur, error = (string?)null }, JsonSerializerOptions.Web);
}
