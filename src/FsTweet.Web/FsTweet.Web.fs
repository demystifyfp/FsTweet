module FsTweetWeb.Main

open Suave
open Suave.Filters
open Suave.Operators
open Suave.DotLiquid
open System.IO
open System.Reflection
open Suave.Files
open FSharp.Data.Sql

let currentPath =
  Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let initDotLiquid () =
  setCSharpNamingConvention ()
  let templatesDir = Path.Combine(currentPath, "views")
  setTemplatesDir templatesDir

let serveAssets =
  let faviconPath = 
    Path.Combine(currentPath, "assets", "images", "favicon.ico")
  choose [
    pathRegex "/assets/*" >=> browseHome
    path "/favicon.ico" >=> file faviconPath
  ]

[<Literal>]
let connString = 
  "Server=127.0.0.1;Port=5432;Database=FsTweet;User Id=postgres;Password=test;"

[<Literal>]
let npgsqlLibPath = @"./../../packages/database/Npgsql/lib/net451"

type Db = SqlDataProvider<
            ConnectionString=connString,
            DatabaseVendor=Common.DatabaseProviderTypes.POSTGRESQL,
            ResolutionPath=npgsqlLibPath,
            UseOptionTypes=true>

[<EntryPoint>]
let main argv =

  initDotLiquid ()
  let app = 
    choose [
      serveAssets
      path "/" >=> page "guest/home.liquid" ""
      UserSignup.Suave.webPart ()
    ]
    
  startWebServer defaultConfig app
  0
