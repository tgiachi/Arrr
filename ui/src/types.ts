export interface Sink {
  id: string
  name: string
  version: string
  author: string
  description: string
  icon: string
  enabled: boolean
  running: boolean
  isBuiltIn: boolean
  hasConfig: boolean
}

export interface Plugin {
  id: string
  name: string
  version: string
  author: string
  description: string
  categories: string[]
  icon: string
  enabled: boolean
  running: boolean
  hasCallback: boolean
  hasQr: boolean
}

export interface ConfigFieldInfo {
  name: string
  description: string | null
  sensitive: boolean
}

export interface PluginConfigResponse {
  values: Record<string, unknown>
  schema: ConfigFieldInfo[]
}

export interface Settings {
  apiKey: string
  baseUrl: string
}

export interface DaemonConfig {
  apiKey: string
  isDebug: boolean
  port: number
  deduplicationEnabled: boolean
  deduplicationWindowSeconds: number
  historyEnabled: boolean
}

export interface HistoryEntry {
  id: string
  source: string
  title: string
  body: string
  timestamp: string
  iconUrl: string | null
  priority: number
}

export interface HistoryPage {
  items: HistoryEntry[]
  total: number
  page: number
  limit: number
}

export interface NotificationItem {
  id: string
  source: string
  title: string
  body: string
  timestamp: string
  iconUrl: string | null
  _fresh?: boolean
}
