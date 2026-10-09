using TourneeVeto.Domain.Biosecurity;

namespace TourneeVeto.Domain.Visits;

/// <summary>Pratique de biosécurité défavorable (« Partiellement » ou « Non ») retenue comme priorité.</summary>
public sealed record PriorityPractice(
    Guid QuestionId,
    string Section,
    string Prompt,
    Answer Answer,
    string AnswerLabel,
    bool IsCritique);

/// <summary>
/// Score indicatif d'une section. <see cref="ScorePercent"/> ne porte que sur les questions répondues
/// et vaut null si aucune ne l'est ; la section n'est complète que si toutes ses questions sont répondues.
/// </summary>
public sealed record BiosecuritySectionSummary(
    string Section,
    int QuestionCount,
    int AnsweredCount,
    decimal? ScorePercent)
{
    public bool IsComplete => QuestionCount > 0 && AnsweredCount == QuestionCount;
}

/// <summary>
/// Synthèse pure du rapport de visite. Score indicatif pondéré (Oui = 100 %, Partiellement = 50 %, Non = 0 %),
/// pondéré par <see cref="BiosecurityQuestion.Poids"/>. Une réponse « Aucune » (<see cref="Answer.None"/>) est
/// considérée comme non renseignée. <see cref="Answer"/> exprime un niveau de conformité, pas forcément la réponse
/// littérale oui/non (ex. « troupeau fermé » = Oui). Sans conseil clinique : les pratiques prioritaires sont uniquement
/// les réponses défavorables enregistrées.
/// </summary>
public sealed record VisitReportSummary
{
    public const int MaxPriorityPractices = 3;

