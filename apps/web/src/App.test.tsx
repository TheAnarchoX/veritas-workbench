import { fireEvent, render, screen } from '@testing-library/react'
import type { ReactElement } from 'react'
import { BrowserRouter } from 'react-router-dom'
import {
  DossierTabs,
  FindingCard,
  ProjectListView,
  ReportView,
  UploadEvidenceForm,
} from './App'
import type { DossierBundle, Finding, Project } from './types'

const projects: Project[] = [
  {
    id: 'project-1',
    name: 'Demo Project',
    description: 'Synthetic investigation',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    dossierCount: 1,
  },
]

const finding: Finding = {
  id: 'finding-1',
  dossierId: 'dossier-1',
  category: 'Metadata',
  claim: 'No EXIF metadata present.',
  confidence: 'High',
  direction: 'Neutral',
  evidence: 'No EXIF tags were returned.',
  limitations: 'This is not evidence of AI generation.',
  falsificationPath: 'Provide original camera file.',
  createdAt: new Date().toISOString(),
}

const bundle: DossierBundle = {
  dossier: {
    id: 'dossier-1',
    projectId: 'project-1',
    title: 'Example dossier',
    summary: 'Review provenance and limitations.',
    status: 'Active',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  sources: [],
  evidence: [
    {
      id: 'evidence-1',
      dossierId: 'dossier-1',
      type: 'Image',
      title: 'Uploaded image',
      contentHashSha256: 'abc123',
      mimeType: 'image/jpeg',
      uploadedAt: new Date().toISOString(),
      provenanceStatus: 'ScreenshotOnly',
      fileUrl: '/api/evidence/evidence-1/file',
      analysisRuns: [],
    },
  ],
  findings: [finding],
  claims: [],
  tasks: [],
  timeline: [],
}

function withRouter(ui: ReactElement) {
  return <BrowserRouter>{ui}</BrowserRouter>
}

test('project list renders', () => {
  render(withRouter(<ProjectListView projects={projects} />))

  expect(screen.getByText('Demo Project')).toBeInTheDocument()
  expect(screen.getByLabelText('project list')).toBeInTheDocument()
})

test('dossier evidence tab renders', () => {
  render(withRouter(<DossierTabs bundle={bundle} activeTab="Evidence" />))

  expect(screen.getByText('Upload Evidence')).toBeInTheDocument()
  expect(screen.getByText('Uploaded image')).toBeInTheDocument()
})

test('upload form validates missing file', () => {
  render(<UploadEvidenceForm dossierId="dossier-1" />)

  fireEvent.click(screen.getByRole('button', { name: /upload original or screenshot/i }))

  expect(screen.getByText('Choose a file before uploading.')).toBeInTheDocument()
})

test('finding confidence badge renders', () => {
  render(<FindingCard finding={finding} />)

  expect(screen.getByText('High confidence')).toBeInTheDocument()
  expect(screen.getByText('No EXIF metadata present.')).toBeInTheDocument()
})

test('report page renders markdown', () => {
  render(<ReportView markdown={'# Example dossier\n\n## Scope and limitations'} />)

  expect(screen.getByText(/Example dossier/)).toBeInTheDocument()
  expect(screen.getByText(/Scope and limitations/)).toBeInTheDocument()
})
