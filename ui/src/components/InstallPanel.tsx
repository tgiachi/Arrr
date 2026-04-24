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
}

interface Props {
  onInstall: (packageId: string, version: string) => Promise<void>
}

const NUGET_SEARCH_URL =
  'https://azuresearch-usnc.nuget.org/query?q=tags:arrr-plugin&prerelease=false&take=25&semVerLevel=2.0.0'

function formatDownloads(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`
  return String(n)
}

export function InstallPanel({ onInstall }: Props) {
  const [packages, setPackages] = useState<NugetPackage[]>([])
  const [loadingPackages, setLoadingPackages] = useState(true)
  const [nugetError, setNugetError] = useState<string | null>(null)
  const [installingIds, setInstallingIds] = useState<Set<string>>(new Set())

  const [packageId, setPackageId] = useState('')
  const [version, setVersion] = useState('')
  const [installing, setInstalling] = useState(false)

  const fetchPackages = async () => {
    setLoadingPackages(true)
    setNugetError(null)
    try {
      const res = await fetch(NUGET_SEARCH_URL)
      if (!res.ok) throw new Error(`NuGet ${res.status}`)
      const data = await res.json()
      setPackages(data.data ?? [])
    } catch (e) {
      setNugetError((e as Error).message)
    } finally {
      setLoadingPackages(false)
    }
  }

  useEffect(() => { fetchPackages() }, [])

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
      bg="whiteAlpha.50"
      borderWidth="1px"
      borderColor="whiteAlpha.100"
      borderRadius="xl"
      p={4}
    >
      {/* NuGet packages */}
      <Flex justify="space-between" align="center" mb={3}>
        <Text
          fontSize="xs"
          fontWeight="600"
          color="gray.500"
          textTransform="uppercase"
          letterSpacing="wider"
          fontFamily="mono"
        >
          Available Plugins
        </Text>
        <Button
          size="xs"
          variant="ghost"
          color="gray.500"
          _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
          onClick={fetchPackages}
          loading={loadingPackages}
          gap={1}
        >
          <RefreshCcw size={12} />
          Refresh
        </Button>
      </Flex>

      {nugetError ? (
        <Text fontSize="xs" color="red.400" fontFamily="mono" mb={3}>{nugetError}</Text>
      ) : loadingPackages ? (
        <Flex justify="center" align="center" h="80px">
          <Spinner color="amber.500" size="sm" />
        </Flex>
      ) : packages.length === 0 ? (
        <Text fontSize="xs" color="gray.600" fontFamily="mono" mb={3}>No packages found</Text>
      ) : (
        <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap={3} mb={4}>
          {packages.map((pkg) => (
            <Box
              key={pkg.id}
              bg="blackAlpha.400"
              borderWidth="1px"
              borderColor="whiteAlpha.100"
              borderRadius="lg"
              p={3}
              _hover={{ borderColor: 'whiteAlpha.200' }}
              transition="all 0.15s"
            >
              <Flex justify="space-between" align="flex-start" gap={2}>
                <Box flex={1} minW={0}>
                  <Text
                    fontSize="sm"
                    fontWeight="600"
                    color="white"
                    fontFamily="mono"
                    overflow="hidden"
                    textOverflow="ellipsis"
                    whiteSpace="nowrap"
                  >
                    {pkg.id}
                  </Text>

                  <HStack gap={2} mt={0.5} mb={1}>
                    <Text fontSize="10px" color="gray.500" fontFamily="mono">
                      v{pkg.version}
                    </Text>
                    {pkg.totalDownloads > 0 && (
                      <HStack gap={0.5}>
                        <Download size={10} color="#6b7280" />
                        <Text fontSize="10px" color="gray.500" fontFamily="mono">
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
                      color="gray.500"
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

                <Button
                  size="xs"
                  colorPalette="amber"
                  variant="solid"
                  flexShrink={0}
                  loading={installingIds.has(pkg.id)}
                  onClick={() => handleInstallPackage(pkg)}
                  gap={1}
                >
                  <PackagePlus size={12} />
                  Install
                </Button>
              </Flex>
            </Box>
          ))}
        </SimpleGrid>
      )}

      {/* Manual install */}
      <Box borderTopWidth="1px" borderColor="whiteAlpha.50" pt={3}>
        <Text fontSize="xs" color="gray.600" fontFamily="mono" mb={2}>
          Manual install
        </Text>
        <Flex as="form" onSubmit={handleManualInstall} gap={2} align="center">
          <Input
            placeholder="Arrr.Plugin.CustomName"
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
