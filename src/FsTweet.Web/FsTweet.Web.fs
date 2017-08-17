module FsTweet.Web

open Suave
open Suave.Filters
open Suave.Operators
open Suave.DotLiquid
open System.IO
open System.Reflection

let currentPath =
  Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let initDotLiquid () =
  setCSharpNamingConvention ()
  let templatesDir = Path.Combine(currentPath, "views")
  setTemplatesDir templatesDir

[<EntryPoint>]
let main argv =
  initDotLiquid ()  
  let app = 
    path "/" >=> page "guest/home.liquid" ""
  startWebServer defaultConfig app
  0
