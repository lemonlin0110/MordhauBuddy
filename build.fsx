// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------
#nowarn "0213"
#r "paket: groupref FakeBuild //"
#load "./tools/FSharpLint.fs"
#load "./tools/ElectronTools.fs"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.JavaScript
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools
open Tools.Linting
open Tools.Electron
open System
open System.IO

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "MordhauBuddy"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Compilation of Mordhau Tools"

// Author(s) of the project
let author = "Shmew"

// File system information
let solutionFile  = "MordhauBuddy.sln"

// Default target configuration
let configuration = "Release"

// Pattern specifying assemblies to be tested using Expecto
let testAssemblies = "tests/**/bin" </> configuration </> "**" </> "*Tests.exe"

// Build docs website root
let website = "/MordhauBuddy"

// Github repository
let repo = @"https://github.com/Shmew/MordhauBuddy"

// List project directories to avoid formatting
// Typically projects that have very next lists for web building
let excludeFormatting = [ (__SOURCE_DIRECTORY__ @@ "src/Electron/Renderer/**")]

// Web or JS related fs projects
// Projects that have bindings to other languages where name linting needs to be more relaxed.
let relaxedNameLinting = [ (__SOURCE_DIRECTORY__ @@ "src/Electron/**/*.fs") ]

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = ReleaseNotes.load (__SOURCE_DIRECTORY__ @@ "RELEASE_NOTES.md")

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

let toolGlob   = __SOURCE_DIRECTORY__  @@ "tools"
let srcGlob    = __SOURCE_DIRECTORY__  @@ "src/**/*.??proj"
let testGlob   = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"
let fsSrcGlob  = __SOURCE_DIRECTORY__  @@ "src/**/*.fs"
let fsTestGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.fs"
let bin        = __SOURCE_DIRECTORY__ @@ "bin"
let fsProjGlob =
    !! (__SOURCE_DIRECTORY__  @@ "src/**/*.fsproj")
    ++ (__SOURCE_DIRECTORY__  @@ "tests/**/*.fsproj")

let foldExcludeGlobs (g: IGlobbingPattern) (d: string) = g -- d
let foldIncludeGlobs (g: IGlobbingPattern) (d: string) = g ++ d

let fsSrcAndTest =
    !! fsSrcGlob
    ++ fsTestGlob
    -- (__SOURCE_DIRECTORY__  @@ "src/**/obj/**")
    -- (__SOURCE_DIRECTORY__  @@ "tests/**/obj/**")
    -- (__SOURCE_DIRECTORY__  @@ "src/**/AssemblyInfo.*")
    -- (__SOURCE_DIRECTORY__  @@ "src/**/**/AssemblyInfo.*")

let fsRelaxedNameLinting =
    let baseGlob s =
        !! s
        -- (__SOURCE_DIRECTORY__  @@ "src/**/AssemblyInfo.*")
        -- (__SOURCE_DIRECTORY__  @@ "src/**/obj/**")
        -- (__SOURCE_DIRECTORY__  @@ "tests/**/obj/**")
    match relaxedNameLinting with
    | [h] when relaxedNameLinting.Length = 1 -> baseGlob h |> Some
    | h::t -> List.fold foldIncludeGlobs (baseGlob h) t |> Some
    | _ -> None

let failOnBadExitAndPrint (p : ProcessResult) =
    if p.ExitCode <> 0 then
        p.Errors |> Seq.iter Trace.traceError
        failwithf "failed with exitcode %d" p.ExitCode

module dotnet =
    let tool optionConfig command args =
        DotNet.exec (fun p -> { p with WorkingDirectory = toolGlob} |> optionConfig ) (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let fantomas optionConfig args =
        tool optionConfig "fantomas" args

    let femto optionConfig args =
        tool optionConfig (!!(__SOURCE_DIRECTORY__ @@ "packages/tooling/Femto/tools/**/**/Femto.dll") |> Seq.head) args

let setCmd f args =
    match Environment.isWindows with
    | true -> Command.RawCommand(f, Arguments.OfArgs args)
    | false -> Command.RawCommand("mono", Arguments.OfArgs (f::args))

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target.create "AssemblyInfo" <| fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion
          AssemblyInfo.Configuration configuration
          AssemblyInfo.InternalsVisibleTo (sprintf "%s.Tests" projectName) ]

    let getProjectDetails projectPath =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! srcGlob
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | Fsproj -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> () )

// --------------------------------------------------------------------------------------
// Update package.json version & name      
Target.create "PackageJson" <| fun _ ->
    let setValues (current: Json.JsonPackage) =
        { current with
            Name = Str.toKebabCase project
            Description = summary
            Version = release.NugetVersion
            Repository = 
                { Type = "git"
                  Url = repo + ".git" }
            Author = author
            License = (File.readLine(__SOURCE_DIRECTORY__ @@ "LICENSE.md").Split(' ')) |> Array.head
            Bugs = {Url = repo + "/issues"}
            Homepage = repo }
    
    Json.setJsonPkg setValues

