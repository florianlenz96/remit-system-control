import { ref } from 'vue'

/** Minimal hash routing: #/ (needs), #/needs/N-001, #/operation. */
export type Route = { name: 'needs' } | { name: 'need'; id: string } | { name: 'operation' }

function parse(hash: string): Route {
  const path = hash.replace(/^#/, '')
  const need = path.match(/^\/needs\/([\w-]+)$/)
  if (need) return { name: 'need', id: need[1] }
  if (path === '/operation') return { name: 'operation' }
  return { name: 'needs' }
}

export const route = ref<Route>(parse(window.location.hash))
window.addEventListener('hashchange', () => {
  route.value = parse(window.location.hash)
  window.scrollTo(0, 0)
})

export function go(path: string) {
  window.location.hash = path
}
