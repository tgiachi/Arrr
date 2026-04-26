import { Box, Flex, IconButton, Spinner, Text } from '@chakra-ui/react'
import { RefreshCcw, Terminal } from 'lucide-react'
import { useTheme } from 'next-themes'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { ArrrApi } from '../api'

interface Props {
  api: ArrrApi
}

type Level = 'ERR' | 'FTL' | 'WRN' | 'INF' | 'DBG' | 'VRB' | null

function parseLevel(line: string): Level {
  const m = line.match(/\[(ERR|FTL|WRN|INF|DBG|VRB)\]/)
  return m ? (m[1] as Level) : null
}

function levelColor(level: Level, dark: boolean): string {
  if (dark) {
    switch (level) {
      case 'ERR': case 'FTL': return '#f87171'
      case 'WRN': return '#fbbf24'
      case 'INF': return '#a3e635'
      case 'DBG': return '#6b7280'
      case 'VRB': return '#4b5563'
      default: return '#9ca3af'
    }
  }
  switch (level) {
    case 'ERR': case 'FTL': return '#b91c1c'
    case 'WRN': return '#b45309'
    case 'INF': return '#166534'
    case 'DBG': return '#3d5a80'
    case 'VRB': return '#64748b'
    default: return '#334155'
  }
}

function levelBg(level: Level, dark: boolean): string {
  switch (level) {
    case 'ERR': case 'FTL':
      return dark ? 'rgba(239,68,68,0.08)' : 'rgba(239,68,68,0.06)'
    case 'WRN':
      return dark ? 'rgba(245,158,11,0.06)' : 'rgba(245,158,11,0.07)'
    default:
      return 'transparent'
  }
}

function levelBadge(level: Level, dark: boolean): string {
  if (dark) {
    switch (level) {
      case 'ERR': case 'FTL': return '#ef4444'
      case 'WRN': return '#f59e0b'
      case 'INF': return '#4d7c0f'
      case 'DBG': return '#374151'
      default: return '#1f2937'
    }
  }
  switch (level) {
    case 'ERR': case 'FTL': return '#fee2e2'
    case 'WRN': return '#fef3c7'
    case 'INF': return '#dcfce7'
    case 'DBG': return '#dbeafe'
    default: return '#e2e8f0'
  }
}

export function LogsView({ api }: Props) {
  const { resolvedTheme } = useTheme()
  const dark = resolvedTheme !== 'light'
  const [lines, setLines] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [autoScroll, setAutoScroll] = useState(true)
  const bottomRef = useRef<HTMLDivElement>(null)
  const scrollRef = useRef<HTMLDivElement>(null)
  const prevLengthRef = useRef(0)

  const fetchLogs = useCallback(async () => {
    try {
      const data = await api.getLogs()
      setLines(data)
    } catch {
      // silently ignore poll errors
    }
  }, [api])

  useEffect(() => {
    setLoading(true)
    fetchLogs().finally(() => setLoading(false))
    const interval = setInterval(fetchLogs, 3000)
    return () => clearInterval(interval)
  }, [fetchLogs])

  useEffect(() => {
    if (autoScroll && lines.length !== prevLengthRef.current && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: 'smooth' })
    }
    prevLengthRef.current = lines.length
  }, [lines, autoScroll])

  return (
    <Box>
      <Flex align="center" justify="space-between" mb={3}>
        <Flex align="center" gap={2}>
          <Terminal size={13} color="#d97706" />
          <Text
            fontSize="xs"
            fontFamily="mono"
            color="app.textMuted"
            textTransform="uppercase"
            letterSpacing="widest"
          >
            Service Logs
          </Text>
          {loading && <Spinner size="xs" color="amber.500" />}
          <Text fontSize="xs" fontFamily="mono" color="app.textDim">
            {lines.length} lines
          </Text>
        </Flex>

        <Flex align="center" gap={3}>
          <Text
            fontSize="xs"
            fontFamily="mono"
            color={autoScroll ? 'amber.500' : 'app.textMuted'}
            cursor="pointer"
            userSelect="none"
            onClick={() => setAutoScroll((v) => !v)}
            _hover={{ color: autoScroll ? 'amber.400' : 'app.text' }}
          >
            {autoScroll ? '↓ auto' : '⊘ paused'}
          </Text>
          <IconButton
            aria-label="Refresh logs"
            size="xs"
            variant="ghost"
            color="app.iconColor"
            _hover={{ color: 'amber.400', bg: 'app.cardBgHover' }}
            onClick={fetchLogs}
          >
            <RefreshCcw size={11} />
          </IconButton>
        </Flex>
      </Flex>

      <Box
        ref={scrollRef}
        bg="app.termBg"
        borderWidth="1px"
        borderColor="app.cardBorder"
        borderRadius="xl"
        p={4}
        h="calc(100vh - 230px)"
        minH="400px"
        overflowY="auto"
        css={{
          '&::-webkit-scrollbar': { width: '4px' },
          '&::-webkit-scrollbar-track': { background: 'transparent' },
          '&::-webkit-scrollbar-thumb': {
            background: 'rgba(255,255,255,0.08)',
            borderRadius: '2px',
          },
        }}
      >
        {lines.length === 0 && !loading ? (
          <Flex align="center" justify="center" h="200px" direction="column" gap={3}>
            <Terminal size={24} color="currentColor" style={{ opacity: 0.3 }} />
            <Text fontFamily="mono" fontSize="xs" color="app.textDim">
              No log entries found
            </Text>
          </Flex>
        ) : (
          lines.map((line, i) => {
            const level = parseLevel(line)
            return (
              <Flex
                key={i}
                px={2}
                py="1px"
                borderRadius="sm"
                bg={levelBg(level, dark)}
                _hover={{ bg: 'app.cardBgHover' }}
                align="baseline"
                gap={2}
                minH="20px"
              >
                {level && (
                  <Box
                    flexShrink={0}
                    px="5px"
                    py="1px"
                    borderRadius="sm"
                    bg={levelBadge(level, dark)}
                    fontSize="9px"
                    fontFamily="mono"
                    color={levelColor(level, dark)}
                    fontWeight="700"
                    letterSpacing="wider"
                    lineHeight="16px"
                  >
                    {level}
                  </Box>
                )}
                <Text
                  as="span"
                  fontFamily="mono"
                  fontSize="xs"
                  color={levelColor(level, dark)}
                  whiteSpace="pre-wrap"
                  wordBreak="break-all"
                  lineHeight="1.6"
                >
                  {line}
                </Text>
              </Flex>
            )
          })
        )}
        <div ref={bottomRef} />
      </Box>
    </Box>
  )
}
