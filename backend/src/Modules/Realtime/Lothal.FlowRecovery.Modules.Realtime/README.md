# Realtime Module

This module currently provides in-memory pub/sub for `SessionNotification`.

Scope:
- `Subscribe`
- `SubscribeToSession`
- `SubscribeToFlow`
- disposal-based unsubscribe
- `Publish` and `TryPublish`, where `TryPublish` ignores `null`

What it does not do:
- persistence
- distributed broker integration
- transport layer implementation
- durable delivery
- client protocol handling

Future realtime capabilities may be added here, but only when they are implemented explicitly.
