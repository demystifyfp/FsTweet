// include Fake libs
#r "./packages/FAKE/tools/FakeLib.dll"

open Fake

// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/"


// Filesets
let appReferences  =
    !! "/**/*.csproj"
    ++ "/**/*.fsproj"

// version info
let version = "0.1"  // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; deployDir]
)

Target "Build" (fun _ ->
    // compile all projects below src/app/
    MSBuildDebug buildDir "Build" appReferences
    |> Log "AppBuild-Output: "
)

Target "Run" (fun _ -> 
    ExecProcess 
        (fun info -> info.FileName <- "./build/FsTweet.Web.exe")
        (System.TimeSpan.FromDays 1.)
    |> ignore
)

let noFilter = fun _ -> true

Target "Views" (fun _ ->
    let srcDir = "./src/FsTweet.Web/views"
    let targetDir = combinePaths buildDir "views"
    CopyDir targetDir srcDir noFilter
)

// Build order
"Clean"
  ==> "Build"
  ==> "Views"
  ==> "Run"

// start build
RunTargetOrDefault "Build"
