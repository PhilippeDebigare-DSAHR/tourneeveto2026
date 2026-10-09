using System.Text.Json;
using Bunit;
using Microsoft.JSInterop;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Tests.Data;

public sealed class IndexedDbVisitRepositoryTests
{
    private static readonly Guid VisitId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FarmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PhotoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Visit Example = new(VisitId, FarmId, new DateOnly(2026, 10, 9),
        "Visite fictive", "Observation de démonstration", null);

    [Fact]
    public async Task Import_EstDiffereUniqueEtPartageEntreAppelsConcurrents()
    {
        using var contexte = new BunitContext();
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        Assert.Empty(contexte.JSInterop.Invocations);
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        var invocation = module.Setup<string>("getAll");

        var premier = repository.GetAllAsync();
        var second = repository.GetAllAsync();
        invocation.SetResult("""{"value":[],"error":null}""");
        await Task.WhenAll(premier, second);

        Assert.Single(contexte.JSInterop.Invocations["import"]);
    }

    [Fact]
    public async Task Lecture_AbsenteRetourneNullSansConfondreUneErreur()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("get", VisitId).SetResult("""{"value":null,"error":null}""");
        module.Setup<string>("getHerd").SetResult("""{"value":null,"error":null}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        Assert.Null(await repository.GetAsync(VisitId));
        Assert.Null(await repository.GetHerdAsync());
    }

    [Fact]
    public async Task Lecture_RestitueLesDatesEtLesTextesFrancais()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        var value = JsonSerializer.Serialize(Example, JsonSerializerOptions.Web);
        module.Setup<string>("get", VisitId).SetResult($$"""{"value":{{value}},"error":null}""");
        module.Setup<string>("getAll").SetResult($$"""{"value":[{{value}}],"error":null}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        Assert.Equivalent(Example, await repository.GetAsync(VisitId), strict: true);
        Assert.Equivalent(Example, Assert.Single(await repository.GetAllAsync()), strict: true);
    }

    [Fact]
    public async Task Sauvegarde_UtiliseCamelCaseDateOnlyEtPhotoBinaireSeparee()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("save", _ => true).SetResult("""{"value":true,"error":null}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var photo = new VisitPhoto(PhotoId, "image/png", [0, 127, 128, 255]);

        await repository.SaveAsync(Example with { PhotoId = PhotoId }, photo);

        var call = Assert.Single(module.Invocations["save"]);
        using var document = JsonDocument.Parse(Assert.IsType<string>(call.Arguments[0]));
        var root = document.RootElement;
        Assert.Equal("2026-10-09", root.GetProperty("date").GetString());
        Assert.Equal(VisitId, root.GetProperty("id").GetGuid());
        Assert.Equal(PhotoId, root.GetProperty("photoId").GetGuid());
        Assert.Equal(Example.Notes, root.GetProperty("notes").GetString());
        Assert.False(root.TryGetProperty("Date", out _));
        Assert.False(root.TryGetProperty("data", out _));
        Assert.Equal(photo.Data, Assert.IsType<byte[]>(call.Arguments[1]));
        Assert.Equal("image/png", call.Arguments[2]);
    }

    [Fact]
    public async Task Troupeau_PreserveLesRelationsLesDatesNullablesEtLeSeed()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("saveHerd", _ => true).SetResult("""{"value":true,"error":null}""");
        var herd = DemoData.Generate(new DateOnly(2026, 10, 9), seed: 42);
        var json = JsonSerializer.Serialize(herd, JsonSerializerOptions.Web);
        module.Setup<string>("getHerd").SetResult($$"""{"value":{{json}},"error":null}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        await repository.SaveHerdAsync(herd);
        var result = await repository.GetHerdAsync();

        Assert.NotNull(result);
        Assert.Equal(herd.Locations, result.Locations);
        Assert.Equal(herd.Cows, result.Cows);
        using var document = JsonDocument.Parse(
            Assert.IsType<string>(Assert.Single(module.Invocations["saveHerd"]).Arguments[0]));
        Assert.Equal(500, document.RootElement.GetProperty("cows").GetArrayLength());
        Assert.Equal(5, document.RootElement.GetProperty("locations").GetArrayLength());
    }