// --------------------------------------------------------------------------------------
// Copies binaries from default VS location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs

Target.create "CopyBinaries" <| fun _ ->
    !! srcGlob
    -- (__SOURCE_DIRECTORY__ @@ "src/**/*.shproj")
    |> Seq.map (fun f -> ((Path.getDirectory f) @@ "bin" @@ configuration, "bin" @@ (Path.GetFileNameWithoutExtension f)))
    |> Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" <| fun _ ->
    !! (__SOURCE_DIRECTORY__  @@ "tests/**/bin")
    ++ (__SOURCE_DIRECTORY__  @@ "tests/**/obj")
    ++ (__SOURCE_DIRECTORY__  @@ "tools/bin")
    ++ (__SOURCE_DIRECTORY__  @@ "tools/obj")
    ++ (__SOURCE_DIRECTORY__  @@ "src/**/bin")
    ++ (__SOURCE_DIRECTORY__  @@ "src/**/obj")
    |> Seq.toList
    |> List.append ["bin"; "temp"; "obj"; "dist"; ".fable"]
    |> Shell.cleanDirs

Target.create "CleanDocs" <| fun _ ->
    Shell.cleanDirs ["docs"]

Target.create "PostBuildClean" <| fun _ ->
    !! srcGlob
    -- (__SOURCE_DIRECTORY__ @@ "src/**/*.shproj")
    |> Seq.map (
        (fun f -> (Path.getDirectory f) @@ "bin" @@ configuration) 
        >> (fun f -> Directory.EnumerateDirectories(f) |> Seq.toList )
        >> (fun fL -> fL |> List.map (fun f -> Directory.EnumerateDirectories(f) |> Seq.toList)))
    |> (Seq.concat >> Seq.concat)
    |> Seq.iter Directory.delete

Target.create "PostPublishClean" <| fun _ ->
    !! (__SOURCE_DIRECTORY__ @@ "src/**/bin" @@ configuration @@ "/**/publish")
    |> Seq.map (Directory.EnumerateDirectories >> Seq.toList )
    |> Seq.concat
    |> Seq.iter Directory.delete

// --------------------------------------------------------------------------------------
// Restore tasks

Target.create "Restore" <| fun _ ->
    solutionFile
    |> DotNet.restore id

/// Add task to make Node.js cli ready
Target.create "YarnInstall" <| fun _ ->
    let setParams (defaults:Yarn.YarnParams) =
        { defaults with
            Yarn.YarnParams.YarnFilePath = (__SOURCE_DIRECTORY__ @@ "packages/Yarnpkg.Yarn/content/bin/yarn.cmd")
        }
    Yarn.install setParams

// --------------------------------------------------------------------------------------
// Build tasks

Target.create "Build" <| fun _ ->
    let setParams (defaults:MSBuildParams) =
        { defaults with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", configuration
                    "Version", release.AssemblyVersion
                    "GenerateDocumentationFile", "true"
                    "DependsOnNETStandard", "true"
                ]
         }
    !! srcGlob
    ++ testGlob
    |> Seq.iter (MSBuild.build setParams)

Target.create "BuildElectron" <| fun _ ->
    Npm.exec "rebuild node-sass" id
    Yarn.exec "compile" id

// Run Devtron
Target.create "Dev" <| fun _ ->
    Yarn.exec "dev" id

// Build packed installer
Target.create "Dist" <| fun _ ->
    Yarn.exec "dist" id

// Build to unpacked directory
Target.create "DistDir" <| fun _ ->
    Yarn.exec "dist:dir" id

// --------------------------------------------------------------------------------------
// Publish net core applications

Target.create "PublishDotNet" <| fun _ ->
    let runPublish (project: string) (framework: string) =
        let setParams (defaults:MSBuildParams) =
            { defaults with
                Verbosity = Some(Quiet)
                Targets = ["Publish"]
                Properties =
                    [
                        "Optimize", "True"
                        "DebugSymbols", "True"
                        "Configuration", configuration
                        "Version", release.AssemblyVersion
                        "GenerateDocumentationFile", "true"
                        "TargetFramework", framework
                    ]
            }
        MSBuild.build setParams project

    !! srcGlob
    -- (__SOURCE_DIRECTORY__ @@ "src/**/*.shproj")
    -- (__SOURCE_DIRECTORY__ @@ "src/**/*.vbproj")
    |> Seq.map
        ((fun f -> (((Path.getDirectory f) @@ "bin" @@ configuration), f) )
        >>
        (fun f ->
            Directory.EnumerateDirectories(fst f) 
            |> Seq.filter (fun frFolder -> frFolder.Contains("netcoreapp"))
            |> Seq.map (fun frFolder -> DirectoryInfo(frFolder).Name), snd f))
    |> Seq.iter (fun (l,p) -> l |> Seq.iter (runPublish p))

