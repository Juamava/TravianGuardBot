import { useEffect, useState } from 'react'
import { Box, Button, Checkbox, Chip, FormControlLabel, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import ShieldIcon from '@mui/icons-material/Shield'
import RefreshIcon from '@mui/icons-material/Refresh'
import { api, post, type Meta } from '../api'
import { panel } from '../ui'
import { SettingsFiles, useAutoSettings, type SettingsBag } from './Settings'

type Status = { lastTrainAt: string | null; nextTrainAt: string | null; queued: boolean; running: boolean; buildings: string[] }

type EvadeStatus = {
  queued: boolean
  running: boolean
  nextCheckAt: string | null
  checkedAt: string | null
  incoming: number
  nextAttackAt: string | null
  waveFirstAt: string | null
  waveLastAt: string | null
  waveCount: number
  stationed: boolean
  homeAt: string | null
  message: string
  history: string[]
  villageX: number | null
  villageY: number | null
}

type Slot = { building: string; title: string; group: 'infantry' | 'cavalry' | 'siege'; troop: string; min: string; max: string }

const slots: Slot[] = [
  { building: 'Barracks', title: 'Cuartel', group: 'infantry', troop: 'BarrackTroop', min: 'BarrackAmountMin', max: 'BarrackAmountMax' },
  { building: 'GreatBarracks', title: 'Gran cuartel', group: 'infantry', troop: 'GreatBarrackTroop', min: 'GreatBarrackAmountMin', max: 'GreatBarrackAmountMax' },
  { building: 'Stable', title: 'Establo', group: 'cavalry', troop: 'StableTroop', min: 'StableAmountMin', max: 'StableAmountMax' },
  { building: 'GreatStable', title: 'Gran establo', group: 'cavalry', troop: 'GreatStableTroop', min: 'GreatStableAmountMin', max: 'GreatStableAmountMax' },
  { building: 'Workshop', title: 'Taller', group: 'siege', troop: 'WorkshopTroop', min: 'WorkshopAmountMin', max: 'WorkshopAmountMax' },
]

function Hint({ text }: { text: string }) {
  return (
    <Tooltip title={text} placement="top" arrow enterDelay={200} slotProps={{ tooltip: { sx: { maxWidth: 280, fontSize: 13, lineHeight: 1.45 } } }}>
      <InfoOutlinedIcon sx={{ fontSize: 16, color: '#5c4529', cursor: 'help' }} />
    </Tooltip>
  )
}

function when(value: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  const today = new Date()
  const time = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  return date.toDateString() === today.toDateString() ? time : `${date.toLocaleDateString([], { day: '2-digit', month: '2-digit' })} ${time}`
}

function countdown(value: string | null) {
  if (!value) return ''
  const remaining = new Date(value).getTime() - Date.now()
  if (!Number.isFinite(remaining)) return ''
  if (remaining <= 0) return 'en espera de turno'
  const minutes = Math.floor(remaining / 60000)
  const seconds = Math.floor((remaining % 60000) / 1000)
  if (minutes >= 60) return `en ${Math.floor(minutes / 60)} h ${minutes % 60} min`
  return `en ${minutes}:${String(seconds).padStart(2, '0')}`
}

export function Troops({ accountId, villageId, meta, notify }: { accountId: number; villageId: number; meta: Meta | null; notify: (message: string) => void }) {
  const path = `/api/accounts/${accountId}/villages/${villageId}/settings`
  const settings = useAutoSettings(path, notify)
  const values = settings.values
  const [status, setStatus] = useState<Status | null>(null)
  const [, tick] = useState(0)

  async function loadStatus() {
    const result = await api<Status>(`/api/accounts/${accountId}/villages/${villageId}/troops`)
    setStatus(result.data)
  }

  useEffect(() => {
    loadStatus().catch(() => undefined)
    const poll = window.setInterval(() => loadStatus().catch(() => undefined), 5000)
    const clock = window.setInterval(() => tick(value => value + 1), 1000)
    return () => {
      window.clearInterval(poll)
      window.clearInterval(clock)
    }
  }, [accountId, villageId])

  async function trainNow() {
    try {
      notify((await post(`/api/accounts/${accountId}/villages/${villageId}/troops/now`)).message)
      await loadStatus()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  const tribe = values.Tribe ?? 0
  const tribeName = meta?.tribes.find(item => item.value === tribe)?.name ?? 'sin tribu'
  const enabled = values.TrainTroopEnable === 1
  const active = slots.filter(slot => (values[slot.troop] ?? 0) !== 0)

  return (
    <Stack spacing={1.5}>
      <EvadePanel accountId={accountId} villageId={villageId} settings={settings} notify={notify} />

      <Stack spacing={1.5} sx={{ ...panel, p: 2 }}>
        <Stack direction="row" alignItems="center" spacing={1.5} flexWrap="wrap" useFlexGap>
          <Stack direction="row" alignItems="center" spacing={0.5}>
            <Switch checked={enabled} onChange={event => settings.set('TrainTroopEnable', event.target.checked ? 1 : 0, true)} />
            <Typography sx={{ fontWeight: 700, fontSize: 16 }}>Entrenar tropas</Typography>
          </Stack>
          <Chip size="small" label={enabled ? (active.length > 0 ? `${active.length} edificio${active.length === 1 ? '' : 's'}` : 'sin edificios elegidos') : 'apagado'} color={enabled && active.length > 0 ? 'secondary' : 'default'} />
          <Box sx={{ flex: 1 }} />
          <Button variant="contained" startIcon={<PlayArrowIcon />} disabled={!enabled || active.length === 0 || status?.running} onClick={trainNow}>Entrenar ahora</Button>
        </Stack>

        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 1 }}>
          <Info label="Último entrenamiento" value={when(status?.lastTrainAt ?? null)} />
          <Info label="Próximo" value={!enabled ? '—' : status?.running ? 'entrenando ahora' : when(status?.nextTrainAt ?? null)} detail={enabled && !status?.running ? countdown(status?.nextTrainAt ?? null) : ''} />
          <Info label="Tribu de la aldea" value={tribeName} detail="Define qué tropas aparecen" />
        </Box>

        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
          <Typography>Repetir cada</Typography>
          <TextField type="number" label="Mín" value={values.TrainTroopRepeatTimeMin ?? 0} onChange={event => settings.set('TrainTroopRepeatTimeMin', Number(event.target.value))} sx={{ width: 96 }} />
          <TextField type="number" label="Máx" value={values.TrainTroopRepeatTimeMax ?? 0} onChange={event => settings.set('TrainTroopRepeatTimeMax', Number(event.target.value))} sx={{ width: 96 }} />
          <Typography color="text.secondary">minutos</Typography>
          <Hint text="Después de entrenar, espera un tiempo al azar entre estos dos valores. La hora queda guardada: si reiniciás el bot, no vuelve a entrenar antes de tiempo." />
        </Stack>
        <Stack direction="row" spacing={0.5} alignItems="center">
          <Switch size="small" checked={values.TrainWhenLowResource === 1} onChange={event => settings.set('TrainWhenLowResource', event.target.checked ? 1 : 0, true)} />
          <Typography>Si no alcanzan los recursos, entrenar lo que se pueda</Typography>
          <Hint text="Sin esto, si no alcanza para la cantidad elegida, ese edificio no entrena en esa vuelta." />
        </Stack>
      </Stack>

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 1.5 }}>
        {slots.map(slot => {
          const options = (meta?.troops ?? []).filter(troop => troop.group === slot.group && (tribe === 0 || troop.tribe === tribe))
          const troop = values[slot.troop] ?? 0
          const on = troop !== 0
          const built = status ? status.buildings.includes(slot.building) : true
          return (
            <Stack key={slot.building} spacing={1.25} sx={{ ...panel, p: 2, opacity: on ? 1 : 0.75, boxShadow: on ? 'inset 3px 0 0 #4f8a10' : 'none' }}>
              <Stack direction="row" alignItems="center" spacing={1}>
                <Typography sx={{ fontWeight: 700, flex: 1 }}>{slot.title}</Typography>
                {!built && <Chip size="small" label="no construido" sx={{ bgcolor: '#f6d5cf', color: '#b8281d' }} />}
              </Stack>
              <TextField select label="Tropa" value={troop} onChange={event => settings.set(slot.troop, Number(event.target.value), true)}>
                <MenuItem value={0}>No entrenar acá</MenuItem>
                {options.map(item => <MenuItem key={item.value} value={item.value}>{item.name}</MenuItem>)}
                {troop !== 0 && !options.some(item => item.value === troop) && (
                  <MenuItem value={troop}>{meta?.troops.find(item => item.value === troop)?.name ?? troop} (otra tribu)</MenuItem>
                )}
              </TextField>
              <Stack direction="row" spacing={1} alignItems="center">
                <TextField type="number" label="Cantidad mín" disabled={!on} value={values[slot.min] ?? 0} onChange={event => settings.set(slot.min, Number(event.target.value))} />
                <TextField type="number" label="Cantidad máx" disabled={!on} value={values[slot.max] ?? 0} onChange={event => settings.set(slot.max, Number(event.target.value))} />
              </Stack>
              <Typography variant="caption" color="text.secondary">
                {on ? `Cada vuelta entrena entre ${values[slot.min] ?? 0} y ${values[slot.max] ?? 0}.` : 'Elegí una tropa para que este edificio entrene.'}
              </Typography>
            </Stack>
          )
        })}
      </Box>

      <SettingsFiles path={path} values={values} replace={settings.replace} notify={notify} />
    </Stack>
  )
}

