import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'

import type { PermissionRequest, WorkItemDetails } from './types'

let connection: HubConnection | null = null

/** Jedno wspólne połączenie SignalR z LiveHub dla całej aplikacji. */
export function liveConnection(): HubConnection {
  connection ??= new HubConnectionBuilder()
    .withUrl('/hubs/live')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  return connection
}

// Nazwy zdarzeń = stałe w FlowHub.Api/Hubs/LiveHub.cs.
export const onWorkItemChanged = (handler: (item: WorkItemDetails) => void) =>
  liveConnection().on('workItemChanged', handler)

export const onStepOutput = (handler: (workItemId: string, stepRunId: string, text: string) => void) =>
  liveConnection().on('stepOutput', handler)

export const onPermissionRequested = (handler: (request: PermissionRequest) => void) =>
  liveConnection().on('permissionRequested', handler)

export const onPermissionResolved = (handler: (request: PermissionRequest) => void) =>
  liveConnection().on('permissionResolved', handler)
