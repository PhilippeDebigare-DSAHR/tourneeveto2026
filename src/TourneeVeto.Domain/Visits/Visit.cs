using TourneeVeto.Domain.Biosecurity;

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
    private readonly DateOnly? _closedOn;
    private readonly IReadOnlyList<VisitAction>? _actions;
    private readonly IReadOnlyList<BiosecurityResponse>? _biosecurityResponses;

    // Les visites du socle antérieur ne possèdent pas encore d'actions.
    public IReadOnlyList<VisitAction> Actions
    {
        get => _actions ?? [];
        init => _actions = CopyWithoutNulls(value, nameof(Actions));
    }

    /// <summary>
    /// Date de clôture ; seule source de l'état clôturé. Absente des anciens documents : null (visite ouverte).
    /// Refuse la valeur par défaut (<see cref="DateOnly.MinValue"/>) et toute date antérieure à <see cref="Date"/>.
    /// </summary>
    public DateOnly? ClosedOn
    {
        get => _closedOn;
        init
        {
            if (value is { } closedOn && (closedOn == default || closedOn < Date))
            {
                throw new ArgumentOutOfRangeException(nameof(ClosedOn),
                    "La date de clôture doit être renseignée et ne peut pas précéder la date de la visite.");
            }

            _closedOn = value;
        }
    }

    /// <summary>
    /// Visite clôturée si et seulement si <see cref="ClosedOn"/> est renseignée : un état incohérent
    /// (clôturée sans date, ou date sans clôture) est impossible. Valeur dérivée, ignorée à la désérialisation.
    /// </summary>
    public bool IsClosed => ClosedOn.HasValue;

    // Les visites antérieures ne possèdent pas de réponses de biosécurité.
    public IReadOnlyList<BiosecurityResponse> BiosecurityResponses
    {
        get => _biosecurityResponses ?? [];
        init => _biosecurityResponses = CopyWithoutNulls(value, nameof(BiosecurityResponses));
    }

    // Copie défensive en lecture seule ; null devient « absent », un élément null est refusé.
    private static IReadOnlyList<T>? CopyWithoutNulls<T>(IReadOnlyList<T>? source, string name)
    {
        if (source is null)
        {
            return null;
        }

        var copy = source.ToArray();
        if (copy.Any(item => item is null))
        {
            throw new ArgumentException("La liste ne peut pas contenir d'élément null.", name);
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed record VisitAction(
    Guid Id,
    Guid CowId,
    ActionType Type,
    DateOnly Date,
    bool IsCompleted,
    string Notes);
