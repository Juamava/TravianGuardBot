import { useEffect, useState } from 'react'
import { Button, Stack, Typography } from '@mui/material'
import { api, post } from '../api'

type DebugData = { tasks: { task: string; executeAt: string; stage: string }[]; logs: string }

export function Debug({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
  const [data, setData] = useState<DebugData>({ tasks: [], logs: '' })

  useEffect(() => {
    const load = () => api<DebugData>(`/api/accounts/${accountId}/debug`).then(result => setData(result.data)).catch(() => undefined)
    load()
    const timer = window.setInterval(load, 2000)
    return () => window.clearInterval(timer)
  }, [accountId])

  return (
    <Stack spacing={2}>
      <Typography variant="h4">Debug</Typography>
      <Stack direction="row" spacing={1}>
        <Button variant="outlined" onClick={() => post('/api/logs/open').then(result => notify(result.message)).catch((error: Error) => notify(error.message))}>Abrir logs</Button>
        <Button variant="outlined" onClick={() => post(`/api/accounts/${accountId}/debug/front`).then(result => notify(result.message)).catch((error: Error) => notify(error.message))}>Traer el navegador</Button>
      </Stack>
      <Stack spacing={0.5}>
        {data.tasks.map((task, index) => <Typography key={index}>{task.stage} · {task.task} · {task.executeAt}</Typography>)}
        {data.tasks.length === 0 && <Typography color="text.secondary">No hay tareas en cola.</Typography>}
      </Stack>
      <Typography component="pre" sx={{ whiteSpace: 'pre-wrap', bgcolor: '#221a12', color: '#f3e9cf', p: 2.5, borderRadius: 1.5, border: '2px solid #6b4423', minHeight: 240, fontFamily: 'Consolas, "Cascadia Mono", monospace', fontSize: 14, lineHeight: 1.6 }}>{data.logs || 'Sin mensajes todavía.'}</Typography>
    </Stack>
  )
}
