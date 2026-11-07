import { useEffect, useRef, useState } from 'react'
import { Alert, Box, LinearProgress, Stack, Switch, Typography } from '@mui/material'
import { api, put, type HeroStatus } from '../api'
import { panel } from '../ui'

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ ...panel, flex: 1, minWidth: 0 }}>
      <Typography variant="overline" sx={{ color: '#5c4529', letterSpacing: '0.12em' }}>{label}</Typography>
      <Typography variant="h5" sx={{ color: '#3b2a17', textTransform: 'none' }}>{value}</Typography>
    </Box>
  )
}

export function Hero({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
  const [data, setData] = useState<HeroStatus | null>(null)
  const [adventures, setAdventures] = useState(false)
  const [ready, setReady] = useState(false)
  const settings = useRef<Record<string, number>>({})

  async function load() {
    const result = await api<HeroStatus>(`/api/accounts/${accountId}/hero`)
    setData(result.data)
  }

  useEffect(() => {
    load().catch((error: Error) => notify(error.message))
    const timer = window.setInterval(() => load().catch(() => undefined), 3000)
    return () => window.clearInterval(timer)
  }, [accountId])

  useEffect(() => {
    let alive = true
    setReady(false)
    api<Record<string, number>>(`/api/accounts/${accountId}/settings`).then(result => {
      if (!alive) return
      settings.current = result.data
      setAdventures(result.data.EnableAutoStartAdventure === 1)
      setReady(true)
    }).catch((error: Error) => { if (alive) notify(error.message) })
    return () => { alive = false }
  }, [accountId])

  async function toggleAdventures(on: boolean) {
    const previous = settings.current
    const next = { ...previous, EnableAutoStartAdventure: on ? 1 : 0 }
    settings.current = next
    setAdventures(on)
    try {
      const result = await put(`/api/accounts/${accountId}/settings`, next)
      notify(result.message)
    } catch (error) {
      settings.current = previous
      setAdventures(previous.EnableAutoStartAdventure === 1)
      notify(error instanceof Error ? error.message : 'No se pudo guardar')
    }
  }

  const hp = data?.hpPercent ?? null
  const minHp = data?.minHpPercent ?? 50
  const lists = data?.heroLists ?? []

  return (
    <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Typography variant="h4">Héroe</Typography>

      <Stack direction="row" spacing={1} alignItems="center" sx={{ ...panel, py: 1 }}>
        <Switch checked={adventures} disabled={!ready} onChange={event => { void toggleAdventures(event.target.checked) }} />
        <Box sx={{ minWidth: 0 }}>
          <Typography fontWeight={600}>Empezar aventuras</Typography>
          <Typography variant="body2" color="text.secondary">Manda al héroe a las aventuras que aparezcan.</Typography>
        </Box>
      </Stack>

      {data && lists.length === 0 && (
        <Alert severity="info">Activá "Levear héroe" en una lista de Farming. El héroe caza en los oasis con animales de esa lista mientras corre el raid.</Alert>
      )}
      {lists.length > 0 && (
        <Alert severity="success">Levea con {lists.length === 1 ? 'la lista' : 'las listas'}: {lists.join(', ')}.</Alert>
      )}

      <Stack direction="row" spacing={2}>
        <Stat label="Nivel" value={data?.level != null ? String(data.level) : '—'} />
        <Stat label="Vida" value={hp != null ? `${hp}%` : '—'} />
        <Stat label="Experiencia" value={data?.experience != null ? String(data.experience) : '—'} />
      </Stack>

      {hp != null && (
        <Box sx={panel}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Vida del héroe (sale a cazar desde {minHp}%){data?.statsAt ? ` · leída ${data.statsAt}` : ''}
          </Typography>
          <LinearProgress variant="determinate" value={Math.max(0, Math.min(100, hp))} color={hp >= minHp ? 'success' : 'warning'} sx={{ height: 10, borderRadius: 5 }} />
        </Box>
      )}

      <Box sx={panel}>
        <Typography variant="overline" sx={{ color: '#5c4529', letterSpacing: '0.12em' }}>Última acción</Typography>
        <Typography variant="h6" sx={{ color: '#3b2a17', textTransform: 'none' }}>{data ? (data.state || 'Todavía nada') : 'Cargando…'}</Typography>
        {data?.message && <Typography color="text.secondary" sx={{ mt: 1 }}>{data.message}</Typography>}
        {data?.updatedAt && <Typography variant="caption" color="text.secondary">{data.updatedAt}</Typography>}
      </Box>

      <Box sx={panel}>
        <Typography variant="body2" color="text.secondary">
          Cómo funciona: con "Levear héroe" activado en una lista de Farming, cada vez que el raid encuentra un oasis con animales simula el atraco con el héroe solo y lo manda únicamente si sobrevive con al menos {data?.minHealthLeft ?? 25}% de vida y mata animales. Sale una vez por vuelta de la lista.
          Si tiene menos de {minHp}% de vida, primero cobra tareas de a una (dan experiencia y al subir de nivel se cura) y guarda el resto. Ante cualquier duda no lo manda.
        </Typography>
      </Box>
    </Stack>
  )
}
