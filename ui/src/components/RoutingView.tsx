import { useEffect, useRef, useState } from 'react'
import {
  Box,
  Button,
  Flex,
  HStack,
  Input,
  Spinner,
  Switch,
  Text,
  VStack,
} from '@chakra-ui/react'
import { Download, Plus, RefreshCcw, Save, Trash2, Upload } from 'lucide-react'
import type { ArrrApi } from '../api'
import type { DaemonConfig, RoutingConfig, RoutingLogEntry, RoutingRule } from '../types'

interface Props {
  api: ArrrApi
  onToast: (msg: string, type: 'success' | 'error') => void
}

const PRIORITY_LABELS = ['Normal', 'High', 'Critical']

const defaultRule = (): RoutingRule => ({
  name: '',
  enabled: true,
  sourcePattern: '*',
  titleContains: '',
  bodyContains: '',
  minPriority: 0,
  block: false,
  allowSinks: [],
  extraConditions: [],
  activeFrom: '',
  activeTo: '',
})

// ─────────────────────────── Rule row ───────────────────────────────────────

interface RuleRowProps {
  rule: RoutingRule
  index: number
  total: number
  onChange: (r: RoutingRule) => void
  onDelete: () => void
  onMoveUp: () => void
  onMoveDown: () => void
}

function RuleRow({ rule, index, total, onChange, onDelete, onMoveUp, onMoveDown }: RuleRowProps) {
  const accent = '#f97316'
  const update = (patch: Partial<RoutingRule>) => onChange({ ...rule, ...patch })

  return (
    <Box
      borderWidth="1px"
      borderColor="rgba(249,115,22,0.25)"
      borderRadius="lg"
      p={3}
      bg="rgba(249,115,22,0.04)"
      style={{ animation: `slideUp 0.25s cubic-bezier(0.16,1,0.3,1) ${index * 40}ms both` }}
    >
      {/* Header */}
      <Flex align="center" gap={2} mb={2.5}>
        <Switch.Root
          size="sm"
          colorPalette="orange"
          checked={rule.enabled}
          onCheckedChange={(e) => update({ enabled: e.checked })}
        >
          <Switch.HiddenInput />
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
        <Input
          size="sm"
          placeholder="Rule name…"
          value={rule.name}
          onChange={(e) => update({ name: e.target.value })}
          flex={1}
          bg="transparent"
          borderColor="rgba(249,115,22,0.2)"
          color="app.inputColor"
          fontFamily="mono"
          fontSize="xs"
          _placeholder={{ color: 'app.placeholder' }}
          _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
        />
        <Flex gap={0.5} flexShrink={0}>
          <Button
            size="xs"
            variant="ghost"
            color="app.textDim"
            _hover={{ color: accent, bg: 'rgba(249,115,22,0.08)' }}
            onClick={onMoveUp}
            disabled={index === 0}
            px={1}
            title="Move rule up"
            fontFamily="mono"
            fontSize="10px"
          >
            ▲
          </Button>
          <Button
            size="xs"
            variant="ghost"
            color="app.textDim"
            _hover={{ color: accent, bg: 'rgba(249,115,22,0.08)' }}
            onClick={onMoveDown}
            disabled={index === total - 1}
            px={1}
            title="Move rule down"
            fontFamily="mono"
            fontSize="10px"
          >
            ▼
          </Button>
        </Flex>
        <Button
          size="xs"
          variant="ghost"
          color="app.textDim"
          _hover={{ color: 'red.400', bg: 'rgba(248,113,113,0.08)' }}
          onClick={onDelete}
          px={1.5}
          flexShrink={0}
        >
          <Trash2 size={12} />
        </Button>
      </Flex>

      {/* Condition grid */}
      <Flex gap={2} wrap="wrap" mb={2.5}>
        <Box flex="1" minW="160px">
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
            Source pattern
          </Text>
          <Input
            size="xs"
            placeholder="com.arrr.plugin.*"
            value={rule.sourcePattern}
            onChange={(e) => update({ sourcePattern: e.target.value })}
            bg="app.inputBg"
            borderColor="app.inputBorder"
            color="app.inputColor"
            fontFamily="mono"
            _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
          />
        </Box>
        <Box flex="1" minW="120px">
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
            Min priority
          </Text>
          <select
            value={rule.minPriority}
            onChange={(e) => update({ minPriority: Number(e.target.value) })}
            style={{
              width: '100%',
              background: 'var(--chakra-colors-app-inputBg, rgba(255,255,255,0.04))',
              border: '1px solid var(--chakra-colors-app-inputBorder, rgba(255,255,255,0.1))',
              borderRadius: '6px',
              color: 'var(--chakra-colors-app-inputColor, #e2e8f0)',
              fontFamily: 'monospace',
              fontSize: '12px',
              padding: '4px 8px',
              cursor: 'pointer',
            }}
          >
            {PRIORITY_LABELS.map((l, v) => <option key={v} value={v}>{l}</option>)}
          </select>
        </Box>
        <Box flex="1" minW="160px">
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
            Title contains
          </Text>
          <Input
            size="xs"
            placeholder="e.g. alert"
            value={rule.titleContains}
            onChange={(e) => update({ titleContains: e.target.value })}
            bg="app.inputBg"
            borderColor="app.inputBorder"
            color="app.inputColor"
            fontFamily="mono"
            _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
          />
        </Box>
        <Box flex="1" minW="160px">
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
            Body contains
          </Text>
          <Input
            size="xs"
            placeholder="e.g. error"
            value={rule.bodyContains}
            onChange={(e) => update({ bodyContains: e.target.value })}
            bg="app.inputBg"
            borderColor="app.inputBorder"
            color="app.inputColor"
            fontFamily="mono"
            _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
          />
        </Box>
        <Box flex="1" minW="200px">
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
            Active hours (local, empty = always)
          </Text>
          <Flex align="center" gap={1.5}>
            <input
              type="time"
              value={rule.activeFrom}
              onChange={(e) => update({ activeFrom: e.target.value })}
              placeholder="--:--"
              style={{
                flex: 1,
                background: 'var(--chakra-colors-app-inputBg, rgba(255,255,255,0.04))',
                border: '1px solid var(--chakra-colors-app-inputBorder, rgba(255,255,255,0.1))',
                borderRadius: '6px',
                color: rule.activeFrom ? accent : 'var(--chakra-colors-app-textDim, #6b7280)',
                fontFamily: 'monospace',
                fontSize: '12px',
                fontWeight: rule.activeFrom ? 700 : 400,
                padding: '4px 6px',
                cursor: 'pointer',
                outline: 'none',
              }}
            />
            <Text fontFamily="mono" fontSize="10px" color="app.textDim" flexShrink={0}>→</Text>
            <input
              type="time"
              value={rule.activeTo}
              onChange={(e) => update({ activeTo: e.target.value })}
              placeholder="--:--"
              style={{
                flex: 1,
                background: 'var(--chakra-colors-app-inputBg, rgba(255,255,255,0.04))',
                border: '1px solid var(--chakra-colors-app-inputBorder, rgba(255,255,255,0.1))',
                borderRadius: '6px',
                color: rule.activeTo ? accent : 'var(--chakra-colors-app-textDim, #6b7280)',
                fontFamily: 'monospace',
                fontSize: '12px',
                fontWeight: rule.activeTo ? 700 : 400,
                padding: '4px 6px',
                cursor: 'pointer',
                outline: 'none',
              }}
            />
            {(rule.activeFrom || rule.activeTo) && (
              <Button
                size="xs"
                variant="ghost"
                color="app.textDim"
                _hover={{ color: 'red.400' }}
                onClick={() => update({ activeFrom: '', activeTo: '' })}
                px={1}
                flexShrink={0}
                title="Clear time window"
              >
                <Trash2 size={10} />
              </Button>
            )}
          </Flex>
        </Box>
      </Flex>

      {/* Action row */}
      <Flex align="center" gap={4} pt={2} borderTopWidth="1px" borderColor="rgba(249,115,22,0.15)">
        <Flex align="center" gap={2} flexShrink={0}>
          <Switch.Root
            size="sm"
            colorPalette="red"
            checked={rule.block}
            onCheckedChange={(e) => update({ block: e.checked, allowSinks: e.checked ? [] : rule.allowSinks })}
          >
            <Switch.HiddenInput />
            <Switch.Control><Switch.Thumb /></Switch.Control>
          </Switch.Root>
          <Text fontFamily="mono" fontSize="10px" color={rule.block ? 'red.400' : 'app.textDim'} fontWeight={rule.block ? '600' : '400'}>
            BLOCK
          </Text>
        </Flex>
        {!rule.block && (
          <Box flex={1}>
            <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" mb={1} textTransform="uppercase" letterSpacing="wider">
              Allow sinks (comma-separated, empty = all)
            </Text>
            <Input
              size="xs"
              placeholder="* (all sinks)"
              value={rule.allowSinks.join(', ')}
              onChange={(e) => {
                const v = e.target.value.trim()
                update({ allowSinks: v === '' ? [] : v.split(',').map((s) => s.trim()).filter(Boolean) })
              }}
              bg="app.inputBg"
              borderColor="app.inputBorder"
              color="app.inputColor"
              fontFamily="mono"
              _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
            />
          </Box>
        )}
      </Flex>

      {/* Extras conditions */}
      <Box pt={2} borderTopWidth="1px" borderColor="rgba(249,115,22,0.15)" mt={2}>
        <Flex align="center" justify="space-between" mb={1.5}>
          <Text fontFamily="mono" fontSize="10px" color="rgba(249,115,22,0.7)" textTransform="uppercase" letterSpacing="wider">
            Extras conditions
          </Text>
          <Button
            size="xs"
            variant="ghost"
            color={accent}
            _hover={{ bg: 'rgba(249,115,22,0.08)' }}
            onClick={() => update({ extraConditions: [...(rule.extraConditions ?? []), { key: '', value: '' }] })}
            gap={1}
            fontFamily="mono"
            fontSize="10px"
            px={2}
          >
            <Plus size={10} />
            Add
          </Button>
        </Flex>

        {(rule.extraConditions ?? []).length === 0 ? (
          <Text fontFamily="mono" fontSize="10px" color="app.textDim" opacity={0.5}>
            No extra conditions
          </Text>
        ) : (
          <VStack gap={1}>
            {(rule.extraConditions ?? []).map((cond, ci) => (
              <Flex key={ci} gap={1.5} align="center" w="full">
                <Input
                  size="xs"
                  placeholder="key (e.g. todoist.project_id)"
                  value={cond.key}
                  onChange={(e) => {
                    const next = (rule.extraConditions ?? []).map((c, i) =>
                      i === ci ? { ...c, key: e.target.value } : c
                    )
                    update({ extraConditions: next })
                  }}
                  flex={2}
                  bg="app.inputBg"
                  borderColor="app.inputBorder"
                  color="app.inputColor"
                  fontFamily="mono"
                  _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
                />
                <Text fontFamily="mono" fontSize="10px" color="app.textDim" flexShrink={0}>=</Text>
                <Input
                  size="xs"
                  placeholder="value (empty = key exists)"
                  value={cond.value}
                  onChange={(e) => {
                    const next = (rule.extraConditions ?? []).map((c, i) =>
                      i === ci ? { ...c, value: e.target.value } : c
                    )
                    update({ extraConditions: next })
                  }}
                  flex={2}
                  bg="app.inputBg"
                  borderColor="app.inputBorder"
                  color="app.inputColor"
                  fontFamily="mono"
                  _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
                />
                <Button
                  size="xs"
                  variant="ghost"
                  color="app.textDim"
                  _hover={{ color: 'red.400', bg: 'rgba(248,113,113,0.08)' }}
                  onClick={() => update({ extraConditions: (rule.extraConditions ?? []).filter((_, i) => i !== ci) })}
                  px={1}
                  flexShrink={0}
                >
                  <Trash2 size={11} />
                </Button>
              </Flex>
            ))}
          </VStack>
        )}
      </Box>
    </Box>
  )
}

