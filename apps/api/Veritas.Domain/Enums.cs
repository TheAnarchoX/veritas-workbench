namespace Veritas.Domain;

public enum DossierStatus
{
    Draft,
    Active,
    Paused,
    Published,
    Archived
}

public enum SourceType
{
    Url,
    ManualUpload,
    Screenshot,
    Archive,
    SocialPost,
    Note
}

public enum CollectionStatus
{
    NotStarted,
    Allowed,
    BlockedByRobots,
    RequiresManualInput,
    Collected,
    Failed
}

public enum EvidenceType
{
    Image,
    Video,
    Audio,
    WebPage,
    Screenshot,
    Text,
    Archive,
    Other
}

public enum ProvenanceStatus
{
    OriginalProvided,
    PlatformOriginal,
    ScreenshotOnly,
    Recompressed,
    Unknown
}

public enum AnalysisStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum FindingCategory
{
    Metadata,
    Compression,
    ELA,
    VideoTemporal,
    Provenance,
    SourceMatch,
    AccountPattern,
    RobotsPolicy,
    ManualObservation,
    Other
}

public enum ConfidenceLevel
{
    None,
    Low,
    Medium,
    High
}

public enum FindingDirection
{
    SupportsAuthentic,
    SupportsAltered,
    SupportsAI,
    SupportsInauthenticPersona,
    Neutral,
    Inconclusive
}

public enum ClaimStatus
{
    Unassessed,
    Unsupported,
    Weak,
    Plausible,
    Likely,
    Confirmed,
    Disproved
}

public enum InvestigationTaskStatus
{
    Open,
    InProgress,
    Done,
    Blocked,
    WontDo
}

public enum Priority
{
    Low,
    Medium,
    High
}

public enum InvestigationTaskType
{
    ProvideOriginalMedia,
    ReverseImageSearch,
    ExtractVideoFrames,
    CheckRobots,
    ManualVerification,
    SourceChronology,
    AccountTimeline,
    Other
}

public enum CustodyEventType
{
    Uploaded,
    Downloaded,
    Hashed,
    Analyzed,
    Exported,
    Annotated,
    Replaced,
    Deleted
}
