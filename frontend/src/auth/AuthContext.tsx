import { createContext, useContext, useMemo, useState, ReactNode } from 'react'
import { api, LoginResponse } from '../api/client'

type AuthState = {
  token: string | null
  userName: string | null
  displayName: string | null
  login: (userName: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

const STORAGE_KEY = 'issuas.auth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const saved = (() => {
    try {
      return JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null') as LoginResponse | null
    } catch {
      return null
    }
  })()

  const [token, setToken] = useState<string | null>(saved?.accessToken ?? null)
  const [userName, setUserName] = useState<string | null>(saved?.userName ?? null)
  const [displayName, setDisplayName] = useState<string | null>(saved?.displayName ?? null)

  const value = useMemo<AuthState>(
    () => ({
      token,
      userName,
      displayName,
      async login(user, password) {
        const res = await api<LoginResponse>('/api/v1/auth/login', {
          method: 'POST',
          body: JSON.stringify({ userName: user, password })
        })
        localStorage.setItem(STORAGE_KEY, JSON.stringify(res))
        setToken(res.accessToken)
        setUserName(res.userName)
        setDisplayName(res.displayName)
      },
      logout() {
        localStorage.removeItem(STORAGE_KEY)
        setToken(null)
        setUserName(null)
        setDisplayName(null)
      }
    }),
    [token, userName, displayName]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth mimo AuthProvider')
  return ctx
}
