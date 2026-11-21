import { describe, expect, it } from 'vitest'
import type { PropertyTemplate } from './api'
import { cost, initialInputs, statusTone, validate } from './labels'

const templates: PropertyTemplate[] = [
  { type: 'Performance', label: 'Response time', question: '', metric: 'p95_response_time', unit: 'ms', defaultValue: 250, defaultStatement: null, measuredBy: '', isNumeric: true, minValue: 1, maxValue: null },
  { type: 'Exclusion', label: 'Exclusion', question: '', metric: null, unit: null, defaultValue: null, defaultStatement: 'Never from a copy', measuredBy: '', isNumeric: false, minValue: null, maxValue: null },
]

describe('initialInputs', () => {
  it('pre-fills every required property as "must" with the template default', () => {
    expect(initialInputs(['Performance', 'Exclusion'], templates)).toEqual([
      { type: 'Performance', value: 250, statement: null, bindingness: 'Must' },
      { type: 'Exclusion', value: null, statement: 'Never from a copy', bindingness: 'Must' },
    ])
  })
})

describe('validate', () => {
  it('accepts complete inputs', () => {
    expect(validate(initialInputs(['Performance', 'Exclusion'], templates), templates)).toBeNull()
  })

  it('names the property that is missing a value', () => {
    const inputs = initialInputs(['Performance', 'Exclusion'], templates)
    inputs[1].statement = '  '
    expect(validate(inputs, templates)).toBe('Exclusion: please describe the rule.')
  })
})

describe('formatting', () => {
  it('formats cost ranges per month', () => {
    expect(cost(150, 200, 'EUR')).toBe('150–200 EUR/month')
    expect(cost(50, 50, 'EUR')).toBe('50 EUR/month')
  })

  it('highlights needs that wait for the TPO', () => {
    expect(statusTone('ProposalAvailable')).toBe('action')
    expect(statusTone('InOperation')).toBe('done')
  })
})
