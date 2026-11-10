import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'

/** Połączenie SignalR z LiveHub (zdarzenia agenta i statusy kroków). */
export function createLiveConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hubs/live')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}
