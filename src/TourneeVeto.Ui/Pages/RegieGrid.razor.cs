using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Ui.Pages;

public partial class RegieGrid
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    // Ordre d'affichage des cinq catégories de régie.
    private static readonly (ActionType Type, string Label)[] CategoryOrder =
    [
        (ActionType.Calving, "Vêlage"),
        (ActionType.DryOff, "Tarissement"),
        (ActionType.Insemination, "Insémination"),
        (ActionType.PregnancyDiagnosis, "Diagnostic de gestation"),
        (ActionType.HighSomaticCellCount, "CCS élevée"),
    ];

    private readonly string _id = $"regie-grid-{Guid.NewGuid():N}";
    private bool _loading = true;
    private bool _notFound;
    private string? _error;
    private string? _farmName;
    private DateOnly _visitDate;
    private int _ignoredActionCount;
    private Guid? _loadedId;
    private int _loadGeneration;
    private IReadOnlyList<RegieCategory> _categories = [];
    private Visit? _visit;
    private bool _saving;
    private bool _saveSuccess;
    private string? _saveError;

    private string TitleId => $"{_id}-title";

    [Parameter]
    public Guid VisitId { get; set; }

    [Inject]
    private IVisitRepository Repository { get; set; } = default!;

    [Inject]
    private ILogger<RegieGrid> Logger { get; set; } = default!;

    protected override Task OnParametersSetAsync() =>
        _loadedId == VisitId && !_loading ? Task.CompletedTask : LoadAsync();

    private async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        var requestedId = VisitId;
        bool IsCurrent() => generation == _loadGeneration && requestedId == VisitId;

        _loadedId = requestedId;
        _loading = true;
        _notFound = false;
        _error = null;
        _farmName = null;
        _ignoredActionCount = 0;
        _categories = [];
        _visit = null;
        _saving = false;
        _saveSuccess = false;
        _saveError = null;

        try
        {
            var visit = requestedId == Guid.Empty ? null : await Repository.GetAsync(requestedId);
            if (!IsCurrent())
            {
                return;
            }

            if (visit is null)
            {
                _notFound = true;
                return;
            }

            var herd = await Repository.GetHerdAsync();
            if (!IsCurrent())
            {
                return;
            }

            if (herd is null)
            {
                _error = "Impossible de charger la grille de régie : le troupeau de démonstration est absent ou n'est pas encore initialisé sur cet appareil. "
                    + "Aucune donnée n'a été effacée. Retournez à l'accueil pour initialiser les données de démonstration, puis réessayez.";
                return;
            }

            var farm = herd.Locations.FirstOrDefault(location => location.Id == visit.FarmId);
            if (farm is null)
            {
                _error = "Impossible de charger la grille de régie : l'élevage de cette visite est introuvable dans les données locales.";
                return;
            }

            var cows = herd.Cows
                .Where(cow => cow.LocationId == visit.FarmId)
                .GroupBy(cow => cow.Id)
                .ToDictionary(group => group.Key, group => group.First());

            var rows = new List<RegieRow>();
            var ignored = 0;
            foreach (var action in visit.Actions)
            {
                if (cows.TryGetValue(action.CowId, out var cow))
                {
                    rows.Add(new RegieRow(action.Id, cow.Id, cow.Name, action.Type, action.IsCompleted, action.Notes));
                }
                else
                {
                    ignored++;
                }
            }

            if (!IsCurrent())
            {
                return;
            }

            _ignoredActionCount = ignored;
            _categories = CategoryOrder
                .Select(category => new RegieCategory(category.Type, category.Label,
                    rows.Where(row => row.Type == category.Type)
                        .OrderBy(row => row.CowName, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(row => row.CowId)
                        .ToArray()))
                .ToArray();
            _farmName = farm.Name;
            _visit = visit;
            _visitDate = visit.Date;
        }
        catch (VisitStorageException exception) when (IsCurrent())
        {
            Logger.LogError("Échec de lecture de la grille de régie ({Code}, {InnerType}).",
                exception.Code, exception.InnerException?.GetType().Name);
            _error = ErrorMessage(exception.Code);
        }
        catch (ArgumentException exception) when (IsCurrent())
        {
            Logger.LogError("Données locales de la grille de régie incohérentes ({ExceptionType}).",
                exception.GetType().Name);
            _error = "Impossible de charger la grille de régie : les données locales sont incohérentes.";
        }
        catch (Exception exception) when (exception is VisitStorageException or ArgumentException)
        {
            // Appel obsolète : l'erreur est ignorée, l'état n'est pas modifié.
        }
        finally
        {
            if (IsCurrent())
            {
                _loading = false;
            }
        }
    }

    private static string ErrorMessage(VisitStorageError code) => code switch
    {
        VisitStorageError.UpgradeBlocked =>
            "Impossible de charger la grille de régie : la mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto, puis réessayez.",
        VisitStorageError.UnsupportedVersion =>
            "Impossible de charger la grille de régie : le stockage local utilise une version plus récente. Mettez l'application à jour.",
        VisitStorageError.QuotaExceeded =>
            "Impossible de charger la grille de régie : le quota de stockage local est dépassé. Libérez de l'espace sur l'appareil, puis réessayez.",
        VisitStorageError.Unavailable =>
            "Impossible de charger la grille de régie : le stockage local est indisponible ou refusé. Autorisez le stockage du navigateur, puis réessayez.",
        VisitStorageError.InvalidData =>
            "Impossible de charger la grille de régie : les données locales sont illisibles. Veuillez réessayer ; si le problème persiste, vérifiez les données locales de l'appareil.",
        _ => "Impossible de charger la grille de régie : l'accès au stockage local a échoué. Veuillez réessayer."
    };

    private static string FormatDate(DateOnly date) => date.ToString("dddd d MMMM yyyy", French);

    private void SetCompleted(RegieRow row, bool value)
    {
        if (_saving)
        {
            return;
        }

        row.IsCompleted = value;
        _saveError = null;
        _saveSuccess = false;
    }

    private void SetNotes(RegieRow row, string? value)
    {
        if (_saving)
        {
            return;
        }

        row.Notes = value ?? string.Empty;
        _saveError = null;
        _saveSuccess = false;
    }

    private async Task SaveAsync()
    {
        // Instantané de la visite et de la génération : une navigation ou un rechargement invalide l'appel.
        var generation = _loadGeneration;
        var requestedId = VisitId;
        var baseVisit = _visit;
        if (_saving || _loading || baseVisit is null || baseVisit.Id != requestedId || _loadedId != requestedId)
        {
            return;
        }

        bool IsCurrent() => generation == _loadGeneration && requestedId == VisitId && _loadedId == requestedId;

        var edits = _categories
            .SelectMany(category => category.Rows)
            .ToDictionary(row => row.ActionId, row => (row.IsCompleted, row.Notes));

        // Seules IsCompleted et Notes changent, par Action.Id ; tout le reste est conservé tel quel.
        var updated = baseVisit with
        {
            Actions = baseVisit.Actions
                .Select(action => edits.TryGetValue(action.Id, out var edit)
                    ? action with { IsCompleted = edit.IsCompleted, Notes = edit.Notes }
                    : action)
                .ToArray()
        };

        _saving = true;
        _saveError = null;
        _saveSuccess = false;
        try
        {
            await Repository.SaveAsync(updated);
            if (!IsCurrent())
            {
                return;
            }

            _visit = updated;
            _saveSuccess = true;
        }
        catch (VisitStorageException exception) when (IsCurrent())
        {
            Logger.LogError("Échec d'enregistrement de la grille de régie ({Code}, {InnerType}).",
                exception.Code, exception.InnerException?.GetType().Name);
            _saveError = SaveErrorMessage(exception.Code);
        }
        catch (Exception exception) when (IsCurrent() && exception is not OperationCanceledException)
        {
            Logger.LogError("Échec d'enregistrement de la grille de régie ({ExceptionType}).",
                exception.GetType().Name);
            _saveError = SaveErrorMessage(null);
        }
        catch (Exception)
        {
            // Appel obsolète : l'erreur est ignorée, l'état n'est pas modifié.
        }
        finally
        {
            if (IsCurrent())
            {
                _saving = false;
            }
        }
    }

    private static string SaveErrorMessage(VisitStorageError? code) => code switch
    {
        VisitStorageError.QuotaExceeded =>
            "Enregistrement impossible : le quota de stockage local est dépassé. Libérez de l'espace sur l'appareil, puis réessayez. Vos modifications sont conservées à l'écran.",
        VisitStorageError.Unavailable =>
            "Enregistrement impossible : le stockage local est indisponible ou refusé. Autorisez le stockage du navigateur, puis réessayez. Vos modifications sont conservées à l'écran.",
        VisitStorageError.UpgradeBlocked =>
            "Enregistrement impossible : la mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto, puis réessayez. Vos modifications sont conservées à l'écran.",
        _ => "Enregistrement impossible : l'accès au stockage local a échoué. Veuillez réessayer. Vos modifications sont conservées à l'écran."
    };

    private sealed class RegieRow(Guid actionId, Guid cowId, string cowName, ActionType type, bool isCompleted, string notes)
    {
        public Guid ActionId { get; } = actionId;
        public Guid CowId { get; } = cowId;
        public string CowName { get; } = cowName;
        public ActionType Type { get; } = type;
        public bool IsCompleted { get; set; } = isCompleted;
        public string Notes { get; set; } = notes ?? string.Empty;
    }

    private sealed record RegieCategory(ActionType Type, string Label, IReadOnlyList<RegieRow> Rows);
}
