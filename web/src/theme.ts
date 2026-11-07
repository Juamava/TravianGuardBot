import { createTheme } from '@mui/material/styles'
import { divider, woodTexture } from './art'

// Inspired by the Travian Legends interface: parchment panels, wood frames, serif titles and green buttons.
const ink = '#2e2010'
const wood = '#6b4423'
const woodLight = '#a07a44'
const parchment = '#fdf9ec'
const green = '#5f8f12'

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: green, dark: '#456b0a', light: '#8fbf2c', contrastText: '#fff' },
    secondary: { main: '#4f8a10', contrastText: '#fff' },
    warning: { main: '#b77a0a', contrastText: '#fff' },
    error: { main: '#b8281d', contrastText: '#fff' },
    info: { main: '#3d6d8a' },
    background: { default: '#1c2625', paper: parchment },
    text: { primary: ink, secondary: '#5c4529' },
    divider: '#d6c296',
  },
  shape: { borderRadius: 4 },
  typography: {
    fontFamily: '"Segoe UI", "Noto Sans", Arial, sans-serif',
    fontSize: 14,
    body1: { fontSize: '0.95rem', lineHeight: 1.5 },
    body2: { fontSize: '0.9rem', lineHeight: 1.5 },
    caption: { fontSize: '0.8rem', lineHeight: 1.45 },
    h4: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, color: '#4a2e15', textShadow: '0 1px 0 rgba(255,250,232,.9), 0 0 12px rgba(255,250,232,.8)', paddingBottom: 18, backgroundImage: divider, backgroundRepeat: 'no-repeat', backgroundPosition: 'left bottom', backgroundSize: '240px 14px' },
    h5: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, color: wood },
    h6: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, fontSize: '1rem', color: wood },
    button: { fontWeight: 600, textTransform: 'none', fontSize: '0.9rem' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: { backgroundColor: '#1c2625', color: ink },
        '*::-webkit-scrollbar': { width: 10, height: 10 },
        '*::-webkit-scrollbar-thumb': { backgroundColor: woodLight, borderRadius: 6, border: '2px solid #efe4c6' },
        '*::-webkit-scrollbar-track': { backgroundColor: 'rgba(107,68,35,.12)' },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 4, boxShadow: 'none', '&.Mui-disabled': { color: '#9c8664' } },
        contained: {
          color: '#fff',
          background: 'linear-gradient(#8fbf2c, #5f8f12 55%, #4a730c)',
          border: '1px solid #3d5e08',
          textShadow: '0 1px 1px rgba(0,0,0,.35)',
          boxShadow: 'inset 0 1px 0 rgba(255,255,255,.35)',
          '&:hover': { background: 'linear-gradient(#9fd03a, #6aa016 55%, #527f0e)', boxShadow: 'inset 0 1px 0 rgba(255,255,255,.35)' },
          '&.Mui-disabled': { background: '#d9cba3', color: '#8a7350', border: '1px solid #c9b27c', textShadow: 'none' },
        },
        containedWarning: { background: 'linear-gradient(#e7b440, #b77a0a)', border: '1px solid #8a5a05' },
        containedError: { background: 'linear-gradient(#e0563f, #b8281d)', border: '1px solid #8a1f16' },
        outlined: { borderColor: woodLight, color: wood, backgroundColor: 'rgba(255,250,232,.7)', '&:hover': { borderColor: wood, backgroundColor: '#f1e5c2' } },
        text: { color: '#4f7a12', '&:hover': { backgroundColor: 'rgba(95,143,18,.08)' } },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none', backgroundColor: parchment, color: ink },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: { border: `3px solid ${wood}`, boxShadow: `0 0 0 2px ${woodLight}, 0 12px 40px rgba(0,0,0,.55)`, borderRadius: 6 },
      },
    },
    MuiDialogTitle: {
      styleOverrides: {
        root: {
          fontFamily: '"Noto Serif", Georgia, serif',
          fontWeight: 700,
          color: '#f6ead0',
          background: `linear-gradient(rgba(74,46,21,.35), rgba(30,16,5,.6)), ${woodTexture}`,
          borderBottom: `2px solid ${woodLight}`,
          textShadow: '0 1px 2px rgba(0,0,0,.5)',
          marginBottom: 12,
        },
      },
    },
    MuiTextField: {
      defaultProps: { size: 'small' },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          backgroundColor: '#fffdf4',
          '& .MuiOutlinedInput-notchedOutline': { borderColor: '#c9b27c' },
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: woodLight },
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: green },
        },
      },
    },
    MuiInputLabel: {
      styleOverrides: { root: { color: '#4a3820', fontWeight: 600, fontSize: '0.92rem', '&.MuiInputLabel-shrink': { color: '#3b2a17', transform: 'translate(10px, -11px) scale(0.88)', backgroundColor: '#fdf9ec', padding: '0 5px', borderRadius: 3 }, '&.Mui-focused': { color: green } } },
    },
    MuiToggleButtonGroup: {
      styleOverrides: {
        root: {
          background: `linear-gradient(rgba(138,90,43,.3), rgba(60,34,12,.5)), ${woodTexture} !important`,
          border: '1px solid #4a2e15',
          padding: 3,
          '& .MuiToggleButton-root': {
            color: '#f6ead0 !important',
            fontFamily: '"Noto Serif", Georgia, serif',
            fontWeight: 700,
            textShadow: '0 1px 1px rgba(0,0,0,.45)',
            border: 0,
          },
          '& .MuiToggleButton-root.Mui-selected': {
            color: `${ink} !important`,
            textShadow: 'none',
            background: 'linear-gradient(#f7e9bd, #d9b977) !important',
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: { root: { fontWeight: 600, fontSize: '0.8rem', border: '1px solid rgba(107,68,35,.25)' } },
    },
    MuiCheckbox: {
      styleOverrides: { root: { color: woodLight, '&.Mui-checked': { color: green } } },
    },
    MuiSwitch: {
      styleOverrides: {
        switchBase: { '&.Mui-checked': { color: green }, '&.Mui-checked + .MuiSwitch-track': { backgroundColor: '#8fbf2c' } },
        track: { backgroundColor: '#a8956f' },
      },
    },
    MuiTooltip: {
      styleOverrides: {
        tooltip: { backgroundColor: '#fff7dc', color: ink, border: `1px solid ${woodLight}`, boxShadow: '0 4px 12px rgba(0,0,0,.25)' },
        arrow: { color: '#fff7dc' },
      },
    },
    MuiMenu: {
      styleOverrides: { paper: { border: `1px solid ${woodLight}` } },
    },
    MuiMenuItem: {
      styleOverrides: { root: { '&.Mui-selected': { backgroundColor: '#e2cf9a' }, '&:hover': { backgroundColor: '#f1e5c2' } } },
    },
  },
})
