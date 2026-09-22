# Native library decision

This project uses Jellyfin's supported **Channel** architecture rather than a synthetic filesystem library. The ChannelManager creates and maintains the Channel-backed Movie entities through public APIs; this plugin never writes `jellyfin.db` directly and never creates a fake file.

The remaining server validation is limited to confirming 10.11.11 behavior on the installed server and clients. See `CHANNEL_ARCHITECTURE.md` for the actual architecture and acceptance checklist.
