import { useEffect, useMemo, useState } from 'react'
import { Box, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, ToggleButton, ToggleButtonGroup, Tooltip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import PlaylistAddIcon from '@mui/icons-material/PlaylistAdd'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import StopIcon from '@mui/icons-material/Stop'
import { api, del, post, put } from '../api'
import { Empty, panel, useUi } from '../ui'

type Village = { id: number; name: string; x: number; y: number }
type ScoutList = { id: number; name: string; minMinutes: number; repeatMinutes: number; autoStart: boolean; fullThreshold: number; running: boolean; countdown: string }
type Target = {
  id: number
  listId: number
  name: string
  url: string
  troops: string
  status: string
  owner: string
  tribe: string
  alliance: string
  population: number
  distance: number
  x: number | null
  y: number | null
  pending: boolean
  scoutedAt: string | null
  scoutClass: string
  danger: number
  label: string
  wood: number
  clay: number
  iron: number
  crop: number
  cranny: number
  defenders: number
  scoutsDied: boolean
  playerPopulation: number
  playerVillages: number
  attackPoints: number
  defensePoints: number
  heroLevel: number
}
type OwnList = { id: number; name: string; sameVillage: boolean }
type ScoutingData = { villages: Village[]; lists: ScoutList[]; targets: Target[]; ownLists: OwnList[]; scoutPlan: string; server: string }
type Filter = 'todas' | 'gorda' | 'flaca' | 'peligro' | 'sin'

const columns = '34px minmax(150px, 1.4fr) 120px minmax(150px, 1fr) 70px 64px 110px 120px 34px'

function Hint({ text }: { text: string }) {
  return (
    <Tooltip title={text} placement="top" arrow enterDelay={200} slotProps={{ tooltip: { sx: { maxWidth: 300, fontSize: 13, lineHeight: 1.45 } } }}>
      <InfoOutlinedIcon sx={{ fontSize: 16, color: '#949ba4', cursor: 'help' }} />
    </Tooltip>
  )
}

function classColor(target: Target) {
  if (target.scoutClass === 'gorda') return { bg: '#1f3b2b', fg: '#3ba55d' }
  if (target.scoutClass === 'flaca') return { bg: '#3d3420', fg: '#f0b232' }
  if (target.scoutClass === 'peligro') {
    const strong = Math.min(1, Math.max(0, (target.danger - 1) / 9))
    return { bg: `rgba(237,66,69,${0.15 + strong * 0.35})`, fg: strong > 0.4 ? '#fff' : '#f2a3a4' }
  }
  return { bg: '#35373c', fg: '#b5bac1' }
}

function number(value: number) {
  return value < 0 ? '—' : value.toLocaleString()
}

function ago(value: string | null) {
  if (!value) return ''
  const minutes = Math.floor((Date.now() - new Date(value).getTime()) / 60000)
  if (!Number.isFinite(minutes) || minutes < 0) return ''
  if (minutes < 60) return `hace ${minutes} min`
  if (minutes < 60 * 48) return `hace ${Math.floor(minutes / 60)} h`
  return `hace ${Math.floor(minutes / 1440)} días`
}

function toUrl(line: string, server: string) {
  const text = line.trim()
  if (!text) return ''
  if (/^https?:\/\//i.test(text)) return text
  const match = text.replace(/[()‭‬‪‫−]/g, value => (value === '−' ? '-' : '')).match(/(-?\d+)\s*[|,; ]\s*(-?\d+)/)
  if (!match || !server) return ''
  return `${server.replace(/\/$/, '')}/karte.php?x=${match[1]}&y=${match[2]}`
}

export function Scouting({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
  const ui = useUi()
  const [villageId, setVillageId] = useState(0)
  const [data, setData] = useState<ScoutingData | null>(null)
  const [listId, setListId] = useState(0)
  const [filter, setFilter] = useState<Filter>('todas')
  const [selected, setSelected] = useState<number[]>([])
  const [draft, setDraft] = useState<{ minMinutes: string; repeatMinutes: string; fullThreshold: string; autoStart: boolean } | null>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [addText, setAddText] = useState('')
  const [addTroops, setAddTroops] = useState('')
  const [adding, setAdding] = useState(false)
  const [promoteOpen, setPromoteOpen] = useState(false)
  const [promoteList, setPromoteList] = useState(0)
  const [promoteTroops, setPromoteTroops] = useState('A:5')

  async function load(village = villageId) {
    const result = await api<ScoutingData>(`/api/accounts/${accountId}/scouting?villageId=${village}`)
    setData(result.data)
    if (!village && result.data.villages[0]) setVillageId(result.data.villages[0].id)
    setListId(current => result.data.lists.some(list => list.id === current) ? current : (result.data.lists[0]?.id ?? 0))
    return result.data
  }

  useEffect(() => { load(0).catch((error: Error) => notify(error.message)) }, [accountId])
  useEffect(() => {
    if (!villageId) return
    load(villageId).catch((error: Error) => notify(error.message))
    const timer = window.setInterval(() => load(villageId).catch(() => undefined), 4000)
    return () => window.clearInterval(timer)
  }, [villageId])

  const list = data?.lists.find(item => item.id === listId) ?? null
  useEffect(() => {
    setDraft(list ? { minMinutes: String(list.minMinutes), repeatMinutes: String(list.repeatMinutes), fullThreshold: String(list.fullThreshold), autoStart: list.autoStart } : null)
    setSelected([])
  }, [listId, list?.minMinutes, list?.repeatMinutes, list?.fullThreshold, list?.autoStart])

  const targets = useMemo(() => {
    const rows = (data?.targets ?? []).filter(target => target.listId === listId)
    const filtered = rows.filter(target => {
      if (filter === 'todas') return true
      if (filter === 'sin') return !target.scoutClass
      return target.scoutClass === filter
    })
    return filtered.sort((a, b) => {
      const rank = (target: Target) => target.scoutClass === 'gorda' ? 0 : target.scoutClass === 'flaca' ? 1 : target.scoutClass === 'peligro' ? 2 : 3
      return rank(a) - rank(b) || a.danger - b.danger || a.distance - b.distance
    })
  }, [data, listId, filter])

  const counts = useMemo(() => {
    const rows = (data?.targets ?? []).filter(target => target.listId === listId)
    return {
      todas: rows.length,
      gorda: rows.filter(target => target.scoutClass === 'gorda').length,
      flaca: rows.filter(target => target.scoutClass === 'flaca').length,
      peligro: rows.filter(target => target.scoutClass === 'peligro').length,
      sin: rows.filter(target => !target.scoutClass).length,
    }
  }, [data, listId])

  async function run(action: () => Promise<{ message: string }>) {
    try {
      const result = await action()
      notify(result.message)
      await load()
      return true
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
      return false
    }
  }

  async function createList() {
    const name = await ui.prompt('Nombre de la lista de scouting', 'Vecinos')
    if (!name) return
    await run(() => post(`/api/accounts/${accountId}/scouting/lists`, { villageId, name }))
  }

  async function saveList() {
    if (!list || !draft) return
    await run(() => put(`/api/accounts/${accountId}/scouting/lists/${list.id}`, {
      name: list.name,
      minMinutes: Number(draft.minMinutes) || 0,
      repeatMinutes: Number(draft.repeatMinutes) || 0,
      fullThreshold: Number(draft.fullThreshold) || 0,
      autoStart: draft.autoStart,
    }))
  }

  async function addTargets() {
    if (!list || !data) return
    const lines = addText.split(/\r?\n/).map(line => line.trim()).filter(Boolean)
    const urls = lines.map(line => toUrl(line, data.server))
    const bad = lines.filter((_, index) => !urls[index])
    if (bad.length > 0) {
      notify(`No entendí: ${bad[0]}. Usá un enlace del mapa o coordenadas como -59|29.`)
      return
    }
    setAdding(true)
    let added = 0
    for (const url of urls) {
      try {
        await post(`/api/accounts/${accountId}/farming/lists/${list.id}/villages`, { url, troops: addTroops || data.scoutPlan })
        added++
      } catch (error) {
        notify(error instanceof Error ? error.message : 'No se pudo agregar')
      }
    }
    setAdding(false)
    setAddOpen(false)
    setAddText('')
    notify(`${added} de ${urls.length} aldeas agregadas.`)
    await load()
  }

  async function promote() {
    if (!promoteList) {
      notify('Elegí una lista propia.')
      return
    }
    const ok = await run(() => post(`/api/accounts/${accountId}/scouting/promote`, { cowIds: selected, listId: promoteList, troops: promoteTroops }))
    if (ok) {
      setPromoteOpen(false)
      setSelected([])
    }
  }

  if (data && data.villages.length === 0) return <Empty title="Todavía no hay aldeas" text="Conectá la cuenta. Cuando el juego cargue las aldeas, vas a poder espiar desde acá." />

  const allChecked = targets.length > 0 && targets.every(target => selected.includes(target.id))

  return (
    <Stack spacing={1.5} sx={{ flex: 1, minHeight: 0 }}>
      <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Scouting</Typography>
        {data && (
          <TextField select label="Los espías salen de" value={villageId || ''} onChange={event => setVillageId(Number(event.target.value))} sx={{ minWidth: 240 }}>
            {data.villages.map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
          </TextField>
        )}
        {data && data.lists.length > 0 && (
          <TextField select label="Lista" value={listId || ''} onChange={event => setListId(Number(event.target.value))} sx={{ minWidth: 220 }}>
            {data.lists.map(item => <MenuItem key={item.id} value={item.id}>{item.name}{item.running ? ' · en curso' : ''}</MenuItem>)}
          </TextField>
        )}
        <Button startIcon={<AddIcon />} onClick={createList} disabled={!villageId}>Nueva lista</Button>
      </Stack>

      {data && data.lists.length === 0 && (
        <Empty title="Creá una lista de scouting" text="Agregá aldeas, el bot les manda espías y clasifica cada una: vaca gorda, vaca flaca o peligro del 1 al 10." />
      )}

      {list && draft && (
        <Stack spacing={1.5} sx={{ ...panel, p: 2 }}>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            <Typography sx={{ fontWeight: 700, fontSize: 16, mr: 1 }}>{list.name}</Typography>
            <Chip size="small" label={list.running ? (list.countdown || 'en curso') : 'detenida'} color={list.running ? 'secondary' : 'default'} />
            <Box sx={{ flex: 1 }} />
            {!list.running && (
              <Button variant="contained" startIcon={<PlayArrowIcon />} onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${list.id}/start`, { minMinutes: Number(draft.minMinutes) || 0, repeatMinutes: Number(draft.repeatMinutes) || 0 }))}>Iniciar</Button>
            )}
            {list.running && <Button variant="outlined" color="warning" startIcon={<StopIcon />} onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${list.id}/stop`))}>Detener</Button>}
            <Tooltip title="Borrar la lista">
              <IconButton sx={{ color: '#ed4245' }} onClick={async () => { if (await ui.confirm(`¿Borrar ${list.name} y sus aldeas?`)) run(() => del(`/api/accounts/${accountId}/farming/lists/${list.id}`)) }}><DeleteOutlineIcon /></IconButton>
            </Tooltip>
          </Stack>
          <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" useFlexGap>
            <TextField type="number" label="Volver a espiar cada (min)" value={draft.minMinutes} onChange={event => setDraft({ ...draft, minMinutes: event.target.value })} onBlur={saveList} sx={{ width: 200 }} />
            <TextField type="number" label="Revisar la lista cada (min)" value={draft.repeatMinutes} onChange={event => setDraft({ ...draft, repeatMinutes: event.target.value })} onBlur={saveList} sx={{ width: 200 }} />
            <Stack direction="row" spacing={0.5} alignItems="center">
              <TextField type="number" label="Vaca gorda desde (recursos)" value={draft.fullThreshold} onChange={event => setDraft({ ...draft, fullThreshold: event.target.value })} onBlur={saveList} sx={{ width: 210 }} />
              <Hint text="Sin defensas y con el almacén lleno (madera, barro y hierro iguales) es vaca gorda. Si ponés un número, también cuenta como gorda cuando el total de recursos llega a ese número. 0 = solo almacén lleno." />
            </Stack>
            <FormControlLabel control={<Switch checked={draft.autoStart} onChange={event => { const next = { ...draft, autoStart: event.target.checked }; setDraft(next); put(`/api/accounts/${accountId}/scouting/lists/${list.id}`, { name: list.name, minMinutes: Number(next.minMinutes) || 0, repeatMinutes: Number(next.repeatMinutes) || 0, fullThreshold: Number(next.fullThreshold) || 0, autoStart: next.autoStart }).then(result => notify(result.message)).catch((error: Error) => notify(error.message)).finally(() => load()) }} />} label="Iniciar al conectar" />
          </Stack>
          <Typography variant="caption" color="text.secondary">
            Cada vuelta primero lee los reportes de espionaje y visita el perfil de los dueños, después manda espías a las aldeas que toca. Se alterna con tus otras listas en el mismo navegador.
          </Typography>
        </Stack>
      )}

      {list && (
        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
          <ToggleButtonGroup exclusive size="small" value={filter} onChange={(_, value) => value && setFilter(value)} sx={{ bgcolor: '#2b2d31', p: 0.5, borderRadius: 1, '& .MuiToggleButton-root': { border: 0, borderRadius: 1, px: 1.5, color: '#b5bac1', textTransform: 'none', fontWeight: 600, '&.Mui-selected': { bgcolor: '#404249', color: '#fff' } } }}>
            <ToggleButton value="todas">Todas {counts.todas}</ToggleButton>
            <ToggleButton value="gorda">Vaca gorda {counts.gorda}</ToggleButton>
            <ToggleButton value="flaca">Vaca flaca {counts.flaca}</ToggleButton>
            <ToggleButton value="peligro">Peligro {counts.peligro}</ToggleButton>
            <ToggleButton value="sin">Sin espiar {counts.sin}</ToggleButton>
          </ToggleButtonGroup>
          <Box sx={{ flex: 1 }} />
          <Button startIcon={<AddIcon />} onClick={() => { setAddTroops(data?.scoutPlan ?? ''); setAddOpen(true) }}>Agregar aldeas</Button>
          <Button variant="contained" startIcon={<PlaylistAddIcon />} disabled={selected.length === 0} onClick={() => { setPromoteList(data?.ownLists[0]?.id ?? 0); setPromoteTroops('A:5'); setPromoteOpen(true) }}>
            Agregar a lista propia{selected.length > 0 ? ` (${selected.length})` : ''}
          </Button>
        </Stack>
      )}

      {list && (
        <Box sx={{ ...panel, flex: 1, minHeight: 0, overflow: 'auto' }}>
          <Box sx={{ display: 'grid', gridTemplateColumns: columns, gap: 1, alignItems: 'center', px: 1, py: 0.75, position: 'sticky', top: 0, zIndex: 1, bgcolor: '#2b2d31', color: '#949ba4', fontSize: 12, fontWeight: 700, minWidth: 900 }}>
            <Checkbox size="small" checked={allChecked} indeterminate={!allChecked && targets.some(target => selected.includes(target.id))} onChange={event => setSelected(event.target.checked ? targets.map(target => target.id) : [])} />
            <Box>Aldea · dueño</Box>
            <Box>Clasificación</Box>
            <Box>Recursos (madera/barro/hierro/cereal)</Box>
            <Box>Tropas</Box>
            <Box>Dist.</Box>
            <Box>Jugador</Box>
            <Box>Estado</Box>
            <Box />
          </Box>
          {targets.length === 0 && <Typography sx={{ p: 2, color: '#949ba4' }}>{counts.todas === 0 ? 'La lista está vacía. Agregá aldeas con su enlace o sus coordenadas.' : 'Ninguna aldea en este filtro.'}</Typography>}
          {targets.map(target => {
            const color = classColor(target)
            const checked = selected.includes(target.id)
            return (
              <Box key={target.id} sx={{ display: 'grid', gridTemplateColumns: columns, gap: 1, alignItems: 'center', px: 1, py: 0.6, borderTop: '1px solid #3f4147', minWidth: 900, bgcolor: checked ? '#35373c' : 'transparent' }}>
                <Checkbox size="small" checked={checked} onChange={event => setSelected(current => event.target.checked ? [...current, target.id] : current.filter(id => id !== target.id))} />
                <Box sx={{ minWidth: 0 }}>
                  <Typography sx={{ fontWeight: 600, fontSize: 14 }} noWrap title={target.url}>{target.name}{target.x != null ? ` (${target.x}|${target.y})` : ''}</Typography>
                  <Typography sx={{ fontSize: 12, color: '#949ba4' }} noWrap>{[target.owner, target.alliance && `[${target.alliance}]`, target.population >= 0 && `${target.population} hab.`].filter(Boolean).join(' · ') || '—'}</Typography>
                </Box>
                <Box>
                  {target.label
                    ? <Chip size="small" label={target.label} sx={{ bgcolor: color.bg, color: color.fg, fontWeight: 700 }} />
                    : <Typography sx={{ fontSize: 12, color: '#949ba4' }}>{target.pending ? 'esperando reporte' : 'sin espiar'}</Typography>}
                </Box>
                <Typography sx={{ fontSize: 13 }}>
                  {target.wood < 0 ? '—' : `${number(target.wood)} / ${number(target.clay)} / ${number(target.iron)} / ${number(target.crop)}`}
                  {target.cranny > 0 && <Box component="span" sx={{ color: '#949ba4' }}> · escondite {target.cranny}</Box>}
                </Typography>
                <Typography sx={{ fontSize: 13, color: target.defenders === 0 ? '#3ba55d' : target.defenders < 0 && target.scoutedAt ? '#ed4245' : '#dbdee1' }}>
                  {!target.scoutedAt ? '—' : target.scoutsDied ? 'mató espías' : number(target.defenders)}
                </Typography>
                <Typography sx={{ fontSize: 13 }}>{target.distance >= 0 ? target.distance.toFixed(1) : '—'}</Typography>
                <Tooltip title={target.playerPopulation >= 0 ? `Población ${target.playerPopulation} · ${target.playerVillages} aldeas · ataque ${number(target.attackPoints)} · defensa ${number(target.defensePoints)} · héroe ${number(target.heroLevel)}` : 'Todavía no se leyó el perfil'}>
                  <Typography sx={{ fontSize: 13, color: '#b5bac1' }} noWrap>{target.playerPopulation >= 0 ? `${target.playerPopulation} hab · ${target.playerVillages} ald.` : '—'}</Typography>
                </Tooltip>
                <Box sx={{ minWidth: 0 }}>
                  <Typography sx={{ fontSize: 12 }} noWrap>{target.status}</Typography>
                  {target.scoutedAt && <Typography sx={{ fontSize: 11, color: '#949ba4' }} noWrap>espiada {ago(target.scoutedAt)}</Typography>}
                </Box>
                <Tooltip title="Quitar de la lista">
                  <IconButton size="small" sx={{ color: '#ed4245' }} onClick={async () => { if (await ui.confirm(`¿Quitar ${target.name} de la lista?`)) run(() => del(`/api/accounts/${accountId}/farming/villages/${target.id}`)) }}><DeleteOutlineIcon fontSize="small" /></IconButton>
                </Tooltip>
              </Box>
            )
          })}
        </Box>
      )}

      <Dialog open={addOpen} onClose={() => !adding && setAddOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Agregar aldeas para espiar</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField multiline minRows={6} label="Una aldea por línea" placeholder={'-59|29\nhttps://servidor/karte.php?x=-60&y=31'} value={addText} onChange={event => setAddText(event.target.value)} />
            <TextField label="Espías" helperText={`Por defecto ${data?.scoutPlan ?? ''}: la letra es la casilla del espía en el punto de reunión.`} value={addTroops} onChange={event => setAddTroops(event.target.value)} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button disabled={adding} onClick={() => setAddOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={adding || !addText.trim()} onClick={addTargets}>{adding ? 'Agregando...' : 'Agregar'}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={promoteOpen} onClose={() => setPromoteOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Agregar {selected.length} aldea{selected.length === 1 ? '' : 's'} a una lista propia</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {(data?.ownLists.length ?? 0) === 0
              ? <Typography color="text.secondary">Todavía no tenés listas propias. Creá una en Farming → Listas propias.</Typography>
              : (
                <TextField select label="Lista" value={promoteList || ''} onChange={event => setPromoteList(Number(event.target.value))}>
                  {data?.ownLists.map(item => <MenuItem key={item.id} value={item.id}>{item.name}</MenuItem>)}
                </TextField>
              )}
            <TextField label="Tropas a mandar" helperText="Ejemplo: A:5 o A:5 D:1" value={promoteTroops} onChange={event => setPromoteTroops(event.target.value)} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPromoteOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!promoteList || !promoteTroops.trim()} onClick={promote}>Agregar</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}
