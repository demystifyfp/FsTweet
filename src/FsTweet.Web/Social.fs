namespace Social

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Auth.Suave
  open User
  open Chiron
  open Chessie

  type FollowUserRequest = FollowUserRequest of int with 
    static member FromJson (_ : FollowUserRequest) = json {
        let! userId = Json.read "userId"
        return FollowUserRequest userId 
      }

  let handleFollowUser (user : User) ctx = async {
    match JSON.deserialize ctx.request with
    | Success (FollowUserRequest userId) -> 
      return! JSON.ok (String "Todo") ctx
    | Failure _ -> 
      return! JSON.badRequest "invalid user follow request" ctx
  }

  let webpart () =
    POST >=> path "/follow" >=> (requiresAuth2 handleFollowUser)