<script setup lang="ts">
import { onMounted, provide, ref } from 'vue'
import { api, type Overview } from './api'
import { route } from './router'
import NeedsView from './views/NeedsView.vue'
import NeedView from './views/NeedView.vue'
import OperationView from './views/OperationView.vue'

const overview = ref<Overview | null>(null)
const error = ref<string | null>(null)

async function refresh() {
  try {
    overview.value = await api.overview()
    error.value = null
  } catch (e) {
    error.value = (e as Error).message
  }
}

provide('refresh', refresh)
onMounted(refresh)
</script>

<template>
  <header class="top">
    <div class="top-inner">
      <a href="#/" class="brand">Remit</a>
      <span class="system" v-if="overview">{{ overview.systemName }}</span>
      <nav>
        <a href="#/" :class="{ active: route.name !== 'operation' }">Needs</a>
        <a href="#/operation" :class="{ active: route.name === 'operation' }">
          Operation
          <span v-if="overview?.openQuestions.length" class="badge">{{ overview.openQuestions.length }}</span>
        </a>
      </nav>
    </div>
  </header>

  <main>
    <p v-if="error" class="error">{{ error }}</p>
    <template v-if="overview">
      <NeedsView v-if="route.name === 'needs'" :overview="overview" />
      <NeedView v-else-if="route.name === 'need'" :key="route.id" :id="route.id" :overview="overview" />
      <OperationView v-else :overview="overview" />
    </template>
  </main>

  <footer v-if="overview">
    Remit {{ overview.version }} · Preview: analysis and building of needs are simulated; a connected operations
    agent works for real. Data is kept only until the next restart.
  </footer>
</template>
