using Bunit;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Tests.Data;

public sealed class DemoInitializationRepositoryTests
{
    [Fact]
    public async Task Initialisation_RejetteUnJeuNullAvantLInterop()
    {
        using var contexte = new BunitContext();
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        await Assert.ThrowsAsync<ArgumentNullException>(() => depot.InitializeDemoAsync(null!));
        Assert.Empty(contexte.JSInterop.Invocations);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"value":false,"error":null}""")]
    public async Task Initialisation_RejetteUneConfirmationInvalide(string reponse)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true).SetResult(reponse);
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var erreur = await Assert.ThrowsAsync<VisitStorageException>(() =>
            depot.InitializeDemoAsync(DemoData.GenerateSeed(new DateOnly(2026, 10, 9), 42)));

        Assert.Equal(VisitStorageError.InvalidData, erreur.Code);
    }

    [Fact]
    public async Task Lecture_AncienneVisiteSansActionsResteCompatible()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getAll").SetResult("""
            {"value":[{"id":"11111111-1111-1111-1111-111111111111",
            "farmId":"22222222-2222-2222-2222-222222222222","date":"2026-10-09",
            "cause":"Visite fictive","notes":"Notes préservées","photoId":null}],"error":null}
            """);
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var visite = Assert.Single(await depot.GetAllAsync());

        Assert.Empty(visite.Actions);
        Assert.Equal("Notes préservées", visite.Notes);
    }

    [Fact]
    public async Task Lecture_RestitueExactementLesActionsEtLeursNotes()
    {
        using var contexte = new BunitContext();
        var jeu = DemoData.GenerateSeed(new DateOnly(2026, 10, 9), 42);
        var visite = jeu.Visits.First(v => v.Actions.Count == 5);
        var json = System.Text.Json.JsonSerializer.Serialize(visite, System.Text.Json.JsonSerializerOptions.Web);
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("get", visite.Id).SetResult($$"""{"value":{{json}},"error":null}""");
        await using var depot = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var resultat = await depot.GetAsync(visite.Id);

        Assert.NotNull(resultat);
        Assert.Equal(visite.Actions, resultat.Actions);
        Assert.Contains(resultat.Actions, action => action.Type == ActionType.Calving);
    }
}
