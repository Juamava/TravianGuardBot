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

type Page = 'aldeas' | 'farming' | 'farming-juego' | 'hero' | 'debug' | 'ajustes' | 'cuentas'

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

function SubNav({ selected, disabled, label, onClick }: { selected?: boolean; disabled?: boolean; label: string; onClick: () => void }) {
  return (
    <Button
      disabled={disabled}
      onClick={onClick}
      sx={{
        justifyContent: 'flex-start',
        minHeight: 32,
        py: 0.35,
        pl: 4.25,
        pr: 1.25,
        fontSize: 13,
        fontWeight: selected ? 600 : 500,
        color: selected ? '#fff' : '#949ba4',
        bgcolor: selected ? '#404249' : 'transparent',
        '&:hover': { bgcolor: '#35373c', color: '#dbdee1' },
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
  const [update, setUpdate] = useState<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string } | null>(null)
  const [updateDue, setUpdateDue] = useState(false)
  const [checkingUpdate, setCheckingUpdate] = useState(false)

  async function refresh() {
    const result = await api<Overview>('/api/overview')
    setOverview(result.data)
    setAccountId(current => result.data.accounts.some(account => account.id === current) ? current : (result.data.accounts[0]?.id ?? null))
  }

  useEffect(() => {
    refresh().catch((error: Error) => ui.notify(error.message))
    api<Meta>('/api/meta').then(result => setMeta(result.data)).catch(() => undefined)
    api<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string }>('/api/update').then(result => setUpdate(result.data)).catch(() => undefined)
    const timer = window.setInterval(() => refresh().catch(() => undefined), 2000)
    return () => window.clearInterval(timer)
  }, [])

  const account = overview?.accounts.find(item => item.id === accountId) ?? null
  useEffect(() => {
    if (page === 'farming-juego' && account && !account.gold) setPage('farming')
  }, [account, page])
  const holder = overview?.accounts.find(item => item.status !== 'Offline') ?? null
  const connected = account != null && isConnected(account.status)
  const heldByOther = holder != null && holder.id !== account?.id
  useEffect(() => {
    if (!update?.available || !update.publishedAt) return
    const published = Date.parse(update.publishedAt)
    if (!Number.isFinite(published)) return
    const remaining = published + 24 * 60 * 60 * 1000 - Date.now()
    if (remaining <= 0) {
      setUpdateDue(true)
      return
    }
    const timer = window.setTimeout(() => setUpdateDue(true), remaining)
    return () => window.clearTimeout(timer)
  }, [update?.available, update?.publishedAt])
  const mandatory = !!update?.available && (update.mandatory || updateDue)

  async function searchUpdate() {
    setCheckingUpdate(true)
    try {
      const result = await api<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string }>('/api/update?force=true')
      setUpdate(result.data)
      if (!result.data.available) ui.notify(result.data.detail || 'Ya está al día.')
    } catch (error) {
      ui.notify(error instanceof Error ? error.message : 'No se pudo buscar la actualización')
    } finally {
      setCheckingUpdate(false)
    }
  }

  async function install(forced: boolean) {
    if (!forced && !await ui.confirm(`Se cierra el bot, se instala la versión ${update?.latest ?? ''} y se vuelve a abrir. Las cuentas y el navegador no se tocan. Si hay una cuenta conectada, desconectala antes.`)) return
    try {
      if (forced && holder && holder.status !== 'Offline') await post(`/api/accounts/${holder.id}/logout`)
      ui.notify((await post('/api/update')).message)
    } catch (error) {
      ui.notify(error instanceof Error ? error.message : 'No se pudo actualizar')
    }
  }

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
          <Typography component="div" variant="caption" color="text.secondary" sx={{ mt: 0.25, lineHeight: 1.45 }}>
            {account && <Box sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{account.host}</Box>}
            <Box>
              {account ? `${account.status} · ${overview?.version ?? ''}` : `Versión ${overview?.version ?? ''}`}
              {' · '}
              <Box
                component="button"
                type="button"
                disabled={checkingUpdate}
                onClick={() => (update?.available && !mandatory ? install(false) : searchUpdate())}
                sx={{
                  p: 0,
                  border: 0,
                  bgcolor: 'transparent',
                  font: 'inherit',
                  lineHeight: 'inherit',
                  cursor: 'pointer',
                  color: update?.available && !mandatory ? '#dee0fc' : 'inherit',
                  textDecoration: 'underline',
                  textUnderlineOffset: '2px',
                  '&:hover': { color: '#fff' },
                  '&:disabled': { cursor: 'default', color: '#6d6f78', textDecoration: 'none' },
                }}
              >
                {checkingUpdate ? 'Buscando...' : update?.available && !mandatory ? `Actualizar a ${update.latest}` : 'Buscar actualización'}
              </Box>
            </Box>
          </Typography>
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
          {pages.map(item => item.id === 'farming' && account?.gold ? (
            <Stack key="farming" spacing={0.15}>
              <NavItem selected={false} disabled={!connected} icon={item.icon} label="Farming" onClick={() => setPage('farming')} />
              <SubNav selected={page === 'farming'} disabled={!connected} label="Listas propias" onClick={() => setPage('farming')} />
              <SubNav selected={page === 'farming-juego'} disabled={!connected} label="Listas del juego" onClick={() => setPage('farming-juego')} />
            </Stack>
          ) : (
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
            {(page === 'farming' || page === 'farming-juego') && <Farming key={`${accountId}-${page}`} accountId={accountId} notify={ui.notify} mode={page === 'farming-juego' ? 'game' : 'bot'} />}
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
      {mandatory && update && (
        <Box sx={{ position: 'fixed', inset: 0, zIndex: 30, bgcolor: 'rgba(0,0,0,.72)', display: 'grid', placeItems: 'center', p: 3 }}>
          <Stack spacing={2} sx={{ width: 420, maxWidth: '100%', bgcolor: '#2b2d31', borderRadius: 2, p: 3 }}>
            <Typography variant="h5">Hay que instalar la versión {update.latest}</Typography>
            <Typography color="text.secondary">Ya pasó un día desde que salió. Esta versión no sigue en uso. Las cuentas y el navegador no se tocan.</Typography>
            <Button variant="contained" onClick={() => install(true)}>Actualizar ahora</Button>
          </Stack>
        </Box>
      )}
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
