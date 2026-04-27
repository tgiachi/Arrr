import { useCallback, useEffect, useRef, useState } from 'react'
import {
  Box,
  Button,
  Flex,
  HStack,
  Input,
  Spinner,
  Text,
  Tooltip,
} from '@chakra-ui/react'
import {
  ChevronLeft,
  ChevronRight,
  Clock,
  Filter,
  RotateCcw,
  Search,
  Trash2,
  X,
} from 'lucide-react'
import type { ArrrApi } from '../api'
import type { HistoryEntry } from '../types'

interface Props {
  api: ArrrApi
  onToast: (msg: string, type: 'success' | 'error') => void
}

const PRIORITY_LABELS: Record<number, { label: string; color: string }> = {
  0: { label: 'low',      color: '#6b7280' },
  1: { label: 'normal',   color: '#4ade80' },
  2: { label: 'high',     color: '#facc15' },
  3: { label: 'critical', color: '#f87171' },
}

function formatTs(ts: string) {
  const d = new Date(ts)
  const now = Date.now()
  const diff = now - d.getTime()
  if (diff < 60_000)  return 'just now'
  if (diff < 3_600_000) return `${Math.floor(diff / 60_000)}m ago`
  if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)}h ago`
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

function HistoryRow({ entry }: { entry: HistoryEntry }) {
  const pr = PRIORITY_LABELS[entry.priority] ?? PRIORITY_LABELS[1]
  return (
    <Flex
      align="flex-start"
      gap={3}
      py={2.5}
      px={4}
      borderBottomWidth="1px"
      borderColor="app.border"
      _last={{ borderBottom: 'none' }}
      _hover={{ bg: 'app.cardBgHover' }}
      transition="background 0.12s"
    >
      {/* priority stripe */}
      <Box w="3px" bg={pr.color} borderRadius="full" flexShrink={0} alignSelf="stretch" mt={0.5} />

      {/* content */}
      <Box flex={1} minW={0}>
        <Flex align="baseline" gap={2} mb={0.5} flexWrap="wrap">
          <Text
            fontFamily="mono"
            fontSize="xs"
            fontWeight="700"
            color="amber.300"
            flexShrink={0}
            textTransform="uppercase"
            letterSpacing="wider"
          >
            {entry.source}
          </Text>
          <Text
            fontSize="sm"
            color="app.text"
            fontWeight="600"
            overflow="hidden"
            whiteSpace="nowrap"
            textOverflow="ellipsis"
          >
            {entry.title}
          </Text>
        </Flex>
        <Text fontSize="xs" color="app.textMuted" lineClamp={2}>
          {entry.body}
        </Text>
      </Box>

      {/* meta */}
      <Flex direction="column" align="flex-end" gap={1} flexShrink={0}>
        <Flex align="center" gap={1} color="app.textDim">
          <Clock size={10} />
          <Text fontSize="10px" fontFamily="mono" whiteSpace="nowrap">
            {formatTs(entry.timestamp)}
          </Text>
        </Flex>
        {entry.priority > 1 && (
          <Text fontSize="10px" fontFamily="mono" color={pr.color} textTransform="uppercase">
            {pr.label}
          </Text>
        )}
      </Flex>
    </Flex>
  )
}

export function HistoryView({ api, onToast }: Props) {
  const [items, setItems] = useState<HistoryEntry[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [clearing, setClearing] = useState(false)
  const [confirmClear, setConfirmClear] = useState(false)

  const [search, setSearch] = useState('')
  const [sourceFilter, setSourceFilter] = useState('')
  const [inputSearch, setInputSearch] = useState('')
  const [inputSource, setInputSource] = useState('')

  const limit = 50
  const totalPages = Math.max(1, Math.ceil(total / limit))
  const searchRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async (p: number, s: string, src: string) => {
    setLoading(true)
    try {
      const res = await api.getHistory(p, limit, s || undefined, src || undefined)
      setItems(res.items)
      setTotal(res.total)
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setLoading(false)
    }
  }, [api, onToast]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { load(page, search, sourceFilter) }, [page, search, sourceFilter, load])

  function applySearch() {
    setSearch(inputSearch)
    setSourceFilter(inputSource)
    setPage(1)
  }

  function clearFilters() {
    setInputSearch('')
    setInputSource('')
    setSearch('')
    setSourceFilter('')
    setPage(1)
    searchRef.current?.focus()
  }

  async function handleClear() {
    if (!confirmClear) {
      setConfirmClear(true)
      setTimeout(() => setConfirmClear(false), 3000)
      return
    }
    setConfirmClear(false)
    setClearing(true)
    try {
      await api.clearHistory()
      onToast('History cleared', 'success')
      setPage(1)
      await load(1, search, sourceFilter)
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setClearing(false)
    }
  }

  const hasFilters = search || sourceFilter

  return (
    <Box>
      {/* ── Toolbar ── */}
      <Flex gap={2} mb={4} wrap="wrap" align="center">
        <Flex
          flex={1}
          minW="220px"
          bg="app.inputBg"
          borderWidth="1px"
          borderColor="app.inputBorder"
          borderRadius="lg"
          align="center"
          px={3}
          gap={2}
        >
          <Search size={13} color="var(--chakra-colors-app-textDim)" />
          <Input
            ref={searchRef}
            value={inputSearch}
            onChange={(e) => setInputSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && applySearch()}
            placeholder="Search title or body…"
            size="sm"
            flex={1}
            minW={0}
            bg="transparent"
            borderWidth={0}
            outline="none"
            p={0}
            fontFamily="mono"
            fontSize="xs"
            color="app.inputColor"
            _placeholder={{ color: 'app.placeholder' }}
            _focus={{ boxShadow: 'none' }}
          />
          {inputSearch && (
            <Box
              as="button"
              color="app.textDim"
              _hover={{ color: 'app.textMuted' }}
              onClick={() => { setInputSearch(''); setSearch(''); setPage(1) }}
              flexShrink={0}
            >
              <X size={12} />
            </Box>
          )}
        </Flex>

        <Flex
          w="160px"
          bg="app.inputBg"
          borderWidth="1px"
          borderColor="app.inputBorder"
          borderRadius="lg"
          align="center"
          px={3}
          gap={2}
        >
          <Filter size={12} color="var(--chakra-colors-app-textDim)" />
          <Input
            value={inputSource}
            onChange={(e) => setInputSource(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && applySearch()}
            placeholder="Source…"
            size="sm"
            flex={1}
            minW={0}
            bg="transparent"
            borderWidth={0}
            outline="none"
            p={0}
            fontFamily="mono"
            fontSize="xs"
            color="app.inputColor"
            _placeholder={{ color: 'app.placeholder' }}
            _focus={{ boxShadow: 'none' }}
          />
        </Flex>

        <Button
          size="sm"
          colorPalette="amber"
          variant="outline"
          fontFamily="mono"
          fontSize="xs"
          onClick={applySearch}
          disabled={loading}
        >
          Search
        </Button>

        {hasFilters && (
          <Tooltip.Root>
            <Tooltip.Trigger asChild>
              <Button size="sm" variant="ghost" color="app.textMuted" onClick={clearFilters} px={2}>
                <RotateCcw size={13} />
              </Button>
            </Tooltip.Trigger>
            <Tooltip.Positioner>
              <Tooltip.Content fontFamily="mono" fontSize="xs">Clear filters</Tooltip.Content>
            </Tooltip.Positioner>
          </Tooltip.Root>
        )}

        <Box ml="auto" />

        <Text fontSize="10px" fontFamily="mono" color="app.textDim" whiteSpace="nowrap">
          {total} entries
        </Text>

        <Tooltip.Root>
          <Tooltip.Trigger asChild>
            <Button
              size="sm"
              variant={confirmClear ? 'solid' : 'ghost'}
              colorPalette={confirmClear ? 'red' : undefined}
              color={confirmClear ? undefined : 'app.textMuted'}
              _hover={{ color: confirmClear ? undefined : 'red.400' }}
              onClick={handleClear}
              loading={clearing}
              px={confirmClear ? 3 : 2}
              fontFamily="mono"
              fontSize="xs"
              style={{ transition: 'all 0.2s' }}
            >
              {confirmClear ? 'Confirm?' : <Trash2 size={13} />}
            </Button>
          </Tooltip.Trigger>
          <Tooltip.Positioner>
            <Tooltip.Content fontFamily="mono" fontSize="xs">
              {confirmClear ? 'Click again to confirm' : 'Clear all history'}
            </Tooltip.Content>
          </Tooltip.Positioner>
        </Tooltip.Root>
      </Flex>

      {/* ── List ── */}
      <Box
        bg="app.cardBg"
        borderWidth="1px"
        borderColor="app.cardBorder"
        borderRadius="xl"
        overflow="hidden"
        minH="200px"
      >
        {loading ? (
          <Flex justify="center" align="center" h="200px">
            <Spinner color="amber.500" size="md" />
          </Flex>
        ) : items.length === 0 ? (
          <Flex direction="column" justify="center" align="center" h="200px" gap={2} color="app.textDim">
            <Clock size={24} />
            <Text fontSize="sm" fontFamily="mono">
              {hasFilters ? 'No results' : 'No notifications recorded yet'}
            </Text>
          </Flex>
        ) : (
          items.map((entry) => <HistoryRow key={entry.id} entry={entry} />)
        )}
      </Box>

      {/* ── Pagination ── */}
      {totalPages > 1 && (
        <Flex justify="center" align="center" gap={3} mt={4}>
          <Button
            size="sm"
            variant="ghost"
            color="app.textMuted"
            disabled={page <= 1 || loading}
            onClick={() => setPage((p) => p - 1)}
            px={2}
          >
            <ChevronLeft size={14} />
          </Button>

          <HStack gap={1}>
            {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
              const p = totalPages <= 7 ? i + 1 : Math.max(1, Math.min(page - 3, totalPages - 6)) + i
              return (
                <Button
                  key={p}
                  size="sm"
                  variant={p === page ? 'solid' : 'ghost'}
                  colorPalette={p === page ? 'amber' : undefined}
                  color={p === page ? undefined : 'app.textMuted'}
                  onClick={() => setPage(p)}
                  px={3}
                  fontFamily="mono"
                  fontSize="xs"
                  minW="32px"
                >
                  {p}
                </Button>
              )
            })}
          </HStack>

          <Button
            size="sm"
            variant="ghost"
            color="app.textMuted"
            disabled={page >= totalPages || loading}
            onClick={() => setPage((p) => p + 1)}
            px={2}
          >
            <ChevronRight size={14} />
          </Button>
        </Flex>
      )}
    </Box>
  )
}
