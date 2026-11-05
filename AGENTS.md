# Working on CARCHECK

React demo + Windows Card Bridge v3. Keep the Thai UI and existing behavior.

## Source and commands
- Edit React in src/. Root app.js is generated; do not hand-edit it.
- Install: npm ci. Test: npm test. Build: npm run build.
- Local UI preview: npm run dev at http://127.0.0.1:5173.
- Root index.html, style.css, icons.js, font/images are GitHub Pages assets. Preserve relative paths for /carcheck-demo/ hosting.
- Windows C# source: bridge/src/CardBridge/. bridge/Build.cmd publishes a .NET 10 Windows x64 application and runs self-tests. bridge/Run-Web.cmd enables the GitHub origin.
- Windows/WinSCard and a real reader are needed for hardware tests. State clearly when C# compilation, DPAPI or hardware testing cannot run.

## Product constraints
- Attendance belongs under บัญชีของฉัน. Card fields and attendance rows are read-only.
- Attendance remains memory-only. Do not add database writes, production calls, LINE sending or real personal data unless the user explicitly requests that scope.
- Remember only pairing token and reader name in browser storage; Windows uses current-user DPAPI. Never commit tokens, pairing.dpapi, passwords, card data, logs or connection strings.
- Preserve localhost binding, token authentication, exact origin checks and card re-read before confirmation. Do not use wildcard CORS.
- Local UI preview is not an allowed Bridge origin yet. Do not claim localhost hardware integration works without adding/testing a deliberate origin configuration.
- Login currently accepts any nonempty fields: it is not production authentication.
- SQL Server/API integration has only been discussed; it is not implemented.
- HTML report templates have not been verified against original production reports.

After UI edits, run relevant tests and build. Include the regenerated app.js for publishing and update its index.html cache query when needed. Preserve unrelated changes.
