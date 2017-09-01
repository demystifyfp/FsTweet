namespace FsTweet.Db.Migrations

open FluentMigrator

[<Migration(201709250622L, "Creating User Table")>]
type CreateUserTable()=
  inherit Migration()

  override this.Up() = 
    base.Create.Table("Users")
      .WithColumn("Id").AsInt32().PrimaryKey().Identity()
      .WithColumn("Username").AsString(12).Unique().NotNullable()
      .WithColumn("Email").AsString(254).Unique().NotNullable()
      .WithColumn("PasswordHash").AsString().NotNullable()
      .WithColumn("EmailVerificationCode").AsString().NotNullable()
      .WithColumn("IsEmailVerified").AsBoolean()
    |> ignore
    
  override this.Down() = 
    base.Delete.Table("Users") |> ignore