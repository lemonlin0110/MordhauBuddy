namespace MordhauBuddy.Core

open Helpers
open INIReader
open INIReader.INIExtensions.Options
open MordhauBuddy.Shared.ElectronBridge

/// Operations on Mordhau Game ini
module INIConfiguration =
    /// Module for modifying character face values
    module Frankenstein =

        let logger = Logger "INIConfiguration.Frakenstein"

        [<RequireQualifiedAccess>]
        type FaceActions =
            | Frankenstein
            | Random
            | Custom of string

        [<AutoOpen>]
        module private Internals =
            let max = 65535
            let min = 0
            let rng = System.Random()

            let faceKeys =
                [ [ "CharacterProfiles"; "FaceCustomization"; "Translate" ]
                  [ "CharacterProfiles"; "FaceCustomization"; "Rotate" ]
                  [ "CharacterProfiles"; "FaceCustomization"; "Scale" ] ]

            /// Generate random values via provided min, max, and mapper function over 49 space `int list`
            let genFaces (mapper: int -> int) (min: int) (max: int) =
                [ 0 .. 48 ]
                |> List.map (fun _ ->
                    rng.Next(min, max)
                    |> mapper
                    |> string
                    |> Some
                    |> INIValue.String)
                |> INIValue.Tuple

            /// Generate random values and map over `int list`
            let randomFaces() = genFaces id min 65536

            /// Randomly assign max or min values over `int list`
            let frankensteinFaces() =
                genFaces (fun i ->
                    if i = 1 then max else min) min 2

            /// Collects all properties matching the input string
            let mapProps (s: string) (iList: INIValue list) =
                iList |> List.collect (fun iVal -> iVal.TryGetProperty s |> Option.defaultValue [])

            /// Modifies the FaceCustomization of the given profile by folding over the `faceKeys`
            /// and applying the given `unit -> INIValue` function
            let modifyFace (profile: INIValue) (f: unit -> INIValue) =
                faceKeys |> List.fold (fun (p: INIValue) sels -> p.Map(sels, f())) profile

            /// Modifies the FaceCustomization of the given profile by mapping the given
            /// FaceCustomization `INIValue`
            let modifyWholeFace (profile: INIValue) (newFace: INIValue) =
                profile.Map([ "CharacterProfiles"; "FaceCustomization" ], newFace)

            /// Get the properties that match the selectors within the input game file
            let getPropValuesOf (gameFile: INIValue) (selectors: string list) =
                selectors
                |> List.fold (fun (acc: INIValue list list) elem ->
                    acc
                    |> List.map (fun iList ->
                        iList
                        |> List.collect (fun iVal ->
                            match iVal.TryGetProperty(elem) with
                            | Some(res) -> res
                            | _ -> []))) [ [ gameFile ] ]
                |> List.concat

            /// Strip " characters from input INIValue `INIValue.String` items
            let iValStrings (iVal: INIValue) =
                match iVal with
                | INIValue.Tuple(tList) when tList.Length = 1 ->
                    match tList.Head with
                    | INIValue.String(Some(s)) -> s.Trim('"')
                    | _ -> ""
                | INIValue.String(Some(s)) -> s.Trim('"')
                | _ -> ""

        /// Get all character profile names within a game file
        let getCharacterProfileNames (gameFile: INIValue) =
            [ "File"
              @"/Game/Mordhau/Blueprints/BP_MordhauSingleton.BP_MordhauSingleton_C"
              "CharacterProfiles"
              "Name"
              "INVTEXT" ]
            |> getPropValuesOf gameFile
            |> List.map iValStrings

        /// Get all character profile names and export strings within a game file
        let getCharacterProfileExports (gameFile: INIValue) (profiles: string list) =
            let characterIVals =
                [ "File"; @"/Game/Mordhau/Blueprints/BP_MordhauSingleton.BP_MordhauSingleton_C" ]
                |> getPropValuesOf gameFile

            let checkVList (vList: INIValue list) (profile: string) =
                vList
                |> List.tryFind (fun iVal ->
                    match iVal with
                    | INIValue.KeyValue("Name", fText) ->
                        match fText with
                        | INIValue.FieldText(_, INIValue.Tuple(tList)) when profile = (tList.Head.ToString().Trim('"')) ->
                            true
                        | _ -> false
                    | _ -> false)
                |> Option.isSome
            profiles
            |> List.map
                ((fun profile ->
                    let exportList =
                        characterIVals
                        |> List.choose (fun iElem ->
                            match iElem with
                            | INIValue.KeyValue("CharacterProfiles", INIValue.Tuple(vList)) when checkVList vList
                                                                                                     profile ->
                                Some(iElem)
                            | _ -> None)
                    match exportList with
                    | [ charIVal ] -> profile, Some(charIVal)
                    | _ -> profile, Some(INIValue.String(None)))
                 >> (fun (pName, iValOpt) ->
                     match iValOpt with
                     | Some(iVal) ->
                         match iVal with
                         | INIValue.KeyValue("CharacterProfiles", INIValue.Tuple(tList)) ->
                             tList
                             |> List.choose (fun tElem ->
                                 match tElem with
                                 | INIValue.KeyValue("FaceCustomization", fVal) -> Some(pName, fVal.ToString())
                                 | _ -> None)
                             |> function
                             | [ single ] -> single
                             | _ -> (pName, "")
                         | _ -> (pName, "")
                     | None -> (pName, "")))

        /// Set the profile's face customization by applying the given `FaceActions`,
        /// then merge the result into the game file
        let setCharacterProfileFace (gameFile: INIValue) (profile: string) (action: FaceActions) =
            let newFace (iVal: INIValue) =
                match action with
                | FaceActions.Frankenstein -> modifyFace iVal frankensteinFaces
                | FaceActions.Random -> modifyFace iVal randomFaces
                | FaceActions.Custom(fValues) ->
                    match INIValue.TryParseSnippet(fValues) with
                    | Some(faces) -> modifyWholeFace iVal faces
                    | _ -> iVal

            let charProfiles, otherItems =
                [ gameFile ]
                |> mapProps "File"
                |> mapProps @"/Game/Mordhau/Blueprints/BP_MordhauSingleton.BP_MordhauSingleton_C"
                |> List.partition (fun iVal -> iVal.TryGetProperty("CharacterProfiles").IsSome)

            charProfiles
            |> List.map (fun iVal ->
                match iVal.TryGetProperty("CharacterProfiles") with
                | Some(iList) -> iList
                | None -> [])
            |> List.filter (List.isEmpty >> not)
            |> List.map
                ((fun iList -> INIValue.KeyValue("CharacterProfiles", INIValue.Tuple(iList)))
                 >> (fun iVal ->
                     match iVal?CharacterProfiles?Name?INVTEXT.AsString() with
                     | Some(s) when s.Trim('"') = profile -> newFace iVal
                     | _ -> iVal))
            |> fun cProfs ->
                let h, t = otherItems |> List.splitAt 1
                List.append cProfs t |> List.append h
            |> fun iList ->
                INIValue.Section(@"/Game/Mordhau/Blueprints/BP_MordhauSingleton.BP_MordhauSingleton_C", iList)
            |> fun section ->
                gameFile.Map([ @"/Game/Mordhau/Blueprints/BP_MordhauSingleton.BP_MordhauSingleton_C" ], section)

        // Attempts to set character profile face values and returns `Option`
        let tryApplyChanges (profiles: string list) (iVal: INIValue) (fAction: FaceActions) =
            try
                profiles
                |> List.fold (fun acc profile -> setCharacterProfileFace acc profile fAction) iVal
                |> Some
            with e ->
                logger.LogError "Failed to apply changes of:\n\tProfiles: %O\n\tINIValue:\n\t%O\n\tAction: %O\n%O"
                    profiles iVal fAction e
                None

    /// Module for modifying Game and GameUserSettings ini files
    module MordhauConfig =

        let logger = Logger "INIConfiguration.MordhauConfig"

        /// Try to cast an INIValue to KeyValue primative
        let castKV (def: KeyValues.Values) (iVal: INIValue option) =
            match def with
            | KeyValues.Values.Float(_) -> INIValueOptionExtensions.AsFloat(iVal) |> Option.map KeyValues.Values.Float
            | KeyValues.Values.Bool(_) -> INIValueOptionExtensions.AsBoolean(iVal) |> Option.map KeyValues.Values.Bool
            | KeyValues.Values.Int(_) -> INIValueOptionExtensions.AsInteger(iVal) |> Option.map KeyValues.Values.Int
            | KeyValues.Values.String(_) ->
                INIValueOptionExtensions.AsString(iVal) |> Option.map KeyValues.Values.String

        /// Map the group settings if they're present in the given `INIValue list`
        let mapGroupSettings (props: INIValue list) (kvList: KeyValues list) =
            kvList
            |> List.map (fun kv ->
                { kv with
                      Value =
                          props
                          |> List.choose (fun p -> p.TryGetProperty(kv.Key))
                          |> List.concat
                          |> List.tryHead
                          |> castKV (kv.Default) })

        /// Map the option groups via `mapGroupSettings`
        let mapOptGroup (oGroup: OptionGroup) (props: INIValue list) =
            { oGroup with Settings = oGroup.Settings |> mapGroupSettings props }

        /// Get current settings of the `OptionGroup list` if present in the configuration files
        let getSettings (engineFile: INIValue) (gameUserFile: INIValue) (inputFile: INIValue) (options: OptionGroup list) =
            let engineSettings =
                engineFile.TryGetProperty("File")
                |> Option.map (
                    List.choose (fun iVal -> iVal.TryGetProperty("SystemSettings"))
                    >> List.concat
                )

            let gameUserSettings =
                gameUserFile.TryGetProperty("File")
                |> Option.map (
                    List.choose (fun iVal -> iVal.TryGetProperty(@"/Script/Mordhau.MordhauGameUserSettings"))
                    >> List.concat
                )

            let inputSettings =
                inputFile.TryGetProperty("File")
                |> Option.map (
                    List.choose (fun iVal -> iVal.TryGetProperty(@"/Script/Engine.InputSettings"))
                    >> List.concat
                )

            options
            |> List.map (fun oGroup ->
                match oGroup.File with
                | ConfigFile.Engine -> engineSettings |> Option.map (mapOptGroup oGroup)
                | ConfigFile.GameUserSettings -> gameUserSettings |> Option.map (mapOptGroup oGroup)
                | ConfigFile.Input -> inputSettings |> Option.map (mapOptGroup oGroup)
                | _ -> None
                |> function
                | Some newOGroup -> newOGroup
                | None -> oGroup)

        /// Filter the `OptionGroup list` based on `File` given
        let filterFile (options: OptionGroup list) (file: ConfigFile) =
            options |> List.filter (fun option -> option.File = file)

        /// Maps the option value if present to the `INIValue` or adds it
        let mapOptionToINIValue (iVal: INIValue) (setting: KeyValues) (selectors: string list) =
            let propMapper (action: INIValue list -> INIValue list) =
                iVal.TryGetProperty("File")
                |> Option.map (fun iList ->
                    iList
                    |> List.tryPick (fun i -> selectors |> List.tryHead |> Option.bind (i.TryGetProperty))
                    |> function
                    | Some resList -> resList
                    | None -> []
                    |> action)
                |> Option.map (fun res ->
                    INIValue.Section(selectors.Head, res) |> fun nVal -> iVal.Map([ selectors.Head ], nVal))

            let isPropAdded =
                let res =
                    iVal.TryGetProperty("File")
                    |> Option.map (fun iList ->
                        iList
                        |> List.choose (fun i -> i.TryGetProperty(selectors.Head))
                        |> List.concat)
                    |> Option.map (fun iList ->
                        iList
                        |> List.choose (fun i -> i.TryGetProperty(selectors.Tail.Head))
                        |> List.concat)
                match res with
                | Some(i) when i.IsEmpty |> not -> true
                | _ -> false

            let iStr =
                setting.Value
                |> Option.map string
                |> INIValue.String

            match isPropAdded, setting.Value.IsNone with
            | _, true ->
                List.filter (fun (iVal: INIValue) ->
                    iVal.Properties
                    |> fst
                    <> setting.Key) 
                |> propMapper
            | false, _ ->
                List.append
                    [ INIValue.KeyValue(setting.Key, INIValue.String(setting.Value |> Option.map string)) ]
                |> propMapper
            | _ -> iVal.Map(selectors, iStr) |> Some
            |> function
            | Some res -> res
            | None -> iVal

        /// Applies the `OptionGroup list` `Settings` to the given `INIValue` based on the selectors
        let mapOptions (iFile: INIValue) (selectors: string list) (options: OptionGroup list) =
            (iFile, options)
            ||> List.fold (fun acc elem ->
                (acc, elem.Settings)
                ||> List.fold (fun subElem setting ->
                    List.append selectors [ setting.Key ] |> mapOptionToINIValue subElem setting))

        /// Map the `OptionGroup list` values to the Engine.ini, GameUserSettings.ini, and Input.ini if present
        let tryMapSettings (engineFile: INIValue) (gameUserFile: INIValue) (inputFile: INIValue) (options: OptionGroup list) =
            try
                let withGetOrAddSelector (original: INIValue) (selector: string) =
                    original.TryGetProperty("File")
                    |> Option.map (fun iVals ->
                        iVals
                        |> List.tryFind (fun iVal -> iVal.Properties |> fst |> (=) selector)
                        |> function
                        | None -> iVals @ [ INIValue.Section(selector,[]) ]
                        | Some _ -> iVals)
                    |> function
                    | Some eFile -> INIValue.File eFile
                    | None -> original

                let purgeEmptySelector (selector: string) (result: INIValue) =
                    result.TryGetProperty("File")
                    |> Option.map (fun resList -> 
                        resList 
                        |> List.filter (fun section -> 
                            (fst section.Properties = selector &&
                             section.Properties |> snd |> List.isEmpty) 
                            |> not)
                        |> INIValue.File)

                let engine =
                    let selector = "SystemSettings"

                    ConfigFile.Engine
                    |> filterFile options
                    |> mapOptions (withGetOrAddSelector engineFile selector) [ selector ]
                    |> purgeEmptySelector selector

                let gameUser =
                    let selector = @"/Script/Mordhau.MordhauGameUserSettings"

                    ConfigFile.GameUserSettings
                    |> filterFile options
                    |> mapOptions (withGetOrAddSelector gameUserFile selector) [ selector ]
                    |> purgeEmptySelector selector

                let input =
                    let selector = @"/Script/Engine.InputSettings"

                    ConfigFile.Input
                    |> filterFile options
                    |> mapOptions (withGetOrAddSelector inputFile selector) [ selector ]
                    |> purgeEmptySelector selector
                        

                engine, gameUser, input
            with e ->
                logger.LogError
                    "Failed to map settings of:\n\tEngine file:\n\t%O\n\tGame user file:\n\t%O\n\tInput file:\n\t%O\n\tOptions:\n\t%O\n%O"
                    engineFile gameUserFile inputFile options e
                None, None, None
