using BCLMSApi.Data;
using BCLMSApi.Models;
using System.Data;
using System.Transactions;

namespace BCLMSApi.Services;

public class ComplaintImageInput { public string FileName { get; set; } = ""; public string DataUrl { get; set; } = ""; }
public class ComplaintReplyInput { public string Message { get; set; } = ""; public List<ComplaintImageInput> Images { get; set; } = []; }
public class ComplaintConversationEntry {
    public int messageId { get; set; }
    public string authorName { get; set; } = "";
    public bool isOfficial { get; set; }
    public string message { get; set; } = "";
    public string? status { get; set; }
    public DateTime createdAtUtc { get; set; }
    public bool isHistorical { get; set; }
    public List<object> images { get; set; } = [];
}
public class ComplaintConversationService(Datalayer data)
{
    public static List<(string Name,string Type,byte[] Content)> ValidateImages(List<ComplaintImageInput>? images)
    {
        if (images is null) return [];
        if (images.Count > 3) throw new ArgumentException("Attach at most three images per message.");
        var result = new List<(string,string,byte[])>();
        foreach(var image in images) {
            if (image is null || string.IsNullOrEmpty(image.DataUrl) || image.DataUrl.Length > 2800000) throw new ArgumentException("Each image must be 2 MB or smaller.");
            var comma = image.DataUrl.IndexOf(',');
            if(comma < 0) throw new ArgumentException("Invalid image.");
            var header = image.DataUrl[..comma];
            var type = header switch { "data:image/jpeg;base64" => "image/jpeg", "data:image/png;base64" => "image/png", "data:image/webp;base64" => "image/webp", _ => throw new ArgumentException("Only JPEG, PNG and WebP images are allowed.") };
            byte[] bytes;
            try { bytes = Convert.FromBase64String(image.DataUrl[(comma+1)..]); } catch(FormatException) { throw new ArgumentException("Invalid image content."); }
            bool valid = type switch {
                "image/jpeg" => bytes.Length>3 && bytes[0]==255 && bytes[1]==216 && bytes[2]==255,
                "image/png" => bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}),
                _ => bytes.Length>12 && System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF" && System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP"
            };
            if(!valid || bytes.Length>2*1024*1024) throw new ArgumentException("Invalid image or image exceeds 2 MB.");
            var name=Path.GetFileName(image.FileName ?? "image");
            if(name.Length>260) throw new ArgumentException("Image filename is too long.");
            result.Add((name,type,bytes));
        }
        return result;
    }

    private async Task CheckAccess(UserTokenPayload user,int complaintId) {
        await using var cmd=data.CreateTextCommand("SELECT UserId FROM dbo.Complaints WHERE ComplaintId=@Id");
        cmd.Parameters.AddWithValue("@Id",complaintId); await cmd.Connection!.OpenAsync();
        var owner=await cmd.ExecuteScalarAsync();
        if(owner is null || (user.IsCustomer && (int)owner!=user.UserId)) throw new UnauthorizedAccessException("Complaint is not accessible.");
    }
    public async Task<object> Get(UserTokenPayload user,int complaintId) {
        await CheckAccess(user,complaintId);
        await using var cmd=data.CreateTextCommand("SELECT MessageId,AuthorName,IsOfficial,Message,Status,CreatedAtUtc,IsHistorical FROM dbo.ComplaintMessages WHERE ComplaintId=@Id ORDER BY CreatedAtUtc,MessageId; SELECT i.MessageId,i.ImageId,i.FileName FROM dbo.ComplaintMessageImages i JOIN dbo.ComplaintMessages m ON m.MessageId=i.MessageId WHERE m.ComplaintId=@Id ORDER BY i.ImageId;");
        cmd.Parameters.AddWithValue("@Id",complaintId); await cmd.Connection!.OpenAsync();
        var messages=new List<ComplaintConversationEntry>();
        await using var reader=await cmd.ExecuteReaderAsync();
        while(await reader.ReadAsync()) {
            var id=reader.GetInt32(0);
            messages.Add(new ComplaintConversationEntry {messageId=id,authorName=reader.GetString(1),isOfficial=reader.GetBoolean(2),message=reader.GetString(3),status=reader.IsDBNull(4)?null:reader.GetString(4),createdAtUtc=DateTime.SpecifyKind(reader.GetDateTime(5),DateTimeKind.Utc),isHistorical=reader.GetBoolean(6)});
        }
        var byId=messages.ToDictionary(m=>m.messageId);
        await reader.NextResultAsync();
        while(await reader.ReadAsync())
            if(byId.TryGetValue(reader.GetInt32(0),out var entry)) entry.images.Add(new {imageId=reader.GetInt32(1),fileName=reader.GetString(2)});
        return messages;
    }
    public async Task AddInitialImages(int complaintId,List<ComplaintImageInput>? images) {
        var validated=ValidateImages(images); if(validated.Count==0)return;
        await using var cmd=data.CreateTextCommand("SELECT MessageId FROM dbo.ComplaintMessages WHERE ComplaintId=@Id AND LegacyKey='initial'");
        cmd.Parameters.AddWithValue("@Id",complaintId); await cmd.Connection!.OpenAsync();
        var id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await cmd.Connection.CloseAsync();
        await SaveImages(id,validated);
    }
    private async Task SaveImages(int id,List<(string Name,string Type,byte[] Content)> images) {
        foreach(var image in images) {
            await using var cmd=data.CreateTextCommand("INSERT dbo.ComplaintMessageImages(MessageId,FileName,ContentType,Content) VALUES(@Id,@Name,@Type,@Content)");
            cmd.Parameters.AddWithValue("@Id",id);cmd.Parameters.AddWithValue("@Name",image.Name);cmd.Parameters.AddWithValue("@Type",image.Type);cmd.Parameters.Add("@Content",SqlDbType.VarBinary,-1).Value=image.Content;
            await cmd.Connection!.OpenAsync(); await cmd.ExecuteNonQueryAsync();
        }
    }
    public async Task Reply(UserTokenPayload user,int complaintId,ComplaintReplyInput input) {
        var images=ValidateImages(input.Images);
        if(string.IsNullOrWhiteSpace(input.Message) && images.Count==0) throw new ArgumentException("Enter a reply or attach an image.");
        if((input.Message?.Length??0)>10000) throw new ArgumentException("Reply may not exceed 10,000 characters.");
        using var scope=new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await CheckAccess(user,complaintId);
        await using var cmd=data.CreateTextCommand("INSERT dbo.ComplaintMessages(ComplaintId,AuthorUserId,AuthorName,IsOfficial,Message) OUTPUT INSERTED.MessageId VALUES(@Id,@User,@Name,@Official,@Message)");
        cmd.Parameters.AddWithValue("@Id",complaintId);cmd.Parameters.AddWithValue("@User",user.UserId);cmd.Parameters.AddWithValue("@Name",user.DisplayName);cmd.Parameters.AddWithValue("@Official",!user.IsCustomer);cmd.Parameters.AddWithValue("@Message",input.Message?.Trim()??"");
        await cmd.Connection!.OpenAsync(); var id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
        // Close before opening another connection in the ambient transaction.
        await cmd.Connection.CloseAsync();
        await SaveImages(id,images);
        scope.Complete();
    }
    public async Task<(byte[] Content,string Type,string Name)> Image(UserTokenPayload user,int complaintId,int imageId) {
        await CheckAccess(user,complaintId);
        await using var cmd=data.CreateTextCommand("SELECT i.Content,i.ContentType,i.FileName FROM dbo.ComplaintMessageImages i JOIN dbo.ComplaintMessages m ON m.MessageId=i.MessageId WHERE m.ComplaintId=@Id AND i.ImageId=@Image");
        cmd.Parameters.AddWithValue("@Id",complaintId);cmd.Parameters.AddWithValue("@Image",imageId);await cmd.Connection!.OpenAsync();
        await using var reader=await cmd.ExecuteReaderAsync();
        if(!await reader.ReadAsync()) throw new KeyNotFoundException("Image not found.");
        return ((byte[])reader[0],reader.GetString(1),reader.GetString(2));
    }
}
