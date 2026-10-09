using TourneeVeto.Domain;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Tests.Domain;

public sealed class FarmPreviousVisitsSummaryTests
{
    private static readonly DateOnly AujourdHui = new(2026, 10, 9);
    private const int Seed = 42;

    private static Visit Visite(Guid id, Guid farmId, DateOnly date, params VisitAction[] actions) =>
        new(id, farmId, date, "Cause fictive", string.Empty, null) { Actions = actions };

    private static VisitAction Action(Guid id, Guid cowId, bool done, DateOnly date, ActionType type = ActionType.Calving) =>
        new(id, cowId, type, date, done, "Fictif");

    private static Guid G(int n) => new(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Seulement_les_visites_anterieures_strictes_du_bon_elevage()
    {
        var jeu = DemoData.GenerateSeed(AujourdHui, Seed);
        var elevage = jeu.Herd.Locations[0];

        var synthese = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, jeu.Visits);

        var visite = Assert.Single(synthese.PreviousVisits);
        Assert.Equal(AujourdHui.AddDays(-30), visite.Date);
        Assert.Equal(elevage.Id, visite.FarmId);
        Assert.Equal(elevage.Id, synthese.FarmId);
        Assert.Equal(AujourdHui, synthese.ReferenceDate);
        Assert.Empty(synthese.PendingActions); // l'action de l'historique fictif est réalisée
    }

    [Fact]
    public void La_date_de_reference_est_exclue_et_la_veille_incluse()
    {
        var elevage = G(1);
        var visites = new[]
        {
            Visite(G(10), elevage, AujourdHui),
            Visite(G(11), elevage, AujourdHui.AddDays(-1)),
            Visite(G(12), elevage, AujourdHui.AddDays(1)),
        };

        var synthese = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, visites);

        Assert.Equal([G(11)], synthese.PreviousVisits.Select(v => v.Id));
    }

    [Fact]
    public void Les_actions_en_suspens_excluent_les_realisees_et_les_autres_elevages()
    {
        var jeu = DemoData.GenerateSeed(AujourdHui, Seed);
        var elevage = jeu.Herd.Locations[1];
        var autre = jeu.Herd.Locations[2];
        var vacheA = jeu.Herd.Cows.First(c => c.LocationId == elevage.Id).Id;
        var vacheB = jeu.Herd.Cows.First(c => c.LocationId == autre.Id).Id;
        var passee = AujourdHui.AddDays(-10);
        var visites = jeu.Visits.Concat(
        [
            Visite(G(20), elevage.Id, passee,
                Action(G(21), vacheA, false, AujourdHui.AddDays(100)),
                Action(G(22), vacheA, true, passee)),
            Visite(G(23), autre.Id, passee, Action(G(24), vacheB, false, passee)),
        ]).ToArray();

        var synthese = FarmPreviousVisitsSummary.Build(elevage.Id, AujourdHui, visites);

        var attendue = Assert.Single(synthese.PendingActions);
        Assert.Equal(G(21), attendue.Action.Id);
        Assert.Equal(vacheA, attendue.Action.CowId);
        Assert.Equal(G(20), attendue.VisitId);
        Assert.Equal(passee, attendue.VisitDate);
        Assert.False(attendue.Action.IsCompleted);
    }

    [Fact]
    public void Les_actions_en_suspens_d_une_visite_non_anterieure_sont_ignorees()
    {
        var elevage = G(1);
        var visites = new[] { Visite(G(10), elevage, AujourdHui, Action(G(11), G(5), false, AujourdHui)) };

        var synthese = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, visites);

        Assert.Empty(synthese.PreviousVisits);
        Assert.Empty(synthese.PendingActions);
    }

    [Fact]
    public void Le_resultat_est_deterministe_quel_que_soit_l_ordre_des_entrees()
    {
        var elevage = G(1);
        var visites = new[]
        {
            Visite(G(12), elevage, AujourdHui.AddDays(-5),
                Action(G(32), G(7), false, AujourdHui.AddDays(-5)),
                Action(G(31), G(6), false, AujourdHui.AddDays(-5)),
                Action(G(30), G(6), false, AujourdHui.AddDays(-9))),
            Visite(G(11), elevage, AujourdHui.AddDays(-5)),
            Visite(G(13), elevage, AujourdHui.AddDays(-20), Action(G(33), G(5), false, AujourdHui.AddDays(-20))),
        };

        var direct = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, visites);
        var inverse = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, visites.Reverse().ToArray());

        Assert.Equal([G(11), G(12), G(13)], direct.PreviousVisits.Select(v => v.Id));
        Assert.Equal([G(30), G(31), G(32), G(33)], direct.PendingActions.Select(a => a.Action.Id));
        Assert.Equal(direct.PreviousVisits, inverse.PreviousVisits);
        Assert.Equal(direct.PendingActions, inverse.PendingActions);
    }

    [Fact]
    public void Les_actions_a_date_et_vache_egales_sont_triees_par_type_puis_par_id()
    {
        var elevage = G(1);
        var jour = AujourdHui.AddDays(-3);
        var visites = new[]
        {
            Visite(G(10), elevage, jour,
                Action(G(42), G(6), false, jour, ActionType.Insemination),
                Action(G(41), G(6), false, jour, ActionType.Calving),
                Action(G(40), G(6), false, jour, ActionType.Calving),
                Action(G(43), G(6), false, jour, ActionType.PregnancyDiagnosis)),
        };

        var synthese = FarmPreviousVisitsSummary.Build(elevage, AujourdHui, visites);

        Assert.Equal([G(43), G(40), G(41), G(42)], synthese.PendingActions.Select(a => a.Action.Id));
    }

    [Fact]
    public void Sans_visite_la_synthese_est_vide()
    {
        var synthese = FarmPreviousVisitsSummary.Build(G(1), AujourdHui, []);

        Assert.Empty(synthese.PreviousVisits);
        Assert.Empty(synthese.PendingActions);
    }

    [Fact]
    public void Un_elevage_inconnu_donne_une_synthese_vide()
    {
        var jeu = DemoData.GenerateSeed(AujourdHui, Seed);

        var synthese = FarmPreviousVisitsSummary.Build(G(999), AujourdHui, jeu.Visits);

        Assert.Empty(synthese.PreviousVisits);
        Assert.Empty(synthese.PendingActions);
    }

    [Fact]
    public void Un_identifiant_d_elevage_vide_est_refuse()
    {
        Assert.Throws<ArgumentException>("farmId", () => FarmPreviousVisitsSummary.Build(Guid.Empty, AujourdHui, []));
    }

    [Fact]
    public void Une_date_de_reference_par_defaut_est_refusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>("referenceDate",
            () => FarmPreviousVisitsSummary.Build(G(1), default, []));
    }

    [Fact]
    public void Une_liste_de_visites_null_ou_avec_element_null_est_refusee()
    {
        Assert.Throws<ArgumentNullException>("visits", () => FarmPreviousVisitsSummary.Build(G(1), AujourdHui, null!));
        Assert.Throws<ArgumentException>("visits",
            () => FarmPreviousVisitsSummary.Build(G(1), AujourdHui, new Visit[] { null! }));
    }

    [Fact]
    public void Un_elevage_null_est_refuse()
    {
        Assert.Throws<ArgumentNullException>("farm", () => FarmPreviousVisitsSummary.Build((Location)null!, AujourdHui, []));
    }
}
