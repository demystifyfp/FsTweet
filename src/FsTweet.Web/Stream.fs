[<RequireQualifiedAccess>]
module GetStream

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