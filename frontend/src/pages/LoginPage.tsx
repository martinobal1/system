import { FormEvent, useState } from 'react'
import { Alert, Box, Button, Paper, TextField, Typography } from '@mui/material'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../api/client'

export default function LoginPage() {
  const { token, login } = useAuth()
  const [userName, setUserName] = useState('gestor')
  const [password, setPassword] = useState('Heslo123!')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (token) return <Navigate to="/" replace />

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      await login(userName, password)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Prihlásenie zlyhalo')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        background: 'linear-gradient(160deg, #d6eaf8 0%, #eaf2f8 45%, #f4f6f8 100%)'
      }}
    >
      <Paper sx={{ p: 4, width: 420 }} elevation={3}>
        <Typography variant="h4" gutterBottom>
          IS ŠUAS
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 3 }}>
          Informačný systém štátnych bezpilotných leteckých systémov
        </Typography>
        <Box component="form" onSubmit={onSubmit}>
          <TextField
            label="Používateľské meno"
            fullWidth
            margin="normal"
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
            required
          />
          <TextField
            label="Heslo"
            type="password"
            fullWidth
            margin="normal"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          {error && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {error}
            </Alert>
          )}
          <Button type="submit" fullWidth variant="contained" sx={{ mt: 3 }} disabled={loading}>
            Prihlásiť sa
          </Button>
        </Box>
      </Paper>
    </Box>
  )
}
