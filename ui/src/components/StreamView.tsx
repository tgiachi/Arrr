import { Box, Flex, HStack, IconButton, Text } from '@chakra-ui/react'
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { Radio, Trash2, Wifi, WifiOff } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import type { NotificationItem, Settings } from '../types'

// ─── source badge colour (deterministic, per-source) ──────────────────────────
const PALETTE = [
  '#f59e0b', '#10b981', '#3b82f6', '#ef4444',
  '#8b5cf6', '#ec4899', '#14b8a6', '#f97316',
]

function sourceColor(src: string): string {
  let h = 0
  for (const c of src) h = (h * 31 + c.charCodeAt(0)) | 0
  return PALETTE[Math.abs(h) % PALETTE.length]
}

function formatTs(ts: string): string {
  try {
    const d = new Date(ts)
    return d.toLocaleTimeString('it-IT', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return '--:--:--'
  }
}

// ─── connection hook ───────────────────────────────────────────────────────────
function useStream(settings: Settings) {
  const [items, setItems] = useState<NotificationItem[]>([])
  const [connected, setConnected] = useState(false)
  const [connError, setConnError] = useState<string | null>(null)
  const connRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    if (!settings.apiKey) return

    const url = `${settings.baseUrl || ''}/stream?key=${encodeURIComponent(settings.apiKey)}`

    const conn = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    conn.on('ReceiveNotification', (n: Omit<NotificationItem, '_fresh'>) => {
      const item: NotificationItem = { ...n, _fresh: true }
      setItems(prev => [item, ...prev].slice(0, 300))
      setTimeout(() => {
        setItems(prev => prev.map(x => x.id === item.id ? { ...x, _fresh: false } : x))
      }, 700)
    })

    conn.onreconnecting(() => { setConnected(false); setConnError(null) })
    conn.onreconnected(() => { setConnected(true); setConnError(null) })
    conn.onclose(err => { setConnected(false); setConnError(err?.message ?? null) })

    connRef.current = conn
    conn.start()
      .then(() => { setConnected(true); setConnError(null) })
      .catch(err => setConnError(String(err)))

    return () => { conn.stop() }
  }, [settings.apiKey, settings.baseUrl])

  const clear = () => setItems([])

  return { items, connected, connError, clear }
}

// ─── component ────────────────────────────────────────────────────────────────
interface StreamViewProps {
  settings: Settings
  onOpenSettings: () => void
}

