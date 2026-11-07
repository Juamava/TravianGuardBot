// Window buttons for the title bar the page draws itself.
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('tbsWindow', {
  minimize: () => ipcRenderer.send('window', 'minimize'),
  toggleMaximize: () => ipcRenderer.send('window', 'maximize'),
  close: () => ipcRenderer.send('window', 'close'),
  onMaximized: callback => ipcRenderer.on('window-maximized', (_event, value) => callback(Boolean(value))),
})