    public Guid VisitId { get; init; }
    public string Cause { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public Guid? PhotoId { get; init; }
    public IReadOnlyList<VisitAction> CompletedActions { get; init; } = [];
    public IReadOnlyList<VisitAction> PendingActions { get; init; } = [];
    public IReadOnlyList<BiosecuritySectionSummary> Sections { get; init; } = [];
    public int QuestionCount { get; init; }
    public int AnsweredCount { get; init; }

    /// <summary>Score global indicatif sur les questions répondues ; null si aucune réponse.</summary>
    public decimal? ScorePercent { get; init; }

    public IReadOnlyList<PriorityPractice> PriorityPractices { get; init; } = [];

    /// <summary>Vrai uniquement si toutes les questions de toutes les sections sont répondues.</summary>
    public bool IsBiosecurityComplete => QuestionCount > 0 && AnsweredCount == QuestionCount;

    /// <exception cref="ArgumentNullException">Visite ou référentiel absent.</exception>
    /// <exception cref="ArgumentException">Référentiel ou réponses invalides (doublon, question/option inconnue, poids ≤ 0).</exception>
    public static VisitReportSummary Create(Visit visit, IReadOnlyList<BiosecurityQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(questions);

        ValidateQuestions(questions);
        var byQuestion = IndexResponses(visit.BiosecurityResponses, questions);

        var answered = questions
            .Where(q => byQuestion.TryGetValue(q.Id, out var option) && option.Value != Answer.None)
            .Select(q => (Question: q, Option: byQuestion[q.Id]))
            .ToList();

        var sections = questions
            .Select((q, index) => (q, index))
            .GroupBy(x => x.q.Section)
            .OrderBy(g => g.Min(x => x.index))
            .Select(g =>
            {
                var inSection = answered.Where(a => a.Question.Section == g.Key).ToList();
                return new BiosecuritySectionSummary(g.Key, g.Count(), inSection.Count, Score(inSection));
            })
            .ToArray();

        var priorities = answered
            .Select((a, index) => (a, index))
            .Where(x => x.a.Option.Value is Answer.No or Answer.Partially)
            .OrderByDescending(x => x.a.Question.IsCritique)
            .ThenBy(x => x.a.Option.Value == Answer.No ? 0 : 1)
            .ThenByDescending(x => x.a.Question.Poids)
            .ThenBy(x => x.index)
            .Take(MaxPriorityPractices)
            .Select(x => new PriorityPractice(
                x.a.Question.Id, x.a.Question.Section, x.a.Question.Prompt,
                x.a.Option.Value, x.a.Option.Label, x.a.Question.IsCritique))
            .ToArray();

        return new VisitReportSummary
        {
            VisitId = visit.Id,
            Cause = visit.Cause ?? string.Empty,
            Notes = visit.Notes ?? string.Empty,
            PhotoId = visit.PhotoId,
            CompletedActions = visit.Actions.Where(a => a.IsCompleted).OrderBy(a => a.Date).ToArray(),
            PendingActions = visit.Actions.Where(a => !a.IsCompleted).OrderBy(a => a.Date).ToArray(),
            Sections = sections,
            QuestionCount = questions.Count,
            AnsweredCount = answered.Count,
            ScorePercent = Score(answered),
            PriorityPractices = priorities
        };
    }

    private static decimal? Score(IReadOnlyCollection<(BiosecurityQuestion Question, BiosecurityOption Option)> answered)
    {
        var totalWeight = answered.Sum(a => a.Question.Poids);
        if (totalWeight == 0)
        {
            return null;
        }

        var points = answered.Sum(a => a.Question.Poids * Factor(a.Option.Value));
        return Math.Round(points * 100m / totalWeight, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal Factor(Answer answer) => answer switch
    {
        Answer.Yes => 1m,
        Answer.Partially => 0.5m,
        _ => 0m
    };

    private static void ValidateQuestions(IReadOnlyList<BiosecurityQuestion> questions)
    {
        var ids = new HashSet<Guid>();
        foreach (var question in questions)
        {
            if (question is null)
            {
                throw new ArgumentException("Le référentiel ne peut pas contenir de question null.", nameof(questions));
            }

            if (question.Poids <= 0)
            {
                throw new ArgumentException("Le poids d'une question doit être strictement positif.", nameof(questions));
            }

            if (string.IsNullOrWhiteSpace(question.Section))
            {
                throw new ArgumentException("Chaque question doit appartenir à une section.", nameof(questions));
            }

            if (!ids.Add(question.Id))
            {
                throw new ArgumentException("Le référentiel contient une question en double.", nameof(questions));
            }

            if (question.Options is null)
            {
                throw new ArgumentException("Une question doit avoir une liste d'options.", nameof(questions));
            }

            ValidateOptions(question.Options);
        }
    }

    private static void ValidateOptions(IEnumerable<BiosecurityOption> options)
    {
        var optionIds = new HashSet<Guid>();
        foreach (var option in options)
        {
            if (option is null)
            {
                throw new ArgumentException("Une question ne peut pas contenir d'option null.", "questions");
            }

            if (!Enum.IsDefined(option.Value))
            {
                throw new ArgumentException("Une option contient une valeur de réponse inconnue.", "questions");
            }

            if (!optionIds.Add(option.Id))
            {
                throw new ArgumentException("Une question contient un identifiant d'option en double.", "questions");
            }
        }
    }

    private static Dictionary<Guid, BiosecurityOption> IndexResponses(
        IReadOnlyList<BiosecurityResponse> responses,
        IReadOnlyList<BiosecurityQuestion> questions)
    {
        var questionsById = questions.ToDictionary(q => q.Id);
        var result = new Dictionary<Guid, BiosecurityOption>();
        foreach (var response in responses)
        {
            if (!questionsById.TryGetValue(response.QuestionId, out var question))
            {
                throw new ArgumentException("Une réponse référence une question inconnue.", nameof(responses));
            }

            var option = question.Options.FirstOrDefault(o => o.Id == response.OptionId)
                ?? throw new ArgumentException("Une réponse référence une option inconnue pour sa question.", nameof(responses));

            if (!result.TryAdd(question.Id, option))
            {
                throw new ArgumentException("Une question a plusieurs réponses enregistrées.", nameof(responses));
            }
        }

        return result;
    }
}
