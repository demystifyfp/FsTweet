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
  open Chessie.ErrorHandling
  open Chessie

  type WallViewModel = {
    Username :  string
    UserId : int
    ApiKey : string
    AppId : string
  }

  type PostRequest = PostRequest of string with
    static member FromJson (_ : PostRequest) = json {
      let! post = Json.read "post"
      return PostRequest post 
    }

  let renderWall 
    (getStreamClient : GetStream.Client) 
    (user : User) ctx = async {
     
    let (UserId userId) = user.UserId
    let vm = {
      Username = user.Username.Value 
      UserId = userId
      ApiKey = getStreamClient.Config.ApiKey
      AppId = getStreamClient.Config.AppId}

    return! page "user/wall.liquid" vm ctx

  }

  let onCreateTweetSuccess (PostId id) = 
    ["id", String (id.ToString())]
    |> Map.ofList
    |> Object
    |> JSON.ok

  let onCreateTweetFailure (ex : System.Exception) =
    printfn "%A" ex
    JSON.internalError

  let handleNewTweet createTweet (user : User) ctx = async {
    match JSON.deserialize ctx.request  with
    | Success (PostRequest post) -> 
      match Post.TryCreate post with
      | Success post -> 
        let! webpart = 
          createTweet user.UserId post
          |> AR.either onCreateTweetSuccess onCreateTweetFailure
        return! webpart ctx
      | Failure err -> 
        return! JSON.badRequest err ctx
    | Failure err -> 
      return! JSON.badRequest err ctx
  }
  
  let webpart getDataCtx getStreamClient =
    let createTweet = Persistence.createPost getDataCtx 
    choose [
      path "/wall" >=> requiresAuth (renderWall getStreamClient)
      POST >=> path "/tweets"  
        >=> requiresAuth2 (handleNewTweet createTweet)  
    ]