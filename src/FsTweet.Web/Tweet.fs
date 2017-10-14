namespace Tweet
open User
open Chessie.ErrorHandling

type PostId = PostId of System.Guid

type Post = private Post of string with
  static member TryCreate (post : string) =
    match post with
    | null | ""  -> fail "Tweet should not be empty"
    | x when x.Length > 140 -> fail "Tweet should not be more than 140 characters"
    | x -> Post x |> ok
  member this.Value = 
    let (Post post) = this
    post

type CreatePost = UserId -> Post -> AsyncResult<PostId, System.Exception>

type Tweet = {
  UserId : UserId
  Username : Username
  PostId : PostId
  Post : Post
}

module Persistence =

  open User
  open Database
  open System

  let createPost (getDataCtx : GetDataContext) (UserId userId) (post : Post) = asyncTrial {
    let ctx = getDataCtx()
    let newTweet = ctx.Public.Tweets.Create()
    let newPostId = Guid.NewGuid()

    newTweet.UserId <- userId
    newTweet.Id <- newPostId
    newTweet.Post <- post.Value
    newTweet.TweetedAt <- DateTime.UtcNow

    do! submitUpdates ctx 
    return PostId newPostId
  }
