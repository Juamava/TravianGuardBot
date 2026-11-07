import { useEffect, useState } from 'react'
import { Box, IconButton, Stack, Tooltip, Typography } from '@mui/material'
import AgricultureIcon from '@mui/icons-material/Agriculture'
import LocationCityIcon from '@mui/icons-material/LocationCity'
import PauseIcon from '@mui/icons-material/Pause'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import SystemUpdateAltIcon from '@mui/icons-material/SystemUpdateAlt'
import { sendOrder, watchClients, type OnlineClient, type OrderAction, type OrderFeature } from '../admin/orders'
import { Empty, panel, useUi } from '../ui'

const actions: { action: OrderAction; feature: OrderFeature; tip: string; color: string; icon: 'pause' | 'play' | 'farm' | 'build' | 'update' }[] = [
  { action: 'stop', feature: 'all', tip: 'Pausar el bot', color: '#b8281d', icon: 'pause' },
  { action: 'resume', feature: 'all', tip: 'Reanudar el bot', color: '#4f8a10', icon: 'play' },
  { action: 'stop', feature: 'farming', tip: 'Detener el farming', color: '#b8281d', icon: 'farm' },
  { action: 'resume', feature: 'farming', tip: 'Reanudar el farming', color: '#4f8a10', icon: 'farm' },
  { action: 'stop', feature: 'building', tip: 'Detener la construcción', color: '#b8281d', icon: 'build' },
  { action: 'resume', feature: 'building', tip: 'Reanudar la construcción', color: '#4f8a10', icon: 'build' },
  { action: 'update', feature: 'all', tip: 'Actualizar el cliente', color: '#a86f08', icon: 'update' },
]

function ActionIcon({ kind }: { kind: typeof actions[number]['icon'] }) {
  if (kind === 'pause') return <PauseIcon fontSize="small" />
  if (kind === 'play') return <PlayArrowIcon fontSize="small" />
  if (kind === 'farm') return <AgricultureIcon fontSize="small" />
  if (kind === 'build') return <LocationCityIcon fontSize="small" />
  return <SystemUpdateAltIcon fontSize="small" />
}

export function Admin({ notify }: { notify: (message: string) => void }) {
  const ui = useUi()
  const [people, setPeople] = useState<OnlineClient[]>([])
  const [busy, setBusy] = useState('')

  useEffect(() => watchClients(setPeople), [])

  async function run(person: OnlineClient, action: OrderAction, feature: OrderFeature, tip: string) {
    const reason = (await ui.prompt(`${tip} de ${person.username}`, 'Razón'))?.trim() ?? ''
    if (!reason) {
      notify('La razón es obligatoria.')
      return
    }
    const key = `${person.host}|${person.username}|${action}|${feature}`
    setBusy(key)
    try {
      await sendOrder({ id: crypto.randomUUID(), username: person.username, host: person.host, action, feature, reason, by: 'Administración' })
      notify(`Orden enviada a ${person.username}.`)
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo enviar la orden.')
    } finally {
      setBusy('')
    }
  }

  return (
    <Stack spacing={1.5} sx={{ minWidth: 0 }}>
      <Typography variant="h4">Administración</Typography>
      <Typography color="text.secondary">Clientes con el chat abierto, de este clan o de cualquier otro. La orden no se escribe en el chat: a la otra persona solo le aparece la ventana.</Typography>
      {people.length === 0 && <Empty title="Nadie en el chat" text="Cuando un cliente abre el chat, aparece en esta lista." />}
      {people.length > 0 && (
        <Stack sx={{ ...panel, p: 1, minWidth: 0 }}>
          {people.map(person => (
            <Stack key={`${person.host}|${person.username}`} direction="row" spacing={1} alignItems="center" sx={{ px: 1, py: 0.75, minWidth: 0, borderRadius: 1, '&:hover': { bgcolor: '#f1e5c2' } }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>{person.username}</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{person.clan || 'Sin alianza'} · {person.host}</Typography>
              </Box>
              <Stack direction="row" spacing={0.25} sx={{ flexShrink: 0 }}>
                {actions.map(action => {
                  const key = `${person.host}|${person.username}|${action.action}|${action.feature}`
                  return (
                    <Tooltip key={key} title={action.tip}>
                      <span>
                        <IconButton size="small" disabled={busy === key} aria-label={`${action.tip} de ${person.username}`} onClick={() => { void run(person, action.action, action.feature, action.tip) }} sx={{ color: action.color }}>
                          <ActionIcon kind={action.icon} />
                        </IconButton>
                      </span>
                    </Tooltip>
                  )
                })}
              </Stack>
            </Stack>
          ))}
        </Stack>
      )}
    </Stack>
  )
}