// ─────────────────────────── Rule history panel ─────────────────────────────

const ACTION_STYLES: Record<string, { color: string; bg: string; label: string }> = {
  blocked:    { color: '#f87171', bg: 'rgba(248,113,113,0.12)', label: 'BLOCKED' },
  restricted: { color: '#f97316', bg: 'rgba(249,115,22,0.12)',  label: 'RESTRICTED' },
  allowed:    { color: '#4ade80', bg: 'rgba(74,222,128,0.12)',  label: 'ALLOWED' },
}

function RuleHistoryPanel({ api }: { api: ArrrApi }) {
  const [entries, setEntries] = useState<RoutingLogEntry[]>([])
  const [loading, setLoading] = useState(false)
  const [open, setOpen] = useState(false)

  useEffect(() => {
    if (!open) return
    let active = true

    async function poll() {
      setLoading(true)
      try {
        const data = await api.getRoutingLog(50)
        if (active) setEntries(data)
      } finally {
        if (active) setLoading(false)
      }
    }

    poll()
    const id = setInterval(poll, 5000)
    return () => { active = false; clearInterval(id) }
  }, [open, api])

  return (
    <Box mt={6} borderRadius="lg" borderWidth="1px" borderColor="app.cardBorder" overflow="hidden">
      <Flex
        align="center"
        justify="space-between"
        px={3}
        py={2}
        cursor="pointer"
        _hover={{ bg: 'whiteAlpha.50' }}
        onClick={() => setOpen((v) => !v)}
        userSelect="none"
      >
        <Flex align="center" gap={2}>
          <Text fontFamily="mono" fontSize="10px" color="app.textDim" textTransform="uppercase" letterSpacing="wider">
            Rule History
          </Text>
          {entries.length > 0 && (
            <Box
              fontFamily="mono"
              fontSize="9px"
              px={1.5}
              py={0.5}
              borderRadius="full"
              bg="rgba(249,115,22,0.15)"
              color="#f97316"
              lineHeight={1}
            >
              {entries.length}
            </Box>
          )}
        </Flex>
        <Flex align="center" gap={2}>
          {loading && <Spinner size="xs" color="orange.500" />}
          <Text fontFamily="mono" fontSize="10px" color="app.textDim">{open ? '▲' : '▼'}</Text>
        </Flex>
      </Flex>

      {open && (
        <Box borderTopWidth="1px" borderColor="app.cardBorder" maxH="360px" overflowY="auto">
          {entries.length === 0 ? (
            <Flex align="center" justify="center" py={8}>
              <Text fontFamily="mono" fontSize="xs" color="app.textDim" opacity={0.5}>
                No routing events yet
              </Text>
            </Flex>
          ) : (
            <VStack gap={0} align="stretch">
              {entries.map((e, i) => {
                const style = ACTION_STYLES[e.action] ?? ACTION_STYLES.allowed
                const ts = new Date(e.timestamp)
                const timeStr = ts.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
                return (
                  <Flex
                    key={i}
                    px={3}
                    py={2}
                    gap={3}
                    align="center"
                    borderBottomWidth={i < entries.length - 1 ? '1px' : '0'}
                    borderColor="app.cardBorder"
                    _hover={{ bg: 'whiteAlpha.30' }}
                    style={{ transition: 'background 0.15s' }}
                  >
                    {/* Time */}
                    <Text fontFamily="mono" fontSize="10px" color="app.textDim" flexShrink={0} w="72px">
                      {timeStr}
                    </Text>

                    {/* Action badge */}
                    <Box
                      fontFamily="mono"
                      fontSize="9px"
                      fontWeight="700"
                      px={1.5}
                      py={0.5}
                      borderRadius="4px"
                      bg={style.bg}
                      color={style.color}
                      flexShrink={0}
                      w="72px"
                      textAlign="center"
                    >
                      {style.label}
                    </Box>

                    {/* Rule name */}
                    <Text fontFamily="mono" fontSize="10px" color="#f97316" flexShrink={0} w="120px" truncate>
                      {e.ruleName}
                    </Text>

                    {/* Notification info */}
                    <Box flex={1} minW={0}>
                      <Text fontFamily="mono" fontSize="10px" color="app.text" truncate>
                        {e.notificationTitle}
                      </Text>
                      <Text fontFamily="mono" fontSize="9px" color="app.textDim" truncate>
                        {e.notificationSource}
                      </Text>
                    </Box>

                    {/* Target sinks */}
                    <Flex gap={1} flexShrink={0} flexWrap="wrap" justify="flex-end" maxW="140px">
                      {e.action === 'blocked' ? (
                        <Box fontFamily="mono" fontSize="9px" px={1} py={0.5} borderRadius="3px"
                          bg="rgba(248,113,113,0.1)" color="#f87171">
                          —
                        </Box>
                      ) : e.targetSinks.length === 0 ? (
                        <Box fontFamily="mono" fontSize="9px" px={1} py={0.5} borderRadius="3px"
                          bg="rgba(74,222,128,0.1)" color="#4ade80">
                          all
                        </Box>
                      ) : (
                        e.targetSinks.map((s) => (
                          <Box key={s} fontFamily="mono" fontSize="9px" px={1} py={0.5} borderRadius="3px"
                            bg="rgba(249,115,22,0.1)" color="#f97316">
                            {s}
                          </Box>
                        ))
                      )}
                    </Flex>
                  </Flex>
                )
              })}
            </VStack>
          )}
        </Box>
      )}
    </Box>
  )
}

