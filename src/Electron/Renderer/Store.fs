namespace MordhauBuddy.App

module Store =
    open Fable.Core
    open Fable.Core.JsInterop
    open Elmish
    open BridgeUtils
    open RenderUtils
    open MordhauBuddy.Shared.ElectronBridge

    type Model =
        { AutoLaunch: bool
          AutoLaunchSet: bool
          DarkTheme: bool
          GameLocation: string option
          EngineLocation: string option
          GameUserLocation: string option
          ModsLocation: string option
          InputLocation: string option
          UpdateSettings: string
          BackupSettings: string }

    type Msg =
        | AutoLaunch of bool
        | AutoLaunchSet of bool
        | ToggleDarkTheme
        | SetGameLocation of string
        | SetEngineLocation of string
        | SetGameUserLocation of string
        | SetInputLocation of string
        | SetModsLocation of string
        | SetUpdateSettings of UpdateSettings
        | SetBackupSettings of BackupSettings

    /// Bindings for electron-store
    ///
    /// See their [repo](https://github.com/sindresorhus/electron-store)
    /// for more information
    module private ElectronStore =
        type Options =
            /// Default values for the store items.
            abstract defaults: obj with get, set
            /// JSON Schema to validate your config data.
            abstract schema: obj with get, set
            /// Name of the storage file (without extension).
            abstract name: string with get, set
            /// Storage file location.
            ///
            /// Don't specify this unless absolutely necessary!
            abstract cwd: string with get, set
            /// This can be used to secure sensitive data if the encryption
            /// key is stored in a secure manner (not plain-text) in the
            /// Node.js app. For example, by using `node-keytar` to store the
            /// encryption key securely, or asking the encryption key from
            /// the user (a password) and then storing it in a variable.
            abstract encryptionKey: bool with get, set
            /// Extension of the config file.
            abstract fileExtention: bool with get, set
            /// The config is cleared if reading the config file causes a `SyntaxError`.
            abstract clearInvalidConfig: bool with get, set
            /// Function to serialize the config object to a UTF-8 string when writing the config file.
            abstract serialize: obj -> string with get, set
            /// Function to deserialize the config object from a UTF-8 string when reading the config file.
            abstract deserialize: string -> obj with get, set
            /// Accessing nested properties by dot notation.
            abstract accessPropertiesByDotNotation: bool with get, set

        type Store =
            /// Set an item.
            abstract set: string * string -> unit
            /// Set multiple items at once.
            abstract set: obj -> unit
            /// Get an item or `defaultValue` if the item does not exist.
            abstract get: string * ?defaultValue:string -> obj
            /// Check if an item exists.
            abstract has: string -> bool
            /// Delete an item.
            abstract delete: string -> unit
            /// Delete all items.
            abstract clear: unit -> unit
            /// Watches the given key, calling callback on any changes.
            abstract onDidChange: string * (obj * obj -> unit) -> unit
            /// Watches the whole config object, calling callback on any changes.
            abstract onDidAnyChange: string * (obj * obj -> unit) -> unit
            /// Get the item count.
            abstract size: int
            /// Get all the data as an object or replace the current data with an object.
            abstract store: Model with get, set
            /// Get the path to the storage file.
            abstract path: string
            /// Open the storage file in the user's editor.
            abstract openInEditor: unit -> unit

        type StoreStatic =

            [<EmitConstructor>]
            abstract Create: unit -> Store

            [<EmitConstructor>]
            abstract Create: Options -> Store

        let private getStore: StoreStatic = importDefault "electron-store"

        let private defaults =
            {| AutoLaunch = true
               AutoLaunchSet = false
               DarkTheme = true
               GameLocation = None
               EngineLocation = None
               GameUserLocation = None
               InputLocation = None
               ModsLocation = None
               BackupSettings = "KeepLast10"
               UpdateSettings = "Installed" |} |> toPlainJsObj

        /// Create store object
        let store = getStore.Create(jsOptions<Options> (fun o -> o.defaults <- defaults))

    let init() = ElectronStore.store.store

    let private set (m: Model) =
        ElectronStore.store.set m
        m

    let update msg m =
        match msg with
        | AutoLaunch b -> set { m with AutoLaunch = b }
        | AutoLaunchSet b -> set { m with AutoLaunchSet = b }
        | ToggleDarkTheme -> set { m with DarkTheme = (m.DarkTheme |> not) }
        | SetGameLocation s -> set { m with GameLocation = Some(s) }
        | SetEngineLocation s -> set { m with EngineLocation = Some(s) }
        | SetGameUserLocation s -> set { m with GameUserLocation = Some(s) }
        | SetInputLocation s -> set { m with InputLocation = Some(s) }
        | SetModsLocation s -> set { m with ModsLocation = Some(s) }
        | SetUpdateSettings uSet -> set { m with UpdateSettings = uSet.ToString() }
        | SetBackupSettings bSet -> set { m with BackupSettings = bSet.ToString() }
