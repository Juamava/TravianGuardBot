import { useEffect, useRef, useState } from 'react'
import { Box, Button, Stack, TextField, Typography } from '@mui/material'
import { api, del, emptyAccess, post, put, type Access, type AccountDetail } from '../api'
import { useUi } from '../ui'

type EditorProps = {
  accountId: number | null
  notify: (message: string) => void
  refresh: () => void
}

export function AccountEditor({ accountId, notify, refresh }: EditorProps) {
  const ui = useUi()
  const editing = accountId !== null
  const [username, setUsername] = useState('')
  const [server, setServer] = useState('')
  const [accesses, setAccesses] = useState<Access[]>([emptyAccess()])
  const draft = useRef({ username: '', server: '', accesses: [emptyAccess()] })
  const saved = useRef('')
  const ready = useRef(false)

  useEffect(() => {
    ready.current = false
    if (!accountId) return
    api<AccountDetail>(`/api/accounts/${accountId}`).then(result => {
      const next = {
        username: result.data.username,
        server: result.data.server,
        accesses: result.data.accesses.length ? result.data.accesses : [emptyAccess()],
      }
      draft.current = next
      saved.current = JSON.stringify(next)
      ready.current = true
      setUsername(next.username)
      setServer(next.server)
      setAccesses(next.accesses)
    }).catch((error: Error) => notify(error.message))
  }, [accountId])

  function updateAccess(index: number, patch: Partial<Access>) {
    const next = accesses.map((access, i) => i === index ? { ...access, ...patch } : access)
    draft.current = { ...draft.current, accesses: next }
    setAccesses(next)
  }

  async function commit() {
    if (!accountId || !ready.current) return
    const body = draft.current
    const text = JSON.stringify(body)
    if (text === saved.current) return
    try {
      const result = await put(`/api/accounts/${accountId}`, body)
      saved.current = text
      notify(result.message)
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo guardar')
    }
  }

  async function create() {
    try {
      const result = await post('/api/accounts', { username, server, accesses })
      notify(result.message)
      setUsername('')
      setServer('')
      setAccesses([emptyAccess()])
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo agregar')
    }
  }

  async function remove() {
    if (!accountId || !(await ui.confirm('¿Borrar esta cuenta y sus aldeas?'))) return
    try {
      const result = await del(`/api/accounts/${accountId}`)
      notify(result.message)
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo borrar')
    }
  }

  return (
    <Stack spacing={2}>
      <Typography color="text.secondary">Nombre, servidor y los accesos para entrar. El proxy y el user agent son opcionales.</Typography>
      <Stack spacing={1.5} sx={{ p: 2, borderRadius: 2, bgcolor: '#2b2d31' }}>
        <TextField label="Nombre" value={username} onChange={event => { setUsername(event.target.value); draft.current = { ...draft.current, username: event.target.value } }} onBlur={commit} />
        <TextField label="Servidor" placeholder="https://ts1.x1.international.travian.com" value={server} onChange={event => { setServer(event.target.value); draft.current = { ...draft.current, server: event.target.value } }} onBlur={commit} />
      </Stack>
      {accesses.map((access, index) => (
        <Box key={index} sx={{ p: 2, borderRadius: 2, bgcolor: '#2b2d31' }}>
          <Stack spacing={1.5}>
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <Typography fontWeight={700}>Acceso {index + 1}</Typography>
              {accesses.length > 1 && <Button size="small" color="error" onClick={() => {
                const next = accesses.filter((_, i) => i !== index)
                draft.current = { ...draft.current, accesses: next }
                setAccesses(next)
                void commit()
              }}>Quitar</Button>}
            </Stack>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
              <TextField fullWidth label="Usuario" value={access.username} onChange={event => updateAccess(index, { username: event.target.value })} onBlur={commit} />
              <TextField fullWidth label="Contraseña" type="password" value={access.password} onChange={event => updateAccess(index, { password: event.target.value })} onBlur={commit} />
            </Stack>
            <Typography variant="caption" color="text.secondary">Proxy, si la cuenta entra por uno.</Typography>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
              <TextField fullWidth label="Proxy" value={access.proxyHost} onChange={event => updateAccess(index, { proxyHost: event.target.value })} onBlur={commit} />
              <TextField fullWidth label="Puerto" type="number" value={access.proxyPort || ''} onChange={event => updateAccess(index, { proxyPort: Number(event.target.value) })} onBlur={commit} />
              <TextField fullWidth label="Usuario proxy" value={access.proxyUsername} onChange={event => updateAccess(index, { proxyUsername: event.target.value })} onBlur={commit} />
              <TextField fullWidth label="Contraseña proxy" value={access.proxyPassword} onChange={event => updateAccess(index, { proxyPassword: event.target.value })} onBlur={commit} />
            </Stack>
            <TextField label="User agent" value={access.useragent} onChange={event => updateAccess(index, { useragent: event.target.value })} onBlur={commit} />
          </Stack>
        </Box>
      ))}
      <Stack direction="row" spacing={1} flexWrap="wrap">
        <Button variant="outlined" onClick={() => {
          const next = [...accesses, emptyAccess()]
          draft.current = { ...draft.current, accesses: next }
          setAccesses(next)
        }}>Otro acceso</Button>
        {!editing && <Button variant="contained" onClick={create}>Agregar</Button>}
        {editing && <Button variant="contained" color="error" onClick={remove}>Borrar cuenta</Button>}
      </Stack>
    </Stack>
  )
}

export function Accounts({ notify, refresh }: { notify: (message: string) => void; refresh: () => void }) {
  const [bulk, setBulk] = useState('')

  async function saveBulk() {
    try {
      const result = await post('/api/accounts/bulk', { text: bulk })
      notify(result.message)
      setBulk('')
      refresh()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo guardar')
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4">Nueva cuenta</Typography>
      <AccountEditor accountId={null} notify={notify} refresh={refresh} />
      <Typography variant="h5">Varias cuentas</Typography>
      <Typography color="text.secondary">Una por línea: url usuario contraseña. Si hay proxy: url usuario contraseña host puerto [usuario contraseña].</Typography>
      <TextField multiline minRows={4} value={bulk} onChange={event => setBulk(event.target.value)} />
      <Button variant="contained" color="secondary" onClick={saveBulk} sx={{ alignSelf: 'flex-start' }}>Agregar la lista</Button>
    </Stack>
  )
}
