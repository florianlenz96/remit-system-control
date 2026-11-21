import type { AutonomyLevel, DecisionKind, NeedStatus, PropertyInput, PropertyTemplate, PropertyType, TransparencyLevel } from './api'

export const needStatus: Record<NeedStatus, string> = {
  New: 'New',
  InAnalysis: 'AI is analysing',
  ProposalAvailable: 'Waiting for your decision',
  Rejected: 'Rejected',
  PropertiesDefined: 'Properties set',
  InImplementation: 'Being built',
  InOperation: 'In operation',
  Closed: 'Closed',
}

/** Visual tone of a status chip. */
export function statusTone(status: NeedStatus): 'action' | 'progress' | 'done' | 'muted' {
  switch (status) {
    case 'ProposalAvailable': return 'action'
    case 'InOperation': return 'done'
    case 'Rejected':
    case 'Closed': return 'muted'
    default: return 'progress'
  }
}

export const autonomy: { value: AutonomyLevel; level: number; text: string }[] = [
  { value: 'ProposeOnly', level: 1, text: 'The AI only proposes' },
  { value: 'ApproveEachStep', level: 2, text: 'The AI builds after you approve each step' },
  { value: 'DecideWithinProperties', level: 3, text: 'The AI decides on its own within your properties' },
  { value: 'AdjustShouldProperties', level: 4, text: 'Like 3, and it may adjust "should" properties' },
]

export const transparency: { value: TransparencyLevel; level: number; text: string }[] = [
  { value: 'Result', level: 1, text: 'Results: targets, actual values, costs' },
  { value: 'Decisions', level: 2, text: 'Additionally every decision with its rationale' },
  { value: 'DataFlow', level: 3, text: 'Additionally the data flow' },
]

export const decisionKind: Record<DecisionKind, string> = {
  AiDecision: 'AI decision',
  TpoDecision: 'Your decision',
  TpoQuestion: 'Your answer',
}

export function cost(min: number, max: number, currency: string): string {
  return min === max ? `${min} ${currency}/month` : `${min}–${max} ${currency}/month`
}

export function dateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

/** Pre-filled inputs for the properties an option requires. */
export function initialInputs(required: PropertyType[], templates: PropertyTemplate[]): PropertyInput[] {
  return required.map((type) => {
    const t = templates.find((x) => x.type === type)
    return {
      type,
      value: t?.isNumeric ? t.defaultValue : null,
      statement: t?.isNumeric ? null : (t?.defaultStatement ?? ''),
      bindingness: 'Must',
    }
  })
}

/** First problem in the inputs, in the TPO's words, or null if all can be sent. */
export function validate(inputs: PropertyInput[], templates: PropertyTemplate[]): string | null {
  for (const input of inputs) {
    const t = templates.find((x) => x.type === input.type)
    if (!t) continue
    if (t.isNumeric && (input.value === null || Number.isNaN(input.value))) return `${t.label}: please enter a value.`
    if (!t.isNumeric && !input.statement?.trim()) return `${t.label}: please describe the rule.`
  }
  return null
}
