namespace TourneeVeto.Domain.Herd;

public enum ReproductiveStatus
{
    None,
    Open,
    Bred,
    Pregnant,
    Dry
}

/// <param name="LocationId">Identifiant de l'élevage (<see cref="Visits.Location"/>).</param>
/// <param name="Lactation">Rang de lactation ; 0 pour une génisse.</param>
/// <param name="LastCcs">Dernier comptage de cellules somatiques, en milliers de cellules/mL.</param>
public sealed record Cow(
    Guid Id,
    string Name,
    Guid LocationId,
    DateOnly BirthDate,
    int Lactation,
    DateOnly? LastCalving,
    DateOnly? LastInsemination,
    ReproductiveStatus ReproductionStatus,
    int? LastCcs);
