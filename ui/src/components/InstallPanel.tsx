import {
  Badge,
  Box,
  Button,
  Flex,
  HStack,
  Input,
  SimpleGrid,
  Spinner,
  Text,
} from '@chakra-ui/react'
import { Download, PackagePlus, RefreshCcw } from 'lucide-react'
import { useEffect, useState } from 'react'

interface NugetPackage {
  id: string
  version: string
  description: string
  authors: string[]
  totalDownloads: number
  verified: boolean
  iconUrl?: string
}

interface Props {
  onInstall: (packageId: string, version: string) => Promise<void>
}

const NUGET_PLUGIN_URL =
  'https://azuresearch-usnc.nuget.org/query?q=tags:arrr-plugin&prerelease=false&take=50&semVerLevel=2.0.0'
const NUGET_SINK_URL =
  'https://azuresearch-usnc.nuget.org/query?q=tags:arrr-sink&prerelease=false&take=50&semVerLevel=2.0.0'

function formatDownloads(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`
  return String(n)
}

function filterPkgs(list: NugetPackage[], q: string): NugetPackage[] {
  if (!q.trim()) return list
  const lq = q.toLowerCase()
  return list.filter(
    (p) =>
      p.id.toLowerCase().includes(lq) ||
      p.description?.toLowerCase().includes(lq)
  )
}

function PackageCard({
  pkg,
  installing,
  onInstall,
}: {
  pkg: NugetPackage
  installing: boolean
  onInstall: (pkg: NugetPackage) => void
}) {
  return (
    <Box
      bg="app.cardBg"
      borderWidth="1px"
      borderColor="app.cardBorder"
      borderRadius="lg"
      p={3}
      _hover={{ borderColor: 'app.cardBorderHover' }}
      transition="all 0.15s"
    >
      <Flex justify="space-between" align="flex-start" gap={2}>
        <Flex gap={2} flex={1} minW={0} align="flex-start">
          {pkg.iconUrl && (
            <Box flexShrink={0} mt="1px">
              <img
                src={pkg.iconUrl}
                alt=""
                width={32}
                height={32}
                style={{ borderRadius: 6, objectFit: 'cover' }}
                onError={(e) => {
                  ;(e.target as HTMLImageElement).style.display = 'none'
                }}
              />
            </Box>
          )}

          <Box flex={1} minW={0}>
            <Text
              fontSize="sm"
              fontWeight="600"
              color="app.text"
              fontFamily="mono"
              overflow="hidden"
              textOverflow="ellipsis"
              whiteSpace="nowrap"
            >
              {pkg.id}
            </Text>

            <HStack gap={2} mt={0.5} mb={1}>
              <Text fontSize="10px" color="app.textMuted" fontFamily="mono">
                v{pkg.version}
              </Text>
              {pkg.totalDownloads > 0 && (
                <HStack gap={0.5}>
                  <Download size={10} color="#6b7280" />
                  <Text fontSize="10px" color="app.textMuted" fontFamily="mono">
                    {formatDownloads(pkg.totalDownloads)}
                  </Text>
                </HStack>
              )}
              {pkg.verified && (
                <Badge
                  size="xs"
                  colorPalette="green"
                  variant="subtle"
                  fontFamily="mono"
                  fontSize="9px"
                >
                  verified
                </Badge>
              )}
            </HStack>

            {pkg.description && (
              <Text
                fontSize="xs"
                color="app.textMuted"
                lineHeight="1.4"
                style={{
                  display: '-webkit-box',
                  WebkitLineClamp: 2,
                  WebkitBoxOrient: 'vertical',
                  overflow: 'hidden',
                }}
              >
                {pkg.description}
              </Text>
            )}
          </Box>
        </Flex>

        <Button
          size="xs"
          colorPalette="amber"
          variant="solid"
          flexShrink={0}
          loading={installing}
          onClick={() => onInstall(pkg)}
          gap={1}
        >
          <PackagePlus size={12} />
          Install
        </Button>
      </Flex>
    </Box>
  )
}

function PackageSection({
  label,
  packages,
  query,
  installingIds,
  onInstall,
}: {
  label: string
  packages: NugetPackage[]
  query: string
  installingIds: Set<string>
  onInstall: (pkg: NugetPackage) => void
}) {
  const filtered = filterPkgs(packages, query)

  return (
    <Box>
      <HStack mb={2} gap={2}>
        <Text
          fontSize="xs"
          fontWeight="600"
          color="app.textMuted"
          textTransform="uppercase"
          letterSpacing="wider"
          fontFamily="mono"
        >
          {label}
        </Text>
        <Badge size="xs" colorPalette="gray" variant="subtle" fontFamily="mono">
          {filtered.length}
        </Badge>
      </HStack>

      {filtered.length === 0 ? (
        <Text fontSize="xs" color="app.textMuted" fontFamily="mono" mb={1}>
          {query ? 'No matches' : 'None available'}
        </Text>
      ) : (
        <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap={3}>
          {filtered.map((pkg) => (
            <PackageCard
              key={pkg.id}
              pkg={pkg}
              installing={installingIds.has(pkg.id)}
              onInstall={onInstall}
            />
          ))}
        </SimpleGrid>
      )}
    </Box>
  )
}

export function InstallPanel({ onInstall }: Props) {
  const [plugins, setPlugins] = useState<NugetPackage[]>([])
  const [sinks, setSinks] = useState<NugetPackage[]>([])
  const [loading, setLoading] = useState(true)
  const [fetchError, setFetchError] = useState<string | null>(null)
  const [installingIds, setInstallingIds] = useState<Set<string>>(new Set())
  const [query, setQuery] = useState('')

  const [packageId, setPackageId] = useState('')
  const [version, setVersion] = useState('')
  const [installing, setInstalling] = useState(false)

  const fetchAll = async () => {
    setLoading(true)
    setFetchError(null)
    try {
      const [pr, sr] = await Promise.all([
        fetch(NUGET_PLUGIN_URL).then((r) => r.json()),
        fetch(NUGET_SINK_URL).then((r) => r.json()),
      ])
      setPlugins(pr.data ?? [])
      setSinks(sr.data ?? [])
    } catch (e) {
      setFetchError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchAll()
  }, [])

  const handleInstallPackage = async (pkg: NugetPackage) => {
    setInstallingIds((prev) => new Set(prev).add(pkg.id))
    try {
      await onInstall(pkg.id, pkg.version)
    } finally {
      setInstallingIds((prev) => {
        const next = new Set(prev)
        next.delete(pkg.id)
        return next
      })
    }
  }

  const handleManualInstall = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!packageId.trim()) return
    setInstalling(true)
    try {
      await onInstall(packageId.trim(), version.trim())
      setPackageId('')
      setVersion('')
    } finally {
      setInstalling(false)
    }
  }

  return (
    <Box
      bg="app.cardBg"
      borderWidth="1px"
      borderColor="app.cardBorder"
      borderRadius="xl"
      p={4}
    >
      {/* Header */}
      <Flex justify="space-between" align="center" mb={3}>
        <Text
          fontSize="xs"
          fontWeight="600"
          color="app.textMuted"
          textTransform="uppercase"
          letterSpacing="wider"
          fontFamily="mono"
        >
          NuGet Registry
        </Text>
        <Button
          size="xs"
          variant="ghost"
          color="app.textMuted"
          _hover={{ color: 'amber.300', bg: 'app.cardBgHover' }}
          onClick={fetchAll}
          loading={loading}
          gap={1}
        >
          <RefreshCcw size={12} />
          Refresh
        </Button>
      </Flex>

      {/* Search */}
      <Box mb={4}>
        <Input
          placeholder="Search plugins and sinks…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          size="sm"
          bg="app.inputBg"
          borderColor="app.inputBorder"
          color="app.inputColor"
          fontFamily="mono"
          _placeholder={{ color: 'app.placeholder' }}
          _focus={{
            borderColor: 'amber.500',
            boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)',
          }}
        />
      </Box>

      {fetchError ? (
        <Text fontSize="xs" color="red.400" fontFamily="mono" mb={3}>
          {fetchError}
        </Text>
      ) : loading ? (
        <Flex justify="center" align="center" h="80px">
          <Spinner color="amber.500" size="sm" />
        </Flex>
      ) : (
        <Flex direction="column" gap={5} mb={4}>
          <PackageSection
            label="Source Plugins"
            packages={plugins}
            query={query}
            installingIds={installingIds}
            onInstall={handleInstallPackage}
          />
          <PackageSection
            label="Output Sinks"
            packages={sinks}
            query={query}
            installingIds={installingIds}
            onInstall={handleInstallPackage}
          />
        </Flex>
      )}

      {/* Manual install */}
      <Box borderTopWidth="1px" borderColor="app.cardBorder" pt={3}>
        <Text fontSize="xs" color="app.textMuted" fontFamily="mono" mb={2}>
          Manual install
        </Text>
        <Flex as="form" onSubmit={handleManualInstall} gap={2} align="center">
          <Input
            placeholder="Arrr.Plugin.CustomName"
            value={packageId}
            onChange={(e) => setPackageId(e.target.value)}
            size="sm"
            bg="app.inputBg"
            borderColor="app.inputBorder"
            color="app.inputColor"
            fontFamily="mono"
            _placeholder={{ color: 'app.placeholder' }}
            _focus={{
              borderColor: 'amber.500',
              boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)',
            }}
            flex={2}
          />
          <Input
            placeholder="latest"
            value={version}
            onChange={(e) => setVersion(e.target.value)}
            size="sm"
            bg="app.inputBg"
            borderColor="app.inputBorder"
            color="app.inputColor"
            fontFamily="mono"
            _placeholder={{ color: 'app.placeholder' }}
            _focus={{
              borderColor: 'amber.500',
              boxShadow: '0 0 0 1px var(--chakra-colors-amber-500)',
            }}
            flex={1}
            maxW="120px"
          />
          <Button
            type="submit"
            size="sm"
            colorPalette="amber"
            variant="outline"
            loading={installing}
            disabled={!packageId.trim()}
            gap={1}
          >
            <PackagePlus size={14} />
            Install
          </Button>
        </Flex>
      </Box>
    </Box>
  )
}
