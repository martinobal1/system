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

type Battery = {
  id: string
  internalNumber: string
  serialNumber: string
  manufacturer?: string | null
  model?: string | null
  cycleCount: number
  healthPercent?: number | null
  status: string | number
}

export default function BatteriesPage() {
  const { token } = useAuth()
  const [items, setItems] = useState<Battery[]>([])
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const [internalNumber, setInternalNumber] = useState('')
  const [serialNumber, setSerialNumber] = useState('')

  async function load() {
    try {
      setItems(await api<Battery[]>('/api/v1/batteries', {}, token))
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
      await api('/api/v1/batteries', {
        method: 'POST',
        body: JSON.stringify({
          internalNumber,
          serialNumber,
          manufacturer: null,
          model: null,
          compatibleUasTypeCodes: null,
          owningOrgUnitId: null
        })
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
        <Typography variant="h4">Batérie</Typography>
        <Button variant="contained" onClick={() => setOpen(true)}>
          Nová batéria
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Interné číslo</TableCell>
            <TableCell>Výrobné číslo</TableCell>
            <TableCell>Cykly</TableCell>
            <TableCell>Zdravie %</TableCell>
            <TableCell>Stav</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((b) => (
            <TableRow key={b.id}>
              <TableCell>{b.internalNumber}</TableCell>
              <TableCell>{b.serialNumber}</TableCell>
              <TableCell>{b.cycleCount}</TableCell>
              <TableCell>{b.healthPercent ?? '—'}</TableCell>
              <TableCell>{String(b.status)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Dialog open={open} onClose={() => setOpen(false)}>
        <form onSubmit={create}>
          <DialogTitle>Nová batéria</DialogTitle>
          <DialogContent>
            <TextField label="Interné číslo" fullWidth margin="dense" required value={internalNumber} onChange={(e) => setInternalNumber(e.target.value)} />
            <TextField label="Výrobné číslo" fullWidth margin="dense" required value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} />
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
