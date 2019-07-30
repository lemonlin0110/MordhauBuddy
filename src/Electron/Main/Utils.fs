namespace MordhauBuddy.App

module Utils =
    open MordhauBuddy.Electron
    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.Import
    open System

    let getRemoteWin() = renderer.remote.getCurrentWindow()

    [<Emit("__static + \"/\" + $0")>]
    let private stat' (s : string) : string = jsNative

    /// Prefixes the string with the static asset root path.
    let stat (s : string) =
#if DEBUG
        s
#else
        stat' s
#endif


    [<AutoOpen>]
    module Extensions =
        type Result<'T, 'TError> with

            member this.IsOk =
                match this with
                | Ok _ -> true
                | Error _ -> false

            member this.IsError =
                match this with
                | Error _ -> true
                | Ok _ -> false

            member this.ErrorOr value =
                match this with
                | Ok _ -> value
                | Error err -> err

    module String =
        let ensureEndsWith (suffix : string) (str : string) =
            if str.EndsWith suffix then str
            else str + suffix

    module Info =
        let private pkgJson : obj = importDefault "../../../package.json"

        let private normalizeKebabCase (s : string) =
            s.Split('-')
            |> Array.map (fun (s : string) ->
                   s.Substring(0, 1)
                   |> (fun c -> c.ToUpper())
                   |> (fun c -> c + s.Substring(1, s.Length)))
            |> Array.reduce (fun acc elem -> acc + " " + elem)

        let version : string = pkgJson?version
        let name : string = pkgJson?name |> normalizeKebabCase
        let homepage : string = pkgJson?homepage
        let description : string = pkgJson?description
        let issues : string = pkgJson?bugs?url
        let author : string = pkgJson?author
        let license : string = pkgJson?license

    module WindowState =
        type State =
            abstract x : int
            abstract y : int
            abstract width : int
            abstract height : int
            abstract isMaximized : bool
            abstract isFullScreen : bool
            abstract manage : BrowserWindow -> unit
            abstract unmanage : unit -> unit
            abstract saveState : BrowserWindow -> unit

        [<AllowNullLiteral>]
        type Options =

            /// The height that should be returned if no file exists yet. Defaults to `600`.
            abstract defaultHeight : int option with get, set

            /// The width that should be returned if no file exists yet. Defaults to `800`.
            abstract defaultWidth : int option with get, set

            abstract fullScreen : bool option with get, set

            /// The path where the state file should be written to. Defaults to `app.getPath('userData')`.
            abstract path : string option with get, set

            /// The name of file. Defaults to `window-state.json`.
            abstract file : string option with get, set

            /// Should we automatically maximize the window, if it was last closed maximized. Defaults to `true`.
            abstract maximize : bool option with get, set

        let getState : Options -> State = importDefault "electron-window-state"

    module ElectronStore =
        type Store =
            abstract set : string * string -> unit
            abstract set : obj -> unit
            abstract get : string * ?defaultValue:string -> obj
            abstract has : string -> bool
            abstract delete : string -> unit
            abstract clear : unit
            abstract onDidChange : string * Browser.Types.Event -> unit
            abstract onDidAnyChange : Browser.Types.Event -> unit
            abstract size : int
            abstract store : obj
            abstract path : string
            abstract openInEditor : unit

        type StoreStatic =
            [<EmitConstructor>]
            abstract Create : unit -> Store

        type StoreNum =
            { Type : string
              Maximum : int
              Minimum : int
              Default : int }

        type StoreString =
            { Type : string
              Format : string }

        type Schema =
            { Test : StoreNum }

        let getStore : StoreStatic = importDefault "electron-store"

    module ElectronBridge =
        type BridgeMsg =
            | SomeMsg
            | Text of string
            | Close

        let port = "8085" |> uint16
        let endpoint = sprintf "http://localhost:%i" port
        let socketPath = "/ws"
