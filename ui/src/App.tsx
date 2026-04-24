import {
  Alert,
  Box,
  Button,
  Flex,
  Grid,
  HStack,
  IconButton,
  SimpleGrid,
  Spinner,
  Text,
} from '@chakra-ui/react'
import { RefreshCcw, Settings, Skull } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrrApi } from './api'
import { PluginCard } from './components/PluginCard'
import { InstallPanel } from './components/InstallPanel'
import { SettingsPanel } from './components/SettingsPanel'
import type { Plugin, Settings as AppSettings } from './types'

const STORAGE_KEY = 'arrr-settings'

interface Toast {
  id: number
  title: string
  type: 'success' | 'error'
}

function loadSettings(): AppSettings {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}')
  } catch {
    return { apiKey: '', baseUrl: '' }
  }
}

export default function App() {
  const [settings, setSettings] = useState<AppSettings>(loadSettings)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [plugins, setPlugins] = useState<Plugin[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set())
  const [reloadingAll, setReloadingAll] = useState(false)
  const [toasts, setToasts] = useState<Toast[]>([])

  const apiRef = useRef<ArrrApi | null>(null)
  apiRef.current = new ArrrApi(settings.baseUrl || '', settings.apiKey || '')

  const api = () => apiRef.current!

  const toast = (title: string, type: 'success' | 'error') => {
    const id = Date.now()
    setToasts((prev) => [...prev, { id, title, type }])
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 3500)
  }

  const fetchPlugins = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const list = await api().getPlugins()
      setPlugins(list)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (settings.apiKey) fetchPlugins()
    else setSettingsOpen(true)
  }, [settings.apiKey, fetchPlugins])

  const withBusy = async (id: string, fn: () => Promise<void>) => {
    setBusyIds((prev) => new Set(prev).add(id))
    try {
      await fn()
    } catch (e) {
      toast((e as Error).message, 'error')
    } finally {
      setBusyIds((prev) => {
        const next = new Set(prev)
        next.delete(id)
        return next
      })
    }
  }

  const handleToggle = (plugin: Plugin, enabled: boolean) =>
    withBusy(plugin.id, async () => {
      if (enabled) await api().enable(plugin.id)
      else await api().disable(plugin.id)
      toast(`${plugin.name} ${enabled ? 'enabled' : 'disabled'}`, 'success')
      await fetchPlugins()
    })

  const handleReload = (plugin: Plugin) =>
    withBusy(plugin.id, async () => {
      await api().reload(plugin.id)
      toast(`${plugin.name} reloaded`, 'success')
      await fetchPlugins()
    })

  const handleUninstall = (plugin: Plugin) =>
    withBusy(plugin.id, async () => {
      await api().uninstall(plugin.id)
      toast(`${plugin.name} uninstalled`, 'success')
      await fetchPlugins()
    })

  const handleReloadAll = async () => {
    setReloadingAll(true)
    try {
      await api().reloadAll()
      toast('All plugins reloaded', 'success')
      await fetchPlugins()
    } catch (e) {
      toast((e as Error).message, 'error')
    } finally {
      setReloadingAll(false)
    }
  }

  const handleInstall = async (packageId: string, version: string) => {
    try {
      await api().install(packageId, version || undefined)
      toast(`${packageId} installed`, 'success')
      await fetchPlugins()
    } catch (e) {
      toast((e as Error).message, 'error')
      throw e
    }
  }

  const handleSaveSettings = (s: AppSettings) => {
    setSettings(s)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
  }

  const runningCount = plugins.filter((p) => p.running).length

  return (
    <Box minH="100vh" bg="#080c14" p={0}>
      {/* Navbar */}
      <Flex
        as="header"
        align="center"
        justify="space-between"
        px={6}
        py={4}
        borderBottomWidth="1px"
        borderColor="whiteAlpha.50"
        bg="blackAlpha.400"
        backdropFilter="blur(10px)"
        position="sticky"
        top={0}
        zIndex={10}
      >
        <HStack gap={3}>
          <Skull size={22} color="#d97706" />
          <Text
            fontFamily="'Pirata One', cursive"
            fontSize="2xl"
            color="amber.400"
            letterSpacing="wider"
            lineHeight={1}
          >
            Arrr
          </Text>
          <Text
            fontSize="xs"
            color="gray.600"
            fontFamily="mono"
            textTransform="uppercase"
            letterSpacing="widest"
            mt="1px"
          >
            Plugin Manager
          </Text>
        </HStack>

        <HStack gap={2}>
          {plugins.length > 0 && (
            <Text fontSize="xs" color="gray.600" fontFamily="mono" mr={1}>
              {runningCount}/{plugins.length} running
            </Text>
          )}
          <Button
            size="sm"
            variant="ghost"
            color="gray.400"
            _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
            onClick={handleReloadAll}
            loading={reloadingAll}
            gap={1}
          >
            <RefreshCcw size={14} />
            Reload all
          </Button>
          <IconButton
            aria-label="Settings"
            size="sm"
            variant="ghost"
            color={settingsOpen ? 'amber.400' : 'gray.500'}
            _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
            onClick={() => setSettingsOpen((o) => !o)}
          >
            <Settings size={16} />
          </IconButton>
          <IconButton
            aria-label="Refresh"
            size="sm"
            variant="ghost"
            color="gray.500"
            _hover={{ color: 'white', bg: 'whiteAlpha.50' }}
            onClick={fetchPlugins}
            loading={loading}
          >
            <RefreshCcw size={16} />
          </IconButton>
        </HStack>
      </Flex>

      {/* Main */}
      <Box maxW="1200px" mx="auto" px={6} py={6}>
        <Grid templateColumns="1fr" gap={4}>
          {/* Settings panel (collapsible) */}
          {settingsOpen && (
            <SettingsPanel
              settings={settings}
              onSave={handleSaveSettings}
              onClose={() => setSettingsOpen(false)}
            />
          )}

          {/* Install panel */}
          {!settingsOpen && settings.apiKey && (
            <InstallPanel onInstall={handleInstall} />
          )}

          {/* Error */}
          {error && (
            <Alert.Root status="error" borderRadius="xl" bg="red.900" borderColor="red.700">
              <Alert.Indicator />
              <Alert.Title fontSize="sm">{error}</Alert.Title>
            </Alert.Root>
          )}

          {/* Plugin grid */}
          {loading && plugins.length === 0 ? (
            <Flex justify="center" align="center" h="200px">
              <Spinner color="amber.500" size="lg" />
            </Flex>
          ) : plugins.length === 0 && !loading && !error && settings.apiKey ? (
            <Flex
              direction="column"
              align="center"
              justify="center"
              h="200px"
              gap={2}
              color="gray.600"
            >
              <Skull size={32} />
              <Text fontSize="sm" fontFamily="mono">
                No plugins found
              </Text>
            </Flex>
          ) : (
            <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap={4}>
              {plugins.map((plugin) => (
                <PluginCard
                  key={plugin.id}
                  plugin={plugin}
                  busy={busyIds.has(plugin.id)}
                  onToggle={handleToggle}
                  onReload={handleReload}
                  onUninstall={handleUninstall}
                />
              ))}
            </SimpleGrid>
          )}
        </Grid>
      </Box>

      {/* Toast stack */}
      <Box position="fixed" bottom={4} right={4} zIndex={100} display="flex" flexDirection="column" gap={2}>
        {toasts.map((t) => (
          <Box
            key={t.id}
            bg={t.type === 'success' ? 'green.900' : 'red.900'}
            borderWidth="1px"
            borderColor={t.type === 'success' ? 'green.700' : 'red.700'}
            color="white"
            px={4}
            py={3}
            borderRadius="lg"
            fontSize="sm"
            fontFamily="body"
            boxShadow="lg"
            style={{ animation: 'fadeIn 0.2s ease' }}
          >
            {t.title}
          </Box>
        ))}
      </Box>

      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.4; }
        }
        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }
      `}</style>
    </Box>
  )
}
