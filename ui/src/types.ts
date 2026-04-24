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
