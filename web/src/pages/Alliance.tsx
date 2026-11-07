import { useEffect, useState } from 'react'
import { Box, Stack, Typography } from '@mui/material'
import { api, type AccountRow, type Meta } from '../api'
import { panel } from '../ui'

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

export function Alliance({ account, meta }: { account: AccountRow; meta: Meta | null }) {
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
