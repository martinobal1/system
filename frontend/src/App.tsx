import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import AppLayout from './layout/AppLayout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import OrgUnitsPage from './pages/OrgUnitsPage'
import AccountsPage from './pages/AccountsPage'
import UasListPage from './pages/UasListPage'
import UasDetailPage from './pages/UasDetailPage'
import ComponentsPage from './pages/ComponentsPage'
import BatteriesPage from './pages/BatteriesPage'

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/*"
        element={
          <PrivateRoute>
            <AppLayout>
              <Routes>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/org-units" element={<OrgUnitsPage />} />
                <Route path="/accounts" element={<AccountsPage />} />
                <Route path="/uas" element={<UasListPage />} />
                <Route path="/uas/:id" element={<UasDetailPage />} />
                <Route path="/components" element={<ComponentsPage />} />
                <Route path="/batteries" element={<BatteriesPage />} />
              </Routes>
            </AppLayout>
          </PrivateRoute>
        }
      />
    </Routes>
  )
}
