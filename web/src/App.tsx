import { useEffect, useRef, useState } from 'react'
import { Box, Button, Stack, Tooltip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import CloseIcon from '@mui/icons-material/Close'
import CropSquareIcon from '@mui/icons-material/CropSquare'
import FilterNoneIcon from '@mui/icons-material/FilterNone'
import RemoveIcon from '@mui/icons-material/Remove'
import AgricultureIcon from '@mui/icons-material/Agriculture'
import GroupsIcon from '@mui/icons-material/Groups'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'
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
import { Admin } from './pages/Admin'
import { isAdminAccess } from './admin/access'
import { Hero } from './pages/Hero'
import { Alliance } from './pages/Alliance'
import { Empty, UiProvider, useUi } from './ui'
import banner from './bannerapp.jpg'
import { crest, parchmentTexture, woodTexture } from './art'
import { GlobalStyles, IconButton, useMediaQuery } from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'

// Inside the Electron window the native title bar is hidden and this app draws its own.
const inElectron = /Electron/i.test(navigator.userAgent)

type TbsWindow = { minimize: () => void; toggleMaximize: () => void; close: () => void; onMaximized: (callback: (value: boolean) => void) => void }
const tbsWindow = (window as unknown as { tbsWindow?: TbsWindow }).tbsWindow

function readNight() {
  try { return localStorage.getItem('tg-night') === '1' } catch { return false }
}

/** Day or night switch drawn as a wooden track with a sun and a moon medallion. */
function DayNightToggle({ night, onChange }: { night: boolean; onChange: (value: boolean) => void }) {
  return (
    <Box
      component="button"
      type="button"
      title={night ? 'Que salga el sol' : 'Que caiga la noche'}
      aria-label={night ? 'Modo día' : 'Modo noche'}
      onClick={() => onChange(!night)}
      sx={{ position: 'relative', width: 64, height: 24, p: 0, border: '1px solid #a07a44', borderRadius: 99, cursor: 'pointer', WebkitAppRegion: 'no-drag', overflow: 'hidden', background: night ? 'linear-gradient(#0b1530, #2b3a66)' : 'linear-gradient(#8fc3e0, #e8d9a6)', boxShadow: 'inset 0 1px 3px rgba(0,0,0,.5)', transition: 'background .4s' }}
    >
      <Box component="svg" viewBox="0 0 64 24" sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: night ? 1 : 0, transition: 'opacity .4s' }}>
        <circle cx="10" cy="7" r="1" fill="#fff8e0" /><circle cx="18" cy="15" r=".8" fill="#fff8e0" /><circle cx="25" cy="6" r="1.1" fill="#fff8e0" /><circle cx="14" cy="19" r=".7" fill="#fff8e0" />
      </Box>
      <Box sx={{ position: 'absolute', top: 1, left: night ? 41 : 1, width: 20, height: 20, borderRadius: '50%', display: 'grid', placeItems: 'center', background: 'radial-gradient(circle at 35% 30%, #f7e3a6, #b88a3e 70%, #6b4423)', border: '1px solid #4a2e15', boxShadow: '0 1px 3px rgba(0,0,0,.6)', transition: 'left .35s cubic-bezier(.5,1.6,.5,1)' }}>
        {night
          ? <svg width="14" height="14" viewBox="0 0 24 24"><path d="M15 3a9 9 0 1 0 6 15A8 8 0 0 1 15 3z" fill="#1f2a4d" stroke="#0b1530" /><circle cx="18" cy="6" r="1.2" fill="#1f2a4d" /></svg>
          : <svg width="16" height="16" viewBox="0 0 24 24"><g stroke="#8a3d0a" strokeWidth="2" strokeLinecap="round">{[0, 45, 90, 135, 180, 225, 270, 315].map(angle => <line key={angle} x1="12" y1="2.5" x2="12" y2="5.5" transform={`rotate(${angle} 12 12)`} />)}</g><circle cx="12" cy="12" r="5" fill="#e86b12" stroke="#8a3d0a" /></svg>}
      </Box>
    </Box>
  )
}

