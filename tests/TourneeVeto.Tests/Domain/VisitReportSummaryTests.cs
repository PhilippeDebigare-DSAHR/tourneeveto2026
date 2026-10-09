using TourneeVeto.Domain.Biosecurity;
using TourneeVeto.Domain.Visits;

namespace TourneeVeto.Tests.Domain;

public sealed class VisitReportSummaryTests
{
    private static readonly DateOnly Jour = new(2026, 10, 9);

    private static BiosecurityQuestion Q(int n, string section, int poids = 1, bool critique = false) =>
        new(Guid.Parse($"a0000000-0000-0000-0000-{n:D12}"), section, poids, critique)
        {
            Prompt = $"Question {n}",
            Options =
            [
                new(Guid.Parse($"b0000000-0000-0000-{n:D4}-000000000001"), Answer.Yes, "Oui"),
                new(Guid.Parse($"b0000000-0000-0000-{n:D4}-000000000002"), Answer.Partially, "Partiellement"),
                new(Guid.Parse($"b0000000-0000-0000-{n:D4}-000000000003"), Answer.No, "Non"),
                new(Guid.Parse($"b0000000-0000-0000-{n:D4}-000000000004"), Answer.None, "Aucune")
            ]
        };

    private static BiosecurityResponse R(BiosecurityQuestion q, Answer a) =>
        new(q.Id, q.Options.First(o => o.Value == a).Id);

