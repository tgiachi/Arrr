import {
  Badge,
  Box,
  Button,
  Dialog,
  Flex,
  HStack,
  IconButton,
  Spinner,
  Text,
  Textarea,
} from '@chakra-ui/react'
import { ChevronDown, ChevronUp, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { ConfigFieldInfo, PluginConfigResponse } from '../types'

interface Props {
  id: string
  name: string
  onClose: () => void
  onToast: (title: string, type: 'success' | 'error') => void
  getConfig: () => Promise<PluginConfigResponse>
  saveConfig: (config: Record<string, unknown>) => Promise<void>
}

export function ConfigModal({ id, name, onClose, onToast, getConfig, saveConfig }: Props) {
  const [json, setJson] = useState('')
  const [schema, setSchema] = useState<ConfigFieldInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [parseError, setParseError] = useState<string | null>(null)
  const [schemaOpen, setSchemaOpen] = useState(false)

  useEffect(() => {
    getConfig()
      .then(({ values, schema }) => {
        setJson(JSON.stringify(values, null, 2))
        setSchema(schema)
      })
      .catch((e: Error) => onToast(e.message, 'error'))
      .finally(() => setLoading(false))
  }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

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
      await saveConfig(parsed)
      onToast(`${name} config saved`, 'success')
      onClose()
    } catch (e) {
      onToast((e as Error).message, 'error')
    } finally {
      setSaving(false)
    }
  }

  const describedFields = schema.filter((f) => f.description)

  return (
    <Dialog.Root open onOpenChange={(e) => !e.open && onClose()} size="lg">
      <Dialog.Backdrop bg="blackAlpha.700" backdropFilter="blur(4px)" />
      <Dialog.Positioner>
        <Dialog.Content
          bg="app.panelBg"
          borderWidth="1px"
          borderColor="app.panelBorder"
          borderRadius="xl"
          maxW="600px"
          w="full"
          mx={4}
        >
          <Dialog.Header px={5} pt={5} pb={3}>
            <Flex justify="space-between" align="center">
              <Box>
                <Dialog.Title fontFamily="heading" fontWeight="700" fontSize="lg" color="app.text">
                  Configure {name}
                </Dialog.Title>
                <Text fontFamily="mono" fontSize="xs" color="app.textMuted" mt={0.5}>
                  {id}
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

          <Dialog.Body px={5} pb={2}>
            {loading ? (
              <Flex justify="center" py={8}>
                <Spinner color="amber.500" />
              </Flex>
            ) : (
              <Box>
                <Textarea
                  value={json}
                  onChange={(e) => { setJson(e.target.value); setParseError(null) }}
                  fontFamily="mono"
                  fontSize="sm"
                  bg="app.inputBg"
                  borderColor={parseError ? 'red.500' : 'app.inputBorder'}
                  color="app.inputColor"
                  rows={14}
                  resize="vertical"
                  spellCheck={false}
                  _focus={{
                    borderColor: parseError ? 'red.500' : 'amber.500',
                    boxShadow: `0 0 0 1px var(--chakra-colors-${parseError ? 'red' : 'amber'}-500)`,
                  }}
                />
                {parseError && (
                  <Text color="red.400" fontSize="xs" mt={1} fontFamily="mono">{parseError}</Text>
                )}

                {describedFields.length > 0 && (
                  <Box mt={3}>
                    <HStack
                      as="button"
                      onClick={() => setSchemaOpen((o) => !o)}
                      gap={1}
                      color="app.textMuted"
                      _hover={{ color: 'app.text' }}
                      fontSize="xs"
                      fontFamily="mono"
                      cursor="pointer"
                    >
                      {schemaOpen ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                      <Text>Field descriptions</Text>
                    </HStack>

                    {schemaOpen && (
                      <Box
                        mt={2}
                        borderWidth="1px"
                        borderColor="app.cardBorder"
                        borderRadius="lg"
                        overflow="hidden"
                      >
                        {describedFields.map((f, i) => (
                          <Flex
                            key={f.name}
                            px={3}
                            py={2}
                            gap={3}
                            align="flex-start"
                            borderTopWidth={i > 0 ? '1px' : '0'}
                            borderColor="app.cardBorder"
                            bg={i % 2 === 0 ? 'app.rowStripe' : 'transparent'}
                          >
                            <HStack gap={1} flexShrink={0} pt="1px">
                              <Text fontFamily="mono" fontSize="xs" color="amber.400" whiteSpace="nowrap">
                                {f.name}
                              </Text>
                              {f.sensitive && (
                                <Badge size="xs" colorPalette="red" variant="subtle" fontFamily="mono">
                                  encrypted
                                </Badge>
                              )}
                            </HStack>
                            <Text fontSize="xs" color="app.textMuted" lineHeight="1.5">
                              {f.description}
                            </Text>
                          </Flex>
                        ))}
                      </Box>
                    )}
                  </Box>
                )}

                <Text fontSize="xs" color="app.textDim" mt={2}>
                  Sensitive fields are shown decrypted and re-encrypted on save.
                </Text>
              </Box>
            )}
          </Dialog.Body>

          <Dialog.Footer px={5} pb={5} pt={3}>
            <Flex gap={2} justify="flex-end" w="full">
              <Button size="sm" variant="ghost" color="app.textMuted" onClick={onClose} disabled={saving}>
                Cancel
              </Button>
              <Button size="sm" colorPalette="amber" onClick={handleSave} loading={saving} disabled={loading}>
                Save
              </Button>
            </Flex>
          </Dialog.Footer>
        </Dialog.Content>
      </Dialog.Positioner>
    </Dialog.Root>
  )
}
