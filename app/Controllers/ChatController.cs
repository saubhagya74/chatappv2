using app.Data;
using app.DTO;
using app.Migrations;
using app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Claims;

namespace app.Controllers
{

    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController:ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SnowFlakeGen _idgen;
        private readonly IWebHostEnvironment _env;
        public ChatController(ApplicationDbContext context, SnowFlakeGen idgen, IWebHostEnvironment env)
        {
            _env = env;

            _context = context;
            _idgen = idgen;
        }
        [HttpGet("loadusers")]
        public async Task<ActionResult<object>> LoadUser()
        {
            string claimuserid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            long.TryParse(claimuserid, out long currentUserId);
            var users = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId) //messages involving current user
                .OrderByDescending(m => m.TimeStamp) //sort latest message first
                .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId) //pick the other user
                .Distinct() //only unique users
                .Take(20) //optional limit results
                .ToListAsync();
            var chatUsers = await _context.Users
                .Where(u => users.Contains(u.UserId))
                .Select(u => new
                {
                    UserId = u.UserId.ToString(),
                    UserName = u.UserName,
                    ProfilePicUrl = u.ProfilePicUrl,
                })
                .ToListAsync();

            return Ok(chatUsers);
        }
        //[HttpGet("loadmessage/{receiverId}")]
        //public async Task<ActionResult<List<MessageDto>>> GetMessages(string receiverId, [FromQuery] string? date)
        //{
        //    // 1. Parse current user ID from claims
        //    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    if (claimId == null || !long.TryParse(claimId, out long currentUserId))
        //        return Unauthorized();

        //    // 2. Parse receiver ID from route
        //    if (!long.TryParse(receiverId, out long receiverIdLong))
        //        return BadRequest("Invalid receiver ID");

        //    // 3. Parse and normalize the 'date' parameter as UTC
        //    if (!DateTime.TryParse(date, null,
        //        System.Globalization.DateTimeStyles.AssumeUniversal |
        //        System.Globalization.DateTimeStyles.AdjustToUniversal,
        //        out DateTime reqDate))
        //    {
        //        return BadRequest("Invalid time");
        //    }

        //    // 4. Query messages older than reqDate (UTC)
        //    var messages = await _context.Messages
        //        .Where(m =>
        //            ((m.SenderId == currentUserId && m.ReceiverId == receiverIdLong) ||
        //             (m.SenderId == receiverIdLong && m.ReceiverId == currentUserId)) &&
        //             m.TimeStamp < reqDate)
        //        .OrderByDescending(m => m.TimeStamp)
        //        .Take(15)
        //        .Select(m => new MessageDto
        //        {
        //            SenderId = m.SenderId.ToString(),
        //            ReceiverId = m.ReceiverId.ToString(),
        //            Content = m.Content,
        //            TimeStamp = m.TimeStamp // keep as UTC, no conversion here
        //        })
        //        .ToListAsync();

        //    // 5. Reverse so oldest first
        //    messages.Reverse();

        //    return Ok(messages);
        //}
        [HttpGet("loadmessage/{receiverId}")]
        public async Task<ActionResult<List<MessageDto>>> GetAllMessages(string receiverId)
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (claimId == null)
                return Unauthorized();

            if (!long.TryParse(receiverId, out var rid))
                return BadRequest("Invalid receiverId");

            if (!long.TryParse(claimId, out var currentUserId))
                return BadRequest("Invalid user identifier");
            var messagedto = await _context.Messages
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == rid) ||
                    (m.SenderId == rid && m.ReceiverId == currentUserId))
                .OrderBy(m => m.TimeStamp)
                .Select(m => new MessageDto
                {
                    SenderId = m.SenderId.ToString(),
                    ReceiverId = m.ReceiverId.ToString(),
                    Content = m.Content,
                    TimeStamp = m.TimeStamp
                })
                .ToListAsync();

            return Ok(messagedto);
        }

        [HttpGet("searchUser/{searchedName}")]
        
        public async Task<ActionResult<object>> SearchUser(string searchedName)
        {
            var currentUserName = User.FindFirst(ClaimTypes.Name)?.Value;
            if (searchedName == currentUserName)
                return BadRequest(new { message = "It's you" });

            var searcheduser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == searchedName);
            if (searcheduser == null)
                return BadRequest(new { message = "User Not Found" });

            var suser = new
            {
                searchedUserName = searcheduser.UserName,
                searchedUserId = searcheduser.UserId.ToString(),
                searchedNoOfFriends = searcheduser.NumOfFriends,
                searchedProfilePicUrl=searcheduser.ProfilePicUrl
            };

            return Ok(suser);
        }
        [HttpGet("seeprofile")]
        [Authorize]
        public async Task<ActionResult<object>> SeeProfile()
        {
            string claimuserid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claimuserid)) return Unauthorized();

            long.TryParse(claimuserid, out long userid);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userid);
            if (user == null) return BadRequest(new { message = "Unable to see profile" });

            var xuser = new
            {
                userName = user.UserName,
                userId = user.UserId.ToString(),
                numOfFriends = user.NumOfFriends,
                profilePicUrl = user.ProfilePicUrl
            };
           
           
            return Ok(xuser);
        }

        [HttpGet("sendrequest/{receiverid}")]
        [Authorize]
        public async Task<ActionResult<object>> SendRequest(string receiverId)
        {
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentuserid);
            var currentuser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentuserid);
            if (currentuser == null) return BadRequest(new { message = "Invalid Request" });

            long.TryParse(receiverId, out long receiverid);
            var receiver = await _context.Users.FirstOrDefaultAsync(u => u.UserId == receiverid);
            if (receiver == null) return BadRequest(new {message= "Receiver Doesnot Exist" });

            var checkreqexist = await _context.FriendRequests.FirstOrDefaultAsync(u =>
              (u.RequesterId == currentuser.UserId && u.RequestToId == receiver.UserId) ||
              (u.RequesterId == receiver.UserId && u.RequestToId == currentuser.UserId)
              );

            if (checkreqexist != null && checkreqexist.RequestStatus == "pending")
                return Ok(new {message="Already Sent"});

            var xreq = new FriendRequestEntity
            {
                RequestId = _idgen.GenerateId(),
                RequesterId = currentuser.UserId,
                RequestToId = receiver.UserId,
                RequestStatus = "pending",
                RequestTime = DateTime.UtcNow
            };
            _context.FriendRequests.Add(xreq);
            await _context.SaveChangesAsync();

            return Ok(new { xreq.RequesterId, xreq.RequestToId, xreq.RequestStatus, xreq.RequestTime });
        }

        [HttpGet("acceptordeclinerequest/{friendid}/{statuschange}")]
        [Authorize]
        public async Task<ActionResult<bool>> AcceptOrDeclineRequest(string friendId, bool statuschange)
        {
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentUserId);
            if (!long.TryParse(friendId, out long friendUserId))
                return BadRequest(new { message="Invalid friend ID" });

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
            var friendUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == friendUserId);

            if (currentUser == null || friendUser == null)
                return BadRequest(new { message="User(s) not found" });

            var friendRequest = await _context.FriendRequests.FirstOrDefaultAsync(u =>
                (u.RequesterId == currentUserId && u.RequestToId == friendUserId) ||
                (u.RequesterId == friendUserId && u.RequestToId == currentUserId));

            if (friendRequest == null)
                return BadRequest(new { message= "Friend request not found" });

            if (!statuschange)
            {
                friendRequest.RequestStatus = "declined";
                await _context.SaveChangesAsync();
                return Ok(true);
            }
            var isfriend = await _context.Friends.FirstOrDefaultAsync(u => u.FUserId1 == currentUserId && u.FUserId2 == friendUserId
            || u.FUserId2 == currentUserId && u.FUserId1 == friendUserId
            );
            if(isfriend!=null)
            {
                return BadRequest(new { message = "already accepted" });
            }
            //AcceptLogic
            friendRequest.RequestStatus = "accepted";

            currentUser.NumOfFriends++;
            friendUser.NumOfFriends++;

            _context.Friends.Add(new FriendEntity
            {
                FriendId = _idgen.GenerateId(),
                FUserId1 = friendRequest.RequesterId,
                FUserId2 = friendRequest.RequestToId,
                Status = "friend",
                FriendSince = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(true);
        }



        //[HttpGet("seenotification")]
        //[Authorize]
        //public async Task<ActionResult<List<NotificationDto>>> SeeNotification()
        //{
        //    long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentuserid);
        //    var currentuser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentuserid);
        //    if (currentuser == null) return BadRequest("invalud userid:unauthorized");
        //    var notification = await _context.FriendRequests.Where(u => u.RequestToId == currentuser.UserId || u.RequesterId == currentuser.UserId).ToListAsync();
        //    if (notification == null) return BadRequest(new { message="no notification" });

        //    var notificationdto = notification.Select(n => new NotificationDto//while working with loop
        //    {

        //        RequesterId = n.RequesterId.ToString(),
        //        RequestToId = n.RequestToId.ToString(),
        //        RequestTime = n.RequestTime,
        //        RequestStatus = n.RequestStatus
        //    }).ToList();
        //    return Ok(notificationdto);
        //}
        [HttpGet("seenotification")]
        [Authorize]
        public async Task<ActionResult<List<NotificationDto>>> SeeNotification()
        {
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentuserid);

            var currentuser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentuserid);
            if (currentuser == null)
                return BadRequest("Invalid userid: Unauthorized");

            var notifications = await _context.FriendRequests
                .Where(u => u.RequestToId == currentuserid || u.RequesterId == currentuserid)
                .ToListAsync();

            if (!notifications.Any())
                return BadRequest(new { message = "No notifications" });

            var userIds = notifications
                .SelectMany(n => new[] { n.RequesterId, n.RequestToId })
                .Distinct()
                .ToList();

            var users = await _context.Users
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => new { u.UserName, u.ProfilePicUrl });

            var notificationDtos = notifications.Select(n => new NotificationDto
            {
                RequesterId = n.RequesterId.ToString(),
                RequesterName = users.ContainsKey(n.RequesterId) ? users[n.RequesterId].UserName : "Unknown",
                RequesterPicUrl = users.ContainsKey(n.RequesterId) ? users[n.RequesterId].ProfilePicUrl : null,
                RequestToId = n.RequestToId.ToString(),
                RequestToName = users.ContainsKey(n.RequestToId) ? users[n.RequestToId].UserName : "Unknown",
                RequestToPicUrl = users.ContainsKey(n.RequestToId) ? users[n.RequestToId].ProfilePicUrl : null,
                RequestTime = n.RequestTime,
                RequestStatus = n.RequestStatus
            }).ToList();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(notificationDtos));

            return Ok(notificationDtos);
        }

        [HttpGet("loadfriends")]
        public async Task<ActionResult<List<object>>> loadfriends()
        {
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentuserid);
            var currentUserId = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentuserid);
            if (currentUserId == null)
                return BadRequest("Invalid userid: Unauthorized");
            // Get all friend relationships for the current user
            var friendLinks = await _context.Friends
                .Where(f => f.FUserId1 == currentuserid || f.FUserId2 == currentuserid)
                .ToListAsync();
            var friendIds = friendLinks
        .Select(f => f.FUserId1 == currentuserid ? f.FUserId2 : f.FUserId1)
        .Distinct()
        .ToList();
            // Get friend user details
            var friends = await _context.Users
                .Where(u => friendIds.Contains(u.UserId))
                .Select(u => new
                {
                    UserId = u.UserId.ToString(),
                    UserName = u.UserName,
                    ProfilePicUrl = u.ProfilePicUrl
                })
                .ToListAsync();
            return Ok(friends);
        }
        [HttpPost("user/{id}/profile-photo")]
        public async Task<IActionResult> UploadProfilePhoto(string id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Empty file");

            if (!long.TryParse(id, out long userId))
                return BadRequest("Invalid user ID");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound("User not found");

            var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "pfp"); // fallback if null
            if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

            var fileName = userId + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            user.ProfilePicUrl = $"{baseUrl}/pfp/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new { user.ProfilePicUrl });
        }

        [HttpPost("selfPost")]
        [Authorize]
        public async Task<ActionResult<object>> SelfPost([FromForm] IFormFile file, [FromForm] string postAbout)
        {

            if (file == null || file.Length == 0)
                return BadRequest("File is required.");

            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentuserid);
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentuserid);
            if (currentUser == null)
                return BadRequest("Invalid userid: Unauthorized");

            // Ensure uploads directory exists
            var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "posts");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Generate unique file name
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Construct post URL
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var postUrl = $"{baseUrl}/posts/{fileName}";

            // Create PostEntity
            var post = new PostEntity
            {
                PostId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // or use your own logic
                UserId = currentUser.UserId,
                PostUrl = postUrl,
                PostAt = DateTime.UtcNow,
                PostAbout = postAbout ?? string.Empty,
                Likes = 0 // Initialize likes to 0
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Post uploaded successfully.",
            });
        }
        [HttpGet("selfPosts")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<PostEntity>>> GetSelfPosts()
        {
            // Extract user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
                return Unauthorized("Invalid or missing user ID claim.");

            // Get posts from DB for this user
            var posts = await _context.Posts
                .Where(p => p.UserId == userId)
                .OrderBy(m => m.PostAt)
                .Select(p => new PostDto
                {
                    PostUrl = p.PostUrl,
                    PostAbout = p.PostAbout,
                    PostAt = p.PostAt,
                    Likes = p.Likes
                })
                .Take(10)
                .ToListAsync();

            return Ok(posts);
        }

       
        [HttpGet("loadfriendsposts")]
        [Authorize]
        public async Task<ActionResult<List<object>>> LoadFriendsPosts()
        {
            // Get current user ID from JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long currentUserId))
                return Unauthorized("Invalid or missing user ID claim.");

            // Verify user exists
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
            if (currentUser == null)
                return BadRequest("Invalid userid: Unauthorized");

            // Get IDs of friends
            var friendIds = await _context.Friends
                .Where(f => f.FUserId1 == currentUserId || f.FUserId2 == currentUserId)
                .Select(f => f.FUserId1 == currentUserId ? f.FUserId2 : f.FUserId1)
                .Distinct()
                .ToListAsync();

            // Get posts by friends
            var friendPosts = await _context.Posts
                .Where(p => friendIds.Contains(p.UserId))
                .OrderByDescending(p => p.PostAt)
                .Join(_context.Users, // Join to include basic user info
                    post => post.UserId,
                    user => user.UserId,
                    (post, user) => new
                    {
                        PostId = post.PostId,
                        PostUrl = post.PostUrl,
                        PostAbout = post.PostAbout,
                        PostAt = post.PostAt,
                        Likes = post.Likes,
                        UserId = user.UserId,
                        UserName = user.UserName,
                        ProfilePicUrl = user.ProfilePicUrl
                    })
                .Take(10)
                .ToListAsync();

            return Ok(friendPosts);
        }
        [HttpGet("like/{postid}")]
        public async Task<ActionResult<object>> Like(string postid)
        {
            if (!long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long currentUserId))
                return Unauthorized(new { message = "Invalid or missing user ID claim." });

            if (!long.TryParse(postid, out long postId))
                return BadRequest(new { message = "Invalid post ID." });

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostId == postId);
            if (post == null)
                return NotFound(new { message = "Post not found." });

            post.Likes++;
            await _context.SaveChangesAsync();

            return Ok(new { like = post.Likes });
        }
    }
}
