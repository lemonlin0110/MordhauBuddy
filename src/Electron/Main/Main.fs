namespace MordhauBuddy.App

module Main =
    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.Import
    open Electron
    open Node.Api
    open Bindings

    /// A global reference to the window object is required in order to prevent garbage collection
    let mutable mainWindow: BrowserWindow option = Option.None
    /// A global reference to the tray object is required in order to prevent garbage collection
    let mutable tray: Tray option = Option.None
#if DEBUG

    module DevTools =
        let private installDevTools (extensionRef: obj) (forceDownload: bool): JS.Promise<string> =
            importDefault "electron-devtools-installer"
        let private REACT_DEVELOPER_TOOLS: obj = import "REACT_DEVELOPER_TOOLS" "electron-devtools-installer"
        let private REDUX_DEVTOOLS: obj = import "REDUX_DEVTOOLS" "electron-devtools-installer"

        let private installDevTool extensionRef =
            promise {
                try
                    let! name = installDevTools extensionRef false
                    JS.console.log ("Added extension", name)
                with err -> JS.console.log ("An error occurred adding extension:", err)
            }
            |> ignore

        let installAllDevTools (win: BrowserWindow) =
            installDevTool REACT_DEVELOPER_TOOLS
            installDevTool REDUX_DEVTOOLS
            win.webContents.executeJavaScript ("require('devtron').install()")

        let uninstallAllDevTools (win: BrowserWindow) =
            main.BrowserWindow.removeDevToolsExtension ("React Developer Tools")
            main.BrowserWindow.removeDevToolsExtension ("Redux DevTools")
            win.webContents.executeJavaScript ("require('devtron').uninstall()")

        let connectRemoteDevViaExtension: unit -> unit = import "connectViaExtension" "remotedev"

#endif

    let startBridge() =
        let bridgeProc =

#if DEBUG
            let bridgePath = path.resolve (__dirname, "..", "../../src/Core/Core.fsproj")

            let args =
                let init = ResizeArray<string>()
                [ "watch"; "run"; "-f"; "netcoreapp3.1"; "--project"; bridgePath ] |> List.iter init.Add
                init

            let options =
                let cwd = path.resolve (__dirname, "..", "../../src/Core")
                {| shell = true
                   stdio = "inherit"
                   cwd = cwd |} |> toPlainJsObj

            childProcess.spawn ("dotnet", args, options = options)
#else
            let bridgePath =
                let core =
                    match ``process``.platform with
                    | Node.Base.Platform.Win32 -> "Core.exe"
                    | _ -> "Core"
                path.resolve (__dirname, "..", "extraResources", core)
            let args =
                let init = ResizeArray<string>()
                init

            let options =
                {| shell = false
                   stdio = "inherit" |} |> toPlainJsObj
            childProcess.spawn (bridgePath, args, options = options)
#endif

        bridgeProc

    /// Create instance lock, focus and/or restore window if
    /// a second is launched.
    if main.app.requestSingleInstanceLock() |> not then
        main.app.quit()
    else
        main.app.onSecondInstance (fun _ _ _ ->
            match mainWindow with
            | Some(win) ->
                if win.isMinimized() then win.restore()
                win.focus()
            | None -> ())
        |> ignore

    /// Create the websocket bridge.
    let bridge = startBridge()

    /// Create the system tray
    let createTray() =
        let show =
            main.MenuItem.Create
                (jsOptions<MenuItemOptions> (fun o ->
                    o.label <- "Open"
                    o.click <-
                        System.Action<_, _, _>(fun _ _ _ ->
                            match mainWindow with
                            | Some(win) -> win.show()
                            | None -> ())))
            |> U2.Case2

        let quit =
            main.MenuItem.Create
                (jsOptions<MenuItemOptions> (fun o ->
                    o.label <- "Quit MordhauBuddy"
                    o.role <- MenuItemRole.Quit))
            |> U2.Case2

        /// Create the base sytem tray object
        let appTray =
            let iconPath =
#if DEBUG
                "icon.png"
#else
                path.resolve (__dirname, "..", "..", "static", "icon.png")
#endif
            main.Tray.Create(iconPath)

        main.Menu.buildFromTemplate [| show; quit |]
        |> Some
        |> appTray.setContextMenu

        appTray.onClick (fun _ _ _ ->
            match mainWindow with
            | Some(win) -> win.show()
            | _ -> ())
        |> ignore

        appTray.setToolTip "MordhauBuddy"
        tray <- Some appTray

    /// Create the main window.
    let createMainWindow() =
        let mainWinState =
            WindowState.getState
                (jsOptions<WindowState.Options> (fun o ->
                    o.defaultHeight <- 925
                    o.defaultWidth <- 1250))

        let win =
            main.BrowserWindow.Create
                (jsOptions<BrowserWindowOptions> (fun o ->
                    o.width <- mainWinState.width
                    o.height <- mainWinState.height
                    o.minHeight <- 925
                    o.minWidth <- 1250
                    o.autoHideMenuBar <- true
                    o.webPreferences <-
                        jsOptions<WebPreferences> (fun w ->
                            w.webSecurity <- false
                            w.contextIsolation <- false
                            w.nodeIntegration <- true)
                    o.frame <- false
                    o.backgroundColor <- "#FFF"
                    o.show <- false))

        win.onceReadyToShow (fun _ ->
            win.setTitle <| sprintf "%s - %s" Info.name Info.version
            if win.isMinimized() |> not then win.show()
            mainWinState.manage win)
        |> ignore
#if DEBUG
        /// Set up dev tools
        DevTools.installAllDevTools win |> ignore
        DevTools.connectRemoteDevViaExtension()
        /// Open dev tools on startup
        win.webContents.openDevTools()
        /// Load correct URL
        win.loadURL (sprintf "http://localhost:%s" ``process``.env?ELECTRON_WEBPACK_WDS_PORT) |> ignore
        ``process``.on ("uncaughtException", (fun err -> JS.console.log (err.ToString()))) |> ignore
#else
        path.join (__dirname, "index.html")
        |> sprintf "file:///%s"
        |> win.loadURL
        |> ignore
#endif

        /// Dereference the window object when closed. If your app supports
        /// multiple windows, you can store them in an array and delete the
        /// corresponding element here.
        win.onClosed (fun _ ->
            bridge.kill()
            mainWindow <- None
            tray <- None)
        |> ignore
        mainWindow <- Some win

    /// This method will be called when Electron has finished
    /// initialization and is ready to create browser windows.
    main.app.onReady (fun _ _ ->
        createMainWindow()
        createTray())
    |> ignore
    /// Quit when all windows are closed.
    main.app.onWindowAllClosed (fun _ ->
        /// On OS X it's common for applications and their menu bar
        /// to stay active until the user quits explicitly with Cmd + Q
        if ``process``.platform <> Node.Base.Platform.Darwin then main.app.quit())
    |> ignore
    main.app.onActivate (fun _ _ ->
        /// On OS X it's common to re-create a window in the app when the
        /// dock icon is clicked and there are no other windows open.
        if mainWindow.IsNone then createMainWindow()
        if tray.IsNone then createTray())
    |> ignore
