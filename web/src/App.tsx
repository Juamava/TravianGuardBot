import { useEffect, useState } from 'react'
import { Box, Button, Stack, Tooltip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import AgricultureIcon from '@mui/icons-material/Agriculture'
import BugReportIcon from '@mui/icons-material/BugReport'
import LinkIcon from '@mui/icons-material/Link'
import LocationCityIcon from '@mui/icons-material/LocationCity'
import LogoutIcon from '@mui/icons-material/Logout'
import PauseIcon from '@mui/icons-material/Pause'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import PowerSettingsNewIcon from '@mui/icons-material/PowerSettingsNew'
import RestartAltIcon from '@mui/icons-material/RestartAlt'
import SettingsIcon from '@mui/icons-material/Settings'
import ShieldIcon from '@mui/icons-material/Shield'
import type { SvgIconComponent } from '@mui/icons-material'
import { api, post, type AccountRow, type Meta, type Overview } from './api'
import { Accounts } from './pages/Accounts'
import { AccountSettings, Templates } from './pages/Settings'
import { Villages } from './pages/Villages'
import { Farming } from './pages/Farming'
import { Debug } from './pages/Debug'
import { Hero } from './pages/Hero'
import { Empty, UiProvider, useUi } from './ui'

type Page = 'aldeas' | 'farming' | 'hero' | 'debug' | 'ajustes' | 'cuentas'

const pages: { id: Page; label: string; icon: SvgIconComponent }[] = [
  { id: 'aldeas', label: 'Aldeas', icon: LocationCityIcon },
  { id: 'farming', label: 'Farming', icon: AgricultureIcon },
  { id: 'hero', label: 'Hero', icon: ShieldIcon },
  { id: 'debug', label: 'Debug', icon: BugReportIcon },
]

const actionButton = {
  minWidth: 0,
  height: 58,
  px: 0.5,
  display: 'flex',
  flexDirection: 'column',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 0.25,
  fontSize: 12,
  lineHeight: 1.2,
  whiteSpace: 'nowrap',
  color: '#dbdee1',
  bgcolor: '#313338',
  '&:hover': { bgcolor: '#35373c' },
  '&.Mui-disabled': { color: '#6d6f78' },
  '&.Mui-disabled .MuiSvgIcon-root': { color: '#6d6f78' },
} as const

function NavItem({ selected, disabled, icon: Icon, label, onClick, tone = 'normal' }: { selected?: boolean; disabled?: boolean; icon: SvgIconComponent; label: string; onClick: () => void; tone?: 'normal' | 'danger' }) {
  const color = tone === 'danger' ? '#ed4245' : selected ? '#fff' : '#949ba4'
  return (
    <Button
      disabled={disabled}
      onClick={onClick}
      startIcon={<Icon sx={{ fontSize: 20 }} />}
      sx={{
        justifyContent: 'flex-start',
        px: 1.25,
        color,
        bgcolor: selected ? '#404249' : 'transparent',
        '&:hover': { bgcolor: tone === 'danger' ? '#3a2426' : '#35373c', color: tone === 'danger' ? '#ed4245' : '#dbdee1' },
        '& .MuiButton-startIcon': { color: 'inherit', mr: 1 },
      }}
    >
      {label}
    </Button>
  )
}

function isConnected(status: string) {
  return status === 'Online' || status === 'Paused'
}

function statusColor(status: string) {
  if (status === 'Online') return '#23a559'
  if (status === 'Paused') return '#f0b232'
  return '#80848e'
}

function initials(name: string) {
  const letters = name.replace(/[^a-zA-Z0-9]/g, '')
  return (letters.slice(0, 2) || 'TG').toUpperCase()
}

function Shell() {
  const ui = useUi()
  const [overview, setOverview] = useState<Overview | null>(null)
  const [meta, setMeta] = useState<Meta | null>(null)
  const [accountId, setAccountId] = useState<number | null>(null)
  const [page, setPage] = useState<Page>('farming')

  async function refresh() {
    const result = await api<Overview>('/api/overview')
    setOverview(result.data)
    setAccountId(current => result.data.accounts.some(account => account.id === current) ? current : (result.data.accounts[0]?.id ?? null))
  }

  useEffect(() => {
    refresh().catch((error: Error) => ui.notify(error.message))
    api<Meta>('/api/meta').then(result => setMeta(result.data)).catch(() => undefined)
    const timer = window.setInterval(() => refresh().catch(() => undefined), 2000)
    return () => window.clearInterval(timer)
  }, [])

  const account = overview?.accounts.find(item => item.id === accountId) ?? null
  const holder = overview?.accounts.find(item => item.status !== 'Offline') ?? null
  const connected = account != null && isConnected(account.status)
  const heldByOther = holder != null && holder.id !== account?.id

  async function act(path: string) {
    if (!accountId) return
    try {
      const result = await post(`/api/accounts/${accountId}/${path}`)
      ui.notify(result.message)
      await refresh()
    } catch (error) {
      ui.notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  return (
    <Box sx={{ height: '100vh', display: 'grid', gridTemplateColumns: '72px 240px 1fr', overflow: 'hidden' }}>
      <Stack spacing={1.2} alignItems="center" sx={{ bgcolor: '#1e1f22', py: 1.5, overflow: 'auto' }}>
        {(overview?.accounts ?? []).map((item: AccountRow) => {
          const selected = item.id === accountId
          return (
            <Tooltip key={item.id} title={`${item.username} · ${item.host}`} placement="right">
              <Box
                onClick={() => setAccountId(item.id)}
                sx={{
                  width: 48,
                  height: 48,
                  borderRadius: selected ? '16px' : '50%',
                  bgcolor: selected ? '#5865f2' : '#313338',
                  color: '#fff',
                  display: 'grid',
                  placeItems: 'center',
                  fontWeight: 700,
                  cursor: 'pointer',
                  position: 'relative',
                  transition: 'border-radius .15s ease, background-color .15s ease',
                  '&:hover': { borderRadius: '16px', bgcolor: selected ? '#5865f2' : '#404249' },
                }}
              >
                {initials(item.username)}
                <Box sx={{ position: 'absolute', right: -2, bottom: -2, width: 14, height: 14, borderRadius: '50%', bgcolor: statusColor(item.status), border: '3px solid #1e1f22' }} />
              </Box>
            </Tooltip>
          )
        })}
        <Tooltip title="Cuentas" placement="right">
          <Box onClick={() => setPage('cuentas')} sx={{ width: 48, height: 48, borderRadius: page === 'cuentas' ? '16px' : '50%', bgcolor: page === 'cuentas' ? '#23a559' : '#313338', color: page === 'cuentas' ? '#fff' : '#23a559', display: 'grid', placeItems: 'center', cursor: 'pointer', '&:hover': { borderRadius: '16px', color: '#fff', bgcolor: '#23a559' } }}>
            <AddIcon />
          </Box>
        </Tooltip>
      </Stack>

      <Stack sx={{ bgcolor: '#2b2d31', minHeight: 0 }}>
        <Box sx={{ px: 2, py: 1.5, boxShadow: '0 1px 0 rgba(0,0,0,.2)' }}>
          <Typography sx={{ fontWeight: 700, color: '#f2f3f5', textTransform: 'none' }} noWrap>{account?.username ?? 'Travian Guard'}</Typography>
          <Typography variant="caption" color="text.secondary">{account ? `${account.host} · ${account.status}` : `Versión ${overview?.version ?? ''}`}</Typography>
        </Box>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 0.75, px: 1.5, py: 1.25 }}>
          <Button onClick={() => act('login')} disabled={!account || account.status !== 'Offline' || heldByOther} sx={actionButton}>
            <LinkIcon sx={{ fontSize: 18, color: '#5865f2' }} />
            Conectar
          </Button>
          <Button onClick={() => act('pause')} disabled={!connected} sx={actionButton}>
            {account?.status === 'Paused' ? <PlayArrowIcon sx={{ fontSize: 18, color: '#f0b232' }} /> : <PauseIcon sx={{ fontSize: 18, color: '#f0b232' }} />}
            {account?.pause ?? 'Pausar'}
          </Button>
          <Button onClick={() => act('logout')} disabled={holder?.id !== account?.id} sx={actionButton}>
            <LogoutIcon sx={{ fontSize: 18, color: '#ed4245' }} />
            Salir
          </Button>
          <Button onClick={() => act('restart')} disabled={!connected} sx={actionButton}>
            <RestartAltIcon sx={{ fontSize: 18, color: '#dbdee1' }} />
            Reiniciar
          </Button>
        </Box>
        <Stack spacing={0.3} sx={{ px: 1, pt: 1, overflow: 'auto', flex: 1 }}>
          {pages.map(item => (
            <NavItem key={item.id} selected={page === item.id} disabled={!connected} icon={item.icon} label={item.label} onClick={() => setPage(item.id)} />
          ))}
        </Stack>
        <Stack spacing={0.3} sx={{ px: 1, pb: 1.5 }}>
          <NavItem selected={page === 'ajustes'} disabled={!account} icon={SettingsIcon} label="Ajustes" onClick={() => setPage('ajustes')} />
          <NavItem tone="danger" icon={PowerSettingsNewIcon} label="Cerrar" onClick={() => post('/api/shutdown')} />
        </Stack>
      </Stack>

      <Box sx={{ overflow: 'auto', p: 3, bgcolor: '#313338', minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        {page === 'cuentas' && (
          <Stack spacing={3}>
            <Accounts notify={ui.notify} refresh={() => refresh().catch((error: Error) => ui.notify(error.message))} />
            <Templates notify={ui.notify} refresh={() => refresh().catch(() => undefined)} />
          </Stack>
        )}
        {page === 'ajustes' && accountId && <AccountSettings key={accountId} accountId={accountId} meta={meta} notify={ui.notify} refresh={() => refresh().catch((error: Error) => ui.notify(error.message))} />}
        {page === 'ajustes' && !accountId && <Empty title="Elegí una cuenta" text="Creá una cuenta con el + y después vas a poder cambiar el servidor, la contraseña y el resto desde acá." />}
        {page !== 'cuentas' && page !== 'ajustes' && connected && accountId && (
          <>
            {page === 'hero' && <Hero key={accountId} accountId={accountId} notify={ui.notify} />}
            {page === 'aldeas' && <Villages key={accountId} accountId={accountId} meta={meta} notify={ui.notify} />}
            {page === 'farming' && <Farming key={accountId} accountId={accountId} notify={ui.notify} />}
            {page === 'debug' && <Debug key={accountId} accountId={accountId} notify={ui.notify} />}
          </>
        )}
        {page !== 'cuentas' && page !== 'ajustes' && !connected && (
          <Empty
            title={heldByOther && holder ? `${holder.username} está conectada en este bot` : 'Conectá la cuenta'}
            text={heldByOther && holder
              ? `Las aldeas y el farming de ${holder.username} pertenecen a esta instancia. Para administrar ${account?.username ?? 'otra cuenta'}, abrí otro bot.`
              : 'Las aldeas, el farming y el resto aparecen recién cuando esta cuenta está conectada. Para otra cuenta, abrí otra instancia del bot.'}
          />
        )}
      </Box>
    </Box>
  )
}

export default function App() {
  return (
    <UiProvider>
      <Shell />
    </UiProvider>
  )
}
