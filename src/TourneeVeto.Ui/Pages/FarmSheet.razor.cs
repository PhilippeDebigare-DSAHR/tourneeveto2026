using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Ui.Pages;

/// <summary>Fiche d'élevage minimale (identité) : le contexte et les suivis relèvent de S04.</summary>
public partial class FarmSheet
{
    private readonly string _id = $"farm-sheet-{Guid.NewGuid():N}";
    private bool _loading = true;
    private string? _error;
    private Location? _farm;
    private Guid? _loadedId;
    private int _generation;

    private IReadOnlyDictionary<Guid, string> _cowNames = new Dictionary<Guid, string>();

    private string TitleId => $"{_id}-title";

    /// <summary>Nom de la vache de l'élevage courant ; repli explicite si absente ou d'un autre élevage.</summary>
    internal string CowName(Guid cowId) =>
        _cowNames.TryGetValue(cowId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "Vache non identifiée";

    /// <summary>Synthèse de contexte de l'élevage courant ; null tant que non chargée ou en cas d'erreur/fiche introuvable.</summary>
    internal FarmPreviousVisitsSummary? Summary { get; private set; }

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Parameter]
    public Guid FarmId { get; set; }

    [Inject]
    private IVisitRepository Repository { get; set; } = default!;

    [Inject]
    private ILogger<FarmSheet> Logger { get; set; } = default!;

    protected override Task OnParametersSetAsync() =>
        _loadedId == FarmId ? Task.CompletedTask : LoadAsync();

    private async Task LoadAsync()
    {
        var farmId = FarmId;
        var generation = ++_generation;
        _loadedId = farmId;
        _loading = true;
        _error = null;
        _farm = null;
        Summary = null;
        _cowNames = new Dictionary<Guid, string>();
        try
        {
            var herd = await Repository.GetHerdAsync();
            if (generation != _generation)
            {
                return;
            }

            var farm = herd?.Locations.FirstOrDefault(l => l.Id == farmId);
            if (farm is null)
            {
                _error = "Fiche introuvable : cet élevage est absent des données locales. Aucune autre fiche n'est affichée.";
            }
            else
            {
                var visits = await Repository.GetAllAsync();
                if (generation != _generation)
                {
                    return;
                }

                var today = DateOnly.FromDateTime(Time.GetLocalNow().DateTime);
                Summary = FarmPreviousVisitsSummary.Build(farm, today, visits);
                _cowNames = herd!.Cows
                    .Where(c => c.LocationId == farmId)
                    .GroupBy(c => c.Id)
                    .ToDictionary(g => g.Key, g => g.First().Name);
                _farm = farm;
            }
        }
        catch (VisitStorageException exception)
        {
            if (generation != _generation)
            {
                return;
            }

            Logger.LogError("Échec de lecture de la fiche ({Code}, {InnerType}).",
                                        exception.Code, exception.InnerException?.GetType().Name);
            _error = ErrorMessage(exception.Code);
        }
        finally
        {
            if (generation == _generation)
            {
                _loading = false;
            }
        }
    }

    private static string ErrorMessage(VisitStorageError code) => code switch
    {
        VisitStorageError.UpgradeBlocked =>
            "Impossible de charger la fiche : la mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto, puis réessayez.",
        VisitStorageError.UnsupportedVersion =>
            "Impossible de charger la fiche : le stockage local utilise une version plus récente. Mettez l'application à jour.",
        VisitStorageError.QuotaExceeded =>
            "Impossible de charger la fiche : le quota de stockage local est dépassé. Libérez de l'espace sur l'appareil, puis réessayez.",
        VisitStorageError.Unavailable =>
            "Impossible de charger la fiche : le stockage local est indisponible ou refusé. Autorisez le stockage du navigateur, puis réessayez.",
        VisitStorageError.InvalidData =>
            "Impossible de charger la fiche : les données locales sont illisibles. Veuillez réessayer ; si le problème persiste, vérifiez les données locales de l'appareil.",
        _ => "Impossible de charger la fiche : l'accès au stockage local a échoué. Veuillez réessayer."
    };
}
