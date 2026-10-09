namespace TourneeVeto.Domain.Visits;

/// <summary>Action enregistrée non réalisée, rattachée à la visite précédente qui la porte.</summary>
public sealed record PendingVisitAction(Guid VisitId, DateOnly VisitDate, VisitAction Action);

/// <summary>
/// Synthèse simplifiée d'un élevage : visites antérieures à la date de référence et actions
/// enregistrées non réalisées. Informatif seulement, sans avis clinique ni exigence réglementaire.
/// </summary>
/// <param name="FarmId">Élevage concerné.</param>
/// <param name="ReferenceDate">Date de référence (exclue : visite précédente = Date &lt; référence).</param>
/// <param name="PreviousVisits">Visites précédentes, de la plus récente à la plus ancienne (puis par Id).</param>
/// <param name="PendingActions">Actions IsCompleted = false de ces visites, dans l'ordre des visites puis par date, vache, type et Id.</param>
public sealed record FarmPreviousVisitsSummary(
    Guid FarmId,
    DateOnly ReferenceDate,
    IReadOnlyList<Visit> PreviousVisits,
    IReadOnlyList<PendingVisitAction> PendingActions)
{
    /// <summary>Construit la synthèse d'un élevage à partir de l'élevage lui-même.</summary>
    /// <exception cref="ArgumentNullException">Élevage ou liste de visites null.</exception>
    /// <exception cref="ArgumentException">Identifiant vide, ou liste contenant une visite null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Date de référence par défaut (non renseignée).</exception>
    public static FarmPreviousVisitsSummary Build(Location farm, DateOnly referenceDate, IReadOnlyList<Visit> visits)
    {
        ArgumentNullException.ThrowIfNull(farm);
        return Build(farm.Id, referenceDate, visits);
    }

    /// <summary>Construit la synthèse d'un élevage à partir de son identifiant.</summary>
    /// <exception cref="ArgumentNullException">Liste de visites null.</exception>
    /// <exception cref="ArgumentException">Identifiant vide, ou liste contenant une visite null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Date de référence par défaut (non renseignée).</exception>
    public static FarmPreviousVisitsSummary Build(Guid farmId, DateOnly referenceDate, IReadOnlyList<Visit> visits)
    {
        if (farmId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de l'élevage doit être renseigné.", nameof(farmId));
        }

        if (referenceDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceDate), "La date de référence doit être renseignée.");
        }

        ArgumentNullException.ThrowIfNull(visits);
        if (visits.Any(visit => visit is null))
        {
            throw new ArgumentException("La liste de visites ne peut pas contenir d'élément null.", nameof(visits));
        }

        var previous = visits
            .Where(visit => visit.FarmId == farmId && visit.Date < referenceDate)
            .OrderByDescending(visit => visit.Date)
            .ThenBy(visit => visit.Id)
            .ToArray();

        var pending = previous
            .SelectMany(visit => visit.Actions
                .Where(action => !action.IsCompleted)
                .OrderBy(action => action.Date)
                .ThenBy(action => action.CowId)
                .ThenBy(action => action.Type)
                .ThenBy(action => action.Id)
                .Select(action => new PendingVisitAction(visit.Id, visit.Date, action)))
            .ToArray();

        return new FarmPreviousVisitsSummary(farmId, referenceDate,
            Array.AsReadOnly(previous), Array.AsReadOnly(pending));
    }
}
