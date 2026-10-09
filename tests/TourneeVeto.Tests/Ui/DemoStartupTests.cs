using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TourneeVeto.Domain;
using TourneeVeto.Ui.Components;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Tests.Ui;

public sealed class DemoStartupTests
{
    [Fact]
    public async Task Chargement_NePresentePasLesDonneesCommeDisponibles()
    {
        await using var contexte = CreateContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true);

        var composant = contexte.Render<DemoStartup>();

        Assert.Equal("true", composant.Find("section").GetAttribute("aria-busy"));
        Assert.Contains("Chargement", composant.Find("[role='status']").TextContent);
        Assert.Empty(composant.FindAll("li"));
        Assert.DoesNotContain("enregistrées localement", composant.Markup);
    }

    [Fact]
    public async Task Succes_AfficheLesDonneesReluesEtLesLimitesDeConservation()
    {
        await using var contexte = CreateContext();
        SetupSuccess(contexte);

        var composant = contexte.Render<DemoStartup>();

        composant.WaitForAssertion(() => Assert.Equal(5, composant.FindAll("li").Count));
        Assert.Contains("enregistrées localement sur cet appareil", composant.Find("[role='status']").TextContent);
        Assert.Contains("15 visites", composant.Markup);
        Assert.Contains("30 actions", composant.Markup);
        Assert.Contains("Toutes les données sont fictives", composant.Markup);
        Assert.Contains("navigation privée", composant.Markup);
        Assert.Contains("ne constitue pas une sauvegarde", composant.Markup);
        Assert.Equal("false", composant.Find("section").GetAttribute("aria-busy"));
        Assert.Empty(composant.FindAll("button"));
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("quota")]
    [InlineData("invalid")]
    public async Task Erreur_EstAccessibleSansDonneesNiFausseConfirmation(string code)
    {
        await using var contexte = CreateContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true)
            .SetResult($$$"""{"value":null,"error":{"code":"{{{code}}}","detail":"échec simulé"}}""");

        var composant = contexte.Render<DemoStartup>();

        composant.WaitForAssertion(() => Assert.NotEmpty(composant.Find("[role='alert']").TextContent));
        Assert.Empty(composant.FindAll("li"));
        Assert.DoesNotContain("enregistrées localement sur cet appareil", composant.Markup);
        Assert.Contains("Réessayer", composant.Find("button").TextContent);
    }

    [Fact]
    public async Task Reessayer_DeclencheUnNouveauChargementEtDesactiveLeBoutonPendantLAttente()
    {
        await using var contexte = CreateContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true)
            .SetResult("""{"value":null,"error":{"code":"unavailable","detail":"refus"}}""");
        var composant = contexte.Render<DemoStartup>();
        composant.WaitForAssertion(() => Assert.Single(composant.FindAll("button")));
        var retry = module.Setup<string>("initializeDemo", _ => true);

        composant.Find("button").Click();

        Assert.True(composant.Find("button").HasAttribute("disabled"));
        Assert.Equal("true", composant.Find("section").GetAttribute("aria-busy"));
        SetupReads(module);
        retry.SetResult("""{"value":true,"error":null}""");
        composant.WaitForAssertion(() => Assert.Equal(5, composant.FindAll("li").Count));
        Assert.Equal(2, module.Invocations["initializeDemo"].Count);
    }

    [Fact]
    public async Task Rendu_UtiliseDesIdentifiantsDeTitreUniques()
    {
        await using var contexte = CreateContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true);
        var premier = contexte.Render<DemoStartup>();
        var second = contexte.Render<DemoStartup>();

        Assert.NotEqual(premier.Find("h2").Id, second.Find("h2").Id);
    }

    private static BunitContext CreateContext()
    {
        var contexte = new BunitContext();
        contexte.Services.AddLogging();
        contexte.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
        contexte.Services.AddScoped<IVisitRepository, IndexedDbVisitRepository>();
        contexte.Services.AddScoped<DemoStartupService>();
        return contexte;
    }

    private static void SetupSuccess(BunitContext contexte)
    {
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("initializeDemo", _ => true).SetResult("""{"value":true,"error":null}""");
        SetupReads(module);
    }

    private static void SetupReads(BunitJSModuleInterop module)
    {
        var jeu = DemoData.GenerateSeed(new DateOnly(2026, 10, 9), 42);
        module.Setup<string>("getHerd").SetResult(Enveloppe(jeu.Herd));
        module.Setup<string>("getAll").SetResult(Enveloppe(jeu.Visits));
    }

    private static string Enveloppe<T>(T valeur) =>
        JsonSerializer.Serialize(new { value = valeur, error = (string?)null }, JsonSerializerOptions.Web);
}
