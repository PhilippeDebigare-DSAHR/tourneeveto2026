using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Ui.Data;

public sealed class IndexedDbVisitRepository : IVisitRepository, IAsyncDisposable
{
    public const string ModulePath = "./_content/TourneeVeto.Ui/js/visitStore.js";

    private readonly IJSRuntime _js;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;
    private bool _disposed;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PhotoReadResult))]
    public IndexedDbVisitRepository(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<IReadOnlyList<Visit>> GetAllAsync() =>
        await ReadAsync("getAll", VisitStoreJsonContext.Default.StorageResultVisitArray)
        ?? throw InvalidResponse();

    public Task InitializeDemoAsync(DemoSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(seed.Herd);
        ArgumentNullException.ThrowIfNull(seed.Visits);
        return MutateAsync("initializeDemo",
            JsonSerializer.Serialize(seed, VisitStoreJsonContext.Default.DemoSeed));
    }

    public Task<Visit?> GetAsync(Guid id)
    {
        ValidateId(id);
        return ReadAsync("get", VisitStoreJsonContext.Default.StorageResultVisit, id);
    }

    public Task SaveAsync(Visit visit, VisitPhoto? photo = null)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ValidateId(visit.Id);
        ValidateId(visit.FarmId);
        if (visit.PhotoId is { } photoId)
        {
            ValidateId(photoId);
        }

        if (photo is not null)
        {
            ValidatePhoto(photo);
            if (visit.PhotoId != photo.Id)
            {
                throw new ArgumentException("La photo doit correspondre au PhotoId de la visite.", nameof(photo));
            }
        }

        return MutateAsync("save", JsonSerializer.Serialize(visit, VisitStoreJsonContext.Default.Visit),
            photo?.Data, photo?.ContentType);
    }

    public Task DeleteAsync(Guid id)
    {
        ValidateId(id);
        return MutateAsync("deleteVisit", id);
    }

    public Task<DemoDataSet?> GetHerdAsync() =>
        ReadAsync("getHerd", VisitStoreJsonContext.Default.StorageResultDemoDataSet);

    public Task SaveHerdAsync(DemoDataSet herd)
    {
        ArgumentNullException.ThrowIfNull(herd);
        ArgumentNullException.ThrowIfNull(herd.Locations);
        ArgumentNullException.ThrowIfNull(herd.Cows);
        var locations = new HashSet<Guid>();
        foreach (var location in herd.Locations)
        {
            ArgumentNullException.ThrowIfNull(location);
            ValidateId(location.Id);
            if (!locations.Add(location.Id))
            {
                throw new ArgumentException("Les identifiants des élevages doivent être uniques.", nameof(herd));
            }
        }

        var cows = new HashSet<Guid>();
        foreach (var cow in herd.Cows)
        {
            ArgumentNullException.ThrowIfNull(cow);
            ValidateId(cow.Id);
            if (!cows.Add(cow.Id) || !locations.Contains(cow.LocationId))
            {
                throw new ArgumentException("Chaque vache doit être unique et rattachée à un élevage du troupeau.", nameof(herd));
            }
        }

        return MutateAsync("saveHerd", JsonSerializer.Serialize(herd, VisitStoreJsonContext.Default.DemoDataSet));
    }

    public async Task<VisitPhoto?> GetPhotoAsync(Guid photoId)
    {
        ValidateId(photoId);
        var result = await InvokeAsync<PhotoReadResult>("getPhoto", photoId);
        if (result is null)
        {
            throw InvalidResponse();
        }

        var metadata = Deserialize(result.Result, VisitStoreJsonContext.Default.StorageResultPhotoMetadata);
        if (metadata is null)
        {
            if (result.Data is not null)
            {
                throw InvalidResponse();
            }
            return null;
        }

        if (result.Data is not { Length: > 0 } || metadata.Id != photoId ||
            string.IsNullOrWhiteSpace(metadata.ContentType) ||
            !metadata.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidResponse();
        }

        return new VisitPhoto(metadata.Id, metadata.ContentType, result.Data);
    }

    public Task SavePhotoAsync(Guid visitId, VisitPhoto photo)
    {
        ValidateId(visitId);
        ValidatePhoto(photo);
        return MutateAsync("savePhoto", visitId, photo.Id, photo.ContentType, photo.Data);
    }

    public Task DeletePhotoAsync(Guid visitId)
    {
        ValidateId(visitId);
        return MutateAsync("deletePhoto", visitId);
    }

    private async Task<T?> ReadAsync<T>(string operation, JsonTypeInfo<StorageResult<T>> type, params object?[] args) =>
        Deserialize(await InvokeAsync<string>(operation, args), type);

    private async Task MutateAsync(string operation, params object?[] args)
    {
        var result = await ReadAsync(operation, VisitStoreJsonContext.Default.MutationResult, args);
        if (result != true)
        {
            throw InvalidResponse();
        }
    }

    private static T? Deserialize<T>(string json, JsonTypeInfo<StorageResult<T>> type)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw InvalidResponse();
        }

        StorageResult<T> result;
        try
        {
            result = JsonSerializer.Deserialize(json, type) ?? throw InvalidResponse();
        }
        catch (JsonException exception)
        {
            throw new VisitStorageException(VisitStorageError.InvalidData,
                "Les données locales sont illisibles. Aucun enregistrement n'est confirmé.",
                exception.Message, exception);
        }

        if (result.Error is { } error)
        {
            throw error.Code switch
            {
                "quota" => new VisitStorageException(VisitStorageError.QuotaExceeded,
                    "Le quota de stockage local est dépassé. Les données n'ont pas été enregistrées.", error.Detail),
                "unavailable" => new VisitStorageException(VisitStorageError.Unavailable,
                    "Le stockage local est indisponible ou refusé. Les données ne sont pas prêtes pour le hors-ligne.", error.Detail),
                "blocked" => new VisitStorageException(VisitStorageError.UpgradeBlocked,
                    "La mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto puis réessayez.", error.Detail),
                "version" => new VisitStorageException(VisitStorageError.UnsupportedVersion,
                    "Le stockage local utilise une version plus récente. Mettez l'application à jour.", error.Detail),
                "invalid" => new VisitStorageException(VisitStorageError.InvalidData,
                    "Les données à enregistrer ou leurs références sont invalides. Aucun enregistrement n'est confirmé.", error.Detail),
                _ => new VisitStorageException(VisitStorageError.Unknown,
                    "L'accès au stockage local a échoué. Aucun enregistrement n'est confirmé.", error.Detail)
            };
        }

        return result.Value;
    }

    private async Task<T> InvokeAsync<T>(string operation, params object?[] args)
    {
        await _moduleLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            return await _module.InvokeAsync<T>(operation, args);
        }
        catch (JSException exception)
        {
            throw new VisitStorageException(VisitStorageError.Unavailable,
                "L'accès au module de stockage local a échoué. Aucun enregistrement n'est confirmé.",
                exception.Message, exception);
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _moduleLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Un identifiant non vide est requis.", nameof(id));
        }
    }

    private static void ValidatePhoto(VisitPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ValidateId(photo.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(photo.ContentType);
        if (photo.ContentType.Length <= "image/".Length ||
            !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            photo.Data is not { Length: > 0 })
        {
            throw new ArgumentException("La photo doit contenir des octets et un type MIME d'image.", nameof(photo));
        }
    }

    private static VisitStorageException InvalidResponse() =>
        new(VisitStorageError.InvalidData, "Le stockage local a retourné une réponse invalide.");
}
