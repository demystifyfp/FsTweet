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

let copyToBuildDir srcDir targetDirName =
  let targetDir = combinePaths buildDir targetDirName
  CopyDir targetDir srcDir noFilter

Target "Views" (fun _ ->
  copyToBuildDir "./src/FsTweet.Web/views" "views"
)

Target "Assets" (fun _ ->
  copyToBuildDir "./src/FsTweet.Web/assets" "assets"
)

// Build order
"Clean"
==> "Build"
==> "Views"
==> "Assets"
==> "Run"

// start build
RunTargetOrDefault "Build"
