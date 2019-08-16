﻿namespace MordhauBuddy.App.MordhauConfig

module rec Types =
    open System
    open Fable.Core
    open FSharp.Core  /// To avoid shadowing Result<_,_>
    open MordhauBuddy.App
    open RenderUtils
    open EngineMods
    open MordhauBuddy.Shared.ElectronBridge
    open Microsoft.FSharp.Reflection

    type Msg =
        | ClientMsg of BridgeMsg
        | GetDefaultDir
        | SetConfigDir of string * Result<string,string list> * File
        | RequestLoad of File
        | LoadCanceled
        | Expand of Panel
        | ToggleOption of OptionGroup
        | Submit
        | SnackMsg of Snackbar.Types.Msg<Msg>
        | SnackDismissMsg

    type ExpansionPanels =
        | Cosmetic
        | Utilities
        | Performance
        | Quality
        member this.Header =
            this.ToString()
            |> String.duToTitle

        member this.SubHeader =
            match this with
            | Cosmetic -> "Make the game look different"
            | Utilities -> "Settings to gather information"
            | Performance -> "Improve game performance" 
            | Quality -> "Improve game quality"

        member this.Modifications =
            match this with
            | Cosmetic -> cosmetics
            | Utilities -> utilities
            | Performance -> performance 
            | Quality -> quality

        static member private Cases =
            FSharpType.GetUnionCases typeof<ExpansionPanels>

        static member private Instantiate name =
            ExpansionPanels.Cases
            |> Array.tryFind (fun uc -> uc.Name = name)
            |> Option.map (fun uc -> 
                Reflection.FSharpValue.MakeUnion( uc, [||] ) :?> ExpansionPanels)
            |> Option.get

        static member GetPanels =
            ExpansionPanels.Cases
            |> Array.map (fun uc ->
                uc.Name |> ExpansionPanels.Instantiate)

        static member Init() : Panel list =
            ExpansionPanels.GetPanels
            |> Array.map (fun p ->
                { Panel = p
                  Expanded = false
                  Items =
                    (ExpansionPanels.Instantiate p.Header).Modifications })
            |> List.ofArray

        member this.GetTag =
            ExpansionPanels.Cases
            |> Seq.tryFind (fun uc -> uc.Name = this.ToString())
            |> Option.map (fun uc -> uc.Tag)
            |> Option.get

    type Panel =
        { Panel : ExpansionPanels
          Expanded : bool
          Items : OptionGroup list }

    type ConfigDir =
        { Waiting : bool  
          Directory : string
          Error : bool
          HelperText : string
          Validated : bool }

    type Submit =
        { Waiting : bool
          Error : bool
          HelperText : string
          Complete : bool }

    type Model = 
        { Waiting : bool
          Complete : bool
          Panels : Panel list
          EngineDir : ConfigDir
          GameUserDir : ConfigDir
          Submit : Submit
          Snack : Snackbar.Types.Model<Msg> }
