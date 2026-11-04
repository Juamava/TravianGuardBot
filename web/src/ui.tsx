import { createContext, useContext, useState, type ReactNode } from 'react'
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Snackbar, TextField, Typography } from '@mui/material'

type ConfirmRequest = { kind: 'confirm'; message: string; resolve: (value: boolean) => void }
type PromptRequest = { kind: 'prompt'; message: string; placeholder: string; resolve: (value: string | null) => void }
type Request = ConfirmRequest | PromptRequest

type Ui = {
  notify: (message: string) => void
  confirm: (message: string) => Promise<boolean>
  prompt: (message: string, placeholder?: string) => Promise<string | null>
}

const UiContext = createContext<Ui | null>(null)

export function useUi() {
  const ui = useContext(UiContext)
  if (!ui) throw new Error('La interfaz no está lista')
  return ui
}

export function UiProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('')
  const [request, setRequest] = useState<Request | null>(null)
  const [draft, setDraft] = useState('')

  const ui: Ui = {
    notify: setMessage,
    confirm: (text) => new Promise(resolve => setRequest({ kind: 'confirm', message: text, resolve })),
    prompt: (text, placeholder = '') => new Promise(resolve => {
      setDraft('')
      setRequest({ kind: 'prompt', message: text, placeholder, resolve })
    }),
  }

  function close(value: boolean | string | null) {
    if (!request) return
    if (request.kind === 'confirm') request.resolve(Boolean(value))
    else request.resolve(typeof value === 'string' ? value : null)
    setRequest(null)
  }

  return (
    <UiContext.Provider value={ui}>
      {children}
      <Dialog open={request !== null} onClose={() => close(request?.kind === 'confirm' ? false : null)} fullWidth maxWidth="xs">
        <DialogTitle>{request?.kind === 'prompt' ? 'Decime' : 'Confirmá'}</DialogTitle>
        <DialogContent>
          <Typography color="text.secondary" sx={{ mb: request?.kind === 'prompt' ? 2 : 0 }}>{request?.message}</Typography>
          {request?.kind === 'prompt' && (
            <TextField autoFocus fullWidth placeholder={request.placeholder} value={draft} onChange={event => setDraft(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') close(draft) }} />
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => close(request?.kind === 'confirm' ? false : null)}>Cancelar</Button>
          <Button variant="contained" onClick={() => close(request?.kind === 'prompt' ? draft : true)}>{request?.kind === 'prompt' ? 'Seguir' : 'Sí'}</Button>
        </DialogActions>
      </Dialog>
      <Snackbar open={Boolean(message)} autoHideDuration={4200} onClose={() => setMessage('')} anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}>
        <Alert severity="info" variant="filled" onClose={() => setMessage('')} sx={{ bgcolor: '#111214', color: '#f2f3f5', border: '1px solid #3f4147' }}>{message}</Alert>
      </Snackbar>
    </UiContext.Provider>
  )
}

export function Empty({ title, text }: { title: string; text: string }) {
  return (
    <Box sx={{ p: 4, borderRadius: 2, bgcolor: '#2b2d31', textAlign: 'center' }}>
      <Typography variant="h5" sx={{ textTransform: 'none', color: '#f2f3f5' }}>{title}</Typography>
      <Typography color="text.secondary" sx={{ mt: 1 }}>{text}</Typography>
    </Box>
  )
}

export const panel = {
  p: 2,
  borderRadius: 2,
  bgcolor: '#2b2d31',
} as const
