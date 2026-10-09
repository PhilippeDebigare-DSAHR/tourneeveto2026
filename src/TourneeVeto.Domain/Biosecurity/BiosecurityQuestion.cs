namespace TourneeVeto.Domain.Biosecurity;

/// <summary>Élément de la liste de contrôle du bilan de biosécurité.</summary>
/// <param name="Poids">Pondération de la question dans la note du bilan.</param>
/// <param name="IsCritique">Une réponse défavorable doit être signalée en priorité dans le rapport.</param>
public sealed record BiosecurityQuestion(
    Guid Id,
    string Section,
    int Poids,
    bool IsCritique);

public enum Answer
{
    Yes,
    Partially,
    No,
    None
}
