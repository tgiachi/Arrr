import { Box, Flex, Text, Input, Textarea, Button, Grid } from '@chakra-ui/react'
import { Zap, Terminal, Clock, CheckCircle, XCircle } from 'lucide-react'
import { useState, useRef } from 'react'
import type { ArrrApi } from '../api'

interface ShotEntry {
  id: number
  ts: string
  source: string
  title: string
  priority: number
  ok: boolean
  error?: string
}

const PRIORITIES = [
  { value: 0, label: 'Low',      color: '#6b7280' },
  { value: 1, label: 'Normal',   color: '#3b82f6' },
  { value: 2, label: 'High',     color: '#f59e0b' },
  { value: 3, label: 'Critical', color: '#ef4444' },
]

interface Props {
  api: ArrrApi
}

export function DebugView({ api }: Props) {
  const [source,   setSource]   = useState('debug.manual')
  const [title,    setTitle]    = useState('Test Notification')
  const [body,     setBody]     = useState('This is a debug notification fired from the Arrr UI.')
  const [priority, setPriority] = useState(1)
  const [iconUrl,  setIconUrl]  = useState('')
  const [url,      setUrl]      = useState('')
  const [extras,   setExtras]   = useState('')
  const [firing,   setFiring]   = useState(false)
  const [shots,    setShots]    = useState<ShotEntry[]>([])
  const [flashOk,  setFlashOk]  = useState(false)
  const counter = useRef(0)

  const parsedExtras = (): Record<string, string> | undefined => {
    if (!extras.trim()) return undefined
    try {
      return JSON.parse(extras)
    } catch {
      return undefined
    }
  }

  const fire = async () => {
    if (firing) return
    setFiring(true)
    const id = ++counter.current
    const ts = new Date().toLocaleTimeString()
    try {
      await api.sendNotification({
        source,
        title,
        body,
        priority,
        iconUrl: iconUrl || undefined,
        url:     url     || undefined,
        extras:  parsedExtras(),
      })
      setShots(prev => [{ id, ts, source, title, priority, ok: true }, ...prev].slice(0, 30))
      setFlashOk(true)
      setTimeout(() => setFlashOk(false), 600)
    } catch (e) {
      setShots(prev => [{ id, ts, source, title, priority, ok: false, error: String(e) }, ...prev].slice(0, 30))
    } finally {
      setFiring(false)
    }
  }

  const extrasError = extras.trim() && (() => {
    try { JSON.parse(extras); return null }
    catch (e) { return String(e) }
  })()

  const pColor = PRIORITIES[priority]?.color ?? '#6b7280'

  return (
    <Box
      minH="calc(100vh - 120px)"
      px={{ base: 4, md: 8 }}
      py={6}
      fontFamily="mono"
      position="relative"
    >
      {/* scanline texture overlay */}
      <Box
        position="fixed" inset={0} pointerEvents="none" zIndex={0}
        style={{
          backgroundImage: 'repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(0,0,0,0.03) 2px, rgba(0,0,0,0.03) 4px)',
        }}
      />

      {/* header */}
      <Flex align="center" gap={3} mb={6} position="relative" zIndex={1}>
        <Box color="#f59e0b">
          <Terminal size={18} />
        </Box>
        <Text fontSize="xs" letterSpacing="0.2em" textTransform="uppercase" color="#f59e0b" fontWeight="700">
          Notification Injector
        </Text>
        <Box flex={1} h="1px" bg="rgba(245,158,11,0.2)" />
        <Text fontSize="10px" color="app.textDim" letterSpacing="0.1em">
          DEV MODE
        </Text>
      </Flex>

      <Grid templateColumns={{ base: '1fr', lg: '1fr 380px' }} gap={6} position="relative" zIndex={1}>

        {/* ── Left: composer ── */}
        <Box
          bg="rgba(0,0,0,0.4)"
          borderWidth="1px"
          borderColor={flashOk ? 'rgba(34,197,94,0.5)' : 'rgba(245,158,11,0.15)'}
          borderRadius="lg"
          p={5}
          style={{
            transition: 'border-color 0.3s ease',
            boxShadow: flashOk
              ? '0 0 20px rgba(34,197,94,0.15)'
              : '0 0 0 transparent',
          }}
        >
          <Grid templateColumns="1fr 1fr" gap={4} mb={4}>
            <Field label="SOURCE">
              <Input
                value={source}
                onChange={e => setSource(e.target.value)}
                placeholder="debug.manual"
                {...inputStyle}
              />
            </Field>
            <Field label="ICON URL">
              <Input
                value={iconUrl}
                onChange={e => setIconUrl(e.target.value)}
                placeholder="https://…/icon.png"
                {...inputStyle}
              />
            </Field>
          </Grid>

          <Field label="TITLE" mb={4}>
            <Input
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Notification title"
              {...inputStyle}
            />
          </Field>

          <Field label="BODY" mb={4}>
            <Textarea
              value={body}
              onChange={e => setBody(e.target.value)}
              rows={3}
              placeholder="Notification body…"
              {...inputStyle}
              resize="vertical"
            />
          </Field>

          <Field label="URL (optional)" mb={4}>
            <Input
              value={url}
              onChange={e => setUrl(e.target.value)}
              placeholder="https://…"
              {...inputStyle}
            />
          </Field>

          {/* Priority */}
          <Box mb={5}>
            <Text fontSize="10px" letterSpacing="0.15em" color="app.textDim" mb={2}>
              PRIORITY
            </Text>
            <Flex gap={2}>
              {PRIORITIES.map(p => (
                <Box
                  key={p.value}
                  as="button"
                  onClick={() => setPriority(p.value)}
                  px={3} py={1}
                  borderRadius="md"
                  fontSize="xs"
                  fontFamily="mono"
                  fontWeight="600"
                  letterSpacing="0.05em"
                  borderWidth="1px"
                  borderColor={priority === p.value ? p.color : 'rgba(255,255,255,0.08)'}
                  color={priority === p.value ? p.color : 'app.textMuted'}
                  bg={priority === p.value ? `${p.color}18` : 'transparent'}
                  style={{ transition: 'all 0.15s ease', cursor: 'pointer' }}
                  _hover={{ borderColor: p.color, color: p.color }}
                >
                  {p.label}
                </Box>
              ))}
            </Flex>
          </Box>

          {/* Extras */}
          <Field
            label="EXTRAS (JSON)"
            error={extrasError ?? undefined}
            mb={6}
          >
            <Textarea
              value={extras}
              onChange={e => setExtras(e.target.value)}
              rows={3}
              placeholder={'{\n  "key": "value"\n}'}
              {...inputStyle}
              borderColor={extrasError ? 'rgba(239,68,68,0.5)' : inputStyle.borderColor}
              resize="vertical"
            />
          </Field>

          {/* Fire button */}
          <Button
            w="full"
            size="lg"
            onClick={fire}
            loading={firing}
            disabled={!source || !title || !body || !!extrasError}
            fontFamily="mono"
            fontWeight="700"
            letterSpacing="0.15em"
            fontSize="sm"
            textTransform="uppercase"
            bg={`${pColor}22`}
            color={pColor}
            borderWidth="1px"
            borderColor={`${pColor}55`}
            _hover={{ bg: `${pColor}33`, borderColor: pColor, boxShadow: `0 0 16px ${pColor}33` }}
            _active={{ bg: `${pColor}44` }}
            style={{ transition: 'all 0.15s ease' }}
          >
            <Zap size={14} />
            &nbsp;Fire
          </Button>
        </Box>

        {/* ── Right: shot log ── */}
        <Box
          bg="rgba(0,0,0,0.3)"
          borderWidth="1px"
          borderColor="rgba(255,255,255,0.06)"
          borderRadius="lg"
          p={4}
          overflow="hidden"
        >
          <Flex align="center" gap={2} mb={4}>
            <Clock size={12} color="#6b7280" />
            <Text fontSize="10px" letterSpacing="0.15em" color="app.textDim" textTransform="uppercase">
              Shot Log
            </Text>
            {shots.length > 0 && (
              <Box
                ml="auto"
                as="button"
                fontSize="10px"
                color="app.textDim"
                _hover={{ color: 'app.textMuted' }}
                style={{ cursor: 'pointer' }}
                onClick={() => setShots([])}
              >
                clear
              </Box>
            )}
          </Flex>

          {shots.length === 0 ? (
            <Flex direction="column" align="center" justify="center" h="200px" gap={2}>
              <Zap size={24} color="rgba(245,158,11,0.2)" />
              <Text fontSize="xs" color="app.textDim">
                No shots fired yet
              </Text>
            </Flex>
          ) : (
            <Flex direction="column" gap={2} overflowY="auto" maxH="520px">
              {shots.map(s => (
                <Box
                  key={s.id}
                  px={3} py={2}
                  borderRadius="md"
                  borderWidth="1px"
                  borderColor={s.ok ? 'rgba(34,197,94,0.15)' : 'rgba(239,68,68,0.15)'}
                  bg={s.ok ? 'rgba(34,197,94,0.04)' : 'rgba(239,68,68,0.04)'}
                >
                  <Flex align="center" gap={2} mb={1}>
                    {s.ok
                      ? <CheckCircle size={11} color="#22c55e" />
                      : <XCircle    size={11} color="#ef4444" />
                    }
                    <Text fontSize="10px" color={s.ok ? '#22c55e' : '#ef4444'} fontWeight="600">
                      {s.ok ? '204 OK' : 'ERROR'}
                    </Text>
                    <Text fontSize="10px" color="app.textDim" ml="auto">
                      {s.ts}
                    </Text>
                  </Flex>
                  <Text fontSize="11px" color="app.text" fontWeight="600" mb="1px" lineClamp={1}>
                    {s.title}
                  </Text>
                  <Flex align="center" gap={2}>
                    <Text fontSize="10px" color="app.textDim" lineClamp={1}>{s.source}</Text>
                    <Box
                      px={1} borderRadius="sm"
                      bg={`${PRIORITIES[s.priority]?.color ?? '#6b7280'}22`}
                      color={PRIORITIES[s.priority]?.color ?? '#6b7280'}
                      fontSize="9px" fontWeight="700" letterSpacing="0.05em"
                    >
                      {PRIORITIES[s.priority]?.label}
                    </Box>
                  </Flex>
                  {s.error && (
                    <Text fontSize="10px" color="#ef4444" mt={1} lineClamp={2}>
                      {s.error}
                    </Text>
                  )}
                </Box>
              ))}
            </Flex>
          )}
        </Box>
      </Grid>
    </Box>
  )
}

// ── helpers ────────────────────────────────────────────────────────────────

const inputStyle = {
  bg: 'rgba(0,0,0,0.4)',
  borderColor: 'rgba(255,255,255,0.08)',
  color: 'app.text',
  fontSize: 'xs',
  fontFamily: 'mono',
  _placeholder: { color: 'app.placeholder' },
  _focusVisible: { borderColor: 'rgba(245,158,11,0.5)', boxShadow: '0 0 0 1px rgba(245,158,11,0.3)' },
  _hover: { borderColor: 'rgba(255,255,255,0.15)' },
} as const

function Field({
  label,
  children,
  error,
  mb,
}: {
  label: string
  children: React.ReactNode
  error?: string
  mb?: number | string
}) {
  return (
    <Box mb={mb}>
      <Text fontSize="10px" letterSpacing="0.15em" color="app.textDim" mb={1}>
        {label}
      </Text>
      {children}
      {error && (
        <Text fontSize="10px" color="red.400" mt={1}>{error}</Text>
      )}
    </Box>
  )
}
