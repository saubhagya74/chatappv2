﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace app.Services
{
    [Authorize]
    public class VideoChatHub:Hub
    {
        public async Task SendOffer(string receiverId, string offer)
        {
            await Clients.User(receiverId).SendAsync("ReceiveOffer", Context.UserIdentifier, offer);
        }
        public async Task SendAnswer(string receiverId, string answer)
        {
            await Clients.User(receiverId).SendAsync("ReceiveAnswer", Context.UserIdentifier, answer);
        }
        public async Task SendIceCandidate(string receiverId,string candidate)
        {
            await Clients.User(receiverId).SendAsync("ReceiveIceCandidate",Context.UserIdentifier,candidate);
        }
        public async Task EndCall(string receiverId)
        {
            await Clients.User(receiverId).SendAsync("CallEnded");
        }
    }
}
