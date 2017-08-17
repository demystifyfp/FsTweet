module FsTweet.Web

open Suave
open Suave.DotLiquid


[<EntryPoint>]
let main argv =
  setCSharpNamingConvention ()
  setTemplatesDir View.viewsDir
  
  let app = SuaveApp.init ()
  startWebServer defaultConfig app
  0
