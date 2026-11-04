import { Box, Typography } from '@mui/material'

export function Hero() {
  return (
    <Box sx={{ p: 6, borderRadius: 5, textAlign: 'center', background: 'linear-gradient(160deg, #FFD84D 0%, #FF9600 100%)' }}>
      <Typography variant="h4" color="#3C3C3C">Hero</Typography>
      <Typography variant="h3" fontWeight={900} color="#fff" sx={{ textShadow: '0 3px 0 #E08900' }}>Coming soon</Typography>
    </Box>
  )
}
