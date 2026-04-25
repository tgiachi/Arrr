import {
  Alert,
  Box,
  Button,
  Flex,
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
import { SinkCard } from './components/SinkCard'
import { ConfigModal } from './components/ConfigModal'
import { CallbackModal } from './components/CallbackModal'
import { QrModal } from './components/QrModal'
import { InstallPanel } from './components/InstallPanel'
import { SettingsPanel } from './components/SettingsPanel'
import { LogsView } from './components/LogsView'
import { StreamView } from './components/StreamView'
import { ThemeToggle } from './components/ThemeToggle'
import type { Plugin, Sink, Settings as AppSettings } from './types'

const STORAGE_KEY = 'arrr-settings'

type Tab = 'configurazione' | 'stream' | 'install' | 'logs'

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

const TAB_LABELS: Record<Tab, string> = {
  configurazione: 'Configuration',
  stream: 'Stream',
  install: 'Install',
  logs: 'Logs',
}

export default function App() {
  const [settings, setSettings] = useState<AppSettings>(loadSettings)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [activeTab, setActiveTab] = useState<Tab>('configurazione')
  const [plugins, setPlugins] = useState<Plugin[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set())
  const [reloadingAll, setReloadingAll] = useState(false)
  const [toasts, setToasts] = useState<Toast[]>([])
  const [configuringPlugin, setConfiguringPlugin] = useState<Plugin | null>(null)
  const [callbackPlugin, setCallbackPlugin] = useState<Plugin | null>(null)
  const [qrPlugin, setQrPlugin] = useState<Plugin | null>(null)

  const [sinks, setSinks] = useState<Sink[]>([])
  const [loadingSinks, setLoadingSinks] = useState(false)
  const [sinksError, setSinksError] = useState<string | null>(null)
  const [sinkBusyIds, setSinkBusyIds] = useState<Set<string>>(new Set())
  const [configuringSink, setConfiguringSink] = useState<Sink | null>(null)

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

  const fetchSinks = useCallback(async () => {
    setLoadingSinks(true)
    setSinksError(null)
    try {
      const list = await api().getSinks()
      setSinks(list)
    } catch (e) {
      setSinksError((e as Error).message)
    } finally {
      setLoadingSinks(false)
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (settings.apiKey) {
      fetchPlugins()
      fetchSinks()
    } else {
      setSettingsOpen(true)
    }
  }, [settings.apiKey, fetchPlugins, fetchSinks])

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

  const withSinkBusy = async (id: string, fn: () => Promise<void>) => {
    setSinkBusyIds((prev) => new Set(prev).add(id))
    try {
      await fn()
    } catch (e) {
      toast((e as Error).message, 'error')
    } finally {
      setSinkBusyIds((prev) => {
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

  const handleSinkToggle = (sink: Sink, enabled: boolean) =>
    withSinkBusy(sink.id, async () => {
      if (enabled) await api().enableSink(sink.id)
      else await api().disableSink(sink.id)
      toast(`${sink.name} ${enabled ? 'enabled' : 'disabled'}`, 'success')
      await fetchSinks()
    })

  const handleSinkReload = (sink: Sink) =>
    withSinkBusy(sink.id, async () => {
      await api().reloadSink(sink.id)
      toast(`${sink.name} reloaded`, 'success')
      await fetchSinks()
    })

  const handleSaveSettings = (s: AppSettings) => {
    setSettings(s)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
  }

  const handleSettingsClick = () => {
    setActiveTab('configurazione')
    setSettingsOpen((o) => !o)
  }

  const runningCount = plugins.filter((p) => p.running).length

  return (
    <Box minH="100vh" bg="app.bg" p={0}>
      {/* Sticky header: brand row + tab row */}
      <Box
        as="header"
        position="sticky"
        top={0}
        zIndex={10}
        bg="app.headerBg"
        backdropFilter="blur(12px)"
        borderBottomWidth="1px"
        borderColor="app.border"
      >
        {/* Brand row */}
        <Flex align="center" justify="space-between" px={6} py={3}>
          <HStack gap={3}>
            <Skull size={20} color="#d97706" />
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
              color="app.textDim"
              fontFamily="mono"
              textTransform="uppercase"
              letterSpacing="widest"
              mt="1px"
            >
              Notification center
            </Text>
          </HStack>

          <HStack gap={2}>
            {plugins.length > 0 && (
              <Text fontSize="xs" color="app.textDim" fontFamily="mono" mr={1}>
                {runningCount}/{plugins.length} running
              </Text>
            )}
            {activeTab === 'configurazione' && (
              <Button
                size="sm"
                variant="ghost"
                color="app.textMuted"
                _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
                onClick={handleReloadAll}
                loading={reloadingAll}
                gap={1}
              >
                <RefreshCcw size={13} />
                Reload all
              </Button>
            )}
            <ThemeToggle />
            <IconButton
              aria-label="Settings"
              size="sm"
              variant="ghost"
              color={settingsOpen ? 'amber.400' : 'app.textMuted'}
              _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
              onClick={handleSettingsClick}
            >
              <Settings size={15} />
            </IconButton>
            <IconButton
              aria-label="Refresh"
              size="sm"
              variant="ghost"
              color="app.textMuted"
              _hover={{ color: 'white', bg: 'whiteAlpha.50' }}
              onClick={fetchPlugins}
              loading={loading}
            >
              <RefreshCcw size={15} />
            </IconButton>
          </HStack>
        </Flex>

        {/* Tab row */}
        <Flex px={4} gap={0} borderTopWidth="1px" borderColor="app.border">
          {(Object.keys(TAB_LABELS) as Tab[]).map((tab) => {
            const active = activeTab === tab
            return (
              <Button
                key={tab}
                variant="ghost"
                size="sm"
                px={5}
                py={3}
                h="auto"
                borderRadius={0}
                borderBottomWidth="2px"
                borderBottomColor={active ? 'amber.500' : 'transparent'}
                color={active ? 'amber.400' : 'gray.600'}
                fontFamily="mono"
                fontSize="xs"
                fontWeight={active ? '600' : '400'}
                textTransform="uppercase"
                letterSpacing="wider"
                onClick={() => setActiveTab(tab)}
                _hover={{
                  color: active ? 'amber.400' : 'gray.400',
                  bg: 'transparent',
                }}
                style={{
                  transition: 'color 0.15s, border-color 0.15s',
                }}
              >
                {TAB_LABELS[tab]}
              </Button>
            )
          })}
        </Flex>
      </Box>

      {/* Main content */}
      <Box maxW="1200px" mx="auto" px={6} py={6}>
        {/* ── TAB: Configurazione ── */}
        {activeTab === 'configurazione' && (
          <Box>
            {settingsOpen && (
              <Box mb={4}>
                <SettingsPanel
                  settings={settings}
                  onSave={handleSaveSettings}
                  onClose={() => setSettingsOpen(false)}
                />
              </Box>
            )}

            {error && (
              <Alert.Root status="error" borderRadius="xl" bg="red.900" borderColor="red.700" mb={4}>
                <Alert.Indicator />
                <Alert.Title fontSize="sm">{error}</Alert.Title>
              </Alert.Root>
            )}

            {/* Plugin section label */}
            {settings.apiKey && (
              <Text
                fontSize="xs"
                fontFamily="mono"
                color="app.textMuted"
                textTransform="uppercase"
                letterSpacing="widest"
                mb={3}
              >
                Source Plugins
              </Text>
            )}

            {loading && plugins.length === 0 ? (
              <Flex justify="center" align="center" h="200px">
                <Spinner color="amber.500" size="lg" />
              </Flex>
            ) : plugins.length === 0 && !loading && !error && settings.apiKey ? (
              <Flex direction="column" align="center" justify="center" h="200px" gap={2} color="app.textDim">
                <Skull size={28} />
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
                    onConfigure={setConfiguringPlugin}
                    onCallback={setCallbackPlugin}
                    onQr={setQrPlugin}
                  />
                ))}
              </SimpleGrid>
            )}

            {/* Output Connectors section */}
            {settings.apiKey && (
              <Box mt={8}>
                <Text
                  fontSize="xs"
                  fontFamily="mono"
                  color="app.textMuted"
                  textTransform="uppercase"
                  letterSpacing="widest"
                  mb={3}
                >
                  Output Connectors
                </Text>

                {sinksError && (
                  <Alert.Root
                    status="error"
                    borderRadius="xl"
                    bg="red.900"
                    borderColor="red.700"
                    mb={3}
                  >
                    <Alert.Indicator />
                    <Alert.Title fontSize="sm">{sinksError}</Alert.Title>
                  </Alert.Root>
                )}

                {loadingSinks && sinks.length === 0 ? (
                  <Flex justify="center" align="center" h="100px">
                    <Spinner color="amber.500" size="md" />
                  </Flex>
                ) : (
                  <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap={4}>
                    {sinks.map((sink) => (
                      <SinkCard
                        key={sink.id}
                        sink={sink}
                        busy={sinkBusyIds.has(sink.id)}
                        onToggle={handleSinkToggle}
                        onReload={handleSinkReload}
                        onConfigure={setConfiguringSink}
                      />
                    ))}
                  </SimpleGrid>
                )}
              </Box>
            )}
          </Box>
        )}

        {/* ── TAB: Install ── */}
        {activeTab === 'install' && settings.apiKey && (
          <InstallPanel onInstall={handleInstall} />
        )}
        {activeTab === 'install' && !settings.apiKey && (
          <Flex direction="column" align="center" justify="center" h="300px" gap={3} color="app.textDim">
            <Settings size={28} />
            <Text fontFamily="mono" fontSize="sm">
              Configure API key first
            </Text>
            <Button
              size="sm"
              variant="outline"
              colorPalette="amber"
              onClick={handleSettingsClick}
            >
              Open Settings
            </Button>
          </Flex>
        )}

        {/* ── TAB: Stream ── */}
        {activeTab === 'stream' && (
          <StreamView settings={settings} onOpenSettings={handleSettingsClick} />
        )}

        {/* ── TAB: Logs ── */}
        {activeTab === 'logs' && settings.apiKey && (
          <LogsView api={api()} />
        )}
        {activeTab === 'logs' && !settings.apiKey && (
          <Flex direction="column" align="center" justify="center" h="300px" gap={3} color="app.textDim">
            <Settings size={28} />
            <Text fontFamily="mono" fontSize="sm">
              Configure API key first
            </Text>
            <Button
              size="sm"
              variant="outline"
              colorPalette="amber"
              onClick={handleSettingsClick}
            >
              Open Settings
            </Button>
          </Flex>
        )}
      </Box>

      {/* Modals */}
      {qrPlugin && (
        <QrModal plugin={qrPlugin} api={api()} onClose={() => setQrPlugin(null)} />
      )}
      {callbackPlugin && (
        <CallbackModal
          plugin={callbackPlugin}
          api={api()}
          onClose={() => setCallbackPlugin(null)}
          onToast={toast}
        />
      )}
      {configuringPlugin && (
        <ConfigModal
          id={configuringPlugin.id}
          name={configuringPlugin.name}
          getConfig={() => api().getConfig(configuringPlugin.id)}
          saveConfig={(c) => api().saveConfig(configuringPlugin.id, c)}
          onClose={() => setConfiguringPlugin(null)}
          onToast={toast}
        />
      )}
      {configuringSink && (
        <ConfigModal
          id={configuringSink.id}
          name={configuringSink.name}
          getConfig={() => api().getSinkConfig(configuringSink.id)}
          saveConfig={(c) => api().saveSinkConfig(configuringSink.id, c)}
          onClose={() => setConfiguringSink(null)}
          onToast={toast}
        />
      )}

      {/* Toast stack */}
      <Box
        position="fixed"
        bottom={4}
        right={4}
        zIndex={100}
        display="flex"
        flexDirection="column"
        gap={2}
      >
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
