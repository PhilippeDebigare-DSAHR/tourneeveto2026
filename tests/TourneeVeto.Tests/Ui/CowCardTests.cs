using Bunit;
using TourneeVeto.Domain.Visits;
using TourneeVeto.Ui.Components;

namespace TourneeVeto.Tests.Ui;

public sealed class CowCardTests
{
    [Theory]
    [InlineData(ActionType.PregnancyDiagnosis, "Diagnostic de gestation")]
    [InlineData(ActionType.HighSomaticCellCount, "CCS élevée")]
    [InlineData(ActionType.DryOff, "Tarissement")]
    [InlineData(ActionType.Calving, "Vêlage")]
    [InlineData(ActionType.Insemination, "Insémination")]
    public void Rendu_AfficheLeTypeFourniSansCalculClinique(ActionType type, string texte)
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen de démonstration")
            .Add(c => c.ActionType, type)
            .Add(c => c.Urgency, Urgency.Routine));

        Assert.Contains(texte, composant.Find(".cow-card__category").TextContent);
        var article = composant.Find("article");
        Assert.Equal(composant.Find("h3").Id, article.GetAttribute("aria-labelledby"));
        Assert.Contains("#1042", article.TextContent);
        Assert.Contains("Examen de démonstration", composant.Find("h3").TextContent);
    }

    [Theory]
    [InlineData(Urgency.Routine, "routine", "Suivi courant")]
    [InlineData(Urgency.Warning, "warning", "À surveiller")]
    [InlineData(Urgency.Urgent, "urgent", "Urgent")]
    public void Rendu_LUrgenceEstExpliciteEtIndependanteDuType(Urgency urgence, string style, string texte)
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1087")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.ActionType, ActionType.HighSomaticCellCount)
            .Add(c => c.Urgency, urgence));

        Assert.Contains($"cow-card--{style}", composant.Find("article").ClassList);
        Assert.Equal(texte, composant.Find(".cow-card__urgency").TextContent);
    }

    [Theory]
    [InlineData(false, "false", "À faire", true)]
    [InlineData(true, "true", "Réalisée", false)]
    public void Basculer_TransmetLeNouvelEtatSansModifierLeParametre(
        bool realisee, string pressed, string texte, bool attendu)
    {
        using var contexte = new BunitContext();
        bool? recu = null;
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.IsCompleted, realisee)
            .Add(c => c.IsCompletedChanged, valeur => recu = valeur));

        var bouton = composant.Find("button");
        Assert.Equal(pressed, bouton.GetAttribute("aria-pressed"));
        Assert.Equal("Action réalisée pour la vache #1042", bouton.GetAttribute("aria-label"));
        Assert.Contains(texte, bouton.TextContent);
        bouton.Click();

        Assert.Equal(attendu, recu);
        Assert.Equal(realisee, composant.Instance.IsCompleted);
    }

    [Fact]
    public void Note_LabelUniqueEtTransmissionAuParentSansMutation()
    {
        using var contexte = new BunitContext();
        string? noteRecue = null;
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.Note, "Note initiale")
            .Add(c => c.NoteChanged, valeur => noteRecue = valeur));
        var autre = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Autre action"));

        var champ = composant.Find("textarea");
        Assert.Equal(champ.Id, composant.Find("label").GetAttribute("for"));
        Assert.NotEqual(autre.Find("textarea").Id, champ.Id);
        champ.Input("Observation fictive");

        Assert.Equal("Observation fictive", noteRecue);
        Assert.Equal("Note initiale", composant.Instance.Note);
    }

    [Fact]
    public void Rendu_EchappeLesDonneesEtNInventePasDeBague()
    {
        using var contexte = new BunitContext();
        const string contenu = "<script>alert('x')</script>";
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, contenu)
            .Add(c => c.ActionTitle, contenu)
            .Add(c => c.Protocol, contenu)
            .Add(c => c.Note, contenu));

        Assert.Empty(composant.FindAll("script"));
        Assert.Empty(composant.FindAll(".cow-card__ear-tag"));
        Assert.Contains(contenu, composant.Find("h3").TextContent);
        Assert.Equal(contenu, composant.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Rendu_AfficheLaBagueEtLeProtocoleFournis()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.EarTag, "CA 04-981-1042")
            .Add(c => c.Protocol, "Échographie"));

        Assert.Contains("CA 04-981-1042", composant.Find(".cow-card__ear-tag").TextContent);
        Assert.Equal("Échographie", composant.Find(".cow-card__protocol").TextContent);
    }

    [Fact]
    public void Rendu_RefleteLesMisesAJourDuParent()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.Note, "Note initiale"));

        composant.Render(p => p
            .Add(c => c.IsCompleted, true)
            .Add(c => c.Note, "Note enregistrée par le parent")
            .Add(c => c.Urgency, Urgency.Warning));

        Assert.Equal("true", composant.Find("button").GetAttribute("aria-pressed"));
        Assert.Equal("Note enregistrée par le parent", composant.Find("textarea").GetAttribute("value"));
        Assert.Equal("À surveiller", composant.Find(".cow-card__urgency").TextContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rendu_SansGestionnaireOuDesactiveNePrometPasDeSauvegarde(bool disabled)
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.Disabled, disabled));

        Assert.True(composant.Find("button").HasAttribute("disabled"));
        Assert.True(composant.Find("textarea").HasAttribute("readonly"));
        composant.Find("button").Click();
        composant.Find("textarea").Input("Modification interdite");
    }

    [Fact]
    public void Rendu_DesactiveLesInteractionsMemeAvecDesGestionnaires()
    {
        using var contexte = new BunitContext();
        var composant = contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.Disabled, true)
            .Add(c => c.IsCompletedChanged, _ => Assert.Fail("Interaction désactivée"))
            .Add(c => c.NoteChanged, _ => Assert.Fail("Interaction désactivée")));

        Assert.True(composant.Find("button").HasAttribute("disabled"));
        Assert.True(composant.Find("textarea").HasAttribute("readonly"));
    }

    [Theory]
    [InlineData("", "Examen")]
    [InlineData("1042", " ")]
    public void Rendu_RefuseLesParametresObligatoiresVides(string identifiant, string titre)
    {
        using var contexte = new BunitContext();
        Assert.Throws<ArgumentException>(() => contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, identifiant)
            .Add(c => c.ActionTitle, titre)));
    }

    [Fact]
    public void Rendu_RefuseLesValeursEnumInconnues()
    {
        using var contexte = new BunitContext();
        Assert.Throws<ArgumentOutOfRangeException>(() => contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.ActionType, (ActionType)999)));
        Assert.Throws<ArgumentOutOfRangeException>(() => contexte.Render<CowCard>(p => p
            .Add(c => c.Identifier, "1042")
            .Add(c => c.ActionTitle, "Examen")
            .Add(c => c.Urgency, (Urgency)999)));
    }
}
