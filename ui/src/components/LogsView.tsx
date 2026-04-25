import { Box, Flex, IconButton, Spinner, Text } from '@chakra-ui/react'
import { RefreshCcw, Terminal } from 'lucide-react'
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

function levelColor(level: Level): string {
  switch (level) {
    case 'ERR':
    case 'FTL':
      return '#f87171'
    case 'WRN':
      return '#fbbf24'
    case 'INF':
      return '#a3e635'
    case 'DBG':
      return '#6b7280'
    case 'VRB':
      return '#4b5563'
    default:
      return '#9ca3af'
  }
}

function levelBg(level: Level): string {
  switch (level) {
    case 'ERR':
    case 'FTL':
      return 'rgba(239,68,68,0.08)'
    case 'WRN':
      return 'rgba(245,158,11,0.06)'
    default:
      return 'transparent'
  }
}

function levelBadge(level: Level): string {
  switch (level) {
    case 'ERR':
    case 'FTL':
      return '#ef4444'
    case 'WRN':
      return '#f59e0b'
    case 'INF':
      return '#4d7c0f'
    case 'DBG':
      return '#374151'
    default:
      return '#1f2937'
  }
}

export function LogsView({ api }: Props) {
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
            color="gray.500"
            textTransform="uppercase"
            letterSpacing="widest"
          >
            Service Logs
          </Text>
          {loading && <Spinner size="xs" color="amber.500" />}
          <Text fontSize="xs" fontFamily="mono" color="gray.700">
            {lines.length} lines
          </Text>
        </Flex>

        <Flex align="center" gap={3}>
          <Text
            fontSize="xs"
            fontFamily="mono"
            color={autoScroll ? 'amber.500' : 'gray.600'}
            cursor="pointer"
            userSelect="none"
            onClick={() => setAutoScroll((v) => !v)}
            _hover={{ color: autoScroll ? 'amber.400' : 'gray.400' }}
          >
            {autoScroll ? '↓ auto' : '⊘ paused'}
          </Text>
          <IconButton
            aria-label="Refresh logs"
            size="xs"
            variant="ghost"
            color="gray.600"
            _hover={{ color: 'amber.400', bg: 'whiteAlpha.50' }}
            onClick={fetchLogs}
          >
            <RefreshCcw size={11} />
          </IconButton>
        </Flex>
      </Flex>

      <Box
        ref={scrollRef}
        bg="blackAlpha.600"
        borderWidth="1px"
        borderColor="whiteAlpha.50"
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
            <Terminal size={24} color="#374151" />
            <Text fontFamily="mono" fontSize="xs" color="gray.700">
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
                bg={levelBg(level)}
                _hover={{ bg: 'whiteAlpha.50' }}
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
                    bg={levelBadge(level)}
                    fontSize="9px"
                    fontFamily="mono"
                    color={levelColor(level)}
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
                  color={levelColor(level)}
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
