using Bunit;
using TourneeVeto.Domain.Biosecurity;
using TourneeVeto.Ui.Components;

namespace TourneeVeto.Tests.Ui;

public sealed class BiosecurityChecklistTests
{
    [Fact]
    public void Rendu_AfficheTousLesElementsEtLesChoixDuJeuDeReference()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, BiosecurityChecklistReference.Questions)
            .Add(c => c.ResponseChanged, _ => Task.CompletedTask));

        var elements = composant.FindAll(".biosecurity-checklist__item");
        Assert.Equal(4, elements.Count);
        Assert.Equal(BiosecurityChecklistReference.Questions.Count, elements.Count);
        Assert.Equal(elements.Count, BiosecurityChecklistReference.Questions
            .Select(question => question.Id).Distinct().Count());
        Assert.Equal(BiosecurityChecklistReference.Questions.Sum(question => question.Options.Count),
            BiosecurityChecklistReference.Questions.SelectMany(question => question.Options)
                .Select(option => option.Id).Distinct().Count());

        foreach (var question in BiosecurityChecklistReference.Questions)
        {
            var element = Assert.Single(elements, item =>
                item.QuerySelector("legend")?.TextContent.Contains(question.Prompt, StringComparison.Ordinal) == true);

            Assert.Contains(question.Section, element.TextContent);
            Assert.Equal("Non renseigné", element.QuerySelector(".biosecurity-checklist__status")?.TextContent.Trim());
            Assert.Equal(question.Options.Count, element.QuerySelectorAll("input[type='radio']").Length);
            foreach (var option in question.Options)
            {
                Assert.Contains(option.Label, element.TextContent);
            }
        }
    }

    [Fact]
    public void Selection_EmetLaNouvelleReponseSansModifierLesAutresReponses()
    {
        using var contexte = new BunitContext();
        var questions = BiosecurityChecklistReference.Questions;
        var question = questions[0];
        var autreQuestion = questions[1];
        var reponseInitiale = new BiosecurityResponse(question.Id, question.Options[0].Id);
        var reponseIndependante = new BiosecurityResponse(autreQuestion.Id, autreQuestion.Options[1].Id);
        IReadOnlyList<BiosecurityResponse> reponses = [reponseInitiale, reponseIndependante];
        BiosecurityResponse? reponseEmise = null;

        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, questions)
            .Add(c => c.Responses, reponses)
            .Add(c => c.ResponseChanged, reponse =>
            {
                reponseEmise = reponse;
                return Task.CompletedTask;
            }));

        var nouvelleOption = question.Options[1];
        var nouveauChoix = composant.Find(
            $"input[data-question-id='{question.Id}'][value='{nouvelleOption.Id}']");
        nouveauChoix.Change(nouvelleOption.Id.ToString());

        Assert.Equal(new BiosecurityResponse(question.Id, nouvelleOption.Id), reponseEmise);
        Assert.Equal([reponseInitiale, reponseIndependante], composant.Instance.Responses);

        var reponsesMisesAJour = new[]
        {
            new BiosecurityResponse(question.Id, nouvelleOption.Id),
            reponseIndependante
        };
        composant.Render(p => p
            .Add(c => c.Questions, questions)
            .Add(c => c.Responses, reponsesMisesAJour)
            .Add(c => c.ResponseChanged, _ => Task.CompletedTask));

        Assert.False(composant.Find(
            $"input[data-question-id='{question.Id}'][value='{question.Options[0].Id}']")
            .HasAttribute("checked"));
        Assert.True(composant.Find(
            $"input[data-question-id='{question.Id}'][value='{nouvelleOption.Id}']")
            .HasAttribute("checked"));
        Assert.True(composant.Find(
            $"input[data-question-id='{autreQuestion.Id}'][value='{autreQuestion.Options[1].Id}']")
            .HasAttribute("checked"));
        Assert.Contains(nouvelleOption.Label, composant.FindAll("fieldset")[0]
            .QuerySelector(".biosecurity-checklist__status")?.TextContent ?? string.Empty);
    }

    [Fact]
    public void Rendu_RestitueLeChoixFourniParLeParentEtLesIdentifiantsSontAccessibles()
    {
        using var contexte = new BunitContext();
        var question = BiosecurityChecklistReference.Questions[0];
        var optionChoisie = question.Options[1];
        var reponse = new BiosecurityResponse(question.Id, optionChoisie.Id);

        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, BiosecurityChecklistReference.Questions)
            .Add(c => c.Responses, [reponse])
            .Add(c => c.ResponseChanged, _ => Task.CompletedTask));

        var champSelectionne = composant.Find(
            $"input[data-question-id='{question.Id}'][value='{optionChoisie.Id}']");
        Assert.True(champSelectionne.HasAttribute("checked"));
        Assert.Equal(champSelectionne.Id,
            composant.Find($"label[for='{champSelectionne.Id}']").GetAttribute("for"));

        var groupes = composant.FindAll("fieldset")
            .Select(groupe => groupe.QuerySelector("input[type='radio']")?.GetAttribute("name"))
            .ToArray();
        Assert.Equal(BiosecurityChecklistReference.Questions.Count, groupes.Length);
        Assert.Equal(groupes.Length, groupes.Distinct().Count());
    }

    [Fact]
    public void ListeIndisponible_AfficheUneErreurEtNePresentePasUnBilanVide()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, (IReadOnlyList<BiosecurityQuestion>?)null));

        Assert.Equal("alert", composant.Find("[role='alert']").GetAttribute("role"));
        Assert.Contains("La liste de contrôle de biosécurité ne peut pas être chargée",
            composant.Find("[role='alert']").TextContent);
        Assert.Empty(composant.FindAll("fieldset"));
        Assert.Empty(composant.FindAll("input"));
    }

    [Fact]
    public void ListeVide_NEstPasPresenteeCommeUnBilanComplet()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, Array.Empty<BiosecurityQuestion>()));

        Assert.Equal("alert", composant.Find("[role='alert']").GetAttribute("role"));
        Assert.Empty(composant.FindAll("fieldset"));
        Assert.Contains("Aucun bilan vide n’est présenté comme complet",
            composant.Find("[role='alert']").TextContent);
    }

    [Fact]
    public void Rendu_SansGestionnaireNePermetPasUneSaisieSansDestination()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, BiosecurityChecklistReference.Questions));

        Assert.All(composant.FindAll("input[type='radio']"),
            choix => Assert.True(choix.HasAttribute("disabled")));
    }

    [Fact]
    public void Reponses_RefuseUnChoixAbsentDeLaQuestion()
    {
        using var contexte = new BunitContext();
        var question = BiosecurityChecklistReference.Questions[0];

        Assert.Throws<ArgumentException>(() => contexte.Render<BiosecurityChecklist>(p => p
            .Add(c => c.Questions, BiosecurityChecklistReference.Questions)
            .Add(c => c.Responses,
                [new BiosecurityResponse(question.Id, Guid.NewGuid())])));
    }
}
