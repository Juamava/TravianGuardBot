import { useEffect, useState } from 'react'
import { Button, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material'
import { api, del, post, type Meta } from '../api'
import { VillageSettings } from './Settings'

type Village = { id: number; name: string; x: number; y: number }
type Building = { id: number; location: number; type: string; name: string; level: number; queueLevel: number; jobLevel: number }
type Build = { buildings: Building[]; queue: { id: number; text: string }[]; jobs: { id: number; text: string }[] }
type Option = { type: string; name: string; maxLevel: number }

export function Villages({ accountId, meta, notify }: { accountId: number; meta: Meta | null; notify: (message: string) => void }) {
  const [villages, setVillages] = useState<Village[]>([])
  const [villageId, setVillageId] = useState(0)
  const [tab, setTab] = useState('build')
  const [build, setBuild] = useState<Build | null>(null)
  const [location, setLocation] = useState(0)
  const [options, setOptions] = useState<Option[]>([])
  const [type, setType] = useState('')
  const [level, setLevel] = useState(1)
  const [plan, setPlan] = useState('AllResources')
  const [resourceLevel, setResourceLevel] = useState(10)

  async function loadVillages() {
    const result = await api<Village[]>(`/api/accounts/${accountId}/villages`)
    setVillages(result.data)
    setVillageId(current => result.data.some(village => village.id === current) ? current : (result.data[0]?.id ?? 0))
  }

  async function loadBuild(id: number) {
    if (!id) return
    const result = await api<Build>(`/api/accounts/${accountId}/villages/${id}/build`)
    setBuild(result.data)
  }

  useEffect(() => { loadVillages().catch((error: Error) => notify(error.message)) }, [accountId])
  useEffect(() => { if (villageId) loadBuild(villageId).catch((error: Error) => notify(error.message)) }, [villageId])

  async function choose(next: number) {
    setLocation(next)
    const result = await api<Option[]>(`/api/accounts/${accountId}/villages/${villageId}/build/options?location=${next}`)
    setOptions(result.data)
    const first = result.data[0]
    if (first) {
      setType(first.type)
      setLevel(first.maxLevel)
    }
  }

  async function run(action: () => Promise<{ message: string }>) {
    try {
      const result = await action()
      notify(result.message)
      await loadBuild(villageId)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  async function reload(path: string) {
    try {
      const result = await post(path)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4">Aldeas</Typography>
      <Stack direction="row" spacing={1} flexWrap="wrap">
        <Button variant="outlined" onClick={() => reload(`/api/accounts/${accountId}/villages/${villageId}/reload`)}>Recargar esta</Button>
        <Button variant="outlined" onClick={() => reload(`/api/accounts/${accountId}/villages/reload-missing`)}>Recargar incompletas</Button>
        <Button variant="outlined" onClick={() => reload(`/api/accounts/${accountId}/villages/reload-all`)}>Recargar todas</Button>
      </Stack>
      <TextField select label="Aldea" value={villageId || ''} onChange={event => setVillageId(Number(event.target.value))}>
        {villages.map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
      </TextField>
      <ToggleButtonGroup exclusive value={tab} onChange={(_, value) => value && setTab(value)}>
        <ToggleButton value="build">Construir</ToggleButton>
        <ToggleButton value="settings">Ajustes</ToggleButton>
      </ToggleButtonGroup>
      {tab === 'settings' && villageId > 0 && <VillageSettings accountId={accountId} villageId={villageId} meta={meta} notify={notify} />}
      {tab === 'build' && build && (
        <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2} alignItems="flex-start">
          <Stack spacing={1} sx={{ flex: 1, minWidth: 260 }}>
            <Typography fontWeight={800}>Edificios</Typography>
            {build.buildings.map(building => (
              <Button key={building.id} variant={location === building.location ? 'contained' : 'text'} onClick={() => choose(building.location)} sx={{ justifyContent: 'flex-start' }}>
                [{building.location}] {building.name} · {building.level}{building.queueLevel ? ` → (${building.queueLevel})` : ''}{building.jobLevel > building.level ? ` → [${building.jobLevel}]` : ''}
              </Button>
            ))}
            <Typography fontWeight={800}>Cola</Typography>
            {build.queue.map(item => <Typography key={item.id}>{item.text}</Typography>)}
          </Stack>
          <Stack spacing={1.5} sx={{ flex: 1, minWidth: 240 }}>
            <TextField select label="Edificio" value={type} onChange={event => setType(event.target.value)}>
              {options.map(option => <MenuItem key={option.type} value={option.type}>{option.name}</MenuItem>)}
            </TextField>
            <TextField type="number" label="Nivel" value={level} onChange={event => setLevel(Number(event.target.value))} />
            <Button variant="contained" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/normal`, { location, type, level }))}>Agregar edificio</Button>
            <Button variant="contained" color="secondary" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/upgrade`, { location, max: false }))}>Subir un nivel</Button>
            <Button variant="contained" color="warning" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/upgrade`, { location, max: true }))}>Subir al máximo</Button>
            <TextField select label="Campos" value={plan} onChange={event => setPlan(event.target.value)}>
              {(meta?.resourcePlans ?? []).map(item => <MenuItem key={item.value} value={item.value}>{item.name}</MenuItem>)}
            </TextField>
            <TextField type="number" label="Nivel de recursos" value={resourceLevel} onChange={event => setResourceLevel(Number(event.target.value))} />
            <Button variant="outlined" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/resource`, { plan, level: resourceLevel }))}>Agregar recursos</Button>
          </Stack>
          <Stack spacing={1} sx={{ flex: 1, minWidth: 240 }}>
            <Typography fontWeight={800}>Trabajos</Typography>
            <Stack direction="row" spacing={1}>
              <Button component="label" size="small">Importar
                <input hidden type="file" accept="application/json" onChange={async event => {
                  const file = event.target.files?.[0]
                  if (!file) return
                  const jobs = JSON.parse(await file.text())
                  const shuffle = window.confirm('¿Mezclar la ubicación de los edificios?')
                  await run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/import`, { jobs, shuffle }))
                }} />
              </Button>
              <Button size="small" onClick={async () => {
                const result = await api<{ type: string; content: string }[]>(`/api/accounts/${accountId}/villages/${villageId}/jobs/export`)
                const blob = new Blob([JSON.stringify(result.data, null, 2)], { type: 'application/json' })
                const link = document.createElement('a')
                link.href = URL.createObjectURL(blob)
                link.download = 'trabajos.json'
                link.click()
              }}>Exportar</Button>
              <Button size="small" color="error" onClick={() => run(() => del(`/api/accounts/${accountId}/villages/${villageId}/jobs`))}>Borrar todos</Button>
            </Stack>
            {build.jobs.map(job => (
              <Stack key={job.id} direction="row" spacing={0.5} alignItems="center">
                <Typography sx={{ flex: 1 }}>{job.text}</Typography>
                {(['top', 'up', 'down', 'bottom'] as const).map(direction => (
                  <Button key={direction} size="small" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}/move`, { direction }))}>{direction}</Button>
                ))}
                <Button size="small" color="error" onClick={() => run(() => del(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}`))}>x</Button>
              </Stack>
            ))}
          </Stack>
        </Stack>
      )}
    </Stack>
  )
}
