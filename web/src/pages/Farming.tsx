import { useEffect, useState } from 'react'
import { Box, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { api, del, post, put } from '../api'

type Village = { id: number; name: string; x: number; y: number }
type CowList = { id: number; name: string; minMinutes: number; repeatMinutes: number }
type Cow = { id: number; listId: number; url: string; troops: string; status: string }
type FarmingData = { villages: Village[]; lists: CowList[]; cows: Cow[]; countdown: string }
type SearchResult = { count: number; villageName: string; originX: number; originY: number; troopPlan: string; hits: { x: number; y: number; distance: number }[] }

export function Farming({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
  const [data, setData] = useState<FarmingData | null>(null)
  const [villageId, setVillageId] = useState(0)
  const [listId, setListId] = useState(0)
  const [name, setName] = useState('')
  const [minMinutes, setMinMinutes] = useState(0)
  const [repeatMinutes, setRepeatMinutes] = useState(0)
  const [cowId, setCowId] = useState(0)
  const [url, setUrl] = useState('')
  const [troops, setTroops] = useState('')
  const [adding, setAdding] = useState(false)

  async function load(nextVillage = villageId) {
    const result = await api<FarmingData>(`/api/accounts/${accountId}/farming?villageId=${nextVillage}`)
    setData(result.data)
    if (!nextVillage && result.data.villages[0]) setVillageId(result.data.villages[0].id)
    return result.data
  }

  useEffect(() => {
    load().catch((error: Error) => notify(error.message))
    const timer = window.setInterval(() => load().catch(() => undefined), 1000)
    return () => window.clearInterval(timer)
  }, [accountId, villageId])

  function selectList(list: CowList) {
    setListId(list.id)
    setName(list.name)
    setMinMinutes(list.minMinutes)
    setRepeatMinutes(list.repeatMinutes)
    setCowId(0)
    setAdding(false)
    setUrl('')
    setTroops('')
  }

  async function run(action: () => Promise<{ message: string }>) {
    try {
      notify((await action()).message)
      await load()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    }
  }

  const cows = data?.cows.filter(cow => cow.listId === listId) ?? []
  const countdown = data?.countdown ?? ''

  return (
    <Stack spacing={2}>
      <Box sx={{ p: 2, borderRadius: 4, background: 'linear-gradient(135deg, #FFD84D, #FFB703)', color: '#3C3C3C' }}>
        <Typography variant="h5">Hero</Typography>
        <Typography fontWeight={800}>Coming soon</Typography>
      </Box>
      <Typography variant="h4">Listas de vacas</Typography>
      <TextField select label="Aldea" value={villageId || ''} onChange={event => { setVillageId(Number(event.target.value)); setListId(0) }}>
        {(data?.villages ?? []).map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
      </TextField>
      <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2} alignItems="stretch">
        <Stack spacing={1} sx={{ flex: 1 }}>
          {(data?.lists ?? []).map(list => (
            <Button key={list.id} variant={list.id === listId ? 'contained' : 'outlined'} onClick={() => selectList(list)}>{list.name}</Button>
          ))}
          <TextField label="Nombre de la lista" value={name} onChange={event => setName(event.target.value)} />
          <Stack direction="row" spacing={1}>
            <Button onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists`, { villageId, name }))}>Crear</Button>
            <Button disabled={!listId} onClick={() => run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes }))}>Renombrar</Button>
            <Button color="error" disabled={!listId} onClick={() => run(() => del(`/api/accounts/${accountId}/farming/lists/${listId}`))}>Borrar</Button>
          </Stack>
        </Stack>
        <Stack spacing={1} sx={{ flex: 1.4 }}>
          {cows.map(cow => (
            <Button key={cow.id} variant={cow.id === cowId ? 'contained' : 'text'} onClick={() => { setCowId(cow.id); setUrl(cow.url); setTroops(cow.troops); setAdding(false) }} sx={{ justifyContent: 'space-between' }}>
              <span>{cow.troops} {cow.url}</span>
              <span>{cow.status}</span>
            </Button>
          ))}
          {cows.length === 0 && <Typography color="text.secondary">Esta lista todavía no tiene aldeas.</Typography>}
        </Stack>
        <Stack spacing={1.5} sx={{ flex: 1, p: 2, borderRadius: 3, bgcolor: '#fff', border: '2px solid #F0E6D6' }}>
          {cowId > 0 || adding ? (
            <>
              <Typography fontWeight={800}>{adding ? 'Nueva aldea' : 'Esta vaca'}</Typography>
              <Typography color="text.secondary">Letras iguales en todas las razas: A D G / B E H / C F. Un número solo usa A.</Typography>
              <TextField label="Enlace" value={url} onChange={event => setUrl(event.target.value)} />
              <TextField label="Tropas" placeholder="2 o A:2 D:1" value={troops} onChange={event => setTroops(event.target.value)} />
              <Button variant="contained" onClick={() => run(() => adding
                ? post(`/api/accounts/${accountId}/farming/lists/${listId}/villages`, { url, troops })
                : put(`/api/accounts/${accountId}/farming/villages/${cowId}`, { url, troops }))}>{adding ? 'Agregar' : 'Guardar cambios'}</Button>
              {!adding && <Button color="error" onClick={() => run(() => del(`/api/accounts/${accountId}/farming/villages/${cowId}`))}>Borrar aldea</Button>}
            </>
          ) : (
            <>
              <Typography color="text.secondary">Mínimo por aldea: minutos para volver a atacar la misma. Revisar la lista: cada cuántos minutos se recorre. 0 la revisa una sola vez.</Typography>
              <TextField type="number" label="Mínimo por aldea (minutos)" value={minMinutes} onChange={event => setMinMinutes(Number(event.target.value))} />
              <TextField type="number" label="Revisar la lista cada (minutos)" value={repeatMinutes} onChange={event => setRepeatMinutes(Number(event.target.value))} />
              <Button variant="contained" disabled={!listId} onClick={() => run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes }))}>Guardar cambios</Button>
              <Button variant="contained" color="secondary" disabled={!listId} onClick={async () => {
                const troopText = window.prompt('Tropas para cada oasis. Ejemplo: 2 o A:2 D:1')
                if (!troopText) return
                const distance = window.prompt('Distancia máxima en casillas')
                if (!distance) return
                try {
                  const found = await post<SearchResult>(`/api/accounts/${accountId}/farming/lists/${listId}/search`, { troops: troopText, distance })
                  notify(found.message)
                  if (!found.data.count) return
                  if (!window.confirm(`Se encontraron ${found.data.count} oasis. ¿Agregarlos con ${found.data.troopPlan}?`)) return
                  const applied = await post(`/api/accounts/${accountId}/farming/lists/${listId}/search/apply`, { troopPlan: found.data.troopPlan, hits: found.data.hits })
                  notify(applied.message)
                  await load()
                } catch (error) {
                  notify(error instanceof Error ? error.message : 'No se pudo buscar')
                }
              }}>Buscar oasis</Button>
              <Button disabled={!listId} onClick={() => { setAdding(true); setCowId(0); setUrl(''); setTroops('') }}>Agregar aldea</Button>
              <Button variant="contained" disabled={!listId} onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/start`, { minMinutes, repeatMinutes }))}>Iniciar raids</Button>
              <Button color="error" onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/stop`))}>Detener</Button>
              {countdown && <Typography color="error" fontWeight={800}>{countdown}</Typography>}
            </>
          )}
        </Stack>
      </Stack>
    </Stack>
  )
}
