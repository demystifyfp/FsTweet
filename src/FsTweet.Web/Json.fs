
[<RequireQualifiedAccess>]
module JSON 

open Suave
open Suave.Operators
open System.Text
open Chiron
open Chessie.ErrorHandling

let parse req =
  req.rawForm
  |> Encoding.UTF8.GetString 
  |> Json.tryParse
  |> ofChoice

let inline deserialize2< ^a when (^a or FromJsonDefaults) 
                          : (static member FromJson: ^a -> ^a Json)> 
                          req : Result< ^a, string> =
  parse req 
  |> bind (fun json -> 
            json 
            |> Json.tryDeserialize 
            |> ofChoice)

// Note: Compiles only with F# 4.1 or above

// For F# 4.0 and below
(* If you are using F# 4.1 or above, delete the this function and uncomment the above function*)
let deserialize f req =
  parse req
  |> bind (fun json -> f json |> ofChoice)

let contentType = "application/json; charset=utf-8"

let json fWebpart json = 
  json
  |> Json.format
  |> fWebpart
  >=> Writers.addHeader "Content-type" contentType

let error fWebpart msg  = 
  ["msg", String msg]
  |> Map.ofList
  |> Object
  |> json fWebpart

let badRequest msg = 
  error RequestErrors.BAD_REQUEST msg
let unauthorized = 
  error RequestErrors.UNAUTHORIZED "login required"

let internalError =
  error ServerErrors.INTERNAL_ERROR "something went wrong"

let ok =
  json (Successful.OK)