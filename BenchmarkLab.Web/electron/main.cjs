const { app, BrowserWindow, dialog } = require('electron');

const frontendUrl = 'http://localhost:4200';

async function createWindow() {
  const window = new BrowserWindow({
    width: 1280,
    height: 850,
    minWidth: 900,
    minHeight: 600,
    title: 'Sysbenchmark',
    backgroundColor: '#111827',
    autoHideMenuBar: true,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true
    }
  });

  // A felület ne nyithasson új ablakokat.
  window.webContents.setWindowOpenHandler(() => ({
    action: 'deny'
  }));

  // Navigálni csak a helyi Angular-felületen belül lehet.
  window.webContents.on('will-navigate', (event, url) => {
    if (new URL(url).origin !== frontendUrl) {
      event.preventDefault();
    }
  });

  // Jelenleg nincs szükség böngészős eszközengedélyekre.
  window.webContents.session.setPermissionRequestHandler(
    (_webContents, _permission, callback) => callback(false)
  );

  window.webContents.session.setPermissionCheckHandler(
    () => false
  );

  try {
    await window.loadURL(frontendUrl);
  } catch {
    dialog.showErrorBox(
      'A felület nem érhető el',
      'Indítsd el az Angular fejlesztői szervert az npm start ' +
      'paranccsal, majd indítsd újra az Electron alkalmazást.'
    );

    app.quit();
  }
}

app.whenReady()
  .then(createWindow)
  .catch((error) => {
    console.error('Az alkalmazás indítása sikertelen:', error);
    app.quit();
  });

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow().catch((error) => {
      console.error('Az ablak megnyitása sikertelen:', error);
      app.quit();
    });
  }
});