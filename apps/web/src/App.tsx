import {
  Activity,
  AlertTriangle,
  Archive,
  ArrowRight,
  BadgeCheck,
  CheckCircle2,
  ClipboardList,
  Database,
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
  Upload,
  Video,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import {
  BrowserRouter,
  Link,
  Navigate,
  Route,
  Routes,
  useParams,
} from 'react-router-dom'
import { apiUrl, getJson, getText, patchJson, postEmpty, postForm, postJson } from './api'
import type {
  AnalysisRun,
  Claim,
  Dossier,
  DossierBundle,
  EvidenceItem,
  Finding,
  InvestigationTask,
  Project,
  Source,
  TimelineEntry,
} from './types'

type LoadState<T> = {
  data?: T
  error?: string
  loading: boolean
}

const tabs = ['Overview', 'Evidence', 'Sources', 'Findings', 'Claims', 'Tasks', 'Timeline', 'Report']
type Theme = 'light' | 'dark'

function getInitialTheme(): Theme {
  if (typeof window === 'undefined') return 'dark'
  const stored = window.localStorage.getItem('veritas-theme')
  return stored === 'light' || stored === 'dark' ? stored : 'dark'
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
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
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
      <main className="mx-auto max-w-7xl px-4 py-4">{children}</main>
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
    void load()
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
    void load()
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
    <div className="grid gap-4 lg:grid-cols-[360px_1fr]">
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
      <Panel title={state.data.project.name} icon={<FolderKanban size={17} />}>
        <p className="mb-4 text-sm text-zinc-600">{state.data.project.description || 'No description'}</p>
        <div className="grid gap-2">
          {state.data.dossiers.map((dossier) => (
            <Link key={dossier.id} to={`/dossiers/${dossier.id}`} className="rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h2 className="text-base font-semibold">{dossier.title}</h2>
                  <p className="mt-1 text-sm text-zinc-600">{dossier.summary || 'No summary'}</p>
                </div>
                <Badge tone="green">{dossier.status}</Badge>
              </div>
            </Link>
          ))}
          {state.data.dossiers.length === 0 && <Empty text="No dossiers in this project." />}
        </div>
      </Panel>
    </div>
  )
}

