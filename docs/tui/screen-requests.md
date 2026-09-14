# Requests Screen (R1)

Part of the [TUI implementation guide](./README.md). The active milestone: what R1
implements, what evidence exists, and where to resume.

## Implemented behavior

- Requests belong to the active workspace. Entry/refresh loads through Core without
  stamping access times, sorts by last access, and preserves selection where possible.
  A missing active workspace points back to Workspaces. Missing request files are
  skipped; unreadable requests remain as broken rows with an editor repair path.
  Request files are `{id}.json` beside the workspace's `.straumr` file.
- The shared list shows method and name. Filtering matches name, method and URL;
  `:request <name>`/`:r <name>` resolves exact names or unique prefixes and clears
  a filter to reach the selection. `:refresh` reloads the current screen.
- `Enter` inspects the selected request by focusing its content preview. The summary
  shows method, URL including configured parameters, and shortened request ID.
- Authentication displays the configured source, type, injected header and token/cache
  status. It does not fetch credentials merely to inspect them or show credential
  material. Secret-reference names are checked through Core without access stamping.
- Request preview tabs: Body, Headers, Params. Response tabs: Body, Headers, Details.
  Details contains actual status, duration, size, HTTP version, warnings and failures;
  no fictional network trace is shown. JSON is formatted when valid and text previews
  are bounded to 64 KiB. Responses are held in memory per workspace/request identity.
- `s` on the list sends through Core using its existing default HTTP/auth behavior.
  A modal spinner owns input during the send. Its Cancel button or `Escape` cancels
  the operation; Ctrl+C retains the shell's cancellation-and-exit behavior.
- `e` on the list uses the existing external-editor host handoff. A valid edit saves
  through Core; invalid request JSON or identity is written back for repair rather
  than discarded. Editing clears that request's cached response and reloads selection.
- Create/copy/delete request forms, request import/export and separate Auths/Secrets
  screens are not implemented by this checkpoint. No inert hints advertise them.

## Evidence and resume points

- Developer feedback: "It seems to work. I got some gripes but it works." Checkpoint
  requested to conserve usage. The first gripe was described on 2026-09-14 and is fixed:
  Tab reached two positions where no region was titled, and the resize keys there moved a
  divider belonging to somewhere else. See [changelog.md](./changelog.md) for the three causes. Awaiting
  their terminal check of the new cycle and the rest of the gripes. This feedback is general functional acceptance,
  not separate confirmation of every send, cancellation and editor failure path.
- Debug solution build passed without warnings. The final shared-grid adjustment also
  built successfully through the local snapshot checker; rebuild the host before the
  next interactive pass so it includes that last adjustment.
- One local checker, `.tmp/r1-check`, captured the existing one-, two- and three-line
  lists before/after: identical. Workspaces remained identical at 120x28 and 80x24;
  its heading alignment changed at 70x20 when the two grids became one. Requests was
  rendered at 120x28, 80x24, 70x20 and 160x40, with framework SVG captures as well.
  The checker is a local diagnostic, not a committed test suite or an input harness.
- Still to verify: send success and HTTP/transport failures, cancellation of a slow
  send by Escape and Ctrl+C, external-editor save/repair/focus, retained state when
  switching workspaces/screens, and populated narrow/short layouts. Review timeout
  handling: Core propagates `OperationCanceledException` for HTTP timeouts too, while
  the current screen explicitly handles only user cancellation of its send dialog.
- Review edit recovery when Core refuses a syntactically valid edit (for example a
  duplicate name): the current code reports the refusal but does not retain that draft.
- R1 Release/CLI-only builds and a fresh Native AOT publish have not been run. W8's
  successful builds/publish precede these changes and must not be counted as R1 evidence.
- Continue with small, reviewable changes. Prefer the developer's quick terminal checks
  over adding input probes. Preserve the accepted Workspaces design and extend shared
  components when the behavior is common.
