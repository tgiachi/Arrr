import {
  Box,
  Button,
  Flex,
  Input,
  Text,
} from '@chakra-ui/react'
import type { Settings } from '../types'

interface Props {
  settings: Settings
  onSave: (s: Settings) => void
  onClose: () => void
}

export function SettingsPanel({ settings, onSave, onClose }: Props) {
  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    onSave({
      apiKey: (fd.get('apiKey') as string).trim(),
      baseUrl: (fd.get('baseUrl') as string).trim(),
    })
    onClose()
  }

  return (
    <Box
      bg="gray.900"
      borderWidth="1px"
      borderColor="whiteAlpha.100"
      borderRadius="xl"
      p={5}
    >
      <form onSubmit={handleSubmit}>
        <Text
          fontSize="xs"
          fontWeight="600"
          color="gray.500"
          textTransform="uppercase"
          letterSpacing="wider"
          mb={4}
          fontFamily="mono"
        >
          Connection Settings
        </Text>

        <Flex direction="column" gap={3} mb={4}>
          <Box>
            <Text fontSize="xs" color="gray.500" mb={1}>API Key</Text>
            <Input
              name="apiKey"
              type="password"
              defaultValue={settings.apiKey}
              placeholder="your-api-key"
              size="sm"
              bg="whiteAlpha.50"
              borderColor="whiteAlpha.100"
              color="white"
              fontFamily="mono"
              _placeholder={{ color: 'gray.600' }}
              _focus={{ borderColor: 'amber.500', boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)' }}
            />
          </Box>
          <Box>
            <Text fontSize="xs" color="gray.500" mb={1}>Base URL (leave empty when served by Arrr)</Text>
            <Input
              name="baseUrl"
              defaultValue={settings.baseUrl}
              placeholder="http://localhost:5150"
              size="sm"
              bg="whiteAlpha.50"
              borderColor="whiteAlpha.100"
              color="white"
              fontFamily="mono"
              _placeholder={{ color: 'gray.600' }}
              _focus={{ borderColor: 'amber.500', boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)' }}
            />
          </Box>
        </Flex>

        <Flex gap={2} justify="flex-end">
          <Button size="sm" variant="ghost" color="gray.500" onClick={onClose} type="button">
            Cancel
          </Button>
          <Button size="sm" colorPalette="amber" type="submit">
            Save
          </Button>
        </Flex>
      </form>
    </Box>
  )
}