function DossierPage() {
  const { dossierId } = useParams()
  const [bundle, setBundle] = useState<LoadState<DossierBundle>>({ loading: true })
  const [activeTab, setActiveTab] = useState('Overview')

  const load = async () => {
    if (!dossierId) return
    setBundle({ loading: true })
    try {
      setBundle({ loading: false, data: await getJson<DossierBundle>(`/dossiers/${dossierId}`) })
    } catch (error) {
      setBundle({ loading: false, error: String(error) })
    }
  }

  useEffect(() => {
    void load()
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
  const counts = {
    Evidence: bundle.evidence.length,
    Sources: bundle.sources.length,
    Findings: bundle.findings.length,
    Claims: bundle.claims.length,
    Tasks: bundle.tasks.length,
    Timeline: bundle.timeline.length,
  }

  return (
    <div className="space-y-4">
      <section className="workspace-hero rounded-md border border-zinc-300 bg-white p-4">
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

      <div className="grid gap-4 xl:grid-cols-[280px_1fr]">
        <WorkflowRail bundle={bundle} activeTab={activeTab} onTab={onTab} />
        <div className="min-w-0 space-y-4">
          <div className="flex gap-2 overflow-x-auto border-b border-zinc-300 pb-2" role="tablist" aria-label="dossier tabs">
            {tabs.map((tab) => (
              <button
                key={tab}
                type="button"
                role="tab"
                aria-selected={activeTab === tab}
                className={`h-9 shrink-0 rounded-md border px-3 text-sm ${activeTab === tab ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-zinc-300 bg-white text-zinc-800'}`}
                onClick={() => onTab?.(tab)}
              >
                {tab} {tab in counts ? <span className="ml-1 opacity-80">{counts[tab as keyof typeof counts]}</span> : null}
              </button>
            ))}
          </div>

          {activeTab === 'Overview' && <OverviewTab bundle={bundle} onTab={onTab} />}
          {activeTab === 'Evidence' && <EvidenceTab dossierId={bundle.dossier.id} evidence={bundle.evidence} sources={bundle.sources} onRefresh={onRefresh} />}
          {activeTab === 'Sources' && <SourcesTab dossierId={bundle.dossier.id} sources={bundle.sources} evidence={bundle.evidence} onRefresh={onRefresh} />}
          {activeTab === 'Findings' && <FindingsTab findings={bundle.findings} />}
          {activeTab === 'Claims' && <ClaimsTab dossierId={bundle.dossier.id} claims={bundle.claims} onRefresh={onRefresh} />}
          {activeTab === 'Tasks' && <TasksTab dossierId={bundle.dossier.id} tasks={bundle.tasks} onRefresh={onRefresh} />}
          {activeTab === 'Timeline' && <TimelineTab dossierId={bundle.dossier.id} timeline={bundle.timeline} onRefresh={onRefresh} />}
          {activeTab === 'Report' && <InlineReport dossierId={bundle.dossier.id} />}
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
  const steps = [
    { label: '1. Intake', tab: 'Sources', icon: <Search size={16} />, metric: `${bundle.sources.length} sources`, ready: bundle.sources.length > 0 },
    { label: '2. Preserve', tab: 'Evidence', icon: <Database size={16} />, metric: `${bundle.evidence.length} evidence`, ready: bundle.evidence.length > 0 },
    { label: '3. Analyze', tab: 'Evidence', icon: <Activity size={16} />, metric: `${runStats.completed}/${runStats.runs.length} complete`, ready: runStats.completed > 0 },
    { label: '4. Corroborate', tab: 'Findings', icon: <Network size={16} />, metric: `${bundle.findings.length} findings`, ready: bundle.findings.length > 0 },
    { label: '5. Report', tab: 'Report', icon: <FileText size={16} />, metric: `${openTasks} open tasks`, ready: bundle.claims.length > 0 && openTasks === 0 },
  ]

  return (
    <aside className="workflow-rail rounded-md border border-zinc-300 bg-white p-3">
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-zinc-800">
        <RouteIcon size={16} className="text-emerald-800" aria-hidden="true" />
        Workflow
      </div>
      <div className="space-y-2">
        {steps.map((step) => (
          <button
            key={step.label}
            type="button"
            className={`workflow-step w-full rounded-md border p-3 text-left ${activeTab === step.tab ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-zinc-300 bg-white text-zinc-800'}`}
            onClick={() => onTab?.(step.tab)}
          >
            <span className="flex items-center justify-between gap-3">
              <span className="flex min-w-0 items-center gap-2">
                {step.icon}
                <span className="truncate text-sm font-semibold">{step.label}</span>
              </span>
              <Badge tone={step.ready ? 'green' : 'zinc'}>{step.ready ? 'active' : 'open'}</Badge>
            </span>
            <span className="mt-2 block text-xs opacity-80">{step.metric}</span>
          </button>
        ))}
      </div>
    </aside>
  )
}

function OverviewTab({ bundle, onTab }: { bundle: DossierBundle; onTab?: (tab: string) => void }) {
  const openTasks = bundle.tasks.filter((task) => task.status !== 'Done').length
  const highConfidence = bundle.findings.filter((finding) => finding.confidence === 'High').length
  const runStats = getRunStats(bundle)
  const sourceLinkedEvidence = bundle.evidence.filter((item) => item.sourceId).length
  const latestEvidence = bundle.evidence.slice(0, 5)

  return (
    <div className="space-y-4">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Metric label="Evidence" value={bundle.evidence.length} icon={<Database size={17} />} />
        <Metric label="Linked sources" value={sourceLinkedEvidence} icon={<LinkIcon size={17} />} />
        <Metric label="Open tasks" value={openTasks} icon={<ListChecks size={17} />} />
        <Metric label="High confidence findings" value={highConfidence} icon={<BadgeCheck size={17} />} />
      </div>

      <Panel title="Case Flow" icon={<RouteIcon size={17} />}>
        <div className="workflow-map grid gap-2 md:grid-cols-5">
          <FlowNode label="Source" value={bundle.sources.length} icon={<Search size={16} />} onClick={() => onTab?.('Sources')} />
          <FlowNode label="Evidence" value={bundle.evidence.length} icon={<Database size={16} />} onClick={() => onTab?.('Evidence')} />
          <FlowNode label="Runs" value={runStats.completed} suffix={`/${runStats.runs.length}`} icon={<Activity size={16} />} onClick={() => onTab?.('Evidence')} />
          <FlowNode label="Findings" value={bundle.findings.length} icon={<Gauge size={16} />} onClick={() => onTab?.('Findings')} />
          <FlowNode label="Report" value={bundle.claims.length} icon={<FileText size={16} />} onClick={() => onTab?.('Report')} />
        </div>
      </Panel>

      <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <Panel title="Evidence Matrix" icon={<Network size={17} />}>
          <div className="space-y-2">
            {latestEvidence.map((item) => {
              const latestRun = item.analysisRuns[0]
              return (
                <Link key={item.id} to={`/dossiers/${item.dossierId}/evidence/${item.id}`} className="matrix-row rounded-md border border-zinc-300 bg-white p-3 hover:border-emerald-700">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge>{item.type}</Badge>
                    <Badge tone={item.sourceId ? 'green' : 'amber'}>{item.sourceId ? 'source linked' : 'unlinked source'}</Badge>
                    <Badge tone={latestRun?.status === 'Completed' ? 'green' : latestRun ? 'amber' : 'zinc'}>{latestRun?.status ?? 'not analyzed'}</Badge>
                  </div>
                  <div className="mt-2 flex items-center justify-between gap-3">
                    <span className="truncate text-sm font-semibold">{item.title}</span>
                    <ArrowRight size={15} aria-hidden="true" />
                  </div>
                  <p className="mt-1 truncate font-mono text-xs text-zinc-600">{item.contentHashSha256 || 'hash pending'}</p>
                </Link>
              )
            })}
            {bundle.evidence.length === 0 && <Empty text="No evidence uploaded." />}
          </div>
        </Panel>
        <Panel title="Control Queue" icon={<ClipboardList size={17} />}>
          <div className="space-y-2">
            {bundle.tasks.slice(0, 6).map((task) => <TaskRow key={task.id} task={task} />)}
            {bundle.tasks.length === 0 && <Empty text="No tasks yet." />}
          </div>
        </Panel>
      </div>

      <Panel title="Recent Findings" icon={<Gauge size={17} />}>
        <div className="grid gap-2 lg:grid-cols-2">
          {bundle.findings.slice(0, 4).map((finding) => <FindingCard key={finding.id} finding={finding} />)}
          {bundle.findings.length === 0 && <Empty text="No findings yet." />}
        </div>
      </Panel>
    </div>
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
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <UploadEvidenceForm dossierId={dossierId} sources={sources} onUploaded={onRefresh} />
      <Panel title="Evidence Inventory" icon={<Database size={17} />}>
        <div className="grid gap-2">
          {evidence.map((item) => <EvidenceRow key={item.id} item={item} />)}
          {evidence.length === 0 && <Empty text="No evidence uploaded." />}
        </div>
      </Panel>
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
            <option>Unknown</option>
            <option>OriginalProvided</option>
            <option>PlatformOriginal</option>
            <option>ScreenshotOnly</option>
            <option>Recompressed</option>
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

function SourcesTab({ dossierId, sources, evidence, onRefresh }: { dossierId: string; sources: Source[]; evidence: EvidenceItem[]; onRefresh?: () => void }) {
  const [url, setUrl] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    if (!url.trim()) return
    setBusy(true)
    try {
      await postJson(`/dossiers/${dossierId}/sources/url`, { url })
      setUrl('')
      onRefresh?.()
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Source" icon={<LinkIcon size={17} />}>
        <form className="space-y-3" onSubmit={submit}>
          <Field label="URL">
            <input className="input" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="https://x.com/example/status/123" />
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
              <p className="mt-2 break-all text-sm font-medium">{source.url || source.title}</p>
              <p className="mt-1 text-sm text-zinc-600">{source.notes}</p>
            </div>
          ))}
          {sources.length === 0 && <Empty text="No sources recorded." />}
        </div>
      </Panel>
    </div>
  )
}

function FindingsTab({ findings }: { findings: Finding[] }) {
  return (
    <Panel title="Findings" icon={<Gauge size={17} />}>
      <div className="grid gap-2 lg:grid-cols-2">
        {findings.map((finding) => <FindingCard key={finding.id} finding={finding} />)}
        {findings.length === 0 && <Empty text="No findings yet." />}
      </div>
    </Panel>
  )
}

export function FindingCard({ finding }: { finding: Finding }) {
  return (
    <article className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="mb-2 flex flex-wrap gap-2">
        <Badge>{finding.category}</Badge>
        <ConfidenceBadge confidence={finding.confidence} />
        <Badge tone={finding.direction === 'Inconclusive' ? 'amber' : 'zinc'}>{finding.direction}</Badge>
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
    </article>
  )
}

function ClaimsTab({ dossierId, claims, onRefresh }: { dossierId: string; claims: Claim[]; onRefresh?: () => void }) {
  const [text, setText] = useState('')
  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!text.trim()) return
    await postJson(`/dossiers/${dossierId}/claims`, { text })
    setText('')
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Claim" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="Claim">
            <textarea className="input min-h-24" value={text} onChange={(event) => setText(event.target.value)} placeholder="What evidence-supported claim is being assessed?" />
          </Field>
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
            </div>
          ))}
          {claims.length === 0 && <Empty text="No claims yet." />}
        </div>
      </Panel>
    </div>
  )
}

