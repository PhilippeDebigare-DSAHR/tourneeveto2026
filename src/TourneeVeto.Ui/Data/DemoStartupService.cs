using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Ui.Data;

public sealed record DemoLocalData(DemoDataSet Herd, IReadOnlyList<Visit> Visits);

public sealed class DemoStartupService(IVisitRepository repository, TimeProvider timeProvider)
{
    public async Task<DemoLocalData> LoadAsync()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        await repository.InitializeDemoAsync(DemoData.GenerateSeed(today, seed: 42));
        var herd = await repository.GetHerdAsync()
            ?? throw new VisitStorageException(VisitStorageError.InvalidData,
                "Les données locales de démonstration sont absentes. Le chargement a échoué.");
        var visits = await repository.GetAllAsync();
        return new DemoLocalData(herd, visits);
    }
}
