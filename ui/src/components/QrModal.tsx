import { Box, Button, Dialog, Flex, IconButton, Spinner, Text } from '@chakra-ui/react'
import { X } from 'lucide-react'
import { useEffect, useState } from 'react'
import QRCode from 'react-qr-code'
import type { ArrrApi } from '../api'
import type { Plugin } from '../types'

interface Props {
  plugin: Plugin
  api: ArrrApi
  onClose: () => void
}

export function QrModal({ plugin, api, onClose }: Props) {
  const [code, setCode] = useState<string | null>(null)
  const [connected, setConnected] = useState(false)

  useEffect(() => {
    let active = true

    const poll = async () => {
      while (active) {
        const qr = await api.getQrCode(plugin.id).catch(() => null)
        if (!active) break
        if (qr === null && code !== null) {
          setConnected(true)
          setTimeout(onClose, 2000)
          return
        }
        if (qr !== null) setCode(qr)
        await new Promise(r => setTimeout(r, 3000))
      }
    }

    poll()
    return () => { active = false }
  }, [plugin.id]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()} size="sm">
      <Dialog.Backdrop bg="blackAlpha.700" backdropFilter="blur(4px)" />
      <Dialog.Positioner>
        <Dialog.Content
          bg="app.panelBg"
          borderWidth="1px"
          borderColor="app.panelBorder"
          borderRadius="xl"
          maxW="380px"
          w="full"
          mx={4}
        >
          <Dialog.Header px={5} pt={5} pb={3}>
            <Flex justify="space-between" align="center">
              <Box>
                <Dialog.Title fontFamily="heading" fontWeight="700" fontSize="lg" color="app.text">
                  Link {plugin.name}
                </Dialog.Title>
                <Text fontFamily="mono" fontSize="xs" color="app.textMuted" mt={0.5}>
                  WhatsApp → Settings → Linked Devices → Link a Device
                </Text>
              </Box>
              <Dialog.CloseTrigger asChild>
                <IconButton aria-label="Close" size="sm" variant="ghost" color="app.iconColor"
                  _hover={{ color: 'app.text', bg: 'app.cardBgHover' }}>
                  <X size={16} />
                </IconButton>
              </Dialog.CloseTrigger>
            </Flex>
          </Dialog.Header>

          <Dialog.Body px={5} pb={5}>
            <Flex direction="column" align="center" gap={4}>
              {connected ? (
                <Text color="green.400" fontFamily="mono" fontSize="sm">Connected!</Text>
              ) : code ? (
                <Box bg="white" p={3} borderRadius="lg">
                  <QRCode value={code} size={220} />
                </Box>
              ) : (
                <Flex direction="column" align="center" gap={2} py={8}>
                  <Spinner color="amber.500" />
                  <Text color="app.textMuted" fontSize="xs" fontFamily="mono">Waiting for QR code…</Text>
                </Flex>
              )}

              {!connected && (
                <Text fontSize="xs" color="app.textDim" textAlign="center">
                  QR code refreshes automatically. Keep this window open until connected.
                </Text>
              )}
            </Flex>
          </Dialog.Body>

          <Dialog.Footer px={5} pb={5} pt={0}>
            <Flex justify="flex-end" w="full">
              <Button size="sm" variant="ghost" color="app.textMuted" onClick={onClose}>
                Cancel
              </Button>
            </Flex>
          </Dialog.Footer>
        </Dialog.Content>
      </Dialog.Positioner>
    </Dialog.Root>
  )
}
