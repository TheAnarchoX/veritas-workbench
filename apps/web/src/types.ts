export type Project = {
  id: string
  name: string
  description?: string
  createdAt: string
  updatedAt: string
  dossierCount: number
}

export type Dossier = {
  id: string
  projectId: string
  title: string
  summary?: string
  status: string
  createdAt: string
  updatedAt: string
}

export type Source = {
  id: string
  dossierId: string
  type: string
  url?: string
  platform?: string
  title?: string
  authorHandle?: string
  observedAt?: string
  firstSeenAt?: string
  collectionStatus: string
  robotsDecision?: string
  notes?: string
}

export type AnalysisArtifact = {
  id: string
  analysisRunId: string
  filename: string
  contentType?: string
  artifactType?: string
  downloadUrl: string
}

export type AnalysisRun = {
  id: string
  evidenceItemId: string
  pipeline: string
  status: string
  startedAt?: string
  completedAt?: string
  toolVersion?: string
  summary?: string
  error?: string
  artifacts: AnalysisArtifact[]
}

export type EvidenceItem = {
  id: string
  dossierId: string
  sourceId?: string
  type: string
  title: string
  description?: string
  originalFilename?: string
  contentHashSha256?: string
  perceptualHash?: string
  mimeType?: string
  fileSizeBytes?: number
  width?: number
  height?: number
  durationSeconds?: number
  capturedAt?: string
  uploadedAt: string
  provenanceStatus: string
  fileUrl: string
  analysisRuns: AnalysisRun[]
}

export type Finding = {
  id: string
  dossierId: string
  evidenceItemId?: string
  analysisRunId?: string
  category: string
  claim: string
  confidence: string
  direction: string
  evidence: string
  limitations: string
  falsificationPath: string
  createdAt: string
}

export type Claim = {
  id: string
  dossierId: string
  text: string
  status: string
  confidence: string
  rationale?: string
  createdAt: string
  updatedAt: string
}

export type InvestigationTask = {
  id: string
  dossierId: string
  title: string
  description?: string
  status: string
  priority: string
  taskType: string
  createdAt: string
  completedAt?: string
}

export type TimelineEntry = {
  id: string
  dossierId: string
  time: string
  platform?: string
  url?: string
  source?: string
  evidenceHash?: string
  caption?: string
  firstKnownAppearance: boolean
  notes?: string
  confidence: string
}

export type DossierBundle = {
  dossier: Dossier
  sources: Source[]
  evidence: EvidenceItem[]
  findings: Finding[]
  claims: Claim[]
  tasks: InvestigationTask[]
  timeline: TimelineEntry[]
}
