import { Box, Typography } from '@mui/material'

export function Hero() {
  return (
    <Box sx={{ minHeight: 420, borderRadius: 2, bgcolor: '#2b2d31', display: 'grid', placeItems: 'center', textAlign: 'center', px: 4 }}>
      <Box>
        <Typography variant="overline" sx={{ color: '#949ba4', letterSpacing: '0.18em' }}>Hero</Typography>
        <Typography variant="h4" sx={{ color: '#f2f3f5', mt: 1 }}>Coming soon</Typography>
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 420 }}>El héroe va a tener su lugar acá. Por ahora las listas de vacas siguen en Farming.</Typography>
      </Box>
    </Box>
  )
}
