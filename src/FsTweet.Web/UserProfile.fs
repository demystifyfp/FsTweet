namespace UserProfile

module Domain =
  open User
  open System.Security.Cryptography
  open Chessie.ErrorHandling
  open System
  open Social.Domain
 
  type UserProfileType =
  | Self
  | OtherNotFollowing
  | OtherFollowing

  

  type UserProfile = {
    User : User
    GravatarUrl : string
    UserProfileType : UserProfileType
  }
  let gravatarUrl (emailAddress : UserEmailAddress) =
    use md5 = MD5.Create()
    emailAddress.Value 
    |> System.Text.Encoding.Default.GetBytes
    |> md5.ComputeHash
    |> Array.map (fun b -> b.ToString("x2"))
    |> String.concat ""
    |> sprintf "http://www.gravatar.com/avatar/%s?s=200"

  let newProfile userProfileType user = { 
    User = user
    GravatarUrl = gravatarUrl user.EmailAddress
    UserProfileType = userProfileType
  }

  type FindUserProfile = 
    Username -> User option -> AsyncResult<UserProfile option, Exception>
  let findUserProfile 
    (findUser : FindUser) (isFollowing : IsFollowing) 
    (username : Username) loggedInUser  = asyncTrial {

    match loggedInUser with
    | None -> 
      let! userMayBe = findUser username
      return Option.map (newProfile OtherNotFollowing) userMayBe
    | Some (user : User) -> 
      if user.Username = username then
        let userProfile = newProfile Self user
        return Some userProfile
      else  
        let! userMayBe = findUser username
        match userMayBe with
        | Some otherUser -> 
          let! isFollowingOtherUser = 
            isFollowing user otherUser.UserId
          let userProfileType =
            if isFollowingOtherUser then
              OtherFollowing
            else OtherNotFollowing 
          let userProfile = 
            newProfile userProfileType otherUser
          return Some userProfile
        | None -> return None
  }

module Suave =
  open Database
  open Suave.Filters
  open Auth.Suave
  open Suave
  open Domain
  open Social
  open User
  open Suave.DotLiquid
  open Chessie
  open System

  type UserProfileViewModel = {
    Username : string
    GravatarUrl : string
    IsLoggedIn : bool
    IsSelf : bool
    IsFollowing : bool
    UserId : int
    UserFeedToken : string
    ApiKey : string
    AppId : string
  }

  let newUserProfileViewModel (getStreamClient : GetStream.Client) (userProfile : UserProfile) = 
    let (UserId userId) = userProfile.User.UserId
    let isSelf, isFollowing = 
      match userProfile.UserProfileType with
      | Self -> true, false
      | OtherFollowing -> false, true
      | OtherNotFollowing -> false, false

    let userFeed = GetStream.userFeed getStreamClient userId
    {
      Username = userProfile.User.Username.Value
      GravatarUrl = userProfile.GravatarUrl
      IsLoggedIn = false
      IsSelf = isSelf
      IsFollowing = isFollowing
      UserId = userId
      UserFeedToken = userFeed.ReadOnlyToken
      ApiKey = getStreamClient.Config.ApiKey
      AppId = getStreamClient.Config.AppId
    }

  let renderUserProfilePage (vm : UserProfileViewModel) = 
    page "user/profile.liquid" vm

  let renderProfileNotFound =
    page "not_found.liquid" "user not found"

  let onFindUserProfileSuccess newUserProfileViewModel isLoggedIn userProfileMayBe = 
    match userProfileMayBe with
    | Some (userProfile : UserProfile) -> 
      let vm = {
        newUserProfileViewModel userProfile with
          IsLoggedIn = isLoggedIn }
      renderUserProfilePage vm
    | None -> 
      renderProfileNotFound

  let onFindUserProfileFailure (ex : Exception) =
    printfn "%A" ex
    page "server_error.liquid" "something went wrong"
    

  let renderUserProfile newUserProfileViewModel (findUserProfile : FindUserProfile) username loggedInUser  ctx = async {
    match Username.TryCreate username with
    | Success validatedUsername -> 
      let isLoggedIn = Option.isSome loggedInUser
      let onSuccess = 
        onFindUserProfileSuccess newUserProfileViewModel isLoggedIn
      let! webpart = 
        findUserProfile validatedUsername loggedInUser
        |> AR.either onSuccess onFindUserProfileFailure
      return! webpart ctx
    | Failure _ -> 
      return! renderProfileNotFound ctx
  }


  let webpart (getDataCtx : GetDataContext) getStreamClient = 
    let findUser = Persistence.findUser getDataCtx
    let isFollowing = Persistence.isFollowing getDataCtx
    let findUserProfile = findUserProfile findUser isFollowing
    let newUserProfileViewModel = newUserProfileViewModel getStreamClient
    let renderUserProfile = renderUserProfile newUserProfileViewModel findUserProfile
    pathScan "/%s" (fun username -> mayRequiresAuth(renderUserProfile username))