    [Theory]
    [InlineData("quota", VisitStorageError.QuotaExceeded)]
    [InlineData("unavailable", VisitStorageError.Unavailable)]
    [InlineData("blocked", VisitStorageError.UpgradeBlocked)]
    [InlineData("version", VisitStorageError.UnsupportedVersion)]
    [InlineData("invalid", VisitStorageError.InvalidData)]
    [InlineData("unknown", VisitStorageError.Unknown)]
    public async Task Echec_EstExpliciteEtConserveLeDetail(string code, VisitStorageError expected)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("save", _ => true)
            .SetResult($$$"""{"value":null,"error":{"code":"{{{code}}}","detail":"refus simulé"}}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.SaveAsync(Example));

        Assert.Equal(expected, error.Code);
        Assert.Equal("refus simulé", error.Detail);
        Assert.NotEmpty(error.Message);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("document illisible")]
    [InlineData("""{"value":false,"error":null}""")]
    public async Task ReponseInvalide_NeDevientJamaisUnSucces(string response)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("save", _ => true).SetResult(response);
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.SaveAsync(Example));

        Assert.Equal(VisitStorageError.InvalidData, error.Code);
    }

    [Fact]
    public async Task InteropEchoue_ConserveLExceptionSansFauxSucces()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getAll").SetException(new JSException("Module indisponible"));
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.GetAllAsync());

        Assert.Equal(VisitStorageError.Unavailable, error.Code);
        Assert.IsType<JSException>(error.InnerException);
        await Assert.ThrowsAsync<VisitStorageException>(() => repository.GetAllAsync());
        Assert.Single(contexte.JSInterop.Invocations["import"]);
        Assert.Equal(2, module.Invocations["getAll"].Count);
    }

    [Fact]
    public async Task ImportEchoue_PeutEtreRetenteSansCacheDeLEchec()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getAll").SetResult("""{"value":[],"error":null}""");
        await using var repository = new IndexedDbVisitRepository(
            new FailFirstImportRuntime(contexte.JSInterop.JSRuntime));

        var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.GetAllAsync());
        Assert.Equal(VisitStorageError.Unavailable, error.Code);
        Assert.Empty(await repository.GetAllAsync());
        Assert.Single(contexte.JSInterop.Invocations["import"]);
    }

    [Fact]
    public async Task Suppression_TransmetLIdentifiant()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("deleteVisit", VisitId).SetResult("""{"value":true,"error":null}""");
        module.Setup<string>("savePhoto", _ => true).SetResult("""{"value":true,"error":null}""");
        module.Setup<string>("deletePhoto", VisitId).SetResult("""{"value":true,"error":null}""");
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var photo = new VisitPhoto(PhotoId, "image/jpeg", [1, 2, 255]);

        await repository.SavePhotoAsync(VisitId, photo);
        await repository.DeletePhotoAsync(VisitId);
        await repository.DeleteAsync(VisitId);

        var call = Assert.Single(module.Invocations["savePhoto"]);
        Assert.Equal(VisitId, call.Arguments[0]);
        Assert.Equal(PhotoId, call.Arguments[1]);
        Assert.Equal(photo.Data, Assert.IsType<byte[]>(call.Arguments[3]));
        Assert.Single(module.Invocations["deletePhoto"]);
        Assert.Single(module.Invocations["deleteVisit"]);
    }

    [Fact]
    public async Task Photo_RestitueLesOctetsSansBase64()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<PhotoReadResult>("getPhoto", PhotoId).SetResult(new PhotoReadResult
        {
            Result = $$"""{"value":{"id":"{{PhotoId}}","contentType":"image/png"},"error":null}""",
            Data = [0, 127, 128, 255]
        });
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var photo = await repository.GetPhotoAsync(PhotoId);

        Assert.NotNull(photo);
        Assert.Equal(PhotoId, photo.Id);
        Assert.Equal("image/png", photo.ContentType);
        Assert.Equal(new byte[] { 0, 127, 128, 255 }, photo.Data);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Photo_AbsenceEtQuotaRestentDistincts(bool quota)
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<PhotoReadResult>("getPhoto", PhotoId).SetResult(new PhotoReadResult
        {
            Result = quota
                ? """{"value":null,"error":{"code":"quota","detail":"QuotaExceededError"}}"""
                : """{"value":null,"error":null}"""
        });
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        if (quota)
        {
            var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.GetPhotoAsync(PhotoId));
            Assert.Equal(VisitStorageError.QuotaExceeded, error.Code);
        }
        else
        {
            Assert.Null(await repository.GetPhotoAsync(PhotoId));
        }
    }

    [Fact]
    public async Task Photo_SansOctetsNeDevientPasUnePhotoVide()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<PhotoReadResult>("getPhoto", PhotoId).SetResult(new PhotoReadResult
        {
            Result = $$"""{"value":{"id":"{{PhotoId}}","contentType":"image/png"},"error":null}"""
        });
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);

        var error = await Assert.ThrowsAsync<VisitStorageException>(() => repository.GetPhotoAsync(PhotoId));

        Assert.Equal(VisitStorageError.InvalidData, error.Code);
    }

    [Fact]
    public async Task ParametresInvalides_NImportentPasLeModule()
    {
        using var contexte = new BunitContext();
        await using var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var invalidPhoto = new VisitPhoto(PhotoId, "image/png", []);
        var herd = DemoData.Generate(new DateOnly(2026, 10, 9), seed: 42);
        var wrongCow = herd.Cows[0] with { LocationId = Guid.NewGuid() };

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Example with { FarmId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SavePhotoAsync(VisitId, invalidPhoto));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Example,
            new VisitPhoto(PhotoId, "image/png", [1])));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveHerdAsync(
            new DemoDataSet(herd.Locations, [wrongCow])));
        Assert.Empty(contexte.JSInterop.Invocations);
    }

    [Fact]
    public async Task Liberation_EstIdempotenteEtInterditLesAppelsUlterieurs()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        module.Setup<string>("getAll").SetResult("""{"value":[],"error":null}""");
        var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        await repository.GetAllAsync();

        await repository.DisposeAsync();
        await repository.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetAllAsync());
        Assert.Single(module.Invocations["getAll"]);
    }

    [Fact]
    public async Task Liberation_AttendLaFinDeLOperationEnCours()
    {
        using var contexte = new BunitContext();
        var module = contexte.JSInterop.SetupModule(IndexedDbVisitRepository.ModulePath);
        var invocation = module.Setup<string>("getAll");
        var repository = new IndexedDbVisitRepository(contexte.JSInterop.JSRuntime);
        var operation = repository.GetAllAsync();

        var liberation = repository.DisposeAsync().AsTask();
        Assert.False(liberation.IsCompleted);
        invocation.SetResult("""{"value":[],"error":null}""");
        await operation;
        await liberation;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetAllAsync());
    }

    private sealed class FailFirstImportRuntime(IJSRuntime inner) : IJSRuntime
    {
        private bool _failed;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import" && !_failed)
            {
                _failed = true;
                throw new JSException("Import refusé une fois.");
            }

            return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
    }
}
