// include Fake libs
#r "./packages/FAKE/tools/FakeLib.dll"
#r "./packages/FAKE/tools/Fake.FluentMigrator.dll"
#r "./packages/database/Npgsql/lib/net451/Npgsql.dll"

open Fake
open Fake.FluentMigratorHelper
open System.IO
open Fake.Azure

// Directories
let buildDir  = "./build/"
let migrationsAssembly = 
  combinePaths buildDir "FsTweet.Db.Migrations.dll"

// version info
let version = "0.1"  // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
  CleanDirs [buildDir]
)

Target "BuildMigrations" (fun _ ->
  !! "src/FsTweet.Db.Migrations/*.fsproj"
  |> MSBuildDebug buildDir "Build" 
  |> Log "MigrationBuild-Output: "
)
let localDbConnString = @"Server=127.0.0.1;Port=5432;Database=FsTweet;User Id=postgres;Password=test;"
let connString = 
  environVarOrDefault 
    "FSTWEET_DB_CONN_STRING"
    localDbConnString

setEnvironVar "FSTWEET_DB_CONN_STRING" connString
let dbConnection = ConnectionString (connString, DatabaseProvider.PostgreSQL)

Target "RunMigrations" (fun _ -> 
  MigrateToLatest dbConnection [migrationsAssembly] DefaultMigrationOptions
)

let env = environVar "FSTWEET_ENVIRONMENT" 

let buildConfig = 
  if env = "dev" then MSBuildDebug else MSBuildRelease

Target "Build" (fun _ ->
  !! "src/FsTweet.Web/*.fsproj"
  |> buildConfig buildDir "Build"
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

let swapConnectionString (oldValue: string) (newValue : string) =
  let dbFilePath = "./src/FsTweet.Web/Db.fs"
  let dbFileContent = File.ReadAllText dbFilePath
  let newDbFileContent = dbFileContent.Replace(oldValue, newValue)
  File.WriteAllText(dbFilePath, newDbFileContent)

Target "ReplaceDbConnStringForBuild" (fun _ -> 
  swapConnectionString localDbConnString connString
)
Target "RevertDbConnStringChange" (fun _ -> 
  swapConnectionString connString localDbConnString
)

Target "StageFiles" ( fun _ ->
  FileHelper.CopyFile Kudu.deploymentTemp "web.config"
  Kudu.stageFolder buildDir (fun _ -> true))

Target "Deploy" Kudu.kuduSync

// Build order
"Clean"
==> "BuildMigrations"
==> "RunMigrations"
==> "ReplaceDbConnStringForBuild"
==> "Build"
==> "RevertDbConnStringChange"
==> "Views"
==> "Assets"


"Assets"
==> "Run"

"Assets"
==> "StageFiles"
==> "Deploy"

// start build
RunTargetOrDefault "Assets"
