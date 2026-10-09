namespace TourneeVeto.Domain.Visits;

/// <summary>Élevage suivi par le vétérinaire.</summary>
public sealed record Location(
    Guid Id,
    string Name,
    string City,
    int AnimalCount);

/// <param name="FarmId">Identifiant de l'élevage visité (<see cref="Location.Id"/>).</param>
/// <param name="PhotoId">Photo associée à la visite, si elle existe.</param>
public sealed record Visit(
    Guid Id,
    Guid FarmId,
    DateOnly Date,
    string Cause,
    string Notes,
    Guid? PhotoId)
{
    private readonly IReadOnlyList<VisitAction>? _actions;

    // Les visites du socle antérieur ne possèdent pas encore d'actions.
    public IReadOnlyList<VisitAction> Actions
    {
        get => _actions ?? [];
        init => _actions = value;
    }
}

public sealed record VisitAction(
    Guid Id,
    Guid CowId,
    ActionType Type,
    DateOnly Date,
    bool IsCompleted,
    string Notes);