// --------------------------------------------------------------------------------------
// Lint and format source code to ensure consistency

Target.create "Format" <| fun _ ->
    excludeFormatting
    |> (fun dirs -> List.fold foldExcludeGlobs fsSrcAndTest dirs)
    |> Seq.iter (fun file ->
        dotnet.fantomas id
            (sprintf "%s --pageWidth 120 --reorderOpenDeclaration" file))

Target.create "Lint" <| fun _ ->
    fsSrcAndTest
    -- (__SOURCE_DIRECTORY__  @@ "src/**/AssemblyInfo.*")
    |> (fun src -> List.fold foldExcludeGlobs src relaxedNameLinting)
    |> (fun fGlob ->
        match fsRelaxedNameLinting with
        | Some(glob) ->
            [(false, fGlob); (true, glob)]
        | None -> [(false, fGlob)])
    |> Seq.map (fun (b,glob) -> (b,glob |> List.ofSeq))
    |> List.ofSeq
    |> FSharpLinter.lintFiles (__SOURCE_DIRECTORY__ @@ "bin/LintResults.xml")

// --------------------------------------------------------------------------------------
// Validate JavaScript dependencies

Target.create "ValidateJSPackages" <| fun _ ->
    fsProjGlob
    |> Seq.iter (fun file ->
        dotnet.femto id
            (sprintf "--resolve %s" file))

// --------------------------------------------------------------------------------------
// Run the unit test binaries

Target.create "RunTests" <| fun _ ->
    !! testAssemblies
    |> Seq.iter (fun f ->
        CreateProcess.fromCommand(setCmd f [])
        |> CreateProcess.withTimeout (TimeSpan.MaxValue)
        |> CreateProcess.ensureExitCodeWithMessage "Tests failed."
        |> Proc.run
        |> ignore)

// --------------------------------------------------------------------------------------
// Generate Paket load scripts
Target.create "LoadScripts" <| fun _ ->
    let frameworks =
        __SOURCE_DIRECTORY__ @@ @"bin"
        |> Directory.EnumerateDirectories
        |> Seq.map (fun d ->
            Directory.EnumerateDirectories d
            |> Seq.map (fun f -> DirectoryInfo(f).Name)
            |> List.ofSeq)
        |> List.ofSeq
        |> List.reduce List.append
        |> List.reduce (fun acc elem -> sprintf "%s --framework %s" elem acc)
        |> function
        | e when e.Length > 0 ->
            Some (sprintf "--framework %s" e)
        | _ -> None

    let arguments =
        [Some("generate-load-scripts"); frameworks]
        |> List.choose id
        |> List.reduce (fun acc elem -> sprintf "%s %s" acc elem)

    arguments
    |> CreateProcess.fromRawCommandLine ((__SOURCE_DIRECTORY__ @@ ".paket") @@ "paket.exe")
    |> CreateProcess.withTimeout (TimeSpan.MaxValue)
    |> CreateProcess.ensureExitCodeWithMessage "Failed to generate paket load scripts."
    |> Proc.run
    |> ignore

// --------------------------------------------------------------------------------------
// Generate the documentation


// Paths with template/source/output locations
let content     = __SOURCE_DIRECTORY__ @@ "docsrc/content"
let output      = __SOURCE_DIRECTORY__ @@ "docs"
let files       = __SOURCE_DIRECTORY__ @@ "docsrc/files"
let templates   = __SOURCE_DIRECTORY__ @@ "docsrc/tools/templates"
let formatting  = __SOURCE_DIRECTORY__ @@ "packages/formatting/FSharp.Formatting"
let toolPath    = __SOURCE_DIRECTORY__ @@ "packages/formatting/FSharp.Formatting.CommandTool/tools/fsformatting.exe"
let docTemplate = "docpage.cshtml"

Target.create "LocalDocs" <| fun _ ->
    FakeVar.set "Website" output
Target.create "ReleaseDocs" <| fun _ ->
    FakeVar.set "Website" website

// Specify more information about your project
let info () =
  [ "project-name", project
    "project-author", author
    "project-summary", summary
    "project-repo", repo
    "root", FakeVar.getOrDefault "Website" website ]

let referenceBinaries = []

let layoutRootsAll = new Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[   templates;
                            formatting @@ "templates"
                            formatting @@ "templates/reference" ])

