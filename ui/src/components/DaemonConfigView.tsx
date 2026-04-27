import { useEffect, useState } from 'react'
import {
  Box,
  Button,
  Flex,
  HStack,
  Input,
  Spinner,
  Switch,
  Text,
  Tooltip,
} from '@chakra-ui/react'
import {
  Eye,
  EyeOff,
  Globe,
  History,
  KeyRound,
  Plus,
  RefreshCcw,
  Save,
  Settings,
  ShieldCheck,
  Timer,
  Trash2,
  Zap,
} from 'lucide-react'
import type { ArrrApi } from '../api'
import type { DaemonConfig, DigestScheduleEntry, Settings as AppSettings } from '../types'

interface Props {
  api: ArrrApi
  onToast: (msg: string, type: 'success' | 'error') => void
  onSettingsChanged: (s: Partial<AppSettings>) => void
}

function diff(a: DaemonConfig, b: DaemonConfig): string[] {
  const labels: Record<keyof DaemonConfig, string> = {
    apiKey: 'API Key',
    isDebug: 'Debug Mode',
    port: 'Port',
    deduplicationEnabled: 'Deduplication',
    deduplicationWindowSeconds: 'Window',
    historyEnabled: 'History',
    digest: 'Digest',
    routing: 'Routing',
  }
  return (Object.keys(a) as (keyof DaemonConfig)[])
    .filter((k) => {
      if (k === 'digest' || k === 'routing') return JSON.stringify(a[k]) !== JSON.stringify(b[k])
      return a[k] !== b[k]
    })
    .map((k) => labels[k] ?? k)
    .filter(Boolean)
}

// ─────────────────────────── Pod wrapper ────────────────────────────────────

interface PodProps {
  accent: string
  icon: React.ReactNode
  title: string
  subtitle?: string
  children: React.ReactNode
  delay?: number
}

function Pod({ accent, icon, title, subtitle, children, delay = 0 }: PodProps) {
  return (
    <Box
      bg="app.cardBg"
      borderWidth="1px"
      borderColor="app.cardBorder"
      borderRadius="xl"
      overflow="hidden"
      style={{
        animation: `slideUp 0.4s cubic-bezier(0.16, 1, 0.3, 1) ${delay}ms both`,
      }}
    >
      {/* Left accent rail + header */}
      <Flex borderBottomWidth="1px" borderColor="app.cardBorder">
        <Box w="3px" bg={accent} flexShrink={0} borderTopLeftRadius="xl" />
        <Flex align="center" gap={2.5} px={5} py={3.5} flex={1}>
          <Box color={accent} opacity={0.85}>
            {icon}
          </Box>
          <Box>
            <Text
              fontFamily="mono"
              fontSize="xs"
              fontWeight="700"
              textTransform="uppercase"
              letterSpacing="widest"
              color={accent}
              lineHeight={1}
            >
              {title}
            </Text>
            {subtitle && (
              <Text fontSize="10px" color="app.textDim" fontFamily="mono" mt={0.5}>
                {subtitle}
              </Text>
            )}
          </Box>
        </Flex>
      </Flex>

      {/* Content */}
      <Box px={5} py={4}>
        {children}
      </Box>
    </Box>
  )
}

// ─────────────────────────── Field row ──────────────────────────────────────

interface FieldRowProps {
  label: string
  hint?: string
  dirty?: boolean
  children: React.ReactNode
}

function FieldRow({ label, hint, dirty, children }: FieldRowProps) {
  return (
    <Flex align="center" py={2.5} gap={4} borderBottomWidth="1px" borderColor="app.cardBorder"
      _last={{ borderBottom: 'none' }}
    >
      <Box flex="0 0 44%">
        <Flex align="center" gap={1.5}>
          {dirty && (
            <Box
              w="5px"
              h="5px"
              borderRadius="full"
              bg="amber.400"
              flexShrink={0}
              style={{ animation: 'pulse 1.5s ease-in-out infinite' }}
            />
          )}
          <Text
            fontFamily="mono"
            fontSize="xs"
            color={dirty ? 'amber.300' : 'app.textMuted'}
            fontWeight={dirty ? '600' : '400'}
            transition="color 0.2s"
          >
            {label}
          </Text>
        </Flex>
        {hint && (
          <Text fontSize="10px" color="app.textDim" fontFamily="mono" mt={0.5} ml={dirty ? 4 : 0}>
            {hint}
          </Text>
        )}
      </Box>
      <Box flex={1}>{children}</Box>
    </Flex>
  )
}

// ─────────────────────────── Digest slot row ────────────────────────────────

