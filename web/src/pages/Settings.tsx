import { useEffect, useRef, useState } from 'react'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import { Box, Button, Checkbox, FormControlLabel, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Tooltip, Typography } from '@mui/material'
import { api, put, type Meta } from '../api'
import { AccountEditor } from './Accounts'

type Field =
  | { kind: 'range'; label: string; unit: string; min: string; max: string; hint: string }
  | { kind: 'bool'; label: string; key: string; hint: string }
  | { kind: 'number'; label: string; key: string; hint: string }
  | { kind: 'tribe'; label: string; hint: string }
  | { kind: 'troop'; label: string; key: string; hint: string }

const normal = ' El valor se elige con media normal: cae más seguido cerca del centro del rango.'

const accountFields: { title: string; fields: Field[] }[] = [
  { title: 'Actividad', fields: [
    { kind: 'range', label: 'Tiempo de trabajo', unit: 'minutos', min: 'WorkTimeMin', max: 'WorkTimeMax', hint: 'Cuánto tiempo sigue la cuenta conectada antes de cerrar Chrome y dormir.' + normal },
    { kind: 'range', label: 'Tiempo de descanso', unit: 'minutos', min: 'SleepTimeMin', max: 'SleepTimeMax', hint: 'Cuánto tiempo queda cerrado Chrome mientras la cuenta duerme. Después se vuelve a abrir sola.' + normal },
  ]},
  { title: 'Demoras', fields: [
    { kind: 'range', label: 'Demora del click', unit: 'ms', min: 'ClickDelayMin', max: 'ClickDelayMax', hint: 'Pausa antes de cada click en el juego. No aplica a la espera entre una aldea y la siguiente.' + normal },
    { kind: 'range', label: 'Demora entre ataques', unit: 'ms', min: 'TaskDelayMin', max: 'TaskDelayMax', hint: 'Espera antes de atacar cada aldea de la lista de vacas. También es la pausa al terminar una tarea del bot y antes de empezar la siguiente.' + normal },
  ]},
  { title: 'Funciones', fields: [
    { kind: 'bool', label: 'Cargar los edificios de las aldeas', key: 'EnableAutoLoadVillageBuilding', hint: 'Al conectar, entra a cada aldea y lee los edificios.' },
    { kind: 'bool', label: 'Empezar aventuras', key: 'EnableAutoStartAdventure', hint: 'Manda al héroe a las aventuras que aparezcan.' },
    { kind: 'bool', label: 'Reclamar la misión diaria', key: 'EnableAutoClaimDailyQuest', hint: 'Reclama la recompensa de la misión diaria.' },
    { kind: 'bool', label: 'Repartir puntos del héroe (1 poder, 3 recursos)', key: 'EnableAutoSetHeroPoint', hint: 'Cuando el héroe sube de nivel, pone 1 punto en poder y 3 en recursos.' },
    { kind: 'bool', label: 'Cuenta Gold', key: 'Gold', hint: 'Con Gold, Farming muestra las listas propias y las del juego. Se usan de a una, porque hay un solo navegador.' },
    { kind: 'tribe', label: 'Tribu', hint: 'La tribu de la cuenta. Define qué tropas se pueden entrenar.' },
  ]},
  { title: 'Chrome', fields: [
    { kind: 'bool', label: 'Chrome oculto', key: 'HeadlessChrome', hint: 'Abre Chrome sin mostrar la ventana. El juego sigue corriendo igual. Aplica la próxima vez que conectes.' },
  ]},
]

const villageConstruction: { title: string; fields: Field[] }[] = [
  { title: 'Construcción', fields: [
    { kind: 'tribe', label: 'Tribu', hint: 'La tribu de esta aldea. Tiene que coincidir con la de la cuenta para entrenar las tropas correctas.' },
    { kind: 'bool', label: 'Usar recursos del héroe si faltan', key: 'UseHeroResourceForBuilding', hint: 'Si faltan recursos para construir, usa los del inventario del héroe.' },
    { kind: 'bool', label: 'Lógica de cola romana', key: 'ApplyRomanQueueLogicWhenBuilding', hint: 'En una aldea romana respeta las dos colas de construcción.' },
    { kind: 'bool', label: 'Completar al instante', key: 'CompleteImmediately', hint: 'Termina la construcción al instante cuando la cola supera el tiempo indicado.' },
    { kind: 'number', label: 'Completar si la cola supera (minutos)', key: 'CompleteImmediatelyTime', hint: 'Minutos de cola a partir de los cuales se completa la construcción al instante.' },
    { kind: 'bool', label: 'Usar la mejora especial', key: 'UseSpecialUpgrade', hint: 'Usa la mejora especial del edificio cuando está disponible.' },
    { kind: 'bool', label: 'Reclamar misiones de la aldea', key: 'AutoClaimQuestEnable', hint: 'Reclama las misiones que se completan en esta aldea.' },
  ]},
  { title: 'Refresco', fields: [
    { kind: 'bool', label: 'Refrescar solo', key: 'AutoRefreshEnable', hint: 'Vuelve a entrar a la aldea para actualizar recursos y edificios, aunque no haya nada que construir.' },
    { kind: 'range', label: 'Refrescar cada', unit: 'minutos', min: 'AutoRefreshMin', max: 'AutoRefreshMax', hint: 'Cada cuánto vuelve a leer la aldea.' + normal },
  ]},
]

