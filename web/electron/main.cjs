const { app, BrowserWindow } = require('electron')

const url = process.argv.slice(1).find(argument => argument.startsWith('http'))

app.whenReady().then(() => {
  const window = new BrowserWindow({
    width: 1280,
    height: 840,
    minWidth: 1024,
    minHeight: 680,
    backgroundColor: '#313338',
    title: 'Travian Guard',
    autoHideMenuBar: true,
    show: false,
  })

  window.once('ready-to-show', () => {
    window.maximize()
    window.show()
  })
  window.loadURL(url || 'data:text/html,<body style="background:#313338;color:#f2f3f5;font-family:sans-serif;padding:32px">Falta la dirección de la administración.</body>')
})

app.on('window-all-closed', () => app.quit())
