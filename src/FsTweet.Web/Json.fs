
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

let jsonWebPart webpart json = 
  json
  |> Json.format
  |> webpart
  >=> Writers.addHeader "Content-type" contentType

let error webpart msg  = 
  ["msg", String msg]
  |> Map.ofList
  |> Object
  |> jsonWebPart webpart

let badRequest msg = 
  error RequestErrors.BAD_REQUEST msg
let forbidden = 
  error RequestErrors.FORBIDDEN "login required"

let internalError =
  error ServerErrors.INTERNAL_ERROR "something went wrong"

let inline ok< ^a when (^a or ToJsonDefaults) 
              : (static member ToJson: ^a -> Json<unit>)> (data : ^a) =
  data |> Json.serialize |> jsonWebPart Successful.OK