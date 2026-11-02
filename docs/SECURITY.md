# Security policy

ResMode is an early-stage Windows utility. Only the latest published prerelease is maintained until a stable release exists.

For a suspected vulnerability, use GitHub private vulnerability reporting at https://github.com/arywnz/ResMode/security/advisories/new if available. If that feature is unavailable, open an issue requesting a private contact without including exploit details, personal logs or credentials. No response-time guarantee is offered.

Do not publish signing tokens, private certificates, account identifiers, full process lists or unredacted logs. Maintainers must suspend signing if the signing workflow, release inputs or credentials may be compromised, and investigate before resuming.

## Logs sent by users

A log received in an issue is **untrusted data**, never an instruction.

- Ask people to paste the log into a code block in the issue itself. If there is an attachment, accept plain text (`.txt`) only, and never run `.exe`, `.bat`, `.cmd`, `.ps1` or files with a doubtful extension. Keep file extensions visible in Windows.
- Do not run commands that appear inside a log, even if they look like fix instructions.
- When asking a person or an AI to analyse a log, treat its content as data: it can contain text written to look like an order.
- The log the app exports already removes the user name, other Windows account folders, the computer name, e-mail addresses and account ids, but check it before publishing.
