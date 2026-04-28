import { Box, Flex, Text, Input, Textarea, Button, Grid } from '@chakra-ui/react'
import { Zap, Clock, CheckCircle, XCircle } from 'lucide-react'
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

const PRIORITIES: { value: number; label: string; palette: string }[] = [
  { value: 0, label: 'Low',      palette: 'gray'   },
  { value: 1, label: 'Normal',   palette: 'blue'   },
  { value: 2, label: 'High',     palette: 'orange' },
  { value: 3, label: 'Critical', palette: 'red'    },
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
    try { return JSON.parse(extras) } catch { return undefined }
  }

  const fire = async () => {
    if (firing) return
    setFiring(true)
    const id = ++counter.current
    const ts = new Date().toLocaleTimeString()
    // Append counter suffix so repeated fires with identical content
    // are not suppressed by the server-side deduplication window.
    const sentBody = `${body} [#${id}]`
    try {
      await api.sendNotification({
        source, title, body: sentBody, priority,
        iconUrl: iconUrl || undefined,
        url:     url     || undefined,
        extras:  parsedExtras(),
      })
      setShots(prev => [{ id, ts, source, title, priority, ok: true }, ...prev].slice(0, 30))
      setFlashOk(true)
      setTimeout(() => setFlashOk(false), 700)
    } catch (e) {
      setShots(prev => [{ id, ts, source, title, priority, ok: false, error: String(e) }, ...prev].slice(0, 30))
    } finally {
      setFiring(false)
    }
  }

  const extrasError = extras.trim() ? (() => {
    try { JSON.parse(extras); return null } catch (e) { return String(e) }
  })() : null

  const palette = PRIORITIES[priority]?.palette ?? 'blue'

  return (
    <Box px={{ base: 4, md: 8 }} py={6}>

      {/* header */}
      <Flex align="center" gap={3} mb={6}>
        <Zap size={15} />
        <Text
          fontSize="xs" fontFamily="mono" fontWeight="700"
          letterSpacing="0.18em" textTransform="uppercase" color="app.text"
        >
          Notification Injector
        </Text>
        <Box flex={1} h="1px" bg="app.border" />
        <Box
          px={2} py="2px" borderRadius="sm" borderWidth="1px"
          borderColor="app.border" bg="app.cardBg"
        >
          <Text fontSize="9px" fontFamily="mono" fontWeight="700"
            letterSpacing="0.15em" color="app.textDim" textTransform="uppercase">
            debug
          </Text>
        </Box>
      </Flex>

      <Grid templateColumns={{ base: '1fr', lg: '1fr 360px' }} gap={5}>

        {/* ── composer ── */}
        <Box
          bg="app.cardBg"
          borderWidth="1px"
          borderColor={flashOk ? 'green.500' : 'app.cardBorder'}
          borderRadius="xl"
          p={5}
          style={{ transition: 'border-color 0.4s ease' }}
        >
          <Grid templateColumns="1fr 1fr" gap={4} mb={4}>
            <Field label="Source">
              <Input value={source} onChange={e => setSource(e.target.value)}
                placeholder="debug.manual" {...inp} />
            </Field>
            <Field label="Icon URL">
              <Input value={iconUrl} onChange={e => setIconUrl(e.target.value)}
                placeholder="https://…/icon.png" {...inp} />
            </Field>
          </Grid>

          <Field label="Title" mb={4}>
            <Input value={title} onChange={e => setTitle(e.target.value)}
              placeholder="Notification title" {...inp} />
          </Field>

          <Field label="Body" mb={4}>
            <Textarea value={body} onChange={e => setBody(e.target.value)}
              rows={3} placeholder="Notification body…" {...inp} resize="vertical" />
          </Field>

          <Field label="URL" mb={4}>
            <Input value={url} onChange={e => setUrl(e.target.value)}
              placeholder="https://…" {...inp} />
          </Field>

          {/* priority */}
          <Box mb={5}>
            <Text fontSize="11px" fontFamily="mono" color="app.textDim" fontWeight="600" mb={2}>
              Priority
            </Text>
            <Flex gap={2} flexWrap="wrap">
              {PRIORITIES.map((p) => (
                <Box
                  key={p.value}
                  as="button"
                  onClick={() => setPriority(p.value)}
                  px={3} py="5px"
                  borderRadius="md"
                  fontSize="xs"
                  fontFamily="mono"
                  fontWeight="600"
                  borderWidth="1px"
                  borderColor={priority === p.value ? `${p.palette}.500` : 'app.cardBorder'}
                  color={priority === p.value ? `${p.palette}.500` : 'app.textMuted'}
                  bg={priority === p.value ? `${p.palette}.500/10` : 'transparent'}
                  _hover={{ borderColor: `${p.palette}.500`, color: `${p.palette}.500` }}
                  style={{ transition: 'all 0.15s', cursor: 'pointer' }}
                >
                  {p.label}
                </Box>
              ))}
            </Flex>
          </Box>

          {/* extras */}
          <Field label="Extras (JSON)" error={extrasError ?? undefined} mb={6}>
            <Textarea
              value={extras}
              onChange={e => setExtras(e.target.value)}
              rows={3}
              placeholder={'{\n  "key": "value"\n}'}
              {...inp}
              borderColor={extrasError ? 'red.500' : inp.borderColor}
              resize="vertical"
            />
          </Field>

          <Button
            w="full" size="lg"
            colorPalette={palette}
            variant="outline"
            onClick={fire}
            loading={firing}
            disabled={!source || !title || !body || !!extrasError}
            fontFamily="mono" fontWeight="700" letterSpacing="0.12em"
            fontSize="sm" textTransform="uppercase"
          >
            <Zap size={13} />
            Fire
          </Button>
        </Box>

        {/* ── shot log ── */}
        <Box
          bg="app.cardBg"
          borderWidth="1px"
          borderColor="app.cardBorder"
          borderRadius="xl"
          p={4}
          overflow="hidden"
        >
          <Flex align="center" gap={2} mb={4}>
            <Clock size={12} />
            <Text fontSize="11px" fontFamily="mono" fontWeight="700"
              letterSpacing="0.1em" textTransform="uppercase" color="app.textDim">
              Shot Log
            </Text>
            {shots.length > 0 && (
              <Box
                ml="auto" as="button"
                fontSize="11px" fontFamily="mono" color="app.textDim"
                _hover={{ color: 'app.text' }}
                style={{ cursor: 'pointer' }}
                onClick={() => setShots([])}
              >
                clear
              </Box>
            )}
          </Flex>

          {shots.length === 0 ? (
            <Flex direction="column" align="center" justify="center" h="180px" gap={2} color="app.textDim">
              <Zap size={22} />
              <Text fontSize="xs" fontFamily="mono">No shots yet</Text>
            </Flex>
          ) : (
            <Flex direction="column" gap={2} overflowY="auto" maxH="540px">
              {shots.map(s => (
                <Box
                  key={s.id}
                  px={3} py={2} borderRadius="lg" borderWidth="1px"
                  borderColor={s.ok ? 'green.500/20' : 'red.500/20'}
                  bg={s.ok ? 'green.500/5' : 'red.500/5'}
                >
                  <Flex align="center" gap={2} mb="3px">
                    {s.ok
                      ? <CheckCircle size={11} color="var(--chakra-colors-green-500)" />
                      : <XCircle    size={11} color="var(--chakra-colors-red-500)"   />
                    }
                    <Text fontSize="10px" fontFamily="mono" fontWeight="700"
                      color={s.ok ? 'green.500' : 'red.500'}>
                      {s.ok ? '204 OK' : 'ERROR'}
                    </Text>
                    <Text fontSize="10px" fontFamily="mono" color="app.textDim" ml="auto">
                      {s.ts}
                    </Text>
                  </Flex>
                  <Text fontSize="12px" fontWeight="600" color="app.text" lineClamp={1}>
                    {s.title}
                  </Text>
                  <Flex align="center" gap={2} mt="2px">
                    <Text fontSize="10px" fontFamily="mono" color="app.textDim" lineClamp={1}>
                      {s.source}
                    </Text>
                    <Box
                      px="5px" py="1px" borderRadius="sm"
                      bg={`${PRIORITIES[s.priority]?.palette ?? 'gray'}.500/10`}
                      color={`${PRIORITIES[s.priority]?.palette ?? 'gray'}.500`}
                      fontSize="9px" fontFamily="mono" fontWeight="700"
                    >
                      {PRIORITIES[s.priority]?.label ?? s.priority}
                    </Box>
                  </Flex>
                  {s.error && (
                    <Text fontSize="10px" fontFamily="mono" color="red.500" mt={1} lineClamp={2}>
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

// ── shared input style using semantic tokens ────────────────────────────────

const inp = {
  bg: 'app.inputBg',
  borderColor: 'app.inputBorder',
  color: 'app.inputColor',
  fontSize: 'sm',
  fontFamily: 'mono',
  _placeholder: { color: 'app.placeholder' },
  _hover: { borderColor: 'app.cardBorderHover' },
  _focusVisible: { borderColor: 'orange.400', boxShadow: '0 0 0 1px var(--chakra-colors-orange-400)' },
} as const

function Field({
  label, children, error, mb,
}: {
  label: string; children: React.ReactNode; error?: string; mb?: number | string
}) {
  return (
    <Box mb={mb}>
      <Text fontSize="11px" fontFamily="mono" fontWeight="600" color="app.textDim" mb={1}>
        {label}
      </Text>
      {children}
      {error && <Text fontSize="10px" fontFamily="mono" color="red.500" mt={1}>{error}</Text>}
    </Box>
  )
}
