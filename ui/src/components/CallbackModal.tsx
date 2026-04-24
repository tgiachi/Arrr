import {
  Box,
  Button,
  Dialog,
  Flex,
  IconButton,
  Text,
  Textarea,
} from '@chakra-ui/react'
import { X } from 'lucide-react'
import { useState } from 'react'
import type { ArrrApi } from '../api'
import type { Plugin } from '../types'

interface Props {
  plugin: Plugin
  api: ArrrApi
  onClose: () => void
  onToast: (title: string, type: 'success' | 'error') => void
}

export function CallbackModal({ plugin, api, onClose, onToast }: Props) {
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)

  const handleSend = async () => {
    if (!body.trim()) return
    setSending(true)
    try {
      await api.sendCallback(plugin.id, body.trim())
      onToast(`Callback delivered to ${plugin.name}`, 'success')
      onClose()
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setSending(false)
    }
  }

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()} size="md">
      <Dialog.Backdrop bg="blackAlpha.700" backdropFilter="blur(4px)" />
      <Dialog.Positioner>
        <Dialog.Content
          bg="gray.900"
          borderWidth="1px"
          borderColor="whiteAlpha.100"
          borderRadius="xl"
          maxW="480px"
          w="full"
          mx={4}
        >
          <Dialog.Header px={5} pt={5} pb={3}>
            <Flex justify="space-between" align="center">
              <Box>
                <Dialog.Title fontFamily="heading" fontWeight="700" fontSize="lg" color="white">
                  Send callback
                </Dialog.Title>
                <Text fontFamily="mono" fontSize="xs" color="gray.500" mt={0.5}>
                  {plugin.id}
                </Text>
              </Box>
              <Dialog.CloseTrigger asChild>
                <IconButton
                  aria-label="Close"
                  size="sm"
                  variant="ghost"
                  color="gray.500"
                  _hover={{ color: 'white', bg: 'whiteAlpha.100' }}
                >
                  <X size={16} />
                </IconButton>
              </Dialog.CloseTrigger>
            </Flex>
          </Dialog.Header>

          <Dialog.Body px={5} pb={2}>
            <Textarea
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder="Payload (plain text — e.g. verification code)"
              fontFamily="mono"
              fontSize="sm"
              bg="blackAlpha.400"
              borderColor="whiteAlpha.100"
              color="gray.100"
              rows={4}
              resize="vertical"
              _focus={{ borderColor: 'amber.500', boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)' }}
            />
            <Text fontSize="xs" color="gray.600" mt={2}>
              The payload is delivered as-is to the plugin's callback handler.
            </Text>
          </Dialog.Body>

          <Dialog.Footer px={5} pb={5} pt={3}>
            <Flex gap={2} justify="flex-end" w="full">
              <Button size="sm" variant="ghost" color="gray.500" onClick={onClose} disabled={sending}>
                Cancel
              </Button>
              <Button
                size="sm"
                colorPalette="amber"
                onClick={handleSend}
                loading={sending}
                disabled={!body.trim()}
              >
                Send
              </Button>
            </Flex>
          </Dialog.Footer>
        </Dialog.Content>
      </Dialog.Positioner>
    </Dialog.Root>
  )
}
