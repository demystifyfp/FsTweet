namespace UserProfile

module Suave =
  open Database
  open Suave.Filters
  open Auth.Suave
  open Suave
  open User
  open Suave.DotLiquid
  open System.Security.Cryptography
  open Chessie

  type UserProfileViewModel = {
    Username : string
    GravatarUrl : string
    IsLoggedIn : bool
    IsSelf : bool
  }

  let emptyUserProfileViewModel = {
    Username = ""
    GravatarUrl = ""
    IsLoggedIn = false
    IsSelf = false
  }

  let gravatarUrl (emailAddress : UserEmailAddress) =
    use md5 = MD5.Create()
    emailAddress.Value.Trim().ToLowerInvariant()  
    |> System.Text.Encoding.Default.GetBytes
    |> md5.ComputeHash
    |> Array.map (fun b -> b.ToString("x2"))
    |> String.concat ""
    |> sprintf "http://www.gravatar.com/avatar/%s?s=200"

  let onFindUserSuccess userProfileViewModel (userMayBe : User option) = 
    match userMayBe with
    | None -> 
      let msg = 
        sprintf "User '%s' not found" userProfileViewModel.Username
      page "not_found.liquid" msg
    | Some user -> 
      let vm = 
        { userProfileViewModel with 
            GravatarUrl = gravatarUrl user.EmailAddress
            Username = user.Username.Value
        }
      page "user/profile.liquid" vm

  let onFindUserFailure ex =
    printfn "%A" ex
    page "server_error.liquid" "something went wrong"


  let renderUserProfile findUser username userMayBe  ctx = async {
    match Username.TryCreate username with
    | Success validatedUsername -> 
      let vm = 
        {emptyUserProfileViewModel 
          with Username = validatedUsername.Value}
      match userMayBe with
      | None -> 
        let! webpart =
            findUser validatedUsername
            |> AR.either (onFindUserSuccess vm) onFindUserFailure
        return! webpart ctx
      | Some (user : User) -> 
        let vm = {vm with IsLoggedIn = true}
        match user.Username = validatedUsername with
        | true -> 
          let vm = {vm with IsSelf = true}
          return! onFindUserSuccess vm (Some user) ctx
        | false ->
          let! webpart =
            findUser validatedUsername
            |> AR.either (onFindUserSuccess vm) onFindUserFailure
          return! webpart ctx
    | Failure _ -> 
      return! page "not_found.liquid" "invalid user name" ctx
  }


  let webpart (getDataCtx : GetDataContext) = 
    let findUser = Persistence.findUser getDataCtx
    pathScan "/%s" 
      (fun username -> mayRequiresAuth (renderUserProfile findUser username))
    