const villageTrade: { title: string; fields: Field[] }[] = [
  { title: 'Comercio', fields: [
    { kind: 'bool', label: 'Cambiar cereal con el NPC', key: 'AutoNPCEnable', hint: 'Cuando el granero llega al porcentaje indicado, cambia recursos con el NPC.' },
    { kind: 'bool', label: 'Aunque se pase el almacén', key: 'AutoNPCOverflow', hint: 'Hace el cambio aunque la madera, el barro o el hierro superen el almacén.' },
    { kind: 'number', label: 'Granero %', key: 'AutoNPCGranaryPercent', hint: 'Porcentaje del granero a partir del cual se usa el NPC.' },
    { kind: 'number', label: 'Madera', key: 'AutoNPCWood', hint: 'Cuánta madera deja después del cambio con el NPC.' },
    { kind: 'number', label: 'Barro', key: 'AutoNPCClay', hint: 'Cuánto barro deja después del cambio con el NPC.' },
    { kind: 'number', label: 'Hierro', key: 'AutoNPCIron', hint: 'Cuánto hierro deja después del cambio con el NPC.' },
    { kind: 'number', label: 'Cereal', key: 'AutoNPCCrop', hint: 'Cuánto cereal deja después del cambio con el NPC.' },
  ]},
]


function Hint({ text }: { text: string }) {
  return (
    <Tooltip title={text} placement="top" arrow enterDelay={200} slotProps={{ tooltip: { sx: { maxWidth: 280, fontSize: 13, lineHeight: 1.45 } } }}>
      <InfoOutlinedIcon sx={{ fontSize: 16, color: '#8a7350', cursor: 'help' }} />
    </Tooltip>
  )
}

const tabGroup = {
  bgcolor: '#fbf5e2',
  p: 1,
  gap: 1,
  borderRadius: 2,
  flexWrap: 'wrap',
  width: 'fit-content',
  maxWidth: '100%',
  '& .MuiToggleButtonGroup-grouped': {
    margin: 0,
    border: 0,
    borderRadius: 1,
  },
  '& .MuiToggleButtonGroup-grouped:not(:first-of-type)': {
    marginLeft: 0,
    borderLeft: 0,
  },
  '& .MuiToggleButton-root': {
    border: 0,
    borderRadius: 1,
    px: 2,
    py: 0.75,
    color: '#6b5233',
    textTransform: 'none',
    fontWeight: 600,
    '&.Mui-selected': { bgcolor: '#e2cf9a', color: '#2b1d0e' },
    '&.Mui-selected:hover': { bgcolor: '#c9b27c' },
    '&:hover': { bgcolor: '#f1e5c2' },
  },
} as const

