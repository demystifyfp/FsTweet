[<RequireQualifiedAccess>]
module GetStream
open Chessie.ErrorHandling
open Stream

type Config = {
  ApiSecret : string
  ApiKey : string
  AppId : string
}

type Client = {
  Config : Config
  StreamClient : StreamClient
}

let newClient config = {
  StreamClient = 
    new StreamClient(config.ApiKey, config.ApiSecret)
  Config = config
}

let userFeed getStreamClient (userId : int) =
  getStreamClient.StreamClient.Feed("user", userId.ToString())

let timeLineFeed getStreamClient (userId : int) =
  getStreamClient.StreamClient.Feed("timeline", userId.ToString())