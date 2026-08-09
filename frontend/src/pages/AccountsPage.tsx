import { useEffect, useState } from 'react'
import { Alert, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

type Account = {
  id: string
  userName: string
  firstName: string
  lastName: string
  email?: string
  status: string | number
  isActive: boolean
}

export default function AccountsPage() {
  const { token } = useAuth()
  const [items, setItems] = useState<Account[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<Account[]>('/api/v1/accounts', {}, token)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Chyba'))
  }, [token])

  return (
    <Stack spacing={2}>
      <Typography variant="h4">Používatelia a roly</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Používateľ</TableCell>
            <TableCell>Meno</TableCell>
            <TableCell>E-mail</TableCell>
            <TableCell>Stav</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((a) => (
            <TableRow key={a.id}>
              <TableCell>{a.userName}</TableCell>
              <TableCell>
                {a.firstName} {a.lastName}
              </TableCell>
              <TableCell>{a.email}</TableCell>
              <TableCell>{String(a.status)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Stack>
  )
}
