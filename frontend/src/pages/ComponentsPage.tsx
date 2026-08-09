import { FormEvent, useEffect, useState } from 'react'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography
} from '@mui/material'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

type ComponentItem = {
  id: string
  internalNumber: string
  serialNumber?: string | null
  name: string
  status: string | number
}

export default function ComponentsPage() {
  const { token } = useAuth()
  const [items, setItems] = useState<ComponentItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const [internalNumber, setInternalNumber] = useState('')
  const [name, setName] = useState('')

  async function load() {
    try {
      setItems(await api<ComponentItem[]>('/api/v1/components', {}, token))
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba')
    }
  }

  useEffect(() => {
    void load()
  }, [token])

  async function create(e: FormEvent) {
    e.preventDefault()
    try {
      await api('/api/v1/components', {
        method: 'POST',
        body: JSON.stringify({ internalNumber, serialNumber: null, name, componentTypeCode: null, owningOrgUnitId: null })
      }, token)
      setOpen(false)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chyba')
    }
  }

  return (
    <Stack spacing={2}>
      <Stack direction="row" justifyContent="space-between">
        <Typography variant="h4">Komponenty a vybavenie</Typography>
        <Button variant="contained" onClick={() => setOpen(true)}>
          Nový komponent
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Interné číslo</TableCell>
            <TableCell>Názov</TableCell>
            <TableCell>Stav</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((c) => (
            <TableRow key={c.id}>
              <TableCell>{c.internalNumber}</TableCell>
              <TableCell>{c.name}</TableCell>
              <TableCell>{String(c.status)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Dialog open={open} onClose={() => setOpen(false)}>
        <form onSubmit={create}>
          <DialogTitle>Nový komponent</DialogTitle>
          <DialogContent>
            <TextField label="Interné číslo" fullWidth margin="dense" required value={internalNumber} onChange={(e) => setInternalNumber(e.target.value)} />
            <TextField label="Názov" fullWidth margin="dense" required value={name} onChange={(e) => setName(e.target.value)} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(false)}>Zrušiť</Button>
            <Button type="submit" variant="contained">
              Uložiť
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </Stack>
  )
}
