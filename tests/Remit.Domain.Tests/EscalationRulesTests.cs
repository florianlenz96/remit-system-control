using static Remit.Domain.Tests.TestData;

namespace Remit.Domain.Tests;

public class EscalationRulesTests
{
    [Theory]
    [InlineData(1, EscalationOutcome.ProposalOnly)]
    [InlineData(2, EscalationOutcome.ApprovalPerStep)]
    [InlineData(3, EscalationOutcome.Autonomous)]
    [InlineData(4, EscalationOutcome.Autonomous)]
    public void Technical_change_follows_the_autonomy_level(int level, EscalationOutcome expected)
    {
        var result = EscalationRules.Evaluate(IntendedChange.Technical([PolicyAt(level)]));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal((AutonomyLevel)level, result.EffectiveAutonomy);
    }

    [Fact]
    public void Change_across_areas_uses_the_strictest_policy()
    {
        var result = EscalationRules.Evaluate(IntendedChange.Technical([PolicyAt(3), PolicyAt(1, 3)]));

        Assert.Equal(EscalationOutcome.ProposalOnly, result.Outcome);
        Assert.Equal(AutonomyLevel.ProposeOnly, result.EffectiveAutonomy);
    }

    [Theory]
    [InlineData(AlwaysAskTrigger.MustPropertyConflict)]
    [InlineData(AlwaysAskTrigger.DataLocationChange)]
    [InlineData(AlwaysAskTrigger.BudgetExceeded)]
    [InlineData(AlwaysAskTrigger.OutageLongerThanRecoveryTime)]
    public void Always_ask_triggers_require_the_tpo_even_at_level_4(AlwaysAskTrigger trigger)
    {
        var result = EscalationRules.Evaluate(IntendedChange.Technical([PolicyAt(4)], trigger));

        Assert.Equal(EscalationOutcome.TpoQuestion, result.Outcome);
        Assert.Contains(trigger.ToString(), result.Reason);
    }

    [Theory]
    [InlineData(ChangeKind.AdjustMustProperty)]
    [InlineData(ChangeKind.ChangePolicy)]
    public void Goals_limits_and_policies_are_never_changed_by_the_ai(ChangeKind kind)
    {
        var result = EscalationRules.Evaluate(new IntendedChange(kind, [PolicyAt(4)], []));

        Assert.Equal(EscalationOutcome.TpoQuestion, result.Outcome);
    }

    [Theory]
    [InlineData(1, EscalationOutcome.TpoQuestion)]
    [InlineData(2, EscalationOutcome.TpoQuestion)]
    [InlineData(3, EscalationOutcome.TpoQuestion)]
    [InlineData(4, EscalationOutcome.Autonomous)]
    public void Should_properties_may_only_be_adjusted_at_level_4(int level, EscalationOutcome expected)
    {
        var result = EscalationRules.Evaluate(new IntendedChange(ChangeKind.AdjustShouldProperty, [PolicyAt(level)], []));

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public void A_change_without_affected_areas_is_rejected()
    {
        Assert.Throws<DomainException>(() => EscalationRules.Evaluate(IntendedChange.Technical([])));
    }
}
