<script setup lang="ts">
import { inject, ref } from 'vue'
import { api, type Overview, type Priority } from '../api'
import { needStatus, statusTone } from '../labels'
import { go } from '../router'

defineProps<{ overview: Overview }>()
const refresh = inject<() => Promise<void>>('refresh')!

const showForm = ref(false)
const title = ref('')
const description = ref('')
const priority = ref<Priority>('Medium')
const busy = ref(false)
const error = ref<string | null>(null)

async function submit() {
  busy.value = true
  error.value = null
  try {
    const { id } = await api.registerNeed(title.value, description.value, priority.value)
    await refresh()
    go(`/needs/${id}`)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="layout">
    <section>
      <div class="heading">
        <h1>Needs</h1>
        <button v-if="!showForm" class="primary" @click="showForm = true">New need</button>
      </div>

      <form v-if="showForm" class="card form" @submit.prevent="submit">
        <h2>What does someone need from the system?</h2>
        <p class="hint">Describe the need in your own words – not the solution. The AI analyses it and proposes options.</p>
        <label>Title <input v-model="title" required placeholder="e.g. Search results appear too slowly" /></label>
        <label>Description
          <textarea v-model="description" required rows="4" placeholder="Who needs it, when does it hurt, what would be better?" />
        </label>
        <label>Priority
          <select v-model="priority">
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </label>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="actions">
          <button type="submit" class="primary" :disabled="busy">Submit need</button>
          <button type="button" @click="showForm = false">Cancel</button>
        </div>
      </form>

      <ul class="list">
        <li v-for="need in overview.needs" :key="need.id" class="card">
          <a class="row" :href="`#/needs/${need.id}`">
            <div>
              <span class="muted">{{ need.id }}</span>
              <strong>{{ need.title }}</strong>
            </div>
            <span class="chip" :class="statusTone(need.status)">{{ needStatus[need.status] }}</span>
          </a>
        </li>
      </ul>
    </section>

    <aside>
      <div class="card" v-if="overview.openQuestions.length">
        <h3>Questions for you</h3>
        <p>{{ overview.openQuestions.length }} decision(s) the AI may not make itself.</p>
        <a href="#/operation">Answer in Operation →</a>
      </div>
      <div class="card">
        <h3>{{ overview.area.name }}</h3>
        <p class="muted">Area of the {{ overview.systemName.toLowerCase() }}</p>
        <h4>System map</h4>
        <ul class="map" v-if="overview.components.length">
          <li v-for="c in overview.components" :key="c.id">{{ c.name }}</li>
        </ul>
        <p v-else class="muted">No building blocks created by the AI yet.</p>
      </div>
    </aside>
  </div>
</template>
