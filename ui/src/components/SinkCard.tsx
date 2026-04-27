import {
  Badge,
  Box,
  Card,
  Flex,
  HStack,
  IconButton,
  Switch,
  Text,
  Tooltip,
} from '@chakra-ui/react'
import { RefreshCw, Settings2 } from 'lucide-react'
import type { Sink } from '../types'

interface Props {
  sink: Sink
  busy: boolean
  onToggle: (sink: Sink, enabled: boolean) => void
  onReload: (sink: Sink) => void
  onConfigure: (sink: Sink) => void
}

export function SinkCard({ sink, busy, onToggle, onReload, onConfigure }: Props) {
  const statusColor = sink.running ? '#4ade80' : sink.enabled ? '#facc15' : '#4b5563'
  const statusLabel = sink.running ? 'Running' : sink.enabled ? 'Starting' : 'Disabled'

  return (
    <Card.Root
      bg="app.cardBg"
      borderWidth="1px"
      borderColor="app.cardBorder"
      borderRadius="xl"
      overflow="hidden"
      transition="all 0.2s cubic-bezier(0.16,1,0.3,1)"
      boxShadow="inset 0 1px 0 rgba(255,255,255,0.07), 0 2px 12px rgba(0,0,0,0.2)"
      _hover={{
        borderColor: 'app.cardBorderHover',
        bg: 'app.cardBgHover',
        transform: 'translateY(-2px)',
        boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.1), 0 8px 28px rgba(0,0,0,0.38)',
      }}
      opacity={busy ? 0.6 : 1}
    >
      <Card.Body p={5}>
        <Flex justify="space-between" align="flex-start" mb={3}>
          <Flex align="center" gap={2} flex={1} minW={0}>
            <Box
              w="8px"
              h="8px"
              borderRadius="full"
              bg={statusColor}
              flexShrink={0}
              boxShadow={sink.running ? `0 0 8px ${statusColor}` : 'none'}
              style={
                sink.running
                  ? { animation: 'pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite' }
                  : {}
              }
            />
            {sink.icon && (
              <Text fontSize="sm" flexShrink={0} lineHeight={1}>
                {sink.icon}
              </Text>
            )}
            <Text
              fontWeight="700"
              fontSize="md"
              fontFamily="heading"
              color="app.text"
              overflow="hidden"
              textOverflow="ellipsis"
              whiteSpace="nowrap"
            >
              {sink.name}
            </Text>
          </Flex>

          <HStack gap={1} flexShrink={0} ml={2}>
            {sink.hasConfig && (
              <Tooltip.Root>
                <Tooltip.Trigger asChild>
                  <IconButton
                    aria-label="Configure sink"
                    size="xs"
                    variant="ghost"
                    color="app.iconColor"
                    _hover={{ color: 'amber.300', bg: 'app.cardBgHover' }}
                    onClick={() => onConfigure(sink)}
                    disabled={busy}
                  >
                    <Settings2 size={13} />
                  </IconButton>
                </Tooltip.Trigger>
                <Tooltip.Positioner>
                  <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Configure</Tooltip.Content>
                </Tooltip.Positioner>
              </Tooltip.Root>
            )}

            <Tooltip.Root>
              <Tooltip.Trigger asChild>
                <IconButton
                  aria-label="Reload sink"
                  size="xs"
                  variant="ghost"
                  color="app.iconColor"
                  _hover={{ color: 'amber.300', bg: 'app.cardBgHover' }}
                  onClick={() => onReload(sink)}
                  disabled={busy}
                >
                  <RefreshCw size={13} />
                </IconButton>
              </Tooltip.Trigger>
              <Tooltip.Positioner>
                <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Reload</Tooltip.Content>
              </Tooltip.Positioner>
            </Tooltip.Root>
          </HStack>
        </Flex>

        <Text
          fontFamily="mono"
          fontSize="xs"
          color="app.textMuted"
          mb={2}
          overflow="hidden"
          textOverflow="ellipsis"
          whiteSpace="nowrap"
        >
          {sink.id} · v{sink.version}
        </Text>

        {sink.description && (
          <Text fontSize="sm" color="app.textMuted" mb={3} lineHeight="1.5">
            {sink.description}
          </Text>
        )}

        {sink.isBuiltIn && (
          <HStack gap={1} mb={4}>
            <Badge
              size="sm"
              variant="subtle"
              colorPalette="amber"
              fontFamily="mono"
              fontSize="10px"
              px={2}
              borderRadius="md"
            >
              built-in
            </Badge>
          </HStack>
        )}

        <Flex justify="space-between" align="center">
          <Text fontSize="xs" color="app.textMuted">
            {statusLabel}
          </Text>
          <Switch.Root
            size="sm"
            colorPalette="green"
            checked={sink.enabled}
            onCheckedChange={(e) => onToggle(sink, e.checked)}
            disabled={busy}
          >
            <Switch.HiddenInput />
            <Switch.Control>
              <Switch.Thumb />
            </Switch.Control>
          </Switch.Root>
        </Flex>
      </Card.Body>
    </Card.Root>
  )
}
