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
import { Link as RouterLink } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

type Uas = {
  id: string
  registrationNumber?: string | null
  serialNumber: string
  manufacturer: string
  model: string
  status: string | number
}

type Paged = { items: Uas[]; totalCount: number }

export default function UasListPage() {
  const { token } = useAuth()
  const [items, setItems] = useState<Uas[]>([])
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const [serialNumber, setSerialNumber] = useState('')
  const [manufacturer, setManufacturer] = useState('DJI')
  const [model, setModel] = useState('')

  async function load() {
    try {
      const data = await api<Paged>('/api/v1/uas?page=1&pageSize=50', {}, token)
      setItems(data.items)
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
      await api('/api/v1/uas', {
        method: 'POST',
        body: JSON.stringify({ serialNumber, manufacturer, model, uasTypeCode: null, owningOrgUnitId: null, notes: null })
      }, token)
      setOpen(false)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chyba vytvorenia')
    }
  }

  return (
    <Stack spacing={2}>
      <Stack direction="row" justifyContent="space-between">
        <Typography variant="h4">Register UAS</Typography>
        <Button variant="contained" onClick={() => setOpen(true)}>
          Nový koncept UAS
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Evid. označenie</TableCell>
            <TableCell>Výrobné číslo</TableCell>
            <TableCell>Výrobca / model</TableCell>
            <TableCell>Stav</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((u) => (
            <TableRow key={u.id} hover>
              <TableCell>
                <RouterLink to={`/uas/${u.id}`}>{u.registrationNumber || '—'}</RouterLink>
              </TableCell>
              <TableCell>{u.serialNumber}</TableCell>
              <TableCell>
                {u.manufacturer} {u.model}
              </TableCell>
              <TableCell>{String(u.status)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Dialog open={open} onClose={() => setOpen(false)}>
        <DialogTitle>Nový koncept UAS</DialogTitle>
        <BoxForm onSubmit={create}>
          <DialogContent>
            <TextField label="Výrobné číslo" fullWidth margin="dense" required value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} />
            <TextField label="Výrobca" fullWidth margin="dense" required value={manufacturer} onChange={(e) => setManufacturer(e.target.value)} />
            <TextField label="Model" fullWidth margin="dense" required value={model} onChange={(e) => setModel(e.target.value)} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(false)}>Zrušiť</Button>
            <Button type="submit" variant="contained">
              Uložiť
            </Button>
          </DialogActions>
        </BoxForm>
      </Dialog>
    </Stack>
  )
}

function BoxForm({ children, onSubmit }: { children: React.ReactNode; onSubmit: (e: FormEvent) => void }) {
  return <form onSubmit={onSubmit}>{children}</form>
}
