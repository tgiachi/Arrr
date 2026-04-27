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
  hasTest: boolean
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

export interface DigestScheduleEntry {
  label: string
  titleEmoji: string
  fireAt: string
  dayOffset: number
}

export interface DigestConfig {
  enabled: boolean
  schedule: DigestScheduleEntry[]
}

export interface ExtraCondition {
  key: string
  value: string
}

export interface RoutingRule {
  id?: string
  name: string
  enabled: boolean
  sourcePattern: string
  titleContains: string
  bodyContains: string
  minPriority: number
  block: boolean
  allowSinks: string[]
  extraConditions: ExtraCondition[]
  activeFrom: string
  activeTo: string
}

export interface RoutingConfig {
  enabled: boolean
  rules: RoutingRule[]
}

export interface DaemonConfig {
  apiKey: string
  isDebug: boolean
  port: number
  deduplicationEnabled: boolean
  deduplicationWindowSeconds: number
  historyEnabled: boolean
  digest: DigestConfig
  routing: RoutingConfig
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

export interface RoutingLogEntry {
  timestamp: string
  ruleName: string
  action: 'blocked' | 'restricted' | 'allowed'
  notificationSource: string
  notificationTitle: string
  targetSinks: string[]
}

export interface DndStatus {
  enabled: boolean
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
