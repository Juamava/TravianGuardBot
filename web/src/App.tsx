import { useEffect, useRef, useState } from 'react'
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
import { Scouting } from './pages/Scouting'
import { ClanChat } from './chat/ClanChat'
import { Debug } from './pages/Debug'
import { Hero } from './pages/Hero'
import { Empty, UiProvider, useUi } from './ui'
import { crest, landscape, parchmentTexture, woodTexture } from './art'

type Page = 'aldeas' | 'aldeas-tropas' | 'aldeas-comercio' | 'farming' | 'farming-juego' | 'farming-scout' | 'hero' | 'debug' | 'ajustes' | 'cuentas'

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
  fontSize: 13,
  lineHeight: 1.2,
  whiteSpace: 'nowrap',
  color: '#eadbb8',
  bgcolor: '#4e321a',
  '&:hover': { bgcolor: '#5a3b1f' },
  '&.Mui-disabled': { color: '#8c7656' },
  '&.Mui-disabled .MuiSvgIcon-root': { color: '#8c7656' },
} as const

function LiveLed() {
  return (
    <Box
      component="span"
      aria-label="En ejecución"
      sx={{
        width: 8,
        height: 8,
        flexShrink: 0,
        borderRadius: '50%',
        bgcolor: '#7cb32a',
        boxShadow: '0 0 0 3px rgba(35,165,89,.18)',
        animation: 'navLed 1.1s ease-in-out infinite',
        '@keyframes navLed': { '0%, 100%': { opacity: 1 }, '50%': { opacity: 0.2 } },
        '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
      }}
    />
  )
}

