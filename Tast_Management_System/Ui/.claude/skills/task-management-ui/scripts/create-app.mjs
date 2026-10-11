#!/usr/bin/env node
// Scaffolds the Task Management UI: Vite react-ts + ESLint, react-router, and the login template.
//
//   node create-app.mjs <target-dir> [--name <package-name>] [--skip-install] [--force]
//
// The app is generated in a temp folder first and then copied into <target-dir>, so existing
// files there (.claude/, .git/, .vscode/, ...) are kept. Files that would be overwritten stop
// the script unless --force is passed.

import { spawnSync } from 'node:child_process'
import { cpSync, existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, delimiter, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const CREATE_VITE = 'create-vite@9' // v9 defaults React templates to Oxlint; --eslint opts back in
const REACT_ROUTER = 'react-router@^8'
const TEMPLATE_DIR = resolve(dirname(fileURLToPath(import.meta.url)), '..', 'assets', 'template')
// Scaffold files the template replaces or makes unused.
const REMOVE = ['src/App.css', 'src/assets', 'public/icons.svg', 'README.md']

const args = process.argv.slice(2)
const flag = (name) => args.includes(name)
const option = (name) => {
  const i = args.indexOf(name)
  return i >= 0 ? args[i + 1] : undefined
}
const positional = args.filter((a, i) => !a.startsWith('--') && !(i > 0 && args[i - 1] === '--name'))

if (positional.length !== 1) {
  console.error('Usage: node create-app.mjs <target-dir> [--name <package-name>] [--skip-install] [--force]')
  process.exit(2)
}

const target = resolve(positional[0])
const packageName = toPackageName(option('--name') ?? basename(target))
const force = flag('--force')

// Node may be installed but missing from PATH (common on Windows); child npm/npx processes need it.
const env = { ...process.env, PATH: `${dirname(process.execPath)}${delimiter}${process.env.PATH ?? ''}` }

function run(command, cwd) {
  console.log(`\n> ${command}   (in ${cwd})`)
  const result = spawnSync(command, { cwd, env, shell: true, stdio: 'inherit' })
  if (result.status !== 0) {
    console.error(`\nFAILED (exit ${result.status}): ${command}`)
    process.exit(result.status ?? 1)
  }
}

function toPackageName(name) {
  const cleaned = name
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, '-')
    .replace(/^[._-]+|[._-]+$/g, '')
  return cleaned.length > 1 ? cleaned : 'task-management-ui'
}

function listFiles(dir, root = dir) {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry)
    return statSync(full).isDirectory() ? listFiles(full, root) : [relative(root, full)]
  })
}

// 1. Generate into a temp folder.
const work = mkdtempSync(join(tmpdir(), 'task-ui-'))
const staged = join(work, 'app')
try {
  run(`npx -y ${CREATE_VITE} app --template react-ts --eslint --no-interactive --no-immediate`, work)

  // 2. Apply the template.
  for (const path of REMOVE) rmSync(join(staged, path), { recursive: true, force: true })
  cpSync(TEMPLATE_DIR, staged, { recursive: true })

  const pkgPath = join(staged, 'package.json')
  const pkg = JSON.parse(readFileSync(pkgPath, 'utf8'))
  pkg.name = packageName
  writeFileSync(pkgPath, `${JSON.stringify(pkg, null, 2)}\n`)

  const htmlPath = join(staged, 'index.html')
  writeFileSync(htmlPath, readFileSync(htmlPath, 'utf8').replace(/<title>.*<\/title>/, '<title>Task Management</title>'))

  // 3. Copy into the target without clobbering anything the user already has.
  const files = listFiles(staged)
  const conflicts = existsSync(target) ? files.filter((f) => existsSync(join(target, f))) : []
  if (conflicts.length > 0 && !force) {
    console.error(`\nThese files already exist in ${target} and would be overwritten:`)
    for (const f of conflicts) console.error(`  ${f}`)
    console.error('Re-run with --force to overwrite them, or choose another folder.')
    process.exit(1)
  }
  cpSync(staged, target, { recursive: true, force: true })
} finally {
  rmSync(work, { recursive: true, force: true })
}

// 4. Install, then prove it lints and builds.
if (flag('--skip-install')) {
  console.log(`\nScaffolded ${packageName} in ${target} (install skipped).`)
  process.exit(0)
}
run('npm install', target)
run(`npm install ${REACT_ROUTER}`, target)
run('npm run lint', target)
run('npm run build', target)

console.log(`\nDone: ${packageName} in ${target}. Lint and build passed.`)
console.log('Next: start TaskManagement.Api (https profile), then `npm run dev` and open http://localhost:5173.')
