<script setup lang="ts">
import { inject, ref, watch } from 'vue'
import { api, type AutonomyLevel, type Overview, type TransparencyLevel } from '../api'
import { autonomy, dateTime, decisionKind, transparency } from '../labels'

const props = defineProps<{ overview: Overview }>()
const refresh = inject<() => Promise<void>>('refresh')!

const level = ref<AutonomyLevel>(props.overview.area.autonomy)
const visibility = ref<TransparencyLevel>(props.overview.area.transparency)
const reason = ref('')
const rationale = ref<Record<string, string>>({})
const busy = ref(false)
const error = ref<string | null>(null)

watch(() => props.overview.area, (area) => {
  level.value = area.autonomy
  visibility.value = area.transparency
})

async function run(action: () => Promise<void>) {
  busy.value = true
  error.value = null
  try {
    await action()
    reason.value = ''
    await refresh()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

const policyChanged = () => level.value !== props.overview.area.autonomy || visibility.value !== props.overview.area.transparency
</script>

<template>
  <h1>Operation</h1>
  <p v-if="error" class="error">{{ error }}</p>

  <section v-for="q in overview.openQuestions" :key="q.id" class="card question">
    <span class="tag">Question for you · {{ q.needTitle }}</span>
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

  <h2>System properties</h2>
  <p v-if="!overview.properties.length" class="muted">No properties set yet. Accept an option of a need to set them.</p>
  <div class="tiles">
    <div v-for="p in overview.properties" :key="p.id" class="card tile">
      <span class="muted">{{ p.label }} · {{ p.bindingness.toLowerCase() }}</span>
      <strong>{{ p.rule }}</strong>
      <span v-if="p.expected">Expected: {{ p.expected }}</span>
      <span class="muted small">Not measured yet · will be checked by: {{ p.measuredBy }}</span>
    </div>
  </div>

  <div class="layout">
    <section>
      <h2>Decision log</h2>
      <p v-if="!overview.decisions.length" class="muted">No decisions yet.</p>
      <ul class="log">
        <li v-for="d in overview.decisions" :key="d.id">
          <span class="muted">{{ dateTime(d.at) }} · {{ decisionKind[d.kind] }} · {{ d.by }}<template v-if="d.needTitle"> · {{ d.needTitle }}</template></span>
          <strong>{{ d.chosen }}</strong>
          <span v-if="d.question" class="muted">Question: {{ d.question }}</span>
          <span v-if="d.rationale">{{ d.rationale }}</span>
        </li>
      </ul>
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
          <li v-for="c in overview.components" :key="c.id">{{ c.name }}</li>
        </ul>
        <p v-else class="muted">No building blocks created by the AI yet.</p>
      </div>
    </aside>
  </div>
</template>
