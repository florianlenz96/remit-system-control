namespace Kommandozentrale.Domain;

public enum ChangeKind
{
    /// <summary>Build or operations change within the system properties (components, configuration).</summary>
    TechnicalChange,

    AdjustShouldProperty,

    AdjustMustProperty,

    ChangePolicy,
}

/// <summary>Situations that always lead to a TPO question, regardless of the autonomy level.</summary>
public enum AlwaysAskTrigger
{
    MustPropertyConflict,
    DataLocationChange,
    BudgetExceeded,
    OutageLongerThanRecoveryTime,
}

public enum EscalationOutcome
{
    /// <summary>Level 1: the AI may only propose; it implements nothing.</summary>
    ProposalOnly,

    /// <summary>Level 2: the AI implements after the TPO approves the step.</summary>
    ApprovalPerStep,

    /// <summary>Levels 3–4: the AI decides and implements, and records the decision.</summary>
    Autonomous,

    /// <summary>The AI must not decide; it raises a TPO question with options, deadline and default.</summary>
    TpoQuestion,
}

/// <summary>A change an agent intends to make.</summary>
/// <param name="AffectedPolicies">Policies of all areas the change touches.</param>
public sealed record IntendedChange(
    ChangeKind Kind,
    IReadOnlyCollection<Policy> AffectedPolicies,
    IReadOnlyCollection<AlwaysAskTrigger> Triggers)
{
    public static IntendedChange Technical(IReadOnlyCollection<Policy> policies, params AlwaysAskTrigger[] triggers) =>
        new(ChangeKind.TechnicalChange, policies, triggers);
}

public sealed record EscalationResult(EscalationOutcome Outcome, AutonomyLevel EffectiveAutonomy, string Reason);

/// <summary>
/// Decides what the AI may do on its own. Autonomy follows risk, per area (principle 4); goals and
/// limits stay with the TPO (principle 2); colliding "must" properties are never resolved by the
/// AI (principle 8).
/// </summary>
public static class EscalationRules
{
    public static EscalationResult Evaluate(IntendedChange change)
    {
        if (change.AffectedPolicies.Count == 0)
        {
            throw new DomainException("A change must affect at least one area.");
        }

        var autonomy = Policy.Strictest(change.AffectedPolicies).Autonomy;

        if (change.Triggers.Count > 0)
        {
            var triggers = string.Join(", ", change.Triggers.Distinct());
            return new(EscalationOutcome.TpoQuestion, autonomy, $"Always requires the TPO: {triggers}.");
        }

        return Guard.Defined(change.Kind, nameof(change.Kind)) switch
        {
            ChangeKind.ChangePolicy =>
                new(EscalationOutcome.TpoQuestion, autonomy, "Only the TPO may change a policy."),
            ChangeKind.AdjustMustProperty =>
                new(EscalationOutcome.TpoQuestion, autonomy, "Only the TPO may change a \"must\" property."),
            ChangeKind.AdjustShouldProperty when autonomy == AutonomyLevel.AdjustShouldProperties =>
                new(EscalationOutcome.Autonomous, autonomy, "Autonomy level 4 allows adjusting \"should\" properties; the AI reports the change."),
            ChangeKind.AdjustShouldProperty =>
                new(EscalationOutcome.TpoQuestion, autonomy, "Adjusting a \"should\" property requires autonomy level 4."),
            _ => autonomy switch
            {
                AutonomyLevel.ProposeOnly =>
                    new(EscalationOutcome.ProposalOnly, autonomy, "Autonomy level 1: the AI only proposes."),
                AutonomyLevel.ApproveEachStep =>
                    new(EscalationOutcome.ApprovalPerStep, autonomy, "Autonomy level 2: every step needs the TPO's approval."),
                _ =>
                    new(EscalationOutcome.Autonomous, autonomy, "Within the system properties the AI decides on its own."),
            },
        };
    }
}
