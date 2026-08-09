import { useEffect, useState } from 'react'
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

type OrgUnit = {
  id: string
  code: string
  name: string
  parentId?: string | null
  path: string
  level: number
  isActive: boolean
}

export default function OrgUnitsPage() {
  const { token } = useAuth()
  const [items, setItems] = useState<OrgUnit[]>([])
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')

  async function load() {
    try {
      setItems(await api<OrgUnit[]>('/api/v1/org-units', {}, token))
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba načítania')
    }
  }

  useEffect(() => {
    void load()
  }, [token])

  async function create() {
    try {
      await api('/api/v1/org-units', {
        method: 'POST',
        body: JSON.stringify({ code, name, parentId: items[0]?.id ?? null })
      }, token)
      setOpen(false)
      setCode('')
      setName('')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba vytvorenia')
    }
  }

  return (
    <Stack spacing={2}>
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Typography variant="h4">Organizačná štruktúra</Typography>
        <Button variant="contained" onClick={() => setOpen(true)}>
          Nový útvar
        </Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Kód</TableCell>
            <TableCell>Názov</TableCell>
            <TableCell>Úroveň</TableCell>
            <TableCell>Cesta</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((o) => (
            <TableRow key={o.id}>
              <TableCell>{o.code}</TableCell>
              <TableCell sx={{ pl: 2 + o.level * 2 }}>{o.name}</TableCell>
              <TableCell>{o.level}</TableCell>
              <TableCell>{o.path}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Dialog open={open} onClose={() => setOpen(false)}>
        <DialogTitle>Nový útvar</DialogTitle>
        <DialogContent>
          <TextField label="Kód" fullWidth margin="dense" value={code} onChange={(e) => setCode(e.target.value)} />
          <TextField label="Názov" fullWidth margin="dense" value={name} onChange={(e) => setName(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Zrušiť</Button>
          <Button variant="contained" onClick={() => void create()}>
            Uložiť
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}
