// Azure Monitor alerts derived from Remit's System properties (ADR 0010).
// Parameters come from Remit: GET /api/setup/alert-rules.parameters.json (signed in).
// The alerts watch the monitored system (e.g. the shop), not Remit itself.

targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Resource id of the Application Insights component of the monitored system.')
param appInsightsId string

@description('Optional action group, e.g. for e-mail. Azure SRE Agent picks up Azure Monitor alerts of its managed resource groups without one.')
param actionGroupId string = ''

@description('Log search alerts from Remit: name, propertyId, description, signal, operator, threshold, windowMinutes, severity.')
param logAlerts array = []

@description('Budgets from Remit: name, propertyId, amount (EUR per month).')
param budgets array = []

@description('E-mail addresses notified when a budget is about to be or is exceeded.')
param budgetContactEmails array = []

@description('First day of the current month; budgets start here.')
param budgetStartDate string = '${utcNow('yyyy-MM')}-01'

var queries = {
  responseTimeP95: 'requests | summarize value = percentile(duration, 95)'
  availability: 'requests | summarize value = 100.0 * countif(success == true) / count()'
}

resource alerts 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [for alert in logAlerts: {
  name: alert.name
  location: location
  properties: {
    displayName: alert.name
    description: alert.description
    severity: alert.severity
    enabled: true
    scopes: [ appInsightsId ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT${alert.windowMinutes}M'
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: queries[alert.signal]
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'value'
          operator: alert.operator
          threshold: alert.threshold
          failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 }
        }
      ]
    }
    actions: {
      actionGroups: empty(actionGroupId) ? [] : [ actionGroupId ]
      customProperties: { remitPropertyId: alert.propertyId }
    }
  }
}]

resource costBudgets 'Microsoft.Consumption/budgets@2023-05-01' = [for budget in budgets: {
  name: budget.name
  properties: {
    category: 'Cost'
    amount: budget.amount
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      forecast80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Forecasted'
        contactEmails: budgetContactEmails
        contactRoles: [ 'Owner' ]
        contactGroups: empty(actionGroupId) ? [] : [ actionGroupId ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: budgetContactEmails
        contactRoles: [ 'Owner' ]
        contactGroups: empty(actionGroupId) ? [] : [ actionGroupId ]
      }
    }
  }
}]
