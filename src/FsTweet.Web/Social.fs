namespace Social

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Auth.Suave
  open User
  open Chiron
  open Chessie

  type FollowUserRequest = FollowUserRequest of string with 
    static member FromJson (_ : FollowUserRequest) = json {
        let! username = Json.read "username"
        return FollowUserRequest username 
      }

  let handleFollowUser (user : User) ctx = async {
    match JSON.deserialize ctx.request with
    | Success (FollowUserRequest username) -> 
      match Username.TryCreate username with
      | Success validatedUserName ->
        return! JSON.ok (String "Todo") ctx
      | Failure _ -> 
        return! JSON.badRequest "invalid username" ctx
    | Failure _ -> 
      return! JSON.badRequest "invalid user follow request" ctx
  }

  let webpart () =
    POST >=> path "/follow" >=> (requiresAuth2 handleFollowUser)