Target.create "ReferenceDocs" <| fun _ ->
    Directory.ensure (output @@ "reference")

    let lDirs = 
        DirectoryInfo.getSubDirectories <| DirectoryInfo bin
        |> Array.map DirectoryInfo.getSubDirectories
        |> Array.reduce Array.append
        |> Array.map (fun x -> x.FullName.ToLower())
        |> List.ofArray

    let binaries () =
        let manuallyAdded =
            referenceBinaries
            |> List.map (fun b -> bin @@ b)

        let conventionBased =
            DirectoryInfo.getSubDirectories <| DirectoryInfo bin
            |> Array.collect (fun d ->
                let name, dInfo =
                    let netFrameworkBin =
                        DirectoryInfo.getSubDirectories d |> Array.filter(fun x -> x.FullName.ToLower().Contains("net4"))
                    let netCoreBin =
                        DirectoryInfo.getSubDirectories d |> Array.filter(fun x -> x.FullName.ToLower().Contains("netcoreapp"))

                    match netFrameworkBin.Length > 0 with
                    | true ->
                        d.Name, netFrameworkBin |> Array.head
                    | false ->
                        d.Name, netCoreBin |> Array.head

                dInfo.GetFiles()
                |> Array.filter (fun x ->
                    x.Name.ToLower() = (sprintf "%s.dll" name).ToLower())
                |> Array.map (fun x -> x.FullName))
            |> List.ofArray

        conventionBased @ manuallyAdded

    binaries()
    |> FSFormatting.createDocsForDlls (fun args ->
        { args with
            OutputDirectory = output @@ "reference"
            LayoutRoots =  layoutRootsAll.["en"]
            ProjectParameters =  info()
            LibDirs = lDirs
            ToolPath = toolPath
            SourceRepository = repo @@ "tree/master" })

let copyFiles () =
    Shell.copyRecursive files output true
    |> Trace.logItems "Copying file: "
    Directory.ensure (output @@ "content")
    Shell.copyRecursive (formatting @@ "styles") (output @@ "content") true
    |> Trace.logItems "Copying styles and scripts: "

Target.create "Docs" <| fun _ ->
    File.delete "docsrc/content/release-notes.md"
    Shell.copyFile "docsrc/content/" "RELEASE_NOTES.md"
    Shell.rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"

    [ "# MordhauBuddy"
      ""
      sprintf "The documentation for this project can be found [here](%s/index.html)." website] 
    |> Seq.ofList
    |> File.writeNew "README.md" 

    DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath templates)
    |> Seq.iter (fun d ->
                    let name = d.Name
                    if name.Length = 2 || name.Length = 3 then
                        layoutRootsAll.Add(
                                name, [templates @@ name
                                       formatting @@ "templates"
                                       formatting @@ "templates/reference" ]))
    copyFiles ()

    for dir in  [ content ] do
        let langSpecificPath(lang, path:string) =
            path.Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.exists(fun i -> i = lang)
        let layoutRoots =
            let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
            match key with
            | Some lang -> layoutRootsAll.[lang]
            | None -> layoutRootsAll.["en"] // "en" is the default language

        FSFormatting.createDocs (fun args ->
            { args with
                Source = content
                OutputDirectory = output
                LayoutRoots = layoutRoots
                ProjectParameters  = info()
                Template = docTemplate })

Target.create "GenerateDocs" ignore

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "GitPush" <| fun p ->
    let msg =
        p.Context.Arguments
        |> List.choose (fun s ->
            match s.StartsWith("--Msg=") with
            | true -> Some(s.Substring 6)
            | false -> None)
        |> List.tryHead
        |> function
        | Some(s) -> s
        | None -> (sprintf "Bump version to %s" release.NugetVersion)

    Git.Staging.stageAll ""
    Git.Commit.exec "" msg
    Git.Branches.push ""

Target.create "GitTag" <| fun _ ->
    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build -t <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "PackageJson"
  ==> "YarnInstall"
  ==> "ValidateJSPackages"
  ==> "Build"
  ==> "BuildElectron"
  ==> "PostBuildClean" 
  ==> "CopyBinaries"

"Build" ==> "RunTests"

"Build"
  ==> "PostBuildClean"
  ==> "PublishDotNet"
  ==> "PostPublishClean"
  ==> "CopyBinaries"

"Restore" ==> "Lint"
"Restore" ==> "Format"

"Format"
  ?=> "Lint" 
  ?=> "Build"
  ?=> "RunTests"
  ?=> "CleanDocs"

"CopyBinaries"
  ==> "LoadScripts"
  ==> "CleanDocs"
  ==> "Docs"
  ==> "ReferenceDocs"
  ==> "GenerateDocs"

"Clean" 
  ==> "GitPush"
  ?=> "GitTag"

"All" <== ["Format"; "Lint"; "RunTests"; "GenerateDocs"]

"LocalDocs" ?=> "All"
"ReleaseDocs" ?=> "All"

"Dev" <== ["All"; "LocalDocs"]

"Dist" <== ["All"; "ReleaseDocs"]

"DistDir" <== ["All"; "ReleaseDocs"]

Target.runOrDefaultWithArguments "Dev"
