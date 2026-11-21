using System.Globalization;
using System.Reflection;
using Remit.App.Persistence;
using Remit.Domain;

namespace Remit.App.Application;

/// <summary>
/// Application layer of version 0.0.1: runs the use cases of the TPO against the domain core and
/// lets the simulated agents react. Read models are derived from the event streams on every request
/// (ADR 0004: history is the truth, current state is derived).
/// </summary>
public sealed class RemitService
{
    public static readonly Actor Tpo = Actor.Person("TPO");

    private static readonly SystemId ShopSystem = new("online-shop");
    private static readonly AreaId CatalogArea = new("catalog-and-search");
    private static readonly TimeSpan QuestionDeadline = TimeSpan.FromDays(3);

    private readonly InMemoryEventStore _store;
    private readonly TimeProvider _clock;
    private readonly Lock _lock = new();

    public RemitService(InMemoryEventStore store, TimeProvider clock)
    {
        _store = store;
        _clock = clock;
    }

    public static string Version { get; } =
        typeof(RemitService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";

    private DateTimeOffset Now => _clock.GetUtcNow();

    // ---- Setup --------------------------------------------------------------------------------

    /// <summary>Creates the one area of the example system. Required; the app cannot work without it.</summary>
    public void EnsureArea()
    {
        lock (_lock)
        {
            if (_store.LoadAll("area-").Count > 0)
            {
                return;
            }

            var area = Area.Define(
                CatalogArea,
                ShopSystem,
                "Catalog and search",
                ProtectionNeed.Medium,
                new Policy(AutonomyLevel.DecideWithinProperties, TransparencyLevel.Result),
                Tpo,
                Now);
            Save($"area-{area.Id}", area);
        }
    }

    /// <summary>Adds an example need that waits for the TPO, unless there are needs already.</summary>
    public void SeedExample()
    {
        EnsureArea();
        if (_store.LoadAll("need-").Count > 0)
        {
            return;
        }

        RegisterNeed(
            "Faster response times in the online shop",
            "Customers complain that the catalog and the search react slowly, especially in the evening. " +
            "Pages should load quickly so that customers do not leave.",
            Priority.High);
    }

    // ---- Use cases ----------------------------------------------------------------------------

    public string RegisterNeed(string title, string description, Priority priority)
    {
        lock (_lock)
        {
            var needId = new NeedId($"N-{_store.LoadAll("need-").Count + 1:000}");
            var need = Need.Register(
                needId, ShopSystem, title, description, [CatalogArea], Tpo.Name, priority, NeedSource.Manual, Tpo, Now);

            need.StartAnalysis(SimulatedAgents.Analyst, Now);
            var analysis = SimulatedAgents.Analyse(title, description, CatalogArea);
            var proposal = Proposal.Create(
                new ProposalId($"PR-{_store.LoadAll("proposal-").Count + 1:000}"),
                needId,
                analysis.Analysis,
                analysis.Options,
                analysis.Recommended,
                SimulatedAgents.Analyst,
                Now);
            need.MarkProposalAvailable(SimulatedAgents.Analyst, Now);

            Save($"need-{needId}", need);
            Save($"proposal-{proposal.Id}", proposal);
            return needId.Value;
        }
    }

    /// <summary>
    /// The TPO accepts an option and sets exactly the properties it requires (and may add more).
    /// Contradicting properties are refused before anything is stored.
    /// </summary>
    public void AcceptOption(string needId, string optionId, IReadOnlyList<PropertyInput> inputs)
    {
        lock (_lock)
        {
            var need = LoadNeed(needId);
            var proposal = ProposalOf(need.Id) ?? throw new DomainException($"Need {needId} has no proposal yet.");
            var option = proposal.GetOption(new OptionId(optionId));

            if (inputs.Select(i => i.Type).Distinct().Count() != inputs.Count)
            {
                throw new DomainException("Each kind of property can only be set once per need.");
            }

            var missing = proposal.MissingProperties(option.Id, inputs.Select(i => i.Type));
            if (missing.Count > 0)
            {
                var labels = missing.Select(t => PropertyTemplates.For(t).Label);
                throw new DomainException($"Please also set: {string.Join(", ", labels)}.");
            }

            var scope = PropertyScope.ForArea(CatalogArea);
            var definitions = inputs.Select(i => PropertyTemplates.ToDefinition(i, scope)).ToList();

            var decisionId = NextDecisionId();
            var nextProperty = _store.LoadAll("property-").Count + 1;
            var properties = definitions
                .Select((d, i) => SystemProperty.Define(
                    new SystemPropertyId($"SP-{nextProperty + i:000}"), d, decisionId, Tpo, Now))
                .ToList();

            var existing = LoadAll("property-", SystemProperty.FromHistory).Select(x => x.Aggregate).ToList();
            var contradiction = PropertyContradictionDetector.Find(existing.Concat(properties))
                .FirstOrDefault(c => properties.Any(p => p.Id == c.First || p.Id == c.Second));
            if (contradiction is not null)
            {
                throw new DomainException($"These properties cannot both apply: {DescribeContradiction(contradiction, existing.Concat(properties))}");
            }

            var unmet = ChangeFeasibility.UnmetTargets(option, existing.Concat(properties), Now)
                .FirstOrDefault(u => u.Property.Definition.Bindingness == Bindingness.Must);
            if (unmet is not null)
            {
                var label = PropertyTemplates.For(unmet.Property.Definition.Type).Label;
                throw new DomainException(
                    $"\"{option.Title}\" is expected to reach {label.ToLowerInvariant()} {DescribeEffect(unmet.Effect).Text}, " +
                    $"but \"{label} {PropertyTemplates.Describe(unmet.Property.Definition.Rule)}\" must apply. " +
                    "Choose another option or relax the property.");
            }

            var decision = Decision.RecordTpoDecision(
                decisionId,
                DecisionReference.To(proposal.Id),
                option.Title,
                $"Accepted for need \"{need.Title}\" and set {properties.Count} properties.",
                new DecisionEffects([], properties.Select(p => p.Id).ToList()),
                Tpo,
                Now);
            proposal.Accept(option.Id, inputs.Select(i => i.Type), Tpo, Now);
            need.MarkPropertiesDefined(Tpo, Now);

            Save($"decision-{decision.Id}", decision);
            foreach (var property in properties)
            {
                Save($"property-{property.Id}", property);
            }

            Save($"proposal-{proposal.Id}", proposal);
            Save($"need-{need.Id}", need);

            TryBuild(need, option, approvedByTpo: false);
        }
    }

    public void RejectProposal(string needId, string reason)
    {
        lock (_lock)
        {
            var need = LoadNeed(needId);
            var proposal = ProposalOf(need.Id) ?? throw new DomainException($"Need {needId} has no proposal yet.");
            proposal.Reject(reason, Tpo, Now);
            need.Reject(reason, Tpo, Now);
            var decision = Decision.RecordTpoDecision(
                NextDecisionId(), DecisionReference.To(need.Id), "Need rejected", reason, DecisionEffects.None, Tpo, Now);
            Save($"decision-{decision.Id}", decision);
            Save($"proposal-{proposal.Id}", proposal);
            Save($"need-{need.Id}", need);
        }
    }

    public void AnswerQuestion(string decisionId, string optionKey, string? rationale)
    {
        lock (_lock)
        {
            var question = Load($"decision-{decisionId}", Decision.FromHistory)
                ?? throw new DomainException($"There is no question {decisionId}.");
            var now = Now;
            if (question.ApplyDefaultIfDue(now))
            {
                Save($"decision-{question.Id}", question);
                ApplyAnswer(question);
                var applied = question.Options.First(o => o.Key == question.Chosen).Title;
                throw new DomainException($"The deadline has passed; the default option \"{applied}\" was applied.");
            }

            question.Answer(optionKey, rationale ?? "", Tpo, now);
            Save($"decision-{question.Id}", question);
            ApplyAnswer(question);
        }
    }

    public void ChangePolicy(AutonomyLevel autonomy, TransparencyLevel transparency, string reason)
    {
        lock (_lock)
        {
            var area = LoadArea();
            var before = area.Policy;
            var after = new Policy(autonomy, transparency);
            if (after == before)
            {
                return;
            }

            area.ChangePolicy(after, reason, Tpo, Now);
            var decision = Decision.RecordTpoDecision(
                NextDecisionId(),
                DecisionReference.To(area.Id),
                $"Policy of \"{area.Name}\": autonomy {(int)before.Autonomy} → {(int)after.Autonomy}, " +
                $"transparency {(int)before.Transparency} → {(int)after.Transparency}",
                reason,
                DecisionEffects.None,
                Tpo,
                Now);
            Save($"area-{area.Id}", area);
            Save($"decision-{decision.Id}", decision);

            if (before.Autonomy != AutonomyLevel.ProposeOnly || after.Autonomy == AutonomyLevel.ProposeOnly)
            {
                return;
            }

            // Needs that waited only because the AI was not allowed to build may continue now. Needs the
            // TPO deferred (declined, kept the budget, or let the default apply) stay as they are.
            foreach (var need in LoadAll("need-", Need.FromHistory).Select(x => x.Aggregate)
                         .Where(n => n.Status == NeedStatus.PropertiesDefined && !HasOpenQuestion(n.Id) && !WasDeferred(n.Id)))
            {
                var proposal = ProposalOf(need.Id)!;
                TryBuild(need, proposal.GetOption(proposal.AcceptedOption!.Value), approvedByTpo: false);
            }
        }
    }

    // ---- Simulated build agent ----------------------------------------------------------------

    /// <summary>
    /// The build agent wants to implement the accepted option. The escalation rules of the domain
    /// decide whether it may do so on its own or has to ask the TPO.
    /// </summary>
    private void TryBuild(Need need, ProposalOption option, bool approvedByTpo)
    {
        var area = LoadArea();
        var plan = SimulatedAgents.PlanFor(option.Id);
        var triggers = new List<AlwaysAskTrigger>();

        var (exceeded, projected) = CheckBudgets(option);
        if (exceeded.Count > 0)
        {
            triggers.Add(AlwaysAskTrigger.BudgetExceeded);
        }

        var escalation = EscalationRules.Evaluate(IntendedChange.Technical([area.Policy], [.. triggers]));

        if (escalation.Outcome == EscalationOutcome.TpoQuestion)
        {
            var limits = string.Join(" and ", exceeded.Select(p => PropertyTemplates.Describe(p.Definition.Rule)).Distinct());
            Ask(need, escalation.EffectiveAutonomy,
                $"With \"{option.Title}\" the monthly cost of \"{area.Name}\" can reach {Money(projected)}, but the budget is {limits}.",
                [
                    new("raise-budget", $"Raise the budget to {Money(projected)} per month", "The AI builds the solution as proposed."),
                    new("keep-budget", "Keep the budget", "Nothing is built. You can send the need back for a new analysis later."),
                ],
                "keep-budget");
            return;
        }

        if (escalation.Outcome == EscalationOutcome.ProposalOnly)
        {
            // Level 1: the AI only proposes. The need waits until the TPO raises the autonomy.
            return;
        }

        if (escalation.Outcome == EscalationOutcome.ApprovalPerStep && !approvedByTpo)
        {
            Ask(need, escalation.EffectiveAutonomy,
                $"May the AI now build: {plan.Choice}?",
                [
                    new("approve", "Yes, build it", plan.Rationale),
                    new("decline", "Not now", "Nothing is built for now."),
                ],
                "decline");
            return;
        }

        var shouldNotes = ChangeFeasibility.UnmetTargets(option, ActiveProperties(), Now)
            .Select(u => $" The \"should\" property {PropertyTemplates.For(u.Property.Definition.Type).Label.ToLowerInvariant()} " +
                         $"{PropertyTemplates.Describe(u.Property.Definition.Rule)} will probably not be met (expected {DescribeEffect(u.Effect).Text}).");
        var rationale = plan.Rationale + (approvedByTpo ? " Approved by the TPO." : "") + string.Concat(shouldNotes);

        var componentId = new ComponentId($"C-{_store.LoadAll("component-").Count + 1:000}");
        var decision = Decision.RecordAiDecision(
            NextDecisionId(),
            DecisionReference.To(need.Id),
            plan.Choice,
            rationale,
            new DecisionEffects([componentId], []),
            escalation.EffectiveAutonomy,
            SimulatedAgents.Builder,
            Reversibility.By("Remove the building block again; the system works as before."),
            ["simulated"],
            Now);
        Save($"decision-{decision.Id}", decision);

        var component = Component.Plan(componentId, CatalogArea, plan.ComponentName, plan.Kind, [], decision.Id, SimulatedAgents.Builder, Now);
        need.StartImplementation(SimulatedAgents.Builder, Now);
        component.Activate([$"simulated:{componentId}"], decision.Id, Now);
        need.StartOperation(SimulatedAgents.Builder, Now);
        Save($"component-{component.Id}", component);
        Save($"need-{need.Id}", need);
    }

    private void Ask(Need need, AutonomyLevel level, string question, IReadOnlyList<QuestionOption> options, string defaultKey)
    {
        var decision = Decision.AskTpo(
            NextDecisionId(), DecisionReference.To(need.Id), question, options, defaultKey,
            Now + QuestionDeadline, level, SimulatedAgents.Builder, Now);
        Save($"decision-{decision.Id}", decision);
    }

    private void ApplyAnswer(Decision question)
    {
        if (question.Reference.Kind != ReferenceKind.Need)
        {
            return;
        }

        var need = LoadNeed(question.Reference.Id);
        if (need.Status != NeedStatus.PropertiesDefined)
        {
            return;
        }

        var proposal = ProposalOf(need.Id)!;
        var option = proposal.GetOption(proposal.AcceptedOption!.Value);

        switch (question.Chosen)
        {
            case "raise-budget":
                var (exceeded, projected) = CheckBudgets(option);
                foreach (var budget in exceeded)
                {
                    var d = budget.Definition;
                    var rule = (ThresholdRule)d.Rule;
                    budget.Change(
                        new PropertyDefinition(d.Type, d.Scope, rule with { Value = projected }, d.Measurement, d.Bindingness, d.Validity, d.Responsible),
                        question.Id, Tpo, LoadArea().Policy.Autonomy, Now);
                    Save($"property-{budget.Id}", budget);
                }

                TryBuild(need, option, approvedByTpo: false);
                break;
            case "approve":
                TryBuild(need, option, approvedByTpo: true);
                break;
        }
    }

    /// <summary>Questions whose deadline passed get their default option (part of every read).</summary>
    private void ApplyDueDefaults()
    {
        foreach (var (question, _) in LoadAll("decision-", Decision.FromHistory)
                     .Where(x => x.Aggregate.QuestionStatus == QuestionStatus.Open))
        {
            if (question.ApplyDefaultIfDue(Now))
            {
                Save($"decision-{question.Id}", question);
                ApplyAnswer(question);
            }
        }
    }

    // ---- Read models --------------------------------------------------------------------------

    public OverviewView GetOverview()
    {
        lock (_lock)
        {
            ApplyDueDefaults();
            var area = LoadArea();
            var needs = LoadAll("need-", Need.FromHistory).Select(x => x.Aggregate).ToList();
            var context = new ReadContext(this, needs);
            var decisions = context.Decisions;

            return new OverviewView(
                Version,
                "Online shop",
                new AreaView(area.Id.Value, area.Name, area.Policy.Autonomy, area.Policy.Transparency),
                needs.OrderByDescending(n => n.Id.Value, StringComparer.Ordinal)
                    .Select(n => new NeedSummaryView(n.Id.Value, n.Title, n.Status, n.Priority)).ToList(),
                context.Properties,
                context.Components,
                decisions.Where(d => d.QuestionStatus == QuestionStatus.Open).ToList(),
                decisions.Where(d => d.QuestionStatus != QuestionStatus.Open).OrderByDescending(d => d.At).ToList());
        }
    }

    public NeedView GetNeed(string needId)
    {
        lock (_lock)
        {
            ApplyDueDefaults();
            var need = LoadNeed(needId);
            var proposal = ProposalOf(need.Id);
            var context = new ReadContext(this, [need]);

            ProposalView? proposalView = proposal is null ? null : new ProposalView(
                proposal.Id.Value,
                proposal.Analysis,
                proposal.Status,
                proposal.AcceptedOption?.Value,
                proposal.Options.Select(o => new OptionView(
                    o.Id.Value,
                    o.Title,
                    o.Description,
                    o.ExpectedEffects.Select(DescribeEffect).ToList(),
                    o.Cost.MinPerMonth,
                    o.Cost.MaxPerMonth,
                    o.Cost.Currency,
                    o.Risks,
                    o.RequiredProperties,
                    o.Id == proposal.Recommended)).ToList());

            return new NeedView(
                need.Id.Value,
                need.Title,
                need.Description,
                need.Status,
                need.Priority,
                need.RequestedBy,
                proposalView,
                context.Properties.Where(p => p.NeedId == need.Id.Value).ToList(),
                context.Components.Where(c => c.NeedId == need.Id.Value).ToList(),
                context.Decisions.Where(d => d.NeedId == need.Id.Value).OrderByDescending(d => d.At).ToList());
        }
    }

    /// <summary>Joins the streams for the read models: property → decision → proposal → need.</summary>
    private sealed class ReadContext
    {
        public ReadContext(RemitService service, IReadOnlyList<Need> needs)
        {
            var needTitles = service.LoadAll("need-", Need.FromHistory).ToDictionary(x => x.Aggregate.Id.Value, x => x.Aggregate.Title);
            var proposals = service.LoadAll("proposal-", Proposal.FromHistory).Select(x => x.Aggregate).ToList();
            var proposalToNeed = proposals.ToDictionary(p => p.Id.Value, p => p.NeedId.Value);
            var decisions = service.LoadAll("decision-", Decision.FromHistory);

            string? NeedOf(Decision d) => d.Reference.Kind switch
            {
                ReferenceKind.Need => d.Reference.Id,
                ReferenceKind.Proposal => proposalToNeed.GetValueOrDefault(d.Reference.Id),
                _ => null,
            };

            var decisionToNeed = decisions.ToDictionary(x => x.Aggregate.Id.Value, x => NeedOf(x.Aggregate));

            Decisions = decisions.Select(x =>
            {
                var d = x.Aggregate;
                var need = NeedOf(d);
                return new DecisionView(
                    d.Id.Value,
                    d.Kind,
                    d.DecidedBy.Name,
                    d.Chosen is not null && d.Kind == DecisionKind.TpoQuestion
                        ? d.Options.FirstOrDefault(o => o.Key == d.Chosen)?.Title ?? d.Chosen
                        : d.Chosen,
                    d.Rationale,
                    d.Level,
                    d.Chosen is null ? x.CreatedAt : d.DecidedAt,
                    need,
                    need is null ? null : needTitles.GetValueOrDefault(need),
                    d.Question,
                    d.Options.Select(o => new QuestionOptionView(o.Key, o.Title, o.Description)).ToList(),
                    d.DefaultOptionKey,
                    d.Deadline,
                    d.QuestionStatus);
            }).ToList();

            var acceptedOptions = proposals
                .Where(p => p.AcceptedOption is not null)
                .ToDictionary(p => p.NeedId.Value, p => p.GetOption(p.AcceptedOption!.Value));

            Properties = service.LoadAll("property-", SystemProperty.FromHistory)
                .Select(x => x.Aggregate)
                .Where(p => !p.IsRetired)
                .Select(p =>
                {
                    var need = decisionToNeed.GetValueOrDefault(p.OriginatingDecision.Value);
                    var template = PropertyTemplates.For(p.Definition.Type);
                    string? expected = null;
                    if (p.Definition.Rule is ThresholdRule rule && need is not null &&
                        acceptedOptions.TryGetValue(need, out var option))
                    {
                        expected = option.ExpectedEffects
                            .Where(e => string.Equals(e.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase))
                            .Select(e => DescribeEffect(e).Text)
                            .FirstOrDefault();
                        if (expected is null && p.Definition.Type == PropertyType.Budget)
                        {
                            expected = $"{PropertyTemplates.Number(option.Cost.MinPerMonth)}–{Money(option.Cost.MaxPerMonth)} per month";
                        }
                    }

                    return new PropertyView(
                        p.Id.Value,
                        p.Definition.Type,
                        template.Label,
                        PropertyTemplates.Describe(p.Definition.Rule),
                        p.Definition.Bindingness,
                        p.Definition.Measurement.Source,
                        expected,
                        need);
                })
                .ToList();

            Components = service.LoadAll("component-", Component.FromHistory)
                .Select(x => x.Aggregate)
                .Select(c => new ComponentView(c.Id.Value, c.Name, c.Kind, c.Status, decisionToNeed.GetValueOrDefault(c.CreatedFrom.Value)))
                .ToList();
        }

        public IReadOnlyList<DecisionView> Decisions { get; }

        public IReadOnlyList<PropertyView> Properties { get; }

        public IReadOnlyList<ComponentView> Components { get; }
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static EffectView DescribeEffect(ExpectedEffect effect)
    {
        var template = PropertyTemplates.All.FirstOrDefault(t => t.Metric == effect.Metric);
        var value = PropertyTemplates.Number(effect.Value);
        return new EffectView(template?.Label ?? effect.Metric, $"about {value} {effect.Unit}");
    }

    private static string Money(decimal amount) => $"{PropertyTemplates.Number(amount)} EUR";

    private static string DescribeContradiction(PropertyContradiction c, IEnumerable<SystemProperty> all)
    {
        var list = all.ToList();
        string Text(SystemPropertyId id)
        {
            var p = list.First(x => x.Id == id);
            return $"{PropertyTemplates.For(p.Definition.Type).Label} {PropertyTemplates.Describe(p.Definition.Rule)}";
        }

        return $"\"{Text(c.First)}\" and \"{Text(c.Second)}\".";
    }

    private bool HasOpenQuestion(NeedId need) =>
        LoadAll("decision-", Decision.FromHistory).Any(x =>
            x.Aggregate.QuestionStatus == QuestionStatus.Open &&
            x.Aggregate.Reference == DecisionReference.To(need));

    private static readonly string[] DeferringAnswers = ["keep-budget", "decline"];

    /// <summary>The latest answered question about the need put the build on hold.</summary>
    private bool WasDeferred(NeedId need) =>
        LoadAll("decision-", Decision.FromHistory)
            .Where(x => x.Aggregate.Kind == DecisionKind.TpoQuestion && x.Aggregate.Reference == DecisionReference.To(need))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Aggregate.Chosen)
            .LastOrDefault() is { } chosen && DeferringAnswers.Contains(chosen);

    private List<SystemProperty> ActiveProperties() =>
        LoadAll("property-", SystemProperty.FromHistory).Select(x => x.Aggregate).Where(p => p.IsActiveAt(Now)).ToList();

    /// <summary>Budgets of the area that the option would exceed, together with the area's projected monthly cost.</summary>
    private (IReadOnlyList<SystemProperty> Exceeded, decimal Projected) CheckBudgets(ProposalOption option)
    {
        var running = LoadAll("need-", Need.FromHistory)
            .Select(x => x.Aggregate)
            .Where(n => n.Status is NeedStatus.InImplementation or NeedStatus.InOperation)
            .Select(n => ProposalOf(n.Id))
            .Where(p => p?.AcceptedOption is not null)
            .Sum(p => p!.GetOption(p.AcceptedOption!.Value).Cost.MaxPerMonth);
        return (ChangeFeasibility.ExceededBudgets(option, running, ActiveProperties(), Now),
                ChangeFeasibility.ProjectedMonthlyCost(option, running));
    }

    private DecisionId NextDecisionId() => new($"D-{_store.LoadAll("decision-").Count + 1:000}");

    private Area LoadArea() => Load($"area-{CatalogArea}", Area.FromHistory)!;

    private Need LoadNeed(string id) =>
        Load($"need-{id}", Need.FromHistory) ?? throw new NotFoundException($"There is no need {id}.");

    private Proposal? ProposalOf(NeedId need) =>
        LoadAll("proposal-", Proposal.FromHistory).Select(x => x.Aggregate).LastOrDefault(p => p.NeedId == need);

    private T? Load<T>(string stream, Func<IEnumerable<IDomainEvent>, T> rehydrate) where T : class
    {
        var events = _store.Load(stream);
        return events.Count == 0 ? null : rehydrate(events);
    }

    private IReadOnlyList<(T Aggregate, DateTimeOffset CreatedAt)> LoadAll<T>(string prefix, Func<IEnumerable<IDomainEvent>, T> rehydrate) =>
        _store.LoadAll(prefix).Select(events => (rehydrate(events), events[0].OccurredAt)).ToList();

    private void Save<TId>(string stream, AggregateRoot<TId> aggregate) where TId : notnull
    {
        var events = aggregate.UncommittedEvents;
        _store.Append(stream, aggregate.Version - events.Count, [.. events]);
        aggregate.MarkCommitted();
    }
}

public sealed class NotFoundException(string message) : Exception(message);
