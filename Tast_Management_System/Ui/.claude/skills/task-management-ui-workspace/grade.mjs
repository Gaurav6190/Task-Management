// Grades every run in an iteration folder and writes <run>/grading.json.
//   node grade.mjs <iteration-dir> <apps-dir>
// <apps-dir>/<eval-name>-<config> is where the run built its app (with node_modules), used for lint/build.
import { spawnSync } from 'node:child_process'
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { delimiter, dirname, join, relative } from 'node:path'

const [iterationDir, appsDir] = process.argv.slice(2)
const env = { ...process.env, PATH: `${dirname(process.execPath)}${delimiter}${process.env.PATH}` }

function files(dir) {
  if (!existsSync(dir)) return []
  return readdirSync(dir).flatMap((e) => {
    if (['node_modules', 'dist', '.git'].includes(e)) return []
    const full = join(dir, e)
    return statSync(full).isDirectory() ? files(full) : [full]
  })
}

function sh(cmd, cwd) {
  const r = spawnSync(cmd, { cwd, env, shell: true, encoding: 'utf8', timeout: 300000 })
  const out = `${r.stdout ?? ''}${r.stderr ?? ''}`.trim()
  return { ok: r.status === 0, tail: out.split('\n').slice(-6).join(' | ').slice(0, 400) }
}