function Info({ label, value, detail }: { label: string; value: string; detail?: string }) {
  return (
    <Box sx={{ bgcolor: '#efe4c6', borderRadius: 1, px: 1.5, py: 1 }}>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      <Typography sx={{ fontWeight: 700 }}>{value}</Typography>
      {detail && <Typography variant="caption" color="text.secondary">{detail}</Typography>}
    </Box>
  )
}

function seconds(value: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function EvadePanel({ accountId, villageId, settings, notify }: { accountId: number; villageId: number; settings: SettingsBag; notify: (message: string) => void }) {
  const values = settings.values
  const [status, setStatus] = useState<EvadeStatus | null>(null)
  const enabled = values.EvadeEnable === 1
  const x = values.EvadeTargetX ?? 0
  const y = values.EvadeTargetY ?? 0
  const hasTarget = x !== 0 || y !== 0

  async function load() {
    const result = await api<EvadeStatus>(`/api/accounts/${accountId}/villages/${villageId}/evade`)
    setStatus(result.data)
  }

  useEffect(() => {
    load().catch(() => undefined)
    const poll = window.setInterval(() => load().catch(() => undefined), 3000)
    return () => window.clearInterval(poll)
  }, [accountId, villageId])

  async function checkNow() {
    try {
      notify((await post(`/api/accounts/${accountId}/villages/${villageId}/evade/now`)).message)
      await load()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  function pasteTarget(text: string) {
    const match = /(-?\d+)\s*[|,;\s]\s*(-?\d+)/.exec(text.replace(/[\u2212\u2013]/g, '-').replace(/[\u202a-\u202e]/g, ''))
    if (!match) return false
    settings.set('EvadeTargetX', Number(match[1]))
    settings.set('EvadeTargetY', Number(match[2]), true)
    return true
  }

  const state = !enabled ? 'apagado'
    : !hasTarget ? 'faltan coordenadas'
      : status?.stationed ? 'tropas con el aliado'
        : status && status.incoming > 0 ? `${status.incoming} ataque${status.incoming === 1 ? '' : 's'} en camino`
          : 'vigilando'
  const danger = enabled && (!hasTarget || (status?.incoming ?? 0) > 0 || status?.stationed)
  const distance = status?.villageX != null && status?.villageY != null && hasTarget
    ? Math.sqrt((status.villageX - x) ** 2 + (status.villageY - y) ** 2).toFixed(1)
    : null

  return (
    <Stack spacing={1.5} sx={{ ...panel, p: 2, border: '2px solid', borderColor: enabled ? '#b8281d' : '#c9b48a', boxShadow: enabled ? 'inset 4px 0 0 #b8281d' : 'none' }}>
      <Stack direction="row" alignItems="center" spacing={1.5} flexWrap="wrap" useFlexGap>
        <FormControlLabel
          sx={{ m: 0 }}
          control={<Checkbox checked={enabled} onChange={event => settings.set('EvadeEnable', event.target.checked ? 1 : 0, true)} sx={{ '& .MuiSvgIcon-root': { fontSize: 30 } }} />}
          label={
            <Stack direction="row" alignItems="center" spacing={0.75}>
              <ShieldIcon sx={{ color: '#b8281d' }} />
              <Typography sx={{ fontWeight: 800, fontSize: 18 }}>Evadir ataques con todas las tropas</Typography>
            </Stack>
          }
        />
        <Chip size="small" label={state} sx={danger ? { bgcolor: '#f6d5cf', color: '#b8281d', fontWeight: 700 } : undefined} color={!danger && enabled ? 'secondary' : 'default'} />
        <Box sx={{ flex: 1 }} />
        <Button variant="outlined" startIcon={<RefreshIcon />} disabled={!enabled || status?.running} onClick={checkNow}>Revisar ahora</Button>
      </Stack>

      <Typography variant="body2" color="text.secondary">
        Mira los ataques que vienen a esta aldea. Justo antes de que llegue el primero, manda todas las tropas que están en casa como refuerzo al aliado y cancela el refuerzo para que vuelvan apenas pasa el último ataque. Si es un tren muy largo, las tropas se quedan con el aliado y vuelven cuando paran los ataques.
      </Typography>

      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
        <Typography sx={{ fontWeight: 700 }}>Aldea del aliado</Typography>
        <TextField type="number" label="X" value={x} onChange={event => settings.set('EvadeTargetX', Number(event.target.value))}
          onPaste={event => { if (pasteTarget(event.clipboardData.getData('text'))) event.preventDefault() }} sx={{ width: 96 }} />
        <TextField type="number" label="Y" value={y} onChange={event => settings.set('EvadeTargetY', Number(event.target.value))}
          onPaste={event => { if (pasteTarget(event.clipboardData.getData('text'))) event.preventDefault() }} sx={{ width: 96 }} />
        {distance && <Typography color="text.secondary">a {distance} casillas</Typography>}
        <Hint text="Una aldea de alguien de tu alianza. Podés pegar las coordenadas como (12|-34). Queda guardada para esta aldea." />
      </Stack>

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: 1 }}>
        <TextField type="number" label="Salir antes del impacto (s)" value={values.EvadeLeadSeconds ?? 10} onChange={event => settings.set('EvadeLeadSeconds', Number(event.target.value))}
          helperText="Entre 3 y 60" />
        <TextField type="number" label="Tren: ataques separados hasta (s)" value={values.EvadeTrainGapSeconds ?? 60} onChange={event => settings.set('EvadeTrainGapSeconds', Number(event.target.value))}
          helperText="Si llegan más juntos, es un solo tren" />
        <TextField type="number" label="Volver después del último (s)" value={values.EvadeSafetySeconds ?? 5} onChange={event => settings.set('EvadeSafetySeconds', Number(event.target.value))}
          helperText="Margen de seguridad" />
        <TextField type="number" label="Revisar ataques cada (s)" value={values.EvadeCheckSeconds ?? 90} onChange={event => settings.set('EvadeCheckSeconds', Number(event.target.value))}
          helperText="Entre 30 y 1800" />
      </Box>
      <Stack direction="row" spacing={0.5} alignItems="center">
        <Switch size="small" checked={values.EvadeIncludeHero === 1} onChange={event => settings.set('EvadeIncludeHero', event.target.checked ? 1 : 0, true)} />
        <Typography>Mandar también al héroe si está en casa</Typography>
      </Stack>

      {enabled && (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 1 }}>
          <Info label="Próximo ataque" value={status?.nextAttackAt ? seconds(status.nextAttackAt) : 'ninguno'} detail={status?.nextAttackAt ? countdown(status.nextAttackAt) : ''} />
          <Info label="Ola actual" value={status && status.waveCount > 1 ? `tren de ${status.waveCount}` : status?.waveCount === 1 ? '1 ataque' : '—'}
            detail={status?.waveLastAt && status.waveCount > 1 ? `hasta las ${seconds(status.waveLastAt)}` : ''} />
          <Info label="Próxima revisión" value={status?.running ? 'ahora' : status?.nextCheckAt ? seconds(status.nextCheckAt) : '—'} detail={status?.checkedAt ? `última: ${seconds(status.checkedAt)}` : ''} />
          {status?.homeAt && <Info label="Tropas en casa a las" value={seconds(status.homeAt)} />}
        </Box>
      )}
      {enabled && status?.message && <Typography sx={{ fontWeight: 600 }}>{status.message}</Typography>}
      {enabled && (status?.history.length ?? 0) > 0 && (
        <Box sx={{ bgcolor: '#efe4c6', borderRadius: 1, px: 1.5, py: 1, maxHeight: 140, overflow: 'auto' }}>
          {status!.history.map((line, index) => <Typography key={index} variant="caption" component="div">{line}</Typography>)}
        </Box>
      )}
      <Typography variant="caption" color="text.secondary">
        Funciona mientras el bot tiene el navegador abierto (no durante el descanso). Travian deja cancelar un envío solo en sus primeros 90 segundos.
      </Typography>
    </Stack>
  )
}
