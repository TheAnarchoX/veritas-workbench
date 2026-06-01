# Security Policy

## Supported versions

The `main` branch is the active development line. Security fixes should target `main` unless a release branch is explicitly maintained.

## Reporting a vulnerability

Do not open a public issue for suspected vulnerabilities. Send a private report to the maintainers with:

- A concise description of the issue and affected component.
- Reproduction steps or a minimal proof of concept.
- Expected impact and any known mitigations.
- Whether the report involves real third-party data.

If no private contact is configured for your fork, create a private advisory or contact the repository owner directly before publishing details.

## Project-specific security boundaries

Veritas Workbench must not add features that enable:

- Unauthorized collection, scraping, account access, or platform bypasses.
- Private-person face recognition, deanonymization, stalking, harassment, or doxxing.
- Misleading certainty around media authenticity, identity, or attribution.
- Uploading sensitive evidence to third-party services without explicit operator control.

## Handling evidence

Treat uploaded files, text samples, sources, and generated artifacts as potentially sensitive. Local development data under `data/` is ignored by git and should not be committed.
