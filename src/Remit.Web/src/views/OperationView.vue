<script setup lang="ts">
import { computed, inject, onMounted, ref, watch } from 'vue'
import { api, type AutonomyLevel, type Overview, type PropertyInput, type PropertyTemplate, type PropertyType, type TransparencyLevel } from '../api'
import { autonomy, dateTime, decisionKind, deviationKind, initialInputs, transparency, validate } from '../labels'

const props = defineProps<{ overview: Overview }>()
const refresh = inject<() => Promise<void>>('refresh')!

const level = ref<AutonomyLevel>(props.overview.area.autonomy)
const visibility = ref<TransparencyLevel>(props.overview.area.transparency)
const reason = ref('')
const rationale = ref<Record<string, string>>({})
const busy = ref(false)
const error = ref<string | null>(null)

const templates = ref<PropertyTemplate[]>([])
const newProperty = ref<PropertyInput | null>(null)
const template = computed(() => templates.value.find((t) => t.type === newProperty.value?.type))
const openDeviations = computed(() => props.overview.deviations.filter((d) => !d.resolved))
const pastDeviations = computed(() => props.overview.deviations.filter((d) => d.resolved))

onMounted(async () => {
  try {
    templates.value = await api.templates()
  } catch (e) {
    error.value = (e as Error).message
  }
})

watch(() => props.overview.area, (area) => {
  level.value = area.autonomy
  visibility.value = area.transparency
})

function startProperty(type: string) {
  newProperty.value = type ? initialInputs([type as PropertyType], templates.value)[0] : null
}

async function run(action: () => Promise<unknown>) {
  busy.value = true
  error.value = null
  try {
    await action()
    reason.value = ''
    newProperty.value = null
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    // Also after an error: e.g. a late answer applies the default option on the server.
    await refresh()
    busy.value = false
  }
}

function saveProperty() {
  const problem = validate([newProperty.value!], templates.value)
  if (problem) {
    error.value = problem
    return
  }
  run(() => api.setAreaProperty(newProperty.value!))
}

const policyChanged = () => level.value !== props.overview.area.autonomy || visibility.value !== props.overview.area.transparency
</script>