function WindowButtons() {
  const [maximized, setMaximized] = useState(true)
  useEffect(() => tbsWindow?.onMaximized(setMaximized), [])
  if (!tbsWindow) return null
  const button = { width: 46, height: '100%', display: 'grid', placeItems: 'center', color: '#f1e1b8', cursor: 'pointer', WebkitAppRegion: 'no-drag', transition: 'background-color .12s', '&:hover': { bgcolor: 'rgba(241,225,184,.15)' } }
  return (
    <Box sx={{ display: 'flex', height: '100%', WebkitAppRegion: 'no-drag' }}>
      <Box title="Minimizar" onClick={tbsWindow.minimize} sx={button}><RemoveIcon sx={{ fontSize: 18 }} /></Box>
      <Box title={maximized ? 'Restaurar' : 'Maximizar'} onClick={tbsWindow.toggleMaximize} sx={button}>{maximized ? <FilterNoneIcon sx={{ fontSize: 14 }} /> : <CropSquareIcon sx={{ fontSize: 16 }} />}</Box>
      <Box title="Cerrar" onClick={tbsWindow.close} sx={{ ...button, '&:hover': { bgcolor: '#b8281d', color: '#fff' } }}><CloseIcon sx={{ fontSize: 18 }} /></Box>
    </Box>
  )
}

type Page = 'aldeas' | 'aldeas-tropas' | 'aldeas-comercio' | 'farming' | 'farming-juego' | 'farming-scout' | 'hero' | 'alianza' | 'admin' | 'admin-debug' | 'ajustes' | 'cuentas'

