<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import { api, type Need, type Option, type Overview, type PropertyInput, type PropertyTemplate, type PropertyType } from '../api'
import { cost, dateTime, decisionKind, initialInputs, needStatus, statusTone, validate } from '../labels'

const props = defineProps<{ id: string; overview: Overview }>()
const refresh = inject<() => Promise<void>>('refresh')!

const need = ref<Need | null>(null)
const templates = ref<PropertyTemplate[]>([])
const chosen = ref<Option | null>(null)
const inputs = ref<PropertyInput[]>([])
const rejecting = ref(false)
const reason = ref('')
const busy = ref(false)
const error = ref<string | null>(null)

async function load() {
  try {
    need.value = await api.need(props.id)
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(async () => {
  try {
    templates.value = await api.templates()
  } catch (e) {
    error.value = (e as Error).message
    return
  }
  await load()
})

const isOpen = computed(() => need.value?.proposal?.status === 'Open')
const template = (type: PropertyType) => templates.value.find((t) => t.type === type)!
const addable = computed(() => templates.value.filter((t) => !inputs.value.some((i) => i.type === t.type)))
const requiredTypes = computed(() => chosen.value?.requiredProperties ?? [])
const openQuestion = computed(() => props.overview.openQuestions.find((q) => q.needId === props.id))

function choose(option: Option) {
  chosen.value = option
  inputs.value = initialInputs(option.requiredProperties, templates.value)
  error.value = null
}

function add(type: string) {
  if (type) inputs.value.push(...initialInputs([type as PropertyType], templates.value))
}

async function run(action: () => Promise<void>) {
  busy.value = true
  error.value = null
  try {
    await action()
    chosen.value = null
    rejecting.value = false
    await Promise.all([refresh(), load()])
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

function approve() {
  const problem = validate(inputs.value, templates.value)
  if (problem) {
    error.value = problem
    return
  }
  run(() => api.accept(props.id, chosen.value!.id, inputs.value))
}
</script>

<template>
  <p v-if="!need && error" class="error">{{ error }}</p>
  <div v-if="need" class="layout">
    <section>
      <a href="#/" class="back">← All needs</a>
      <div class="heading">
        <h1><span class="muted">{{ need.id }}</span> {{ need.title }}</h1>
        <span class="chip" :class="statusTone(need.status)">{{ needStatus[need.status] }}</span>
      </div>
      <p class="description">{{ need.description }}</p>
      <p class="muted">Priority {{ need.priority.toLowerCase() }} · entered by {{ need.requestedBy }}</p>

      <div v-if="openQuestion" class="card question">
        <strong>The AI has a question about this need.</strong>
        <a href="#/operation">Answer it in Operation →</a>
      </div>

      <!-- Screen 1: AI proposal -->
      <template v-if="need.proposal && !chosen">
        <h2>AI analysis</h2>
        <p>{{ need.proposal.analysis }}</p>
        <div class="options">
          <article
            v-for="option in need.proposal.options" :key="option.id" class="card option"
            :class="{ recommended: option.recommended, accepted: option.id === need.proposal.acceptedOption }">
            <span v-if="option.recommended" class="tag">Recommended</span>
            <span v-if="option.id === need.proposal.acceptedOption" class="tag done">Your choice</span>
            <h3>{{ option.title }}</h3>
            <p>{{ option.description }}</p>
            <dl>
              <template v-for="effect in option.effects" :key="effect.label">
                <dt>Expected effect</dt><dd>{{ effect.label }} {{ effect.text }}</dd>
              </template>
              <dt>Cost</dt><dd>{{ cost(option.costMin, option.costMax, option.currency) }}</dd>
              <dt>Risks</dt><dd><ul><li v-for="r in option.risks" :key="r">{{ r }}</li></ul></dd>
              <dt>You set</dt><dd>{{ option.requiredProperties.map((t) => template(t)?.label).join(', ') }}</dd>
            </dl>
            <button v-if="isOpen" :class="{ primary: option.recommended }" @click="choose(option)">
              Accept and set properties
            </button>
          </article>
        </div>

        <div v-if="isOpen" class="reject">
          <button v-if="!rejecting" class="quiet" @click="rejecting = true">Reject the need</button>
          <form v-else class="card form" @submit.prevent="run(() => api.reject(need!.id, reason))">
            <label>Why is the need rejected? <input v-model="reason" required /></label>
            <p v-if="error" class="error">{{ error }}</p>
            <div class="actions">
              <button type="submit" :disabled="busy">Reject</button>
              <button type="button" class="quiet" @click="rejecting = false">Cancel</button>
            </div>
          </form>
        </div>
      </template>

      <!-- Screen 2: set system properties -->
      <form v-if="chosen" class="card form properties" @submit.prevent="approve">
        <h2>Set system properties</h2>
        <p class="lead">You set what must apply. The AI decides how.</p>
        <p class="muted">Option: {{ chosen.title }}</p>

        <div v-for="(input, index) in inputs" :key="input.type" class="property">
          <div class="property-head">
            <strong>{{ template(input.type).label }}</strong>
            <span v-if="!requiredTypes.includes(input.type)" class="muted">additional</span>
            <button v-if="!requiredTypes.includes(input.type)" type="button" class="quiet small" @click="inputs.splice(index, 1)">Remove</button>
          </div>
          <label class="hint">{{ template(input.type).question }}
            <span v-if="template(input.type).isNumeric" class="value">
              <input type="number" step="any" v-model.number="input.value" required
                :min="template(input.type).minValue ?? undefined" :max="template(input.type).maxValue ?? undefined" />
              <span>{{ template(input.type).unit }}</span>
            </span>
            <input v-else v-model="input.statement" required />
          </label>
          <div class="binding">
            <label><input type="radio" :name="`binding-${input.type}`" value="Must" v-model="input.bindingness" /> must</label>
            <label><input type="radio" :name="`binding-${input.type}`" value="Should" v-model="input.bindingness" /> should</label>
            <span class="muted">Checked by: {{ template(input.type).measuredBy }}</span>
          </div>
        </div>

        <label v-if="addable.length" class="add">Add a further property
          <select @change="add(($event.target as HTMLSelectElement).value); ($event.target as HTMLSelectElement).value = ''">
            <option value="">Choose…</option>
            <option v-for="t in addable" :key="t.type" :value="t.type">{{ t.label }}</option>
          </select>
        </label>

        <div class="explain">
          <strong>What the AI does with it</strong>
          <p>
            In "{{ overview.area.name }}" the autonomy level is
            {{ { ProposeOnly: 1, ApproveEachStep: 2, DecideWithinProperties: 3, AdjustShouldProperties: 4 }[overview.area.autonomy] }}.
            The AI decides on its own only within these properties. If a solution would exceed the budget,
            change the data location or two "must" properties collide, it asks you.
          </p>
        </div>

        <p v-if="error" class="error">{{ error }}</p>
        <div class="actions">
          <button type="submit" class="primary" :disabled="busy">Approve – AI implements and operates</button>
          <button type="button" class="quiet" @click="chosen = null">Back to the options</button>
        </div>
      </form>

      <template v-if="need.properties.length">
        <h2>Properties of this need</h2>
        <table>
          <tr v-for="p in need.properties" :key="p.id">
            <td>{{ p.label }}</td><td><strong>{{ p.rule }}</strong></td><td class="muted">{{ p.bindingness.toLowerCase() }}</td>
          </tr>
        </table>
      </template>

      <template v-if="need.decisions.length">
        <h2>Decisions</h2>
        <ul class="log">
          <li v-for="d in need.decisions" :key="d.id">
            <span class="muted">{{ dateTime(d.at) }} · {{ d.kind === 'TpoQuestion' && d.questionStatus === 'Open' ? 'Open question' : decisionKind[d.kind] }}</span>
            <strong>{{ d.chosen ?? d.question }}</strong>
            <span v-if="d.rationale">{{ d.rationale }}</span>
          </li>
        </ul>
      </template>
    </section>

    <aside>
      <div class="card">
        <h3>System map</h3>
        <ul class="map">
          <li v-for="c in overview.components" :key="c.id" :class="{ highlight: c.needId === need.id }">
            {{ c.name }} <span v-if="c.needId === need.id" class="muted">· for this need</span>
            <span v-else-if="c.takenOver" class="muted">· existing</span>
          </li>
          <li v-if="chosen" class="proposed">{{ chosen.title }} <span class="muted">· proposed</span></li>
        </ul>
        <p v-if="!overview.components.length && !chosen" class="muted">No building blocks created by the AI yet.</p>
      </div>
      <div v-if="overview.needs.length > 1" class="card">
        <h3>Further needs</h3>
        <ul class="map">
          <li v-for="n in overview.needs.filter((x) => x.id !== need!.id)" :key="n.id">
            <a :href="`#/needs/${n.id}`">{{ n.title }}</a>
          </li>
        </ul>
      </div>
    </aside>
  </div>
</template>
