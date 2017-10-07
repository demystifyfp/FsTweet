
[<RequireQualifiedAccess>]
module JSON 

open Suave
open Suave.Operators
open System.Text
open Chiron
open Chessie.ErrorHandling
open Chessie

let parse req =
  req.rawForm
  |> Encoding.UTF8.GetString 
  |> Json.tryParse
  |> ofChoice

let inline deserialize< ^a when (^a or FromJsonDefaults) 
                          : (static member FromJson: ^a -> ^a Json)> req : Result< ^a, string> =
  parse req 
  |> mapSuccess Json.deserialize


let contentType = "application/json; charset=utf-8"
let badRequest msg = 
  ["msg", String msg]
  |> Map.ofList
  |> Object
  |> Json.format
  |> RequestErrors.BAD_REQUEST
  >=> Writers.addHeader "Content-type" contentType