namespace Social

module Domain = 
  open System
  open Chessie.ErrorHandling
  open User

  type CreateFollowing = User -> UserId -> AsyncResult<unit, Exception>
  type Subscribe = User -> UserId -> AsyncResult<unit, Exception>
  type FollowUser = User -> UserId -> AsyncResult<unit, Exception>

  let followUser 
    (subscribe : Subscribe) (createFollowing : CreateFollowing) 
    user userId = asyncTrial {

    do! subscribe user userId
    do! createFollowing user userId

  } 

module Persistence =
  open Database
  open User
  let createFollowing (getDataCtx : GetDataContext) (user : User) (UserId userId) = 
     
     let ctx = getDataCtx ()
     let social = ctx.Public.Social.Create()
     let (UserId followerUserId) = user.UserId
      
     social.FollowerUserId <- followerUserId
     social.FollowingUserId <- userId

     submitUpdates ctx
   
module GetStream = 
  open User
  open Chessie
  let subscribe (getStreamClient : GetStream.Client) (user : User) (UserId userId) = 
    let (UserId followerUserId) = user.UserId
    let timelineFeed = 
      GetStream.timeLineFeed getStreamClient followerUserId
    let userFeed =
      GetStream.userFeed getStreamClient userId
    timelineFeed.FollowFeed(userFeed) 
    |> Async.AwaitTask
    |> AR.catch


module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Auth.Suave
  open User
  open Chiron
  open Chessie
  open Persistence
  open Domain

  type FollowUserRequest = FollowUserRequest of int with 
    static member FromJson (_ : FollowUserRequest) = json {
        let! userId = Json.read "userId"
        return FollowUserRequest userId 
      }

  let onFollowUserSuccess () =
    Successful.NO_CONTENT
  let onFollowUserFailure (ex : System.Exception) =
    printfn "%A" ex
    JSON.internalError

  let handleFollowUser (followUser : FollowUser) (user : User) ctx = async {
    match JSON.deserialize ctx.request with
    | Success (FollowUserRequest userId) -> 
      let! webpart =
        followUser user (UserId userId)
        |> AR.either onFollowUserSuccess onFollowUserFailure
      return! webpart ctx
    | Failure _ -> 
      return! JSON.badRequest "invalid user follow request" ctx
  }

  let webpart getDataCtx getStreamClient =
    let createFollowing = createFollowing getDataCtx
    let subscribe = GetStream.subscribe getStreamClient
    let followUser = followUser subscribe createFollowing
    let handleFollowUser = handleFollowUser followUser
    POST >=> path "/follow" >=> requiresAuth2 handleFollowUser