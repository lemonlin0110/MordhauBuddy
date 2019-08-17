﻿namespace MordhauBuddy.App.MordhauConfig

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
    open MordhauBuddy.Shared.ElectronBridge
    open RenderUtils.MaterialUI
    open RenderUtils.MaterialUI.Core
    open Elmish.React
    open Electron
    open Types

    let private styles (theme : ITheme) : IStyles list = [
        Styles.Custom("accordHead", [
            CSSProp.FontSize (theme.typography.pxToRem(15.))
            CSSProp.FlexBasis "33.33%"
            CSSProp.FlexShrink 0
        ])
        Styles.Custom("accordSubHead", [
            CSSProp.FontSize (theme.typography.pxToRem(15.))
            CSSProp.Color (theme.palette.text.secondary)
        ])
        Styles.Custom ("darkList", [
            CSSProp.BackgroundColor theme.palette.background.``default``
        ])
        Styles.Custom ("subExpansionPanelSummary", [
            CSSProp.Border "none"
            CSSProp.MarginTop "-5em"
        ])
    ]

    let mutableKeyValue (classes: IClasses) model dispatch (oGroup : OptionGroup) =
        let subPanelDetails (kvList : KeyValues list) =
            kvList
            |> List.mapi (fun ind s ->
                [
                    yield
                        listItem [ 
                        ] [
                            listItemText [] [
                                str s.Key
                            ]
                            formControl [
                            ] [
                                formGroup [ Style [CSSProp.FlexGrow "1"] ] [
                                    formControlLabel [
                                        FormControlLabelProp.Control <|
                                            slider [
                                                HTMLAttr.DefaultValue <| 
                                                    (if s.Value.IsSome then
                                                        s.Value.Value.ToString()
                                                    else s.Default.ToString()
                                                    |> box)
                                                HTMLAttr.Disabled (oGroup.Enabled |> not)
                                                SliderProp.ValueLabelDisplay SliderLabelDisplay.Auto
                                                SliderProp.Step <| 0.25
                                                SliderProp.Marks <| Fable.Core.Case1(true)
                                                SliderProp.Min <| s.Mutable.Value.Min.ToFloat()
                                                SliderProp.Max <| s.Mutable.Value.Max.ToFloat()
                                                SliderProp.OnChangeCommitted <| fun _ newValue -> dispatch (MoveSlider(s.Key,newValue |> string |> float))
                                                Style [ CSSProp.MinWidth "20em" ]
                                            ] []
                                    ] []
                                ]
                            ]
                        ]
                    if kvList.Length > ind + 1 then
                        yield divider []
                ])
            |> List.concat

        match oGroup.Settings |> List.filter (fun s -> s.Mutable.IsSome) with
        | [] -> None
        | kvList -> 
            div [
                Style [ CSSProp.PaddingBottom "2em" ]
            ] [
                expansionPanel [
                    ExpansionPanelProp.Expanded oGroup.Expanded
                    DOMAttr.OnChange (fun _ -> dispatch <| ExpandSubPanel(oGroup))

                    Style [ CSSProp.Border "none" ]
                ] [
                    expansionPanelSummary [
                        Class classes?subExpansionPanelSummary
                        DOMAttr.OnClick (fun ev -> ev.preventDefault())
                        ExpansionPanelSummaryProp.ExpandIcon <| expandMoreIcon []
                    ] []
                    expansionPanelDetails [
                        Style [ CSSProp.PaddingTop "1em" ]
                    ] [ 
                        grid [
                            GridProp.Container true
                            GridProp.Spacing GridSpacing.``0``
                            GridProp.Justify GridJustify.Center
                            GridProp.AlignItems GridAlignItems.Center
                            Style [ CSSProp.Width "100%" ]
                        ] [
                            card [
                                MaterialProp.Elevation 2
                                Style [
                                    CSSProp.FlexGrow "1"
                                    CSSProp.MarginLeft "1em"
                                    CSSProp.MarginRight "1em"
                                ]
                            ] [
                                list [
                                    MaterialProp.Dense true
                                    Style [ 
                                        CSSProp.MaxHeight "30em"
                                        CSSProp.Width "100%"
                                    ]
                                ] <| subPanelDetails kvList
                            ]
                        ]
                    ]
                ]
            ]
            |> Some

    let panelDetails (classes: IClasses) model dispatch (oGroups : OptionGroup list) =
        oGroups
        |> List.mapi (fun ind oGroup ->
            let subPanel = mutableKeyValue (classes: IClasses) model dispatch oGroup
            [
                yield
                    listItem [
                        Style [ 
                            CSSProp.ZIndex "1" 
                            CSSProp.PaddingRight "4em"
                        ]
                    ] [
                        listItemText [] [
                            str oGroup.Title
                        ]
                        formControl [
                            Style [ CSSProp.PaddingRight "4em" ]
                        ] [
                            formGroup [] [
                                formControlLabel [
                                    FormControlLabelProp.Control <|
                                        switch [
                                            HTMLAttr.Checked oGroup.Enabled
                                            DOMAttr.OnChange <| fun _ -> dispatch (ToggleOption(oGroup))
                                        ]
                                ] []
                            ]
                        ]
                        div [
                            Style [ CSSProp.Width "33%" ]
                        ] [ 
                            typography [
                                TypographyProp.Variant TypographyVariant.Caption
                            ] [ str oGroup.Caption ] 
                        ]
                    ]
                if subPanel.IsSome then
                    yield subPanel.Value
                if oGroups.Length > ind + 1 then
                    yield divider []
            ])
        |> List.concat

    let private expansionPanels (classes: IClasses) model dispatch =
        model.Panels
        |> List.map (fun p ->
            expansionPanel [
                ExpansionPanelProp.Expanded p.Expanded
                DOMAttr.OnChange (fun _ -> dispatch <| Expand(p))
            ] [
                expansionPanelSummary [
                    ExpansionPanelSummaryProp.ExpandIcon <| expandMoreIcon []
                ] [
                    typography [
                        Class classes?accordHead
                    ] [ str p.Panel.Header ]
                    typography [
                        Class classes?accordSubHead
                    ] [ str p.Panel.SubHeader ]
                ]
                expansionPanelDetails [] [ 
                    grid [
                        GridProp.Container true
                        GridProp.Spacing GridSpacing.``0``
                        GridProp.Justify GridJustify.Center
                        GridProp.AlignItems GridAlignItems.Center
                        Style [ CSSProp.Width "100%" ]
                    ] [
                        list [
                            MaterialProp.Dense false
                            Style [ 
                                CSSProp.OverflowY "auto"
                                CSSProp.MaxHeight "30em"
                                CSSProp.Width "100%"
                            ]
                        ] <| panelDetails classes model dispatch p.Items 
                    ]
                ]
            ] )

    //let private config (classes: IClasses) model dispatch =
    //    paper [] [
    //        div [
    //            Style [
    //                CSSProp.Padding "2em 5em"
    //                CSSProp.Display DisplayOptions.Flex
    //                CSSProp.MinHeight "76px"
    //            ]
    //        ] [
    //            textField [
    //                TextFieldProp.Variant TextFieldVariant.Outlined
    //                MaterialProp.FullWidth true
    //                HTMLAttr.Label "Mordhau Engine.ini Directory"
    //                HTMLAttr.Value model.GameUserDir.Directory
    //                MaterialProp.Error model.GameUserDir.Error
    //                TextFieldProp.HelperText (model.GameUserDir.HelperText |> str)
    //            ] []
    //            button [
    //                ButtonProp.Variant ButtonVariant.Contained
    //                MaterialProp.Color ComponentColor.Secondary
    //                DOMAttr.OnClick <| fun _ -> dispatch (RequestLoad(File.Engine))
    //                Style [
    //                    CSSProp.MarginLeft "1em" 
    //                    CSSProp.MaxHeight "4em" 
    //                ]
    //            ] [ str "Select" ]

    //        ]
    //    ]
    //config classes model dispatch ]
    let private content (classes: IClasses) model dispatch =
        [    
            card [ CardProp.Raised true ] 
                <| expansionPanels classes model dispatch
            div [ 
                Style [ CSSProp.MarginTop "2em" ] 
            ] [ ]
            div [ 
                Style [
                    CSSProp.MarginTop "auto"
                    CSSProp.MarginLeft "auto"
                ] 
            ] [
                button [
                    HTMLAttr.Disabled <| model.Submit.Complete
                    ButtonProp.Variant ButtonVariant.Contained
                    MaterialProp.Color ComponentColor.Primary
                    DOMAttr.OnClick <| fun _ -> 
                        dispatch Submit
                    Style [ 
                        CSSProp.MaxHeight "2.6em"
                        CSSProp.MarginTop "auto"
                        CSSProp.MarginLeft "auto"
                    ]
                ] [ 
                    if model.Submit.Waiting then 
                        yield circularProgress [ 
                            CircularProgressProp.Size (
                                CircularProgressSize.Case1(20))
                            Style [ CSSProp.MaxHeight "2.6em" ] 
                        ]
                    else yield str "Submit"
                ]
            ]
        ]

    let private view' (classes: IClasses) model dispatch =
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
                ] 
                    <|
                        match model.Waiting with
                        | true when model.GameUserDir.Directory = "" || model.EngineDir.Directory = "" ->
                            [ circularProgress [
                              Style [CSSProp.MarginLeft "45%"]
                              DOMAttr.OnAnimationStart <| fun _ ->
                                dispatch GetDefaultDir ] ]
                        | _ -> content classes model dispatch
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
