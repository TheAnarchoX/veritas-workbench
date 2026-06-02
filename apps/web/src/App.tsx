import {
  AlertTriangle,
  Archive,
  ArrowRight,
  Bot,
  CheckCircle2,
  Clock,
  ClipboardList,
  Database,
  Download,
  Eye,
  FileArchive,
  FileDown,
  FileJson,
  FileText,
  Fingerprint,
  FolderKanban,
  Gauge,
  Image as ImageIcon,
  Link as LinkIcon,
  ListChecks,
  Loader2,
  Moon,
  Network,
  Plus,
  RefreshCw,
  Route as RouteIcon,
  Search,
  Settings,
  ShieldCheck,
  Sun,
  Trash2,
  Type,
  Upload,
  UserRound,
  Video,
  X,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import {
  BrowserRouter,
  Link,
  Navigate,
  Route,
  Routes,
  useNavigate,
  useParams,
} from 'react-router-dom'
import { apiUrl, deleteJson, getJson, getText, patchJson, postEmpty, postForm, postJson } from './api'
import type {
  AnalysisArtifact,
  AnalysisRun,
  Claim,
  Dossier,
  DossierBundle,
  DossierEntity,
  DossierEntityRelation,
  EvidenceItem,
  Finding,
  InvestigationTask,
  Project,
  Source,
  TextTriageResult,
  TimelineEntry,
} from './types'

type LoadState<T> = {
  data?: T
  error?: string
  loading: boolean
}

type Theme = 'light' | 'dark'

const confidenceLevels = ['None', 'Low', 'Medium', 'High']
const claimStatuses = ['Unassessed', 'Unsupported', 'Weak', 'Plausible', 'Likely', 'Confirmed', 'Disproved']
const evidenceStances = ['Supports', 'Contradicts', 'Mixed', 'Contextual']
const taskStatuses = ['Open', 'InProgress', 'Done', 'Blocked', 'WontDo']
const taskTypes = ['ProvideOriginalMedia', 'ReverseImageSearch', 'ExtractVideoFrames', 'CheckRobots', 'ManualVerification', 'SourceChronology', 'AccountTimeline', 'TextAuthenticityReview', 'Other']
const priorities = ['Low', 'Medium', 'High']
const entityKinds = ['SocialAccount', 'Person', 'Organization', 'Website', 'Location', 'Alias', 'Other']
const findingCategories = ['Metadata', 'Compression', 'ELA', 'VideoTemporal', 'Provenance', 'SourceMatch', 'AccountPattern', 'RobotsPolicy', 'ManualObservation', 'Other']
const findingDirections = ['SupportsAuthentic', 'SupportsAltered', 'SupportsAI', 'SupportsInauthenticPersona', 'Neutral', 'Inconclusive']
const provenanceStatuses = ['Unknown', 'OriginalProvided', 'PlatformOriginal', 'ScreenshotOnly', 'Recompressed']
const sourceStatuses = ['NotStarted', 'Allowed', 'BlockedByRobots', 'RequiresManualInput', 'Collected', 'Failed']
const evidenceTypes = ['Image', 'Video', 'Audio', 'WebPage', 'Screenshot', 'Text', 'Archive', 'Other']
const relationTypes = ['same account', 'alias of', 'operates', 'owns', 'posted by', 'amplifies', 'member of', 'located near', 'references', 'related']

function getInitialTheme(): Theme {
  if (typeof window === 'undefined') return 'dark'
  const stored = window.localStorage.getItem('veritas-theme')
  return stored === 'light' || stored === 'dark' ? stored : 'dark'
}

function toDateTimeLocal(value?: string) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

function fromDateTimeLocal(value: string) {
  return value ? new Date(value).toISOString() : undefined
}

function shortLabel(value?: string, fallback = 'Untitled') {
  if (!value) return fallback
  return value.length > 80 ? `${value.slice(0, 80)}...` : value
}

function stanceFromFindingDirection(direction: string) {
  if (direction === 'Neutral') return 'Contextual'
  if (direction === 'Inconclusive') return 'Mixed'
  return 'Supports'
}

function sourceAuthorLabel(source: Source, entities: DossierEntity[]) {
  const entity = source.authorEntityId ? entities.find((item) => item.id === source.authorEntityId) : undefined
  if (entity) return `${entity.name}${entity.handle ? ` (${entity.handle})` : ''}`
  return source.authorHandle || 'Unlinked author'
}

export default function App() {
  return (
    <BrowserRouter>
      <Shell>
        <Routes>
          <Route path="/" element={<Navigate to="/projects" replace />} />
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/projects/:projectId" element={<ProjectPage />} />
          <Route path="/dossiers/:dossierId" element={<DossierPage />} />
          <Route path="/dossiers/:dossierId/evidence/:evidenceItemId" element={<EvidenceDetailPage />} />
          <Route path="/dossiers/:dossierId/evidence/:evidenceItemId/artifacts/:artifactId" element={<ArtifactDetailPage />} />
          <Route path="/dossiers/:dossierId/report" element={<ReportPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </Shell>
    </BrowserRouter>
  )
}

function Shell({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(getInitialTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    window.localStorage.setItem('veritas-theme', theme)
  }, [theme])

  const nextTheme = theme === 'dark' ? 'light' : 'dark'

  return (
    <div className="app-shell min-h-screen bg-[#f6f6f3] text-zinc-900">
      <header className="topbar border-b border-zinc-300 bg-white">
        <div className="mx-auto flex w-full max-w-[1920px] flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between 2xl:px-6">
          <Link to="/projects" className="flex items-center gap-3">
            <span className="brand-mark grid h-9 w-9 place-items-center rounded-md bg-emerald-700 text-white">
              <ShieldCheck size={19} aria-hidden="true" />
            </span>
            <span>
              <span className="block text-base font-semibold">Veritas Workbench</span>
              <span className="block text-xs text-zinc-600">OSINT media provenance desk</span>
            </span>
          </Link>
          <nav className="flex flex-wrap items-center gap-2 text-sm">
            <NavLink to="/projects" icon={<FolderKanban size={16} />}>Projects</NavLink>
            <NavLink to="/settings" icon={<Settings size={16} />}>Settings</NavLink>
            <button
              className="icon-btn"
              type="button"
              onClick={() => setTheme(nextTheme)}
              aria-label={`Switch to ${nextTheme} mode`}
              title={`Switch to ${nextTheme} mode`}
            >
              {theme === 'dark' ? <Sun size={16} aria-hidden="true" /> : <Moon size={16} aria-hidden="true" />}
            </button>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-[1920px] px-4 py-4 2xl:px-6">{children}</main>
    </div>
  )
}

function NavLink({ to, icon, children }: { to: string; icon: ReactNode; children: ReactNode }) {
  return (
    <Link
      to={to}
      className="inline-flex h-9 items-center gap-2 rounded-md border border-zinc-300 bg-white px-3 text-zinc-800 hover:border-emerald-700"
    >
      {icon}
      {children}
    </Link>
  )
}

function ProjectsPage() {
  const [state, setState] = useState<LoadState<Project[]>>({ loading: true })
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')

  const load = async () => {
    setState({ loading: true })
    try {
      setState({ loading: false, data: await getJson<Project[]>('/projects') })
    } catch (error) {
      setState({ loading: false, error: String(error) })
    }
  }

  useEffect(() => {
    let cancelled = false
    getJson<Project[]>('/projects')
      .then((data) => {
        if (!cancelled) setState({ loading: false, data })
      })
      .catch((error) => {
        if (!cancelled) setState({ loading: false, error: String(error) })
      })
    return () => {
      cancelled = true
    }
  }, [])

  const create = async (event: FormEvent) => {
    event.preventDefault()
    if (!name.trim()) return
    await postJson<Project>('/projects', { name, description })
    setName('')
    setDescription('')
    await load()
  }

  return (
    <ProjectListView
      projects={state.data ?? []}
      loading={state.loading}
      error={state.error}
      name={name}
      description={description}
      onName={setName}
      onDescription={setDescription}
      onCreate={create}
    />
  )
}

export function ProjectListView({
  projects,
  loading,
  error,
  name,
  description,
  onName,
  onDescription,
  onCreate,
}: {
  projects: Project[]
  loading?: boolean
  error?: string
  name?: string
  description?: string
  onName?: (value: string) => void
  onDescription?: (value: string) => void
  onCreate?: (event: FormEvent) => void
}) {
  return (
    <div className="grid gap-4 lg:grid-cols-[360px_1fr]">
      <Panel title="Create Project" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={onCreate}>
          <Field label="Name">
            <input className="input" value={name ?? ''} onChange={(event) => onName?.(event.target.value)} placeholder="Investigation project" />
          </Field>
          <Field label="Description">
            <textarea className="input min-h-24" value={description ?? ''} onChange={(event) => onDescription?.(event.target.value)} placeholder="Scope, sources, and boundaries" />
          </Field>
          <button className="btn-primary" type="submit">
            <Plus size={16} aria-hidden="true" />
            Create
          </button>
        </form>
      </Panel>
      <Panel title="Projects" icon={<FolderKanban size={17} />}>
        {loading && <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading projects" />}
        {error && <InlineStatus icon={<AlertTriangle size={16} />} text={error} tone="warning" />}
        <div className="grid gap-2" aria-label="project list">
          {projects.map((project) => (
            <Link key={project.id} to={`/projects/${project.id}`} className="rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h2 className="text-base font-semibold">{project.name}</h2>
                  <p className="mt-1 text-sm text-zinc-600">{project.description || 'No description'}</p>
                </div>
                <Badge>{project.dossierCount} dossiers</Badge>
              </div>
            </Link>
          ))}
          {!loading && projects.length === 0 && <Empty text="No projects yet." />}
        </div>
      </Panel>
    </div>
  )
}

function ProjectPage() {
  const { projectId } = useParams()
  const [state, setState] = useState<LoadState<{ project: Project; dossiers: Dossier[] }>>({ loading: true })
  const [title, setTitle] = useState('')
  const [summary, setSummary] = useState('')

  const load = async () => {
    if (!projectId) return
    setState({ loading: true })
    try {
      setState({ loading: false, data: await getJson(`/projects/${projectId}`) })
    } catch (error) {
      setState({ loading: false, error: String(error) })
    }
  }

  useEffect(() => {
    if (!projectId) return
    let cancelled = false
    getJson<{ project: Project; dossiers: Dossier[] }>(`/projects/${projectId}`)
      .then((data) => {
        if (!cancelled) setState({ loading: false, data })
      })
      .catch((error) => {
        if (!cancelled) setState({ loading: false, error: String(error) })
      })
    return () => {
      cancelled = true
    }
  }, [projectId])

  const create = async (event: FormEvent) => {
    event.preventDefault()
    if (!title.trim() || !projectId) return
    await postJson(`/projects/${projectId}/dossiers`, { title, summary })
    setTitle('')
    setSummary('')
    await load()
  }

  if (state.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading project" />
  if (state.error || !state.data) return <InlineStatus icon={<AlertTriangle size={16} />} text={state.error || 'Project not found'} tone="warning" />

  return (
    <div>
      <ProjectOverview project={state.data.project} dossiers={state.data.dossiers} />
      <div className="grid lg:grid-cols-[360px_1fr]">
        <Panel title="New Dossier" icon={<Plus size={17} />}>
          <form className="space-y-3" onSubmit={create}>
            <Field label="Title">
              <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Example dossier" />
            </Field>
            <Field label="Summary">
              <textarea className="input min-h-24" value={summary} onChange={(event) => setSummary(event.target.value)} placeholder="Working theory, scope, limitations" />
            </Field>
            <button className="btn-primary" type="submit">
              <Plus size={16} aria-hidden="true" />
              Create dossier
            </button>
          </form>
        </Panel>
        <Panel title="Dossiers" icon={<FolderKanban size={17} />}>
          <div className="grid gap-2">
            {state.data.dossiers.map((dossier) => (
              <Link key={dossier.id} to={`/dossiers/${dossier.id}`} className="matrix-row rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="truncate text-base font-semibold">{dossier.title}</h2>
                    <p className="mt-1 text-sm text-zinc-600">{dossier.summary || 'No summary'}</p>
                    <p className="mt-2 text-xs text-zinc-500">Updated {new Date(dossier.updatedAt).toLocaleString()}</p>
                  </div>
                  <Badge tone={dossier.status === 'Active' ? 'green' : 'zinc'}>{dossier.status}</Badge>
                </div>
              </Link>
            ))}
            {state.data.dossiers.length === 0 && <Empty text="No dossiers in this project." />}
          </div>
        </Panel>
      </div>
    </div>
  )
}

function ProjectOverview({ project, dossiers }: { project: Project; dossiers: Dossier[] }) {
  const active = dossiers.filter((dossier) => dossier.status === 'Active').length
  const latest = [...dossiers].sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt))[0]
  const withSummary = dossiers.filter((dossier) => dossier.summary?.trim()).length

  return (
    <Panel title={project.name} icon={<FolderKanban size={17} />}>
      <div className="grid gap-3 lg:grid-cols-[1fr_auto]">
        <div>
          <p className="text-sm text-zinc-600">{project.description || 'No project description recorded.'}</p>
          <div className="mt-3 grid gap-2 sm:grid-cols-3">
            <StatusTile label="Dossiers" value={String(dossiers.length)} detail={`${active} active`} tone={active ? 'green' : 'zinc'} />
            <StatusTile label="Scoped" value={`${withSummary}/${dossiers.length}`} detail="with summaries" tone={withSummary === dossiers.length && dossiers.length > 0 ? 'green' : 'amber'} />
            <StatusTile label="Latest update" value={latest ? new Date(latest.updatedAt).toLocaleDateString() : '-'} detail={latest?.title || 'No dossier activity'} tone={latest ? 'green' : 'zinc'} />
          </div>
        </div>
        <div className="min-w-64 rounded-md border border-zinc-300 bg-white p-3">
          <div className="text-xs font-semibold uppercase text-zinc-500">Project posture</div>
          <div className="mt-2 text-sm text-zinc-700">
            {dossiers.length === 0 ? 'Create the first dossier to start collecting sources and evidence.' : `${active} active dossiers are available for source collection, evidence analysis, and reporting.`}
          </div>
        </div>
      </div>
    </Panel>
  )
}

function DossierPage() {
  const { dossierId } = useParams()
  const [bundle, setBundle] = useState<LoadState<DossierBundle>>({ loading: true })
  const [activeTab, setActiveTab] = useState('Overview')

  const load = async () => {
    if (!dossierId) return
    try {
      setBundle({ loading: false, data: await getJson<DossierBundle>(`/dossiers/${dossierId}`) })
    } catch (error) {
      setBundle((current) => ({ loading: false, data: current.data, error: String(error) }))
    }
  }

  useEffect(() => {
    if (!dossierId) return
    let cancelled = false
    getJson<DossierBundle>(`/dossiers/${dossierId}`)
      .then((data) => {
        if (!cancelled) setBundle({ loading: false, data })
      })
      .catch((error) => {
        if (!cancelled) setBundle({ loading: false, error: String(error) })
      })
    return () => {
      cancelled = true
    }
  }, [dossierId])

  if (bundle.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading dossier" />
  if (bundle.error || !bundle.data) return <InlineStatus icon={<AlertTriangle size={16} />} text={bundle.error || 'Dossier not found'} tone="warning" />

  return <DossierTabs bundle={bundle.data} activeTab={activeTab} onTab={setActiveTab} onRefresh={load} />
}

export function DossierTabs({
  bundle,
  activeTab,
  onTab,
  onRefresh,
}: {
  bundle: DossierBundle
  activeTab: string
  onTab?: (tab: string) => void
  onRefresh?: () => void
}) {
  return (
    <div className="workspace-surface">
      <section className="workspace-hero border border-zinc-300 bg-white p-4">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl font-semibold">{bundle.dossier.title}</h1>
              <Badge tone="green">{bundle.dossier.status}</Badge>
            </div>
            <p className="mt-1 max-w-3xl text-sm text-zinc-600">{bundle.dossier.summary || 'No summary yet.'}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link className="btn-secondary" to={`/dossiers/${bundle.dossier.id}/report`}>
              <FileText size={16} aria-hidden="true" />
              Report
            </Link>
            <button className="btn-secondary" onClick={onRefresh} type="button">
              <RefreshCw size={16} aria-hidden="true" />
              Refresh
            </button>
          </div>
        </div>
      </section>

      <div className="grid xl:grid-cols-[280px_1fr]">
        <WorkflowRail bundle={bundle} activeTab={activeTab} onTab={onTab} />
        <div className="min-w-0">
          <div className="workspace-content border border-zinc-300 bg-white p-0">
            {activeTab === 'Overview' && <OverviewTab bundle={bundle} onTab={onTab} />}
            {activeTab === 'Evidence' && <EvidenceTab dossierId={bundle.dossier.id} evidence={bundle.evidence} sources={bundle.sources} onRefresh={onRefresh} />}
            {activeTab === 'Entities' && <EntitiesTab dossierId={bundle.dossier.id} entities={bundle.entities} relations={bundle.entityRelations} onRefresh={onRefresh} />}
            {activeTab === 'Sources' && <SourcesTab dossierId={bundle.dossier.id} sources={bundle.sources} evidence={bundle.evidence} entities={bundle.entities} onRefresh={onRefresh} />}
            {activeTab === 'Findings' && <FindingsTab dossierId={bundle.dossier.id} findings={bundle.findings} evidence={bundle.evidence} onRefresh={onRefresh} />}
            {activeTab === 'Claims' && <ClaimsTab dossierId={bundle.dossier.id} claims={bundle.claims} evidence={bundle.evidence} onRefresh={onRefresh} />}
            {activeTab === 'Tasks' && <TasksTab dossierId={bundle.dossier.id} tasks={bundle.tasks} onRefresh={onRefresh} />}
            {activeTab === 'Timeline' && <TimelineTab dossierId={bundle.dossier.id} timeline={bundle.timeline} onRefresh={onRefresh} />}
            {activeTab === 'Report' && <InlineReport dossierId={bundle.dossier.id} />}
          </div>
        </div>
      </div>
    </div>
  )
}

function getRunStats(bundle: DossierBundle) {
  const runs = bundle.evidence.flatMap((item) => item.analysisRuns)
  return {
    runs,
    completed: runs.filter((run) => run.status === 'Completed').length,
    running: runs.filter((run) => run.status === 'Running' || run.status === 'Pending').length,
    failed: runs.filter((run) => run.status === 'Failed').length,
  }
}

function WorkflowRail({ bundle, activeTab, onTab }: { bundle: DossierBundle; activeTab: string; onTab?: (tab: string) => void }) {
  const openTasks = bundle.tasks.filter((task) => task.status !== 'Done').length
  const runStats = getRunStats(bundle)
  const phases = [
    {
      label: 'Frame',
      items: [
        { label: 'Overview', tab: 'Overview', icon: <RouteIcon size={16} />, metric: `${openTasks} open tasks`, ready: true },
        { label: 'Entities', tab: 'Entities', icon: <UserRound size={16} />, metric: `${bundle.entities.length} entities`, ready: bundle.entities.length > 0 },
        { label: 'Sources', tab: 'Sources', icon: <Search size={16} />, metric: `${bundle.sources.length} sources`, ready: bundle.sources.length > 0 },
      ],
    },
    {
      label: 'Collect and analyze',
      items: [
        { label: 'Evidence', tab: 'Evidence', icon: <Database size={16} />, metric: `${bundle.evidence.length} items`, ready: bundle.evidence.length > 0 },
        { label: 'Timeline', tab: 'Timeline', icon: <Clock size={16} />, metric: `${bundle.timeline.length} events`, ready: bundle.timeline.length > 0 },
      ],
    },
    {
      label: 'Assess',
      items: [
        { label: 'Findings', tab: 'Findings', icon: <Gauge size={16} />, metric: `${bundle.findings.length} findings`, ready: bundle.findings.length > 0 },
        { label: 'Claims', tab: 'Claims', icon: <FileText size={16} />, metric: `${bundle.claims.length} claims`, ready: bundle.claims.length > 0 },
      ],
    },
    {
      label: 'Resolve',
      items: [
        { label: 'Tasks', tab: 'Tasks', icon: <ListChecks size={16} />, metric: `${openTasks} open`, ready: openTasks === 0 && bundle.tasks.length > 0 },
        { label: 'Report', tab: 'Report', icon: <FileArchive size={16} />, metric: `${runStats.completed}/${runStats.runs.length} runs`, ready: bundle.claims.length > 0 && openTasks === 0 },
      ],
    },
  ]

  return (
    <aside className="workflow-rail border border-zinc-300 bg-white p-3">
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-zinc-800">
        <RouteIcon size={16} className="text-emerald-800" aria-hidden="true" />
        Dossier workflow
      </div>
      <div className="space-y-4">
        {phases.map((phase) => (
          <div key={phase.label} className="workflow-phase">
            <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">{phase.label}</div>
            <div className="space-y-1">
              {phase.items.map((step) => (
                <button
                  key={step.label}
                  type="button"
                  className={`workflow-step w-full border px-3 py-2 text-left ${activeTab === step.tab ? 'active border-emerald-700 text-white' : 'border-zinc-300 bg-white text-zinc-800'}`}
                  onClick={() => onTab?.(step.tab)}
                >
                  <span className="flex items-center justify-between gap-3">
                    <span className="flex min-w-0 items-center gap-2">
                      {step.icon}
                      <span className="truncate text-sm font-semibold">{step.label}</span>
                    </span>
                    <span className="shrink-0 font-mono text-[11px] opacity-75">{step.metric}</span>
                  </span>
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>
    </aside>
  )
}

function OverviewTab({ bundle, onTab }: { bundle: DossierBundle; onTab?: (tab: string) => void }) {
  const openTasks = bundle.tasks.filter((task) => task.status !== 'Done').length
  const runStats = getRunStats(bundle)
  const sourceLinkedEvidence = bundle.evidence.filter((item) => item.sourceId).length
  const unlinkedEvidence = bundle.evidence.filter((item) => !item.sourceId).length
  const pendingEvidence = bundle.evidence.filter((item) => item.analysisRuns.length === 0 || item.analysisRuns.some((run) => run.status === 'Pending' || run.status === 'Running')).length
  const failedRuns = runStats.runs.filter((run) => run.status === 'Failed').length
  const highPriorityTasks = bundle.tasks.filter((task) => task.status !== 'Done' && task.priority === 'High').length
  const unresolvedClaims = bundle.claims.filter((claim) => claim.status !== 'Confirmed' && claim.status !== 'Disproved').length
  const latestEvidence = bundle.evidence.slice(0, 6)
  const nextTasks = bundle.tasks.filter((task) => task.status !== 'Done').slice(0, 4)

  return (
    <div>
      <div className="grid lg:grid-cols-[1.1fr_0.9fr]">
        <Panel title="Dossier Status" icon={<RouteIcon size={17} />}>
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            <StatusTile label="Evidence ready" value={`${sourceLinkedEvidence}/${bundle.evidence.length}`} detail="linked to sources" tone={unlinkedEvidence ? 'amber' : 'green'} />
            <StatusTile label="Analysis" value={`${runStats.completed}/${runStats.runs.length}`} detail={failedRuns ? `${failedRuns} failed` : 'completed runs'} tone={failedRuns ? 'amber' : 'green'} />
            <StatusTile label="Claims" value={String(bundle.claims.length)} detail={`${unresolvedClaims} unresolved`} tone={unresolvedClaims ? 'amber' : 'green'} />
            <StatusTile label="Tasks" value={String(openTasks)} detail={`${highPriorityTasks} high priority`} tone={highPriorityTasks ? 'amber' : openTasks ? 'zinc' : 'green'} />
          </div>
          <div className="mt-4 grid gap-2 md:grid-cols-4">
            <FlowNode label="Entities" value={bundle.entities.length} icon={<UserRound size={16} />} onClick={() => onTab?.('Entities')} />
            <FlowNode label="Sources" value={bundle.sources.length} icon={<Search size={16} />} onClick={() => onTab?.('Sources')} />
            <FlowNode label="Findings" value={bundle.findings.length} icon={<Gauge size={16} />} onClick={() => onTab?.('Findings')} />
            <FlowNode label="Timeline" value={bundle.timeline.length} icon={<Clock size={16} />} onClick={() => onTab?.('Timeline')} />
          </div>
        </Panel>
        <Panel title="What Needs Attention" icon={<ClipboardList size={17} />}>
          <div className="grid gap-2">
            <AttentionRow label="Unlinked evidence" value={unlinkedEvidence} action="Link sources" onClick={() => onTab?.('Evidence')} />
            <AttentionRow label="Pending analysis" value={pendingEvidence} action="Open evidence" onClick={() => onTab?.('Evidence')} />
            <AttentionRow label="Open tasks" value={openTasks} action="Review tasks" onClick={() => onTab?.('Tasks')} />
            <AttentionRow label="Unresolved claims" value={unresolvedClaims} action="Assess claims" onClick={() => onTab?.('Claims')} />
          </div>
        </Panel>
      </div>

      <div className="grid lg:grid-cols-[1.15fr_0.85fr]">
        <Panel title="Evidence Readiness" icon={<Network size={17} />}>
          <div className="grid gap-2">
            {latestEvidence.map((item) => <EvidenceMatrixRow key={item.id} item={item} />)}
            {bundle.evidence.length === 0 && <Empty text="No evidence uploaded." />}
          </div>
        </Panel>
        <Panel title="Next Actions" icon={<ListChecks size={17} />}>
          <div className="space-y-2">
            {nextTasks.map((task) => <TaskRow key={task.id} task={task} />)}
            {nextTasks.length === 0 && <Empty text="No open tasks." />}
          </div>
        </Panel>
      </div>

      <Panel title="Recent Findings" icon={<Gauge size={17} />}>
        <div className="grid gap-2 xl:grid-cols-3">
          {bundle.findings.slice(0, 3).map((finding) => <FindingCard key={finding.id} finding={finding} />)}
          {bundle.findings.length === 0 && <Empty text="No findings yet." />}
        </div>
      </Panel>
    </div>
  )
}

function StatusTile({ label, value, detail, tone }: { label: string; value: string; detail: string; tone: 'green' | 'amber' | 'zinc' }) {
  return (
    <div className={`status-tile ${tone} rounded-md border p-3`}>
      <div className="text-xs font-semibold uppercase text-zinc-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold">{value}</div>
      <div className="mt-1 text-xs text-zinc-600">{detail}</div>
    </div>
  )
}

function AttentionRow({ label, value, action, onClick }: { label: string; value: number; action: string; onClick: () => void }) {
  return (
    <button className="matrix-row flex items-center justify-between gap-3 rounded-md border border-zinc-300 bg-white p-3 text-left hover:border-emerald-700" type="button" onClick={onClick}>
      <span>
        <span className="block text-sm font-semibold">{label}</span>
        <span className="text-xs text-zinc-600">{value === 0 ? 'No action needed' : action}</span>
      </span>
      <span className={`rounded border px-2 py-1 font-mono text-sm ${value === 0 ? 'border-emerald-300 text-emerald-900' : 'border-amber-300 text-amber-950'}`}>{value}</span>
    </button>
  )
}

function EvidenceMatrixRow({ item }: { item: EvidenceItem }) {
  const latestRun = item.analysisRuns[0]
  const runTone = latestRun?.status === 'Completed' ? 'green' : latestRun ? 'amber' : 'zinc'
  const runLabel = latestRun?.status ?? 'not analyzed'
  return (
    <Link key={item.id} to={`/dossiers/${item.dossierId}/evidence/${item.id}`} className="matrix-row grid gap-3 rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700 md:grid-cols-[1fr_auto]">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <Badge>{item.type}</Badge>
          <Badge tone={item.sourceId ? 'green' : 'amber'}>{item.sourceId ? 'linked source' : 'needs source'}</Badge>
          <Badge tone={runTone}>{runLabel}</Badge>
        </div>
        <div className="mt-2 truncate text-sm font-semibold">{item.title}</div>
        <div className="mt-1 text-xs text-zinc-600">{item.description || item.originalFilename || 'No collection note recorded'}</div>
      </div>
      <div className="flex items-center gap-2 text-xs text-zinc-500">
        <span>{item.analysisRuns.length} runs</span>
        <ArrowRight size={15} aria-hidden="true" />
      </div>
    </Link>
  )
}

function FlowNode({ label, value, suffix = '', icon, onClick }: { label: string; value: number; suffix?: string; icon: ReactNode; onClick?: () => void }) {
  return (
    <button className="flow-node rounded-md border border-zinc-300 bg-white p-3 text-left hover:border-emerald-700" type="button" onClick={onClick}>
      <span className="flex items-center justify-between gap-3 text-sm text-zinc-600">
        <span className="flex items-center gap-2">
          {icon}
          {label}
        </span>
        <ArrowRight size={14} aria-hidden="true" />
      </span>
      <span className="mt-3 block text-2xl font-semibold text-zinc-900">
        {value}<span className="text-sm text-zinc-500">{suffix}</span>
      </span>
    </button>
  )
}

function EvidenceTab({ dossierId, evidence, sources, onRefresh }: { dossierId: string; evidence: EvidenceItem[]; sources: Source[]; onRefresh?: () => void }) {
  return (
    <div className="grid lg:grid-cols-[390px_1fr]">
      <div>
        <UploadEvidenceForm dossierId={dossierId} sources={sources} onUploaded={onRefresh} />
        <TextEvidenceTriage dossierId={dossierId} sources={sources} onRefresh={onRefresh} />
      </div>
      <div>
        <Panel title="Evidence Inventory" icon={<Database size={17} />}>
          <div className="grid gap-2">
            {evidence.map((item) => <EvidenceRow key={item.id} item={item} />)}
            {evidence.length === 0 && <Empty text="No evidence uploaded." />}
          </div>
        </Panel>
      </div>
    </div>
  )
}

export function UploadEvidenceForm({ dossierId, sources = [], onUploaded }: { dossierId: string; sources?: Source[]; onUploaded?: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const [title, setTitle] = useState('')
  const [provenanceStatus, setProvenanceStatus] = useState('Unknown')
  const [sourceId, setSourceId] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    if (!file) {
      setError('Choose a file before uploading.')
      return
    }
    const form = new FormData()
    form.set('file', file)
    form.set('title', title || file.name)
    form.set('provenanceStatus', provenanceStatus)
    if (sourceId) form.set('sourceId', sourceId)
    setBusy(true)
    try {
      await postForm(`/dossiers/${dossierId}/evidence/upload`, form)
      setFile(null)
      setTitle('')
      setSourceId('')
      onUploaded?.()
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Panel title="Upload Evidence" icon={<Upload size={17} />}>
      <form className="space-y-3" onSubmit={submit}>
        <Field label="Title">
          <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Evidence title" />
        </Field>
        <Field label="Provenance">
          <select className="input" value={provenanceStatus} onChange={(event) => setProvenanceStatus(event.target.value)}>
            {provenanceStatuses.map((value) => <option key={value}>{value}</option>)}
          </select>
        </Field>
        <Field label="Linked source">
          <select className="input" value={sourceId} onChange={(event) => setSourceId(event.target.value)}>
            <option value="">No source link</option>
            {sources.map((source) => (
              <option key={source.id} value={source.id}>{source.platform || source.type}: {source.title || source.url}</option>
            ))}
          </select>
        </Field>
        <Field label="File">
          <input
            className="input file:mr-3 file:rounded-md file:border-0 file:bg-zinc-800 file:px-3 file:py-2 file:text-white"
            type="file"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        </Field>
        {error && <InlineStatus icon={<AlertTriangle size={16} />} text={error} tone="warning" />}
        <button className="btn-primary" disabled={busy} type="submit">
          {busy ? <Loader2 className="animate-spin" size={16} /> : <Upload size={16} />}
          Upload original or screenshot
        </button>
      </form>
    </Panel>
  )
}

function EvidenceRow({ item }: { item: EvidenceItem }) {
  const Icon = item.type === 'Video' ? Video : ImageIcon
  return (
    <Link to={`/dossiers/${item.dossierId}/evidence/${item.id}`} className="rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700">
      <div className="flex items-start gap-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-md bg-zinc-100 text-zinc-700">
          <Icon size={18} aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate text-sm font-semibold">{item.title}</h3>
            <Badge>{item.type}</Badge>
            <Badge tone={item.provenanceStatus === 'OriginalProvided' ? 'green' : 'amber'}>{item.provenanceStatus}</Badge>
          </div>
          <p className="mt-1 truncate font-mono text-xs text-zinc-600">{item.contentHashSha256 || 'hash pending'}</p>
          <p className="mt-1 text-xs text-zinc-500">{item.analysisRuns.length} analysis runs</p>
        </div>
      </div>
    </Link>
  )
}

function SourcesTab({ dossierId, sources, evidence, entities, onRefresh }: { dossierId: string; sources: Source[]; evidence: EvidenceItem[]; entities: DossierEntity[]; onRefresh?: () => void }) {
  const [url, setUrl] = useState('')
  const [title, setTitle] = useState('')
  const [authorHandle, setAuthorHandle] = useState('')
  const [authorEntityId, setAuthorEntityId] = useState('')
  const [observedAt, setObservedAt] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    if (!url.trim()) return
    setBusy(true)
    try {
      await postJson(`/dossiers/${dossierId}/sources/url`, {
        url,
        title: title || undefined,
        authorHandle: authorEntityId ? undefined : authorHandle || undefined,
        authorEntityId: authorEntityId || null,
        observedAt: fromDateTimeLocal(observedAt),
      })
      setUrl('')
      setTitle('')
      setAuthorHandle('')
      setAuthorEntityId('')
      setObservedAt('')
      onRefresh?.()
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }
  const remove = async (source: Source) => {
    if (!window.confirm(`Delete source ${source.title || source.url || source.id}? Evidence will stay in the dossier but lose this source link.`)) return
    await deleteJson(`/sources/${source.id}`)
    onRefresh?.()
  }
  const updateSource = async (source: Source, body: Partial<Source> & { clearAuthorEntity?: boolean }) => {
    await patchJson(`/sources/${source.id}`, body)
    onRefresh?.()
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Source" icon={<LinkIcon size={17} />}>
        <form className="space-y-3" onSubmit={submit}>
          <Field label="URL">
            <input className="input" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="https://x.com/example/status/123" />
          </Field>
          <Field label="Title">
            <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Post, article, archive capture, or source label" />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Author / handle">
              <select className="input" value={authorEntityId} onChange={(event) => setAuthorEntityId(event.target.value)}>
                <option value="">No linked entity</option>
                {entities.map((entity) => (
                  <option key={entity.id} value={entity.id}>{entity.name}{entity.handle ? ` (${entity.handle})` : ''}</option>
                ))}
              </select>
            </Field>
            <Field label="Observed">
              <input className="input" type="datetime-local" value={observedAt} onChange={(event) => setObservedAt(event.target.value)} />
            </Field>
          </div>
          <Field label="Fallback author / handle">
            <input className="input" value={authorHandle} onChange={(event) => setAuthorHandle(event.target.value)} placeholder="@account or author when no entity exists yet" disabled={!!authorEntityId} />
          </Field>
          <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
            X/Twitter and other terms-sensitive URLs default to manual original-media collection unless an official/API route is configured.
          </div>
          {error && <InlineStatus icon={<AlertTriangle size={16} />} text={error} tone="warning" />}
          <button className="btn-primary" disabled={busy} type="submit">
            {busy ? <Loader2 className="animate-spin" size={16} /> : <Search size={16} />}
            Check collection policy
          </button>
        </form>
      </Panel>
      <Panel title="Sources" icon={<Archive size={17} />}>
        <div className="space-y-2">
          {sources.map((source) => (
            <div key={source.id} className="rounded-md border border-zinc-300 bg-white p-3">
              <div className="flex flex-wrap items-center gap-2">
                <Badge>{source.platform || source.type}</Badge>
                <Badge tone={source.collectionStatus.includes('Blocked') || source.collectionStatus.includes('Requires') ? 'amber' : 'green'}>{source.collectionStatus}</Badge>
                <Badge tone={evidence.some((item) => item.sourceId === source.id) ? 'green' : 'zinc'}>
                  {evidence.filter((item) => item.sourceId === source.id).length} evidence
                </Badge>
              </div>
              <p className="mt-2 break-all text-sm font-medium">{source.title || source.url}</p>
              {(source.authorEntityId || source.authorHandle) && <p className="mt-1 text-sm text-zinc-600">{sourceAuthorLabel(source, entities)}</p>}
              <p className="mt-1 text-sm text-zinc-600">{source.notes}</p>
              <div className="mt-3 grid gap-2 sm:grid-cols-2">
                <Field label="Source title">
                  <input className="input" defaultValue={source.title || ''} onBlur={(event) => updateSource(source, { title: event.target.value })} placeholder="Title" />
                </Field>
                <Field label="Author entity">
                  <select className="input" defaultValue={source.authorEntityId || ''} onChange={(event) => updateSource(source, event.target.value ? { authorEntityId: event.target.value } : { clearAuthorEntity: true })}>
                    <option value="">No linked entity</option>
                    {entities.map((entity) => (
                      <option key={entity.id} value={entity.id}>{entity.name}{entity.handle ? ` (${entity.handle})` : ''}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Fallback author / handle">
                  <input className="input" defaultValue={source.authorHandle || ''} onBlur={(event) => updateSource(source, { authorHandle: event.target.value })} placeholder="Author or handle" />
                </Field>
                <Field label="Platform">
                  <input className="input" defaultValue={source.platform || ''} onBlur={(event) => updateSource(source, { platform: event.target.value })} placeholder="Platform" />
                </Field>
                <Field label="Collection status">
                  <select className="input" defaultValue={source.collectionStatus} onChange={(event) => updateSource(source, { collectionStatus: event.target.value })}>
                    {sourceStatuses.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Observed at">
                  <input className="input" type="datetime-local" defaultValue={toDateTimeLocal(source.observedAt)} onBlur={(event) => updateSource(source, { observedAt: fromDateTimeLocal(event.target.value) })} />
                </Field>
                <Field label="First seen at">
                  <input className="input" type="datetime-local" defaultValue={toDateTimeLocal(source.firstSeenAt)} onBlur={(event) => updateSource(source, { firstSeenAt: fromDateTimeLocal(event.target.value) })} />
                </Field>
              </div>
              <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_auto]">
                <Field label="Collection notes">
                  <input className="input" defaultValue={source.notes || ''} onBlur={(event) => updateSource(source, { notes: event.target.value })} placeholder="Collection notes" />
                </Field>
                <button className="btn-secondary" type="button" onClick={() => remove(source)}>
                  <Trash2 size={16} aria-hidden="true" />
                  Delete
                </button>
              </div>
            </div>
          ))}
          {sources.length === 0 && <Empty text="No sources recorded." />}
        </div>
      </Panel>
    </div>
  )
}

function EntitiesTab({ dossierId, entities, relations, onRefresh }: { dossierId: string; entities: DossierEntity[]; relations: DossierEntityRelation[]; onRefresh?: () => void }) {
  const [name, setName] = useState('')
  const [kind, setKind] = useState('SocialAccount')
  const [handle, setHandle] = useState('')
  const [platform, setPlatform] = useState('')
  const [url, setUrl] = useState('')
  const [notes, setNotes] = useState('')
  const [fromEntityId, setFromEntityId] = useState('')
  const [toEntityId, setToEntityId] = useState('')
  const [relationType, setRelationType] = useState('related')
  const [relationConfidence, setRelationConfidence] = useState('Low')
  const [evidenceBasis, setEvidenceBasis] = useState('')
  const [relationNotes, setRelationNotes] = useState('')

  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!name.trim()) return
    await postJson(`/dossiers/${dossierId}/entities`, { name, kind, handle, platform, url, notes, confidence: 'Low' })
    setName('')
    setHandle('')
    setPlatform('')
    setUrl('')
    setNotes('')
    onRefresh?.()
  }
  const remove = async (entity: DossierEntity) => {
    if (!window.confirm(`Delete entity ${entity.name}?`)) return
    await deleteJson(`/entities/${entity.id}`)
    onRefresh?.()
  }
  const updateEntity = async (entity: DossierEntity, body: Partial<DossierEntity>) => {
    await patchJson(`/entities/${entity.id}`, body)
    onRefresh?.()
  }
  const addRelation = async (event: FormEvent) => {
    event.preventDefault()
    if (!fromEntityId || !toEntityId || fromEntityId === toEntityId) return
    await postJson(`/dossiers/${dossierId}/entity-relations`, {
      fromEntityId,
      toEntityId,
      relationType,
      confidence: relationConfidence,
      evidenceBasis,
      notes: relationNotes,
    })
    setEvidenceBasis('')
    setRelationNotes('')
    onRefresh?.()
  }
  const updateRelation = async (relation: DossierEntityRelation, body: Partial<DossierEntityRelation>) => {
    await patchJson(`/entity-relations/${relation.id}`, body)
    onRefresh?.()
  }
  const removeRelation = async (relation: DossierEntityRelation) => {
    if (!window.confirm('Delete this entity relation?')) return
    await deleteJson(`/entity-relations/${relation.id}`)
    onRefresh?.()
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-4 xl:grid-cols-[380px_1fr]">
        <Panel title="Add Entity" icon={<UserRound size={17} />}>
          <form className="space-y-3" onSubmit={add}>
            <Field label="Kind">
              <select className="input" value={kind} onChange={(event) => setKind(event.target.value)}>
                {entityKinds.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Name">
              <input className="input" value={name} onChange={(event) => setName(event.target.value)} placeholder="Display name or account label" />
            </Field>
            <Field label="Handle">
              <input className="input" value={handle} onChange={(event) => setHandle(event.target.value)} placeholder="@account" />
            </Field>
            <Field label="Platform">
              <input className="input" value={platform} onChange={(event) => setPlatform(event.target.value)} placeholder="X, TikTok, Instagram, website" />
            </Field>
            <Field label="URL">
              <input className="input" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="Profile or reference URL" />
            </Field>
            <Field label="Notes">
              <textarea className="input min-h-20" value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="Why this entity matters, aliases, caveats" />
            </Field>
            <button className="btn-primary" type="submit">
              <Plus size={16} />
              Add entity
            </button>
          </form>
        </Panel>
        <Panel title="Entity Graph" icon={<Network size={17} />}>
          <EntityGraph entities={entities} relations={relations} />
        </Panel>
      </div>

      <div className="grid gap-4 xl:grid-cols-[420px_1fr]">
        <Panel title="Link Entities" icon={<LinkIcon size={17} />}>
          <form className="space-y-3" onSubmit={addRelation}>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="From">
                <select className="input" value={fromEntityId} onChange={(event) => setFromEntityId(event.target.value)}>
                  <option value="">Choose entity</option>
                  {entities.map((entity) => <option key={entity.id} value={entity.id}>{entity.name}</option>)}
                </select>
              </Field>
              <Field label="To">
                <select className="input" value={toEntityId} onChange={(event) => setToEntityId(event.target.value)}>
                  <option value="">Choose entity</option>
                  {entities.map((entity) => <option key={entity.id} value={entity.id}>{entity.name}</option>)}
                </select>
              </Field>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Relation">
                <input className="input" list="entity-relation-types" value={relationType} onChange={(event) => setRelationType(event.target.value)} placeholder="alias of, operates, amplifies" />
                <datalist id="entity-relation-types">
                  {relationTypes.map((value) => <option key={value} value={value} />)}
                </datalist>
              </Field>
              <Field label="Confidence">
                <select className="input" value={relationConfidence} onChange={(event) => setRelationConfidence(event.target.value)}>
                  {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
                </select>
              </Field>
            </div>
            <Field label="Evidence basis">
              <textarea className="input min-h-20" value={evidenceBasis} onChange={(event) => setEvidenceBasis(event.target.value)} placeholder="Why this relation exists, URLs, artifacts, observation basis" />
            </Field>
            <Field label="Notes">
              <textarea className="input min-h-16" value={relationNotes} onChange={(event) => setRelationNotes(event.target.value)} />
            </Field>
            <button className="btn-primary" type="submit" disabled={entities.length < 2 || !fromEntityId || !toEntityId || fromEntityId === toEntityId}>
              <Plus size={16} />
              Add relation
            </button>
          </form>
        </Panel>
        <Panel title="Relationships" icon={<RouteIcon size={17} />}>
          <div className="grid gap-2">
            {relations.map((relation) => (
              <EntityRelationRow
                key={relation.id}
                relation={relation}
                entities={entities}
                onUpdate={(body) => updateRelation(relation, body)}
                onDelete={() => removeRelation(relation)}
              />
            ))}
            {relations.length === 0 && <Empty text="No entity relations yet." />}
          </div>
        </Panel>
      </div>

      <Panel title="Related Entities" icon={<UserRound size={17} />}>
        <div className="grid gap-2 md:grid-cols-2 2xl:grid-cols-3">
          {entities.map((entity) => (
            <EntityCard key={entity.id} entity={entity} relationCount={relations.filter((relation) => relation.fromEntityId === entity.id || relation.toEntityId === entity.id).length} onUpdate={(body) => updateEntity(entity, body)} onDelete={() => remove(entity)} />
          ))}
          {entities.length === 0 && <Empty text="No entities yet." />}
        </div>
      </Panel>
    </div>
  )
}

function EntityGraph({ entities, relations }: { entities: DossierEntity[]; relations: DossierEntityRelation[] }) {
  if (entities.length === 0) {
    return <Empty text="Add entities to start mapping the graph." />
  }

  const width = 980
  const height = 420
  const centerX = width / 2
  const centerY = height / 2
  const radius = Math.min(330, 115 + entities.length * 18)
  const positions = new Map<string, { x: number; y: number }>()
  entities.forEach((entity, index) => {
    const angle = entities.length === 1 ? -Math.PI / 2 : (Math.PI * 2 * index) / entities.length - Math.PI / 2
    positions.set(entity.id, {
      x: centerX + Math.cos(angle) * radius,
      y: centerY + Math.sin(angle) * Math.min(radius, 150),
    })
  })

  return (
    <div className="entity-graph min-h-[420px] overflow-hidden rounded-md border border-zinc-300 bg-zinc-50">
      <svg viewBox={`0 0 ${width} ${height}`} className="h-[420px] w-full" role="img" aria-label="Entity relationship graph">
        <defs>
          <marker id="entity-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" className="fill-emerald-700" />
          </marker>
        </defs>
        {relations.map((relation) => {
          const from = positions.get(relation.fromEntityId)
          const to = positions.get(relation.toEntityId)
          if (!from || !to) return null
          const midX = (from.x + to.x) / 2
          const midY = (from.y + to.y) / 2
          return (
            <g key={relation.id}>
              <line x1={from.x} y1={from.y} x2={to.x} y2={to.y} className="stroke-emerald-700" strokeWidth="2" markerEnd="url(#entity-arrow)" opacity="0.75" />
              <rect x={midX - 58} y={midY - 13} width="116" height="26" rx="6" className="fill-white stroke-zinc-300" />
              <text x={midX} y={midY + 4} textAnchor="middle" className="fill-zinc-700 text-[12px]">
                {shortLabel(relation.relationType, 'related')}
              </text>
            </g>
          )
        })}
        {entities.map((entity) => {
          const point = positions.get(entity.id)!
          const relationCount = relations.filter((relation) => relation.fromEntityId === entity.id || relation.toEntityId === entity.id).length
          return (
            <g key={entity.id}>
              <circle cx={point.x} cy={point.y} r={42} className="fill-white stroke-emerald-700" strokeWidth="2" />
              <circle cx={point.x + 30} cy={point.y - 28} r={12} className={relationCount > 0 ? 'fill-emerald-700' : 'fill-zinc-400'} />
              <text x={point.x + 30} y={point.y - 24} textAnchor="middle" className="fill-white text-[10px] font-semibold">{relationCount}</text>
              <text x={point.x} y={point.y - 4} textAnchor="middle" className="fill-zinc-900 text-[13px] font-semibold">
                {shortLabel(entity.name, 'Entity')}
              </text>
              <text x={point.x} y={point.y + 14} textAnchor="middle" className="fill-zinc-600 text-[11px]">
                {entity.handle || entity.kind}
              </text>
            </g>
          )
        })}
      </svg>
    </div>
  )
}

function EntityRelationRow({ relation, entities, onUpdate, onDelete }: { relation: DossierEntityRelation; entities: DossierEntity[]; onUpdate: (body: Partial<DossierEntityRelation>) => void; onDelete: () => void }) {
  const from = entities.find((entity) => entity.id === relation.fromEntityId)
  const to = entities.find((entity) => entity.id === relation.toEntityId)
  return (
    <article className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge>{relation.relationType}</Badge>
        <ConfidenceBadge confidence={relation.confidence} />
      </div>
      <p className="mt-2 text-sm font-semibold">{from?.name ?? 'Unknown entity'} -&gt; {to?.name ?? 'Unknown entity'}</p>
      {relation.evidenceBasis && <p className="mt-1 text-sm text-zinc-600">{relation.evidenceBasis}</p>}
      {relation.notes && <p className="mt-1 text-sm text-zinc-500">{relation.notes}</p>}
      <div className="mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-4">
        <Field label="From entity">
          <select className="input" defaultValue={relation.fromEntityId} onChange={(event) => onUpdate({ fromEntityId: event.target.value })}>
            {entities.map((entity) => <option key={entity.id} value={entity.id}>{entity.name}</option>)}
          </select>
        </Field>
        <Field label="To entity">
          <select className="input" defaultValue={relation.toEntityId} onChange={(event) => onUpdate({ toEntityId: event.target.value })}>
            {entities.map((entity) => <option key={entity.id} value={entity.id}>{entity.name}</option>)}
          </select>
        </Field>
        <Field label="Relation type">
          <input className="input" list="entity-relation-types" defaultValue={relation.relationType} onBlur={(event) => onUpdate({ relationType: event.target.value })} />
        </Field>
        <Field label="Confidence">
          <select className="input" defaultValue={relation.confidence} onChange={(event) => onUpdate({ confidence: event.target.value })}>
            {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
          </select>
        </Field>
      </div>
      <div className="mt-2 grid gap-2 lg:grid-cols-[1fr_1fr_auto]">
        <Field label="Evidence basis">
          <textarea className="input min-h-16" defaultValue={relation.evidenceBasis || ''} onBlur={(event) => onUpdate({ evidenceBasis: event.target.value })} placeholder="Evidence basis" />
        </Field>
        <Field label="Notes">
          <textarea className="input min-h-16" defaultValue={relation.notes || ''} onBlur={(event) => onUpdate({ notes: event.target.value })} placeholder="Notes" />
        </Field>
        <button className="btn-secondary self-start" type="button" onClick={onDelete}>
          <Trash2 size={16} />
          Delete
        </button>
      </div>
    </article>
  )
}

function EntityCard({ entity, relationCount, onUpdate, onDelete }: { entity: DossierEntity; relationCount: number; onUpdate: (body: Partial<DossierEntity>) => void; onDelete: () => void }) {
  return (
    <article className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge>{entity.kind}</Badge>
        <ConfidenceBadge confidence={entity.confidence} />
        <Badge tone={relationCount > 0 ? 'green' : 'zinc'}>{relationCount} links</Badge>
      </div>
      <h3 className="mt-2 text-sm font-semibold">{entity.name}</h3>
      {(entity.handle || entity.platform) && <p className="mt-1 text-sm text-zinc-600">{entity.handle} {entity.platform}</p>}
      {entity.url && <p className="mt-1 break-all text-xs text-zinc-500">{entity.url}</p>}
      {entity.notes && <p className="mt-2 text-sm text-zinc-600">{entity.notes}</p>}
      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        <Field label="Entity kind">
          <select className="input" defaultValue={entity.kind} onChange={(event) => onUpdate({ kind: event.target.value })}>
            {entityKinds.map((value) => <option key={value}>{value}</option>)}
          </select>
        </Field>
        <Field label="Confidence">
          <select className="input" defaultValue={entity.confidence} onChange={(event) => onUpdate({ confidence: event.target.value })}>
            {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
          </select>
        </Field>
        <Field label="Name">
          <input className="input" defaultValue={entity.name} onBlur={(event) => onUpdate({ name: event.target.value })} placeholder="Name" />
        </Field>
        <Field label="Handle">
          <input className="input" defaultValue={entity.handle || ''} onBlur={(event) => onUpdate({ handle: event.target.value })} placeholder="Handle" />
        </Field>
        <Field label="Platform">
          <input className="input" defaultValue={entity.platform || ''} onBlur={(event) => onUpdate({ platform: event.target.value })} placeholder="Platform" />
        </Field>
        <Field label="URL">
          <input className="input" defaultValue={entity.url || ''} onBlur={(event) => onUpdate({ url: event.target.value })} placeholder="URL" />
        </Field>
      </div>
      <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_auto]">
        <Field label="Notes">
          <input className="input" defaultValue={entity.notes || ''} onBlur={(event) => onUpdate({ notes: event.target.value })} placeholder="Notes" />
        </Field>
        <button className="btn-secondary" type="button" onClick={onDelete}>
          <Trash2 size={16} />
          Delete
        </button>
      </div>
    </article>
  )
}

function TextEvidenceTriage({ dossierId, sources, onRefresh }: { dossierId: string; sources: Source[]; onRefresh?: () => void }) {
  const [title, setTitle] = useState('')
  const [text, setText] = useState('')
  const [sourceId, setSourceId] = useState('')
  const [result, setResult] = useState<TextTriageResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    if (!text.trim()) return
    setBusy(true)
    try {
      const response = await postJson<TextTriageResult>(`/dossiers/${dossierId}/text/triage`, { title: title || 'Text sample', text, sourceId: sourceId || null })
      setResult(response)
      setTitle('')
      setText('')
      setSourceId('')
      await Promise.resolve(onRefresh?.())
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Panel title="Text Evidence Triage" icon={<Type size={17} />}>
      <form className="space-y-3" onSubmit={submit}>
        <Field label="Title">
          <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Post text, caption, bio, reply" />
        </Field>
        <Field label="Linked source">
          <select className="input" value={sourceId} onChange={(event) => setSourceId(event.target.value)}>
            <option value="">No source link</option>
            {sources.map((source) => (
              <option key={source.id} value={source.id}>{source.platform || source.type}: {source.title || source.url}</option>
            ))}
          </select>
        </Field>
        <Field label="Text">
          <textarea className="input min-h-56" value={text} onChange={(event) => setText(event.target.value)} placeholder="Paste text for cautious style triage" />
        </Field>
        {error && <InlineStatus icon={<AlertTriangle size={16} />} text={error} tone="warning" />}
        <button className="btn-primary" disabled={busy} type="submit">
          {busy ? <Loader2 className="animate-spin" size={16} /> : <Bot size={16} />}
          Run triage
        </button>
      </form>
      {result && (
        <div className="mt-4 space-y-3 border-t border-zinc-300 pt-4">
          <div className="text-sm font-semibold text-zinc-800">Latest triage output</div>
          <div className="grid gap-3 xl:grid-cols-2">
            <FindingCard finding={result.finding} />
            <div className="rounded-md border border-zinc-300 bg-white p-3">
              <h3 className="text-sm font-semibold">Signals</h3>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-zinc-600">
                {(result.signals.length ? result.signals : ['No strong signals triggered.']).map((signal) => <li key={signal}>{signal}</li>)}
              </ul>
            </div>
          </div>
          <div className="rounded-md border border-zinc-300 bg-white p-3 text-sm">
            <div className="font-semibold">Created evidence</div>
            <Link className="mt-2 inline-flex text-emerald-800 hover:underline" to={`/dossiers/${dossierId}/evidence/${result.evidence.id}`}>
              Open {result.evidence.title}
            </Link>
          </div>
        </div>
      )}
    </Panel>
  )
}

function FindingsTab({ dossierId, findings, evidence, onRefresh }: { dossierId: string; findings: Finding[]; evidence: EvidenceItem[]; onRefresh?: () => void }) {
  const [category, setCategory] = useState('ManualObservation')
  const [claim, setClaim] = useState('')
  const [confidence, setConfidence] = useState('Low')
  const [direction, setDirection] = useState('Inconclusive')
  const [evidenceText, setEvidenceText] = useState('')
  const [limitations, setLimitations] = useState('')
  const [falsificationPath, setFalsificationPath] = useState('')
  const [evidenceItemId, setEvidenceItemId] = useState('')
  const [analysisRunId, setAnalysisRunId] = useState('')

  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!claim.trim()) return
    await postJson(`/dossiers/${dossierId}/findings`, {
      category,
      claim,
      confidence,
      direction,
      evidence: evidenceText,
      limitations,
      falsificationPath,
      evidenceItemId: evidenceItemId || null,
      analysisRunId: analysisRunId || null,
    })
    setClaim('')
    setEvidenceText('')
    setLimitations('')
    setFalsificationPath('')
    setEvidenceItemId('')
    setAnalysisRunId('')
    onRefresh?.()
  }
  const remove = async (finding: Finding) => {
    if (!window.confirm('Delete this finding?')) return
    await deleteJson(`/findings/${finding.id}`)
    onRefresh?.()
  }
  const update = async (finding: Finding, body: Partial<Finding>) => {
    await patchJson(`/findings/${finding.id}`, body)
    onRefresh?.()
  }
  const promote = async (finding: Finding) => {
    await postJson(`/dossiers/${dossierId}/claims`, {
      text: finding.claim,
      status: finding.direction === 'Inconclusive' ? 'Weak' : 'Plausible',
      confidence: finding.confidence,
      rationale: `${finding.evidence}\n\nLimitations: ${finding.limitations}`,
      evidenceItemId: finding.evidenceItemId || null,
      evidenceStance: stanceFromFindingDirection(finding.direction),
      evidenceNote: finding.evidence,
    })
    onRefresh?.()
  }
  const selectedEvidence = evidence.find((item) => item.id === evidenceItemId)

  return (
    <div className="grid gap-4 lg:grid-cols-[420px_1fr]">
      <Panel title="Add Finding" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Category">
              <select className="input" value={category} onChange={(event) => setCategory(event.target.value)}>
                {findingCategories.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Direction">
              <select className="input" value={direction} onChange={(event) => setDirection(event.target.value)}>
                {findingDirections.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Claim">
            <textarea className="input min-h-20" value={claim} onChange={(event) => setClaim(event.target.value)} placeholder="Observation that can support or challenge a claim" />
          </Field>
          <Field label="Evidence item">
            <select className="input" value={evidenceItemId} onChange={(event) => { setEvidenceItemId(event.target.value); setAnalysisRunId('') }}>
              <option value="">No evidence link</option>
              {evidence.map((item) => (
                <option key={item.id} value={item.id}>{item.title}</option>
              ))}
            </select>
          </Field>
          {selectedEvidence && selectedEvidence.analysisRuns.length > 0 && (
            <Field label="Analysis run">
              <select className="input" value={analysisRunId} onChange={(event) => setAnalysisRunId(event.target.value)}>
                <option value="">No analysis-run link</option>
                {selectedEvidence.analysisRuns.map((run) => (
                  <option key={run.id} value={run.id}>{run.pipeline} - {run.status}</option>
                ))}
              </select>
            </Field>
          )}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Confidence">
              <select className="input" value={confidence} onChange={(event) => setConfidence(event.target.value)}>
                {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Evidence basis">
            <textarea className="input min-h-20" value={evidenceText} onChange={(event) => setEvidenceText(event.target.value)} placeholder="Observed data, artifact, hash, or source basis" />
          </Field>
          <Field label="Limitations">
            <textarea className="input min-h-16" value={limitations} onChange={(event) => setLimitations(event.target.value)} placeholder="What this does not prove" />
          </Field>
          <Field label="Falsification path">
            <textarea className="input min-h-16" value={falsificationPath} onChange={(event) => setFalsificationPath(event.target.value)} placeholder="What evidence would change or disprove it" />
          </Field>
          <button className="btn-primary" type="submit">
            <Plus size={16} />
            Add finding
          </button>
        </form>
      </Panel>
      <Panel title="Findings" icon={<Gauge size={17} />}>
        <div className="grid gap-2">
          {findings.map((finding) => <FindingCard key={finding.id} finding={finding} evidence={evidence} onPromote={() => promote(finding)} onDelete={() => remove(finding)} onUpdate={(body) => update(finding, body)} />)}
          {findings.length === 0 && <Empty text="No findings yet." />}
        </div>
      </Panel>
    </div>
  )
}

export function FindingCard({ finding, evidence = [], onPromote, onDelete, onUpdate }: { finding: Finding; evidence?: EvidenceItem[]; onPromote?: () => void; onDelete?: () => void; onUpdate?: (body: Partial<Finding>) => void }) {
  const linkedEvidence = evidence.find((item) => item.id === finding.evidenceItemId)
  const analysisRuns = linkedEvidence?.analysisRuns ?? []

  return (
    <article className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="mb-2 flex flex-wrap gap-2">
        <Badge>{finding.category}</Badge>
        <ConfidenceBadge confidence={finding.confidence} />
        <Badge tone={finding.direction === 'Inconclusive' ? 'amber' : 'zinc'}>{finding.direction}</Badge>
        {linkedEvidence && <Badge tone="green">{shortLabel(linkedEvidence.title, 'Linked evidence')}</Badge>}
      </div>
      <h3 className="text-sm font-semibold">{finding.claim}</h3>
      <dl className="mt-2 grid gap-2 text-sm">
        <div>
          <dt className="font-semibold text-zinc-700">Evidence</dt>
          <dd className="text-zinc-600">{finding.evidence}</dd>
        </div>
        <div>
          <dt className="font-semibold text-zinc-700">Limitations</dt>
          <dd className="text-zinc-600">{finding.limitations}</dd>
        </div>
        <div>
          <dt className="font-semibold text-zinc-700">Falsification</dt>
          <dd className="text-zinc-600">{finding.falsificationPath}</dd>
        </div>
      </dl>
      {(onDelete || onUpdate) && (
        <div className="mt-3 space-y-2">
          {onUpdate && (
            <>
              <div className="grid gap-2 sm:grid-cols-3">
                <Field label="Category">
                  <select className="input" defaultValue={finding.category} onChange={(event) => onUpdate({ category: event.target.value })}>
                    {findingCategories.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Confidence">
                  <select className="input" defaultValue={finding.confidence} onChange={(event) => onUpdate({ confidence: event.target.value })}>
                    {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Direction">
                  <select className="input" defaultValue={finding.direction} onChange={(event) => onUpdate({ direction: event.target.value })}>
                    {findingDirections.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
              </div>
              {evidence.length > 0 && (
                <div className="grid gap-2 sm:grid-cols-2">
                  <Field label="Linked evidence">
                    <select className="input" defaultValue={finding.evidenceItemId || ''} onChange={(event) => event.target.value && onUpdate({ evidenceItemId: event.target.value })}>
                      <option value="">No evidence link</option>
                      {evidence.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
                    </select>
                  </Field>
                  {analysisRuns.length > 0 && (
                    <Field label="Linked analysis run">
                      <select className="input" defaultValue={finding.analysisRunId || ''} onChange={(event) => event.target.value && onUpdate({ analysisRunId: event.target.value })}>
                        <option value="">No analysis-run link</option>
                        {analysisRuns.map((run) => <option key={run.id} value={run.id}>{run.pipeline} - {run.status}</option>)}
                      </select>
                    </Field>
                  )}
                </div>
              )}
              <Field label="Finding claim">
                <textarea className="input min-h-16" defaultValue={finding.claim} onBlur={(event) => onUpdate({ claim: event.target.value })} />
              </Field>
              <Field label="Evidence basis">
                <textarea className="input min-h-16" defaultValue={finding.evidence} onBlur={(event) => onUpdate({ evidence: event.target.value })} />
              </Field>
              <Field label="Limitations">
                <textarea className="input min-h-16" defaultValue={finding.limitations} onBlur={(event) => onUpdate({ limitations: event.target.value })} />
              </Field>
              <Field label="Falsification path">
                <textarea className="input min-h-16" defaultValue={finding.falsificationPath} onBlur={(event) => onUpdate({ falsificationPath: event.target.value })} />
              </Field>
            </>
          )}
          {onPromote && (
            <button className="btn-secondary" type="button" onClick={onPromote}>
              <ArrowRight size={16} />
              Promote to claim
            </button>
          )}
          {onDelete && (
            <button className="btn-secondary" type="button" onClick={onDelete}>
              <Trash2 size={16} />
              Delete
            </button>
          )}
        </div>
      )}
    </article>
  )
}

function ClaimsTab({ dossierId, claims, evidence, onRefresh }: { dossierId: string; claims: Claim[]; evidence: EvidenceItem[]; onRefresh?: () => void }) {
  const [text, setText] = useState('')
  const [status, setStatus] = useState('Unassessed')
  const [confidence, setConfidence] = useState('None')
  const [rationale, setRationale] = useState('')
  const [evidenceItemId, setEvidenceItemId] = useState('')
  const [evidenceStance, setEvidenceStance] = useState('Supports')
  const [evidenceNote, setEvidenceNote] = useState('')
  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!text.trim()) return
    await postJson(`/dossiers/${dossierId}/claims`, {
      text,
      status,
      confidence,
      rationale,
      evidenceItemId: evidenceItemId || null,
      evidenceStance,
      evidenceNote,
    })
    setText('')
    setRationale('')
    setEvidenceItemId('')
    setEvidenceStance('Supports')
    setEvidenceNote('')
    onRefresh?.()
  }
  const update = async (claim: Claim, next: Partial<Claim>) => {
    await patchJson(`/claims/${claim.id}`, next)
    onRefresh?.()
  }
  const remove = async (claim: Claim) => {
    if (!window.confirm('Delete this claim?')) return
    await deleteJson(`/claims/${claim.id}`)
    onRefresh?.()
  }
  const linkEvidence = async (claim: Claim, evidenceId: string, stance: string, note: string) => {
    if (!evidenceId) return
    await postJson(`/claims/${claim.id}/evidence`, { evidenceItemId: evidenceId, stance, note })
    onRefresh?.()
  }
  const updateLink = async (linkId: string, body: { stance?: string; note?: string }) => {
    await patchJson(`/claim-evidence-links/${linkId}`, body)
    onRefresh?.()
  }
  const removeLink = async (linkId: string) => {
    await deleteJson(`/claim-evidence-links/${linkId}`)
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Claim" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="Claim">
            <textarea className="input min-h-24" value={text} onChange={(event) => setText(event.target.value)} placeholder="What evidence-supported claim is being assessed?" />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Status">
              <select className="input" value={status} onChange={(event) => setStatus(event.target.value)}>
                {claimStatuses.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Confidence">
              <select className="input" value={confidence} onChange={(event) => setConfidence(event.target.value)}>
                {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Rationale">
            <textarea className="input min-h-20" value={rationale} onChange={(event) => setRationale(event.target.value)} placeholder="Evidence basis and caveats" />
          </Field>
          <div className="rounded-md border border-zinc-300 bg-white p-3">
            <div className="mb-2 text-xs font-semibold uppercase text-zinc-500">Optional initial evidence</div>
            <div className="grid gap-3">
              <Field label="Evidence item">
                <select className="input" value={evidenceItemId} onChange={(event) => setEvidenceItemId(event.target.value)}>
                  <option value="">No evidence link</option>
                  {evidence.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
                </select>
              </Field>
              <Field label="Evidence stance">
                <select className="input" value={evidenceStance} onChange={(event) => setEvidenceStance(event.target.value)} disabled={!evidenceItemId}>
                  {evidenceStances.map((value) => <option key={value}>{value}</option>)}
                </select>
              </Field>
              <Field label="Evidence note">
                <textarea className="input min-h-16" value={evidenceNote} onChange={(event) => setEvidenceNote(event.target.value)} placeholder="Why this evidence supports, weakens, or contextualizes the claim" disabled={!evidenceItemId} />
              </Field>
            </div>
          </div>
          <button className="btn-primary" type="submit">
            <Plus size={16} />
            Add claim
          </button>
        </form>
      </Panel>
      <Panel title="Claims" icon={<FileText size={17} />}>
        <div className="space-y-2">
          {claims.map((claim) => (
            <div key={claim.id} className="rounded-md border border-zinc-300 bg-white p-3">
              <div className="mb-2 flex flex-wrap gap-2">
                <Badge tone="amber">{claim.status}</Badge>
                <ConfidenceBadge confidence={claim.confidence} />
              </div>
              <p className="text-sm font-semibold">{claim.text}</p>
              {claim.rationale && <p className="mt-1 text-sm text-zinc-600">{claim.rationale}</p>}
              <div className="mt-3 grid gap-2 sm:grid-cols-2">
                <Field label="Claim status">
                  <select className="input" defaultValue={claim.status} onChange={(event) => update(claim, { status: event.target.value })}>
                    {claimStatuses.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Confidence">
                  <select className="input" defaultValue={claim.confidence} onChange={(event) => update(claim, { confidence: event.target.value })}>
                    {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
              </div>
              <div className="mt-2 space-y-2">
                <Field label="Claim text">
                  <textarea className="input min-h-16" defaultValue={claim.text} onBlur={(event) => update(claim, { text: event.target.value })} placeholder="Claim text" />
                </Field>
                <Field label="Rationale and caveats">
                  <textarea className="input min-h-16" defaultValue={claim.rationale || ''} onBlur={(event) => update(claim, { rationale: event.target.value })} placeholder="Rationale and caveats" />
                </Field>
              </div>
              <ClaimEvidenceManager claim={claim} evidence={evidence} onAdd={(evidenceId, stance, note) => linkEvidence(claim, evidenceId, stance, note)} onUpdate={updateLink} onDelete={removeLink} />
              <div className="mt-2 flex flex-wrap gap-2">
                <button className="btn-secondary" type="button" onClick={() => remove(claim)}>
                  <Trash2 size={16} />
                  Delete
                </button>
              </div>
            </div>
          ))}
          {claims.length === 0 && <Empty text="No claims yet." />}
        </div>
      </Panel>
    </div>
  )
}

function ClaimEvidenceManager({
  claim,
  evidence,
  onAdd,
  onUpdate,
  onDelete,
}: {
  claim: Claim
  evidence: EvidenceItem[]
  onAdd: (evidenceItemId: string, stance: string, note: string) => void
  onUpdate: (linkId: string, body: { stance?: string; note?: string }) => void
  onDelete: (linkId: string) => void
}) {
  const [evidenceItemId, setEvidenceItemId] = useState('')
  const [stance, setStance] = useState('Supports')
  const [note, setNote] = useState('')
  const linkedIds = new Set((claim.evidenceLinks || []).map((link) => link.evidenceItemId))
  const availableEvidence = evidence.filter((item) => !linkedIds.has(item.id))

  const add = () => {
    if (!evidenceItemId) return
    onAdd(evidenceItemId, stance, note)
    setEvidenceItemId('')
    setStance('Supports')
    setNote('')
  }

  return (
    <div className="mt-3 rounded-md border border-zinc-300 bg-zinc-50 p-3">
      <div className="mb-2 flex items-center justify-between gap-2">
        <div className="text-xs font-semibold uppercase text-zinc-500">Claim evidence</div>
        <Badge tone={(claim.evidenceLinks || []).length > 0 ? 'green' : 'zinc'}>{(claim.evidenceLinks || []).length} linked</Badge>
      </div>
      <div className="grid gap-2">
        {(claim.evidenceLinks || []).map((link) => {
          const item = evidence.find((entry) => entry.id === link.evidenceItemId)
          return (
            <div key={link.id} className="rounded-md border border-zinc-300 bg-white p-2">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold">{item?.title || 'Evidence item'}</div>
                  <div className="text-xs text-zinc-500">{item?.type || 'Evidence'} {item?.contentHashSha256 ? `- ${item.contentHashSha256.slice(0, 12)}` : ''}</div>
                </div>
                <Badge tone={link.stance === 'Supports' ? 'green' : link.stance === 'Contradicts' ? 'amber' : 'zinc'}>{link.stance}</Badge>
              </div>
              <div className="mt-2 grid gap-2 sm:grid-cols-[180px_1fr_auto]">
                <Field label="Stance">
                  <select className="input" defaultValue={link.stance} onChange={(event) => onUpdate(link.id, { stance: event.target.value })}>
                    {evidenceStances.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Note">
                  <input className="input" defaultValue={link.note || ''} onBlur={(event) => onUpdate(link.id, { note: event.target.value })} placeholder="Evidence relevance" />
                </Field>
                <button className="icon-btn self-end" type="button" onClick={() => onDelete(link.id)} title="Unlink evidence">
                  <Trash2 size={16} />
                </button>
              </div>
            </div>
          )
        })}
        {(claim.evidenceLinks || []).length === 0 && <Empty text="No evidence linked to this claim." />}
      </div>
      <div className="mt-3 grid gap-2 sm:grid-cols-[1fr_160px]">
        <Field label="Add evidence">
          <select className="input" value={evidenceItemId} onChange={(event) => setEvidenceItemId(event.target.value)}>
            <option value="">Choose evidence</option>
            {availableEvidence.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
          </select>
        </Field>
        <Field label="Stance">
          <select className="input" value={stance} onChange={(event) => setStance(event.target.value)}>
            {evidenceStances.map((value) => <option key={value}>{value}</option>)}
          </select>
        </Field>
      </div>
      <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_auto]">
        <Field label="Evidence note">
          <input className="input" value={note} onChange={(event) => setNote(event.target.value)} placeholder="Why this evidence matters to the claim" />
        </Field>
        <button className="btn-secondary self-end" type="button" disabled={!evidenceItemId} onClick={add}>
          <Plus size={16} />
          Link evidence
        </button>
      </div>
    </div>
  )
}

function TasksTab({ dossierId, tasks, onRefresh }: { dossierId: string; tasks: InvestigationTask[]; onRefresh?: () => void }) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState('Medium')
  const [taskType, setTaskType] = useState('Other')
  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!title.trim()) return
    await postJson(`/dossiers/${dossierId}/tasks`, { title, description, taskType, priority })
    setTitle('')
    setDescription('')
    setPriority('Medium')
    setTaskType('Other')
    onRefresh?.()
  }
  const markDone = async (task: InvestigationTask) => {
    await patchJson(`/tasks/${task.id}`, { status: 'Done' })
    onRefresh?.()
  }
  const setStatus = async (task: InvestigationTask, status: string) => {
    await patchJson(`/tasks/${task.id}`, { status })
    onRefresh?.()
  }
  const updateTask = async (task: InvestigationTask, body: Partial<InvestigationTask>) => {
    await patchJson(`/tasks/${task.id}`, body)
    onRefresh?.()
  }
  const remove = async (task: InvestigationTask) => {
    if (!window.confirm(`Delete task ${task.title}?`)) return
    await deleteJson(`/tasks/${task.id}`)
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Task" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="Task">
            <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Manual verification step" />
          </Field>
          <Field label="Description">
            <textarea className="input min-h-20" value={description} onChange={(event) => setDescription(event.target.value)} placeholder="Why it matters and what output is expected" />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Priority">
              <select className="input" value={priority} onChange={(event) => setPriority(event.target.value)}>
                {priorities.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Type">
              <select className="input" value={taskType} onChange={(event) => setTaskType(event.target.value)}>
                {taskTypes.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
          </div>
          <button className="btn-primary" type="submit">
            <Plus size={16} />
            Add task
          </button>
        </form>
      </Panel>
      <Panel title="Tasks" icon={<ClipboardList size={17} />}>
        <div className="space-y-2">
          {tasks.map((task) => <TaskRow key={task.id} task={task} onDone={() => markDone(task)} onStatus={(status) => setStatus(task, status)} onUpdate={(body) => updateTask(task, body)} onDelete={() => remove(task)} />)}
          {tasks.length === 0 && <Empty text="No tasks yet." />}
        </div>
      </Panel>
    </div>
  )
}

function TaskRow({ task, onDone, onStatus, onUpdate, onDelete }: { task: InvestigationTask; onDone?: () => void; onStatus?: (status: string) => void; onUpdate?: (body: Partial<InvestigationTask>) => void; onDelete?: () => void }) {
  return (
    <div className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap gap-2">
            <Badge tone={task.priority === 'High' ? 'amber' : 'zinc'}>{task.priority}</Badge>
            <Badge>{task.taskType}</Badge>
            <Badge tone={task.status === 'Done' ? 'green' : 'zinc'}>{task.status}</Badge>
          </div>
          <h3 className="mt-2 text-sm font-semibold">{task.title}</h3>
          {task.description && <p className="mt-1 text-sm text-zinc-600">{task.description}</p>}
        </div>
        {onDone && task.status !== 'Done' && (
          <button className="icon-btn" type="button" onClick={onDone} title="Mark done">
            <CheckCircle2 size={16} />
          </button>
        )}
      </div>
      {(onStatus || onUpdate || onDelete) && (
        <div className="mt-3 space-y-2">
          {onStatus && (
            <Field label="Task status">
              <select className="input max-w-44" defaultValue={task.status} onChange={(event) => onStatus(event.target.value)}>
                {taskStatuses.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
          )}
          {onUpdate && (
            <>
              <div className="grid gap-2 sm:grid-cols-2">
                <Field label="Task title">
                  <input className="input" defaultValue={task.title} onBlur={(event) => onUpdate({ title: event.target.value })} placeholder="Task title" />
                </Field>
                <Field label="Priority">
                  <select className="input" defaultValue={task.priority} onChange={(event) => onUpdate({ priority: event.target.value })}>
                    {priorities.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
                <Field label="Task type">
                  <select className="input" defaultValue={task.taskType} onChange={(event) => onUpdate({ taskType: event.target.value })}>
                    {taskTypes.map((value) => <option key={value}>{value}</option>)}
                  </select>
                </Field>
              </div>
              <Field label="Description">
                <textarea className="input min-h-16" defaultValue={task.description || ''} onBlur={(event) => onUpdate({ description: event.target.value })} placeholder="Description" />
              </Field>
            </>
          )}
          {onDelete && (
            <div className="flex flex-wrap gap-2">
              <button className="btn-secondary" type="button" onClick={onDelete}>
                <Trash2 size={16} />
                Delete
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function TimelineTab({ dossierId, timeline, onRefresh }: { dossierId: string; timeline: TimelineEntry[]; onRefresh?: () => void }) {
  const [time, setTime] = useState('')
  const [platform, setPlatform] = useState('')
  const [url, setUrl] = useState('')
  const [source, setSource] = useState('')
  const [caption, setCaption] = useState('')
  const [notes, setNotes] = useState('')
  const [confidence, setConfidence] = useState('Low')
  const [firstKnownAppearance, setFirstKnownAppearance] = useState(false)
  const add = async (event: FormEvent) => {
    event.preventDefault()
    await postJson(`/dossiers/${dossierId}/timeline`, {
      time: fromDateTimeLocal(time),
      platform,
      url,
      source,
      caption,
      notes,
      confidence,
      firstKnownAppearance,
    })
    setTime('')
    setPlatform('')
    setUrl('')
    setSource('')
    setCaption('')
    setNotes('')
    setConfidence('Low')
    setFirstKnownAppearance(false)
    onRefresh?.()
  }
  const remove = async (entry: TimelineEntry) => {
    if (!window.confirm('Delete this timeline entry?')) return
    await deleteJson(`/timeline/${entry.id}`)
    onRefresh?.()
  }
  const update = async (entry: TimelineEntry, body: Partial<TimelineEntry>) => {
    await patchJson(`/timeline/${entry.id}`, body)
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Manual Timeline Entry" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="URL or source">
            <input className="input" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="Source URL or archive" />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Time">
              <input className="input" type="datetime-local" value={time} onChange={(event) => setTime(event.target.value)} />
            </Field>
            <Field label="Platform">
              <input className="input" value={platform} onChange={(event) => setPlatform(event.target.value)} placeholder="X, TikTok, website" />
            </Field>
          </div>
          <Field label="Source label">
            <input className="input" value={source} onChange={(event) => setSource(event.target.value)} placeholder="Account, archive, article, or analyst note" />
          </Field>
          <Field label="Caption or note">
            <textarea className="input min-h-20" value={caption} onChange={(event) => setCaption(event.target.value)} />
          </Field>
          <Field label="Notes">
            <textarea className="input min-h-16" value={notes} onChange={(event) => setNotes(event.target.value)} />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Confidence">
              <select className="input" value={confidence} onChange={(event) => setConfidence(event.target.value)}>
                {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <label className="mt-6 flex items-center gap-2 text-sm text-zinc-700">
              <input type="checkbox" checked={firstKnownAppearance} onChange={(event) => setFirstKnownAppearance(event.target.checked)} />
              First known appearance
            </label>
          </div>
          <button className="btn-primary" type="submit">
            <Plus size={16} />
            Add entry
          </button>
        </form>
      </Panel>
      <Panel title="Source Chronology" icon={<Search size={17} />}>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] border-collapse text-left text-sm">
            <thead className="border-b border-zinc-300 text-xs uppercase text-zinc-500">
              <tr>
                <th className="py-2 pr-3">Time</th>
                <th className="py-2 pr-3">Platform</th>
                <th className="py-2 pr-3">Source</th>
                <th className="py-2 pr-3">Caption</th>
                <th className="py-2 pr-3">Confidence</th>
                <th className="py-2 pr-3">Actions</th>
              </tr>
            </thead>
            <tbody>
              {timeline.map((entry) => (
                <tr key={entry.id} className="border-b border-zinc-200">
                  <td className="py-2 pr-3">
                    <input className="input min-w-48" type="datetime-local" defaultValue={toDateTimeLocal(entry.time)} onBlur={(event) => update(entry, { time: fromDateTimeLocal(event.target.value) })} />
                  </td>
                  <td className="py-2 pr-3">
                    <input className="input min-w-28" defaultValue={entry.platform || ''} onBlur={(event) => update(entry, { platform: event.target.value })} placeholder="Platform" />
                  </td>
                  <td className="max-w-72 py-2 pr-3">
                    <input className="input min-w-56" defaultValue={entry.url || entry.source || ''} onBlur={(event) => update(entry, { url: event.target.value })} placeholder="URL" />
                    <input className="input mt-1 min-w-56" defaultValue={entry.source || ''} onBlur={(event) => update(entry, { source: event.target.value })} placeholder="Source label" />
                  </td>
                  <td className="py-2 pr-3">
                    <textarea className="input min-h-16 min-w-64" defaultValue={entry.caption || ''} onBlur={(event) => update(entry, { caption: event.target.value })} placeholder="Caption" />
                    <textarea className="input mt-1 min-h-16 min-w-64" defaultValue={entry.notes || ''} onBlur={(event) => update(entry, { notes: event.target.value })} placeholder="Notes" />
                  </td>
                  <td className="py-2 pr-3">
                    <select className="input max-w-32" defaultValue={entry.confidence} onChange={(event) => update(entry, { confidence: event.target.value })}>
                      {confidenceLevels.map((value) => <option key={value}>{value}</option>)}
                    </select>
                    <label className="mt-2 flex items-center gap-2 text-xs text-zinc-600">
                      <input type="checkbox" defaultChecked={entry.firstKnownAppearance} onChange={(event) => update(entry, { firstKnownAppearance: event.target.checked })} />
                      First seen
                    </label>
                  </td>
                  <td className="py-2 pr-3">
                    <button className="icon-btn" type="button" onClick={() => remove(entry)} title="Delete entry">
                      <Trash2 size={16} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {timeline.length === 0 && <Empty text="No timeline entries yet." />}
        </div>
      </Panel>
    </div>
  )
}

function InlineReport({ dossierId }: { dossierId: string }) {
  const [state, setState] = useState<LoadState<string>>({ loading: true })
  useEffect(() => {
    getText(`/dossiers/${dossierId}/report/markdown`)
      .then((data) => setState({ loading: false, data }))
      .catch((error) => setState({ loading: false, error: String(error) }))
  }, [dossierId])
  return <ReportView markdown={state.data ?? ''} loading={state.loading} error={state.error} />
}

function EvidenceDetailPage() {
  const { dossierId, evidenceItemId } = useParams()
  const navigate = useNavigate()
  const [state, setState] = useState<LoadState<EvidenceItem>>({ loading: true })
  const [sources, setSources] = useState<Source[]>([])
  const [selectedArtifactId, setSelectedArtifactId] = useState<string | null>(null)

  const load = async () => {
    if (!evidenceItemId) return
    setState({ loading: true })
    try {
      const [evidenceItem, sourceItems] = await Promise.all([
        getJson<EvidenceItem>(`/evidence/${evidenceItemId}`),
        dossierId ? getJson<Source[]>(`/dossiers/${dossierId}/sources`) : Promise.resolve([] as Source[]),
      ])
      setSources(sourceItems)
      setState({ loading: false, data: evidenceItem })
    } catch (error) {
      setState({ loading: false, error: String(error) })
    }
  }

  useEffect(() => {
    if (!evidenceItemId) return
    let cancelled = false
    Promise.all([
      getJson<EvidenceItem>(`/evidence/${evidenceItemId}`),
      dossierId ? getJson<Source[]>(`/dossiers/${dossierId}/sources`) : Promise.resolve([] as Source[]),
    ])
      .then(([evidenceItem, sourceItems]) => {
        if (cancelled) return
        setSources(sourceItems)
        setState({ loading: false, data: evidenceItem })
      })
      .catch((error) => {
        if (!cancelled) setState({ loading: false, error: String(error) })
      })
    return () => {
      cancelled = true
    }
  }, [dossierId, evidenceItemId])

  const run = async (pipeline: 'image' | 'video' | 'text') => {
    if (!evidenceItemId) return
    await postEmpty(`/evidence/${evidenceItemId}/analysis/${pipeline}`)
    await load()
  }
  const updateEvidence = async (body: Partial<EvidenceItem> & { clearSource?: boolean }) => {
    if (!evidenceItemId) return
    await patchJson(`/evidence/${evidenceItemId}`, body)
    await load()
  }
  const remove = async () => {
    if (!evidenceItemId || !window.confirm('Delete this evidence item and its analysis runs?')) return
    await deleteJson(`/evidence/${evidenceItemId}`)
    navigate(dossierId ? `/dossiers/${dossierId}` : '/projects')
  }

  if (state.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading evidence" />
  if (state.error || !state.data) return <InlineStatus icon={<AlertTriangle size={16} />} text={state.error || 'Evidence not found'} tone="warning" />

  const item = state.data
  const latestArtifacts = item.analysisRuns.flatMap((runItem) => runItem.artifacts)
  const selectedArtifact = latestArtifacts.find((artifact) => artifact.id === selectedArtifactId)
  const filePath = item.fileUrl.replace('/api', '')
  const canPreviewText = item.type === 'Text' || item.mimeType?.startsWith('text/') || item.mimeType?.includes('json')
  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <button className="btn-secondary" type="button" onClick={() => navigate(dossierId ? `/dossiers/${dossierId}` : '/projects')}>
          <ArrowRight className="rotate-180" size={16} />
          Back to dossier
        </button>
        <span className="text-sm text-zinc-600">{item.title}</span>
      </div>
      <div className="grid items-start lg:grid-cols-[420px_1fr]">
      <Panel title="Evidence Preview" icon={<Fingerprint size={17} />}>
        <div className="rounded-md border border-zinc-300 bg-zinc-100 p-2">
          {item.type === 'Image' ? (
            <img className="max-h-[360px] w-full rounded object-contain" src={apiUrl(filePath)} alt={item.title} />
          ) : item.type === 'Video' ? (
            <video className="max-h-[360px] w-full rounded" controls src={apiUrl(filePath)} />
          ) : canPreviewText ? (
            <TextEvidencePreview path={filePath} />
          ) : (
            <a className="btn-secondary" href={apiUrl(filePath)}>
              <FileDown size={16} />
              Download file
            </a>
          )}
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          {item.type === 'Image' && (
            <button className="btn-primary" type="button" onClick={() => run('image')}>
              <ImageIcon size={16} />
              Run image analysis
            </button>
          )}
          {item.type === 'Video' && (
            <button className="btn-primary" type="button" onClick={() => run('video')}>
              <Video size={16} />
              Run video analysis
            </button>
          )}
          {canPreviewText && (
            <>
              <button className="btn-primary" type="button" onClick={() => run('text')}>
                <Type size={16} />
                Run text analysis again
              </button>
              <a className="btn-secondary" href={apiUrl(filePath)}>
                <FileDown size={16} />
                Download text
              </a>
            </>
          )}
          <button className="btn-secondary" type="button" onClick={remove}>
            <Trash2 size={16} />
            Delete evidence
          </button>
        </div>
      </Panel>
      <div>
        <Panel title={item.title} icon={<FileJson size={17} />}>
          <MetadataGrid item={item} />
          <div className="mt-3 grid gap-2 sm:grid-cols-2">
            <Field label="Evidence title">
              <input className="input" defaultValue={item.title} onBlur={(event) => updateEvidence({ title: event.target.value })} />
            </Field>
            <Field label="Evidence type">
              <select className="input" defaultValue={item.type} onChange={(event) => updateEvidence({ type: event.target.value })}>
                {evidenceTypes.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Provenance status">
              <select className="input" defaultValue={item.provenanceStatus} onChange={(event) => updateEvidence({ provenanceStatus: event.target.value })}>
                {provenanceStatuses.map((value) => <option key={value}>{value}</option>)}
              </select>
            </Field>
            <Field label="Captured at">
              <input className="input" type="datetime-local" defaultValue={toDateTimeLocal(item.capturedAt)} onBlur={(event) => updateEvidence({ capturedAt: fromDateTimeLocal(event.target.value) })} />
            </Field>
            <Field label="Linked source">
              <select className="input" defaultValue={item.sourceId || ''} onChange={(event) => updateEvidence(event.target.value ? { sourceId: event.target.value } : { clearSource: true })}>
                <option value="">No linked source</option>
                {sources.map((source) => <option key={source.id} value={source.id}>{source.platform || source.type}: {source.title || source.url}</option>)}
              </select>
            </Field>
          </div>
          <div className="mt-2">
            <Field label="Description">
              <textarea className="input min-h-20" defaultValue={item.description || ''} onBlur={(event) => updateEvidence({ description: event.target.value })} placeholder="Evidence description and collection context" />
            </Field>
          </div>
        </Panel>
        <Panel title="Analysis Runs" icon={<Gauge size={17} />}>
          <div className="space-y-2">
            {item.analysisRuns.map((runItem) => <AnalysisRunRow key={runItem.id} run={runItem} />)}
            {item.analysisRuns.length === 0 && <Empty text="No analysis runs yet." />}
          </div>
        </Panel>
        <Panel title="Artifacts" icon={<Archive size={17} />}>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {latestArtifacts.map((artifactItem) => (
              <div key={artifactItem.id} className="rounded-md border border-zinc-300 bg-white p-2 text-sm">
                <span className="block truncate font-medium">{artifactItem.filename}</span>
                <span className="text-xs text-zinc-500">{artifactItem.artifactType || artifactItem.contentType}</span>
                <div className="mt-2 flex flex-wrap gap-2">
                  <button className="btn-secondary" type="button" onClick={() => setSelectedArtifactId(artifactItem.id)}>
                    <Eye size={16} />
                    View
                  </button>
                  <a className="btn-secondary" href={apiUrl(artifactItem.downloadUrl.replace('/api', ''))}>
                    <Download size={16} />
                    Download
                  </a>
                </div>
              </div>
            ))}
            {latestArtifacts.length === 0 && <Empty text="No artifacts yet." />}
          </div>
        </Panel>
      </div>
      </div>
      {selectedArtifact && <ArtifactModal artifact={selectedArtifact} onClose={() => setSelectedArtifactId(null)} />}
    </div>
  )
}

function TextEvidencePreview({ path }: { path: string }) {
  const [state, setState] = useState<LoadState<string>>({ loading: true })

  useEffect(() => {
    let cancelled = false
    getText(path)
      .then((data) => {
        if (!cancelled) setState({ loading: false, data })
      })
      .catch((error) => {
        if (!cancelled) setState({ loading: false, error: String(error) })
      })
    return () => {
      cancelled = true
    }
  }, [path])

  if (state.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading text evidence" />
  if (state.error) return <InlineStatus icon={<AlertTriangle size={16} />} text={state.error} tone="warning" />

  return (
    <pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap rounded bg-black p-3 text-sm leading-6 text-zinc-100">
      {state.data || 'No text content stored for this evidence item.'}
    </pre>
  )
}

function ArtifactModal({ artifact, onClose }: { artifact: AnalysisArtifact; onClose: () => void }) {
  const viewUrl = apiUrl(`/analysis-artifacts/${artifact.id}/view`)
  const downloadUrl = apiUrl(artifact.downloadUrl.replace('/api', ''))
  const isImage = artifact.contentType?.startsWith('image/')
  const isText = artifact.contentType?.includes('json') || artifact.contentType?.startsWith('text/')

  return (
    <div className="modal-backdrop fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4" role="dialog" aria-modal="true" aria-label={artifact.filename}>
      <div className="modal-panel flex max-h-[92vh] w-full max-w-6xl flex-col overflow-hidden border border-zinc-300 bg-white shadow-2xl">
        <div className="flex items-center justify-between gap-3 border-b border-zinc-300 px-4 py-3">
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold">{artifact.filename}</div>
            <div className="text-xs text-zinc-500">{artifact.artifactType || artifact.contentType || 'artifact'}</div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <a className="btn-secondary" href={downloadUrl}>
              <Download size={16} />
              Download
            </a>
            <button className="icon-btn" type="button" onClick={onClose} title="Close artifact viewer">
              <X size={16} />
            </button>
          </div>
        </div>
        <div className="min-h-0 flex-1 overflow-auto bg-zinc-100 p-4">
          {isImage ? (
            <img className="mx-auto max-h-[78vh] w-full object-contain" src={viewUrl} alt={artifact.filename} />
          ) : isText ? (
            <iframe className="h-[78vh] w-full bg-white" title={artifact.filename} src={viewUrl} />
          ) : (
            <div className="flex min-h-[360px] flex-col justify-center gap-3 text-sm text-zinc-600">
              <FileJson size={28} />
              This artifact type cannot be previewed inline. Use Download to inspect it locally.
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function ArtifactDetailPage() {
  const { dossierId, evidenceItemId, artifactId } = useParams()
  const [state, setState] = useState<LoadState<EvidenceItem>>({ loading: true })

  useEffect(() => {
    if (!evidenceItemId) return
    getJson<EvidenceItem>(`/evidence/${evidenceItemId}`)
      .then((data) => setState({ loading: false, data }))
      .catch((error) => setState({ loading: false, error: String(error) }))
  }, [evidenceItemId])

  if (state.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading artifact" />
  if (state.error || !state.data) return <InlineStatus icon={<AlertTriangle size={16} />} text={state.error || 'Artifact not found'} tone="warning" />

  const item = state.data
  const artifact = item.analysisRuns.flatMap((run) => run.artifacts).find((entry) => entry.id === artifactId)
  if (!artifact) return <InlineStatus icon={<AlertTriangle size={16} />} text="Artifact not found for this evidence item." tone="warning" />

  const viewUrl = apiUrl(`/analysis-artifacts/${artifact.id}/view`)
  const downloadUrl = apiUrl(artifact.downloadUrl.replace('/api', ''))
  const isImage = artifact.contentType?.startsWith('image/')
  const isText = artifact.contentType?.includes('json') || artifact.contentType?.startsWith('text/')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link className="btn-secondary" to={`/dossiers/${dossierId}/evidence/${evidenceItemId}`}>
          <ArrowRight className="rotate-180" size={16} />
          Back to evidence
        </Link>
        <a className="btn-primary" href={downloadUrl}>
          <Download size={16} />
          Download artifact
        </a>
      </div>
      <Panel title={artifact.filename} icon={<Archive size={17} />}>
        <div className="rounded-md border border-zinc-300 bg-zinc-100 p-3">
          {isImage ? (
            <img className="max-h-[72vh] w-full object-contain" src={viewUrl} alt={artifact.filename} />
          ) : isText ? (
            <iframe className="h-[72vh] w-full rounded bg-white" title={artifact.filename} src={viewUrl} />
          ) : (
            <div className="flex flex-col gap-3 p-6 text-sm text-zinc-600">
              <FileJson size={28} />
              This artifact type cannot be previewed inline. Use the download action to inspect it locally.
            </div>
          )}
        </div>
      </Panel>
    </div>
  )
}

function MetadataGrid({ item }: { item: EvidenceItem }) {
  const rows = [
    ['Type', item.type],
    ['Provenance', item.provenanceStatus],
    ['MIME', item.mimeType || 'unknown'],
    ['Size', item.fileSizeBytes ? `${item.fileSizeBytes} bytes` : 'unknown'],
    ['Dimensions', item.width && item.height ? `${item.width} x ${item.height}` : 'not recorded'],
    ['Duration', item.durationSeconds ? `${item.durationSeconds}s` : 'not recorded'],
    ['SHA-256', item.contentHashSha256 || 'not recorded'],
  ]
  return (
    <dl className="grid gap-2 text-sm md:grid-cols-2">
      {rows.map(([label, value]) => (
        <div key={label} className="rounded-md border border-zinc-200 bg-zinc-50 p-2">
          <dt className="text-xs font-semibold uppercase text-zinc-500">{label}</dt>
          <dd className="mt-1 break-all font-mono text-xs text-zinc-800">{value}</dd>
        </div>
      ))}
    </dl>
  )
}

function AnalysisRunRow({ run }: { run: AnalysisRun }) {
  const result = parseAnalysisResult(run.resultJson)
  return (
    <div className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge>{run.pipeline}</Badge>
        <Badge tone={run.status === 'Completed' ? 'green' : run.status === 'Failed' ? 'amber' : 'zinc'}>{run.status}</Badge>
        {run.toolVersion && <Badge>{run.toolVersion}</Badge>}
      </div>
      <p className="mt-2 text-sm text-zinc-600">{run.summary || run.error || 'No summary yet.'}</p>
      {result && (
        <div className="mt-3 rounded-md border border-zinc-300 bg-zinc-50 p-3 text-sm">
          <div className="mb-2 text-xs font-semibold uppercase text-zinc-500">Result</div>
          {result.signals?.length ? (
            <ul className="list-disc space-y-1 pl-5 text-zinc-700">
              {result.signals.map((signal) => <li key={signal}>{signal}</li>)}
            </ul>
          ) : (
            <p className="text-zinc-700">{result.signalText || result.summary || 'No strong signals recorded.'}</p>
          )}
          {result.limitation && <p className="mt-2 text-xs text-zinc-500">{result.limitation}</p>}
        </div>
      )}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <span className="text-xs text-zinc-500">{run.artifacts.length} artifacts</span>
        {run.artifacts.length > 0 && (
          <a className="btn-secondary" href={apiUrl(`/analysis-runs/${run.id}/artifacts.zip`)}>
            <Download size={16} />
            Download all
          </a>
        )}
      </div>
    </div>
  )
}

function parseAnalysisResult(resultJson?: string): { signals?: string[]; signalText?: string; summary?: string; limitation?: string } | null {
  if (!resultJson) return null
  try {
    const parsed = JSON.parse(resultJson) as { signals?: unknown; signalText?: unknown; summary?: unknown; limitation?: unknown }
    return {
      signals: Array.isArray(parsed.signals) ? parsed.signals.filter((item): item is string => typeof item === 'string') : undefined,
      signalText: typeof parsed.signalText === 'string' ? parsed.signalText : undefined,
      summary: typeof parsed.summary === 'string' ? parsed.summary : undefined,
      limitation: typeof parsed.limitation === 'string' ? parsed.limitation : undefined,
    }
  } catch {
    return { signalText: resultJson }
  }
}

function ReportPage() {
  const { dossierId } = useParams()
  const [state, setState] = useState<LoadState<string>>({ loading: true })
  useEffect(() => {
    if (!dossierId) return
    getText(`/dossiers/${dossierId}/report/markdown`)
      .then((data) => setState({ loading: false, data }))
      .catch((error) => setState({ loading: false, error: String(error) }))
  }, [dossierId])
  return <ReportView markdown={state.data ?? ''} loading={state.loading} error={state.error} />
}

export function ReportView({ markdown, loading, error }: { markdown: string; loading?: boolean; error?: string }) {
  return (
    <Panel title="Markdown Report" icon={<FileText size={17} />}>
      {loading && <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Generating report" />}
      {error && <InlineStatus icon={<AlertTriangle size={16} />} text={error} tone="warning" />}
      {!loading && !error && (
        <pre className="max-h-[72vh] overflow-auto whitespace-pre-wrap rounded-md border border-zinc-300 bg-zinc-950 p-4 text-sm leading-6 text-zinc-100">
          {markdown}
        </pre>
      )}
    </Panel>
  )
}

type SettingsData = {
  name?: string
  collectionPolicy?: string
  userAgent?: string
  ethicalBoundaries?: string[]
}

function SettingsPage() {
  const [state, setState] = useState<LoadState<SettingsData>>({ loading: true })
  useEffect(() => {
    getJson<SettingsData>('/settings')
      .then((data) => setState({ loading: false, data }))
      .catch((error) => setState({ loading: false, error: String(error) }))
  }, [])

  const settings = state.data
  return (
    <div className="grid gap-0 lg:grid-cols-[0.9fr_1.1fr]">
      <Panel title="Workspace Settings" icon={<Settings size={17} />}>
        {state.loading && <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading settings" />}
        {state.error && <InlineStatus icon={<AlertTriangle size={16} />} text={state.error} tone="warning" />}
        {settings && (
          <div className="grid gap-3">
            <StatusTile label="Application" value={settings.name || 'Veritas'} detail="local workbench" tone="green" />
            <div className="rounded-md border border-zinc-300 bg-white p-3">
              <div className="text-xs font-semibold uppercase text-zinc-500">Collection policy</div>
              <p className="mt-2 text-sm text-zinc-700">{settings.collectionPolicy || 'No collection policy reported.'}</p>
            </div>
            <div className="rounded-md border border-zinc-300 bg-white p-3">
              <div className="text-xs font-semibold uppercase text-zinc-500">Robots user agent</div>
              <p className="mt-2 break-all font-mono text-sm text-zinc-700">{settings.userAgent || 'Not configured'}</p>
            </div>
          </div>
        )}
      </Panel>
      <Panel title="Operating Boundaries" icon={<ShieldCheck size={17} />}>
        {settings ? (
          <div className="grid gap-2">
            {(settings.ethicalBoundaries || []).map((boundary) => (
              <div key={boundary} className="rounded-md border border-zinc-300 bg-white p-3 text-sm text-zinc-700">
                {boundary}
              </div>
            ))}
          </div>
        ) : (
          <Empty text="Settings unavailable." />
        )}
      </Panel>
    </div>
  )
}

function Panel({ title, icon, children, className = '' }: { title: string; icon: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`workspace-panel border border-zinc-300 bg-[#fbfbf8] p-4 ${className}`}>
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-zinc-800">
        <span className="text-emerald-800">{icon}</span>
        <h2>{title}</h2>
      </div>
      {children}
    </section>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-zinc-700">{label}</span>
      {children}
    </label>
  )
}

function Badge({ children, tone = 'zinc' }: { children: ReactNode; tone?: 'zinc' | 'green' | 'amber' }) {
  const styles = {
    zinc: 'border-zinc-300 bg-zinc-100 text-zinc-700',
    green: 'border-emerald-300 bg-emerald-50 text-emerald-900',
    amber: 'border-amber-300 bg-amber-50 text-amber-950',
  }
  return <span className={`inline-flex h-6 items-center rounded border px-2 text-xs font-medium ${styles[tone]}`}>{children}</span>
}

function ConfidenceBadge({ confidence }: { confidence: string }) {
  const tone = confidence === 'High' ? 'green' : confidence === 'Medium' || confidence === 'Low' ? 'amber' : 'zinc'
  return <Badge tone={tone}>{confidence} confidence</Badge>
}

function InlineStatus({ icon, text, tone = 'zinc' }: { icon: ReactNode; text: string; tone?: 'zinc' | 'warning' }) {
  return (
    <div className={`flex items-center gap-2 rounded-md border p-3 text-sm ${tone === 'warning' ? 'border-amber-300 bg-amber-50 text-amber-950' : 'border-zinc-300 bg-white text-zinc-700'}`}>
      {icon}
      <span>{text}</span>
    </div>
  )
}

function Empty({ text }: { text: string }) {
  return <div className="rounded-md border border-dashed border-zinc-300 bg-white p-4 text-sm text-zinc-500">{text}</div>
}
