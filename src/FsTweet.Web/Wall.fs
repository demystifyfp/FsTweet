namespace Wall

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open User
  open Auth.Suave
  open Suave.DotLiquid
  open System.Text
  open Tweet
  open Chessie.ErrorHandling
  open Chiron
  open Chessie

  type WallViewModel = {
    Username :  string
  }

  let parse req =
    req.rawForm
    |> Encoding.UTF8.GetString 
    |> Json.tryParse
    |> ofChoice

  let inline deserialize< ^a when (^a or FromJsonDefaults) 
                            : (static member FromJson: ^a -> ^a Json)> req : Result< ^a, string> =
    parse req 
    |> mapSuccess Json.deserialize
    
  let jsonContentType = "application/json; charset=utf-8"
  let badRequest msg = 
    ["msg", String msg]
    |> Map.ofList
    |> Object
    |> Json.format
    |> RequestErrors.BAD_REQUEST
    >=> Writers.addHeader "Content-type" jsonContentType

  type PostRequest = PostRequest of string with
    static member FromJson (_ : PostRequest) = json {
      let! post = Json.read "post"
      return PostRequest post 
    }

  let renderWall (user : User) ctx = async {
    let vm = {Username = user.Username.Value }
    return! page "user/wall.liquid" vm ctx
  }

  let handleNewTweet ctx = async {
    match deserialize ctx.request  with
    | Success (PostRequest post) -> 
      return! Successful.OK "TODO" ctx
    | Failure err -> 
      return! badRequest err ctx
  }
  
  let webpart () = 
    choose [
      path "/wall" >=> requiresAuth renderWall
      POST >=> path "/tweets"  
        >=> handleNewTweet  
    ]