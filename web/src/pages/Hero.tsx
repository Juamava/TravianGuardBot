import { useEffect, useState } from 'react'
import { Alert, Box, Button, LinearProgress, Stack, Typography } from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import StopIcon from '@mui/icons-material/Stop'
import { api, post, type HeroStatus } from '../api'
import { panel } from '../ui'

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ ...panel, flex: 1, minWidth: 0 }}>
      <Typography variant="overline" sx={{ color: '#949ba4', letterSpacing: '0.12em' }}>{label}</Typography>
      <Typography variant="h5" sx={{ color: '#f2f3f5', textTransform: 'none' }}>{value}</Typography>
    </Box>
  )
}

export function Hero({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
  const [data, setData] = useState<HeroStatus | null>(null)
  const [saving, setSaving] = useState(false)

  async function load() {
    const result = await api<HeroStatus>(`/api/accounts/${accountId}/hero`)
    setData(result.data)
  }

  useEffect(() => {
    load().catch((error: Error) => notify(error.message))
    const timer = window.setInterval(() => load().catch(() => undefined), 2000)
    return () => window.clearInterval(timer)
  }, [accountId])

  async function toggle() {
    if (!data) return
    setSaving(true)
    try {
      const result = await post(`/api/accounts/${accountId}/hero/leveling/${data.enabled ? 'stop' : 'start'}`)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    } finally {
      setSaving(false)
      await load().catch(() => undefined)
    }
  }

  const hp = data?.hpPercent ?? null
  const missingList = data != null && data.oasisCount <= 0

  return (
    <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
        <Typography variant="h4">Héroe</Typography>
        <Button
          variant="contained"
          color={data?.enabled ? 'error' : 'success'}
          startIcon={data?.enabled ? <StopIcon /> : <PlayArrowIcon />}
          onClick={toggle}
          disabled={!data || saving}
        >
          {data?.enabled ? 'Dejar de levear' : 'Poner a levear'}
        </Button>
      </Stack>

      {missingList && !data?.enabled && (
        <Alert severity="info">Primero creá en Farming una lista de vacas llamada "Oasis" y agregale oasis. El héroe solo caza en esos oasis.</Alert>
      )}

      <Stack direction="row" spacing={2}>
        <Stat label="Nivel" value={data?.level != null ? String(data.level) : '—'} />
        <Stat label="Vida" value={hp != null ? `${hp}%` : '—'} />
        <Stat label="Experiencia" value={data?.experience != null ? String(data.experience) : '—'} />
        <Stat label="Oasis en la lista" value={data ? (data.oasisCount < 0 ? 'Sin lista' : String(data.oasisCount)) : '—'} />
      </Stack>

      {hp != null && (
        <Box sx={panel}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Vida del héroe (va de aventura o a cazar desde {data?.minHpPercent}%)
          </Typography>
          <LinearProgress variant="determinate" value={Math.max(0, Math.min(100, hp))} color={hp >= (data?.minHpPercent ?? 50) ? 'success' : 'warning'} sx={{ height: 10, borderRadius: 5 }} />
        </Box>
      )}

      <Box sx={panel}>
        <Typography variant="overline" sx={{ color: '#949ba4', letterSpacing: '0.12em' }}>Estado</Typography>
        <Typography variant="h6" sx={{ color: '#f2f3f5', textTransform: 'none' }}>
          {data ? (data.enabled ? data.state || 'Activo' : 'Detenido') : 'Cargando…'}
          {data?.enabled && data.nextCheckAt ? ` · próxima revisión ${data.nextCheckAt}` : ''}
        </Typography>
        {data?.message && <Typography color="text.secondary" sx={{ mt: 1 }}>{data.message}</Typography>}
        {data?.updatedAt && <Typography variant="caption" color="text.secondary">Actualizado {data.updatedAt}</Typography>}
      </Box>

      <Box sx={panel}>
        <Typography variant="body2" color="text.secondary">
          Cómo funciona: si hay aventura y el héroe tiene {data?.minHpPercent ?? 50}% de vida o más, va de aventura. Con menos vida cobra las tareas (dan experiencia y al subir de nivel se cura) y si no alcanza espera a que se cure.
          Si no hay aventura, simula el atraco a los oasis de tu lista "Oasis" del más cerca al más lejos y solo manda al héroe solo cuando la simulación dice que sobrevive con al menos {data?.minHealthLeft ?? 25}% de vida y mata animales. Ante cualquier duda no lo manda.
        </Typography>
      </Box>
    </Stack>
  )
}
