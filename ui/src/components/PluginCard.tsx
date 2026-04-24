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
import { RefreshCw, Settings2, Trash2, Webhook } from 'lucide-react'
import type { Plugin } from '../types'

interface Props {
  plugin: Plugin
  busy: boolean
  onToggle: (plugin: Plugin, enabled: boolean) => void
  onReload: (plugin: Plugin) => void
  onUninstall: (plugin: Plugin) => void
  onConfigure: (plugin: Plugin) => void
  onCallback: (plugin: Plugin) => void
}

export function PluginCard({ plugin, busy, onToggle, onReload, onUninstall, onConfigure, onCallback }: Props) {
  const statusColor = plugin.running ? '#4ade80' : plugin.enabled ? '#facc15' : '#4b5563'
  const statusLabel = plugin.running ? 'Running' : plugin.enabled ? 'Starting' : 'Disabled'

  return (
    <Card.Root
      bg="whiteAlpha.50"
      borderWidth="1px"
      borderColor="whiteAlpha.100"
      borderRadius="xl"
      overflow="hidden"
      transition="all 0.2s"
      _hover={{ borderColor: 'whiteAlpha.200', bg: 'whiteAlpha.100', transform: 'translateY(-2px)' }}
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
              boxShadow={plugin.running ? `0 0 8px ${statusColor}` : 'none'}
              style={
                plugin.running
                  ? { animation: 'pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite' }
                  : {}
              }
            />
            <Text
              fontWeight="700"
              fontSize="md"
              fontFamily="heading"
              color="white"
              overflow="hidden"
              textOverflow="ellipsis"
              whiteSpace="nowrap"
            >
              {plugin.name}
            </Text>
          </Flex>

          <HStack gap={1} flexShrink={0} ml={2}>
            <Tooltip.Root>
              <Tooltip.Trigger asChild>
                <IconButton
                  aria-label="Configure plugin"
                  size="xs"
                  variant="ghost"
                  color="gray.400"
                  _hover={{ color: 'amber.300', bg: 'whiteAlpha.100' }}
                  onClick={() => onConfigure(plugin)}
                  disabled={busy}
                >
                  <Settings2 size={13} />
                </IconButton>
              </Tooltip.Trigger>
              <Tooltip.Positioner>
                <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Configure</Tooltip.Content>
              </Tooltip.Positioner>
            </Tooltip.Root>

            {plugin.hasCallback && (
              <Tooltip.Root>
                <Tooltip.Trigger asChild>
                  <IconButton
                    aria-label="Send callback"
                    size="xs"
                    variant="ghost"
                    color="gray.400"
                    _hover={{ color: 'cyan.300', bg: 'whiteAlpha.100' }}
                    onClick={() => onCallback(plugin)}
                    disabled={busy}
                  >
                    <Webhook size={13} />
                  </IconButton>
                </Tooltip.Trigger>
                <Tooltip.Positioner>
                  <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Send callback</Tooltip.Content>
                </Tooltip.Positioner>
              </Tooltip.Root>
            )}

            <Tooltip.Root>
              <Tooltip.Trigger asChild>
                <IconButton
                  aria-label="Reload plugin"
                  size="xs"
                  variant="ghost"
                  color="gray.400"
                  _hover={{ color: 'amber.300', bg: 'whiteAlpha.100' }}
                  onClick={() => onReload(plugin)}
                  disabled={busy}
                >
                  <RefreshCw size={13} />
                </IconButton>
              </Tooltip.Trigger>
              <Tooltip.Positioner>
                <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Reload</Tooltip.Content>
              </Tooltip.Positioner>
            </Tooltip.Root>

            <Tooltip.Root>
              <Tooltip.Trigger asChild>
                <IconButton
                  aria-label="Uninstall plugin"
                  size="xs"
                  variant="ghost"
                  color="gray.600"
                  _hover={{ color: 'red.400', bg: 'whiteAlpha.100' }}
                  onClick={() => onUninstall(plugin)}
                  disabled={busy}
                >
                  <Trash2 size={13} />
                </IconButton>
              </Tooltip.Trigger>
              <Tooltip.Positioner>
                <Tooltip.Content bg="gray.800" color="white" fontSize="xs">Uninstall</Tooltip.Content>
              </Tooltip.Positioner>
            </Tooltip.Root>
          </HStack>
        </Flex>

        <Text
          fontFamily="mono"
          fontSize="xs"
          color="gray.500"
          mb={2}
          overflow="hidden"
          textOverflow="ellipsis"
          whiteSpace="nowrap"
        >
          {plugin.id} · v{plugin.version}
        </Text>

        {plugin.description && (
          <Text fontSize="sm" color="gray.400" mb={3} lineHeight="1.5">
            {plugin.description}
          </Text>
        )}

        {plugin.categories.length > 0 && (
          <HStack gap={1} flexWrap="wrap" mb={4}>
            {plugin.categories.map((cat) => (
              <Badge
                key={cat}
                size="sm"
                variant="subtle"
                colorPalette="gray"
                fontFamily="mono"
                fontSize="10px"
                px={2}
                borderRadius="md"
              >
                {cat}
              </Badge>
            ))}
          </HStack>
        )}

        <Flex justify="space-between" align="center">
          <Text fontSize="xs" color="gray.600">
            {statusLabel}
          </Text>
          <Switch.Root
            size="sm"
            colorPalette="green"
            checked={plugin.enabled}
            onCheckedChange={(e) => onToggle(plugin, e.checked)}
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
