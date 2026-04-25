import { useRef, useState } from 'react'
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

  const fileRef = useRef<HTMLInputElement>(null)
  const [isBackingUp, setIsBackingUp] = useState(false)
  const [isRestoring, setIsRestoring] = useState(false)
  const [backupError, setBackupError] = useState<string | null>(null)

  const apiBase = settings.baseUrl || ''
  const authHeaders = { 'X-Api-Key': settings.apiKey }

  const handleBackup = async () => {
    setIsBackingUp(true)
    setBackupError(null)
    try {
      const res = await fetch(`${apiBase}/api/config/backup`, { headers: authHeaders })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `arrr-backup-${new Date().toISOString().slice(0, 10)}.json`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setBackupError(e instanceof Error ? e.message : 'Backup failed')
    } finally {
      setIsBackingUp(false)
    }
  }

  const handleRestoreFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setIsRestoring(true)
    setBackupError(null)
    try {
      const text = await file.text()
      const body = JSON.parse(text)
      const res = await fetch(`${apiBase}/api/config/restore`, {
        method: 'POST',
        headers: { ...authHeaders, 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data = await res.json()
      alert(`Restored ${data.restored} config(s)`)
    } catch (e) {
      setBackupError(e instanceof Error ? e.message : 'Restore failed')
    } finally {
      setIsRestoring(false)
      e.target.value = ''
    }
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

      <Box mt={5} pt={4} borderTopWidth="1px" borderTopColor="whiteAlpha.100">
        <Text
          fontSize="xs"
          fontWeight="600"
          color="gray.500"
          textTransform="uppercase"
          letterSpacing="wider"
          mb={3}
          fontFamily="mono"
        >
          Config Backup
        </Text>

        {backupError && (
          <Text fontSize="xs" color="red.400" mb={2}>{backupError}</Text>
        )}

        <Flex gap={2}>
          <Button
            size="sm"
            variant="outline"
            colorPalette="amber"
            onClick={handleBackup}
            disabled={isBackingUp || isRestoring}
          >
            {isBackingUp ? 'Backing up…' : 'Backup'}
          </Button>
          <Button
            size="sm"
            variant="outline"
            colorPalette="gray"
            onClick={() => fileRef.current?.click()}
            disabled={isBackingUp || isRestoring}
          >
            {isRestoring ? 'Restoring…' : 'Restore'}
          </Button>
          <input
            type="file"
            accept=".json"
            ref={fileRef}
            style={{ display: 'none' }}
            onChange={handleRestoreFile}
          />
        </Flex>
      </Box>
    </Box>
  )
}