<template>
  <h1>Operation</h1>
  <p v-if="error" class="error">{{ error }}</p>

  <section v-for="q in overview.openQuestions" :key="q.id" class="card question">
    <span class="tag">Question for you<template v-if="q.about"> · {{ q.about }}</template> · from {{ q.by }}</span>
    <h2>{{ q.question }}</h2>
    <p class="muted">
      If you do not answer by {{ dateTime(q.deadline!) }}, this applies:
      {{ q.options.find((o) => o.key === q.defaultOption)?.title }}.
    </p>
    <div class="options">
      <div v-for="o in q.options" :key="o.key" class="card option">
        <h3>{{ o.title }}</h3>
        <p>{{ o.description }}</p>
        <button :class="{ primary: o.key !== q.defaultOption }" :disabled="busy"
          @click="run(() => api.answer(q.id, o.key, rationale[q.id] ?? ''))">Choose</button>
      </div>
    </div>
    <label class="hint">Your rationale (optional) <input v-model="rationale[q.id]" /></label>
  </section>

  <template v-if="openDeviations.length">
    <h2>Deviations</h2>
    <ul class="log">
      <li v-for="d in openDeviations" :key="d.id" class="deviation">
        <span class="muted">{{ dateTime(d.detectedAt) }} · reported by {{ d.detectedBy }}</span>
        <strong>{{ d.propertyLabel }} {{ d.propertyRule }}: {{ deviationKind[d.kind].toLowerCase() }}</strong>
        <span>{{ d.description }}</span>
        <span class="muted small">{{ d.measureId ? 'The AI is handling it.' : 'No measure yet.' }}</span>
      </li>
    </ul>
  </template>

  <div class="heading">
    <h2>System properties</h2>
  </div>
  <p v-if="!overview.properties.length" class="muted">No properties set yet. Accept an option of a need, or set one below.</p>
  <div class="tiles">
    <div v-for="p in overview.properties" :key="p.id" class="card tile" :class="{ alert: p.openDeviation }">
      <span class="muted">{{ p.label }} · {{ p.bindingness.toLowerCase() }}</span>
      <strong>{{ p.rule }}</strong>
      <span v-if="p.openDeviation" class="chip action">{{ deviationKind[p.openDeviation] }}</span>
      <span v-if="p.openDeviationText">{{ p.openDeviationText }}</span>
      <span v-else-if="p.expected">Expected: {{ p.expected }}</span>
      <span class="muted small">Checked by: {{ p.measuredBy }}</span>
    </div>
  </div>

  <form class="card form add-property" @submit.prevent="saveProperty">
    <label>Set a system property for "{{ overview.area.name }}"
      <select :value="newProperty?.type ?? ''" @change="startProperty(($event.target as HTMLSelectElement).value)">
        <option value="">Choose…</option>
        <option v-for="t in templates" :key="t.type" :value="t.type">{{ t.label }}</option>
      </select>
    </label>
    <template v-if="newProperty && template">
      <label class="hint">{{ template.question }}
        <span v-if="template.isNumeric" class="value">
          <input type="number" step="any" v-model.number="newProperty.value" required
            :min="template.minValue ?? undefined" :max="template.maxValue ?? undefined" />
          <span>{{ template.unit }}</span>
        </span>
        <input v-else v-model="newProperty.statement" required />
      </label>
      <div class="binding">
        <label><input type="radio" name="binding-new" value="Must" v-model="newProperty.bindingness" /> must</label>
        <label><input type="radio" name="binding-new" value="Should" v-model="newProperty.bindingness" /> should</label>
        <span class="muted">Checked by: {{ template.measuredBy }}</span>
      </div>
      <div class="actions">
        <button type="submit" class="primary" :disabled="busy">Set property</button>
        <button type="button" class="quiet" @click="newProperty = null">Cancel</button>
      </div>
    </template>
  </form>

  <div class="layout">
    <section>
      <h2>Decision log</h2>
      <p v-if="!overview.decisions.length" class="muted">No decisions yet.</p>
      <ul class="log">
        <li v-for="d in overview.decisions" :key="d.id">
          <span class="muted">{{ dateTime(d.at) }} · {{ d.by === 'Setup' ? 'Setup' : decisionKind[d.kind] }} · {{ d.by }}<template v-if="d.about"> · {{ d.about }}</template></span>
          <strong>{{ d.chosen }}</strong>
          <span v-if="d.question" class="muted">Question: {{ d.question }}</span>
          <span v-if="d.rationale">{{ d.rationale }}</span>
        </li>
      </ul>
      <template v-if="pastDeviations.length">
        <h2>Resolved deviations</h2>
        <ul class="log">
          <li v-for="d in pastDeviations" :key="d.id">
            <span class="muted">{{ dateTime(d.detectedAt) }} – {{ dateTime(d.resolvedAt!) }} · {{ d.detectedBy }}</span>
            <strong>{{ d.propertyLabel }} {{ d.propertyRule }}: {{ deviationKind[d.kind].toLowerCase() }}</strong>
            <span>{{ d.description }}</span>
          </li>
        </ul>
      </template>
    </section>

    <aside>
      <form class="card form" @submit.prevent="run(() => api.changePolicy(level, visibility, reason))">
        <h3>Policy: {{ overview.area.name }}</h3>
        <label>Autonomy
          <select v-model="level">
            <option v-for="a in autonomy" :key="a.value" :value="a.value">{{ a.level }} – {{ a.text }}</option>
          </select>
        </label>
        <label>Transparency
          <select v-model="visibility">
            <option v-for="t in transparency" :key="t.value" :value="t.value">{{ t.level }} – {{ t.text }}</option>
          </select>
        </label>
        <template v-if="policyChanged()">
          <label>Why? <input v-model="reason" required /></label>
          <button type="submit" class="primary" :disabled="busy">Change policy</button>
        </template>
      </form>
      <div class="card">
        <h3>System map</h3>
        <ul class="map" v-if="overview.components.length">
          <li v-for="c in overview.components" :key="c.id">{{ c.name }} <span v-if="c.takenOver" class="muted">· existing</span></li>
        </ul>
        <p v-else class="muted">No building blocks yet.</p>
      </div>
    </aside>
  </div>
</template>
