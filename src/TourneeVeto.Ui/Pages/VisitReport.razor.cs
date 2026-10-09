using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using TourneeVeto.Domain.Biosecurity;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Ui.Pages;

public partial class VisitReport : IAsyncDisposable
{
    private const string PrintModulePath = "./_content/TourneeVeto.Ui/js/printReport.js";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly HashSet<string> SupportedPhotoTypes =
        new(["image/jpeg", "image/png", "image/webp", "image/gif"], StringComparer.OrdinalIgnoreCase);

    private readonly string _id = $"visit-report-{Guid.NewGuid():N}";
    private IJSObjectReference? _printModule;
    private bool _disposed;
    private bool _printing;
    private string? _printError;
    private string? _photoError;
    private string? _photoSource;
    private bool _loading = true;
    private bool _saving;
    private string? _loadError;
    private string? _saveError;
    private bool _notFound;
    private Visit? _visit;
    private VisitReportSummary? _summary;
    private Guid? _loadedId;

    private string TitleId => $"{_id}-title";

    [Parameter]
    public Guid? VisitId { get; set; }

    [Inject]
    private IVisitRepository Repository { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    [Inject]
    private ILogger<VisitReport> Logger { get; set; } = default!;

    protected override Task OnParametersSetAsync() =>
        _loadedId == VisitId && !_loading ? Task.CompletedTask : LoadAsync();

    private async Task LoadAsync()
    {
        _loadedId = VisitId;
        _loading = true;
        _loadError = null;
        _saveError = null;
        _printError = null;
        _photoError = null;
        _photoSource = null;
        _notFound = false;
        _visit = null;
        _summary = null;

        if (VisitId is not { } id || id == Guid.Empty)
        {
            _notFound = true;
            _loading = false;
            return;
        }

        try
        {
            var visit = await Repository.GetAsync(id);
            if (visit is null)
            {
                _notFound = true;
            }
            else
            {
                _summary = CreateSummary(visit);
                _visit = visit;
                await LoadPhotoAsync(visit);
            }
        }
        catch (VisitStorageException exception)
        {
            Logger.LogError(exception, "Échec de lecture de la visite ({Code}) : {Detail}",
                exception.Code, exception.Detail);
            _loadError = $"La visite n'a pas pu être lue. {exception.Message}";
        }
        catch (ArgumentException exception)
        {
            Logger.LogError(exception, "Visite enregistrée incohérente avec le référentiel de biosécurité.");
            _loadError = "Les réponses de biosécurité enregistrées sont incohérentes : la synthèse ne peut pas être produite.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadPhotoAsync(Visit visit)
    {
        if (visit.PhotoId is not { } photoId)
        {
            return;
        }

        try
        {
            var photo = await Repository.GetPhotoAsync(photoId);
            if (photo is null)
            {
                _photoError = "La photo associée à la visite est introuvable sur cet appareil. Elle n'est pas affichée.";
            }
            else if (photo.Data is not { Length: > 0 } || !SupportedPhotoTypes.Contains(photo.ContentType))
            {
                _photoError = "La photo associée à la visite a un format non pris en charge. Elle n'est pas affichée.";
            }
            else
            {
                // Source locale uniquement : data URL (type MIME de la liste fermée ci-dessus), aucune requête réseau.
                _photoSource = $"data:{photo.ContentType.ToLowerInvariant()};base64,{Convert.ToBase64String(photo.Data)}";
            }
        }
        catch (VisitStorageException exception)
        {
            Logger.LogError(exception, "Échec de lecture de la photo ({Code}) : {Detail}",
                exception.Code, exception.Detail);
            _photoError = $"La photo n'a pas pu être lue. {exception.Message}";
        }
        catch (ArgumentException exception)
        {
            Logger.LogError(exception, "Identifiant de photo invalide.");
            _photoError = "La photo associée à la visite est invalide. Elle n'est pas affichée.";
        }
    }

    private async Task PrintAsync()
    {
        if (_printing)
        {
            return;
        }

        _printing = true;
        _printError = null;
        try
        {
            _printModule ??= await Js.InvokeAsync<IJSObjectReference>("import", PrintModulePath);
            await _printModule.InvokeVoidAsync("printReport");
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            Logger.LogError(exception, "Échec de l'impression du rapport.");
            _printError = "L'impression n'a pas pu être lancée. Aucun document n'a été produit. Réessayez.";
        }
        finally
        {
            _printing = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_printModule is not null)
        {
            try
            {
                await _printModule.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException)
            {
                // Circuit ou page déjà libérés : rien à nettoyer.
            }

            _printModule = null;
        }
    }

    private async Task CloseAsync()
    {
        if (_visit is null || _visit.IsClosed || _saving)
        {
            return;
        }

        _saving = true;
        _saveError = null;
        try
        {
            var today = DateOnly.FromDateTime(Time.GetLocalNow().DateTime);
            var closed = _visit with { ClosedOn = today };
            await Repository.SaveAsync(closed);

            // Aucune réussite implicite : la clôture n'est affichée qu'après relecture du stockage local.
            var saved = await Repository.GetAsync(closed.Id);
            if (saved is not { IsClosed: true })
            {
                _saveError = "La clôture n'a pas pu être confirmée par le stockage local. Réessayez.";
                return;
            }

            _visit = saved;
            _summary = CreateSummary(saved);
        }
        catch (ArgumentOutOfRangeException)
        {
            _saveError = "La visite ne peut pas être clôturée avant sa date. Aucun enregistrement n'a été effectué.";
        }
        catch (VisitStorageException exception)
        {
            Logger.LogError(exception, "Échec de clôture de la visite ({Code}) : {Detail}",
                exception.Code, exception.Detail);
            _saveError = $"La clôture n'a pas été enregistrée. {exception.Message}";
        }
        catch (ArgumentException exception)
        {
            Logger.LogError(exception, "Visite invalide lors de la clôture.");
            _saveError = "La visite contient des données invalides. La clôture n'a pas été enregistrée.";
        }
        finally
        {
            _saving = false;
        }
    }

    private static VisitReportSummary CreateSummary(Visit visit) =>
        VisitReportSummary.Create(visit, BiosecurityChecklistReference.Questions);

    private static string FormatDate(DateOnly date) => date.ToString("d MMMM yyyy", French);

    private static string FormatScore(decimal? score) =>
        score is { } value ? $"{value.ToString("0.#", French)} %" : "Non calculé";

    private static string ActionText(VisitAction action) =>
        $"{ActionLabel(action.Type)} — {FormatDate(action.Date)}"
        + (string.IsNullOrWhiteSpace(action.Notes) ? "" : " — " + action.Notes);

    private static string ActionLabel(ActionType type) => type switch
    {
        ActionType.PregnancyDiagnosis => "Diagnostic de gestation",
        ActionType.HighSomaticCellCount => "Cellules somatiques élevées",
        ActionType.DryOff => "Tarissement",
        ActionType.Calving => "Vêlage",
        ActionType.Insemination => "Insémination",
        _ => "Action"
    };
}
