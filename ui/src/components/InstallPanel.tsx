import {
  Box,
  Button,
  Flex,
  Input,
  Text,
} from '@chakra-ui/react'
import { PackagePlus } from 'lucide-react'
import { useState } from 'react'

interface Props {
  onInstall: (packageId: string, version: string) => Promise<void>
}

export function InstallPanel({ onInstall }: Props) {
  const [packageId, setPackageId] = useState('')
  const [version, setVersion] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!packageId.trim()) return
    setLoading(true)
    try {
      await onInstall(packageId.trim(), version.trim())
      setPackageId('')
      setVersion('')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box
      as="form"
      onSubmit={handleSubmit}
      bg="whiteAlpha.50"
      borderWidth="1px"
      borderColor="whiteAlpha.100"
      borderRadius="xl"
      p={4}
    >
      <Text
        fontSize="xs"
        fontWeight="600"
        color="gray.500"
        textTransform="uppercase"
        letterSpacing="wider"
        mb={3}
        fontFamily="mono"
      >
        Install from NuGet
      </Text>
      <Flex gap={2} align="center">
        <Input
          placeholder="Arrr.Plugin.Rss"
          value={packageId}
          onChange={(e) => setPackageId(e.target.value)}
          size="sm"
          bg="whiteAlpha.50"
          borderColor="whiteAlpha.100"
          color="white"
          fontFamily="mono"
          _placeholder={{ color: 'gray.600' }}
          _focus={{ borderColor: 'amber.500', boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)' }}
          flex={2}
        />
        <Input
          placeholder="latest"
          value={version}
          onChange={(e) => setVersion(e.target.value)}
          size="sm"
          bg="whiteAlpha.50"
          borderColor="whiteAlpha.100"
          color="white"
          fontFamily="mono"
          _placeholder={{ color: 'gray.600' }}
          _focus={{ borderColor: 'amber.500', boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)' }}
          flex={1}
          maxW="120px"
        />
        <Button
          type="submit"
          size="sm"
          colorPalette="amber"
          variant="solid"
          loading={loading}
          disabled={!packageId.trim()}
          gap={1}
        >
          <PackagePlus size={14} />
          Install
        </Button>
      </Flex>
    </Box>
  )
}
