using System.Text.Json.Serialization;
using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Ui.Data;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Visit))]
[JsonSerializable(typeof(DemoDataSet))]
[JsonSerializable(typeof(DemoSeed))]
[JsonSerializable(typeof(StorageResult<Visit[]>))]
[JsonSerializable(typeof(StorageResult<Visit>))]
[JsonSerializable(typeof(StorageResult<DemoDataSet>))]
[JsonSerializable(typeof(StorageResult<PhotoMetadata>))]
[JsonSerializable(typeof(StorageResult<bool?>), TypeInfoPropertyName = "MutationResult")]
internal partial class VisitStoreJsonContext : JsonSerializerContext;

internal sealed record StorageResult<T>(
    [property: JsonRequired] T? Value,
    [property: JsonRequired] StorageFailure? Error);
internal sealed record StorageFailure(string Code, string? Detail);
internal sealed record PhotoMetadata(Guid Id, string ContentType);

internal sealed class PhotoReadResult
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public byte[]? Data { get; set; }
}
