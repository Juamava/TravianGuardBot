import { useEffect, useState } from 'react'
import { Alert, Box, Button, Snackbar, Stack, Typography } from '@mui/material'
import { api, post, type AccountRow, type Meta, type Overview } from './api'
import { Accounts } from './pages/Accounts'
import { AccountSettings, Templates } from './pages/Settings'
import { Villages } from './pages/Villages'
import { Farming } from './pages/Farming'
import { Debug } from './pages/Debug'
import { Hero } from './pages/Hero'

const pages = [
  ['cuentas', 'Cuentas'],
  ['ajustes', 'Ajustes'],
  ['aldeas', 'Aldeas'],
  ['farming', 'Farming'],
  ['hero', 'Hero'],
  ['debug', 'Debug'],
] as const

type Page = typeof pages[number][0]

function dot(status: string) {
  if (status === 'Online') return '#58CC02'
  if (status === 'Paused') return '#FFC800'
  return '#CFCFCF'
}

export default function App() {
  const [overview, setOverview] = useState<Overview | null>(null)
  const [meta, setMeta] = useState<Meta | null>(null)
  const [accountId, setAccountId] = useState<number | null>(null)
  const [page, setPage] = useState<Page>('farming')
  const [message, setMessage] = useState('')

  async function refresh() {
    const result = await api<Overview>('/api/overview')
    setOverview(result.data)
    setAccountId(current => result.data.accounts.some(account => account.id === current) ? current : (result.data.accounts[0]?.id ?? null))
  }

  useEffect(() => {
    refresh().catch((error: Error) => setMessage(error.message))
    api<Meta>('/api/meta').then(result => setMeta(result.data)).catch(() => undefined)
    const timer = window.setInterval(() => refresh().catch(() => undefined), 2000)
    return () => window.clearInterval(timer)
  }, [])

  const account = overview?.accounts.find(item => item.id === accountId) ?? null

  async function act(path: string) {
    if (!accountId) return
    try {
      const result = await post(`/api/accounts/${accountId}/${path}`)
      setMessage(result.message)
      await refresh()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', gridTemplateColumns: '280px 1fr' }}>
      <Stack spacing={1.5} sx={{ p: 2, bgcolor: '#58CC02', color: '#fff' }}>
        <Typography variant="h5">Travian Guard</Typography>
        <Typography fontWeight={700}>Versión {overview?.version}</Typography>
        <Button variant="contained" color="secondary" onClick={() => act('login')} disabled={!account}>Conectar</Button>
        <Stack direction="row" spacing={1}>
          <Button variant="contained" color="warning" onClick={() => act('pause')} disabled={!account}>{account?.pause ?? 'Pausar'}</Button>
          <Button variant="contained" sx={{ bgcolor: '#fff', color: '#3C3C3C' }} onClick={() => act('logout')} disabled={!account}>Salir</Button>
        </Stack>
        <Button variant="contained" sx={{ bgcolor: '#fff', color: '#3C3C3C' }} onClick={() => act('restart')} disabled={!account}>Reiniciar</Button>
        <Stack spacing={1} sx={{ flex: 1, overflow: 'auto' }}>
          {(overview?.accounts ?? []).map((item: AccountRow) => (
            <Button key={item.id} onClick={() => setAccountId(item.id)} sx={{ justifyContent: 'flex-start', bgcolor: item.id === accountId ? '#fff' : 'rgba(255,255,255,.25)', color: item.id === accountId ? '#3C3C3C' : '#fff' }}>
              <Box sx={{ width: 12, height: 12, borderRadius: '50%', bgcolor: dot(item.status), mr: 1 }} />
              <Box sx={{ textAlign: 'left' }}>
                <div>{item.username}</div>
                <Typography variant="caption">{item.host} · {item.status}</Typography>
              </Box>
            </Button>
          ))}
        </Stack>
        <Button variant="contained" color="error" onClick={() => post('/api/shutdown')}>Cerrar</Button>
      </Stack>
      <Stack spacing={2} sx={{ p: 3, overflow: 'auto' }}>
        <Stack direction="row" spacing={1} flexWrap="wrap">
          {pages.map(([id, label]) => (
            <Button key={id} variant={page === id ? 'contained' : 'outlined'} onClick={() => setPage(id)}>{label}</Button>
          ))}
        </Stack>
        {page === 'hero' && <Hero />}
        {page === 'cuentas' && <Accounts accountId={accountId} notify={setMessage} refresh={() => refresh().catch((error: Error) => setMessage(error.message))} />}
        {page === 'cuentas' && <Templates notify={setMessage} refresh={() => refresh().catch(() => undefined)} />}
        {page === 'ajustes' && accountId && <AccountSettings accountId={accountId} meta={meta} notify={setMessage} />}
        {page === 'aldeas' && accountId && <Villages accountId={accountId} meta={meta} notify={setMessage} />}
        {page === 'farming' && accountId && <Farming accountId={accountId} notify={setMessage} />}
        {page === 'debug' && accountId && <Debug accountId={accountId} notify={setMessage} />}
        {page !== 'cuentas' && page !== 'hero' && !accountId && <Typography>Agregá una cuenta para empezar.</Typography>}
      </Stack>
      <Snackbar open={Boolean(message)} autoHideDuration={4000} onClose={() => setMessage('')}>
        <Alert severity="info" onClose={() => setMessage('')}>{message}</Alert>
      </Snackbar>
    </Box>
  )
}
