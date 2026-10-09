using TourneeVeto.Domain.Herd;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Domain
{
    
    public sealed record DemoDataSet(
        IReadOnlyList<Location> Locations,
        IReadOnlyList<Cow> Cows);

    /// <summary>
    /// Génère des données de démonstration entièrement fictives et reproductibles
    /// (même <c>today</c> + même <c>seed</c> = mêmes données).
    /// Les durées utilisées sont simplifiées et ne constituent pas des règles cliniques.
    /// </summary>
    public static class DemoData
    {
        public const int FarmCount = 5;
        public const int CowsPerFarm = 100;
        public const int HighCcsThreshold = 250;

        private const int GestationDays = 283;
        private const int DryPeriodDays = 60;

        private static readonly (string Name, string City)[] FictionalFarms =
        [
            ("Ferme Démo des Érables", "Saint-Fictif"),
            ("Ferme Exemple du Rang 4", "Lac-Imaginaire"),
            ("Ferme Fictive Belle-Prairie", "Sainte-Démo"),
            ("Ferme Test des Trois-Collines", "Val-Exemple"),
            ("Ferme Démo du Ruisseau", "Rivière-Fictive"),
            ("Ferme Exemple Mont-Vert", "Saint-Prototype"),
            ("Ferme Fictive de la Butte", "Notre-Dame-du-Test"),
        ];

        private static readonly string[] CowNames =
        [
            "Bella", "Daisy", "Marguerite", "Noisette", "Caramel", "Praline", "Violette", "Perle",
            "Étoile", "Biscotte", "Clochette", "Pâquerette", "Rosie", "Luna", "Prune", "Cannelle",
            "Mirabelle", "Blanchette", "Tulipe", "Framboise", "Muscade", "Câline", "Gaufrette", "Sirop",
        ];

        private enum Profile
        {
            RecentlyCalved,
            CalvingSoon,
            PregnancyCheckDue,
            DryOffDue,
            Routine
        }

        public static DemoDataSet Generate(DateOnly today, int seed)
        {
            var rng = new Random(seed);
            var locations = new List<Location>(FarmCount);
            var cows = new List<Cow>(FarmCount * CowsPerFarm);

            var farms = FictionalFarms.ToArray();
            rng.Shuffle(farms);

            for (var f = 0; f < FarmCount; f++)
            {
                var location = new Location(NewGuid(rng), farms[f].Name, farms[f].City, CowsPerFarm);
                locations.Add(location);
                cows.AddRange(GenerateHerd(location.Id, today, rng));
            }

            return new DemoDataSet(locations, cows);
        }

        private static List<Cow> GenerateHerd(Guid locationId, DateOnly today, Random rng)
        {
            var profiles = new List<Profile>(CowsPerFarm);
            AddProfiles(profiles, Profile.RecentlyCalved, rng.Next(4, 9));
            AddProfiles(profiles, Profile.CalvingSoon, rng.Next(4, 9));
            AddProfiles(profiles, Profile.PregnancyCheckDue, rng.Next(5, 11));
            AddProfiles(profiles, Profile.DryOffDue, rng.Next(3, 8));
            AddProfiles(profiles, Profile.Routine, CowsPerFarm - profiles.Count);

            var shuffled = profiles.ToArray();
            rng.Shuffle(shuffled);

            var herd = shuffled
                .Select((profile, i) => CreateCow(locationId, i + 1, profile, today, rng))
                .ToList();

            ForceHighCcs(herd, rng);
            return herd;
        }

        private static void AddProfiles(List<Profile> profiles, Profile profile, int count)
        {
            for (var i = 0; i < count; i++)
            {
                profiles.Add(profile);
            }
        }

        private static Cow CreateCow(Guid locationId, int number, Profile profile, DateOnly today, Random rng)
        {
            DateOnly? lastCalving;
            DateOnly? lastInsemination;
            ReproductiveStatus status;
            var lactation = rng.Next(1, 7);

            switch (profile)
            {
                case Profile.RecentlyCalved:
                    lastCalving = today.AddDays(-rng.Next(0, 31));
                    lastInsemination = null;
                    status = ReproductiveStatus.Open;
                    break;

                case Profile.CalvingSoon:
                    // Vêlage prévu dans les 30 prochains jours.
                    lastInsemination = today.AddDays(-(GestationDays - rng.Next(0, 31)));
                    lastCalving = lastInsemination.Value.AddDays(-rng.Next(60, 151));
                    status = ReproductiveStatus.Dry;
                    break;

                case Profile.PregnancyCheckDue:
                    lastInsemination = today.AddDays(-rng.Next(28, 46));
                    lastCalving = lastInsemination.Value.AddDays(-rng.Next(50, 121));
                    status = ReproductiveStatus.Bred;
                    break;

                case Profile.DryOffDue:
                    // Tarissement prévu environ 60 jours avant le vêlage attendu (± 7 jours).
                    lastInsemination = today.AddDays(-(GestationDays - DryPeriodDays + rng.Next(-7, 8)));
                    lastCalving = lastInsemination.Value.AddDays(-rng.Next(60, 121));
                    status = ReproductiveStatus.Pregnant;
                    break;

                default:
                    (lactation, lastCalving, lastInsemination, status) = CreateRoutineState(today, rng);
                    break;
            }

            return new Cow(
                NewGuid(rng),
                $"{CowNames[rng.Next(CowNames.Length)]} {number:000}",
                locationId,
                CreateBirthDate(today, lactation, lastCalving, rng),
                lactation,
                lastCalving,
                lastInsemination,
                status,
                lactation == 0 ? null : rng.Next(20, HighCcsThreshold));
        }

        private static (int Lactation, DateOnly? LastCalving, DateOnly? LastInsemination, ReproductiveStatus Status)
            CreateRoutineState(DateOnly today, Random rng)
        {
            var lactation = rng.Next(1, 7);
            var roll = rng.Next(100);

            if (roll < 10)
            {
                var bred = rng.Next(2) == 0;
                return (0, null, bred ? today.AddDays(-rng.Next(1, 27)) : null,
                    bred ? ReproductiveStatus.Bred : ReproductiveStatus.None);
            }

            if (roll < 35)
            {
                return (lactation, today.AddDays(-rng.Next(31, 121)), null, ReproductiveStatus.Open);
            }

            if (roll < 50)
            {
                var insemination = today.AddDays(-rng.Next(1, 27));
                return (lactation, insemination.AddDays(-rng.Next(50, 121)), insemination, ReproductiveStatus.Bred);
            }

            // Gestation confirmée, hors fenêtre de tarissement et de vêlage.
            var pregnantInsemination = today.AddDays(-rng.Next(46, GestationDays - DryPeriodDays - 10));
            return (lactation, pregnantInsemination.AddDays(-rng.Next(50, 121)), pregnantInsemination,
                ReproductiveStatus.Pregnant);
        }

        private static DateOnly CreateBirthDate(DateOnly today, int lactation, DateOnly? lastCalving, Random rng)
        {
            if (lactation == 0)
            {
                return today.AddDays(-rng.Next(400, 700));
            }

            var ageAtFirstCalvingDays = rng.Next(670, 820);
            var previousCalvingIntervalsDays = (lactation - 1) * rng.Next(370, 430);
            return lastCalving!.Value.AddDays(-(ageAtFirstCalvingDays + previousCalvingIntervalsDays));
        }

        private static void ForceHighCcs(List<Cow> herd, Random rng)
        {
            var candidates = herd
                .Select((cow, index) => (cow, index))
                .Where(x => x.cow.Lactation > 0 && x.cow.ReproductionStatus != ReproductiveStatus.Dry)
                .ToArray();
            rng.Shuffle(candidates);

            var count = rng.Next(4, 9);
            for (var i = 0; i < count && i < candidates.Length; i++)
            {
                var ccs = i == 0 ? HighCcsThreshold : rng.Next(HighCcsThreshold, 900);
                herd[candidates[i].index] = candidates[i].cow with { LastCcs = ccs };
            }
        }

        private static Guid NewGuid(Random rng)
        {
            Span<byte> bytes = stackalloc byte[16];
            rng.NextBytes(bytes);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }
    }
}