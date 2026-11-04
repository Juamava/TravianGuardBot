import { useEffect, useState } from 'react'
import { Button, Checkbox, FormControlLabel, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { api, put, type Meta } from '../api'

type Field =
  | { kind: 'range'; label: string; unit: string; min: string; max: string }
  | { kind: 'bool'; label: string; key: string }
  | { kind: 'number'; label: string; key: string }
  | { kind: 'tribe'; label: string }
  | { kind: 'troop'; label: string; key: string }

const accountFields: { title: string; fields: Field[] }[] = [
  { title: 'Actividad', fields: [
    { kind: 'range', label: 'Tiempo de trabajo', unit: 'minutos', min: 'WorkTimeMin', max: 'WorkTimeMax' },
    { kind: 'range', label: 'Tiempo de descanso', unit: 'minutos', min: 'SleepTimeMin', max: 'SleepTimeMax' },
  ]},
  { title: 'Demoras', fields: [
    { kind: 'range', label: 'Demora del click', unit: 'ms', min: 'ClickDelayMin', max: 'ClickDelayMax' },
    { kind: 'range', label: 'Demora entre tareas', unit: 'ms', min: 'TaskDelayMin', max: 'TaskDelayMax' },
  ]},
  { title: 'Funciones', fields: [
    { kind: 'bool', label: 'Cargar los edificios de las aldeas', key: 'EnableAutoLoadVillageBuilding' },
    { kind: 'bool', label: 'Empezar aventuras', key: 'EnableAutoStartAdventure' },
    { kind: 'bool', label: 'Reclamar la misión diaria', key: 'EnableAutoClaimDailyQuest' },
    { kind: 'bool', label: 'Repartir puntos del héroe (1 poder, 3 recursos)', key: 'EnableAutoSetHeroPoint' },
    { kind: 'tribe', label: 'Tribu' },
  ]},
  { title: 'Chrome', fields: [
    { kind: 'bool', label: 'Chrome oculto', key: 'HeadlessChrome' },
  ]},
]

const villageFields: { title: string; fields: Field[] }[] = [
  { title: 'Construcción', fields: [
    { kind: 'tribe', label: 'Tribu' },
    { kind: 'bool', label: 'Usar recursos del héroe si faltan', key: 'UseHeroResourceForBuilding' },
    { kind: 'bool', label: 'Lógica de cola romana', key: 'ApplyRomanQueueLogicWhenBuilding' },
    { kind: 'bool', label: 'Completar al instante', key: 'CompleteImmediately' },
    { kind: 'number', label: 'Completar si la cola supera (minutos)', key: 'CompleteImmediatelyTime' },
    { kind: 'bool', label: 'Usar la mejora especial', key: 'UseSpecialUpgrade' },
  ]},
  { title: 'Refresco', fields: [
    { kind: 'bool', label: 'Refrescar solo', key: 'AutoRefreshEnable' },
    { kind: 'range', label: 'Refrescar cada', unit: 'minutos', min: 'AutoRefreshMin', max: 'AutoRefreshMax' },
  ]},
  { title: 'NPC', fields: [
    { kind: 'bool', label: 'Cambiar cereal con el NPC', key: 'AutoNPCEnable' },
    { kind: 'bool', label: 'Aunque se pase el almacén', key: 'AutoNPCOverflow' },
    { kind: 'number', label: 'Granero %', key: 'AutoNPCGranaryPercent' },
    { kind: 'number', label: 'Madera', key: 'AutoNPCWood' },
    { kind: 'number', label: 'Barro', key: 'AutoNPCClay' },
    { kind: 'number', label: 'Hierro', key: 'AutoNPCIron' },
    { kind: 'number', label: 'Cereal', key: 'AutoNPCCrop' },
  ]},
  { title: 'Tropas', fields: [
    { kind: 'bool', label: 'Entrenar tropas', key: 'TrainTroopEnable' },
    { kind: 'bool', label: 'Entrenar el máximo si faltan recursos', key: 'TrainWhenLowResource' },
    { kind: 'range', label: 'Repetir cada', unit: 'minutos', min: 'TrainTroopRepeatTimeMin', max: 'TrainTroopRepeatTimeMax' },
    { kind: 'troop', label: 'Cuartel', key: 'BarrackTroop' },
    { kind: 'range', label: 'Cantidad cuartel', unit: 'unidades', min: 'BarrackAmountMin', max: 'BarrackAmountMax' },
    { kind: 'troop', label: 'Gran cuartel', key: 'GreatBarrackTroop' },
    { kind: 'range', label: 'Cantidad gran cuartel', unit: 'unidades', min: 'GreatBarrackAmountMin', max: 'GreatBarrackAmountMax' },
    { kind: 'troop', label: 'Establo', key: 'StableTroop' },
    { kind: 'range', label: 'Cantidad establo', unit: 'unidades', min: 'StableAmountMin', max: 'StableAmountMax' },
    { kind: 'troop', label: 'Gran establo', key: 'GreatStableTroop' },
    { kind: 'range', label: 'Cantidad gran establo', unit: 'unidades', min: 'GreatStableAmountMin', max: 'GreatStableAmountMax' },
    { kind: 'troop', label: 'Taller', key: 'WorkshopTroop' },
    { kind: 'range', label: 'Cantidad taller', unit: 'unidades', min: 'WorkshopAmountMin', max: 'WorkshopAmountMax' },
    { kind: 'bool', label: 'Reclamar misiones de la aldea', key: 'AutoClaimQuestEnable' },
  ]},
]

type Props = {
  title: string
  path: string
  groups: { title: string; fields: Field[] }[]
  meta: Meta | null
  notify: (message: string) => void
}

export function SettingForm({ title, path, groups, meta, notify }: Props) {
  const [values, setValues] = useState<Record<string, number>>({})

  useEffect(() => {
    api<Record<string, number>>(path).then(result => setValues(result.data)).catch((error: Error) => notify(error.message))
  }, [path])

  function set(key: string, value: number) {
    setValues(current => ({ ...current, [key]: value }))
  }

  async function save() {
    try {
      const result = await put(path, values)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo guardar')
    }
  }

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
      setValues(parsed)
      notify(result.message)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'Archivo inválido')
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4">{title}</Typography>
      <Stack direction="row" spacing={1}>
        <Button variant="contained" onClick={save}>Guardar</Button>
        <Button variant="outlined" onClick={download}>Exportar</Button>
        <Button variant="outlined" component="label">Importar
          <input hidden type="file" accept="application/json" onChange={event => { const file = event.target.files?.[0]; if (file) upload(file) }} />
        </Button>
      </Stack>
      {groups.map(group => (
        <Stack key={group.title} spacing={1.5} sx={{ p: 2, borderRadius: 2, bgcolor: '#2b2d31' }}>
          <Typography variant="h6">{group.title}</Typography>
          {group.fields.map(field => {
            if (field.kind === 'bool') {
              return <FormControlLabel key={field.key} control={<Checkbox checked={values[field.key] === 1} onChange={event => set(field.key, event.target.checked ? 1 : 0)} />} label={field.label} />
            }
            if (field.kind === 'range') {
              return (
                <Stack key={field.min} direction="row" spacing={1} alignItems="center">
                  <Typography sx={{ minWidth: 180 }}>{field.label}</Typography>
                  <TextField size="small" type="number" label="Mín" value={values[field.min] ?? 0} onChange={event => set(field.min, Number(event.target.value))} />
                  <TextField size="small" type="number" label="Máx" value={values[field.max] ?? 0} onChange={event => set(field.max, Number(event.target.value))} />
                  <Typography color="text.secondary">{field.unit}</Typography>
                </Stack>
              )
            }
            if (field.kind === 'tribe') {
              return (
                <TextField key="tribe" select label={field.label} value={values.Tribe ?? 0} onChange={event => set('Tribe', Number(event.target.value))}>
                  {(meta?.tribes ?? []).map(tribe => <MenuItem key={tribe.value} value={tribe.value}>{tribe.name}</MenuItem>)}
                </TextField>
              )
            }
            if (field.kind === 'troop') {
              return (
                <TextField key={field.key} select label={field.label} value={values[field.key] ?? 0} onChange={event => set(field.key, Number(event.target.value))}>
                  {(meta?.troops ?? []).map(troop => <MenuItem key={troop.value} value={troop.value}>{troop.name}</MenuItem>)}
                </TextField>
              )
            }
            return <TextField key={field.key} type="number" label={field.label} value={values[field.key] ?? 0} onChange={event => set(field.key, Number(event.target.value))} />
          })}
        </Stack>
      ))}
    </Stack>
  )
}

export function AccountSettings(props: { accountId: number; meta: Meta | null; notify: (message: string) => void }) {
  return <SettingForm title="Ajustes de la cuenta" path={`/api/accounts/${props.accountId}/settings`} groups={accountFields} meta={props.meta} notify={props.notify} />
}

export function VillageSettings(props: { accountId: number; villageId: number; meta: Meta | null; notify: (message: string) => void }) {
  return <SettingForm title="Ajustes de la aldea" path={`/api/accounts/${props.accountId}/villages/${props.villageId}/settings`} groups={villageFields} meta={props.meta} notify={props.notify} />
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
