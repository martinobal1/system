import {
  AppBar,
  Box,
  Button,
  Drawer,
  List,
  ListItemButton,
  ListItemText,
  Toolbar,
  Typography
} from '@mui/material'
import { Link as RouterLink, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const width = 260

const links = [
  { to: '/', label: 'Prehľad' },
  { to: '/org-units', label: 'Organizačná štruktúra' },
  { to: '/accounts', label: 'Používatelia a roly' },
  { to: '/uas', label: 'Register UAS' },
  { to: '/components', label: 'Komponenty a vybavenie' },
  { to: '/batteries', label: 'Batérie' }
]

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { displayName, logout } = useAuth()
  const location = useLocation()

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            IS ŠUAS
          </Typography>
          <Typography sx={{ mr: 2 }}>{displayName}</Typography>
          <Button color="inherit" onClick={logout}>
            Odhlásiť
          </Button>
        </Toolbar>
      </AppBar>
      <Drawer
        variant="permanent"
        sx={{
          width,
          [`& .MuiDrawer-paper`]: { width, boxSizing: 'border-box' }
        }}
      >
        <Toolbar />
        <List>
          {links.map((l) => (
            <ListItemButton
              key={l.to}
              component={RouterLink}
              to={l.to}
              selected={location.pathname === l.to || (l.to !== '/' && location.pathname.startsWith(l.to))}
            >
              <ListItemText primary={l.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        {children}
      </Box>
    </Box>
  )
}
