namespace Wall

module Domain = 
  open User
  open Tweet
  open Chessie.ErrorHandling
  open System
  open Chessie

  type NotifyTweet = Tweet -> AsyncResult<unit, Exception>

  type PublishTweetError =
  | CreatePostError of Exception
  | NotifyTweetError of (PostId * Exception)

  type PublishTweet =
    CreatePost -> NotifyTweet -> AsyncResult<PostId, PublishTweetError>

  let publishTweet createPost notifyTweet (user : User) post = asyncTrial {
    let! postId = 
      createPost user.UserId post
      |> AR.mapFailure CreatePostError

    let tweet = {
      PostId = postId
      UserId = user.UserId
      Username = user.Username
      Post = post
    }
    do! notifyTweet tweet 
        |> AR.mapFailure (fun ex -> NotifyTweetError(postId, ex))

    return postId
  }

module GetStream = 
  open Tweet
  open User
  open Stream
  open Chessie.ErrorHandling

  let notifyTweet (getStreamClient: GetStream.Client) (tweet : Tweet) = 
    let (UserId userId) = tweet.UserId
    let userIdAsString = userId.ToString()
    let userFeed =
      GetStream.userFeed getStreamClient userIdAsString
    let (PostId postId) = tweet.PostId
    let activity = new Activity(userIdAsString, "tweet", postId.ToString())
    activity.SetData("tweet", tweet.Post.Value)
    activity.SetData("username", tweet.Username.Value)
    
    userFeed.AddActivity(activity)
    |> Async.AwaitTask
    |> Async.Catch
    |> Async.map GetStream.mapNewActivityResponse
    |> AR

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
  open Domain

  type WallViewModel = {
    Username :  string
    UserId : int
    UserFeedToken : string
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
    
    let userFeed = 
      GetStream.userFeed getStreamClient (userId.ToString())
    
    let vm = {
      Username = user.Username.Value 
      UserId = userId
      UserFeedToken = userFeed.ReadOnlyToken
      ApiKey = getStreamClient.Config.ApiKey
      AppId = getStreamClient.Config.AppId}

    return! page "user/wall.liquid" vm ctx

  }

  let onPublishTweetSuccess (PostId id) = 
    ["id", String (id.ToString())]
    |> Map.ofList
    |> Object
    |> JSON.ok

  let onPublishTweetFailure (err : PublishTweetError) =
    match err with
    | NotifyTweetError (postId, ex) ->
      printfn "%A" ex
      onPublishTweetSuccess postId
    | CreatePostError ex ->
      printfn "%A" ex
      JSON.internalError

  let handleNewTweet publishTweet (user : User) ctx = async {
    match JSON.deserialize ctx.request  with
    | Success (PostRequest post) -> 
      match Post.TryCreate post with
      | Success post -> 
        let! webpart = 
          publishTweet user post
          |> AR.either onPublishTweetSuccess onPublishTweetFailure
        return! webpart ctx
      | Failure err -> 
        return! JSON.badRequest err ctx
    | Failure err -> 
      return! JSON.badRequest err ctx
  }
  
  let webpart getDataCtx getStreamClient =
    let createPost = Persistence.createPost getDataCtx 
    let notifyTweet = GetStream.notifyTweet getStreamClient
    let publishTweet = publishTweet createPost notifyTweet
    choose [
      path "/wall" >=> requiresAuth (renderWall getStreamClient)
      POST >=> path "/tweets"  
        >=> requiresAuth2 (handleNewTweet publishTweet)  
    ]