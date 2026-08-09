import { useEffect, useState } from 'react'
import { Alert, Button, Stack, Typography } from '@mui/material'
import { useParams } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

type UasDetail = {
  uas: {
    id: string
    registrationNumber?: string | null
    serialNumber: string
    manufacturer: string
    model: string
    status: string | number
    submittedByAccountId?: string | null
  }
  assignments: Array<{ id: string; orgUnitName: string; validFrom: string; validTo?: string | null; reason: string }>
  statusHistory: Array<{ fromStatus: string | number; toStatus: string | number; reason: string; changedAt: string }>
}

export default function UasDetailPage() {
  const { id } = useParams()
  const { token } = useAuth()
  const [detail, setDetail] = useState<UasDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  async function load() {
    try {
      setDetail(await api<UasDetail>(`/api/v1/uas/${id}`, {}, token))
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba')
    }
  }

  useEffect(() => {
    void load()
  }, [id, token])

  async function submit() {
    try {
      await api(`/api/v1/uas/${id}/submit`, { method: 'POST' }, token)
      setMessage('UAS bol predložený na schválenie.')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba')
    }
  }

  async function approve() {
    try {
      await api(`/api/v1/uas/${id}/approve`, { method: 'POST' }, token)
      setMessage('UAS bol schválený a dostal evidenčné označenie.')
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Chyba')
    }
  }

  if (!detail && !error) return <Typography>Načítavam…</Typography>
  if (!detail) return <Alert severity="error">{error}</Alert>

  const u = detail.uas
  return (
    <Stack spacing={2}>
      <Typography variant="h4">Detail UAS</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <Typography>
        <strong>Evid. označenie:</strong> {u.registrationNumber || '—'}
      </Typography>
      <Typography>
        <strong>Výrobné číslo:</strong> {u.serialNumber}
      </Typography>
      <Typography>
        <strong>Výrobca / model:</strong> {u.manufacturer} {u.model}
      </Typography>
      <Typography>
        <strong>Stav:</strong> {String(u.status)}
      </Typography>
      <Stack direction="row" spacing={1}>
        <Button variant="outlined" onClick={() => void submit()}>
          Predložiť
        </Button>
        <Button variant="contained" onClick={() => void approve()}>
          Schváliť
        </Button>
      </Stack>
      <Typography variant="h6">Pridelenia</Typography>
      {detail.assignments.length === 0 && <Typography color="text.secondary">Žiadne pridelenia</Typography>}
      {detail.assignments.map((a) => (
        <Typography key={a.id}>
          {a.orgUnitName} — od {new Date(a.validFrom).toLocaleString('sk-SK')}
          {a.validTo ? ` do ${new Date(a.validTo).toLocaleString('sk-SK')}` : ' (aktívne)'} — {a.reason}
        </Typography>
      ))}
      <Typography variant="h6">História stavov</Typography>
      {detail.statusHistory.map((h, i) => (
        <Typography key={i}>
          {String(h.fromStatus)} → {String(h.toStatus)} ({new Date(h.changedAt).toLocaleString('sk-SK')}) — {h.reason}
        </Typography>
      ))}
    </Stack>
  )
}