interface SlotRowProps {
  entry: DigestScheduleEntry
  index: number
  onChange: (index: number, patch: Partial<DigestScheduleEntry>) => void
  onDelete: (index: number) => void
}

function SlotRow({ entry, index, onChange, onDelete }: SlotRowProps) {
  const accent = '#2dd4bf'

  return (
    <Flex
      align="center"
      gap={2.5}
      py={2.5}
      px={3}
      borderRadius="lg"
      borderWidth="1px"
      borderColor="app.cardBorder"
      bg="app.inputBg"
      _hover={{ borderColor: accent + '44' }}
      style={{
        animation: `slideUp 0.25s cubic-bezier(0.16,1,0.3,1) ${index * 40}ms both`,
        transition: 'border-color 0.2s',
      }}
    >
      {/* Emoji pill */}
      <Input
        value={entry.titleEmoji}
        onChange={(e) => onChange(index, { titleEmoji: e.target.value })}
        size="xs"
        w="44px"
        textAlign="center"
        fontSize="lg"
        bg="transparent"
        border="none"
        p={0}
        _focus={{ outline: 'none', boxShadow: 'none' }}
        title="Emoji"
      />

      {/* Time badge */}
      <Box
        px={2.5}
        py={1}
        borderRadius="md"
        bg="rgba(45,212,191,0.08)"
        borderWidth="1px"
        borderColor="rgba(45,212,191,0.2)"
        flexShrink={0}
      >
        <input
          type="time"
          value={entry.fireAt}
          onChange={(e) => onChange(index, { fireAt: e.target.value })}
          style={{
            background: 'transparent',
            border: 'none',
            outline: 'none',
            color: accent,
            fontFamily: 'monospace',
            fontSize: '12px',
            fontWeight: 700,
            letterSpacing: '0.05em',
            width: '72px',
            cursor: 'pointer',
          }}
        />
      </Box>

      {/* Label */}
      <Input
        value={entry.label}
        onChange={(e) => onChange(index, { label: e.target.value })}
        placeholder="Label…"
        size="xs"
        flex={1}
        bg="transparent"
        borderColor="app.cardBorder"
        color="app.inputColor"
        fontFamily="mono"
        fontSize="xs"
        _placeholder={{ color: 'app.placeholder' }}
        _focus={{ borderColor: accent, boxShadow: `0 0 0 1px ${accent}` }}
      />

      {/* Day offset toggle */}
      <Flex
        as="button"
        align="center"
        gap={1}
        px={2}
        py={1}
        borderRadius="md"
        borderWidth="1px"
        borderColor={entry.dayOffset === 0 ? 'rgba(45,212,191,0.4)' : 'rgba(167,139,250,0.4)'}
        bg={entry.dayOffset === 0 ? 'rgba(45,212,191,0.07)' : 'rgba(167,139,250,0.07)'}
        onClick={() => onChange(index, { dayOffset: entry.dayOffset === 0 ? 1 : 0 })}
        flexShrink={0}
        cursor="pointer"
        style={{ transition: 'all 0.2s' }}
        title="Toggle today/tomorrow"
      >
        <Text
          fontFamily="mono"
          fontSize="10px"
          fontWeight="600"
          color={entry.dayOffset === 0 ? '#2dd4bf' : '#a78bfa'}
          textTransform="uppercase"
          letterSpacing="wider"
          style={{ transition: 'color 0.2s' }}
        >
          {entry.dayOffset === 0 ? 'Today' : 'Tomorrow'}
        </Text>
      </Flex>

      {/* Delete */}
      <Button
        size="xs"
        variant="ghost"
        color="app.textDim"
        _hover={{ color: 'red.400', bg: 'rgba(248,113,113,0.08)' }}
        onClick={() => onDelete(index)}
        px={1.5}
        flexShrink={0}
      >
        <Trash2 size={12} />
      </Button>
    </Flex>
  )
}

// ─────────────────────────── Digest pod ─────────────────────────────────────

interface DigestPodProps {
  form: DaemonConfig
  original: DaemonConfig | null
  setForm: React.Dispatch<React.SetStateAction<DaemonConfig | null>>
}

