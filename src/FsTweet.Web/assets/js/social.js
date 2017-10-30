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
      alert("successfully followed");
      $this.attr('id', 'unfollow');
      $this.html('Following');
      $this.addClass('disabled');
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });
  });
});