using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
namespace BCLMSApi.Controllers;
[ApiController,Route("api/Complaints/{complaintId:int}/conversation")]
public class ComplaintConversationController(ComplaintConversationService service,IUserTokenService tokens):ControllerBase {
    [HttpGet] public Task<IActionResult> Get(int complaintId)=>Run(async user=>Ok(await service.Get(user,complaintId)));
    [HttpPost,RequestSizeLimit(9000000)] public Task<IActionResult> Reply(int complaintId,ComplaintReplyInput input)=>Run(async user=>{await service.Reply(user,complaintId,input);return Ok(new {message="Reply saved."});});
    [HttpGet("images/{imageId:int}")] public Task<IActionResult> Image(int complaintId,int imageId)=>Run(async user=>{var image=await service.Image(user,complaintId,imageId);return File(image.Content,image.Type,image.Name);});
    private async Task<IActionResult> Run(Func<BCLMSApi.Models.UserTokenPayload,Task<IActionResult>> action) {
        var user=tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if(user is null)return StatusCode(403,new {message="Login is required."});
        try{return await action(user);}catch(UnauthorizedAccessException e){return StatusCode(403,new {message=e.Message});}catch(ArgumentException e){return BadRequest(new {message=e.Message});}catch(KeyNotFoundException){return NotFound();}
    }
}
