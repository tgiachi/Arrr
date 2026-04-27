using Arrr.Core.Data.Config;

namespace Arrr.Core.Data.Api;

public record DaemonConfigDto(
    string ApiKey,
    bool IsDebug,
    int Port,
    bool DeduplicationEnabled,
    int DeduplicationWindowSeconds,
    bool HistoryEnabled,
    DigestConfig Digest
);