// ─────────────────────────── Main view ──────────────────────────────────────

export function RoutingView({ api, onToast }: Props) {
  const accent = '#f97316'

  const [fullConfig, setFullConfig] = useState<DaemonConfig | null>(null)
  const [routing, setRouting] = useState<RoutingConfig>({ enabled: false, rules: [] })
  const [original, setOriginal] = useState<RoutingConfig>({ enabled: false, rules: [] })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  const isDirty = JSON.stringify(routing) !== JSON.stringify(original)
  const importRef = useRef<HTMLInputElement>(null)

  useEffect(() => { load() }, []) // eslint-disable-line react-hooks/exhaustive-deps

  async function load() {
    setLoading(true)
    try {
      const cfg = await api.getDaemonConfig()
      const r = cfg.routing ?? { enabled: false, rules: [] }
      setFullConfig(cfg)
      setRouting(r)
      setOriginal(r)
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setLoading(false)
    }
  }

  async function handleSave() {
    if (!fullConfig) return
    setSaving(true)
    try {
      await api.saveDaemonConfig({ ...fullConfig, routing })
      setOriginal(routing)
      setFullConfig((c) => c ? { ...c, routing } : c)
      onToast('Routing rules saved', 'success')
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setSaving(false)
    }
  }

  function exportRules() {
    const json = JSON.stringify(routing.rules, null, 2)
    const blob = new Blob([json], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'arrr-routing-rules.json'
    a.click()
    URL.revokeObjectURL(url)
  }

  function importRules(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const parsed = JSON.parse(ev.target?.result as string)
        const rules: RoutingRule[] = Array.isArray(parsed) ? parsed : parsed.rules ?? []
        patch({ rules })
        onToast(`Imported ${rules.length} rule${rules.length !== 1 ? 's' : ''}`, 'success')
      } catch {
        onToast('Invalid JSON file', 'error')
      } finally {
        if (importRef.current) importRef.current.value = ''
      }
    }
    reader.readAsText(file)
  }

  function patch(p: Partial<RoutingConfig>) {
    setRouting((r) => ({ ...r, ...p }))
  }

  function addRule() {
    patch({ rules: [...routing.rules, defaultRule()] })
  }

  function updateRule(i: number, r: RoutingRule) {
    patch({ rules: routing.rules.map((x, idx) => (idx === i ? r : x)) })
  }

  function deleteRule(i: number) {
    patch({ rules: routing.rules.filter((_, idx) => idx !== i) })
  }

  function moveRule(from: number, dir: -1 | 1) {
    const to = from + dir
    if (to < 0 || to >= routing.rules.length) return
    const rules = [...routing.rules]
    ;[rules[from], rules[to]] = [rules[to], rules[from]]
    patch({ rules })
  }

  if (loading) {
    return (
      <Flex justify="center" align="center" h="300px">
        <Spinner color="orange.500" size="lg" />
      </Flex>
    )
  }

  return (
    <>
      <Box maxW="720px" mx="auto" pb={isDirty ? '80px' : '0'} style={{ transition: 'padding-bottom 0.3s' }}>

        {/* Header */}
        <Flex align="center" justify="space-between" mb={6} style={{ animation: 'slideUp 0.3s ease both' }}>
          <Box>
            <Text fontFamily="mono" fontSize="xs" textTransform="uppercase" letterSpacing="widest" color="app.textMuted" lineHeight={1}>
              Routing Rules
            </Text>
            <Text fontSize="10px" color="app.textDim" fontFamily="mono" mt={0.5}>
              First-match-wins · source wildcard · block or restrict to specific sinks
            </Text>
          </Box>

          {/* Enable toggle */}
          <Flex align="center" gap={2}>
            <Text fontFamily="mono" fontSize="xs" color={routing.enabled ? accent : 'app.textDim'} fontWeight={routing.enabled ? '600' : '400'}>
              {routing.enabled ? 'ENABLED' : 'DISABLED'}
            </Text>
            <Switch.Root
              size="md"
              colorPalette="orange"
              checked={routing.enabled}
              onCheckedChange={(e) => patch({ enabled: e.checked })}
            >
              <Switch.HiddenInput />
              <Switch.Control><Switch.Thumb /></Switch.Control>
            </Switch.Root>
          </Flex>
        </Flex>

        {/* Rules toolbar */}
        <Flex align="center" justify="space-between" mb={3}>
          <Text fontFamily="mono" fontSize="10px" color="app.textDim" textTransform="uppercase" letterSpacing="widest">
            {routing.rules.length} rule{routing.rules.length !== 1 ? 's' : ''} · evaluated top to bottom
          </Text>
          <HStack gap={1}>
            <Button
              size="xs"
              variant="ghost"
              color="app.textMuted"
              _hover={{ color: 'app.textMuted', bg: 'whiteAlpha.50' }}
              onClick={exportRules}
              gap={1}
              fontFamily="mono"
              fontSize="10px"
              title="Export rules as JSON"
            >
              <Download size={11} />
              Export
            </Button>
            <Button
              size="xs"
              variant="ghost"
              color="app.textMuted"
              _hover={{ color: 'app.textMuted', bg: 'whiteAlpha.50' }}
              onClick={() => importRef.current?.click()}
              gap={1}
              fontFamily="mono"
              fontSize="10px"
              title="Import rules from JSON"
            >
              <Upload size={11} />
              Import
            </Button>
            <input
              ref={importRef}
              type="file"
              accept=".json,application/json"
              style={{ display: 'none' }}
              onChange={importRules}
            />
            <Button
              size="xs"
              variant="outline"
              colorPalette="orange"
              onClick={addRule}
              gap={1}
              fontFamily="mono"
              fontSize="10px"
            >
              <Plus size={11} />
              Add rule
            </Button>
          </HStack>
        </Flex>

        {/* Rules list */}
        {routing.rules.length === 0 ? (
          <Flex
            align="center"
            justify="center"
            direction="column"
            gap={2}
            py={12}
            borderRadius="xl"
            borderWidth="1px"
            borderStyle="dashed"
            borderColor="app.cardBorder"
            opacity={0.6}
          >
            <Text fontSize="2xl">🔀</Text>
            <Text fontFamily="mono" fontSize="xs" color="app.textDim">
              No rules — all notifications go to all sinks
            </Text>
            <Button size="xs" variant="ghost" colorPalette="orange" onClick={addRule} gap={1} mt={1}>
              <Plus size={11} />
              Add first rule
            </Button>
          </Flex>
        ) : (
          <VStack gap={2}>
            {routing.rules.map((rule, i) => (
              <RuleRow
                key={i}
                rule={rule}
                index={i}
                total={routing.rules.length}
                onChange={(r) => updateRule(i, r)}
                onDelete={() => deleteRule(i)}
                onMoveUp={() => moveRule(i, -1)}
                onMoveDown={() => moveRule(i, 1)}
              />
            ))}
          </VStack>
        )}

        {/* Rule history */}
        <RuleHistoryPanel api={api} />

        {/* Legend */}
        <Box mt={6} p={3} borderRadius="lg" borderWidth="1px" borderColor="app.cardBorder" opacity={0.7}>
          <Text fontFamily="mono" fontSize="10px" color="app.textDim" mb={1.5} textTransform="uppercase" letterSpacing="wider">
            How rules are evaluated
          </Text>
          <Flex direction="column" gap={1}>
            {[
              ['source pattern', 'exact match or trailing * wildcard (e.g. com.arrr.plugin.*)'],
              ['title / body', 'case-insensitive substring — empty = match anything'],
              ['min priority', '0 = Normal (matches all), 1 = High, 2 = Critical'],
              ['active hours', 'local time window — supports midnight crossing (e.g. 22:00 → 08:00)'],
              ['block', 'drop notification entirely — no sink receives it'],
              ['allow sinks', 'restrict to listed sink IDs (intersection with running sinks)'],
              ['extras', 'match Notification.Extras[key] contains value (case-insensitive)'],
            ].map(([k, v]) => (
              <Flex key={k} gap={2} align="baseline">
                <Text fontFamily="mono" fontSize="10px" color={accent} flexShrink={0} w="100px">{k}</Text>
                <Text fontFamily="mono" fontSize="10px" color="app.textDim">{v}</Text>
              </Flex>
            ))}
          </Flex>
        </Box>
      </Box>

      {/* Floating save bar */}
      <Box
        position="fixed"
        bottom={0}
        left={0}
        right={0}
        zIndex={50}
        style={{
          transform: isDirty ? 'translateY(0)' : 'translateY(100%)',
          transition: 'transform 0.3s cubic-bezier(0.16, 1, 0.3, 1)',
        }}
      >
        <Box
          bg="rgba(8,12,20,0.95)"
          borderTopWidth="1px"
          borderColor="orange.800"
          backdropFilter="blur(16px)"
          px={6}
          py={3}
          boxShadow="0 -4px 32px rgba(249,115,22,0.12)"
        >
          <Box position="absolute" top={0} left={0} right={0} h="1px"
            bgGradient="to-r" gradientFrom="transparent" gradientVia="orange.500" gradientTo="transparent" opacity={0.6}
          />
          <Flex align="center" justify="space-between" maxW="720px" mx="auto">
            <Flex align="center" gap={3}>
              <Box w="6px" h="6px" borderRadius="full" bg="orange.400"
                style={{ animation: 'pulse 1.5s ease-in-out infinite' }}
              />
              <Text fontFamily="mono" fontSize="xs" color="orange.300" fontWeight="600">
                UNSAVED CHANGES
              </Text>
            </Flex>
            <HStack gap={2}>
              <Button size="sm" variant="ghost" color="app.textMuted" fontFamily="mono" fontSize="xs"
                onClick={() => setRouting(original)} disabled={saving} gap={1.5}
              >
                <RefreshCcw size={12} />
                Reset
              </Button>
              <Button size="sm" colorPalette="orange" fontFamily="mono" fontSize="xs"
                onClick={handleSave} loading={saving} gap={1.5}
              >
                <Save size={12} />
                Save
              </Button>
            </HStack>
          </Flex>
        </Box>
      </Box>

      <style>{`
        @keyframes slideUp {
          from { opacity: 0; transform: translateY(16px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.4; }
        }
      `}</style>
    </>
  )
}
