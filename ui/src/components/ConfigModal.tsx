import {
  Box,
  Button,
  Dialog,
  Flex,
  IconButton,
  Spinner,
  Text,
  Textarea,
} from '@chakra-ui/react'
import { X } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { ArrrApi } from '../api'
import type { Plugin } from '../types'

interface Props {
  plugin: Plugin
  api: ArrrApi
  onClose: () => void
  onToast: (title: string, type: 'success' | 'error') => void
}

export function ConfigModal({ plugin, api, onClose, onToast }: Props) {
  const [json, setJson] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [parseError, setParseError] = useState<string | null>(null)

  useEffect(() => {
    api
      .getConfig(plugin.id)
      .then((cfg) => setJson(JSON.stringify(cfg, null, 2)))
      .catch((e: Error) => onToast(e.message, 'error'))
      .finally(() => setLoading(false))
  }, [plugin.id]) // eslint-disable-line react-hooks/exhaustive-deps

  const handleSave = async () => {
    let parsed: Record<string, unknown>
    try {
      parsed = JSON.parse(json)
      setParseError(null)
    } catch {
      setParseError('Invalid JSON')
      return
    }

    setSaving(true)
    try {
      await api.saveConfig(plugin.id, parsed)
      onToast(`${plugin.name} config saved`, 'success')
      onClose()
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()} size="lg">
      <Dialog.Backdrop bg="blackAlpha.700" backdropFilter="blur(4px)" />
      <Dialog.Positioner>
        <Dialog.Content
          bg="gray.900"
          borderWidth="1px"
          borderColor="whiteAlpha.100"
          borderRadius="xl"
          maxW="600px"
          w="full"
          mx={4}
        >
          <Dialog.Header px={5} pt={5} pb={3}>
            <Flex justify="space-between" align="center">
              <Box>
                <Dialog.Title
                  fontFamily="heading"
                  fontWeight="700"
                  fontSize="lg"
                  color="white"
                >
                  Configure {plugin.name}
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
            {loading ? (
              <Flex justify="center" py={8}>
                <Spinner color="amber.500" />
              </Flex>
            ) : (
              <Box>
                <Textarea
                  value={json}
                  onChange={(e) => {
                    setJson(e.target.value)
                    setParseError(null)
                  }}
                  fontFamily="mono"
                  fontSize="sm"
                  bg="blackAlpha.400"
                  borderColor={parseError ? 'red.500' : 'whiteAlpha.100'}
                  color="gray.100"
                  rows={14}
                  resize="vertical"
                  spellCheck={false}
                  _focus={{
                    borderColor: parseError ? 'red.500' : 'amber.500',
                    boxShadow: `0 0 0 1px var(--chakra-colors-${parseError ? 'red' : 'amber'}-500)`,
                  }}
                />
                {parseError && (
                  <Text color="red.400" fontSize="xs" mt={1} fontFamily="mono">
                    {parseError}
                  </Text>
                )}
                <Text fontSize="xs" color="gray.600" mt={2}>
                  Sensitive fields (passwords, tokens) are shown in plaintext and re-encrypted on save.
                </Text>
              </Box>
            )}
          </Dialog.Body>

          <Dialog.Footer px={5} pb={5} pt={3}>
            <Flex gap={2} justify="flex-end" w="full">
              <Button
                size="sm"
                variant="ghost"
                color="gray.500"
                onClick={onClose}
                disabled={saving}
              >
                Cancel
              </Button>
              <Button
                size="sm"
                colorPalette="amber"
                onClick={handleSave}
                loading={saving}
                disabled={loading}
              >
                Save
              </Button>
            </Flex>
          </Dialog.Footer>
        </Dialog.Content>
      </Dialog.Positioner>
    </Dialog.Root>
  )
}
