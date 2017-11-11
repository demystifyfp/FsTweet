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
open Email
open System.Net
open Logary.Configuration
open Logary
open Logary.Targets
open Hopac


let currentPath =
  Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let initDotLiquid () =
  let templatesDir = Path.Combine(currentPath, "views")
  setTemplatesDir templatesDir

let serveAssets =
  let faviconPath = 
    Path.Combine(currentPath, "assets", "images", "favicon.ico")
  choose [
    pathRegex "/assets/*" >=> browseHome
    path "/favicon.ico" >=> file faviconPath
  ]

let readUserState ctx key : 'value option =
  ctx.userState 
  |> Map.tryFind key 
  |> Option.map (fun x -> x :?> 'value)
let logIfError (logger : Logger) ctx = 
  readUserState ctx "err"
  |> Option.iter logger.logSimple
  succeed

[<EntryPoint>]
let main argv =
  initDotLiquid ()
  setCSharpNamingConvention ()

  let fsTweetConnString = 
   Environment.GetEnvironmentVariable  "FSTWEET_DB_CONN_STRING"

  let serverToken =
    Environment.GetEnvironmentVariable "FSTWEET_POSTMARK_SERVER_TOKEN"

  let senderEmailAddress =
    Environment.GetEnvironmentVariable "FSTWEET_SENDER_EMAIL_ADDRESS"

  let env = 
    Environment.GetEnvironmentVariable "FSTWEET_ENVIRONMENT"

  let streamConfig : GetStream.Config = {
      ApiKey = 
        Environment.GetEnvironmentVariable "FSTWEET_STREAM_KEY"
      ApiSecret = 
        Environment.GetEnvironmentVariable "FSTWEET_STREAM_SECRET"
      AppId = 
        Environment.GetEnvironmentVariable "FSTWEET_STREAM_APP_ID"
  }

  let sendEmail = 
    match env with
    | "dev" -> consoleSendEmail
    | _ -> initSendEmail senderEmailAddress serverToken

  let getDataCtx = dataContext fsTweetConnString

  let getStreamClient = GetStream.newClient streamConfig

  let app = 
    choose [
      serveAssets
      path "/" >=> page "guest/home.liquid" ""
      UserSignup.Suave.webPart getDataCtx sendEmail
      Auth.Suave.webpart getDataCtx
      Wall.Suave.webpart getDataCtx getStreamClient
      Social.Suave.webpart getDataCtx getStreamClient
      UserProfile.Suave.webpart getDataCtx getStreamClient
    ]
    
  let serverKey = 
    Environment.GetEnvironmentVariable "FSTWEET_SERVER_KEY"
    |> ServerKey.fromBase64

  let ipZero = IPAddress.Parse("0.0.0.0")
  
  let port = 
    Environment.GetEnvironmentVariable "PORT"

  let targets = withTarget (Console.create Console.empty "console")
  let rules = withRule (Rule.createForTarget "console")
  let logaryConf = targets >> rules

  use logary =
    withLogaryManager "FsTweet.Web" logaryConf |> run

  let logger =
    logary.getLogger (PointName [|"Suave"|])

  let serverConfig = 
    {defaultConfig with 
      serverKey = serverKey
      bindings=[HttpBinding.create HTTP ipZero (uint16 port)]}

  let appWithLogger = 
    app >=> context (logIfError logger)
  startWebServer serverConfig appWithLogger

  0
