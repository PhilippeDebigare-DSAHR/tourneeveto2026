using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TourneeVeto.Ui.Data;

namespace TourneeVeto.Ui.Components;

public partial class DemoStartup
{
    private readonly string _id = $"demo-startup-{Guid.NewGuid():N}";
    private string TitleId => $"{_id}-title";
    private bool _loading = true;
    private string? _error;
    private DemoLocalData? _data;

    [Inject]
    private DemoStartupService Startup { get; set; } = default!;

    [Inject]
    private ILogger<DemoStartup> Logger { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _data = null;
        try
        {
            _data = await Startup.LoadAsync();
            _error = null;
        }
        catch (VisitStorageException exception)
        {
            Logger.LogError(exception, "Échec du chargement des données locales ({Code}) : {Detail}",
                exception.Code, exception.Detail);
            _error = $"Le chargement des données de démonstration a échoué. {exception.Message}";
        }
        finally
        {
            _loading = false;
        }
    }
}