    private static Visit V(params BiosecurityResponse[] responses) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Jour, "Contrôle", "Notes", null)
        { BiosecurityResponses = responses };

    private static Visit V0(Guid? photo, params VisitAction[] actions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Jour, "Cause", "Constat", photo) { Actions = actions };

    [Fact]
    public void Score_PondereOuiPartiellementNon()
    {
        var q1 = Q(1, "A"); var q2 = Q(2, "A", 2); var q3 = Q(3, "B");
        var s = VisitReportSummary.Create(
            V(R(q1, Answer.Yes), R(q2, Answer.Partially), R(q3, Answer.No)), [q1, q2, q3]);

        Assert.Equal(50m, s.ScorePercent); // (1 + 1 + 0) / 4
        Assert.Equal(66.7m, s.Sections[0].ScorePercent); // (1 + 1) / 3
        Assert.True(s.IsBiosecurityComplete);
    }

    [Theory]
    [InlineData(Answer.Yes, 100.0)]
    [InlineData(Answer.Partially, 50.0)]
    [InlineData(Answer.No, 0.0)]
    public void Score_UneSeuleReponse_AppliqueLaPonderationValidee(Answer answer, double attendu)
    {
        var q = Q(1, "A");
        var s = VisitReportSummary.Create(V(R(q, answer)), [q]);
        Assert.Equal((decimal)attendu, s.ScorePercent);
        Assert.Equal((decimal)attendu, s.Sections[0].ScorePercent);
        Assert.True(s.Sections[0].IsComplete);
    }

    [Fact]
    public void Referentiel_TroupeauFerme_EstFavorableEtNonPrioritaire()
    {
        var questions = BiosecurityChecklistReference.Questions;
        var acquisition = questions.Single(q => q.Prompt.StartsWith("Acquisition de bovins extérieurs", StringComparison.Ordinal));
        var ferme = acquisition.Options.Single(o => o.Label.Contains("troupeau fermé", StringComparison.Ordinal));
        var reponses = questions
            .Select(q => q.Id == acquisition.Id
                ? new BiosecurityResponse(q.Id, ferme.Id)
                : new BiosecurityResponse(q.Id, q.Options.First(o => o.Value == Answer.Yes).Id))
            .ToArray();

        var s = VisitReportSummary.Create(V(reponses), questions);

        Assert.Equal(Answer.Yes, ferme.Value);
        Assert.Equal(100m, s.ScorePercent);
        Assert.Empty(s.PriorityPractices);
    }

    [Theory]
    [InlineData("Aucun bovin introduit (troupeau fermé)", Answer.Yes, 100.0, 0)]
    [InlineData("Oui, avec certificat sanitaire", Answer.Partially, 50.0, 1)]
    [InlineData("Oui, sans contrôle formel", Answer.No, 0.0, 1)]
    public void Referentiel_AcquisitionBovins_CorrespondanceLibelleNiveauScoreEtPriorite(
        string libelle, Answer attendu, double score, int priorites)
    {
        var acquisition = BiosecurityChecklistReference.Questions
            .Single(q => q.Prompt.StartsWith("Acquisition de bovins extérieurs", StringComparison.Ordinal));
        var option = acquisition.Options.Single(o => o.Label == libelle);

        var s = VisitReportSummary.Create(V(new BiosecurityResponse(acquisition.Id, option.Id)), [acquisition]);

        Assert.Equal(attendu, option.Value);
        Assert.Equal((decimal)score, s.ScorePercent);
        Assert.Equal(priorites, s.PriorityPractices.Count);
        Assert.Equal(3, acquisition.Options.Count);
    }

    [Fact]
    public void Referentiel_IdentifiantsUniquesEtNiveauxDistinctsParQuestion()
    {
        var questions = BiosecurityChecklistReference.Questions;

        Assert.Equal(questions.Count, questions.Select(q => q.Id).Distinct().Count());
        var optionIds = questions.SelectMany(q => q.Options).Select(o => o.Id).ToList();
        Assert.Equal(optionIds.Count, optionIds.Distinct().Count());
        foreach (var q in questions)
        {
            Assert.Equal(q.Options.Count, q.Options.Select(o => o.Value).Distinct().Count());
            Assert.DoesNotContain(q.Options, o => o.Value == Answer.None);
        }
    }

    [Fact]
    public void Score_AucuneNEstPasComptee()
    {
        var q1 = Q(1, "A"); var q2 = Q(2, "A");
        var s = VisitReportSummary.Create(V(R(q1, Answer.Yes), R(q2, Answer.None)), [q1, q2]);
        Assert.Equal(100m, s.ScorePercent);
        Assert.Equal(1, s.AnsweredCount);
        Assert.False(s.IsBiosecurityComplete);
    }

    [Fact]
    public void PratiquesPrioritaires_LimiteATroisEtCritiquesAvantNonCritiques()
    {
        var nonNormal1 = Q(1, "A"); var nonNormal2 = Q(2, "A"); var nonNormal3 = Q(3, "A");
        var partielCritique = Q(4, "B", critique: true);
        var nonCritique = Q(5, "B", critique: true);
        var aucune = Q(6, "B", critique: true);
        var qs = new[] { nonNormal1, nonNormal2, nonNormal3, partielCritique, nonCritique, aucune };

        var s = VisitReportSummary.Create(
            V(R(nonNormal1, Answer.No), R(nonNormal2, Answer.No), R(nonNormal3, Answer.No),
              R(partielCritique, Answer.Partially), R(nonCritique, Answer.No), R(aucune, Answer.None)), qs);

        Assert.Equal(3, s.PriorityPractices.Count);
        Assert.Equal([nonCritique.Id, partielCritique.Id, nonNormal1.Id],
            s.PriorityPractices.Select(p => p.QuestionId));
        Assert.DoesNotContain(s.PriorityPractices, p => p.QuestionId == aucune.Id);
    }

    [Fact]
    public void Score_SectionAPoidsInegauxArrondiAUneDecimale()
    {
        var q1 = Q(1, "A"); var q2 = Q(2, "A", 2);
        var s = VisitReportSummary.Create(V(R(q1, Answer.Yes), R(q2, Answer.No)), [q1, q2]);
        Assert.Equal(33.3m, s.ScorePercent);
    }

    [Fact]
    public void SectionIncompleteOuNonRenseignee_JamaisPresenteeCommeComplete()
    {
        var q1 = Q(1, "A"); var q2 = Q(2, "A"); var q3 = Q(3, "B"); var q4 = Q(4, "C");
        var s = VisitReportSummary.Create(
            V(R(q1, Answer.Yes), R(q4, Answer.None)), [q1, q2, q3, q4]);

        Assert.False(s.Sections[0].IsComplete);
        Assert.Equal(1, s.Sections[0].AnsweredCount);
        Assert.False(s.Sections[1].IsComplete);
        Assert.Null(s.Sections[1].ScorePercent);
        Assert.False(s.Sections[2].IsComplete); // « Aucune » = non renseigné
        Assert.Null(s.Sections[2].ScorePercent);
        Assert.False(s.IsBiosecurityComplete);
        Assert.Equal(1, s.AnsweredCount);
        Assert.Equal(4, s.QuestionCount);
    }

    [Fact]
    public void SansReponse_ScoreNulEtNonComplet()
    {
        var q1 = Q(1, "A");
        var s = VisitReportSummary.Create(V(), [q1]);
        Assert.Null(s.ScorePercent);
        Assert.False(s.IsBiosecurityComplete);
        Assert.Empty(s.PriorityPractices);
    }

    [Fact]
    public void ReferentielVide_NonComplet()
    {
        var s = VisitReportSummary.Create(V(), []);
        Assert.False(s.IsBiosecurityComplete);
        Assert.Null(s.ScorePercent);
        Assert.Empty(s.Sections);
    }

    [Fact]
    public void PratiquesPrioritaires_TroisMaxCritiquesPuisNonPuisPartiellement_FavorablesExclues()
    {
        var oui = Q(1, "A", critique: true);
        var partielCritique = Q(2, "A", critique: true);
        var nonNormal = Q(3, "B");
        var nonLourd = Q(4, "B", poids: 3);
        var partielNormal = Q(5, "C");
        var qs = new[] { oui, partielCritique, nonNormal, nonLourd, partielNormal };

        var s = VisitReportSummary.Create(
            V(R(oui, Answer.Yes), R(partielCritique, Answer.Partially), R(nonNormal, Answer.No),
              R(nonLourd, Answer.No), R(partielNormal, Answer.Partially)), qs);

        Assert.Equal([partielCritique.Id, nonLourd.Id, nonNormal.Id],
            s.PriorityPractices.Select(p => p.QuestionId));
        Assert.True(s.PriorityPractices[0].IsCritique);
        Assert.Equal("Partiellement", s.PriorityPractices[0].AnswerLabel);
    }

    [Fact]
    public void PratiquesPrioritaires_ExclusivementReponsesEnregistrees()
    {
        var q1 = Q(1, "A", critique: true); var q2 = Q(2, "A");
        var s = VisitReportSummary.Create(V(R(q2, Answer.No)), [q1, q2]);
        Assert.Equal([q2.Id], s.PriorityPractices.Select(p => p.QuestionId));
    }

    [Fact]
    public void Actions_SeparentFaitesEtAFaire_TrieesParDate()
    {
        var fait = new VisitAction(Guid.NewGuid(), Guid.NewGuid(), ActionType.Calving, Jour, true, "");
        var tard = new VisitAction(Guid.NewGuid(), Guid.NewGuid(), ActionType.DryOff, Jour.AddDays(5), false, "");
        var tot = new VisitAction(Guid.NewGuid(), Guid.NewGuid(), ActionType.Insemination, Jour.AddDays(1), false, "");
        var photo = Guid.NewGuid();

        var s = VisitReportSummary.Create(V0(photo, tard, fait, tot), []);

        Assert.Equal([fait], s.CompletedActions);
        Assert.Equal([tot, tard], s.PendingActions);
        Assert.Equal("Cause", s.Cause);
        Assert.Equal("Constat", s.Notes);
        Assert.Equal(photo, s.PhotoId);
    }

    [Fact]
    public void SansPhoto_ReferenceNulle()
    {
        Assert.Null(VisitReportSummary.Create(V0(null), []).PhotoId);
    }

    [Fact]
    public void Invalides_Arguments()
    {
        Assert.Throws<ArgumentNullException>(() => VisitReportSummary.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => VisitReportSummary.Create(V(), null!));
    }

    [Fact]
    public void Invalides_ReponseQuestionOuOptionInconnue()
    {
        var q1 = Q(1, "A"); var q2 = Q(2, "A");
        Assert.Throws<ArgumentException>(() =>
            VisitReportSummary.Create(V(R(q2, Answer.Yes)), [q1]));
        Assert.Throws<ArgumentException>(() =>
            VisitReportSummary.Create(V(new BiosecurityResponse(q1.Id, Guid.NewGuid())), [q1]));
        Assert.Throws<ArgumentException>(() =>
            VisitReportSummary.Create(V(new BiosecurityResponse(q1.Id, q2.Options[0].Id)), [q1, q2]));
    }

    [Fact]
    public void Invalides_ReponseEnDoubleEtReferentielIncoherent()
    {
        var q1 = Q(1, "A");
        Assert.Throws<ArgumentException>(() =>
            VisitReportSummary.Create(V(R(q1, Answer.Yes), R(q1, Answer.No)), [q1]));
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [q1, q1]));
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [Q(2, "A", poids: 0)]));
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [Q(3, " ")]));
    }

    [Fact]
    public void Invalide_OptionNulle_EstRejetee()
    {
        var q = Q(1, "A") with { Options = [null!] };
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [q]));
    }

    [Fact]
    public void Invalide_ListeOptionsNulle_EstRejetee()
    {
        var q = Q(1, "A") with { Options = null! };
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [q]));
    }

    [Fact]
    public void Invalide_IdentifiantsOptionDupliques_SontRejetes()
    {
        var q = Q(1, "A");
        var doublon = q with { Options = [q.Options[0], q.Options[1] with { Id = q.Options[0].Id }] };
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [doublon]));
    }

    [Fact]
    public void Invalide_ValeurAnswerInconnue_EstRejetee()
    {
        var q = Q(1, "A");
        var inconnue = q with { Options = [q.Options[0], q.Options[1] with { Value = (Answer)99 }] };
        Assert.Throws<ArgumentException>(() => VisitReportSummary.Create(V(), [inconnue]));
    }
}
