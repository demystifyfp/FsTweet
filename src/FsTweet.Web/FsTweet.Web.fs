module FsTweetWeb.Main

open Suave
open Suave.Filters
open Suave.Operators
open Suave.DotLiquid
open System.IO
open System.Reflection
open Suave.Files
open Database
open Email
open System.Net
open Logary.Configuration
open Logary
open Logary.Targets
open Hopac
open FsConfig

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

type StreamConfig = {
  Key : string
  Secret : string
  AppId : string
}

[<Convention("FSTWEET")>]
type Config = {
  DbConnString : string
  PostmarkServerToken : string
  SenderEmailAddress : string
  ServerKey : string
  [<CustomName("PORT")>]
  Port : uint16
  Environment : string
  Stream : StreamConfig
}

[<EntryPoint>]
let main argv =
  initDotLiquid ()
  setCSharpNamingConvention ()

  let config = 
    match EnvConfig.Get<Config>() with
    | Ok config -> config
    | Result.Error error -> 
      match error with
      | NotFound envVarName -> 
        failwithf "Environment variable %s not found" envVarName
      | BadValue (envVarName, value) ->
        failwithf "Unable to parse the value %s of the Environment variable %s" value envVarName
      | NotSupported msg -> 
        failwithf "Unable to read : %s" msg

  let streamConfig : GetStream.Config = {
      ApiKey = config.Stream.Key
      ApiSecret = config.Stream.Secret
      AppId = config.Stream.AppId
  }

  let sendEmail = 
    match config.Environment with
    | "dev" -> consoleSendEmail
    | _ -> initSendEmail config.SenderEmailAddress config.PostmarkServerToken

  let getDataCtx = dataContext config.DbConnString

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
    
  let serverKey = ServerKey.fromBase64 config.ServerKey

  let ipZero = IPAddress.Parse("0.0.0.0")

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
      bindings=[HttpBinding.create HTTP ipZero config.Port]}

  let appWithLogger = 
    app >=> context (logIfError logger)
  startWebServer serverConfig appWithLogger

  0
