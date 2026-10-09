using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Ui.Data;

public interface IVisitRepository
{
    Task InitializeDemoAsync(DemoSeed seed);
    Task<IReadOnlyList<Visit>> GetAllAsync();
    Task<Visit?> GetAsync(Guid id);
    Task SaveAsync(Visit visit, VisitPhoto? photo = null);
    Task DeleteAsync(Guid id);
    Task<DemoDataSet?> GetHerdAsync();
    Task SaveHerdAsync(DemoDataSet herd);
    Task<VisitPhoto?> GetPhotoAsync(Guid photoId);
    Task SavePhotoAsync(Guid visitId, VisitPhoto photo);
    Task DeletePhotoAsync(Guid visitId);
}

public sealed record VisitPhoto(Guid Id, string ContentType, byte[] Data);
