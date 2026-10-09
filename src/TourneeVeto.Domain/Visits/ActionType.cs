namespace TourneeVeto.Domain.Visits;

public enum ActionType
{
    PregnancyDiagnosis,
    HighSomaticCellCount,
    DryOff,
    Calving,
    Insemination
}

public enum Urgency
{
    Routine,
    Warning,
    Urgent
}
