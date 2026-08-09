import { Alert, Paper, Stack, Typography } from '@mui/material'
import { useAuth } from '../auth/AuthContext'

export default function DashboardPage() {
  const { displayName } = useAuth()
  return (
    <Stack spacing={2}>
      <Typography variant="h4">Prehľad</Typography>
      <Alert severity="info">
        Vitajte, {displayName}. Toto je úvodný dashboard Fázy 0 / modulov 6.1 a 6.2.
      </Alert>
      <Paper sx={{ p: 2 }}>
        <Typography>
          Použite menu na správu organizačnej štruktúry, používateľov, registra UAS, komponentov a batérií.
        </Typography>
      </Paper>
    </Stack>
  )
}
