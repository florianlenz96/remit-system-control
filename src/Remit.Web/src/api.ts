// Types mirror the read models of the backend (src/Remit.App/Application/Views.cs).

export type NeedStatus =
  | 'New' | 'InAnalysis' | 'ProposalAvailable' | 'Rejected'
  | 'PropertiesDefined' | 'InImplementation' | 'InOperation' | 'Closed'
export type Priority = 'High' | 'Medium' | 'Low'
export type PropertyType =
  | 'Performance' | 'Availability' | 'Freshness' | 'Exclusion'
  | 'FailureBehavior' | 'Recovery' | 'Budget' | 'Location'
export type Bindingness = 'Must' | 'Should'
export type AutonomyLevel = 'ProposeOnly' | 'ApproveEachStep' | 'DecideWithinProperties' | 'AdjustShouldProperties'
export type TransparencyLevel = 'Result' | 'Decisions' | 'DataFlow'
export type DecisionKind = 'AiDecision' | 'TpoDecision' | 'TpoQuestion'
export type QuestionStatus = 'Open' | 'Answered' | 'DefaultApplied'

export interface Area { id: string; name: string; autonomy: AutonomyLevel; transparency: TransparencyLevel }
export interface NeedSummary { id: string; title: string; status: NeedStatus; priority: Priority }
export interface Option {
  id: string; title: string; description: string
  effects: { label: string; text: string }[]
  costMin: number; costMax: number; currency: string
  risks: string[]; requiredProperties: PropertyType[]; recommended: boolean
}
export interface Proposal { id: string; analysis: string; status: string; acceptedOption: string | null; options: Option[] }
export interface Property {
  id: string; type: PropertyType; label: string; rule: string; bindingness: Bindingness
  measuredBy: string; expected: string | null; needId: string | null
}
export interface Component { id: string; name: string; kind: string; status: string; needId: string | null }
export interface Decision {
  id: string; kind: DecisionKind; by: string; chosen: string | null; rationale: string | null
  level: AutonomyLevel | null; at: string; needId: string | null; needTitle: string | null
  question: string | null; options: { key: string; title: string; description: string }[]
  defaultOption: string | null; deadline: string | null; questionStatus: QuestionStatus | null
}
export interface Overview {
  version: string; systemName: string; area: Area; needs: NeedSummary[]; properties: Property[]
  components: Component[]; openQuestions: Decision[]; decisions: Decision[]
}
export interface Need {
  id: string; title: string; description: string; status: NeedStatus; priority: Priority; requestedBy: string
  proposal: Proposal | null; properties: Property[]; components: Component[]; decisions: Decision[]
}
export interface PropertyTemplate {
  type: PropertyType; label: string; question: string; metric: string | null; unit: string | null
  defaultValue: number | null; defaultStatement: string | null; measuredBy: string; isNumeric: boolean
  minValue: number | null; maxValue: number | null
}
export interface PropertyInput { type: PropertyType; value: number | null; statement: string | null; bindingness: Bindingness }

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new Error(problem?.title ?? 'Something went wrong. Please try again.')
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export const api = {
  overview: () => request<Overview>('GET', '/api/overview'),
  need: (id: string) => request<Need>('GET', `/api/needs/${id}`),
  templates: () => request<PropertyTemplate[]>('GET', '/api/property-templates'),
  registerNeed: (title: string, description: string, priority: Priority) =>
    request<{ id: string }>('POST', '/api/needs', { title, description, priority }),
  accept: (needId: string, optionId: string, properties: PropertyInput[]) =>
    request<void>('POST', `/api/needs/${needId}/accept`, { optionId, properties }),
  reject: (needId: string, reason: string) => request<void>('POST', `/api/needs/${needId}/reject`, { reason }),
  answer: (questionId: string, optionKey: string, rationale: string) =>
    request<void>('POST', `/api/questions/${questionId}/answer`, { optionKey, rationale }),
  changePolicy: (autonomy: AutonomyLevel, transparency: TransparencyLevel, reason: string) =>
    request<void>('PUT', '/api/area/policy', { autonomy, transparency, reason }),
}
