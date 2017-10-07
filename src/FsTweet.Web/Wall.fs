namespace Wall

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open User
  open Auth.Suave
  open Suave.DotLiquid
  open Tweet
  open Chiron
  open Chessie

  type WallViewModel = {
    Username :  string
  }

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
    match JSON.deserialize ctx.request  with
    | Success (PostRequest post) -> 
      return! Successful.OK "TODO" ctx
    | Failure err -> 
      return! JSON.badRequest err ctx
  }
  
  let webpart () = 
    choose [
      path "/wall" >=> requiresAuth renderWall
      POST >=> path "/tweets"  
        >=> handleNewTweet  
    ]