function SettingGroups({ groups, values, set, meta }: { groups: { title: string; fields: Field[] }[]; values: Record<string, number>; set: (key: string, value: number, immediate?: boolean) => void; meta: Meta | null }) {
  return (
    <>
      {groups.map(group => (
        <Stack key={group.title} spacing={1.5} sx={{ p: 2, borderRadius: 2, bgcolor: '#fbf5e2' }}>
          <Typography variant="h6">{group.title}</Typography>
          {group.fields.map(field => {
            if (field.kind === 'bool') {
              return (
                <Stack key={field.key} direction="row" spacing={0.75} alignItems="center">
                  <FormControlLabel control={<Checkbox checked={values[field.key] === 1} onChange={event => set(field.key, event.target.checked ? 1 : 0, true)} />} label={field.label} />
                  <Hint text={field.hint} />
                </Stack>
              )
            }
            if (field.kind === 'range') {
              return (
                <Stack key={field.min} direction="row" spacing={1} alignItems="center" flexWrap="wrap">
                  <Stack direction="row" spacing={0.75} alignItems="center" sx={{ minWidth: 220 }}>
                    <Typography>{field.label}</Typography>
                    <Hint text={field.hint} />
                  </Stack>
                  <TextField size="small" type="number" label="Mín" value={values[field.min] ?? 0} onChange={event => set(field.min, Number(event.target.value))} />
                  <TextField size="small" type="number" label="Máx" value={values[field.max] ?? 0} onChange={event => set(field.max, Number(event.target.value))} />
                  <Typography color="text.secondary">{field.unit}</Typography>
                </Stack>
              )
            }
            if (field.kind === 'tribe') {
              return (
                <Stack key="tribe" direction="row" spacing={1} alignItems="center">
                  <TextField select label={field.label} value={values.Tribe ?? 0} onChange={event => set('Tribe', Number(event.target.value), true)} sx={{ flex: 1 }}>
                    {(meta?.tribes ?? []).map(tribe => <MenuItem key={tribe.value} value={tribe.value}>{tribe.name}</MenuItem>)}
                  </TextField>
                  <Hint text={field.hint} />
                </Stack>
              )
            }
            if (field.kind === 'troop') {
              return (
                <Stack key={field.key} direction="row" spacing={1} alignItems="center">
                  <TextField select label={field.label} value={values[field.key] ?? 0} onChange={event => set(field.key, Number(event.target.value), true)} sx={{ flex: 1 }}>
                    {(meta?.troops ?? []).map(troop => <MenuItem key={troop.value} value={troop.value}>{troop.name}</MenuItem>)}
                  </TextField>
                  <Hint text={field.hint} />
                </Stack>
              )
            }
            return (
              <Stack key={field.key} direction="row" spacing={1} alignItems="center">
                <TextField type="number" label={field.label} value={values[field.key] ?? 0} onChange={event => set(field.key, Number(event.target.value))} sx={{ flex: 1 }} />
                <Hint text={field.hint} />
              </Stack>
            )
          })}
        </Stack>
      ))}
    </>
  )
}

type SettingsBag = {
  values: Record<string, number>
  set: (key: string, value: number, immediate?: boolean) => void
  replace: (values: Record<string, number>) => void
}

export function useAutoSettings(path: string, notify: (message: string) => void): SettingsBag {
  const [values, setValues] = useState<Record<string, number>>({})
  const valuesRef = useRef(values)
  const pathRef = useRef(path)
  const readyRef = useRef(false)
  const timerRef = useRef<number | null>(null)

  async function persist(target: string, snapshot: Record<string, number>) {
    try {
      const result = await put(target, snapshot)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo guardar')
    }
  }

  useEffect(() => {
    pathRef.current = path
    readyRef.current = false
    let cancelled = false
    api<Record<string, number>>(path).then(result => {
      if (cancelled || pathRef.current !== path) return
      valuesRef.current = result.data
      setValues(result.data)
      readyRef.current = true
    }).catch((error: Error) => {
      if (!cancelled) notify(error.message)
    })
    return () => {
      cancelled = true
      if (timerRef.current === null) return
      window.clearTimeout(timerRef.current)
      timerRef.current = null
      void persist(path, valuesRef.current)
    }
  }, [path])

  function set(key: string, value: number, immediate = false) {
    const next = { ...valuesRef.current, [key]: Number.isFinite(value) ? value : 0 }
    valuesRef.current = next
    setValues(next)
    if (!readyRef.current) return
    if (timerRef.current !== null) window.clearTimeout(timerRef.current)
    const target = pathRef.current
    if (immediate) {
      timerRef.current = null
      void persist(target, next)
      return
    }
    timerRef.current = window.setTimeout(() => {
      timerRef.current = null
      void persist(target, next)
    }, 500)
  }

  function replace(next: Record<string, number>) {
    valuesRef.current = next
    setValues(next)
    readyRef.current = true
  }

  return { values, set, replace }
}

