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
}

export interface Settings {
  apiKey: string
  baseUrl: string
}
