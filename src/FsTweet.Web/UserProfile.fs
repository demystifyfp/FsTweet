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
    emailAddress.Value.Trim().ToLowerInvariant()  
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
    Username -> User option -> AsyncResult<UserProfile option, Exception>

  let findUserProfile (findUser : FindUser) (username : Username) loggedInUser  = asyncTrial {
    match loggedInUser with
    | None -> 
      let! userMayBe = findUser username
      return Option.map newProfile userMayBe
    | Some (user : User) -> 
      if user.Username = username then
        let userProfile =
          {newProfile user with IsSelf = true}
        return Some userProfile
      else  
        let! userMayBe = findUser username
        return Option.map newProfile userMayBe
  }

module Suave =
  open Database
  open Suave.Filters
  open Auth.Suave
  open Suave
  open Domain
  open User
  open Suave.DotLiquid
  open Chessie.ErrorHandling
  open Chessie

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
      IsSelf = false
      UserId = userId
      UserFeedToken = userFeed.ReadOnlyToken
      ApiKey = getStreamClient.Config.ApiKey
      AppId = getStreamClient.Config.AppId
    }

  let renderUserProfilePage (vm : UserProfileViewModel) = 
    page "user/profile.liquid" vm

  let renderProfileNotFound =
    page "not_found.liquid" "user not found"
  
  let handleUserProfileAsyncResult newUserProfileViewModel loggedInUser aResult = async {
    let! result = aResult |> Async.ofAsyncResult
    match result with
    | Success (Some (userProfile : UserProfile)) -> 
      let vm = {
        newUserProfileViewModel userProfile with
          IsLoggedIn = Option.isSome loggedInUser
          IsSelf = userProfile.IsSelf }
      return renderUserProfilePage vm
    | Success None -> 
      return renderProfileNotFound
    | Failure ex -> 
      printfn "%A" ex
      return page "server_error.liquid" "something went wrong"
  } 
    

  let renderUserProfile newUserProfileViewModel findUserProfile username loggedInUser  ctx = async {
    match Username.TryCreate username with
    | Success validatedUsername -> 
      let! webpart = 
        findUserProfile validatedUsername loggedInUser
        |> handleUserProfileAsyncResult newUserProfileViewModel loggedInUser
      return! webpart ctx
    | Failure _ -> 
      return! renderProfileNotFound ctx
  }


  let webpart (getDataCtx : GetDataContext) getStreamClient = 
    let findUser = Persistence.findUser getDataCtx
    let findUserProfile = findUserProfile findUser
    let newUserProfileViewModel = newUserProfileViewModel getStreamClient
    let renderUserProfile = renderUserProfile newUserProfileViewModel findUserProfile
    pathScan "/%s" (fun username -> mayRequiresAuth(renderUserProfile username))