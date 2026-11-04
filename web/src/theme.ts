import { createTheme } from '@mui/material/styles'

export const theme = createTheme({
  palette: {
    primary: { main: '#58CC02', contrastText: '#fff' },
    secondary: { main: '#1CB0F6', contrastText: '#fff' },
    warning: { main: '#FFC800', contrastText: '#3C3C3C' },
    error: { main: '#FF4B4B' },
    background: { default: '#FFF8EE', paper: '#FFFFFF' },
    text: { primary: '#3C3C3C', secondary: '#777777' },
  },
  shape: { borderRadius: 18 },
  typography: {
    fontFamily: '"Nunito", "Trebuchet MS", sans-serif',
    h4: { fontWeight: 900 },
    h5: { fontWeight: 900 },
    h6: { fontWeight: 800 },
    button: { fontWeight: 800, textTransform: 'none', fontSize: '1rem' },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 999, paddingInline: 18, boxShadow: 'none' },
        containedPrimary: { boxShadow: '0 4px 0 #46A302' },
        containedSecondary: { boxShadow: '0 4px 0 #1899D6' },
        containedWarning: { boxShadow: '0 4px 0 #E0A800' },
        containedError: { boxShadow: '0 4px 0 #D93A3A' },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { border: '2px solid #F0E6D6' },
      },
    },
  },
})