export function StreamView({ settings, onOpenSettings }: StreamViewProps) {
  const { items, connected, connError, clear } = useStream(settings)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const toggleExpand = (id: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  if (!settings.apiKey) {
    return (
      <Flex direction="column" align="center" justify="center" h="320px" gap={3} color="app.textDim">
        <Radio size={32} strokeWidth={1} />
        <Text fontFamily="mono" fontSize="sm" letterSpacing="wider">configure api key to connect</Text>
        <Box
          as="button"
          px={4}
          py={2}
          borderRadius="md"
          borderWidth="1px"
          borderColor="amber.600"
          color="amber.400"
          fontFamily="mono"
          fontSize="xs"
          textTransform="uppercase"
          letterSpacing="widest"
          _hover={{ bg: 'rgba(245,158,11,0.08)' }}
          onClick={onOpenSettings}
        >
          open settings
        </Box>
      </Flex>
    )
  }

  return (
    <Box>
      {/* ── header bar ───────────────────────────────────────────────── */}
      <Flex
        align="center"
        justify="space-between"
        mb={4}
        px={4}
        py="10px"
        bg="app.cardBg"
        borderWidth="1px"
        borderColor="app.cardBorder"
        borderRadius="lg"
      >
        <HStack gap={3}>
          {/* live indicator */}
          <Box
            w="8px" h="8px"
            borderRadius="full"
            flexShrink={0}
            style={{
              background: connected ? '#22c55e' : connError ? '#ef4444' : '#6b7280',
              boxShadow: connected ? '0 0 10px #22c55e88' : 'none',
              animation: connected ? 'streamDot 2s ease-in-out infinite' : 'none',
            }}
          />

          {connected
            ? <Wifi size={13} color="#22c55e" />
            : <WifiOff size={13} color={connError ? '#ef4444' : '#6b7280'} />
          }

          <Text
            fontFamily="mono" fontSize="xs" fontWeight="700"
            textTransform="uppercase" letterSpacing="widest"
            color={connected ? 'green.400' : connError ? 'red.400' : 'app.textDim'}
          >
            {connected ? 'live' : connError ? 'error' : 'connecting…'}
          </Text>

          <Box w="1px" h="12px" bg="app.border" />

          <Text fontFamily="mono" fontSize="xs" color="app.textDim">
            {items.length} {items.length === 1 ? 'signal' : 'signals'} received
          </Text>
        </HStack>

        <HStack gap={2}>
          {connError && (
            <Text
              fontSize="10px" color="red.400" fontFamily="mono"
              maxW="260px"
              style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
            >
              {connError}
            </Text>
          )}
          <IconButton
            aria-label="Clear stream"
            size="xs" variant="ghost"
            color="app.textMuted"
            _hover={{ color: 'red.400', bg: 'app.cardBgHover' }}
            onClick={clear}
            title="Clear all"
          >
            <Trash2 size={13} />
          </IconButton>
        </HStack>
      </Flex>

      {/* ── feed ─────────────────────────────────────────────────────── */}
      {items.length === 0 ? (
        <Flex direction="column" align="center" justify="center" h="320px" gap={4} color="app.textDim">
          <Box
            w="48px" h="48px"
            borderRadius="full"
            borderWidth="1px"
            borderColor="app.cardBorder"
            display="flex"
            alignItems="center"
            justifyContent="center"
            style={{ animation: 'streamDot 3s ease-in-out infinite' }}
          >
            <Radio size={20} strokeWidth={1.5} />
          </Box>
          <Text fontFamily="mono" fontSize="sm" letterSpacing="wider">
            — awaiting transmissions —
          </Text>
        </Flex>
      ) : (
        <Box
          borderWidth="1px"
          borderColor="app.cardBorder"
          borderRadius="lg"
          overflow="hidden"
          position="relative"
        >
          {/* subtle scanlines */}
          <Box
            position="absolute" inset={0} pointerEvents="none" zIndex={0}
            style={{
              background: 'repeating-linear-gradient(0deg, transparent, transparent 3px, rgba(0,0,0,0.025) 3px, rgba(0,0,0,0.025) 4px)',
            }}
          />

          {items.map((n, i) => {
            const color = sourceColor(n.source)
            const isExp = expanded.has(n.id)
            const showBody = n.body && n.body !== n.title

            return (
              <Box
                key={n.id}
                position="relative" zIndex={1}
                px={4} py={isExp ? 3 : '9px'}
                borderBottomWidth={i < items.length - 1 ? '1px' : '0'}
                borderColor="app.border"
                bg={i % 2 === 0 ? 'app.rowStripe' : 'transparent'}
                _hover={{ bg: 'app.cardBgHover', cursor: 'pointer' }}
                onClick={() => toggleExpand(n.id)}
                style={{
                  animation: n._fresh ? 'streamEntry 0.25s ease-out, streamFlash 0.7s ease-out' : 'none',
                  borderLeft: n._fresh ? '2px solid #f59e0b' : '2px solid transparent',
                  transition: 'border-color 0.5s ease, background 0.1s',
                }}
              >
                <Flex align="baseline" gap={3} wrap="nowrap" minW={0}>
                  {/* timestamp */}
                  <Text
                    fontFamily="mono" fontSize="11px"
                    color="app.textDim"
                    flexShrink={0}
                    style={{ letterSpacing: '0.04em', userSelect: 'none' }}
                  >
                    {formatTs(n.timestamp)}
                  </Text>

                  {/* source pill */}
                  <Box
                    flexShrink={0}
                    px="6px" py="1px"
                    borderRadius="3px"
                    fontFamily="mono"
                    fontSize="10px"
                    fontWeight="700"
                    textTransform="uppercase"
                    letterSpacing="0.08em"
                    style={{
                      color,
                      background: `${color}1a`,
                      border: `1px solid ${color}44`,
                      lineHeight: '1.6',
                    }}
                  >
                    {n.source}
                  </Box>

                  {/* title */}
                  <Text
                    fontFamily="mono" fontSize="sm" fontWeight="600"
                    color="app.text"
                    minW={0}
                    style={{
                      overflow: 'hidden',
                      textOverflow: isExp ? 'unset' : 'ellipsis',
                      whiteSpace: isExp ? 'normal' : 'nowrap',
                      flex: 1,
                    }}
                  >
                    {n.title}
                  </Text>
                </Flex>

                {/* body */}
                {showBody && (
                  <Text
                    fontFamily="mono" fontSize="xs"
                    color="app.textMuted"
                    mt={isExp ? 2 : '3px'}
                    pl="94px"
                    lineHeight="1.5"
                    style={{
                      overflow: 'hidden',
                      display: '-webkit-box',
                      WebkitBoxOrient: 'vertical',
                      WebkitLineClamp: isExp ? 'unset' : '2',
                    } as React.CSSProperties}
                  >
                    {n.body}
                  </Text>
                )}
              </Box>
            )
          })}
        </Box>
      )}

      <style>{`
        @keyframes streamEntry {
          from { opacity: 0; transform: translateY(-6px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes streamFlash {
          0%   { background-color: rgba(245, 158, 11, 0.10); }
          100% { background-color: transparent; }
        }
        @keyframes streamDot {
          0%, 100% { opacity: 1;   transform: scale(1);   }
          50%       { opacity: 0.4; transform: scale(0.75); }
        }
      `}</style>
    </Box>
  )
}
