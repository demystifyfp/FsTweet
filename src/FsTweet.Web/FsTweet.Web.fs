module FsTweet.Web

open Suave

[<EntryPoint>]
let main argv =
  //DotLiquid.setCSharpNamingConvention ()
  DotLiquid.setTemplatesDir View.viewsDir
  let app = SuaveApp.init ()
  startWebServer defaultConfig app
  0