function grade(evalName, appSrcDir, builtDir) {
  const all = files(appSrcDir)
  const rel = (f) => relative(appSrcDir, f).replace(/\\/g, '/')
  const read = (f) => readFileSync(f, 'utf8')
  const src = all.filter((f) => /[\\/]src[\\/].*\.(tsx?|css)$/.test(f))
  const code = src.filter((f) => /\.tsx?$/.test(f)).map((f) => ({ f: rel(f), t: read(f) }))
  const grep = (re) => code.filter(({ t }) => re.test(t)).map(({ f }) => f)
  const pkgPath = join(appSrcDir, 'package.json')
  const pkg = existsSync(pkgPath) ? JSON.parse(read(pkgPath)) : {}
  const deps = { ...pkg.dependencies, ...pkg.devDependencies }
  const viteCfg = all.find((f) => /vite\.config\.(ts|js|mts)$/.test(f))
  const viteTxt = viteCfg ? read(viteCfg) : ''
  const results = []
  const add = (text, passed, evidence) => results.push({ text, passed: !!passed, evidence })

  add('Vite + React + TypeScript project (vite, react, typescript deps; tsconfig; src/main.tsx)',
    deps.vite && deps.react && deps.typescript && all.some((f) => /tsconfig\.json$/.test(f)) && all.some((f) => /src[\\/]main\.tsx$/.test(f)),
    `vite=${deps.vite} react=${deps.react} typescript=${deps.typescript}`)

  const eslintCfg = all.find((f) => /eslint\.config\.(js|mjs|ts)$/.test(f))
  add('ESLint is the linter (eslint dep, eslint.config.*, lint script runs eslint, no oxlint)',
    deps.eslint && eslintCfg && /eslint/.test(pkg.scripts?.lint ?? '') && !deps.oxlint,
    `eslint=${deps.eslint} config=${eslintCfg ? rel(eslintCfg) : 'none'} lint="${pkg.scripts?.lint}" oxlint=${deps.oxlint ?? 'absent'}`)

  if (builtDir && existsSync(join(builtDir, 'node_modules'))) {
    const lint = sh('npm run lint', builtDir)
    add('npm run lint passes with no errors', lint.ok, lint.tail)
    const build = sh('npm run build', builtDir)
    add('npm run build (tsc -b + vite build) passes', build.ok, build.tail)
  } else {
    add('npm run lint passes with no errors', false, `no installed app at ${builtDir}`)
    add('npm run build (tsc -b + vite build) passes', false, `no installed app at ${builtDir}`)
  }

  const emailInput = grep(/type=["']email["']/)
  add('Username input uses type="email"', emailInput.length, emailInput.join(', ') || 'no type="email" input found')

  const emailCheck = grep(/@[^'"`\n]*\\\.|isValidEmail|validateEmail|validateUsername|EMAIL_(PATTERN|REGEX)|emailRegex/i)
  add('Client-side email validation before submitting (pattern/validator in code)', emailCheck.length, emailCheck.join(', ') || 'no email validator found')

  const ws = grep(/replace\(\s*\/\\s[+*]?\/g|\.trim\(\)/)
  add('Whitespace is stripped or trimmed from the username input', ws.length, ws.join(', ') || 'no whitespace handling found')

  const maxLen = grep(/256/)
  add('Username limited to 256 characters (matches API validator)', maxLen.length, maxLen.join(', ') || '256 not found')

  const loginCall = grep(/\/api\/auth\/login/)
  add('Login POSTs to /api/auth/login', loginCall.length, loginCall.join(', ') || 'endpoint not referenced')

  const proxy = /proxy[\s\S]*['"]\/api['"]/.test(viteTxt)
  const httpsDev = /https\s*:/.test(viteTxt) && /5173/.test(viteTxt)
  add('Dev setup avoids the API CORS mismatch (Vite /api proxy, or https on :5173)', proxy || httpsDev,
    proxy ? 'vite /api proxy' : httpsDev ? 'https dev server on 5173' : 'neither proxy nor https on 5173 in vite config')

  const token = grep(/accessToken/)
  // tokenType is always "Bearer", so `${tokenType} ${accessToken}` counts too.
  const bearer = grep(/Bearer|tokenType\}?\s*\$?\{?[\w.]*accessToken/)
  add('Access token from the response is kept and sent as a Bearer header', token.length && bearer.length,
    `accessToken in: ${token.join(', ') || '-'}; Bearer in: ${bearer.join(', ') || '-'}`)

  // Showing the API's problem.detail also works: for a 401 it is "Invalid username or password."
  const invalidCreds = grep(/401|InvalidCredentials|problem\??\.detail/)
  add('Handles 401 / invalid credentials with a message', invalidCreds.length, invalidCreds.join(', ') || 'no 401 handling')

  if (evalName === 'casual-with-tasks-page') {
    const tasks = grep(/\/tasks/)
    const logout = grep(/logout|signOut|sign out|log out/i)
    add('Has a /tasks page reached after login, with a logout button', tasks.length && logout.length,
      `tasks: ${tasks.join(', ') || '-'}; logout: ${logout.join(', ') || '-'}`)
  }
  if (evalName === 'disabled-submit-variant') {
    const disabled = grep(/type=["']submit["'][\s\S]{0,200}disabled=\{[^}]*(valid|Valid|canSubmit|Error|error)/)
    const disabled2 = grep(/disabled=\{[^}]*(isValid|canSubmit|!valid|invalid|formValid|isFormValid)/)
    const hits = [...new Set([...disabled, ...disabled2])]
    add('Sign in button is disabled until both fields are valid', hits.length, hits.join(', ') || 'no validity-bound disabled prop on submit')
  }

  const passed = results.filter((r) => r.passed).length
  return {
    expectations: results,
    summary: { passed, failed: results.length - passed, total: results.length, pass_rate: +(passed / results.length).toFixed(2) },
  }
}

for (const evalName of readdirSync(iterationDir)) {
  const evalDir = join(iterationDir, evalName)
  if (!statSync(evalDir).isDirectory()) continue
  for (const config of ['with_skill', 'without_skill']) {
    const runDir = join(evalDir, config)
    if (!existsSync(runDir)) continue
    const g = grade(evalName, join(runDir, 'outputs', 'app'), join(appsDir, `${evalName}-${config}`))
    writeFileSync(join(runDir, 'grading.json'), JSON.stringify(g, null, 2))
    console.log(`${evalName}/${config}: ${g.summary.passed}/${g.summary.total}`)
    for (const r of g.expectations.filter((r) => !r.passed)) console.log(`   FAIL ${r.text} -- ${r.evidence}`)
  }
}
