# Vision

## The idea in one sentence

Programming languages are losing importance for most people involved: whoever is responsible for a system
will in future describe intent and limits – AI builds and operates. The Kommandozentrale is the place
where that happens.

## Why

- The building blocks exist individually (coding agents, SRE Agent, Chaos Studio, ticket systems, wikis),
  but nothing connects them into a continuous loop from intent to operation.
- Customers will not give AI full access to production systems. They need graduated,
  measurable trust.
- The role "Technical Product Owner" is emerging: someone who understands the overall system and
  describes in technical domain language what they want, without having to understand every Component.

## The role of the TPO

- Writes or takes over **Needs** (e.g. from Jira).
- Evaluates the AI's **Proposals** and sets **System properties**: targets, TTL, backup,
  failure behavior, budget, data location.
- Sets per **Area** how much the AI may do on its own (**autonomy**) and how precisely it
  must explain itself (**transparency**).
- Makes the Decisions that no technology can answer – above all when properties
  conflict (example: on Black Friday, target response time and budget cannot both be met).
- Keeps **ownership**. The AI executes.

## The loop

1. **Need**: "The shop should respond faster."
2. **Proposal**: The AI analyzes the cause and offers Options (cache, CDN, scale the DB).
3. **Properties**: The TPO accepts an Option and sets what must apply.
4. **Build**: A build agent implements it and documents each of its own Decisions with a rationale.
5. **Operate**: An operations agent keeps the properties met permanently.
6. **Learn**: Deviations lead to new Decisions – by the AI or as a TPO question.

## Autonomy and transparency

How much the TPO has to understand does not depend on technical complexity, but on
what happens in the worst case. Three questions per Area:

- Is the damage limited?
- Can a mistake be undone?
- Are people or legal matters affected?

A cache (limited, reversible, reconstructable) gets high autonomy and little transparency.
Customer data (not reversible, GDPR accountability) gets low autonomy and full
transparency, down to the data flow.

**Earned autonomy:** The command center evaluates AI Decisions after the fact. Above a
configurable success rate, it proposes a higher level to the TPO – the TPO must decide.

Autonomy is additionally enforced via Azure permissions: an agent at level 1 only has
read permissions and *cannot* change anything. Autonomy is thus technically guaranteed, not merely agreed.

## Delivery

The command center runs in the **customer's Azure tenant**, one instance per customer, deployed via Bicep,
later as an Azure Managed Application. Data, models (Microsoft Foundry, EU region) and agents
stay with the customer.

## Business model

- Framework and method as marketing and positioning ("CAF for the AI era" –
  a complement to Microsoft, not competition).
- License per instance.
- Consulting: assessment ("You are at level 1"), introduction, support in moving up
  the autonomy levels.

## Differentiation

Backstage, Port and Cortex are catalogs – they show what exists. The Kommandozentrale
*steers* the loop from intent to operation.
