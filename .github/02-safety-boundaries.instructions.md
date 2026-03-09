---
applyTo: "**"
---

# Safety Boundaries

These boundaries apply to all operations. When uncertain if an action violates these boundaries, ask before executing.

## Git: Read-only by Default

- No modifications to working tree, index, refs, or remote without explicit permission
- Safe: any command that only reads/queries (log, diff, status, show, blame, ls-*, rev-*)
- Forbidden without permission: commit, push, pull, fetch, checkout, reset, rebase, merge, cherry-pick, revert, stash push/pop/drop, tag (create), branch (create/delete)

## File Operations

- Allowed: within workspace only
- Forbidden: write/delete outside workspace

## External Systems

- Read-only for any remote API, service, or resource
- Forbidden: create, update, delete operations on external systems
- Forbidden: sending workspace data externally

## Always Forbidden

- Credential, secret, or token operations
- Package install without explicit approval
- Process/service management (stop, start, restart)
- Environment variable modifications that persist
- Database writes
- Cloud resource creation or deletion

## Uncertainty Protocol

- When uncertain if an operation violates these boundaries, ask before executing
- Before executing unfamiliar commands, explain what they do and wait for approval
