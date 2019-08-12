﻿namespace MordhauBuddy.App.FaceTools

module View =
    open Fable.Core.JsInterop
    open Fable.React
    open Fable.React.Props
    open Fable.MaterialUI
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.MaterialDesignIcons
    open Fable.MaterialUI.Icons
    open FSharp.Core  /// To avoid shadowing Result<_,_>
    open MordhauBuddy.App
    open RenderUtils
    open RenderUtils.Validation
    open Elmish.React
    open Electron
    open Types

    let private styles (theme : ITheme) : IStyles list = [
        Styles.Custom ("darkList", [
            CSSProp.BackgroundColor theme.palette.background.``default``
        ])
    ]

    let private content (classes: IClasses) model dispatch =
        match model.Stepper with
        | LocateConfig ->
            paper [] [
                div [
                    Style [
                        CSSProp.Padding "2em 5em"
                        CSSProp.Display DisplayOptions.Flex
                        CSSProp.MinHeight "76px"
                    ]
                ] [
                    textField [
                        TextFieldProp.Variant TextFieldVariant.Outlined
                        MaterialProp.FullWidth true
                        HTMLAttr.Label "Mordhau Config Directory"
                        HTMLAttr.Value model.ConfigDir.Directory
                        MaterialProp.Error model.ConfigDir.Error
                        TextFieldProp.HelperText (model.ConfigDir.HelperText |> str)
                    ] []
                    button [
                        ButtonProp.Variant ButtonVariant.Contained
                        MaterialProp.Color ComponentColor.Secondary
                        DOMAttr.OnClick <| fun _ -> dispatch RequestLoad
                        Style [
                            CSSProp.MarginLeft "1em" 
                            CSSProp.MaxHeight "4em" 
                        ]
                    ] [ str "Select" ]

                ]
            ]
        | ChooseProfiles ->
            let createCard (direction: ToggleDirection) =
                let checkNum,profiles,headerTitle =
                    match direction with
                    | Left ->
                        model.TransferList.LeftChecked,
                        model.TransferList.LeftProfiles,
                        "Profile List"
                    | Right ->
                        model.TransferList.RightChecked,
                        model.TransferList.RightProfiles,
                        "Modification List"
                let pLen = profiles.Length
                grid [ 
                    GridProp.Item true
                    Style [ CSSProp.Padding "0 2em" ]
                ] [
                    card [
                        Style [ CSSProp.MinWidth "20em" ]
                    ] [
                        cardHeader [
                            checkbox [
                                DOMAttr.OnClick <| fun _ ->
                                    (direction, checkNum = 0 && checkNum <> pLen) |> ToggleAll |> dispatch
                                HTMLAttr.Checked (checkNum = pLen && pLen <> 0)
                                CheckboxProp.Indeterminate (checkNum <> pLen && checkNum > 0)
                                HTMLAttr.Disabled (pLen = 0)
                            ]
                            |> CardHeaderProp.Avatar
                            CardHeaderProp.Title <| str headerTitle
                            CardHeaderProp.Subheader (sprintf "%i/%i" checkNum pLen |> str)
                        ] []
                        divider []
                        list [
                            MaterialProp.Dense true
                            Style [ 
                                CSSProp.OverflowY "scroll"
                                CSSProp.Height "20em"
                            ]
                        ] [
                            for pItem in profiles do
                                yield
                                    listItem [
                                        ListItemProp.Button true
                                        MaterialProp.DisableRipple true
                                        DOMAttr.OnClick <| fun _ -> dispatch (Toggle(direction,pItem))
                                    ] [
                                        listItemIcon [] [
                                            checkbox [
                                                HTMLAttr.Checked pItem.Checked
                                                MaterialProp.DisableRipple true
                                                Style [ CSSProp.BackgroundColor "transparent"]
                                            ]
                                        ]
                                        listItemText [] [
                                            str pItem.Name
                                        ]
                                    ]
                        ]
                    ]
                ]

            grid [
                GridProp.Container true
                GridProp.Spacing GridSpacing.``0``
                GridProp.Justify GridJustify.Center
                GridProp.AlignItems GridAlignItems.Center
                Style [ CSSProp.Width "unset" ]
            ] [
                createCard Left
                grid [ 
                    GridProp.Container true
                    GridProp.Direction GridDirection.Column
                    GridProp.AlignItems GridAlignItems.Center
                    Style [ CSSProp.Width "unset" ]
                ] [
                    button [
                        ButtonProp.Variant ButtonVariant.Outlined
                        ButtonProp.Size ButtonSize.Small
                        DOMAttr.OnClick <| fun _ -> dispatch (Move(Right))
                        HTMLAttr.Disabled (model.TransferList.LeftChecked = 0)
                    ] [ str ">" ]
                    button [
                        ButtonProp.Variant ButtonVariant.Outlined
                        ButtonProp.Size ButtonSize.Small
                        DOMAttr.OnClick <| fun _ -> dispatch (Move(Left))
                        HTMLAttr.Disabled (model.TransferList.RightChecked = 0)
                    ] [ str "<" ]
                ]
                createCard Right
            ]
        | ChooseAction ->
            let tabDescription =
                match model.TabSelected with
                | 0 ->
                    str "Frankenstein mode is done automatically \
                        and will select at random the maximum or \
                        minimum allowed values for each aspect of \
                        the character's face."
                | 1 -> 
                    str "Random mode is done automatically and \
                        will select at random all aspects of the \
                        character's face." 
                | 2 -> 
                    str "Import mode enables you to set faces from a face value string."
                | _ ->
                    str "Export lets you generate string values of the profiles you've selected."

            let tabContent =
                match model.TabSelected with
                | 0 ->
                    grid [
                        GridProp.Container true
                        GridProp.Spacing GridSpacing.``0``
                        GridProp.Direction GridDirection.Column
                        GridProp.AlignItems GridAlignItems.Center
                        Style [ 
                            CSSProp.Padding "1em 5em"
                            CSSProp.Display DisplayOptions.Flex
                        ] 
                    ] [
                        img [
                            HTMLAttr.Src (stat "frankenstein.png")
                            Style [ CSSProp.BorderRadius "4px" ]
                        ]
                    ]
                | 2 ->
                    div [] [
                        div [ 
                            Style [ 
                                CSSProp.Padding "2em 5em"
                                CSSProp.Display DisplayOptions.Flex
                            ] 
                        ] [
                            textField [
                                TextFieldProp.Variant TextFieldVariant.Outlined
                                MaterialProp.FullWidth true
                                HTMLAttr.Label "Import string"
                                HTMLAttr.Placeholder "Paste import string here"
                                HTMLAttr.Value model.Import.ImportString
                                MaterialProp.Color ComponentColor.Secondary
                                DOMAttr.OnChange (fun ev -> dispatch <| SetImportString(ev.Value) )
                                MaterialProp.Error model.Import.Error
                                TextFieldProp.HelperText (model.Import.HelperText |> str)
                            ] []
                            button [
                                HTMLAttr.Disabled model.Import.Validated
                                ButtonProp.Variant ButtonVariant.Contained
                                MaterialProp.Color ComponentColor.Secondary
                                DOMAttr.OnClick <| fun _ -> dispatch ValidateImport
                                Style [ CSSProp.MarginLeft "1em"; CSSProp.MaxHeight "4em" ]
                            ] [ str "Validate" ]
                        ]
                        div [ Style [ CSSProp.Padding "0em 5em" ] ] [
                            expansionPanel [
                                MaterialProp.Elevation 2
                            ] [
                                expansionPanelSummary [
                                    ExpansionPanelSummaryProp.ExpandIcon <| expandMoreIcon []
                                ] [ str "An example of an import string" ]
                                divider []
                                expansionPanelDetails [
                                    Style [ CSSProp.PaddingRight "0em" ]
                                ] [
                                    code [
                                        Style [ 
                                            CSSProp.WordBreak "break-all"
                                        ]
                                    ] [
                                        str Samples.faceImport
                                    ]
                                    button [
                                        DOMAttr.OnClick <| fun _ ->
                                            renderer.clipboard.writeText(Samples.faceImport)
                                            dispatch CopiedClipboard
                                        Style [ 
                                            CSSProp.MaxHeight "4em"
                                            CSSProp.MaxWidth "3em"
                                            CSSProp.Float "right"
                                            CSSProp.MarginRight "0em"
                                        ]
                                    ] [ clipboardTextIcon [] ]

                                ]
                            ]
                        ]
                    ]
                | 3 ->
                    let profileInd =
                        model.TransferList.RightProfiles
                        |> List.indexed
                        |> Seq.ofList
                    div [ ] [
                        div [ 
                            Style [ 
                                CSSProp.Padding "2em 5em"
                                CSSProp.Display DisplayOptions.Flex
                            ] 
                        ] [ 
                            list [
                                Class classes?darkList
                                Style [ 
                                    CSSProp.FlexGrow 1 
                                    CSSProp.MaxHeight "20em"
                                    CSSProp.OverflowY "scroll"
                                ]
                            ] [
                                for (index,profile) in profileInd do
                                    yield
                                        div [] [
                                            listItem [] [
                                                listItemText [] [
                                                    str profile.Name
                                                ]
                                                listItemSecondaryAction [] [
                                                    button [
                                                        DOMAttr.OnClick <| fun _ ->
                                                            renderer.clipboard.writeText(profile.Export)
                                                            dispatch CopiedClipboard
                                                        Style [ 
                                                            CSSProp.MaxHeight "4em"
                                                            CSSProp.MaxWidth "3em"
                                                            CSSProp.Float "right"
                                                            CSSProp.MarginRight "0em"
                                                        ]
                                                    ] [ clipboardTextIcon [] ]
                                                ]
                                            ]
                                            divider [
                                                HTMLAttr.Hidden (index + 1 = model.TransferList.RightProfiles.Length)
                                            ]
                                        ]
                            ]
                        ]
                    ]
                | _ -> div [] []

            paper [] [
                tabs [
                    HTMLAttr.Value (model.TabSelected)
                    TabsProp.Variant TabsVariant.FullWidth
                    TabsProp.ScrollButtons ScrollButtonsType.On
                    TabsProp.IndicatorColor TabsIndicatorColor.Secondary
                    TabsProp.TextColor TabsTextColor.Secondary
                    TabsProp.Centered true
                    TabsProp.OnChange (fun _ tabPicked -> dispatch <| TabSelected(tabPicked) )
                ] [
                    tab [ HTMLAttr.Label "Frankenstein" ]
                    tab [ HTMLAttr.Label "Random" ]
                    tab [ HTMLAttr.Label "Import" ]
                    tab [ HTMLAttr.Label "Export" ]
                ]
                divider []
                div [
                    Style [ CSSProp.Padding "2em"; CSSProp.MinHeight "10em" ]
                ] [ 
                    typography [] [ tabDescription ]
                    tabContent
                ]
            ]


    let private view' (classes: IClasses) model dispatch =
        let isLocateConfig =
            match model.Stepper with
            | LocateConfig -> true
            | _ -> false
        div [
            Style [
                CSSProp.FlexDirection "column"
                CSSProp.Display DisplayOptions.Flex
                CSSProp.Height "inherit"
            ]
        ] [
            yield lazyView2 Snackbar.View.view model.Snack (SnackMsg >> dispatch)
            yield
                div [
                    Style [
                        CSSProp.FlexDirection "column"
                        CSSProp.Display DisplayOptions.Flex
                        CSSProp.Height "inherit"
                    ]
                ] [
                    stepper [
                        MaterialProp.Elevation 1
                        StepperProp.ActiveStep (model.Stepper.StepValue)
                    ]
                        <| model.Stepper.StepElems model.StepperComplete
            
                    div [ Style [ CSSProp.Padding (string "3em") ] ] [ 
                        match model.Waiting, model.ParseWaiting with
                        | true, false when model.ConfigDir.Directory = "" && isLocateConfig ->
                            yield circularProgress [
                                Style [CSSProp.MarginLeft "45%"]
                                DOMAttr.OnAnimationStart <| fun _ ->
                                    async {
                                        do! Async.Sleep 1000
                                        return dispatch GetDefaultDir
                                    } |> Async.StartImmediate
                            ]
                        | true, false when isLocateConfig ->
                            yield circularProgress [
                                Style [CSSProp.MarginLeft "45%"]
                                DOMAttr.OnAnimationStart <| fun _ ->
                                    async {
                                        do! Async.Sleep 1000
                                        return dispatch <| SetConfigDir
                                            (model.ConfigDir.Directory, validateConfigDir model.ConfigDir.Directory)
                                    } |> Async.StartImmediate
                            ]
                        | _ when model.Waiting || model.ParseWaiting ->
                            yield circularProgress [
                                Style [CSSProp.MarginLeft "45%"]
                            ]
                        | _ -> yield content classes model dispatch
                    ]
                    div [ Style [CSSProp.PaddingLeft "3em"] ] model.Stepper.StepCaption
                    model.Stepper.Buttons dispatch model
                ]
            ]

    /// Workaround for using JSS with Elmish
    /// https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : (Msg -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() = ReactElementType.create viewWithStyles this.props []
            
    let view (model : Model) (dispatch : Msg -> unit) : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
