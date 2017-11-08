$(function(){
  
  $("#follow").on('click', function(){
    var $this = $(this);
    var userId = $this.data('user-id');
    $.ajax({
      url : "/follow",
      type: "post",
      data: JSON.stringify({userId : userId}),
      contentType: "application/json"
    }).done(function(){
      $this.attr('id', 'unfollow');
      $this.html('Following');
      $this.addClass('disabled');
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });
  });

  var usersTemplate = `
    {{#users}}
      <div class="well user-card">
        <a href="/{{username}}">@{{username}}</a>
      </div>
    {{/users}}`;

  
  function renderUsers(data, $body, $count) {
    var htmlOutput = Mustache.render(usersTemplate, data);
    $body.html(htmlOutput);
    $count.html(data.users.length);
  }
  

  (function loadFollowers () {
    var url = "/" + fsTweet.user.id  + "/followers"
    $.getJSON(url, function(data){
      renderUsers(data, $("#followers"), $("#followersCount"))
    })
  })();

  (function loadFollowingUsers() {
    var url = "/" + fsTweet.user.id  + "/following"
    $.getJSON(url, function(data){
      renderUsers(data, $("#following"), $("#followingCount"))
    })
  })();

});