function TasksTab({ dossierId, tasks, onRefresh }: { dossierId: string; tasks: InvestigationTask[]; onRefresh?: () => void }) {
  const [title, setTitle] = useState('')
  const add = async (event: FormEvent) => {
    event.preventDefault()
    if (!title.trim()) return
    await postJson(`/dossiers/${dossierId}/tasks`, { title, taskType: 'Other', priority: 'Medium' })
    setTitle('')
    onRefresh?.()
  }
  const markDone = async (task: InvestigationTask) => {
    await patchJson(`/tasks/${task.id}`, { status: 'Done' })
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Add Task" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="Task">
            <input className="input" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Manual verification step" />
          </Field>
          <button className="btn-primary" type="submit">
            <Plus size={16} />
            Add task
          </button>
        </form>
      </Panel>
      <Panel title="Tasks" icon={<ClipboardList size={17} />}>
        <div className="space-y-2">
          {tasks.map((task) => <TaskRow key={task.id} task={task} onDone={() => markDone(task)} />)}
          {tasks.length === 0 && <Empty text="No tasks yet." />}
        </div>
      </Panel>
    </div>
  )
}

function TaskRow({ task, onDone }: { task: InvestigationTask; onDone?: () => void }) {
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
    </div>
  )
}

function TimelineTab({ dossierId, timeline, onRefresh }: { dossierId: string; timeline: TimelineEntry[]; onRefresh?: () => void }) {
  const [url, setUrl] = useState('')
  const [caption, setCaption] = useState('')
  const add = async (event: FormEvent) => {
    event.preventDefault()
    await postJson(`/dossiers/${dossierId}/timeline`, { url, caption, confidence: 'Low' })
    setUrl('')
    setCaption('')
    onRefresh?.()
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <Panel title="Manual Timeline Entry" icon={<Plus size={17} />}>
        <form className="space-y-3" onSubmit={add}>
          <Field label="URL or source">
            <input className="input" value={url} onChange={(event) => setUrl(event.target.value)} placeholder="Source URL or archive" />
          </Field>
          <Field label="Caption or note">
            <textarea className="input min-h-20" value={caption} onChange={(event) => setCaption(event.target.value)} />
          </Field>
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
              </tr>
            </thead>
            <tbody>
              {timeline.map((entry) => (
                <tr key={entry.id} className="border-b border-zinc-200">
                  <td className="py-2 pr-3">{new Date(entry.time).toLocaleString()}</td>
                  <td className="py-2 pr-3">{entry.platform || '-'}</td>
                  <td className="max-w-72 truncate py-2 pr-3">{entry.url || entry.source || '-'}</td>
                  <td className="py-2 pr-3">{entry.caption || entry.notes || '-'}</td>
                  <td className="py-2 pr-3"><ConfidenceBadge confidence={entry.confidence} /></td>
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
  const { evidenceItemId } = useParams()
  const [state, setState] = useState<LoadState<EvidenceItem>>({ loading: true })

  const load = async () => {
    if (!evidenceItemId) return
    setState({ loading: true })
    try {
      setState({ loading: false, data: await getJson<EvidenceItem>(`/evidence/${evidenceItemId}`) })
    } catch (error) {
      setState({ loading: false, error: String(error) })
    }
  }

  useEffect(() => {
    void load()
  }, [evidenceItemId])

  const run = async (pipeline: 'image' | 'video') => {
    if (!evidenceItemId) return
    await postEmpty(`/evidence/${evidenceItemId}/analysis/${pipeline}`)
    await load()
  }

  if (state.loading) return <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading evidence" />
  if (state.error || !state.data) return <InlineStatus icon={<AlertTriangle size={16} />} text={state.error || 'Evidence not found'} tone="warning" />

  const item = state.data
  const latestArtifacts = item.analysisRuns.flatMap((runItem) => runItem.artifacts)
  return (
    <div className="grid gap-4 lg:grid-cols-[420px_1fr]">
      <Panel title="Evidence Preview" icon={<Fingerprint size={17} />}>
        <div className="rounded-md border border-zinc-300 bg-zinc-100 p-2">
          {item.type === 'Image' ? (
            <img className="max-h-[360px] w-full rounded object-contain" src={apiUrl(item.fileUrl.replace('/api', ''))} alt={item.title} />
          ) : item.type === 'Video' ? (
            <video className="max-h-[360px] w-full rounded" controls src={apiUrl(item.fileUrl.replace('/api', ''))} />
          ) : (
            <a className="btn-secondary" href={apiUrl(item.fileUrl.replace('/api', ''))}>
              <FileDown size={16} />
              Download file
            </a>
          )}
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          <button className="btn-primary" type="button" onClick={() => run('image')}>
            <ImageIcon size={16} />
            Run image analysis
          </button>
          <button className="btn-secondary" type="button" onClick={() => run('video')}>
            <Video size={16} />
            Run video analysis
          </button>
        </div>
      </Panel>
      <div className="space-y-4">
        <Panel title={item.title} icon={<FileJson size={17} />}>
          <MetadataGrid item={item} />
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
              <a key={artifactItem.id} className="rounded-md border border-zinc-300 bg-white p-2 text-sm hover:border-emerald-700" href={apiUrl(artifactItem.downloadUrl.replace('/api', ''))}>
                <span className="block truncate font-medium">{artifactItem.filename}</span>
                <span className="text-xs text-zinc-500">{artifactItem.artifactType || artifactItem.contentType}</span>
              </a>
            ))}
            {latestArtifacts.length === 0 && <Empty text="No artifacts yet." />}
          </div>
        </Panel>
      </div>
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
  return (
    <div className="rounded-md border border-zinc-300 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge>{run.pipeline}</Badge>
        <Badge tone={run.status === 'Completed' ? 'green' : run.status === 'Failed' ? 'amber' : 'zinc'}>{run.status}</Badge>
        {run.toolVersion && <Badge>{run.toolVersion}</Badge>}
      </div>
      <p className="mt-2 text-sm text-zinc-600">{run.summary || run.error || 'No summary yet.'}</p>
      <p className="mt-1 text-xs text-zinc-500">{run.artifacts.length} artifacts</p>
    </div>
  )
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

function SettingsPage() {
  const [state, setState] = useState<LoadState<Record<string, unknown>>>({ loading: true })
  useEffect(() => {
    getJson<Record<string, unknown>>('/settings')
      .then((data) => setState({ loading: false, data }))
      .catch((error) => setState({ loading: false, error: String(error) }))
  }, [])
  return (
    <Panel title="Settings" icon={<Settings size={17} />}>
      {state.loading && <InlineStatus icon={<Loader2 className="animate-spin" size={16} />} text="Loading settings" />}
      {state.error && <InlineStatus icon={<AlertTriangle size={16} />} text={state.error} tone="warning" />}
      {state.data && <pre className="overflow-auto rounded-md border border-zinc-300 bg-white p-3 text-sm">{JSON.stringify(state.data, null, 2)}</pre>}
    </Panel>
  )
}

function Metric({ label, value, icon }: { label: string; value: number; icon: ReactNode }) {
  return (
    <div className="rounded-md border border-zinc-300 bg-white p-4">
      <div className="flex items-center justify-between">
        <span className="text-sm text-zinc-600">{label}</span>
        <span className="text-emerald-800">{icon}</span>
      </div>
      <div className="mt-2 text-2xl font-semibold">{value}</div>
    </div>
  )
}

function Panel({ title, icon, children, className = '' }: { title: string; icon: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-md border border-zinc-300 bg-[#fbfbf8] p-4 ${className}`}>
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
