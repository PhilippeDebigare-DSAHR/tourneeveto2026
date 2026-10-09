using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Ui.Pages;

public partial class DailyRoundPage
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly string _id = $"daily-round-{Guid.NewGuid():N}";
    private bool _loading = true;
    private string? _error;
    private DailyRound? _round;

    private string TitleId => $"{_id}-title";

    [Inject]
    private IVisitRepository Repository { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private ILogger<DailyRoundPage> Logger { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        _round = null;
        try
        {
            var herd = await Repository.GetHerdAsync();
            if (herd is null)
            {
                _error = "Impossible de charger la tournée : le troupeau de démonstration est absent ou n'est pas encore initialisé sur cet appareil. "
                    + "Ce chargement n'a effacé aucune donnée. Retournez à l'accueil pour initialiser les données de démonstration s'il s'agit d'un premier démarrage, "
                    + "ou réessayez après correction du stockage local.";
                return;
            }

            var visits = await Repository.GetAllAsync();
            var today = DateOnly.FromDateTime(Time.GetLocalNow().DateTime);
            _round = DailyRound.Create(today, visits, herd.Locations);
        }
        catch (VisitStorageException exception)
        {
            Logger.LogError("Échec de lecture de la tournée ({Code}, {InnerType}).",
                                        exception.Code, exception.InnerException?.GetType().Name);
            _error = ErrorMessage(exception.Code);
        }
        catch (ArgumentException exception)
        {
            Logger.LogError("Données locales de la tournée incohérentes ({ExceptionType}).",
                            exception.GetType().Name);
            _error = "Impossible de charger la tournée : les données locales sont incohérentes.";
        }
        finally
        {
            _loading = false;
        }
    }

    private static string ErrorMessage(VisitStorageError code) => code switch
    {
        VisitStorageError.UpgradeBlocked =>
            "Impossible de charger la tournée : la mise à jour du stockage est bloquée. Fermez les autres onglets TournéeVéto, puis réessayez.",
        VisitStorageError.UnsupportedVersion =>
            "Impossible de charger la tournée : le stockage local utilise une version plus récente. Mettez l'application à jour.",
        VisitStorageError.QuotaExceeded =>
            "Impossible de charger la tournée : le quota de stockage local est dépassé. Libérez de l'espace sur l'appareil, puis réessayez.",
        VisitStorageError.Unavailable =>
            "Impossible de charger la tournée : le stockage local est indisponible ou refusé. Autorisez le stockage du navigateur, puis réessayez.",
        VisitStorageError.InvalidData =>
            "Impossible de charger la tournée : les données locales sont illisibles. Veuillez réessayer ; si le problème persiste, vérifiez les données locales de l'appareil.",
        _ => "Impossible de charger la tournée : l'accès au stockage local a échoué. Veuillez réessayer."
    };

    private static string FormatDate(DateOnly date) => date.ToString("dddd d MMMM yyyy", French);
}
