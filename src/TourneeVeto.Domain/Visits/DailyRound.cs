namespace TourneeVeto.Domain.Visits;

/// <summary>Élevage de la tournée avec ses visites prévues à la date de la tournée.</summary>
public sealed record RoundFarm(Location Farm, IReadOnlyList<Visit> Visits);

/// <summary>
/// Tournée d'un jour : uniquement les élevages ayant au moins une visite datée de <see cref="Date"/>.
/// Chaque élevage apparaît une seule fois, triés par nom, ville puis identifiant (ordre déterministe).
/// Un élevage dont les visites sont toutes à une autre date est exclu.
/// </summary>
public sealed record DailyRound
{
    private DailyRound(DateOnly date, IReadOnlyList<RoundFarm> farms)
    {
        Date = date;
        Farms = farms;
    }

    public DateOnly Date { get; }

    public IReadOnlyList<RoundFarm> Farms { get; }

    /// <summary>Sélectionne les élevages prévus à <paramref name="date"/>.</summary>
    /// <exception cref="ArgumentNullException">Collection absente.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Date par défaut (<see cref="DateOnly.MinValue"/>).</exception>
    /// <exception cref="ArgumentException">
    /// Élément null, identifiant d'élevage en double, ou visite du jour rattachée à un élevage inconnu :
    /// une donnée incohérente est signalée plutôt que masquée par une tournée vide.
    /// </exception>
    public static DailyRound Create(DateOnly date, IEnumerable<Visit> visits, IEnumerable<Location> farms)
    {
        ArgumentNullException.ThrowIfNull(visits);
        ArgumentNullException.ThrowIfNull(farms);

        if (date == default)
        {
            throw new ArgumentOutOfRangeException(nameof(date), "La date de la tournée doit être renseignée.");
        }

        var visitList = visits.ToList();
        var farmList = farms.ToList();

        if (visitList.Any(v => v is null))
        {
            throw new ArgumentException("La liste de visites ne peut pas contenir d'élément null.", nameof(visits));
        }

        if (farmList.Any(f => f is null))
        {
            throw new ArgumentException("La liste d'élevages ne peut pas contenir d'élément null.", nameof(farms));
        }

        var farmsById = IndexById(farmList);

        var ordered = visitList
            .Where(v => v.Date == date)
            .GroupBy(v => v.FarmId)
            .Select(group => ToRoundFarm(group, farmsById))
            .OrderBy(r => r.Farm.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.Farm.City, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.Farm.Id)
            .ToArray();

        return new DailyRound(date, Array.AsReadOnly(ordered));
    }

    private static Dictionary<Guid, Location> IndexById(IEnumerable<Location> farms)
    {
        var farmsById = new Dictionary<Guid, Location>();
        foreach (var farm in farms)
        {
            if (!farmsById.TryAdd(farm.Id, farm))
            {
                throw new ArgumentException("Deux élevages partagent le même identifiant.", nameof(farms));
            }
        }

        return farmsById;
    }

    private static RoundFarm ToRoundFarm(IGrouping<Guid, Visit> group, Dictionary<Guid, Location> farmsById)
    {
        if (!farmsById.TryGetValue(group.Key, out var farm))
        {
            throw new ArgumentException("Une visite du jour référence un élevage inconnu.", "visits");
        }

        return new RoundFarm(farm, Array.AsReadOnly(group.OrderBy(v => v.Id).ToArray()));
    }
}
