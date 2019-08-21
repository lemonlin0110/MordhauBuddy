﻿namespace MordhauBuddy.App.Maps

module State =
    open FSharp.Core  // To avoid shadowing Result<_,_>
    open MordhauBuddy.App
    open RenderUtils
    open RenderUtils.Validation
    open Elmish
    open Elmish.Bridge
    open MordhauBuddy.Shared.ElectronBridge
    open BridgeUtils
    open RenderUtils.Directory
    open Types
    open Electron

    let init() =
        { Waiting = true
          MapsDir = 
              { Dir = DirLoad.MapDir
                Label = ""
                Waiting = false
                Directory = ""
                Error = false
                HelperText = "" 
                Validated = false }
          Available = []
          Installed = []
          Installing = []
          TabSelected = Available
          Snack = Snackbar.State.init() }

    let private sender = new MapBridgeSender(Caller.MordhauConfig)

    let update (msg: Msg) (model: Model) =
        match msg with
        | ClientMsg bMsg ->
            match bMsg.BridgeResult with
            | BridgeResult.MapOperation mOp ->
                match mOp with
                | MapOperationResult.DirExists b ->
                    if b then
                        { model with
                            Waiting = false 
                            MapsDir =
                                { model.MapsDir with
                                    Waiting = false
                                    Error = false
                                    HelperText = "Maps directory located"
                                    Validated = true } }, Cmd.ofMsg GetAvailableMaps
                    else
                        { model with
                            Waiting = false 
                            MapsDir =
                                { model.MapsDir with
                                    Waiting = false
                                    Error = true
                                    HelperText = "Maps directory not found"
                                    Validated = false } }, Cmd.none
                | _ -> model, Cmd.none
            | _ -> { model with Waiting = false }, Cmd.none
        | TabSelected i -> model, Cmd.none
        | ImgSkeleton -> model, Cmd.none
        | InstallMap s -> model, Cmd.none
        | UninstallMap s -> model, Cmd.none
        | CancelMapInstall s -> model, Cmd.none
        | UpdateMap s -> model, Cmd.none
        | GetInstalledMaps -> model, Cmd.none
        | GetAvailableMaps -> model, Cmd.none
        | RefreshMaps -> model, Cmd.none
        | SnackMsg msg' ->
            let m, cmd, actionCmd = Snackbar.State.update msg' model.Snack
            { model with Snack = m },
            Cmd.batch [ Cmd.map SnackMsg cmd
                        actionCmd ]
        | SnackDismissMsg ->
            let cmd =
                Snackbar.State.create ""
                |> Snackbar.State.withDismissAction "OK"
                |> Snackbar.State.withTimeout 80000
                |> Snackbar.State.add
            model, Cmd.map SnackMsg cmd


