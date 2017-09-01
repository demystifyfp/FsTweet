module FsTweetWeb.Main

open Suave
open Suave.Filters
open Suave.Operators
open Suave.DotLiquid
open System.IO
open System.Reflection
open Suave.Files
open Database
open System
open System.Transactions

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

[<EntryPoint>]
let main argv =
  initDotLiquid ()
  // use transaction = 
  //   new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)

  let fsTweetConnString = 
   Environment.GetEnvironmentVariable  "FSTWEET_DB_CONN_STRING"
  let dbCtx : DbContext = Db.GetDataContext(fsTweetConnString)
  
  let app = 
    choose [
      serveAssets
      path "/" >=> page "guest/home.liquid" ""
      UserSignup.Suave.webPart dbCtx
    ]
    
  startWebServer defaultConfig app
  // transaction.Complete()
  0
