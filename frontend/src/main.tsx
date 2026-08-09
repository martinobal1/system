import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material'
import { skSK } from '@mui/material/locale'
import App from './App'
import { AuthProvider } from './auth/AuthContext'

const theme = createTheme(
  {
    palette: {
      mode: 'light',
      primary: { main: '#1b4f72' },
      secondary: { main: '#117a65' },
      background: { default: '#f4f6f8' }
    },
    typography: {
      fontFamily: '"Source Sans 3", "Segoe UI", Helvetica, Arial, sans-serif',
      h4: { fontWeight: 700 },
      h5: { fontWeight: 700 }
    }
  },
  skSK
)

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  </React.StrictMode>
)