function formatNext(at: string | null | undefined, now: number) {
  if (!at) return ''
  const target = Date.parse(at)
  if (!Number.isFinite(target)) return ''
  const remaining = target - now
  if (remaining <= 1000) return 'ahora'
  const total = Math.ceil(remaining / 1000)
  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60
  if (hours > 0) return `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

function sooner(left?: string | null, right?: string | null) {
  const a = left ? Date.parse(left) : Number.POSITIVE_INFINITY
  const b = right ? Date.parse(right) : Number.POSITIVE_INFINITY
  if (!Number.isFinite(a) && !Number.isFinite(b)) return null
  return (Number.isFinite(a) ? a : Number.POSITIVE_INFINITY) <= (Number.isFinite(b) ? b : Number.POSITIVE_INFINITY) ? left ?? null : right ?? null
}

function NextCount({ text }: { text: string }) {
  if (!text) return null
  return (
    <Box component="span" title={`Próxima acción en ${text}`} sx={{ color: '#7cb32a', fontSize: 13, fontWeight: 700, fontVariantNumeric: 'tabular-nums', lineHeight: 1, flexShrink: 0 }}>
      {text}
    </Box>
  )
}

function NavItem({ selected, disabled, icon: Icon, label, onClick, tone = 'normal', live = false, count = '' }: { selected?: boolean; disabled?: boolean; icon: SvgIconComponent; label: string; onClick: () => void; tone?: 'normal' | 'danger'; live?: boolean; count?: string }) {
  const color = tone === 'danger' ? '#e0563f' : selected ? '#fff' : '#c8b088'
  return (
    <Button
      disabled={disabled}
      onClick={onClick}
      startIcon={<Icon sx={{ fontSize: 20 }} />}
      sx={{
        justifyContent: 'flex-start',
        width: '100%',
        px: 1.25,
        color,
        bgcolor: selected ? '#6e4a24' : 'transparent',
        ...(selected ? { background: 'linear-gradient(90deg, rgba(226,191,110,.35), rgba(160,122,68,.15))', boxShadow: 'inset 3px 0 0 #e2bf6e, inset 0 0 0 1px rgba(226,191,110,.35)', textShadow: '0 1px 2px rgba(0,0,0,.6)' } : {}),
        '&:hover': { bgcolor: tone === 'danger' ? '#5a1f14' : '#5a3b1f', color: tone === 'danger' ? '#e0563f' : '#eadbb8' },
        '& .MuiButton-startIcon': { color: 'inherit', mr: 1 },
      }}
    >
      <Box component="span" sx={{ flex: 1, textAlign: 'left' }}>{label}</Box>
      <Box component="span" sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexShrink: 0 }}>
        {live && <NextCount text={count} />}
        {live && <LiveLed />}
      </Box>
    </Button>
  )
}

function SubNav({ selected, disabled, label, onClick, live = false, count = '' }: { selected?: boolean; disabled?: boolean; label: string; onClick: () => void; live?: boolean; count?: string }) {
  return (
    <Button
      disabled={disabled}
      onClick={onClick}
      sx={{
        justifyContent: 'flex-start',
        width: '100%',
        minHeight: 32,
        py: 0.35,
        pl: 4.25,
        pr: 1.25,
        fontSize: 13,
        fontWeight: selected ? 600 : 500,
        color: selected ? '#fff' : '#c8b088',
        bgcolor: selected ? '#6e4a24' : 'transparent',
        ...(selected ? { background: 'linear-gradient(90deg, rgba(226,191,110,.35), rgba(160,122,68,.15))', boxShadow: 'inset 3px 0 0 #e2bf6e, inset 0 0 0 1px rgba(226,191,110,.35)', textShadow: '0 1px 2px rgba(0,0,0,.6)' } : {}),
        '&:hover': { bgcolor: '#5a3b1f', color: '#eadbb8' },
      }}
    >
      <Box component="span" sx={{ flex: 1, textAlign: 'left' }}>{label}</Box>
      <Box component="span" sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexShrink: 0 }}>
        {live && <NextCount text={count} />}
        {live && <LiveLed />}
      </Box>
    </Button>
  )
}

function isConnected(status: string) {
  return status === 'Online' || status === 'Paused'
}

function statusColor(status: string) {
  if (status === 'Online') return '#7cb32a'
  if (status === 'Paused') return '#e7b440'
  return '#8c7656'
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
  const [update, setUpdate] = useState<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string; phase?: string; percent?: number; progress?: string } | null>(null)
  const [updateDue, setUpdateDue] = useState(false)
  const [checkingUpdate, setCheckingUpdate] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const updatePoll = useRef<number | null>(null)
  useEffect(() => () => {
    if (updatePoll.current !== null) window.clearInterval(updatePoll.current)
  }, [])

  async function refresh(selectId?: number) {
    const result = await api<Overview>('/api/overview')
    setOverview(result.data)
    setAccountId(current => {
      if (selectId != null && result.data.accounts.some(account => account.id === selectId)) return selectId
      return result.data.accounts.some(account => account.id === current) ? current : (result.data.accounts[0]?.id ?? null)
    })
  }

  useEffect(() => {
    refresh().catch((error: Error) => ui.notify(error.message))
    api<Meta>('/api/meta').then(result => setMeta(result.data)).catch(() => undefined)
    api<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string; phase?: string; percent?: number; progress?: string }>('/api/update').then(result => setUpdate(result.data)).catch(() => undefined)
    const timer = window.setInterval(() => refresh().catch(() => undefined), 2000)
    const clock = window.setInterval(() => setNow(Date.now()), 1000)
    return () => {
      window.clearInterval(timer)
      window.clearInterval(clock)
    }
  }, [])

  const account = overview?.accounts.find(item => item.id === accountId) ?? null
  useEffect(() => {
    if (page === 'farming-juego' && account && !account.gold) setPage('farming')
  }, [account, page])
  const holder = overview?.accounts.find(item => item.status !== 'Offline') ?? null
  const connected = account != null && isConnected(account.status)
  const villagesLive = !!account?.villages
  const farmsLive = !!account?.farms
  const gameLive = !!account?.gameFarms
  const scoutLive = !!account?.scouts
  const villageCount = villagesLive ? formatNext(account?.villageNext, now) : ''
  const farmCount = farmsLive ? formatNext(account?.farmNext, now) : ''
  const gameCount = gameLive ? formatNext(account?.gameFarmNext, now) : ''
  const scoutCount = scoutLive ? formatNext(account?.scoutNext, now) : ''
  const heldByOther = holder != null && holder.id !== account?.id
  const canConnect = account != null && account.status === 'Offline' && !heldByOther
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
  const updateBusy = update?.phase === 'checking' || update?.phase === 'downloading' || update?.phase === 'extracting' || update?.phase === 'installing'
  const updateLabel = checkingUpdate ? 'Buscando...' : updateBusy ? (update?.progress || 'Descargando...') : update?.available && !mandatory ? `Actualizar a ${update.latest}` : 'Buscar actualización'

  async function searchUpdate() {
    setCheckingUpdate(true)
    try {
      const result = await api<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string; phase?: string; percent?: number; progress?: string }>('/api/update?force=true')
      setUpdate(result.data)
      if (!result.data.available) ui.notify(result.data.detail || 'Ya está al día.')
    } catch (error) {
      ui.notify(error instanceof Error ? error.message : 'No se pudo buscar la actualización')
    } finally {
      setCheckingUpdate(false)
    }
  }

  function watchUpdate() {
    if (updatePoll.current !== null) window.clearInterval(updatePoll.current)
    let reading = false
    const read = async () => {
      if (reading) return
      reading = true
      try {
        const result = await api<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string; phase?: string; percent?: number; progress?: string }>('/api/update')
        setUpdate(result.data)
        if (result.data.phase === 'failed') {
          if (updatePoll.current !== null) window.clearInterval(updatePoll.current)
          updatePoll.current = null
          ui.notify(result.data.progress || 'No se pudo actualizar')
        }
      } catch {
        // The app is about to close. The next tick retries.
      } finally {
        reading = false
      }
    }
    void read()
    updatePoll.current = window.setInterval(() => { void read() }, 1000)
  }

  async function install(forced: boolean) {
    if (!forced && !await ui.confirm(`Se descarga la versión ${update?.latest ?? ''} mientras el bot sigue. Al terminar se cierra unos segundos para instalar y se vuelve a abrir.`)) return
    try {
      ui.notify((await post('/api/update')).message)
      watchUpdate()
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
      <Stack spacing={1.2} alignItems="center" sx={{ bgcolor: '#241609', backgroundImage: `linear-gradient(rgba(20,10,3,.55), rgba(20,10,3,.55)), ${woodTexture}`, backgroundSize: 'auto, 400px 240px', borderRight: '2px solid #a07a44', py: 1.5, overflow: 'auto' }}>
        <Tooltip title="Travian Guard" placement="right">
          <Box component="img" src={crest} alt="" sx={{ width: 46, height: 46, filter: 'drop-shadow(0 2px 3px rgba(0,0,0,.6))', mb: 0.5 }} />
        </Tooltip>
        <Box sx={{ width: 36, height: 2, bgcolor: '#a07a44', borderRadius: 1, opacity: .6 }} />
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
                  bgcolor: selected ? '#6c9a1d' : '#4e321a',
                  color: '#fff',
                  display: 'grid',
                  placeItems: 'center',
                  fontWeight: 700,
                  cursor: 'pointer',
                  position: 'relative',
                  transition: 'border-radius .15s ease, background-color .15s ease',
                  '&:hover': { borderRadius: '16px', bgcolor: selected ? '#6c9a1d' : '#6e4a24' },
                }}
              >
                {initials(item.username)}
                <Box sx={{ position: 'absolute', right: -2, bottom: -2, width: 14, height: 14, borderRadius: '50%', bgcolor: statusColor(item.status), border: '3px solid #241609' }} />
              </Box>
            </Tooltip>
          )
        })}
        <Tooltip title="Cuentas" placement="right">
          <Box onClick={() => setPage('cuentas')} sx={{ width: 48, height: 48, borderRadius: page === 'cuentas' ? '16px' : '50%', bgcolor: page === 'cuentas' ? '#7cb32a' : '#4e321a', color: page === 'cuentas' ? '#fff' : '#7cb32a', display: 'grid', placeItems: 'center', cursor: 'pointer', '&:hover': { borderRadius: '16px', color: '#fff', bgcolor: '#7cb32a' } }}>
            <AddIcon />
          </Box>
        </Tooltip>
      </Stack>

      <Stack sx={{ bgcolor: '#3a2513', backgroundImage: `linear-gradient(90deg, rgba(0,0,0,.25), rgba(0,0,0,0) 12%, rgba(0,0,0,0) 88%, rgba(0,0,0,.3)), ${woodTexture}`, backgroundSize: 'auto, 400px 240px', minHeight: 0 }}>
        <Box sx={{ px: 2, py: 1.5, background: 'linear-gradient(#4a2e15, #2e1c0b)', borderBottom: '2px solid #a07a44', boxShadow: '0 2px 6px rgba(0,0,0,.45)' }}>
          <Typography sx={{ fontWeight: 700, color: '#f6ead0', textTransform: 'none' }} noWrap>{account?.username ?? 'Travian Guard'}</Typography>
          <Typography component="div" variant="caption" color="text.secondary" sx={{ mt: 0.25, lineHeight: 1.45 }}>
            {account && <Box sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{account.host}</Box>}
            <Box>
              {account ? `${account.status} · ${overview?.version ?? ''}` : `Versión ${overview?.version ?? ''}`}
              {' · '}
              <Box
                component="button"
                type="button"
                disabled={checkingUpdate || updateBusy}
                onClick={() => (update?.available && !mandatory ? install(false) : searchUpdate())}
                sx={{
                  p: 0,
                  border: 0,
                  bgcolor: 'transparent',
                  font: 'inherit',
                  lineHeight: 'inherit',
                  cursor: 'pointer',
                  color: update?.available && !mandatory ? '#d8f0a6' : 'inherit',
                  textDecoration: 'underline',
                  textUnderlineOffset: '2px',
                  '&:hover': { color: '#fff' },
                  '&:disabled': { cursor: 'default', color: '#8c7656', textDecoration: 'none' },
                }}
              >
                {updateLabel}
              </Box>
            </Box>
          </Typography>
        </Box>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 0.75, px: 1.5, py: 1.25 }}>
          <Button onClick={() => act('login')} disabled={!canConnect} sx={{ ...actionButton, ...(canConnect ? { color: '#7cb32a' } : {}) }}>
            <LinkIcon sx={{ fontSize: 18, color: 'inherit' }} />
            Conectar
          </Button>
          <Button onClick={() => act('pause')} disabled={!connected} sx={actionButton}>
            {account?.status === 'Paused' ? <PlayArrowIcon sx={{ fontSize: 18, color: '#e7b440' }} /> : <PauseIcon sx={{ fontSize: 18, color: '#e7b440' }} />}
            {account?.pause ?? 'Pausar'}
          </Button>
          <Button onClick={() => act('logout')} disabled={holder?.id !== account?.id} sx={actionButton}>
            <LogoutIcon sx={{ fontSize: 18, color: '#e0563f' }} />
            Salir
          </Button>
          <Button onClick={() => act('restart')} disabled={!connected} sx={actionButton}>
            <RestartAltIcon sx={{ fontSize: 18, color: '#eadbb8' }} />
            Reiniciar
          </Button>
        </Box>
        <Stack spacing={0.3} sx={{ px: 1, pt: 1, overflow: 'auto', flex: 1 }}>
          {pages.map(item => {
            if (item.id === 'aldeas') {
              return (
                <Stack key="aldeas" spacing={0.15}>
                  <NavItem selected={false} disabled={!connected} live={villagesLive} count={villageCount} icon={item.icon} label="Aldeas" onClick={() => setPage('aldeas')} />
                  <SubNav selected={page === 'aldeas'} disabled={!connected} label="Construcción" onClick={() => setPage('aldeas')} />
                  <SubNav selected={page === 'aldeas-tropas'} disabled={!connected} label="Tropas" onClick={() => setPage('aldeas-tropas')} />
                  <SubNav selected={page === 'aldeas-comercio'} disabled={!connected} label="Comercio" onClick={() => setPage('aldeas-comercio')} />
                </Stack>
              )
            }
            if (item.id === 'farming') {
              return (
                <Stack key="farming" spacing={0.15}>
                  <NavItem selected={false} disabled={!connected} live={!account?.gold && (farmsLive || scoutLive)} count={!account?.gold ? formatNext(sooner(account?.farmNext, account?.scoutNext), now) : ''} icon={item.icon} label="Farming" onClick={() => setPage('farming')} />
                  <SubNav selected={page === 'farming'} disabled={!connected} live={farmsLive} count={farmCount} label="Listas propias" onClick={() => setPage('farming')} />
                  {account?.gold && <SubNav selected={page === 'farming-juego'} disabled={!connected} live={gameLive} count={gameCount} label="Listas del juego" onClick={() => setPage('farming-juego')} />}
                  <SubNav selected={page === 'farming-scout'} disabled={!connected} live={scoutLive} count={scoutCount} label="Scouting" onClick={() => setPage('farming-scout')} />
                </Stack>
              )
            }
            return <NavItem key={item.id} selected={page === item.id} disabled={!connected} icon={item.icon} label={item.label} onClick={() => setPage(item.id)} />
          })}
        </Stack>
        <Stack spacing={0.3} sx={{ px: 1, pb: 1.5 }}>
          <NavItem selected={page === 'ajustes'} disabled={!account} icon={SettingsIcon} label="Ajustes" onClick={() => setPage('ajustes')} />
          <NavItem tone="danger" icon={PowerSettingsNewIcon} label="Cerrar" onClick={() => post('/api/shutdown')} />
        </Stack>
      </Stack>

      <Box sx={{ overflow: 'auto', p: 3, minHeight: 0, display: 'flex', flexDirection: 'column', color: '#3b2a17', bgcolor: '#f4ecd6', backgroundImage: `linear-gradient(rgba(244,236,214,0) 70px, rgba(244,236,214,.85) 150px, #f4ecd6 190px), ${landscape}, ${parchmentTexture}`, backgroundSize: '100% 190px, 100% 190px, 300px 300px', backgroundRepeat: 'no-repeat, no-repeat, repeat', backgroundAttachment: 'local, local, local', pt: '110px', borderLeft: '6px solid #6b4423', boxShadow: 'inset 0 0 0 2px #a07a44, inset 0 0 40px rgba(110,74,36,.25)' }}>
        {page === 'cuentas' && (
          <Stack spacing={3}>
            <Accounts notify={ui.notify} refresh={id => { void refresh(id).catch((error: Error) => ui.notify(error.message)) }} />
            <Templates notify={ui.notify} refresh={() => refresh().catch(() => undefined)} />
          </Stack>
        )}
        {page === 'ajustes' && accountId && <AccountSettings key={accountId} accountId={accountId} meta={meta} notify={ui.notify} refresh={() => refresh().catch((error: Error) => ui.notify(error.message))} />}
        {page === 'ajustes' && !accountId && <Empty title="Elegí una cuenta" text="Creá una cuenta con el + y después vas a poder cambiar el servidor, la contraseña y el resto desde acá." />}
        {page !== 'cuentas' && page !== 'ajustes' && connected && accountId && (
          <>
            {page === 'hero' && <Hero key={accountId} accountId={accountId} notify={ui.notify} />}
            {(page === 'aldeas' || page === 'aldeas-tropas' || page === 'aldeas-comercio') && <Villages key={accountId} accountId={accountId} meta={meta} notify={ui.notify} tab={page === 'aldeas-tropas' ? 'troops' : page === 'aldeas-comercio' ? 'trade' : 'build'} />}
            {(page === 'farming' || page === 'farming-juego') && <Farming key={`${accountId}-${page}`} accountId={accountId} notify={ui.notify} mode={page === 'farming-juego' ? 'game' : 'bot'} />}
            {page === 'farming-scout' && <Scouting key={accountId} accountId={accountId} notify={ui.notify} />}
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
      {account?.clan && account.host && <ClanChat username={account.username} host={account.host} clan={account.clan} page={page} />}
      {mandatory && update && (
        <Box sx={{ position: 'fixed', inset: 0, zIndex: 30, bgcolor: 'rgba(0,0,0,.72)', display: 'grid', placeItems: 'center', p: 3 }}>
          <Stack spacing={2} sx={{ width: 420, maxWidth: '100%', bgcolor: '#3a2513', borderRadius: 2, p: 3 }}>
            <Typography variant="h5">Hay que instalar la versión {update.latest}</Typography>
            <Typography color="text.secondary">Ya pasó un día desde que salió. Se descarga ahora y el bot se cierra unos segundos para instalarla.</Typography>
            <Button variant="contained" disabled={updateBusy} onClick={() => install(true)}>{updateBusy ? (update.progress || 'Descargando...') : 'Actualizar ahora'}</Button>
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
