import { createTheme } from '@mui/material/styles'

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#5865f2', contrastText: '#fff' },
    secondary: { main: '#23a559', contrastText: '#fff' },
    warning: { main: '#f0b232', contrastText: '#1e1f22' },
    error: { main: '#da373c', contrastText: '#fff' },
    background: { default: '#313338', paper: '#2b2d31' },
    text: { primary: '#f2f3f5', secondary: '#b5bac1' },
    divider: '#3f4147',
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", sans-serif',
    h4: { fontWeight: 700, letterSpacing: '-0.02em' },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 700, fontSize: '1rem' },
    button: { fontWeight: 600, textTransform: 'none' },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 4, boxShadow: 'none' },
        contained: { boxShadow: 'none' },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
    MuiTextField: {
      defaultProps: { size: 'small' },
    },
    MuiCssBaseline: {
      styleOverrides: {
        body: { backgroundColor: '#313338' },
      },
    },
  },
})
