import { useEffect, useState } from 'react'
import { Box, Stack, Typography } from '@mui/material'
import { api, type AccountRow, type Meta } from '../api'
import { clanChat, describeClanCommand, parseClanCommand, roomOf } from '../chat/commands'
import { Empty, panel } from '../ui'

function statusLabel(status: string) {
  if (status === 'Online') return 'Conectada'
  if (status === 'Paused') return 'Pausada'
  if (status === 'Offline') return 'Desconectada'
  return status || '—'
}

function Value({ value }: { value: string }) {
  if (!value.includes('.') || value.includes(' ')) return value
  return value.split('.').map((part, index) => (
    <span key={`${part}-${index}`}>{index > 0 && <wbr />}{index > 0 ? `.${part}` : part}</span>
  ))
}

function Facts({ title, rows }: { title: string; rows: { label: string; value: string }[] }) {
  return (
    <Box sx={{ ...panel, minWidth: 0 }}>
      <Typography variant="h6" sx={{ color: '#3b2a17', textTransform: 'none', mb: 1.5 }}>{title}</Typography>
      <Stack spacing={1.25} sx={{ minWidth: 0 }}>
        {rows.map(row => (
          <Box key={row.label} sx={{ minWidth: 0 }}>
            <Typography variant="overline" sx={{ color: '#8a7350', letterSpacing: '0.08em' }}>{row.label}</Typography>
            <Typography sx={{ color: '#3b2a17', minWidth: 0, overflowWrap: 'anywhere' }}><Value value={row.value} /></Typography>
          </Box>
        ))}
      </Stack>
    </Box>
  )
}

type CommandRow = { id: string; username: string; body: string; created_at: string }

function when(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('es', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function Commands({ account }: { account: AccountRow }) {
  const host = account.host || ''
  const clan = account.clan?.trim() || ''
  const room = host && clan ? roomOf(host, clan) : ''
  const [rows, setRows] = useState<CommandRow[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    if (!room) return
    let alive = true
    const since = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString()
    clanChat.from('messages').select('id,username,body,created_at').eq('room', room).gte('created_at', since).order('created_at', { ascending: false }).limit(500)
      .then(({ data, error: loadError }) => {
        if (!alive) return
        if (loadError) {
          setError('No se pudieron leer los comandos del clan.')
          return
        }
        setRows(((data ?? []) as CommandRow[]).filter(row => parseClanCommand(row.body ?? '')))
      })
    const channel = clanChat.channel(`clan-commands:${room}`)
      .on('postgres_changes', { event: 'INSERT', schema: 'public', table: 'messages' }, payload => {
        const message = payload.new as CommandRow & { room?: string }
        if (message.room !== room || !parseClanCommand(message.body ?? '')) return
        setRows(current => current.some(row => row.id === message.id) ? current : [message, ...current])
      })
      .on('postgres_changes', { event: 'DELETE', schema: 'public', table: 'messages' }, payload => {
        const id = (payload.old as { id?: string }).id
        if (!id) return
        setRows(current => current.filter(row => row.id !== id))
      })
      .subscribe()
    return () => {
      alive = false
      void clanChat.removeChannel(channel)
    }
  }, [room])

  if (!room) return <Empty title="Sin alianza" text="Cuando la cuenta tenga servidor y alianza, acá van a aparecer los comandos del clan." />

  return (
    <Stack spacing={1.5} sx={{ minWidth: 0 }}>
      <Typography variant="h4">Comandos</Typography>
      <Typography color="text.secondary">Todo lo que el clan escribió como /stop, /resume o /help. Los mensajes del chat se borran al día, y con ellos estos comandos.</Typography>
      {error && <Typography color="error">{error}</Typography>}
      {!error && rows.length === 0 && <Empty title="Todavía no hay comandos" text="Cuando alguien del clan mande /stop o /resume, va a quedar en esta lista." />}
      {rows.length > 0 && (
        <Stack sx={{ ...panel, p: 1, minWidth: 0 }}>
          {rows.map(row => {
            const command = parseClanCommand(row.body ?? '')
            return (
              <Stack key={row.id} direction="row" spacing={1.5} sx={{ px: 1, py: 1, minWidth: 0, borderRadius: 1, '&:hover': { bgcolor: '#f1e5c2' } }}>
                <Typography sx={{ width: 132, flex: 'none', color: '#5c4529', fontSize: 13, pt: 0.25 }}>{when(row.created_at)}</Typography>
                <Box sx={{ minWidth: 0 }}>
                  <Typography sx={{ fontWeight: 700 }}>{row.username || '—'}</Typography>
                  <Typography sx={{ overflowWrap: 'anywhere' }}>{command ? describeClanCommand(command) : row.body}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{row.body}</Typography>
                </Box>
              </Stack>
            )
          })}
        </Stack>
      )}
    </Stack>
  )
}

export function Alliance({ account, meta, tab }: { account: AccountRow; meta: Meta | null; tab: 'datos' | 'comandos' }) {
  const [tribe, setTribe] = useState('—')

  useEffect(() => {
    let alive = true
    api<Record<string, number>>(`/api/accounts/${account.id}/settings`).then(result => {
      if (!alive) return
      const name = meta?.tribes.find(item => item.value === result.data.Tribe)?.name
      setTribe(name || '—')
    }).catch(() => {
      if (alive) setTribe('—')
    })
    return () => { alive = false }
  }, [account.id, meta])

  if (tab === 'comandos') return <Commands account={account} />

  return (
    <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Typography variant="h4">Alianza</Typography>
      <Facts
        title="Usuario"
        rows={[
          { label: 'Nombre', value: account.username || '—' },
          { label: 'Servidor', value: account.host || account.server || '—' },
          { label: 'Estado', value: statusLabel(account.status) },
          { label: 'Tribu', value: tribe },
        ]}
      />
      <Facts
        title="Alianza"
        rows={[{ label: 'Nombre', value: account.clan?.trim() || 'Sin datos' }]}
      />
    </Stack>
  )
}
