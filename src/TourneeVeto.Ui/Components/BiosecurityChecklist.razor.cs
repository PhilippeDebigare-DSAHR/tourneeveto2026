using Microsoft.AspNetCore.Components;
using TourneeVeto.Domain.Biosecurity;

namespace TourneeVeto.Ui.Components;

public partial class BiosecurityChecklist
{
    private readonly string _id = $"biosecurity-checklist-{Guid.NewGuid():N}";

    private string TitleId => $"{_id}-title";

    private bool IsUnavailable => Questions is not { Count: > 0 };

    [Parameter]
    public IReadOnlyList<BiosecurityQuestion>? Questions { get; set; }

    [Parameter]
    public IReadOnlyList<BiosecurityResponse> Responses { get; set; } = [];

    [Parameter]
    public EventCallback<BiosecurityResponse> ResponseChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (Questions is null || Questions.Count == 0)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(Responses);
        var questionIds = new HashSet<Guid>();
        var optionIds = new HashSet<Guid>();
        foreach (var question in Questions)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentException.ThrowIfNullOrWhiteSpace(question.Section);
            ArgumentException.ThrowIfNullOrWhiteSpace(question.Prompt);
            ArgumentNullException.ThrowIfNull(question.Options);
            if (question.Id == Guid.Empty || !questionIds.Add(question.Id))
            {
                throw new ArgumentException("Les éléments doivent avoir des identifiants uniques.", nameof(Questions));
            }

            if (question.Options.Count == 0)
            {
                throw new ArgumentException("Chaque élément doit proposer au moins une réponse.", nameof(Questions));
            }

            foreach (var option in question.Options)
            {
                ArgumentNullException.ThrowIfNull(option);
                ArgumentException.ThrowIfNullOrWhiteSpace(option.Label);
                if (option.Id == Guid.Empty || !optionIds.Add(option.Id) || !Enum.IsDefined(option.Value))
                {
                    throw new ArgumentException("Les réponses doivent être valides et identifiées de façon unique.",
                        nameof(Questions));
                }
            }
        }

        var optionsByQuestion = Questions.ToDictionary(
            question => question.Id,
            question => question.Options.Select(option => option.Id).ToHashSet());
        var answeredQuestionIds = new HashSet<Guid>();
        foreach (var response in Responses)
        {
            if (!optionsByQuestion.TryGetValue(response.QuestionId, out var options)
                || !options.Contains(response.OptionId)
                || !answeredQuestionIds.Add(response.QuestionId))
            {
                throw new ArgumentException(
                    "Chaque réponse doit désigner un choix disponible et unique pour sa question.",
                    nameof(Responses));
            }
        }
    }

    private BiosecurityOption? SelectedOption(BiosecurityQuestion question)
    {
        var response = Responses.FirstOrDefault(candidate => candidate.QuestionId == question.Id);
        return response is null
            ? null
            : question.Options.First(option => option.Id == response.OptionId);
    }

    private bool IsSelected(BiosecurityQuestion question, BiosecurityOption option) =>
        SelectedOption(question)?.Id == option.Id;

    private string GroupName(BiosecurityQuestion question) => $"{_id}-{question.Id:N}";

    private string InputId(BiosecurityQuestion question, BiosecurityOption option) =>
        $"{_id}-{question.Id:N}-{option.Id:N}";

    private Task SelectAsync(BiosecurityQuestion question, BiosecurityOption option) =>
        ResponseChanged.HasDelegate
            ? ResponseChanged.InvokeAsync(new BiosecurityResponse(question.Id, option.Id))
            : Task.CompletedTask;
}