function DigestPod({ form, original, setForm }: DigestPodProps) {
  const accent = '#2dd4bf'
  const digest = form.digest ?? { enabled: false, schedule: [] }
  const isDirty = JSON.stringify(digest) !== JSON.stringify(original?.digest)

  function patchDigest(patch: Partial<typeof digest>) {
    setForm((prev) =>
      prev ? { ...prev, digest: { ...prev.digest, ...patch } } : prev
    )
  }

  function updateSlot(index: number, patch: Partial<DigestScheduleEntry>) {
    const next = digest.schedule.map((e, i) => (i === index ? { ...e, ...patch } : e))
    patchDigest({ schedule: next })
  }

  function deleteSlot(index: number) {
    patchDigest({ schedule: digest.schedule.filter((_, i) => i !== index) })
  }

  function addSlot() {
    const newEntry: DigestScheduleEntry = {
      label: 'Digest',
      titleEmoji: '📋',
      fireAt: '08:00',
      dayOffset: 0,
    }
    patchDigest({ schedule: [...digest.schedule, newEntry] })
  }

  return (
    <Pod
      accent={isDirty ? accent : 'var(--chakra-colors-teal-600)'}
      icon={<span style={{ fontSize: 14 }}>📋</span>}
      title="Digest"
      subtitle="Morning & evening notification digests from registered providers"
      delay={200}
    >
      {/* Enable toggle */}
      <FieldRow
        label="enabled"
        hint="DigestService fires when any plugin implements IDigestProvider"
        dirty={digest.enabled !== original?.digest?.enabled}
      >
        <Switch.Root
          size="sm"
          colorPalette="teal"
          checked={digest.enabled}
          onCheckedChange={(e) => patchDigest({ enabled: e.checked })}
        >
          <Switch.HiddenInput />
          <Switch.Control>
            <Switch.Thumb />
          </Switch.Control>
        </Switch.Root>
      </FieldRow>

      {/* Schedule list */}
      <Box mt={3}>
        <Flex align="center" justify="space-between" mb={2}>
          <Text fontFamily="mono" fontSize="10px" color="app.textDim" textTransform="uppercase" letterSpacing="widest">
            Schedule — {digest.schedule.length} slot{digest.schedule.length !== 1 ? 's' : ''}
          </Text>
          <Button
            size="xs"
            variant="ghost"
            color={accent}
            _hover={{ bg: 'rgba(45,212,191,0.08)' }}
            onClick={addSlot}
            gap={1}
            fontFamily="mono"
            fontSize="10px"
          >
            <Plus size={11} />
            Add slot
          </Button>
        </Flex>

        {digest.schedule.length === 0 ? (
          <Flex
            align="center"
            justify="center"
            direction="column"
            gap={1.5}
            py={6}
            borderRadius="lg"
            borderWidth="1px"
            borderStyle="dashed"
            borderColor="app.cardBorder"
            opacity={0.6}
          >
            <Text fontSize="18px">📋</Text>
            <Text fontFamily="mono" fontSize="10px" color="app.textDim">
              No digest slots configured
            </Text>
          </Flex>
        ) : (
          <Flex direction="column" gap={1.5}>
            {digest.schedule.map((entry, i) => (
              <SlotRow
                key={i}
                entry={entry}
                index={i}
                onChange={updateSlot}
                onDelete={deleteSlot}
              />
            ))}
          </Flex>
        )}
      </Box>
    </Pod>
  )
}

// ─────────────────────────── Main view ──────────────────────────────────────

