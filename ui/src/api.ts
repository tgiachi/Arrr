import type { DaemonConfig, DndStatus, HistoryPage, Plugin, PluginConfigResponse, RoutingLogEntry, Sink } from './types'

export class ArrrApi {
  constructor(
    private readonly baseUrl: string,
    private readonly apiKey: string,
  ) {}

  private headers(): HeadersInit {
    return {
      'Content-Type': 'application/json',
      'X-Api-Key': this.apiKey,
    }
  }

  private async req(path: string, init?: RequestInit): Promise<Response> {
    const r = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers: { ...this.headers(), ...(init?.headers ?? {}) },
    })
    if (!r.ok) {
      const body = await r.text().catch(() => '')
      throw new Error(body || `${r.status} ${r.statusText}`)
    }
    return r
  }

  async getPlugins(): Promise<Plugin[]> {
    return (await this.req('/api/plugins/available')).json()
  }

  async enable(id: string) {
    await this.req(`/api/plugins/${encodeURIComponent(id)}/enable`, { method: 'POST' })
  }

  async disable(id: string) {
    await this.req(`/api/plugins/${encodeURIComponent(id)}/disable`, { method: 'POST' })
  }

  async reload(id: string) {
    await this.req(`/api/plugins/${encodeURIComponent(id)}/reload`, { method: 'POST' })
  }

  async reloadAll() {
    await this.req('/api/plugins/reload/all', { method: 'POST' })
  }

  async install(packageId: string, version?: string) {
    await this.req('/api/plugins/install', {
      method: 'POST',
      body: JSON.stringify({ packageId, version: version || null }),
    })
  }

  async uninstall(packageId: string) {
    await this.req(`/api/plugins/${encodeURIComponent(packageId)}/uninstall`, { method: 'POST' })
  }

  async update(packageId: string) {
    await this.req(`/api/plugins/${encodeURIComponent(packageId)}/update`, { method: 'POST' })
  }

  async getConfig(pluginId: string): Promise<PluginConfigResponse> {
    return (await this.req(`/api/plugins/${encodeURIComponent(pluginId)}/config`)).json()
  }

  async saveConfig(pluginId: string, config: Record<string, unknown>) {
    await this.req(`/api/plugins/${encodeURIComponent(pluginId)}/config`, {
      method: 'POST',
      body: JSON.stringify(config),
    })
  }

  async sendCallback(pluginId: string, body: string) {
    await this.req(`/api/plugins/${encodeURIComponent(pluginId)}/callback`, {
      method: 'POST',
      headers: { 'Content-Type': 'text/plain' },
      body,
    })
  }

  async getSinks(): Promise<Sink[]> {
    return (await this.req('/api/sinks')).json()
  }

  async enableSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/enable`, { method: 'POST' })
  }

  async disableSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/disable`, { method: 'POST' })
  }

  async reloadSink(id: string) {
    await this.req(`/api/sinks/${encodeURIComponent(id)}/reload`, { method: 'POST' })
  }

  async getSinkConfig(sinkId: string): Promise<PluginConfigResponse> {
    return (await this.req(`/api/sinks/${encodeURIComponent(sinkId)}/config`)).json()
  }

  async saveSinkConfig(sinkId: string, config: Record<string, unknown>) {
    await this.req(`/api/sinks/${encodeURIComponent(sinkId)}/config`, {
      method: 'POST',
      body: JSON.stringify(config),
    })
  }

  async getVersion(): Promise<{ version: string; runtimeVersion: string; os: string }> {
    const r = await fetch(`${this.baseUrl}/api/version`)
    if (!r.ok) throw new Error(`${r.status}`)
    return r.json()
  }

  async getHistory(page = 1, limit = 50, search?: string, source?: string): Promise<HistoryPage> {
    const params = new URLSearchParams({ page: String(page), limit: String(limit) })
    if (search) params.set('search', search)
    if (source) params.set('source', source)
    return (await this.req(`/api/history?${params}`)).json()
  }

  async clearHistory(): Promise<void> {
    await this.req('/api/history', { method: 'DELETE' })
  }

  async getDaemonConfig(): Promise<DaemonConfig> {
    return (await this.req('/api/config')).json()
  }

  async saveDaemonConfig(config: DaemonConfig): Promise<void> {
    await this.req('/api/config', { method: 'PUT', body: JSON.stringify(config) })
  }

  async getLogs(): Promise<string[]> {
    return (await this.req('/api/logs')).json()
  }

  async getRoutingLog(limit = 50): Promise<RoutingLogEntry[]> {
    return (await this.req(`/api/routing/log?limit=${limit}`)).json()
  }

  async getDnd(): Promise<DndStatus> {
    return (await this.req('/api/dnd')).json()
  }

  async setDnd(enabled: boolean): Promise<DndStatus> {
    return (await this.req('/api/dnd', { method: 'PUT', body: JSON.stringify({ enabled }) })).json()
  }

  async getQrCode(pluginId: string): Promise<string | null> {
    const r = await fetch(`${this.baseUrl}/api/plugins/${encodeURIComponent(pluginId)}/qr`, {
      headers: this.headers(),
    })
    if (r.status === 204) return null
    if (!r.ok) return null
    const data = await r.json()
    return data.code ?? null
  }
}
