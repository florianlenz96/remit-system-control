using Remit.Domain;

namespace Remit.App.Application;

/// <summary>
/// Use cases of the operations agent (ADR 0009) and the parts of setup that belong to operations:
/// taking over existing building blocks and setting System properties for the area directly.
/// </summary>
public sealed partial class RemitService
{
    public static readonly Actor Setup = Actor.Person("Setup");

    private static readonly TimeSpan MaxQuestionDeadline = TimeSpan.FromDays(30);

    // ---- Setup and TPO ------------------------------------------------------------------------

    /// <summary>Takes over building blocks that already run. Idempotent per resource.</summary>
    public void AdoptExisting(IEnumerable<ExistingComponent> existing)
    {
        lock (_lock)
        {
            var known = LoadAll("component-", Component.FromHistory).SelectMany(x => x.Aggregate.ResourceRefs)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in existing.Where(e => !string.IsNullOrWhiteSpace(e.ResourceId) && !known.Contains(e.ResourceId)))
            {
                var componentId = new ComponentId($"C-{_store.LoadAll("component-").Count + 1:000}");
                var decision = Decision.RecordTpoDecision(
                    NextDecisionId(),
                    DecisionReference.To(componentId),
                    $"Existing building block taken over: {item.Name}",
                    "It existed before Remit and is operated from now on within the system properties of the area.",
                    new DecisionEffects([componentId], []),
                    Setup,
                    Now);
                var component = Component.Adopt(componentId, CatalogArea, item.Name, item.Kind, [item.ResourceId], decision.Id, Setup, Now);
                Save($"decision-{decision.Id}", decision);
                Save($"component-{component.Id}", component);
                known.Add(item.ResourceId);
            }
        }
    }

    /// <summary>The TPO sets a property for the whole area, without a need (e.g. for something that already runs).</summary>
    public string SetAreaProperty(PropertyInput input)
    {
        lock (_lock)
        {
            var area = LoadArea();
            var definition = PropertyTemplates.ToDefinition(input, PropertyScope.ForArea(area.Id));
            var decisionId = NextDecisionId();
            var property = SystemProperty.Define(
                new SystemPropertyId($"SP-{_store.LoadAll("property-").Count + 1:000}"), definition, decisionId, Tpo, Now);

            var existing = ActiveProperties();
            var contradiction = PropertyContradictionDetector.Find([.. existing, property])
                .FirstOrDefault(c => c.First == property.Id || c.Second == property.Id);
            if (contradiction is not null)
            {
                throw new DomainException($"These properties cannot both apply: {DescribeContradiction(contradiction, [.. existing, property])}");
            }

            var label = PropertyTemplates.For(definition.Type).Label;
            var decision = Decision.RecordTpoDecision(
                decisionId,
                DecisionReference.To(area.Id),
                $"{label}: {PropertyTemplates.Describe(definition.Rule)} ({definition.Bindingness.ToString().ToLowerInvariant()})",
                $"Set for \"{area.Name}\".",
                new DecisionEffects([], [property.Id]),
                Tpo,
                Now);
            Save($"decision-{decision.Id}", decision);
            Save($"property-{property.Id}", property);
            return property.Id.Value;
        }
    }

    /// <summary>Alert rules for all active properties (ADR 0010). For setup, never shown to the TPO.</summary>
    public AlertPlan GetAlertPlan()
    {
        lock (_lock)
        {
            return AlertRules.Derive(ActiveProperties());
        }
    }

    // ---- Operations agent ---------------------------------------------------------------------

    public RulesView GetRules(string? resourceId)
    {
        lock (_lock)
        {
            ApplyDueDefaults();
            var area = LoadArea();
            _ = FindComponent(resourceId);
            var policy = area.Policy;

            var properties = ActiveProperties().Select(p =>
            {
                var t = p.Definition.Rule as ThresholdRule;
                return new AgentPropertyView(
                    p.Id.Value,
                    p.Definition.Type,
                    PropertyTemplates.For(p.Definition.Type).Label,
                    PropertyTemplates.Describe(p.Definition.Rule),
                    p.Definition.Bindingness.ToString().ToLowerInvariant(),
                    p.Definition.Measurement.Source,
                    t?.Metric,
                    t?.Operator.ToString(),
                    t?.Value,
                    t?.Unit);
            }).ToList();

            var components = LoadAll("component-", Component.FromHistory).Select(x => x.Aggregate)
                .Where(c => c.Status == ComponentStatus.Active)
                .Select(c => new AgentComponentView(c.Id.Value, c.Name, c.Kind, c.IsAdopted, c.ResourceRefs))
                .ToList();

            var deviations = LoadAll("deviation-", Deviation.FromHistory).Select(x => x.Aggregate)
                .Where(d => !d.IsResolved)
                .Select(d => new AgentDeviationView(d.Id.Value, d.PropertyId.Value, d.Kind, d.Description, d.DetectedAt, d.Measure?.Value))
                .ToList();

            var questions = LoadAll("decision-", Decision.FromHistory).Select(x => x.Aggregate)
                .Where(d => d.QuestionStatus == QuestionStatus.Open)
                .Select(d => new AgentQuestionView(d.Id.Value, d.Question!, d.DefaultOptionKey, d.Deadline))
                .ToList();

            return new RulesView(
                area.Id.Value,
                area.Name,
                new AgentPolicyView(
                    (int)policy.Autonomy,
                    AutonomyMeaning(policy.Autonomy),
                    (int)policy.Transparency,
                    policy.Autonomy >= AutonomyLevel.DecideWithinProperties ? "Autonomous" : "Review"),
                properties,
                components,
                deviations,
                questions,
                "Remit is the source of truth for targets, limits and who decides. Before every change call check_change " +
                "and follow its outcome. Report a violated or endangered property with report_deviation. After acting, " +
                "report what you did with report_decision. When the outcome is ask_tpo or ask_approval, use ask_tpo and " +
                "wait for the answer (get_question) before acting. Never resolve two conflicting \"must\" properties yourself.");
        }
    }

    public ChangeCheckView CheckChange(ChangeDescription change)
    {
        lock (_lock)
        {
            var (escalation, projected, blocking) = Evaluate(change);
            var outcome = OutcomeName(escalation.Outcome);
            var next = escalation.Outcome switch
            {
                EscalationOutcome.Autonomous => "Make the change, then call report_decision with your rationale.",
                EscalationOutcome.ApprovalPerStep => "Call ask_tpo with an option to approve this step and one to decline it; act only after the TPO approved it.",
                EscalationOutcome.ProposalOnly => "Do not change anything. Describe your proposal with ask_tpo so the TPO can decide.",
                _ => "Do not act. Call ask_tpo with options, a default option and a deadline, then wait for the answer.",
            };
            return new ChangeCheckView(outcome, (int)escalation.EffectiveAutonomy, escalation.Reason, projected, blocking, next);
        }
    }

    public ReportedView ReportDeviation(string propertyId, DeviationKind kind, string description, Actor agent)
    {
        lock (_lock)
        {
            var property = ActiveProperties().FirstOrDefault(p => p.Id.Value == propertyId)
                ?? throw new DomainException($"There is no active property {propertyId}. Call get_rules for the current properties.");
            var deviation = Deviation.Detect(
                new DeviationId($"DV-{_store.LoadAll("deviation-").Count + 1:000}"), property.Id, kind, description, agent, Now);
            Save($"deviation-{deviation.Id}", deviation);
            return new ReportedView(deviation.Id.Value, "Deviation recorded. Check your intended measure with check_change.");
        }
    }

    public ReportedView ResolveDeviation(string deviationId, string note, Actor agent)
    {
        lock (_lock)
        {
            var deviation = LoadDeviation(deviationId);
            deviation.Resolve(note, agent, Now);
            Save($"deviation-{deviation.Id}", deviation);
            return new ReportedView(deviation.Id.Value, "Deviation resolved.");
        }
    }

    /// <summary>
    /// Records what the agent did. Allowed when the escalation rules let it act on its own, or when the TPO
    /// answered a question about this change (approval at level 2, or a TPO decision).
    /// </summary>
    public ReportedView ReportDecision(
        ChangeDescription change,
        string chosen,
        string rationale,
        string? deviationId,
        string? questionId,
        string? howToUndo,
        IReadOnlyList<string>? references,
        Actor agent)
    {
        lock (_lock)
        {
            ApplyDueDefaults();
            var component = FindComponent(change.ResourceId);
            var deviation = deviationId is null ? null : LoadDeviation(deviationId);
            var (escalation, _, _) = Evaluate(change);

            if (escalation.Outcome == EscalationOutcome.ProposalOnly)
            {
                throw new DomainException("Autonomy level 1: the AI only proposes and must not change anything.");
            }

            var level = escalation.EffectiveAutonomy;
            if (escalation.Outcome != EscalationOutcome.Autonomous)
            {
                if (questionId is null)
                {
                    throw new DomainException(
                        $"This change needs the TPO ({OutcomeName(escalation.Outcome)}: {escalation.Reason}) Ask with ask_tpo and pass its id as questionId once answered.");
                }

                var question = Load($"decision-{questionId}", Decision.FromHistory)
                    ?? throw new DomainException($"There is no question {questionId}.");
                if (question.Kind != DecisionKind.TpoQuestion || question.QuestionStatus == QuestionStatus.Open)
                {
                    throw new DomainException($"Question {questionId} has not been answered yet.");
                }
            }

            var decision = Decision.RecordAiDecision(
                NextDecisionId(),
                deviation is not null ? DecisionReference.To(deviation.Id)
                    : component is not null ? DecisionReference.To(component.Id)
                    : DecisionReference.To(CatalogArea),
                chosen,
                questionId is null ? rationale : $"{rationale} Based on the TPO's answer to {questionId}.",
                new DecisionEffects(component is null ? [] : [component.Id], []),
                level,
                agent,
                string.IsNullOrWhiteSpace(howToUndo) ? Reversibility.Irreversible : Reversibility.By(howToUndo),
                references ?? [],
                Now);
            Save($"decision-{decision.Id}", decision);

            if (deviation is not null && deviation.Measure is null)
            {
                deviation.AssignMeasure(decision.Id, Now);
                Save($"deviation-{deviation.Id}", deviation);
            }

            return new ReportedView(decision.Id.Value, "Decision recorded.");
        }
    }

    public ReportedView AskTpoFromAgent(
        string? resourceId,
        string question,
        IReadOnlyList<QuestionOptionInput> options,
        string defaultOption,
        int deadlineHours,
        string? deviationId,
        Actor agent)
    {
        lock (_lock)
        {
            var deadline = TimeSpan.FromHours(deadlineHours);
            if (deadline <= TimeSpan.Zero || deadline > MaxQuestionDeadline)
            {
                throw new DomainException("The deadline must be between 1 hour and 30 days.");
            }

            var component = FindComponent(resourceId);
            var deviation = deviationId is null ? null : LoadDeviation(deviationId);
            var decision = Decision.AskTpo(
                NextDecisionId(),
                deviation is not null ? DecisionReference.To(deviation.Id)
                    : component is not null ? DecisionReference.To(component.Id)
                    : DecisionReference.To(CatalogArea),
                question,
                options.Select(o => new QuestionOption(o.Key, o.Title, o.Description ?? "")),
                defaultOption,
                Now + deadline,
                LoadArea().Policy.Autonomy,
                agent,
                Now);
            Save($"decision-{decision.Id}", decision);

            if (deviation is not null && deviation.Measure is null)
            {
                deviation.AssignMeasure(decision.Id, Now);
                Save($"deviation-{deviation.Id}", deviation);
            }

            return new ReportedView(decision.Id.Value, "Question raised. Poll get_question until it is answered or the default applies.");
        }
    }

    public QuestionStatusView GetQuestion(string questionId)
    {
        lock (_lock)
        {
            ApplyDueDefaults();
            var question = Load($"decision-{questionId}", Decision.FromHistory);
            if (question is null || question.Kind != DecisionKind.TpoQuestion)
            {
                throw new DomainException($"There is no question {questionId}.");
            }

            return new QuestionStatusView(
                question.Id.Value,
                question.QuestionStatus switch
                {
                    QuestionStatus.Open => "open",
                    QuestionStatus.Answered => "answered",
                    _ => "default_applied",
                },
                question.Question!,
                question.Chosen,
                question.Options.FirstOrDefault(o => o.Key == question.Chosen)?.Title,
                question.Chosen is null ? null : question.Rationale,
                question.DefaultOptionKey,
                question.Deadline);
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private (EscalationResult Escalation, decimal? Projected, IReadOnlyList<BlockingProperty> Blocking) Evaluate(ChangeDescription change)
    {
        if (!Enum.TryParse<ChangeKind>(change.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            kind = change.Kind?.ToLowerInvariant() switch
            {
                "technical" or "technical_change" => ChangeKind.TechnicalChange,
                "adjust_should_property" => ChangeKind.AdjustShouldProperty,
                "adjust_must_property" => ChangeKind.AdjustMustProperty,
                "change_policy" => ChangeKind.ChangePolicy,
                _ => throw new DomainException(
                    "kind must be one of: technical_change, adjust_should_property, adjust_must_property, change_policy."),
            };
        }

        _ = FindComponent(change.ResourceId);
        var area = LoadArea();
        var properties = ActiveProperties();
        var effects = (change.ExpectedEffects ?? [])
            .Select(e => new ExpectedEffect(e.Metric, e.Value, e.Unit))
            .ToList();
        var unmet = ChangeFeasibility.UnmetTargets(effects, [area.Id], properties, Now);

        decimal? projected = null;
        IReadOnlyList<SystemProperty> exceeded = [];
        // Only a change that costs more (or a known current cost) is checked against the budgets.
        if ((change.ExtraMonthlyCost is { } extra && extra > 0) || change.CurrentMonthlyCost is not null)
        {
            projected = Math.Max(0, (change.CurrentMonthlyCost ?? RunningMonthlyCost()) + (change.ExtraMonthlyCost ?? 0));
            exceeded = ChangeFeasibility.ExceededBudgets(projected.Value, [area.Id], properties, Now);
        }

        var escalation = EscalationRules.Evaluate(
            ChangeFeasibility.ToIntendedChange(kind, [area.Policy], unmet, exceeded, change.ChangesDataLocation));

        var blocking = unmet
            .Select(u => new BlockingProperty(
                u.Property.Id.Value,
                PropertyTemplates.For(u.Property.Definition.Type).Label,
                PropertyTemplates.Describe(u.Property.Definition.Rule),
                u.Property.Definition.Bindingness.ToString().ToLowerInvariant(),
                $"Expected {DescribeEffect(u.Effect).Text}."))
            .Concat(exceeded.Select(b => new BlockingProperty(
                b.Id.Value,
                PropertyTemplates.For(b.Definition.Type).Label,
                PropertyTemplates.Describe(b.Definition.Rule),
                b.Definition.Bindingness.ToString().ToLowerInvariant(),
                $"Projected monthly cost {Money(projected!.Value)}.")))
            .ToList();

        return (escalation, projected, blocking);
    }

    private decimal RunningMonthlyCost() =>
        LoadAll("need-", Need.FromHistory)
            .Select(x => x.Aggregate)
            .Where(n => n.Status is NeedStatus.InImplementation or NeedStatus.InOperation)
            .Select(n => ProposalOf(n.Id))
            .Where(p => p?.AcceptedOption is not null)
            .Sum(p => p!.GetOption(p.AcceptedOption!.Value).Cost.MaxPerMonth);

    /// <summary>The component a resource belongs to (also for sub-resources), or null when no resource is given.</summary>
    private Component? FindComponent(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        var id = resourceId.Trim().TrimEnd('/');
        return LoadAll("component-", Component.FromHistory)
                   .Select(x => x.Aggregate)
                   .FirstOrDefault(c => c.Status == ComponentStatus.Active && c.ResourceRefs.Any(r =>
                       string.Equals(r, id, StringComparison.OrdinalIgnoreCase) ||
                       id.StartsWith(r.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
               ?? throw new DomainException(
                   $"Remit does not know the resource {resourceId}. Call get_rules without a resource to see the known components.");
    }

    private Deviation LoadDeviation(string id) =>
        Load($"deviation-{id}", Deviation.FromHistory) ?? throw new DomainException($"There is no deviation {id}.");

    private static string OutcomeName(EscalationOutcome outcome) => outcome switch
    {
        EscalationOutcome.Autonomous => "act",
        EscalationOutcome.ApprovalPerStep => "ask_approval",
        EscalationOutcome.ProposalOnly => "propose_only",
        _ => "ask_tpo",
    };

    private static string AutonomyMeaning(AutonomyLevel level) => level switch
    {
        AutonomyLevel.ProposeOnly => "Only propose. Do not change anything.",
        AutonomyLevel.ApproveEachStep => "Every change needs the TPO's approval first (ask_tpo).",
        AutonomyLevel.DecideWithinProperties => "Decide on your own within the system properties; report every decision.",
        _ => "Like 3; you may also adjust \"should\" properties and must report it.",
    };
}
