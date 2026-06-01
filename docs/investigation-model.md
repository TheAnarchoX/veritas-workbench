# Investigation Model

The workspace is organized as:

- Project: a top-level investigation container.
- Dossier: a specific case or media/persona question inside a project.
- Source: a URL, manual upload, screenshot, archive, social post, or note.
- Evidence item: immutable uploaded media or document linked to a dossier and optionally a source.
- Analysis run: a worker invocation over one evidence item.
- Finding: a cautious claim fragment with confidence, direction, evidence, limitations, and falsification path.
- Claim: a user-level allegation or hypothesis being assessed.
- Investigation task: a concrete next step.
- Timeline entry: source chronology and first-known-appearance tracking.
- Chain-of-custody event: upload, hash, analysis, export, annotation, replacement, or deletion record.

The model is designed to resist confirmation bias. Claims can be unassessed, unsupported, weak, plausible, likely, confirmed, or disproved. Findings can support authenticity, alteration, AI indicators, inauthentic persona indicators, remain neutral, or be inconclusive.