export function DaemonConfigView({ api, onToast, onSettingsChanged }: Props) {
  const [original, setOriginal] = useState<DaemonConfig | null>(null)
  const [form, setForm] = useState<DaemonConfig | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [showKey, setShowKey] = useState(false)

  const changedFields = original && form ? diff(original, form) : []
  const isDirty = changedFields.length > 0

  useEffect(() => {
    load()
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  async function load() {
    setLoading(true)
    try {
      const cfg = await api.getDaemonConfig()
      const normalized = {
        ...cfg,
        digest: cfg.digest ?? { enabled: false, schedule: [] },
      }
      setOriginal(normalized)
      setForm(normalized)
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setLoading(false)
    }
  }

  function set<K extends keyof DaemonConfig>(key: K, value: DaemonConfig[K]) {
    setForm((prev) => prev ? { ...prev, [key]: value } : prev)
  }

  async function handleSave() {
    if (!form) return
    setSaving(true)
    try {
      await api.saveDaemonConfig(form)
      if (original?.apiKey !== form.apiKey) {
        onSettingsChanged({ apiKey: form.apiKey })
      }
      if (original?.port !== form.port) {
        onToast('Port change takes effect after restart', 'success')
      }
      setOriginal(form)
      onToast('Configuration saved', 'success')
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setSaving(false)
    }
  }

  function handleReset() {
    setForm(original)
  }

  if (loading) {
    return (
      <Flex justify="center" align="center" h="300px">
        <Spinner color="amber.500" size="lg" />
      </Flex>
    )
  }

  if (!form) return null

  return (
    <>
      <Box maxW="680px" mx="auto" pb={isDirty ? '80px' : '0'} style={{ transition: 'padding-bottom 0.3s' }}>
        {/* Page header */}
        <Flex align="center" gap={3} mb={6} style={{ animation: 'slideUp 0.3s ease both' }}>
          <Box color="app.textDim">
            <Settings size={18} />
          </Box>
          <Box>
            <Text
              fontFamily="mono"
              fontSize="xs"
              textTransform="uppercase"
              letterSpacing="widest"
              color="app.textMuted"
              lineHeight={1}
            >
              Daemon Configuration
            </Text>
            <Text fontSize="10px" color="app.textDim" fontFamily="mono" mt={0.5}>
              Changes to Port require a service restart
            </Text>
          </Box>
        </Flex>

        {/* ── Security & Access ── */}
        <Pod
          accent="var(--chakra-colors-amber-500)"
          icon={<ShieldCheck size={14} />}
          title="Security & Access"
          subtitle="Authentication and debug mode"
          delay={0}
        >
          <FieldRow
            label="api_key"
            hint="Required on every request"
            dirty={form.apiKey !== original?.apiKey}
          >
            <Flex gap={2}>
              <Input
                value={form.apiKey}
                type={showKey ? 'text' : 'password'}
                onChange={(e) => set('apiKey', e.target.value)}
                size="sm"
                bg="app.inputBg"
                borderColor="app.inputBorder"
                color="app.inputColor"
                fontFamily="mono"
                fontSize="xs"
                _placeholder={{ color: 'app.placeholder' }}
                _focus={{
                  borderColor: 'amber.500',
                  boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)',
                }}
              />
              <Tooltip.Root>
                <Tooltip.Trigger asChild>
                  <Button
                    size="sm"
                    variant="ghost"
                    px={2}
                    color="app.textMuted"
                    _hover={{ color: 'amber.300' }}
                    onClick={() => setShowKey((v) => !v)}
                  >
                    {showKey ? <EyeOff size={13} /> : <Eye size={13} />}
                  </Button>
                </Tooltip.Trigger>
                <Tooltip.Positioner>
                  <Tooltip.Content fontFamily="mono" fontSize="xs">
                    {showKey ? 'Hide' : 'Reveal'}
                  </Tooltip.Content>
                </Tooltip.Positioner>
              </Tooltip.Root>
            </Flex>
          </FieldRow>

          <FieldRow
            label="debug_mode"
            hint="Enables /openapi + /scalar/v1"
            dirty={form.isDebug !== original?.isDebug}
          >
            <Switch.Root
              size="sm"
              colorPalette="amber"
              checked={form.isDebug}
              onCheckedChange={(e) => set('isDebug', e.checked)}
            >
              <Switch.HiddenInput />
              <Switch.Control>
                <Switch.Thumb />
              </Switch.Control>
            </Switch.Root>
          </FieldRow>
        </Pod>

        {/* ── Network ── */}
        <Pod
          accent="#38bdf8"
          icon={<Globe size={14} />}
          title="Network"
          subtitle="HTTP listener settings"
          delay={60}
        >
          <FieldRow
            label="http_port"
            hint="Restart required"
            dirty={form.port !== original?.port}
          >
            <Input
              value={form.port}
              type="number"
              min={1}
              max={65535}
              onChange={(e) => set('port', Number(e.target.value))}
              size="sm"
              w="120px"
              bg="app.inputBg"
              borderColor="app.inputBorder"
              color="app.inputColor"
              fontFamily="mono"
              fontSize="xs"
              _focus={{
                borderColor: '#38bdf8',
                boxShadow: '0 0 0 1px #38bdf8',
              }}
            />
          </FieldRow>
        </Pod>

        {/* ── Notification Deduplication ── */}
        <Pod
          accent="#4ade80"
          icon={<Zap size={14} />}
          title="Notification Deduplication"
          subtitle="Suppress identical notifications within a time window"
          delay={120}
        >
          <FieldRow
            label="enabled"
            dirty={form.deduplicationEnabled !== original?.deduplicationEnabled}
          >
            <Switch.Root
              size="sm"
              colorPalette="green"
              checked={form.deduplicationEnabled}
              onCheckedChange={(e) => set('deduplicationEnabled', e.checked)}
            >
              <Switch.HiddenInput />
              <Switch.Control>
                <Switch.Thumb />
              </Switch.Control>
            </Switch.Root>
          </FieldRow>

          <FieldRow
            label="window_seconds"
            hint="Duration to suppress duplicates"
            dirty={form.deduplicationWindowSeconds !== original?.deduplicationWindowSeconds}
          >
            <Flex align="center" gap={3}>
              <Input
                value={form.deduplicationWindowSeconds}
                type="number"
                min={1}
                max={86400}
                onChange={(e) => set('deduplicationWindowSeconds', Number(e.target.value))}
                size="sm"
                w="100px"
                bg="app.inputBg"
                borderColor="app.inputBorder"
                color="app.inputColor"
                fontFamily="mono"
                fontSize="xs"
                disabled={!form.deduplicationEnabled}
                opacity={form.deduplicationEnabled ? 1 : 0.4}
                _focus={{
                  borderColor: '#4ade80',
                  boxShadow: '0 0 0 1px #4ade80',
                }}
              />
              <Flex align="center" gap={1} color="app.textDim">
                <Timer size={11} />
                <Text fontSize="10px" fontFamily="mono">
                  {form.deduplicationWindowSeconds >= 60
                    ? `${Math.round(form.deduplicationWindowSeconds / 60)}m`
                    : `${form.deduplicationWindowSeconds}s`}
                </Text>
              </Flex>
            </Flex>
          </FieldRow>
        </Pod>

        {/* ── Notification History ── */}
        <Pod
          accent="#a78bfa"
          icon={<History size={14} />}
          title="Notification History"
          subtitle="Persist incoming notifications to local SQLite database"
          delay={160}
        >
          <FieldRow
            label="enabled"
            hint="Record every notification for later browsing"
            dirty={form.historyEnabled !== original?.historyEnabled}
          >
            <Switch.Root
              size="sm"
              colorPalette="purple"
              checked={form.historyEnabled}
              onCheckedChange={(e) => set('historyEnabled', e.checked)}
            >
              <Switch.HiddenInput />
              <Switch.Control>
                <Switch.Thumb />
              </Switch.Control>
            </Switch.Root>
          </FieldRow>
        </Pod>

        {/* ── Digest ── */}
        <DigestPod form={form} original={original} setForm={setForm} />

        {/* ── API Key hint ── */}
        {original?.apiKey !== form.apiKey && (
          <Box
            mt={3}
            p={3}
            borderRadius="lg"
            borderWidth="1px"
            borderColor="amber.800"
            bg="rgba(245,158,11,0.08)"
            style={{ animation: 'slideUp 0.2s ease both' }}
          >
            <Flex align="center" gap={2}>
              <KeyRound size={12} color="var(--chakra-colors-amber-400)" />
              <Text fontSize="xs" fontFamily="mono" color="amber.300">
                API key change — your connection settings will be updated on save
              </Text>
            </Flex>
          </Box>
        )}
      </Box>

      {/* ── Floating save bar ── */}
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
          borderColor="amber.800"
          backdropFilter="blur(16px)"
          px={6}
          py={3}
          boxShadow="0 -4px 32px rgba(245,158,11,0.12)"
        >
          {/* amber glow line at top */}
          <Box
            position="absolute"
            top={0}
            left={0}
            right={0}
            h="1px"
            bgGradient="to-r"
            gradientFrom="transparent"
            gradientVia="amber.500"
            gradientTo="transparent"
            opacity={0.6}
          />

          <Flex align="center" justify="space-between" maxW="680px" mx="auto">
            <Flex align="center" gap={3}>
              <Box
                w="6px"
                h="6px"
                borderRadius="full"
                bg="amber.400"
                style={{ animation: 'pulse 1.5s ease-in-out infinite' }}
              />
              <Text fontFamily="mono" fontSize="xs" color="amber.300" fontWeight="600">
                {changedFields.length} UNSAVED{' '}
                {changedFields.length === 1 ? 'CHANGE' : 'CHANGES'}
              </Text>
              <Text fontFamily="mono" fontSize="10px" color="app.textDim">
                {changedFields.join(' · ')}
              </Text>
            </Flex>

            <HStack gap={2}>
              <Button
                size="sm"
                variant="ghost"
                color="app.textMuted"
                fontFamily="mono"
                fontSize="xs"
                onClick={handleReset}
                disabled={saving}
                gap={1.5}
              >
                <RefreshCcw size={12} />
                Reset
              </Button>
              <Button
                size="sm"
                colorPalette="amber"
                fontFamily="mono"
                fontSize="xs"
                onClick={handleSave}
                loading={saving}
                gap={1.5}
              >
                <Save size={12} />
                Save Changes
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
      `}</style>
    </>
  )
}
