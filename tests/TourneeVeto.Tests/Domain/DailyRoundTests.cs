using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Tests.Domain;

public sealed class DailyRoundTests
{
    private static readonly DateOnly Jour = new(2026, 10, 9);

    private static Location Ferme(int n, string nom = "Ferme", string ville = "Ville") =>
        new(Guid.Parse($"c0000000-0000-0000-0000-{n:D12}"), nom, ville, 50);

    private static Visit Visite(int n, Location ferme, DateOnly date) =>
        new(Guid.Parse($"d0000000-0000-0000-0000-{n:D12}"), ferme.Id, date, "Contrôle", "", null);

    [Fact]
    public void Tournee_contient_les_elevages_ayant_une_visite_ce_jour()
    {
        var a = Ferme(1, "A");
        var b = Ferme(2, "B");

        var tournee = DailyRound.Create(Jour, [Visite(1, a, Jour), Visite(2, b, Jour)], [a, b]);

        Assert.Equal(Jour, tournee.Date);
        Assert.Equal([a.Id, b.Id], tournee.Farms.Select(f => f.Farm.Id));
    }

    [Fact]
    public void Tournee_exclut_les_elevages_prevus_uniquement_a_une_autre_date()
    {
        var a = Ferme(1, "A");
        var b = Ferme(2, "B");
        var c = Ferme(3, "C");

        var tournee = DailyRound.Create(
            Jour,
            [Visite(1, a, Jour), Visite(2, b, Jour.AddDays(1)), Visite(3, c, Jour.AddDays(-1))],
            [a, b, c]);

        var ferme = Assert.Single(tournee.Farms);
        Assert.Equal(a.Id, ferme.Farm.Id);
    }

    [Fact]
    public void Elevage_avec_visites_a_plusieurs_dates_ne_garde_que_celles_du_jour()
    {
        var a = Ferme(1);
        var duJour = Visite(1, a, Jour);

        var tournee = DailyRound.Create(Jour, [Visite(2, a, Jour.AddDays(7)), duJour, Visite(3, a, Jour.AddDays(-7))], [a]);

        var ferme = Assert.Single(tournee.Farms);
        Assert.Equal([duJour.Id], ferme.Visits.Select(v => v.Id));
    }

    [Fact]
    public void Elevage_avec_deux_visites_le_meme_jour_apparait_une_seule_fois()
    {
        var a = Ferme(1);

        var tournee = DailyRound.Create(Jour, [Visite(2, a, Jour), Visite(1, a, Jour)], [a]);

        var ferme = Assert.Single(tournee.Farms);
        Assert.Equal(2, ferme.Visits.Count);
    }

    [Fact]
    public void Elevages_homonymes_restent_distincts_par_identifiant()
    {
        var a1 = Ferme(1, "Ferme", "Ville");
        var a2 = Ferme(2, "Ferme", "Ville");

        var tournee = DailyRound.Create(Jour, [Visite(1, a2, Jour), Visite(2, a1, Jour)], [a1, a2]);

        Assert.Equal([a1.Id, a2.Id], tournee.Farms.Select(f => f.Farm.Id));
        Assert.All(tournee.Farms, f => Assert.All(f.Visits, v => Assert.Equal(f.Farm.Id, v.FarmId)));
    }

    [Fact]
    public void Ordre_est_independant_de_l_ordre_des_entrees()
    {
        var a = Ferme(1, "Alpha");
        var b = Ferme(2, "Bravo");
        var visites = new[] { Visite(1, a, Jour), Visite(2, b, Jour) };

        var direct = DailyRound.Create(Jour, visites, [a, b]);
        var inverse = DailyRound.Create(Jour, visites.Reverse(), [b, a]);

        Assert.Equal(direct.Farms.Select(f => f.Farm.Id), inverse.Farms.Select(f => f.Farm.Id));
    }

    [Fact]
    public void Aucune_visite_ce_jour_donne_une_tournee_vide()
    {
        var a = Ferme(1);

        Assert.Empty(DailyRound.Create(Jour, [Visite(1, a, Jour.AddDays(1))], [a]).Farms);
        Assert.Empty(DailyRound.Create(Jour, [], []).Farms);
    }

    [Fact]
    public void Elevage_sans_visite_n_est_pas_dans_la_tournee()
    {
        var a = Ferme(1);
        var b = Ferme(2);

        var tournee = DailyRound.Create(Jour, [Visite(1, a, Jour)], [a, b]);

        Assert.DoesNotContain(tournee.Farms, f => f.Farm.Id == b.Id);
    }

    [Fact]
    public void Dates_limites_sont_comparees_exactement()
    {
        var a = Ferme(1);
        var fin = new DateOnly(2026, 12, 31);

        var tournee = DailyRound.Create(fin, [Visite(1, a, fin), Visite(2, a, fin.AddDays(1))], [a]);

        Assert.Single(Assert.Single(tournee.Farms).Visits);
    }

    [Fact]
    public void Date_par_defaut_est_refusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRound.Create(default, [], []));
    }

    [Fact]
    public void Collections_null_sont_refusees()
    {
        Assert.Throws<ArgumentNullException>(() => DailyRound.Create(Jour, null!, []));
        Assert.Throws<ArgumentNullException>(() => DailyRound.Create(Jour, [], null!));
    }

    [Fact]
    public void Elements_null_sont_refuses()
    {
        var a = Ferme(1);

        Assert.Throws<ArgumentException>(() => DailyRound.Create(Jour, [null!], [a]));
        Assert.Throws<ArgumentException>(() => DailyRound.Create(Jour, [], [null!]));
    }

    [Fact]
    public void Identifiants_d_elevage_en_double_sont_refuses()
    {
        var a = Ferme(1);

        Assert.Throws<ArgumentException>(() => DailyRound.Create(Jour, [], [a, a]));
    }

    [Fact]
    public void Visite_du_jour_d_un_elevage_inconnu_est_refusee()
    {
        var a = Ferme(1);
        var inconnu = Ferme(9);

        Assert.Throws<ArgumentException>(() => DailyRound.Create(Jour, [Visite(1, inconnu, Jour)], [a]));
    }

    [Fact]
    public void Visite_d_un_autre_jour_d_un_elevage_inconnu_est_ignoree()
    {
        var a = Ferme(1);
        var inconnu = Ferme(9);

        var tournee = DailyRound.Create(Jour, [Visite(1, inconnu, Jour.AddDays(1))], [a]);

        Assert.Empty(tournee.Farms);
    }
}
