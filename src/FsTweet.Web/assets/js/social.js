$(function(){
  $("#follow").on('click', function(){
    var $this = $(this);
    var username = $this.data('username');
    $this.prop('disabled', true);
    $.ajax({
      url : "/follow",
      type: "post",
      data: JSON.stringify({username : username}),
      contentType: "application/json"
    }).done(function(){
      alert("successfully followed");
      $this.prop('disabled', false);
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });
  });
});