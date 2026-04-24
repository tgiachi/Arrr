import type { Plugin, PluginConfigResponse, Sink } from './types'

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
