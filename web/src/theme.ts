import { createTheme } from '@mui/material/styles'

// Inspired by the Travian Legends interface: parchment panels, wood frames, serif titles and green buttons.
const ink = '#3b2a17'
const wood = '#6b4423'
const woodLight = '#a07a44'
const parchment = '#fbf5e2'
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
    text: { primary: ink, secondary: '#6b5233' },
    divider: '#c9b27c',
  },
  shape: { borderRadius: 4 },
  typography: {
    fontFamily: 'Arial, Helvetica, Verdana, sans-serif',
    fontSize: 13,
    h4: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, color: wood, textShadow: '0 1px 0 rgba(255,255,255,.6)' },
    h5: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, color: wood },
    h6: { fontFamily: '"Noto Serif", Georgia, serif', fontWeight: 700, fontSize: '1rem', color: wood },
    button: { fontWeight: 700, textTransform: 'none' },
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
          background: `linear-gradient(${wood}, #4a2e15)`,
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
      styleOverrides: { root: { color: '#6b5233', '&.Mui-focused': { color: green } } },
    },
    MuiToggleButtonGroup: {
      styleOverrides: {
        root: {
          background: `linear-gradient(#8a5a2b, ${wood}) !important`,
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
      styleOverrides: { root: { fontWeight: 700, border: '1px solid rgba(107,68,35,.25)' } },
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
