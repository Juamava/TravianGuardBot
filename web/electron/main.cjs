const { app, BrowserWindow, session, shell } = require('electron')
const { spawn, execFileSync } = require('child_process')
const fs = require('fs')
const path = require('path')

const appRoot = path.resolve(path.dirname(process.execPath), '..')
const logDir = process.env.TBS_LOG_DIR || path.join(appRoot, 'logs')
const logFile = path.join(logDir, 'electron.log')

app.setPath('userData', path.join(appRoot, 'Data', 'electron'))

function isGameLink(target) {
  return /^https?:\/\//i.test(target) && !/^https?:\/\/(127\.0\.0\.1|localhost)([:/]|$)/i.test(target)
}

let chromePath = ''
let chromeLooked = false

function chromeExecutable() {
  if (chromeLooked) return chromePath
  chromeLooked = true
  const candidates = [
    process.env.LOCALAPPDATA && path.join(process.env.LOCALAPPDATA, 'Google', 'Chrome', 'Application', 'chrome.exe'),
    process.env.PROGRAMFILES && path.join(process.env.PROGRAMFILES, 'Google', 'Chrome', 'Application', 'chrome.exe'),
    process.env['PROGRAMFILES(X86)'] && path.join(process.env['PROGRAMFILES(X86)'], 'Google', 'Chrome', 'Application', 'chrome.exe'),
  ].filter(Boolean)
  for (const key of ['HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe', 'HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe']) {
    try {
      const output = execFileSync('reg', ['query', key, '/ve'], { encoding: 'utf8', windowsHide: true })
      const match = output.match(/REG_SZ\s+(\S.*)/)
      if (match) candidates.unshift(match[1].trim())
    } catch {
      // Chrome can still be in one of the usual folders.
    }
  }
  chromePath = candidates.find(candidate => fs.existsSync(candidate)) || ''
  return chromePath
}

function openInChrome(target) {
  if (!isGameLink(target)) return
  const chrome = chromeExecutable()
  log((chrome ? 'abre en chrome ' : 'abre con el navegador del sistema ') + target)
  if (!chrome) {
    shell.openExternal(target)
    return
  }
  const child = spawn(chrome, [target], { detached: true, stdio: 'ignore', windowsHide: true })
  child.unref()
}

function log(message) {
  try {
    fs.mkdirSync(logDir, { recursive: true })
    fs.appendFileSync(logFile, new Date().toISOString() + ' ' + message + '\n')
  } catch {
    // The window still has to open when the log folder cannot be written.
  }
}

const url = process.argv.slice(1).find(argument => argument.startsWith('http'))
let window
let reportedFailure = false

function show(detail) {
  if (!window || window.isDestroyed()) return
  if (!window.isVisible()) {
    window.maximize()
    window.show()
  }
  if (!detail || reportedFailure) return
  reportedFailure = true
  const message = String(detail).replace(/[&<>]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[character]))
  const html = '<body style="background:#313338;color:#f2f3f5;font-family:Segoe UI,sans-serif;padding:32px"><h2>No se pudo abrir la administración</h2><p>' + message + '</p><p>El detalle quedó en la carpeta logs.</p></body>'
  window.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(html))
}

app.whenReady().then(() => {
  log('abre ' + (url || '(sin dirección)') + ' perfil=' + app.getPath('userData'))
  window = new BrowserWindow({
    width: 1280,
    height: 840,
    minWidth: 1024,
    minHeight: 680,
    backgroundColor: '#313338',
    title: 'Travian Guard',
    autoHideMenuBar: true,
    show: false,
  })

  window.webContents.on('before-input-event', (event, input) => {
    if (input.type !== 'keyDown' || input.isAutoRepeat) return
    if (!input.control || input.alt || input.meta || input.shift) return
    if (input.code !== 'KeyD' && String(input.key || '').toLowerCase() !== 'd') return
    event.preventDefault()
    window.webContents.executeJavaScript('window.dispatchEvent(new Event("travian-toggle-debug"))').catch(() => undefined)
  })

  window.once('ready-to-show', () => {
    log('lista para mostrar')
    window.maximize()
    window.show()
  })

  // Game links open in the person's Chrome, where they are signed in.
  // A new Electron window, and the bot's Playwright browser, stay out of it.
  window.webContents.setWindowOpenHandler(({ url: target }) => {
    openInChrome(target)
    return { action: 'deny' }
  })
  window.webContents.on('will-navigate', (event, target) => {
    if (!isGameLink(target)) return
    event.preventDefault()
    openInChrome(target)
  })

  window.webContents.on('did-fail-load', (_event, code, description, failedUrl, isMainFrame) => {
    log('falló la carga ' + code + ' ' + description + ' main=' + isMainFrame + ' ' + failedUrl)
    if (isMainFrame && !String(failedUrl).startsWith('data:')) show(description + ' (' + code + ')')
  })
  window.webContents.on('did-finish-load', () => log('cargó ' + window.webContents.getURL()))
  window.webContents.on('render-process-gone', (_event, details) => {
    log('el proceso de la página murió ' + details.reason + ' ' + details.exitCode)
    show('La página se cerró: ' + details.reason)
  })
  window.webContents.on('console-message', (_event, level, message, line, source) => {
    const details = level && typeof level === 'object' ? level : { level, message, lineNumber: line, sourceId: source }
    if ((details.level ?? 0) < 2) return
    log('consola ' + details.message + ' ' + (details.sourceId || '') + ':' + (details.lineNumber || ''))
  })
  window.webContents.on('unresponsive', () => log('la página no responde'))

  session.defaultSession.webRequest.onErrorOccurred(details => {
    log('red ' + details.error + ' ' + details.url)
  })
  session.defaultSession.webRequest.onCompleted(details => {
    if (details.statusCode >= 400) log('http ' + details.statusCode + ' ' + details.url)
  })

  app.on('child-process-gone', (_event, details) => {
    log('proceso hijo ' + details.type + ' ' + details.reason + ' ' + details.exitCode)
  })

  window.loadURL(url || 'data:text/html,<body style="background:#313338;color:#f2f3f5;font-family:sans-serif;padding:32px">Falta la dirección de la administración.</body>')
})

app.on('window-all-closed', () => app.quit())
process.on('uncaughtException', error => log('excepción ' + (error && error.stack ? error.stack : error)))