const pages: { id: Page; label: string; icon: SvgIconComponent }[] = [
  { id: 'aldeas', label: 'Aldeas', icon: LocationCityIcon },
  { id: 'farming', label: 'Farming', icon: AgricultureIcon },
  { id: 'hero', label: 'Hero', icon: ShieldIcon },
  { id: 'alianza', label: 'Alianza', icon: GroupsIcon },
  { id: 'admin', label: 'Administración', icon: AdminPanelSettingsIcon },
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
  const narrow = useMediaQuery('(max-width:900px)')
  const [navOpen, setNavOpen] = useState(false)
  const [night, setNightState] = useState(readNight)
  const setNight = (value: boolean) => {
    setNightState(value)
    try { localStorage.setItem('tg-night', value ? '1' : '0') } catch { /* the choice just does not persist */ }
  }
  const pageColor = night ? '#d6c094' : '#f4ecd6'
  const headerMask = 'linear-gradient(to bottom, #000 0px, #000 70px, transparent 190px)'
  const [showAdmin, setShowAdmin] = useState(false)
  const adminToggleAt = useRef(0)
  const adminAllowedRef = useRef(false)
  const [update, setUpdate] = useState<{ current: string; latest: string; available: boolean; mandatory: boolean; publishedAt: string; detail: string; phase?: string; percent?: number; progress?: string } | null>(null)
  const [updateDue, setUpdateDue] = useState(false)
  const [checkingUpdate, setCheckingUpdate] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const updatePoll = useRef<number | null>(null)
  useEffect(() => () => {
    if (updatePoll.current !== null) window.clearInterval(updatePoll.current)
  }, [])

  useEffect(() => {
    const toggle = () => {
      const at = Date.now()
      if (at - adminToggleAt.current < 80) return
      adminToggleAt.current = at
      if (!adminAllowedRef.current) return
      setShowAdmin(visible => !visible)
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.repeat || event.altKey || event.metaKey || event.shiftKey || !event.ctrlKey) return
      if (event.code !== 'KeyD' && event.key.toLowerCase() !== 'd') return
      event.preventDefault()
      toggle()
    }
    window.addEventListener('keydown', onKey, true)
    window.addEventListener('travian-toggle-debug', toggle)
    return () => {
      window.removeEventListener('keydown', onKey, true)
      window.removeEventListener('travian-toggle-debug', toggle)
    }
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
  const [knownAccess, setKnownAccess] = useState<string[]>([])
  useEffect(() => {
    if (!accountId || account?.accesses) return
    let alive = true
    setKnownAccess([])
    api<{ accesses?: { username?: string }[] }>(`/api/accounts/${accountId}`)
      .then(result => { if (alive) setKnownAccess((result.data.accesses ?? []).map(item => item.username || '').filter(Boolean)) })
      .catch(() => { if (alive) setKnownAccess([]) })
    return () => { alive = false }
  }, [accountId, Boolean(account?.accesses)])
  const adminAllowed = account?.status === 'Online' && isAdminAccess([...(account?.accesses ?? []), ...knownAccess])
  adminAllowedRef.current = adminAllowed
  useEffect(() => {
    if ((!showAdmin || !adminAllowed) && (page === 'admin' || page === 'admin-debug')) setPage('farming')
  }, [showAdmin, adminAllowed, page])
  useEffect(() => {
    if (page === 'farming-juego' && account && !account.gold) setPage('farming')
  }, [account, page])
  const holder = overview?.accounts.find(item => item.status !== 'Offline') ?? null
  const connected = account != null && isConnected(account.status)
  const buildLive = !!account?.build
  const troopsLive = !!account?.troops
  const tradeLive = !!account?.trade
  const farmsLive = !!account?.farms
  const gameLive = !!account?.gameFarms
  const scoutLive = !!account?.scouts
  const adventureLive = !!account?.adventures
  const buildCount = buildLive ? formatNext(account?.buildNext, now) : ''
  const troopCount = troopsLive ? formatNext(account?.troopNext, now) : ''
  const tradeCount = tradeLive ? formatNext(account?.tradeNext, now) : ''
  const farmCount = farmsLive ? formatNext(account?.farmNext, now) : ''
  const gameCount = gameLive ? formatNext(account?.gameFarmNext, now) : ''
  const scoutCount = scoutLive ? formatNext(account?.scoutNext, now) : ''
  const adventureCount = adventureLive ? formatNext(account?.adventureNext, now) : ''
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
    <Box sx={{ height: '100vh', display: 'grid', gridTemplateColumns: narrow ? '60px minmax(0, 1fr)' : '72px 240px 1fr', gridTemplateRows: '34px minmax(0, 1fr)', overflow: 'hidden', position: 'relative' }}>
      <GlobalStyles styles={{ ':root': night ? { '--tg-panel': '#efe0ba', '--tg-input': '#f7ebcb' } : { '--tg-panel': '#fdf9ec', '--tg-input': '#fffdf4' } }} />
      {(
        <Box sx={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: 1, pl: narrow ? 0.5 : 1.5, height: 34, background: `linear-gradient(rgba(60,34,12,.55), rgba(30,16,5,.75)), ${woodTexture}`, backgroundSize: 'auto, 400px 240px', borderBottom: '2px solid #a07a44', WebkitAppRegion: 'drag', userSelect: 'none' }}>
          {narrow && <IconButton size="small" aria-label="Menú" onClick={() => setNavOpen(open => !open)} sx={{ color: '#f1e1b8', WebkitAppRegion: 'no-drag' }}><MenuIcon fontSize="small" /></IconButton>}
          <Box component="img" src={crest} alt="" sx={{ width: 20, height: 20 }} />
          <Typography sx={{ fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, fontSize: 13, color: '#f1e1b8', textShadow: '0 1px 2px rgba(0,0,0,.6)' }}>Travian Guard</Typography>
          <Box sx={{ flex: 1 }} />
          <DayNightToggle night={night} onChange={setNight} />
          <Box sx={{ width: 8 }} />
          {inElectron && <WindowButtons />}
        </Box>
      )}
      <Stack spacing={1.2} alignItems="center" sx={{ bgcolor: '#241609', backgroundImage: `linear-gradient(rgba(20,10,3,${night ? '.72' : '.55'}), rgba(20,10,3,${night ? '.72' : '.55'})), ${woodTexture}`, backgroundSize: 'auto, 400px 240px', borderRight: '2px solid #a07a44', py: 1.5, overflow: 'auto' }}>
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

      {narrow && navOpen && <Box onClick={() => setNavOpen(false)} sx={{ position: 'absolute', inset: '34px 0 0 60px', zIndex: 19, bgcolor: 'rgba(20,10,3,.45)' }} />}
      <Stack onClick={event => { if (narrow && (event.target as HTMLElement).closest('button')) setNavOpen(false) }} sx={{ ...(narrow ? { position: 'absolute', top: 34, bottom: 0, left: 60, width: 250, zIndex: 20, boxShadow: '6px 0 18px rgba(0,0,0,.5)', display: navOpen ? 'flex' : 'none' } : {}), bgcolor: '#3a2513', backgroundImage: `${night ? 'radial-gradient(ellipse at 50% 0%, rgba(255,170,70,.22), rgba(255,170,70,0) 55%), linear-gradient(rgba(8,10,25,.45), rgba(8,10,25,.45)), ' : ''}linear-gradient(90deg, rgba(0,0,0,.25), rgba(0,0,0,0) 12%, rgba(0,0,0,0) 88%, rgba(0,0,0,.3)), ${woodTexture}`, backgroundSize: night ? 'auto, auto, auto, 400px 240px' : 'auto, 400px 240px', minHeight: 0 }}>
        <Box sx={{ px: 2, py: 1.5, background: 'linear-gradient(#4a2e15, #2e1c0b)', borderBottom: '2px solid #a07a44', boxShadow: '0 2px 6px rgba(0,0,0,.45)' }}>
          <Typography sx={{ fontWeight: 700, color: '#f6ead0', textTransform: 'none' }} noWrap>{account?.username ?? 'Travian Guard'}</Typography>
          <Typography component="div" variant="caption" sx={{ mt: 0.25, lineHeight: 1.45, color: '#e6d3a8', fontSize: 12.5 }}>
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
          {pages.filter(item => item.id !== 'admin' || (showAdmin && adminAllowed)).map(item => {
            if (item.id === 'aldeas') {
              return (
                <Stack key="aldeas" spacing={0.15}>
                  <NavItem selected={false} disabled={!connected} icon={item.icon} label="Aldeas" onClick={() => setPage('aldeas')} />
                  <SubNav selected={page === 'aldeas'} disabled={!connected} live={buildLive} count={buildCount} label="Construcción" onClick={() => setPage('aldeas')} />
                  <SubNav selected={page === 'aldeas-tropas'} disabled={!connected} live={troopsLive} count={troopCount} label="Tropas" onClick={() => setPage('aldeas-tropas')} />
                  <SubNav selected={page === 'aldeas-comercio'} disabled={!connected} live={tradeLive} count={tradeCount} label="Comercio" onClick={() => setPage('aldeas-comercio')} />
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
            if (item.id === 'admin') {
              return (
                <Stack key="admin" spacing={0.15}>
                  <NavItem selected={false} disabled={!adminAllowed} icon={item.icon} label="Administración" onClick={() => setPage('admin')} />
                  <SubNav selected={page === 'admin-debug'} disabled={!adminAllowed} label="Debug" onClick={() => setPage('admin-debug')} />
                  <SubNav selected={page === 'admin'} disabled={!adminAllowed} label="Administración" onClick={() => setPage('admin')} />
                </Stack>
              )
            }
            return <NavItem key={item.id} selected={page === item.id} disabled={item.id === 'alianza' ? !account : !connected} live={item.id === 'hero' && adventureLive} count={item.id === 'hero' ? adventureCount : ''} icon={item.icon} label={item.label} onClick={() => setPage(item.id)} />
          })}
        </Stack>
        <Stack spacing={0.3} sx={{ px: 1, pb: 1.5 }}>
          <NavItem selected={page === 'ajustes'} disabled={!account} icon={SettingsIcon} label="Ajustes" onClick={() => setPage('ajustes')} />
          <NavItem tone="danger" icon={PowerSettingsNewIcon} label="Cerrar" onClick={() => post('/api/shutdown')} />
        </Stack>
      </Stack>

      <Box sx={{ overflow: 'auto', p: narrow ? 1.5 : 3, minHeight: 0, minWidth: 0, display: 'flex', flexDirection: 'column', color: '#3b2a17', bgcolor: pageColor, backgroundImage: `${night ? 'radial-gradient(ellipse at 50% 35%, rgba(255,214,140,.35), rgba(60,38,15,.35) 95%), ' : ''}${parchmentTexture}`, backgroundSize: night ? '100% 100%, 300px 300px' : '300px 300px', backgroundRepeat: night ? 'no-repeat, repeat' : 'repeat', backgroundAttachment: 'local', position: 'relative', isolation: 'isolate', pt: '110px', borderLeft: '6px solid #6b4423', boxShadow: 'inset 0 0 0 2px #a07a44, inset 0 0 40px rgba(110,74,36,.25)' }}>
        <Box aria-hidden sx={{ position: 'absolute', top: 0, left: 0, right: 0, height: 190, zIndex: -1, pointerEvents: 'none', backgroundImage: `linear-gradient(to bottom, transparent 0px, transparent 70px, ${pageColor} 190px), url(${banner})`, backgroundSize: '100% 100%, cover', backgroundPosition: 'center top, center 42%', backgroundRepeat: 'no-repeat', maskImage: headerMask, WebkitMaskImage: headerMask }} />
        {page === 'cuentas' && (
          <Stack spacing={3}>
            <Accounts notify={ui.notify} refresh={id => { void refresh(id).catch((error: Error) => ui.notify(error.message)) }} />
            <Templates notify={ui.notify} refresh={() => refresh().catch(() => undefined)} />
          </Stack>
        )}
        {page === 'ajustes' && accountId && <AccountSettings key={accountId} accountId={accountId} meta={meta} notify={ui.notify} refresh={() => refresh().catch((error: Error) => ui.notify(error.message))} />}
        {page === 'ajustes' && !accountId && <Empty title="Elegí una cuenta" text="Creá una cuenta con el + y después vas a poder cambiar el servidor, la contraseña y el resto desde acá." />}
        {page === 'alianza' && account && <Alliance key={account.id} account={account} meta={meta} />}
        {page === 'alianza' && !account && <Empty title="Elegí una cuenta" text="Creá una cuenta con el + para ver sus datos y los de la alianza." />}
        {page !== 'cuentas' && page !== 'ajustes' && page !== 'alianza' && connected && accountId && (
          <>
            {page === 'hero' && <Hero key={accountId} accountId={accountId} notify={ui.notify} />}
            {(page === 'aldeas' || page === 'aldeas-tropas' || page === 'aldeas-comercio') && <Villages key={accountId} accountId={accountId} meta={meta} notify={ui.notify} tab={page === 'aldeas-tropas' ? 'troops' : page === 'aldeas-comercio' ? 'trade' : 'build'} />}
            {(page === 'farming' || page === 'farming-juego') && <Farming key={`${accountId}-${page}`} accountId={accountId} notify={ui.notify} mode={page === 'farming-juego' ? 'game' : 'bot'} />}
            {page === 'farming-scout' && <Scouting key={accountId} accountId={accountId} notify={ui.notify} />}
            {showAdmin && adminAllowed && page === 'admin' && <Admin notify={ui.notify} />}
            {showAdmin && adminAllowed && page === 'admin-debug' && <Debug key={accountId} accountId={accountId} notify={ui.notify} />}
          </>
        )}
        {page !== 'cuentas' && page !== 'ajustes' && page !== 'alianza' && !connected && (
          <Empty
            title={heldByOther && holder ? `${holder.username} está conectada en este bot` : 'Conectá la cuenta'}
            text={heldByOther && holder
              ? `Las aldeas y el farming de ${holder.username} pertenecen a esta instancia. Para administrar ${account?.username ?? 'otra cuenta'}, abrí otro bot.`
              : 'Las aldeas, el farming y el resto aparecen recién cuando esta cuenta está conectada. Para otra cuenta, abrí otra instancia del bot.'}
          />
        )}
      </Box>
      {account?.clan && account.host && <ClanChat username={account.username} host={account.host} clan={account.clan} page={page} accountId={account.id} />}
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
