namespace TourneeVeto.Domain.Biosecurity;

public static class BiosecurityChecklistReference
{
    public static IReadOnlyList<BiosecurityQuestion> Questions { get; } =
        Array.AsReadOnly(new[]
        {
            CreateQuestion(
                Guid.Parse("07000000-0000-0000-0000-000000000001"),
                "Visiteurs et intervenants extérieurs",
                "Présence d’un registre de visiteurs et mise à disposition de bottes de ferme désinfectées ?",
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0001-000000000001"),
                    Answer.Yes,
                    "Oui, protocole appliqué"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0001-000000000002"),
                    Answer.Partially,
                    "Partiellement"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0001-000000000003"),
                    Answer.No,
                    "Non, bottes non fournies")),
            CreateQuestion(
                Guid.Parse("07000000-0000-0000-0000-000000000002"),
                "Animaux introduits et origine",
                "Acquisition de bovins extérieurs au cours des 30 derniers jours ?",
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0002-000000000001"),
                    Answer.No,
                    "Aucun bovin introduit (troupeau fermé)"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0002-000000000002"),
                    Answer.Yes,
                    "Oui, avec certificat sanitaire"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0002-000000000003"),
                    Answer.Partially,
                    "Oui, sans contrôle formel")),
            CreateQuestion(
                Guid.Parse("07000000-0000-0000-0000-000000000003"),
                "Quarantaine et isolement",
                "Parc d’isolement dédié disponible et étanche pour les animaux malades ?",
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0003-000000000001"),
                    Answer.Yes,
                    "Oui, box d’infirmerie séparé"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0003-000000000002"),
                    Answer.No,
                    "Non, même étable")),
            CreateQuestion(
                Guid.Parse("07000000-0000-0000-0000-000000000004"),
                "Nettoyage, désinfection et gestion des carcasses",
                "Zone d’équarrissage balisée et inaccessible aux animaux de la ferme et aux carnivores ?",
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0004-000000000001"),
                    Answer.Yes,
                    "Conforme, bac fermé en limite"),
                new BiosecurityOption(
                    Guid.Parse("07000000-0000-0000-0004-000000000002"),
                    Answer.No,
                    "Non conforme"))
        });

    private static BiosecurityQuestion CreateQuestion(
        Guid id,
        string section,
        string prompt,
        params BiosecurityOption[] options) =>
        new(id, section, Poids: 1, IsCritique: false)
        {
            Prompt = prompt,
            Options = Array.AsReadOnly(options)
        };
}