function SettingsFiles({ path, values, replace, notify }: { path: string; values: Record<string, number>; replace: (values: Record<string, number>) => void; notify: (message: string) => void }) {
  function download() {
    const blob = new Blob([JSON.stringify(values, null, 2)], { type: 'application/json' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = 'ajustes.json'
    link.click()
  }

  async function upload(file: File) {
    try {
      const parsed = JSON.parse(await file.text()) as Record<string, number>
      const result = await put<Record<string, number>>(path, parsed)
      replace(parsed)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Archivo inválido')
    }
  }

  return (
    <Stack direction="row" spacing={2} useFlexGap flexWrap="wrap">
      <Button variant="outlined" onClick={download}>Exportar</Button>
      <Button variant="outlined" component="label">Importar
        <input hidden type="file" accept="application/json" onChange={event => { const file = event.target.files?.[0]; event.target.value = ''; if (file) upload(file) }} />
      </Button>
    </Stack>
  )
}

const accountTabs = [
  { id: 'cuenta', label: 'Cuenta' },
  { id: 'actividad', label: 'Actividad' },
  { id: 'demoras', label: 'Demoras' },
  { id: 'funciones', label: 'Funciones' },
] as const

export function AccountSettings(props: { accountId: number; meta: Meta | null; notify: (message: string) => void; refresh: () => void }) {
  const [tab, setTab] = useState<(typeof accountTabs)[number]['id']>('cuenta')
  const path = `/api/accounts/${props.accountId}/settings`
  const settings = useAutoSettings(path, props.notify)
  const groups = tab === 'actividad'
    ? accountFields.filter(group => group.title === 'Actividad')
    : tab === 'demoras'
      ? accountFields.filter(group => group.title === 'Demoras')
      : accountFields.filter(group => group.title === 'Funciones' || group.title === 'Chrome')

  return (
    <Stack spacing={2} sx={{ flex: 1, minHeight: 0 }}>
      <Typography variant="h4">Ajustes de la cuenta</Typography>
      <ToggleButtonGroup exclusive size="small" value={tab} onChange={(_, value) => value && setTab(value)} sx={tabGroup}>
        {accountTabs.map(item => <ToggleButton key={item.id} value={item.id}>{item.label}</ToggleButton>)}
      </ToggleButtonGroup>
      <Box sx={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        {tab === 'cuenta' && <AccountEditor key={props.accountId} accountId={props.accountId} notify={props.notify} refresh={props.refresh} />}
        {tab !== 'cuenta' && (
          <Stack spacing={2}>
            <SettingsFiles path={path} values={settings.values} replace={settings.replace} notify={props.notify} />
            <SettingGroups groups={groups} values={settings.values} set={settings.set} meta={props.meta} />
          </Stack>
        )}
      </Box>
    </Stack>
  )
}

const villageSections = {
  build: villageConstruction,
  trade: villageTrade,
} as const

export { SettingsFiles }

export function VillageSettings(props: { accountId: number; villageId: number; meta: Meta | null; notify: (message: string) => void; section: keyof typeof villageSections }) {
  const path = `/api/accounts/${props.accountId}/villages/${props.villageId}/settings`
  const settings = useAutoSettings(path, props.notify)
  return (
    <Stack spacing={2}>
      <SettingsFiles path={path} values={settings.values} replace={settings.replace} notify={props.notify} />
      <SettingGroups groups={villageSections[props.section]} values={settings.values} set={settings.set} meta={props.meta} />
    </Stack>
  )
}

export function Templates({ notify, refresh }: { notify: (message: string) => void; refresh: () => void }) {
  async function upload(kind: 'account' | 'village', file: File) {
    try {
      const response = await fetch(`/api/templates/${kind}/upload`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: await file.text() })
      const body = await response.json()
      if (!response.ok) throw new Error(body.message)
      notify(body.message)
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo guardar la plantilla')
    }
  }

  async function clear(kind: 'account' | 'village') {
    try {
      const result = await api(`/api/templates/${kind}`, { method: 'DELETE' })
      notify(result.message)
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo borrar')
    }
  }

  return (
    <Stack spacing={1} sx={{ mt: 2 }}>
      <Typography fontWeight={800}>Plantillas para cuentas y aldeas nuevas</Typography>
      <Stack direction="row" spacing={1} flexWrap="wrap">
        <Button component="label" variant="outlined">Plantilla de cuenta
          <input hidden type="file" accept="application/json" onChange={event => { const file = event.target.files?.[0]; if (file) upload('account', file) }} />
        </Button>
        <Button onClick={() => clear('account')}>Quitar</Button>
        <Button component="label" variant="outlined">Plantilla de aldea
          <input hidden type="file" accept="application/json" onChange={event => { const file = event.target.files?.[0]; if (file) upload('village', file) }} />
        </Button>
        <Button onClick={() => clear('village')}>Quitar</Button>
      </Stack>
    </Stack>
  )
}
