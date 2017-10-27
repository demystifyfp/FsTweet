namespace UserProfile

module Domain =
  open User
  open System.Security.Cryptography
  open Chessie.ErrorHandling
  open System

  type UserProfile = {
    User : User
    GravatarUrl : string
    IsSelf : bool
  }
  let gravatarUrl (emailAddress : UserEmailAddress) =
    use md5 = MD5.Create()
    emailAddress.Value 
    |> System.Text.Encoding.Default.GetBytes
    |> md5.ComputeHash
    |> Array.map (fun b -> b.ToString("x2"))
    |> String.concat ""
    |> sprintf "http://www.gravatar.com/avatar/%s?s=200"

  let newProfile user = { 
    User = user
    GravatarUrl = gravatarUrl user.EmailAddress
    IsSelf = false
  }

  type FindUserProfile = 
    Username -> AsyncResult<UserProfile option, Exception>
  let findUserProfile (findUser : FindUser) username = asyncTrial {
    let! userMayBe = findUser username
    return Option.map newProfile userMayBe
  }

  type HandleUserProfile = 
    Username -> User option -> AsyncResult<UserProfile option, Exception>
  let handleUserProfile findUserProfile (username : Username) loggedInUser  = asyncTrial {
    match loggedInUser with
    | None -> 
      return! findUserProfile username
    | Some (user : User) -> 
      if user.Username = username then
        let userProfile =
          {newProfile user with IsSelf = true}
        return Some userProfile
      else  
        return! findUserProfile username
  }

module Suave =
  open Database
  open Suave.Filters
  open Auth.Suave
  open Suave
  open Domain
  open User
  open Suave.DotLiquid
  open Chessie
  open System

  type UserProfileViewModel = {
    Username : string
    GravatarUrl : string
    IsLoggedIn : bool
    IsSelf : bool
    UserId : int
    UserFeedToken : string
    ApiKey : string
    AppId : string
  }

  let newUserProfileViewModel (getStreamClient : GetStream.Client) (userProfile : UserProfile) = 
    let (UserId userId) = userProfile.User.UserId
    let userFeed = GetStream.userFeed getStreamClient userId
    {
      Username = userProfile.User.Username.Value
      GravatarUrl = userProfile.GravatarUrl
      IsLoggedIn = false
      IsSelf = userProfile.IsSelf
      UserId = userId
      UserFeedToken = userFeed.ReadOnlyToken
      ApiKey = getStreamClient.Config.ApiKey
      AppId = getStreamClient.Config.AppId
    }

  let renderUserProfilePage (vm : UserProfileViewModel) = 
    page "user/profile.liquid" vm

  let renderProfileNotFound =
    page "not_found.liquid" "user not found"

  let onHandleUserProfileSuccess newUserProfileViewModel isLoggedIn userProfileMayBe = 
    match userProfileMayBe with
    | Some (userProfile : UserProfile) -> 
      let vm = {
        newUserProfileViewModel userProfile with
          IsLoggedIn = isLoggedIn }
      renderUserProfilePage vm
    | None -> 
      renderProfileNotFound

  let onHandleUserProfileFailure (ex : Exception) =
    printfn "%A" ex
    page "server_error.liquid" "something went wrong"
    

  let renderUserProfile newUserProfileViewModel (handleUserProfile : HandleUserProfile) username loggedInUser  ctx = async {
    match Username.TryCreate username with
    | Success validatedUsername -> 
      let isLoggedIn = Option.isSome loggedInUser
      let onSuccess = 
        onHandleUserProfileSuccess newUserProfileViewModel isLoggedIn
      let! webpart = 
        handleUserProfile validatedUsername loggedInUser
        |> AR.either onSuccess onHandleUserProfileFailure
      return! webpart ctx
    | Failure _ -> 
      return! renderProfileNotFound ctx
  }


  let webpart (getDataCtx : GetDataContext) getStreamClient = 
    let findUserProfile = findUserProfile (Persistence.findUser getDataCtx)
    let handleUserProfile = handleUserProfile findUserProfile
    let newUserProfileViewModel = newUserProfileViewModel getStreamClient
    let renderUserProfile = renderUserProfile newUserProfileViewModel handleUserProfile
    pathScan "/%s" (fun username -> mayRequiresAuth(renderUserProfile username))