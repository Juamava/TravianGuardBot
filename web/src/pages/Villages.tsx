import { useEffect, useState, type ReactNode } from 'react'
import { Box, Button, IconButton, MenuItem, Stack, TextField, Tooltip, Typography } from '@mui/material'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined'
import FileUploadOutlinedIcon from '@mui/icons-material/FileUploadOutlined'
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown'
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp'
import RefreshIcon from '@mui/icons-material/Refresh'
import VerticalAlignBottomIcon from '@mui/icons-material/VerticalAlignBottom'
import VerticalAlignTopIcon from '@mui/icons-material/VerticalAlignTop'
import { api, del, post, type Meta } from '../api'
import { Empty, panel, useUi } from '../ui'
import { VillageSettings } from './Settings'
import { Troops } from './Troops'

type Village = { id: number; name: string; x: number; y: number }
type Building = { id: number; location: number; type: string; name: string; level: number; queueLevel: number; jobLevel: number }
type Build = { buildings: Building[]; queue: { id: number; text: string }[]; jobs: { id: number; text: string }[] }
type Option = { type: string; name: string; maxLevel: number }

export type VillageTab = 'build' | 'troops' | 'trade'

const titles: Record<VillageTab, string> = { build: 'Construcción', troops: 'Tropas', trade: 'Comercio' }

export function Villages({ accountId, meta, notify, tab }: { accountId: number; meta: Meta | null; notify: (message: string) => void; tab: VillageTab }) {
  const ui = useUi()
  const [villages, setVillages] = useState<Village[]>([])
  const [villageId, setVillageId] = useState(0)
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
  useEffect(() => {
    setLocation(0)
    setOptions([])
    setType('')
  }, [villageId])

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

  const selected = build?.buildings.find(building => building.location === location)
  const fields = build?.buildings.filter(building => building.location <= 18) ?? []
  const spots = build?.buildings.filter(building => building.location > 18) ?? []

  return (
    <Stack spacing={1.5} sx={{ flex: 1, minHeight: 0 }}>
      <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
        <Typography variant="h4" sx={{ flexShrink: 0 }}>{titles[tab]}</Typography>
        {villages.length > 0 && (
          <TextField select size="small" label="Aldea" value={villageId || ''} onChange={event => setVillageId(Number(event.target.value))} sx={{ flex: '1 1 280px', maxWidth: 480, minWidth: 220 }}>
            {villages.map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
          </TextField>
        )}
      </Stack>

      {villages.length === 0 && <Empty title="Todavía no hay aldeas" text="Conectá la cuenta. Cuando el juego cargue las aldeas, van a aparecer acá." />}

      {villages.length > 0 && (
        <Box sx={{ flex: 1, minHeight: 0, overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {tab === 'build' && build && (
            <>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 1 }}>
            <ReloadTile icon={<RefreshIcon />} label="Esta aldea" hint="Vuelve a leer los edificios de la aldea seleccionada." onClick={() => reload(`/api/accounts/${accountId}/villages/${villageId}/reload`)} />
            <ReloadTile icon={<RefreshIcon />} label="Incompletas" hint="Lee las aldeas a las que todavía les faltan edificios." onClick={() => reload(`/api/accounts/${accountId}/villages/reload-missing`)} />
            <ReloadTile icon={<RefreshIcon />} label="Todas" hint="Vuelve a leer los edificios de todas las aldeas." onClick={() => reload(`/api/accounts/${accountId}/villages/reload-all`)} />
          </Box>

          <Box sx={{ minHeight: 520, display: 'grid', gap: 1.5, alignItems: 'stretch', gridTemplateColumns: 'minmax(240px, 1.15fr) minmax(250px, 320px) minmax(280px, 1fr)', '@media (max-width: 1100px)': { gridTemplateColumns: '1fr' } }}>
            <Stack sx={{ ...panel, p: 1, minHeight: 0, height: '100%', overflow: 'hidden' }}>
              <Typography sx={{ px: 1, py: 0.75, fontWeight: 700 }}>Edificios</Typography>
              <Box sx={{ flex: 1, overflow: 'auto', minHeight: 0, pr: 0.5 }}>
                <BuildingGroup title="Campos" buildings={fields} location={location} onChoose={choose} />
                <BuildingGroup title="Pueblo" buildings={spots} location={location} onChoose={choose} />
              </Box>
            </Stack>

            <Stack spacing={1.5} sx={{ minHeight: 0, height: '100%', overflow: 'auto' }}>
              <Stack spacing={1.5} sx={{ ...panel, p: 2 }}>
                <Box>
                  <Typography sx={{ fontWeight: 700, fontSize: 16 }}>{selected ? selected.name : 'Elegí un edificio'}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {selected ? `Casillero ${selected.location} · nivel ${selected.level}` : 'Hacé click en un edificio de la lista.'}
                  </Typography>
                </Box>
                <TextField select size="small" label="Qué construir" value={type} disabled={!location} onChange={event => setType(event.target.value)}>
                  {options.map(option => <MenuItem key={option.type} value={option.type}>{option.name}</MenuItem>)}
                </TextField>
                <TextField size="small" type="number" label="Hasta el nivel" value={level} disabled={!location} onChange={event => setLevel(Number(event.target.value))} />
                <Button variant="contained" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/normal`, { location, type, level }))}>Agregar trabajo</Button>
                <Stack direction="row" spacing={1}>
                  <Button fullWidth variant="outlined" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/upgrade`, { location, max: false }))}>Subir un nivel</Button>
                  <Button fullWidth variant="outlined" color="warning" disabled={!location} onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/upgrade`, { location, max: true }))}>Al máximo</Button>
                </Stack>
              </Stack>

              <Stack spacing={1.5} sx={{ ...panel, p: 2 }}>
                <Box>
                  <Typography sx={{ fontWeight: 700, fontSize: 16 }}>Campos de recursos</Typography>
                  <Typography variant="caption" color="text.secondary">Arma trabajos para todos los campos de esta aldea.</Typography>
                </Box>
                <TextField select size="small" label="Plan" value={plan} onChange={event => setPlan(event.target.value)}>
                  {(meta?.resourcePlans ?? []).map(item => <MenuItem key={item.value} value={item.value}>{item.name}</MenuItem>)}
                </TextField>
                <TextField size="small" type="number" label="Nivel" value={resourceLevel} onChange={event => setResourceLevel(Number(event.target.value))} />
                <Button variant="outlined" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/resource`, { plan, level: resourceLevel }))}>Agregar campos</Button>
              </Stack>
            </Stack>

            <Stack spacing={1.5} sx={{ minHeight: 0, height: '100%', overflow: 'hidden' }}>
              <Stack sx={{ ...panel, p: 1, maxHeight: '34%', minHeight: 112, overflow: 'hidden' }}>
                <Typography sx={{ px: 1, py: 0.75, fontWeight: 700 }}>En construcción</Typography>
                <Box sx={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
                  {build.queue.length === 0 && <Typography sx={{ px: 1, py: 0.5, color: '#5c4529', fontSize: 13 }}>Nada construyéndose.</Typography>}
                  {build.queue.map(item => (
                    <Typography key={item.id} sx={{ px: 1, py: 0.6, fontSize: 14 }}>{item.text}</Typography>
                  ))}
                </Box>
              </Stack>

              <Stack sx={{ ...panel, p: 1, flex: 1, minHeight: 0, overflow: 'hidden' }}>
                <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 0.5, pb: 0.5 }}>
                  <Typography sx={{ fontWeight: 700 }}>Trabajos</Typography>
                  <Stack direction="row" spacing={0.25}>
                    <Tooltip title="Importar">
                      <IconButton component="label" size="small" sx={{ color: '#3b2a17' }}>
                        <FileUploadOutlinedIcon fontSize="small" />
                        <input hidden type="file" accept="application/json" onChange={async event => {
                          const file = event.target.files?.[0]
                          event.target.value = ''
                          if (!file) return
                          try {
                            const jobs = JSON.parse(await file.text())
                            const shuffle = await ui.confirm('¿Mezclar la ubicación de los edificios? Si cancelás, se importan en el orden del archivo.')
                            await run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/import`, { jobs, shuffle }))
                          } catch (error) {
                            notify(error instanceof Error ? error.message : 'El archivo no es un JSON válido')
                          }
                        }} />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Exportar">
                      <IconButton size="small" sx={{ color: '#3b2a17' }} onClick={async () => {
                        const result = await api<{ type: string; content: string }[]>(`/api/accounts/${accountId}/villages/${villageId}/jobs/export`)
                        const blob = new Blob([JSON.stringify(result.data, null, 2)], { type: 'application/json' })
                        const link = document.createElement('a')
                        link.href = URL.createObjectURL(blob)
                        link.download = 'trabajos.json'
                        link.click()
                      }}>
                        <FileDownloadOutlinedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Borrar todos">
                      <IconButton size="small" sx={{ color: '#b8281d' }} onClick={async () => { if (await ui.confirm('¿Borrar todos los trabajos de esta aldea?')) run(() => del(`/api/accounts/${accountId}/villages/${villageId}/jobs`)) }}>
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </Stack>
                <Box sx={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
                  {build.jobs.length === 0 && <Typography sx={{ px: 1, py: 0.5, color: '#5c4529', fontSize: 13 }}>Todavía no hay trabajos. Elegí un edificio y agregalo.</Typography>}
                  {build.jobs.map(job => (
                    <Stack key={job.id} direction="row" spacing={0.5} alignItems="center" sx={{ px: 1, py: 0.4, borderRadius: 1, '&:hover': { bgcolor: '#f1e5c2' } }}>
                      <Typography sx={{ flex: 1, fontSize: 14, overflowWrap: 'anywhere' }}>{job.text}</Typography>
                      <MoveButton title="Al inicio" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}/move`, { direction: 'top' }))}><VerticalAlignTopIcon fontSize="small" /></MoveButton>
                      <MoveButton title="Subir" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}/move`, { direction: 'up' }))}><KeyboardArrowUpIcon fontSize="small" /></MoveButton>
                      <MoveButton title="Bajar" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}/move`, { direction: 'down' }))}><KeyboardArrowDownIcon fontSize="small" /></MoveButton>
                      <MoveButton title="Al final" onClick={() => run(() => post(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}/move`, { direction: 'bottom' }))}><VerticalAlignBottomIcon fontSize="small" /></MoveButton>
                      <MoveButton title="Borrar" tone="danger" onClick={async () => { if (await ui.confirm('¿Borrar este trabajo?')) run(() => del(`/api/accounts/${accountId}/villages/${villageId}/jobs/${job.id}`)) }}><DeleteOutlineIcon fontSize="small" /></MoveButton>
                    </Stack>
                  ))}
                </Box>
              </Stack>
            </Stack>
          </Box>
            </>
          )}
          {villageId > 0 && tab === 'troops' && <Troops key={villageId} accountId={accountId} villageId={villageId} meta={meta} notify={notify} />}
          {villageId > 0 && tab !== 'troops' && (
            <VillageSettings accountId={accountId} villageId={villageId} meta={meta} notify={notify} section={tab} />
          )}
        </Box>
      )}
    </Stack>
  )
}

function ReloadTile({ icon, label, hint, onClick }: { icon: ReactNode; label: string; hint: string; onClick: () => void }) {
  return (
    <Tooltip title={hint}>
      <Button onClick={onClick} sx={{ height: 48, color: '#3b2a17', bgcolor: '#fdf9ec', display: 'flex', gap: 1, '&:hover': { bgcolor: '#f1e5c2' } }}>
        <Box sx={{ color: '#4f7a12', display: 'flex' }}>{icon}</Box>
        {label}
      </Button>
    </Tooltip>
  )
}

function BuildingGroup({ title, buildings, location, onChoose }: { title: string; buildings: Building[]; location: number; onChoose: (location: number) => void }) {
  if (buildings.length === 0) return null
  return (
    <Box sx={{ mb: 1 }}>
      <Typography sx={{ position: 'sticky', top: 0, zIndex: 1, px: 1, py: 0.5, bgcolor: '#fdf9ec', color: '#5c4529', fontSize: 13, fontWeight: 700 }}>{title}</Typography>
      {buildings.map(building => {
        const selected = location === building.location
        return (
          <Box key={building.id} onClick={() => onChoose(building.location)} sx={{ display: 'grid', gridTemplateColumns: '28px minmax(0, 1fr) auto', gap: 1, alignItems: 'center', px: 1, py: 0.8, borderRadius: 1, cursor: 'pointer', bgcolor: selected ? '#e2cf9a' : 'transparent', boxShadow: selected ? 'inset 3px 0 0 #4f7a12' : 'none', '&:hover': { bgcolor: selected ? '#e2cf9a' : '#f1e5c2' } }}>
            <Typography sx={{ color: '#5c4529', fontSize: 13 }}>{building.location}</Typography>
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontWeight: 600, fontSize: 14, overflowWrap: 'anywhere' }}>{building.name}</Typography>
              {(building.queueLevel > 0 || building.jobLevel > building.level) && (
                <Typography sx={{ fontSize: 13, color: '#4a3820' }}>
                  {building.queueLevel > 0 && <Box component="span" sx={{ color: '#a86f08' }}>cola {building.queueLevel}</Box>}
                  {building.queueLevel > 0 && building.jobLevel > building.level && ' · '}
                  {building.jobLevel > building.level && <Box component="span" sx={{ color: '#4f7a12' }}>plan {building.jobLevel}</Box>}
                </Typography>
              )}
            </Box>
            <Typography sx={{ fontWeight: 700, fontSize: 15 }}>{building.level}</Typography>
          </Box>
        )
      })}
    </Box>
  )
}

function MoveButton({ title, tone = 'normal', onClick, children }: { title: string; tone?: 'normal' | 'danger'; onClick: () => void; children: ReactNode }) {
  return (
    <Tooltip title={title}>
      <IconButton size="small" onClick={onClick} sx={{ color: tone === 'danger' ? '#b8281d' : '#4a3820' }}>
        {children}
      </IconButton>
    </Tooltip>
  )
}
